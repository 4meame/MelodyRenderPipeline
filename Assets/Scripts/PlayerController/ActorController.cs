using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ActorController : MonoBehaviour {
    public GameObject model;
    public float walkSpeed = 1.4f;
    public float runMultiply = 2.0f;
    [Range(0, 1.0f)]
    public float turnSpeedRate = 0.2f;
    public float jumpVelocity = 1.0f;
    PlayerInput pi;
    Animator anim;
    Rigidbody rigid;
    Vector3 planarVec;
    Vector3 thrustVec;
    public bool lockPlanar;

    void Awake() {
        pi = GetComponent<PlayerInput>();
        anim = model.GetComponent<Animator>();
        rigid = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update() {
        //make translation softer
        anim.SetFloat("forward", pi.Dmag * Mathf.Lerp(anim.GetFloat("forward"), (pi.run ? 2.0f : 1.0f), 0.5f));
        if (pi.jump) {
            anim.SetTrigger("jump");
        }
        if (pi.Dmag > 0.05f) {
            //make translation softer
            model.transform.forward = Vector3.Slerp(model.transform.forward, pi.DVec, turnSpeedRate);
        }
        if (lockPlanar == false) {
            planarVec = pi.DVec * pi.Dmag * walkSpeed * (pi.run ? runMultiply : 1.0f);
        }
    }
    void FixedUpdate() {
        rigid.velocity = new Vector3(planarVec.x, rigid.velocity.y, planarVec.z) + thrustVec;
        thrustVec = Vector3.zero;
    }

    public void OnJumpEnter() {
        pi.inputEnable = false;
        lockPlanar = true;
        thrustVec = new Vector3(0, jumpVelocity, 0);
    }

    public void OnJumpExit() {
        pi.inputEnable = true;
        lockPlanar = false;
    }
}
