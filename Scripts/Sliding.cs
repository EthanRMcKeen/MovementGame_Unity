using System.Collections;
using System.Collections.Generic;
using System.Resources;
using UnityEngine;

public class Sliding : MonoBehaviour
{
    [Header("References")]
    public Transform orientation;
    public Transform playerObj;
    private Rigidbody rb;
    private PlayerScript ps;
    public PlayerCam cam;
    public CapsuleCollider env_col;
    CapsuleCollider atk_col;

    [Header("Sliding")]
    public float maxSliderTime;
    private float slideTimer;
    public float slideAcceleration = 50f;
    public float maxSlideSpeed = 50f;
    private float slideSpeed;
    private Vector3 slideDirection;

    public float slideYScale;
    private float startHeight;

    [Header("Input")]
    public KeyCode slideKey = KeyCode.LeftControl;
    public KeyCode sprintKey = KeyCode.LeftShift;

    private float startFOV;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        ps = GetComponent<PlayerScript>();
        atk_col = GetComponent<CapsuleCollider>();

        startHeight = env_col.height;

        ps.sliding = false;
        startFOV = cam.cam.fieldOfView;
    }

    private void Update()
    {

        if(Input.GetKeyDown(slideKey) && Input.GetKey(sprintKey))
        {
            StartSlide();
        }

        if (Input.GetKeyUp(slideKey))
        {
            if (ps.sliding)
                StopSlide();
        }

        if (ps.sliding && (!ps.grounded || (ps.OnSlope() && rb.linearVelocity.y < 0.1f)))
        {
            slideTimer = maxSliderTime;
        }
    }

    private void FixedUpdate()
    {
        if (ps.sliding)
            SlidingMovement();
    }

    public void StartSlide()
    {
        ps.sliding = true;

        Vector3 currentVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        slideSpeed = currentVel.magnitude;

        Vector3 inputDir = orientation.forward * Input.GetAxisRaw("Vertical") + orientation.right * Input.GetAxisRaw("Horizontal");
        if (slideSpeed > 0)
        {
            slideDirection = currentVel.normalized;
        }
        else if (inputDir.magnitude > 0)
        {
            slideDirection = inputDir.normalized;
        }
        else
        {
            slideDirection = orientation.forward;
        }

        float slideHeight = startHeight * slideYScale;
        float heightDiff = startHeight - slideHeight;
        env_col.height = slideHeight;
        env_col.center = new Vector3(env_col.center.x, -heightDiff / 2f, env_col.center.z);
        atk_col.height = slideHeight;
        atk_col.center = new Vector3(atk_col.center.x, -heightDiff / 2f, atk_col.center.z);


        slideTimer = maxSliderTime;

        //camera effects
        cam.DoFov(startFOV + 20f);
    }

    private void SlidingMovement()
    {
        Vector3 inputDir =
            orientation.forward * Input.GetAxisRaw("Vertical") +
            orientation.right * Input.GetAxisRaw("Horizontal");

        if (inputDir.magnitude > 0.1f)
        {
            // Smoothly interpolate slide direction towards input direction
            slideDirection = Vector3.Lerp(
                slideDirection,
                inputDir.normalized,
                Time.deltaTime * 1.5f
            ).normalized;
        }
        
        if (ps.OnSlope())
        {
            slideDirection = ps.GetSlopeMoveDirection(slideDirection);
            Vector3 downSlopeDir = Vector3.ProjectOnPlane(Vector3.down, ps.slopeHit.normal).normalized;
            if (Vector3.Dot(slideDirection, downSlopeDir) > 0)
            {
                slideSpeed += slideAcceleration * Time.deltaTime;
                slideSpeed = Mathf.Min(slideSpeed, maxSlideSpeed);
            }
        }

        Vector3 targetVel = slideDirection * slideSpeed;

        if (ps.OnSlope())
        {
            rb.linearVelocity = Vector3.ProjectOnPlane(targetVel, ps.slopeHit.normal);
        }
        else
        {
            rb.linearVelocity = new Vector3(targetVel.x, rb.linearVelocity.y, targetVel.z);
        }

        slideTimer -= Time.deltaTime;

        if (slideTimer < 0)
            StopSlide();
    }

    public void StopSlide()
    {
        ps.sliding = false;
        env_col.height = startHeight;
        env_col.center = new Vector3(env_col.center.x, 0f, env_col.center.z);
        atk_col.height = startHeight;
        atk_col.center = new Vector3(atk_col.center.x, 0f, atk_col.center.z);
    
        // reset camera
        cam.DoFov(startFOV);
    }
}
