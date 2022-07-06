using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateAround : MonoBehaviour
{
    public GameObject center;
    public Light pointLight;
    public float rotateSpeed;
    void Update()
    {
        pointLight.transform.RotateAround(center.transform.position, center.transform.up, rotateSpeed * Time.deltaTime);
    }
}
