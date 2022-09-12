using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInput : MonoBehaviour {
    //varible
    [Header("Key Settings")]
    public string keyUp = "w";
    public string keyDown = "s";
    public string keyLeft = "a";
    public string keyRight = "d";

    public string keyA;
    //public string keyB;
    //public string keyC;
    //public string keyD;

    [Header("Signal Settings")]
    public float Dup;
    public float Dright;
    public float Dmag;
    public Vector3 DVec;
    //1. pressing signal
    public bool run;
    //2. trigger once signal
    //3. double trigger

    [Header("Others")]
    public bool inputEnable = false;

    //private
    float targetDup;
    float targetDright;
    float velocityDup;
    float velocityDright;

    // Start is called before the first frame update
    void Start() {
        
    }

    // Update is called once per frame
    void Update() {
        targetDup = (Input.GetKey(keyUp) ? 1 : 0) - (Input.GetKey(keyDown) ? 1 : 0);
        targetDright = (Input.GetKey(keyRight) ? 1 : 0) - (Input.GetKey(keyLeft) ? 1 : 0);

        if(inputEnable == false)  {
            targetDup = 0;
            targetDright = 0;
        }

        Dright = Mathf.SmoothDamp(Dright, targetDright, ref velocityDright, 0.1f);
        Dup = Mathf.SmoothDamp(Dup, targetDup, ref velocityDup, 0.1f);

        Vector2 tempDAxis = SquareToCircle(new Vector2(Dright, Dup));
        float Dright2 = tempDAxis.x;
        float Dup2 = tempDAxis.y;


        Dmag = Mathf.Sqrt(Dup2 * Dup2 + Dright2 * Dright2);
        DVec = Dright2 * transform.right + Dup2 * transform.forward;

        run = Input.GetKey(keyA);
    }

    Vector2 SquareToCircle(Vector2 input) {
        Vector2 output = Vector2.zero;
        output.x = input.x * Mathf.Sqrt(1 - (input.y * input.y) / 2.0f);
        output.y = input.y * Mathf.Sqrt(1 - (input.x * input.x) / 2.0f);
        return output;
    }
}
