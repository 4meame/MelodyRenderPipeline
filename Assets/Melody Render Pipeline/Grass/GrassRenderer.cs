using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static System.Runtime.InteropServices.Marshal;

public class GrassRenderer : MonoBehaviour {
    [Header("Grass")]
    public bool useMeshData = false;
    public Vector2 fieldSize;
    public float heightScale;
    //smaller the number, CPU needs more time, but GPU is faster
    public Vector2 chunkSize;
    public float density;
    public float viewDistance;
    public ShadowCastingMode castShadows;
    public Mesh grassMesh;
    public Material grassMaterial;
    //do culling and data remake
    public ComputeShader dataProcessing;

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
    Plane[] frustumPlanes;
    float minX, minY, minZ, maxX, maxY, maxZ;
    Bounds bounds;
    ComputeBuffer argsBuffer;
    uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
    ComputeBuffer dataBuffer;
    ComputeBuffer IdBuffer;

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
    }

    void Start() {
        allGrassPositions = new List<Vector3>();
        meshPositions = new List<Vector3>();
        proceduralPositions = new List<Vector3>();
        allGrassData = new GrassData[1];
        visiableChunkID = new List<int>();
        frustumPlanes = new Plane[6];
        mainCamera = Camera.main;
        bounds = new Bounds();
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
        if (argsBuffer != null) {
            argsBuffer.Release();
        }

        if(noiseTexture != null) {
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

        if (IdBuffer != null) {
            IdBuffer.Release();
        }
        IdBuffer = new ComputeBuffer(allGrassData.Length, sizeof(uint), ComputeBufferType.Append);
        IdBuffer.SetCounterValue(0);
        dataProcessing.SetBuffer(0, "_IdOfVisibleGrass", IdBuffer);
        grassMaterial.SetBuffer("_IdOfVisibleGrass", IdBuffer);

        //indirect args
        if (argsBuffer != null) {
            argsBuffer.Release();
        }
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        args[0] = (uint)GetGrassMeshCache().GetIndexCount(subMeshIndex);
        args[1] = (uint)allGrassData.Length;
        args[2] = (uint)GetGrassMeshCache().GetIndexStart(subMeshIndex);
        args[3] = (uint)GetGrassMeshCache().GetBaseVertex(subMeshIndex);
        argsBuffer.SetData(args);

        cacheInstancedCount = allGrassData.Length;
    }

    //draw grass mesh instances indirect
    void Render() {
        DoGrassCulling();

        GenerateWaveTexure();

        Graphics.DrawMeshInstancedIndirect(GetGrassMeshCache(), subMeshIndex, grassMaterial, bounds, argsBuffer, 0, null, castShadows);
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
            Vector2 origin = new Vector2(UnityEngine.Random.Range(0, 1000), UnityEngine.Random.Range(0, 1000));
            Vector2 scale = new Vector2(UnityEngine.Random.Range(2, 2.5f), UnityEngine.Random.Range(2, 2.5f));
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
            verts[2] = new Vector3(-0.0f, 1);
            int[] trinagles = new int[3] { 2, 1, 0, };
            grassMesh.SetVertices(verts);
            grassMesh.SetTriangles(trinagles, 0);
            grassMesh.uv = new Vector2[] { new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(0.5f, 1.0f) };
        }
        return grassMesh;
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
        bounds.SetMinMax(new Vector3(minX, minY - 10, minZ), new Vector3(maxX, maxY + 20, maxZ));
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
            GrassData data = new GrassData();
            data.position = pos;
            data.chunkID = id;
            allChunksData[id].Add(data);
        }   
    }

    //rough quick frustum culling in CPU first to filter unvisible chunk, then filter in GPU by compute shader
    void DoGrassCulling() {
        //apply grass view distance
        float farClipPlane = mainCamera.farClipPlane;
        mainCamera.farClipPlane = viewDistance;
        //Ordering: [0] = Left, [1] = Right, [2] = Down, [3] = Up, [4] = Near, [5] = Far
        GeometryUtility.CalculateFrustumPlanes(mainCamera, frustumPlanes);
        mainCamera.farClipPlane = farClipPlane;

        visiableChunkID.Clear();
        for (int i = 0; i < allChunksData.Length; i++) {
            //create per chunk bounds
            Vector3 center = new Vector3(i % chunkCountX + 0.5f, (minY + maxY) / 2, Mathf.CeilToInt(i / chunkCountX) + 0.5f);
            center.x = Mathf.Lerp(minX, maxX, center.x / chunkCountX);
            center.z = Mathf.Lerp(minZ, maxZ, center.z / chunkCountZ);
            Vector3 size = new Vector3(Mathf.Abs(maxX - minX) / chunkCountX, maxY - minY, Mathf.Abs(maxZ - minZ) / chunkCountZ);
            Bounds bounds = new Bounds(center, size);
            if (GeometryUtility.TestPlanesAABB(frustumPlanes, bounds)) {
                visiableChunkID.Add(i);
            }
        }

        //loop though only visible cells, each visible cell dispatch GPU culling job once, at the end compute shader will fill all visible instance into IdBuffer
        Matrix4x4 v = mainCamera.worldToCameraMatrix;
        Matrix4x4 p = mainCamera.projectionMatrix;
        Matrix4x4 vp = p * v;
        //init
        IdBuffer.SetCounterValue(0);
        int dispatchCount = 0;
        dataProcessing.SetMatrix("_VPMatrix", vp);
        dataProcessing.SetFloat("_MaxDrawDistance", viewDistance);
        dataProcessing.SetFloat("_MinX", minX);
        dataProcessing.SetFloat("_MinZ", minZ);
        dataProcessing.SetFloat("_MaxX", maxX);
        dataProcessing.SetFloat("_MaxZ", maxZ);
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
    }
}
