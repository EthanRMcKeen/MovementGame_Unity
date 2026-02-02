using UnityEngine;

public class WallRunning : MonoBehaviour
{
    [Header("Wall Running")]
    public LayerMask whatIsWall;
    public LayerMask whatIsGround;
    public float wallRunForce;
    public float wallJumpUpForce;
    public float wallJumpSideForce;
    public float wallClimbSpeed;
    public float maxWallRunTime;
    public float gravityCounterForce;
    public float normalVelocityRetention; // 0 - lose all, 1 - keep all
    private float wallRunTimer;
    private float wallRunSpeed;
    public float minWallRunningSpeed;

    [Header("Input")]
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode upwardRunKey = KeyCode.LeftShift;
    public KeyCode downwardRunKey = KeyCode.LeftControl;
    private float horizontalInput;

    private bool upwardRunning;
    private bool downwardRunning;

    [Header("Detection")]
    public float wallCheckDistance;
    public float minJumpHeight;

    private RaycastHit leftWallHit;
    private RaycastHit rightWallHit;
    private bool wallLeft;
    private bool wallRight;

    [Header("References")]
    public Transform orientation;
    private Rigidbody rb;
    private PlayerMovementAdv pm;
    public PlayerCam cam;

    // NEW
    private bool runningOnRightWall;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        pm = GetComponent<PlayerMovementAdv>();
    }

    private void Update()
    {
        CheckForWall();
        StateMachine();
    }

    private void FixedUpdate()
    {
        if (pm.wallRunning)
        {
            WallRunningMovement();

            wallRunTimer -= Time.fixedDeltaTime;
            if (wallRunTimer <= 0)
                StopWallRun();
        }
    }

    private void CheckForWall()
    {
        wallRight = Physics.Raycast(transform.position, orientation.right, out rightWallHit, wallCheckDistance, whatIsWall);
        wallLeft  = Physics.Raycast(transform.position, -orientation.right, out leftWallHit, wallCheckDistance, whatIsWall);
    }

    private bool AboveGround()
    {
        return !pm.grounded;
    }

    private void StateMachine()
    {
        upwardRunning = Input.GetKey(upwardRunKey);
        downwardRunning = Input.GetKey(downwardRunKey);

        horizontalInput = Input.GetAxisRaw("Horizontal");

        bool pressRight = horizontalInput > 0f;
        bool pressLeft  = horizontalInput < 0f;

        // enter wall run
        if (!pm.wallRunning && AboveGround())
        {
            if (wallRight)
            {
                runningOnRightWall = true;
                StartWallRun();
            }
            else if (wallLeft)
            {
                runningOnRightWall = false;
                StartWallRun();
            }
        }

        // while wall running
        if (pm.wallRunning)
        {
            // Stop if wall is lost
            if ((runningOnRightWall && !wallRight) ||
                (!runningOnRightWall && !wallLeft))
            {
                StopWallRun();
                return;
            }

            // Stop if opposite direction is pressed
            // if ((runningOnRightWall && pressLeft) ||
            //     (!runningOnRightWall && pressRight))
            // {
            //     StopWallRun();
            //     return;
            // }

            if (Input.GetKeyDown(jumpKey))
                WallJump();
        }
    }

    private void StartWallRun()
    {
        pm.wallRunning = true;
        wallRunTimer = maxWallRunTime;

        RaycastHit wallHit = runningOnRightWall ? rightWallHit : leftWallHit;
        Vector3 wallNormal = wallHit.normal;

        // Horizontal velocity only
        Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        // Decompose velocity
        Vector3 normalComponent   = Vector3.Project(flatVel, wallNormal);
        Vector3 parallelComponent = flatVel - normalComponent;

        // Retain only a portion of the normal component
        Vector3 retainedVelocity = parallelComponent + normalComponent * normalVelocityRetention;

        float retainedSpeed = retainedVelocity.magnitude;

        // Enforce minimum wall run speed
        wallRunSpeed = Mathf.Max(retainedSpeed, minWallRunningSpeed);

        // Re-scale velocity ONLY if needed
        Vector3 finalVelocity = retainedVelocity;

        if (retainedSpeed > 0f && retainedSpeed < minWallRunningSpeed)
        {
            finalVelocity = retainedVelocity.normalized * minWallRunningSpeed;
        }

        // Apply corrected velocity
        rb.linearVelocity = new Vector3(
            finalVelocity.x,
            rb.linearVelocity.y,
            finalVelocity.z
        );


        // Camera effects
        cam.DoFov(90f);
        if(wallLeft)
            cam.DoTilt(-5f);
        if(wallRight)
            cam.DoTilt(5f);
    }


    private void WallRunningMovement()
    {
        RaycastHit wallHit = runningOnRightWall ? rightWallHit : leftWallHit;
        Vector3 wallNormal = wallHit.normal;

        Vector3 wallForward = Vector3.Cross(wallNormal, Vector3.up);
        if ((orientation.forward - wallForward).magnitude >
            (orientation.forward + wallForward).magnitude)
        {
            wallForward = -wallForward;
        }

        // Maintain speed
        pm.Accelerate(wallForward, wallRunSpeed, pm.groundAcceleration);

        // Vertical movement
        if (upwardRunning)
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, wallClimbSpeed, rb.linearVelocity.z);
        else if (downwardRunning)
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, -wallClimbSpeed, rb.linearVelocity.z);

        // Stick to wall
        rb.AddForce(-wallNormal * 100f, ForceMode.Force);

        // Counter gravity
        rb.AddForce(Vector3.up * gravityCounterForce, ForceMode.Force);
    }

    private void StopWallRun()
    {
        pm.wallRunning = false;

        pm.ClearJumpQueue();

        // reset camera
        cam.DoFov(80f);
        cam.DoTilt(0f);
    }

    private void WallJump()
    {
        RaycastHit wallHit = runningOnRightWall ? rightWallHit : leftWallHit;
        Vector3 forceToApply = transform.up * wallJumpUpForce +
                              wallHit.normal * wallJumpSideForce;

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(forceToApply, ForceMode.Impulse);

        StopWallRun();
        
        pm.ClearJumpQueue();
    }
}
