using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static System.Runtime.InteropServices.Marshal;
using Random = UnityEngine.Random;

public class GrassRenderer : MonoBehaviour {
    [Header("Grass")]
    public bool useMeshData = false;
    public Vector2 fieldSize;
    public float heightScale;
    //smaller the number, CPU needs more time, but GPU is faster
    public Vector2 chunkSize;
    public int seed;
    public float density;
    public float viewDistance;
    public float lodDsitance = 80;
    public ShadowCastingMode castShadows;
    public Mesh grassMesh;
    public Mesh lodGrassMesh;
    public Material grassMaterial;
    public Material lodGrassMaterial;
    //do culling and data remake
    public ComputeShader dataProcessing;
    public bool useFrustumCulling;
    public bool useOcclusionCulling;

    [Header("Wave")]
    public ComputeShader noiseShader;
    public float xPeriod;
    public float yPeriod;
    public float turbPower;
    public float turbSize;
    public float frequency;
    public float amplitude;
    public float speed;
    RenderTexture noiseTexture;

    Camera mainCamera;
    Mesh mesh;
    int subMeshIndex = 0;
    Plane[] planes = new Plane[6];
    Vector4[] frustumPlanes = new Vector4[6];
    float minX, minY, minZ, maxX, maxY, maxZ;
    Bounds fieldBounds, meshBounds;
    ComputeBuffer argsBuffer;
    uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
    ComputeBuffer lodsArgsBuffer;
    uint[] lodsArgs = new uint[5] { 0, 0, 0, 0, 0 };
    ComputeBuffer dataBuffer;
    ComputeBuffer IdBuffer;
    ComputeBuffer lodIdBuffer;

    int cachePositionCount = -1;
    int cacheInstancedCount = -1;
    int chunkCountX = -1;
    int chunkCountZ = -1;

    List<Vector3> allGrassPositions;
    List<Vector3> meshPositions;
    List<Vector3> proceduralPositions;
    GrassData[] allGrassData;
    List<int> visiableChunkID;
    List<GrassData>[] allChunksData;

    bool shouldBatchDispatch = true;
    bool debug = false;

    public struct GrassData {
        public Vector3 position;
        public int chunkID;
        public Vector2 worldCoord;
        //TODO: aabb data for accurate culling
        public Vector3 boundsMin;
        public Vector3 boundsMax;
    }

    void Start() {
        allGrassPositions = new List<Vector3>();
        meshPositions = new List<Vector3>();
        proceduralPositions = new List<Vector3>();
        allGrassData = new GrassData[1];
        visiableChunkID = new List<int>();
        mainCamera = Camera.main;
        fieldBounds = new Bounds();
        mesh = transform.GetComponent<MeshFilter>().sharedMesh;
        UpdateGrassPosition();

        if (debug) {
            Debug.Log(minX + "," + maxX + "," + minY + "," + minY + "," + minZ + "," + maxZ);
        }
    }

    void Update() {
        UpdateGrassPosition();
        UpdateBuffer();
        Render();
    }

    void OnDrawGizmos() {
        if (debug) {
            for (int i = 0; i < allChunksData.Length; i++) {
                //create per chunk bounds
                Vector3 center = new Vector3(i % chunkCountX + 0.5f, (minY + maxY) / 2, Mathf.CeilToInt(i / chunkCountX) + 0.5f);
                center.x = Mathf.Lerp(minX, maxX, center.x / chunkCountX);
                center.z = Mathf.Lerp(minZ, maxZ, center.z / chunkCountZ);
                Vector3 size = new Vector3(Mathf.Abs(maxX - minX) / chunkCountX, maxY - minY, Mathf.Abs(maxZ - minZ) / chunkCountZ);
                Gizmos.DrawWireCube(center, size);
            }

            for (int i = 0; i < GetGrassCount(); i++) {
                CalculateMeshAABB(allGrassPositions[i]);
                Gizmos.color = Color.black;
                Gizmos.DrawWireCube(meshBounds.center, meshBounds.size);
            }
        }
    }

    void OnDisable() {
        cachePositionCount = -1;
        cacheInstancedCount = -1;

        if (dataBuffer != null) {
            dataBuffer.Release();
        }
        if (IdBuffer != null) {
            IdBuffer.Release();
        }
        if (lodIdBuffer != null) {
            lodIdBuffer.Release();
        }
        if (argsBuffer != null) {
            argsBuffer.Release();
        }
        if (lodsArgsBuffer != null) {
            lodsArgsBuffer.Release();
        }

        if (noiseTexture != null) {
            noiseTexture.Release();
        }
    }

    void UpdateBuffer() {
        if(cacheInstancedCount == allGrassData.Length) {
            return;
        }

        if (allChunksData != null) {
            foreach (var c in allChunksData) {
                c.Clear();
            }
        }
        //group all grass data into chunks
        GetChunksData();

        //flatten 2d array into 1d buffer
        allGrassData = new GrassData[allGrassPositions.Count];
        int index = 0;
        for (int i = 0; i < allChunksData.Length; i++) {
            for (int j = 0; j < allChunksData[i].Count; j++) {
                allGrassData[index] = allChunksData[i][j];
                index++;
            }
        }

        if (dataBuffer != null) {
            dataBuffer.Release();
        }
        dataBuffer = new ComputeBuffer(allGrassData.Length, SizeOf(typeof(GrassData)));
        dataBuffer.SetData(allGrassData);
        dataProcessing.SetBuffer(0, "_GrassData", dataBuffer);
        grassMaterial.SetBuffer("_GrassData", dataBuffer);
        lodGrassMaterial.SetBuffer("_GrassData", dataBuffer);

        if (IdBuffer != null) {
            IdBuffer.Release();
        }
        IdBuffer = new ComputeBuffer(allGrassData.Length, sizeof(uint), ComputeBufferType.Append);
        IdBuffer.SetCounterValue(0);
        dataProcessing.SetBuffer(0, "_IdOfVisibleGrass", IdBuffer);
        grassMaterial.SetBuffer("_IdOfVisibleGrass", IdBuffer);
        if (lodIdBuffer != null) {
            lodIdBuffer.Release();
        }
        lodIdBuffer = new ComputeBuffer(allGrassData.Length, sizeof(uint), ComputeBufferType.Append);
        lodIdBuffer.SetCounterValue(0);
        dataProcessing.SetBuffer(0, "_IdOfLodGrass", lodIdBuffer);
        lodGrassMaterial.SetBuffer("_IdOfLodGrass", lodIdBuffer);

        //indirect args
        if (argsBuffer != null) {
            argsBuffer.Release();
        }
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        args[0] = (uint)GetGrassMeshCache().GetIndexCount(subMeshIndex);
        //args[1] = (uint)allGrassData.Length;
        args[2] = (uint)GetGrassMeshCache().GetIndexStart(subMeshIndex);
        args[3] = (uint)GetGrassMeshCache().GetBaseVertex(subMeshIndex);
        argsBuffer.SetData(args);
        if (lodsArgsBuffer != null) {
            lodsArgsBuffer.Release();
        }
        lodsArgsBuffer = new ComputeBuffer(1, lodsArgs.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        lodsArgs[0] = (uint)GetLodGrassMeshCache().GetIndexCount(subMeshIndex);
        //lodsArgs[1] = (uint)allGrassData.Length;
        lodsArgs[2] = (uint)GetLodGrassMeshCache().GetIndexStart(subMeshIndex);
        lodsArgs[3] = (uint)GetLodGrassMeshCache().GetBaseVertex(subMeshIndex);
        lodsArgsBuffer.SetData(lodsArgs);

        cacheInstancedCount = allGrassData.Length;
    }

    //draw grass mesh instances indirect
    void Render() {
        DoGrassCulling();

        GenerateWaveTexure();

        grassMaterial.SetFloat("useLod", 0.0f);
        Graphics.DrawMeshInstancedIndirect(GetGrassMeshCache(), subMeshIndex, grassMaterial, fieldBounds, argsBuffer, 0, null, castShadows);

        lodGrassMaterial.SetFloat("useLod", 1.0f);
        Graphics.DrawMeshInstancedIndirect(GetLodGrassMeshCache(), subMeshIndex, lodGrassMaterial, fieldBounds, lodsArgsBuffer, 0, null, ShadowCastingMode.Off);

    }

    //Init grass position by mesh vertices, for example, but we generate these in procedural to get chunks
    void UpdateGrassPosition() {
        //we don't have to update position every frame.
        if (cachePositionCount == GetGrassCount()) {
            return;
        }
        //define grass worldspace position
        allGrassPositions.Clear();
        InitPositionData();
        for (int i = 0; i < GetGrassCount(); i++) {
            Vector3 position = GetGrassPosition(i);
            position += transform.position;
            allGrassPositions.Add(position);
        }
        if (debug) {
            Debug.Log("all grass count:" + allGrassPositions.Count);
        }
        CalcualteBounds(allGrassPositions);
        cachePositionCount = allGrassPositions.Count;
    }

    void InitPositionData() {
        if (useMeshData) {
            meshPositions.Clear();
            for (int i = 0; i < mesh.vertexCount; i++) {
                //TODO: Write a tessellation shader
                meshPositions.Add(mesh.vertices[i]);
            }
        } else {
            proceduralPositions.Clear();
            List<Vector2> samplingPoints = PoissonDiscSampling.GeneratePoints(1 / density, fieldSize);
            //TODO: Sample height map here to give y dimension coord
            Random.InitState(seed);
            Vector2 origin = new Vector2(Random.Range(0, 1000), Random.Range(0, 1000));
            Vector2 scale = new Vector2(Random.Range(2, 2.5f), Random.Range(2, 2.5f));
            for (int i = 0; i < samplingPoints.Count; i++) {
                float coordX = samplingPoints[i].x / fieldSize.x * scale.x + origin.x;
                float coordY = samplingPoints[i].y / fieldSize.y * scale.y + origin.y;
                float height = Mathf.PerlinNoise(coordX, coordY);
                proceduralPositions.Add(new Vector3(samplingPoints[i].x, samplingPoints[i].y, height * heightScale));
            }
        }
    }

    Mesh GetGrassMeshCache() {
        if (!grassMesh) {
            //if not exist, create mesh procedurally
            grassMesh = new Mesh();
            Vector3[] verts = new Vector3[3];
            verts[0] = new Vector3(-1.0f, 0);
            verts[1] = new Vector3(+1.0f, 0);
            verts[2] = new Vector3(-0.0f, 1.732f);
            int[] trinagles = new int[3] { 2, 1, 0, };
            grassMesh.SetVertices(verts);
            grassMesh.SetTriangles(trinagles, 0);
            grassMesh.uv = new Vector2[] { new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(0.5f, 1.0f) };
            grassMesh.RecalculateNormals();
        }
        return grassMesh;
    }

    Mesh GetLodGrassMeshCache() {
        if (!lodGrassMesh) {
            //if not exist, create mesh procedurally
            lodGrassMesh = new Mesh();
            Vector3[] verts = new Vector3[3];
            verts[0] = new Vector3(-1.0f, 0);
            verts[1] = new Vector3(+1.0f, 0);
            verts[2] = new Vector3(-0.0f, 1.732f);
            int[] trinagles = new int[3] { 2, 1, 0, };
            lodGrassMesh.SetVertices(verts);
            lodGrassMesh.SetTriangles(trinagles, 0);
            lodGrassMesh.uv = new Vector2[] { new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(0.5f, 1.0f) };
            lodGrassMesh.RecalculateNormals();
        }
        return lodGrassMesh;
    }

    void GenerateWaveTexure() {
        if (noiseTexture == null) {
            noiseTexture = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            noiseTexture.enableRandomWrite = true;
            noiseTexture.useMipMap = true;
            noiseTexture.autoGenerateMips = true;
            noiseTexture.Create();
        }
        noiseShader.SetTexture(0, "_WaveNoise", noiseTexture);
        noiseShader.SetFloat("_XPeriod", xPeriod);
        noiseShader.SetFloat("_YPeriod", yPeriod);
        noiseShader.SetFloat("_TurbPower", turbPower);
        noiseShader.SetFloat("_TurbSize", turbSize);
        noiseShader.SetFloat("_Time", Time.time * speed);
        noiseShader.SetFloat("_Frequency", frequency);
        noiseShader.SetFloat("_Amplitude", amplitude);
        noiseShader.Dispatch(0, 64, 64, 1);

        grassMaterial.SetTexture("_WaveNoise", noiseTexture);
        lodGrassMaterial.SetTexture("_WaveNoise", noiseTexture);
    }

    int GetGrassCount() {
        int count;
        if (useMeshData) {
            count = meshPositions.Count;
        } else  {
            count = proceduralPositions.Count;
        }
        return count;
    }

    Vector3 GetGrassPosition(int index) {
        Vector3 pos = Vector3.zero;
        if (useMeshData) {
            pos = meshPositions[index];
        } else {
            pos.x = proceduralPositions[index].x;
            pos.z = proceduralPositions[index].y;
            pos.y = proceduralPositions[index].z;
        }
        return pos;
    }

    //find all instances in the list to get min&max bound
    void CalcualteBounds(List<Vector3> positions) {
        minX = float.MaxValue;
        minY = float.MaxValue;
        minZ = float.MaxValue;
        maxX = float.MinValue;
        maxY = float.MinValue;
        maxZ = float.MinValue;
        for (int i = 0; i < positions.Count; i++) {
            Vector3 target = positions[i];
            minX = Mathf.Min(target.x, minX);
            minY = Mathf.Min(target.y, minY);
            minZ = Mathf.Min(target.z, minZ);
            maxX = Mathf.Max(target.x, maxX);
            maxY = Mathf.Max(target.y, maxY);
            maxZ = Mathf.Max(target.z, maxZ);
        }
        //if camera frustum is not overlapping this bound, DrawMeshInstancedIndirect will not even render
        fieldBounds.SetMinMax(new Vector3(minX, minY - 10, minZ), new Vector3(maxX, maxY + 20, maxZ));
    }

    //find instanced mesh aabb
    void CalculateMeshAABB(Vector3 worldPos) {
        Mesh mesh = GetGrassMeshCache();
        Vector3 meshMin, meshMax;
        meshMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        meshMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        float height = grassMaterial.GetFloat("_Height");
        float width = grassMaterial.GetFloat("_Width");
        for (int i = 0; i < mesh.vertexCount; i++) {
            Vector3 target = mesh.vertices[i];
            target.y *= height;
            target.x *= width;
            //give a z division offset
            target = Quaternion.Euler(0, 45, 0) * target;
            target += worldPos;
            meshMin.x = Mathf.Min(target.x, meshMin.x);
            meshMin.y = Mathf.Min(target.y, meshMin.y);
            meshMin.z = Mathf.Min(target.z, meshMin.z);
            meshMax.x = Mathf.Max(target.x, meshMax.x);
            meshMax.y = Mathf.Max(target.y, meshMax.y);
            meshMax.z = Mathf.Max(target.z, meshMax.z);
        }
        meshBounds.SetMinMax(meshMin, meshMax);
    }

    //decide chunk count by current min&max and chunkSize
    void GetChunksData() {
        chunkCountX = Mathf.CeilToInt((maxX - minX) / chunkSize.x);
        chunkCountZ = Mathf.CeilToInt((maxZ - minZ) / chunkSize.y);
        allChunksData = new List<GrassData>[chunkCountX * chunkCountZ];
        for (int i = 0; i < allChunksData.Length; i++) {
            allChunksData[i] = new List<GrassData>();
        }
        if (debug) {
            Debug.Log("all chunks count:" + allChunksData.Length);
        }
        for (int i = 0; i < allGrassPositions.Count; i++) {
            Vector3 pos = allGrassPositions[i];
            //find chunkID
            int xID = Mathf.Min(chunkCountX - 1, Mathf.FloorToInt(Mathf.InverseLerp(minX, maxX, pos.x) * chunkCountX)); //use min to force within 0~[cellCountX-1]  
            int zID = Mathf.Min(chunkCountZ - 1, Mathf.FloorToInt(Mathf.InverseLerp(minZ, maxZ, pos.z) * chunkCountZ)); //use min to force within 0~[cellCountZ-1]
            int id = xID + zID * chunkCountX;
            GrassData data = new GrassData {
                position = pos,
                chunkID = id
            };
            CalculateMeshAABB(pos);
            data.boundsMin = meshBounds.min;
            data.boundsMax = meshBounds.max;
            allChunksData[id].Add(data);
        }   
    }

    //rough quick frustum culling in CPU first to filter unvisible chunk, then filter in GPU by compute shader
    void DoGrassCulling() {
        //apply grass view distance
        float farClipPlane = mainCamera.farClipPlane;
        float fov = mainCamera.fieldOfView;
        mainCamera.farClipPlane = viewDistance;
        mainCamera.fieldOfView *= 1.01f;
        //Ordering: [0] = Left, [1] = Right, [2] = Down, [3] = Up, [4] = Near, [5] = Far
        GeometryUtility.CalculateFrustumPlanes(mainCamera, planes);
        mainCamera.farClipPlane = farClipPlane;
        mainCamera.fieldOfView = fov;
        visiableChunkID.Clear();
        for (int i = 0; i < allChunksData.Length; i++) {
            //create per chunk bounds
            Vector3 center = new Vector3(i % chunkCountX + 0.5f, (minY + maxY) / 2, Mathf.CeilToInt(i / chunkCountX) + 0.5f);
            center.x = Mathf.Lerp(minX, maxX, center.x / chunkCountX);
            center.z = Mathf.Lerp(minZ, maxZ, center.z / chunkCountZ);
            Vector3 size = new Vector3(Mathf.Abs(maxX - minX) / chunkCountX, maxY - minY, Mathf.Abs(maxZ - minZ) / chunkCountZ);
            Bounds bounds = new Bounds(center, size);
            if (GeometryUtility.TestPlanesAABB(planes, bounds)) {
                visiableChunkID.Add(i);
            }
        }

        //loop though only visible cells, each visible cell dispatch GPU culling job once, at the end compute shader will fill all visible instance into IdBuffer
        Matrix4x4 v = mainCamera.worldToCameraMatrix;
        //Matrix4x4 p = mainCamera.projectionMatrix;
        Matrix4x4 p = GL.GetGPUProjectionMatrix(mainCamera.projectionMatrix, false);
        Matrix4x4 vp = p * v;
        //init
        IdBuffer.SetCounterValue(0);
        lodIdBuffer.SetCounterValue(0);
        int dispatchCount = 0;
        dataProcessing.SetMatrix("_VPMatrix", vp);
        dataProcessing.SetFloat("_MaxDrawDistance", viewDistance);
        dataProcessing.SetFloat("_LodDistance", lodDsitance);
        dataProcessing.SetFloat("_MinX", minX);
        dataProcessing.SetFloat("_MinZ", minZ);
        dataProcessing.SetFloat("_MaxX", maxX);
        dataProcessing.SetFloat("_MaxZ", maxZ);
        for (int i = 0; i < frustumPlanes.Length; i++) {
            var normal = -planes[i].normal;
            frustumPlanes[i] = new Vector4(normal.x, normal.y, normal.z, -planes[i].distance);
        }
        dataProcessing.SetVectorArray("_FrustumPlanes", frustumPlanes);
        dataProcessing.SetBool("FrustumCulling", useFrustumCulling);
        dataProcessing.SetBool("OcclusionCulling", useOcclusionCulling);
        //dispatch culling compute per chunk
        for (int i = 0; i < visiableChunkID.Count; i++) {
            int currentChunkId = visiableChunkID[i];
            int memoryOffset = 0;
            for (int j = 0; j < currentChunkId; j++) {
                memoryOffset += allChunksData[j].Count;
            }
            //offset current chunk grass ID to global ID of all grass array
            dataProcessing.SetInt("_MemoryOffset", memoryOffset);
            int jobLength = allChunksData[currentChunkId].Count;
            //batch n dispatchs into 1 dispatch, if memory is continuous in allInstancesPosWSBuffer
            if (shouldBatchDispatch) {
                while ((i < visiableChunkID.Count - 1) && //test this first to avoid out of bound access to visibleChunkIDList
                        (visiableChunkID[i + 1] == visiableChunkID[i] + 1)) {
                    //if memory is continuous, append them together into the same dispatch call
                    jobLength += allChunksData[visiableChunkID[i + 1]].Count;
                    i++;
                }
            }

            dataProcessing.Dispatch(0, Mathf.CeilToInt(jobLength / 64f), 1, 1);
            dispatchCount++;
        }

        //GPU per instance culling finished, copy visible count to argsBuffer, to setup DrawMeshInstancedIndirect's draw amount 
        ComputeBuffer.CopyCount(IdBuffer, argsBuffer, 4);
        ComputeBuffer.CopyCount(lodIdBuffer, lodsArgsBuffer, 4);
    }

}
