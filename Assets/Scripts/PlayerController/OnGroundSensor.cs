using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OnGroundSensor : MonoBehaviour
{
    public CapsuleCollider capCol;
    public LayerMask layerMask;
    public float offset = 0.1f;
    Vector3 point1;
    Vector3 point2;
    float radius;

    void Awake() {
        radius = capCol.radius - 0.05f;
    }

    void FixedUpdate() {
        point1 = transform.position + transform.up * (radius - offset);
        point2 = transform.position + transform.up * (capCol.height - offset) - transform.up * radius;
        Collider[] colliders = Physics.OverlapCapsule(point1, point2, radius, layerMask);
        if (colliders.Length > 0)
        {
            SendMessageUpwards("IsGround");
        } else {
            SendMessageUpwards("IsNotGround");
        }
    }
}
