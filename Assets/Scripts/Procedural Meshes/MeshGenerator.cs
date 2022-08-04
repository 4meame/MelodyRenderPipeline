using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MeshGenerator : MonoBehaviour {
    void OnEnable() {
        var mesh = new Mesh {
            name = "Procedural Mesh"
        };
        mesh.vertices = new Vector3[] { Vector3.zero, Vector3.right, Vector3.up, new Vector3(1f, 1f)};
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

}
