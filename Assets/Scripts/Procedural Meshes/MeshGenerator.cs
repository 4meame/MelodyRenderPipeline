using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MeshGenerator : MonoBehaviour {
    public enum Type {
        Quad,
        Plane,
        UVSphere,
        Cubesphere,
        Octasphere,
        Icosphere
    }
    public Type type;
    public int resolution;
    public bool showGizmo;
    Mesh mesh;
    Vector3[] vertices, normals;
    Vector4[] tangents;
    void OnEnable() {
        switch (type) {
            case Type.Quad:
                GenerateQuad();
                break;
            case Type.Plane:
                GeneratePlane(resolution);
                break;
            case Type.UVSphere:

                break;
            case Type.Cubesphere:

                break;
            case Type.Octasphere:

                break;
            case Type.Icosphere:

                break;
        }
    }

    void Update() {
        vertices = mesh.vertices;
        normals = mesh.normals;
        tangents = mesh.tangents;
    }

    void GenerateQuad() {
        mesh = new Mesh {
            name = "Procedural Quad"
        };
        mesh.vertices = new Vector3[] { Vector3.zero, Vector3.right, Vector3.up, new Vector3(1f, 1f) };
        //mesh.triangles = new int[] { 0, 1, 2 };
        //only visible when look the front face by default, we can turn this around by swaping the order of the second and third vertex indices
        mesh.triangles = new int[] { 0, 2, 1, 1, 2, 3 };
        mesh.normals = new Vector3[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        //tangents help shader construct the third axis
        mesh.tangents = new Vector4[] {
            new Vector4(1f, 0f, 0f, -1f),
            new Vector4(1f, 0f, 0f, -1f),
            new Vector4(1f, 0f, 0f, -1f),
            new Vector4(1f, 0f, 0f, -1f)
        };
        mesh.uv = new Vector2[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        GetComponent<MeshFilter>().mesh = mesh;
    }

    void GeneratePlane(int resolution) {
        mesh = new Mesh {
            name = "Procedural Plane"
        };
        int width = resolution;
        int height = resolution;
        MeshData meshData = new MeshData(width,height);
        int vertexIndex = 0;
        int triangleIndex = 0;
        for (int y = 0; y < height + 1; y++) {
            for (int x = 0; x < width + 1; x++) {
                meshData.vertices[vertexIndex] = new Vector3(width / -2 + x, 0, height / -2 + y);
                if (x < width && y < height) {
                    meshData.triangles[triangleIndex] = vertexIndex;
                    meshData.triangles[triangleIndex + 1] = vertexIndex + width + 1;
                    meshData.triangles[triangleIndex + 2] = vertexIndex + 1;
                    meshData.triangles[triangleIndex + 3] = vertexIndex + 1;
                    meshData.triangles[triangleIndex + 4] = vertexIndex + width + 1;
                    meshData.triangles[triangleIndex + 5] = vertexIndex + width + 2;
                    triangleIndex += 6;
                }
                meshData.uv0[vertexIndex] = new Vector3(x/(float)width, y/(float)height);
                vertexIndex++;
            }
        }
        mesh.vertices = meshData.vertices;
        mesh.triangles = meshData.triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        mesh.uv = meshData.uv0;
        GetComponent<MeshFilter>().mesh = mesh;
    }

    void OnDrawGizmos() {
        if (!showGizmo || mesh == null) {
            return;
        }
        for (int i = 0; i < vertices.Length; i++) {
            Vector3 position = transform.TransformPoint(vertices[i]);
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(position, 0.05f);
            Gizmos.color = Color.green;
            Gizmos.DrawRay(position, transform.TransformDirection(normals[i]) * 0.5f);
            Gizmos.color = Color.red;
            Gizmos.DrawRay(position, transform.TransformDirection(tangents[i]) * 0.5f);
        }
    }

    public class MeshData {
        public Vector3[] vertices;
        public int[] triangles;
        public Vector3[] normals;
        public Vector4[] tangents;
        public Vector2[] uv0;

        public MeshData(int meshWidth, int meshHeight) {
            vertices = new Vector3[(meshWidth +1) * (meshHeight+1)];
            //triagngles index
            triangles = new int[meshWidth * meshHeight * 6];
            normals = new Vector3[(meshWidth + 1) * (meshHeight + 1)];
            tangents = new Vector4[(meshWidth + 1) * (meshHeight + 1)];
            uv0 = new Vector2[(meshWidth + 1) * (meshHeight + 1)];
        }
    }
}
