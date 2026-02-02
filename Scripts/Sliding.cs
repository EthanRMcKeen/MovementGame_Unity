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
    private PlayerMovementAdv pm;
    public PlayerCam cam;

    [Header("Sliding")]
    public float maxSliderTime;
    private float slideTimer;
    public float slideAcceleration = 50f;
    public float maxSlideSpeed = 50f;
    private float slideSpeed;
    private Vector3 slideDirection;

    public float slideYScale;
    private float startYScale;

    [Header("Input")]
    public KeyCode slideKey = KeyCode.LeftControl;
    public KeyCode sprintKey = KeyCode.LeftShift;

    // If true, we should resume sliding automatically when the player lands
    [HideInInspector]
    public bool resumeOnLand;

    public void QueueResumeOnLand()
    {
        resumeOnLand = true;
    }

    public void ClearResumeOnLand()
    {
        resumeOnLand = false;
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        pm = GetComponent<PlayerMovementAdv>();

        startYScale = playerObj.localScale.y;

        pm.sliding = false;
    }

    private void Update()
    {

        if(Input.GetKeyDown(slideKey) && Input.GetKey(sprintKey))
        {
            StartSlide();
        }

        if (Input.GetKeyUp(slideKey))
        {
            // If player releases the slide key, stop sliding and cancel any queued resume
            if (pm.sliding)
                StopSlide();
            resumeOnLand = false;
        }

        if (pm.sliding && (!pm.grounded || (pm.OnSlope() && rb.linearVelocity.y < 0.1f)))
        {
            slideTimer = maxSliderTime;
        }
    }

    private void FixedUpdate()
    {
        if (pm.sliding)
            SlidingMovement();
    }

    public void StartSlide()
    {
        pm.sliding = true;

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

        playerObj.localScale = new Vector3(playerObj.localScale.x, slideYScale, playerObj.localScale.z);
        rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);

        slideTimer = maxSliderTime;

        //camera effects
        cam.DoFov(90f);
    }

    private void SlidingMovement()
    {
        Vector3 moveDir = slideDirection;
        if (pm.OnSlope())
        {
            moveDir = pm.GetSlopeMoveDirection(moveDir);
            Vector3 downSlopeDir = Vector3.ProjectOnPlane(Vector3.down, pm.slopeHit.normal).normalized;
            if (Vector3.Dot(moveDir, downSlopeDir) > 0)
            {
                slideSpeed += slideAcceleration * Time.deltaTime;
                slideSpeed = Mathf.Min(slideSpeed, maxSlideSpeed);
            }
        }

        Vector3 targetVel = moveDir * slideSpeed;

        if (pm.OnSlope())
        {
            rb.linearVelocity = Vector3.ProjectOnPlane(targetVel, pm.slopeHit.normal);
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
        pm.sliding = false;
        playerObj.localScale = new Vector3(playerObj.localScale.x, startYScale, playerObj.localScale.z);
    
        // reset camera
        cam.DoFov(80f);
    }
}
