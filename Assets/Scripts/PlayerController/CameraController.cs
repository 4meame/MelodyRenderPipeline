using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public PlayerInput pi;
    public float horizontalSpeed;
    public float verticalSpeed;
    public float cameraDampValue;
    GameObject playerHandle;
    GameObject cameraHandle;
    float tempEulerX;
    GameObject model;
    Vector3 modelEuler;
    GameObject roleCamera;
    Vector3 cameraDampVelocity;
    
    void Awake()
    {
        cameraHandle = transform.parent.gameObject;
        playerHandle = cameraHandle.transform.parent.gameObject;
        tempEulerX = 0;
        model = playerHandle.GetComponent<ActorController>().model;
        roleCamera = Camera.main.gameObject;
    }

    void FixedUpdate()
    {
        modelEuler = model.transform.eulerAngles;
        playerHandle.transform.Rotate(Vector3.up, pi.Jright * horizontalSpeed * Time.fixedDeltaTime);
        tempEulerX -= pi.Jup * verticalSpeed * Time.deltaTime;
        tempEulerX = Mathf.Clamp(tempEulerX, -40, 30);
        cameraHandle.transform.localEulerAngles = new Vector3(tempEulerX, 0, 0);
        model.transform.eulerAngles = modelEuler;

        roleCamera.transform.position = Vector3.SmoothDamp(roleCamera.transform.position, transform.position, ref cameraDampVelocity, cameraDampValue);
        roleCamera.transform.eulerAngles = transform.eulerAngles;
    }
}
