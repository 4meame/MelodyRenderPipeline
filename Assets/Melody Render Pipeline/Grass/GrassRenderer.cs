using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static System.Runtime.InteropServices.Marshal;

public class GrassRenderer : MonoBehaviour {
    public bool useMeshData = false;
    public Vector2 fieldSize;
    //smaller the number, CPU needs more time, but GPU is faster
    public Vector2 chunkSize;
    public float density;
    public float viewDistance;
    public Mesh grassMesh;
    public Material grassMaterial;

    Camera mainCamera;
    Mesh mesh;
    int subMeshIndex = 0;
    Plane[] frustumPlanes;
    float minX, minZ, maxX, maxZ;
    ComputeBuffer argsBuffer;
    uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
    ComputeBuffer dataBuffer;
    ComputeBuffer IdBuffer;

    int instancedGrassCount = 0;
    int cachedPositionCount = -1;
    int cacheInstancedCount = 1;
    int chunkCountX = -1;
    int chunkCountZ = -1;

    List<Vector3> allGrassPositions;
    List<Vector3> meshPositions;
    List<Vector2> proceduralPositions;
    List<GrassData>[] allChunksData;
    List<int> visiableChunkID;
    List<GrassData> allGrassData;

    public struct GrassData {
        public Vector3 position;
        public int chunkID;
        public int visable;

        public GrassData(Vector3 pos, int id, int vis) {
            position= pos;
            chunkID = id;
            visable = vis;
        }
    }

    void Start() {
        allGrassPositions = new List<Vector3>();
        meshPositions = new List<Vector3>();
        proceduralPositions = new List<Vector2>();
        allGrassData = new List<GrassData>();
        visiableChunkID = new List<int>();
        frustumPlanes = new Plane[6];
        mainCamera = Camera.main;
        mesh = transform.GetComponent<MeshFilter>().mesh;
        UpdateGrassPosition();
    }

    void OnEnable() {
        //ensure submesh index is in range
        if (grassMesh != null) {
            subMeshIndex = Mathf.Clamp(subMeshIndex, 0, grassMesh.subMeshCount - 1);
        }
    }

    void Update() {
        UpdateGrassPosition();
        UpdateBuffer();
        Render();
    }

    void OnDrawGizmos() {
        Gizmos.DrawWireCube(transform.position, new Vector3(maxX - minX, 10.0f, maxZ - minZ));
    }

    void OnDisable() {
        cachedPositionCount = -1;
        cacheInstancedCount = -1;
    }

    void UpdateBuffer() {
        if(cacheInstancedCount == allGrassData.Count) {
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
        if (allGrassData != null) {
            allGrassData.Clear();
        }
        for (int i = 0; i < allChunksData.Length; i++) {
            for (int j = 0; j < allChunksData[i].Count; j++) {
                allGrassData.Add(allChunksData[i][j]);
            }
        }

        if (dataBuffer != null) {
            dataBuffer.Release();
        }
        dataBuffer = new ComputeBuffer(allGrassData.Count, SizeOf(typeof(GrassData)));
        dataBuffer.SetData(allGrassData);
        grassMaterial.SetBuffer("_GrassData", dataBuffer);
        Debug.Log(allGrassData.Count);
        //indirect args
        if (argsBuffer != null) {
            argsBuffer.Release();
        }
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        args[0] = (uint)GetGrassMeshCache().GetIndexCount(subMeshIndex);
        args[1] = (uint)allGrassData.Count;
        args[2] = (uint)GetGrassMeshCache().GetIndexStart(subMeshIndex);
        args[3] = (uint)GetGrassMeshCache().GetBaseVertex(subMeshIndex);
        argsBuffer.SetData(args);

        cacheInstancedCount = allGrassData.Count;
    }

    //draw grass mesh instances indirect
    void Render() {
        DoGrassCulling();

        //IdBuffer = new ComputeBuffer(visiableChunkID.Count, SizeOf(typeof(int)));
        //IdBuffer.SetData(visiableChunkID);
        //grassMaterial.SetBuffer("_GrassID", IdBuffer);

        Graphics.DrawMeshInstancedIndirect(GetGrassMeshCache(), subMeshIndex, grassMaterial, new Bounds(Vector3.zero, new Vector3(2000.0f, 2000.0f, 2000.0f)), argsBuffer);
    }

    //Init grass position by mesh vertices, for example, but we generate these in procedural to get chunks
    void UpdateGrassPosition() {
        //we don't have to update position every frame.
        if (cachedPositionCount == GetGrassCount()) {
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
        Debug.Log(allGrassPositions.Count);
        CalcualteBounds(allGrassPositions);
        cachedPositionCount = allGrassPositions.Count;
    }

    void InitPositionData() {
        if (useMeshData) {
            meshPositions.Clear();
            for (int i = 0; i < mesh.vertexCount; i++) {
                meshPositions.Add(mesh.vertices[i]);
            }
        } else {
            proceduralPositions.Clear();
            proceduralPositions = PoissonDiscSampling.GeneratePoints(1 / density, fieldSize);
        }
    }

    Mesh GetGrassMeshCache() {
        if (!grassMesh) {
            //if not exist, create mesh procedurally
            grassMesh = new Mesh();
            Vector3[] verts = new Vector3[3];
            verts[0] = new Vector3(-0.25f, 0);
            verts[1] = new Vector3(+0.25f, 0);
            verts[2] = new Vector3(-0.0f, 1);
            int[] trinagles = new int[3] { 2, 1, 0, };
            grassMesh.SetVertices(verts);
            grassMesh.SetTriangles(trinagles, 0);
        }
        return grassMesh;
    }

    int GetGrassCount() {
        if (useMeshData) {
            instancedGrassCount = meshPositions.Count;
        } else  {
            instancedGrassCount = proceduralPositions.Count;
        }
        return instancedGrassCount;
    }

    Vector3 GetGrassPosition(int index) {
        Vector3 pos = Vector3.zero;
        if (useMeshData) {
            pos = meshPositions[index];
        } else {
            pos.x = proceduralPositions[index].x;
            pos.z = proceduralPositions[index].y;
        }
        return pos;
    }

    //find all instances in the list to get min&max bound
    void CalcualteBounds(List<Vector3> positions) {
        minX = float.MaxValue;
        minZ = float.MaxValue;
        maxX = float.MinValue;
        maxZ = float.MinValue;
        for (int i = 0; i < positions.Count; i++) {
            Vector3 target = positions[i];
            minX = Mathf.Min(target.x, minX);
            minZ = Mathf.Min(target.z, minZ);
            maxX = Mathf.Max(target.x, maxX);
            maxZ = Mathf.Max(target.z, maxZ);
        }
    }

    //decide chunk count by current min&max and chunkSize
    void GetChunksData() {
        chunkCountX = Mathf.CeilToInt((maxX - minX) / chunkSize.x);
        chunkCountZ = Mathf.CeilToInt((maxZ - minZ) / chunkSize.y);
        allChunksData = new List<GrassData>[chunkCountX * chunkCountZ];
        for (int i = 0; i < allChunksData.Length; i++) {
            allChunksData[i] = new List<GrassData>();
        }

        for (int i = 0; i < allGrassPositions.Count; i++) {
            Vector3 pos = allGrassPositions[i];
            //find chunkID
            int xID = Mathf.Min(chunkCountX - 1, Mathf.FloorToInt(Mathf.InverseLerp(minX, maxX, pos.x) * chunkCountX)); //use min to force within 0~[cellCountX-1]  
            int zID = Mathf.Min(chunkCountZ - 1, Mathf.FloorToInt(Mathf.InverseLerp(minZ, maxZ, pos.z) * chunkCountZ)); //use min to force within 0~[cellCountZ-1]
            int id = xID + zID * chunkCountX;
            GrassData data = new GrassData(pos, id, 1);
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
            Vector3 center = new Vector3(i % chunkCountX + 0.5f, transform.position.y, Mathf.CeilToInt(i / chunkCountX) + 0.5f);
            center.x = Mathf.Lerp(minX, maxX, center.x / chunkCountX);
            center.z = Mathf.Lerp(minZ, maxZ, center.z / chunkCountZ);
            Vector3 size = new Vector3(Mathf.Abs(maxX - minX) / chunkCountX, 0, Mathf.Abs(maxZ - minZ) / chunkCountZ);
            Bounds bounds = new Bounds(center, size);
            if (GeometryUtility.TestPlanesAABB(frustumPlanes, bounds)) {
                visiableChunkID.Add(i);

                //for (int j = 0; j < allChunksData[i].Count; j++) {
                //    GrassData d = new GrassData(allChunksData[i][j].position, allChunksData[i][j].chunkID, 1);
                //    allChunksData[i][j] = d;
                //}

            }
        }


    }
}
