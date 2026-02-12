using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

public class PlayerMovementAdv : MonoBehaviour
{
    [Header("Movement")]
    private float maxGroundSpeed; // moveSpeed
    public float maxAirSpeed = 7f;
    public float walkSpeed = 7f;
    public float sprintSpeed = 10f;
    public float slideSpeed = 10f;
    public float wallRunSpeed = 10f;
    public float dashspeed = 20f;

    public float groundAcceleration = 50f;
    public float airAcceleration = 15f;

    public float groundDrag = 6f;

    [Header("Jumping")]
    public float jumpForce = 5f;
    public float jumpCooldown = 0f; // 0 for true bhop

    //double jumping
    private float jumpsRemaining;
    private int maxJumps = 2;
    bool jumpHeld;
    bool jumpQueued;


    [Header("Crouching")]
    public float crouchSpeed = 4f;
    public float crouchYScale = 0.5f;
    private float startYScale;

    [Header("Keybinds")]
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode sprintKey = KeyCode.LeftShift;
    public KeyCode crouchKey = KeyCode.LeftControl;

    [Header("Ground Check")]
    public LayerMask whatIsGround;
    public bool grounded;
    public float playerHeight = 2f;

    [Header("Slope Handling")]
    public float maxSlopeAngle;
    public RaycastHit slopeHit;

    [Header("References")]
    public Transform orientation;
    public Animator anim;
    private PlayerCombat combat;
    public Toggle debugMode;

    float horizontalInput;
    float verticalInput;

    bool readyToJump;
    bool wishJump;
    bool onSlope;
    bool wasGrounded;
    bool wasWallRunning;

    Rigidbody rb;
    Vector3 wishDir;

    [Header("States")]
    public bool sliding;
    public bool wallRunning;
    public bool dashing;
    public bool slamming;
    public bool superSlamming;
    public bool lightAttacking;

    public MovementState state;
    public enum MovementState
    {
        idle,
        walking,
        sprinting,
        wallrunning,
        crouching,
        sliding,
        dashing,
        air,
        slamming,
        superslamming,
        lightAttacking, 
        unknown
    }
    

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        //anim = GetComponentInChildren<Animator>();
        combat = GetComponent<PlayerCombat>();
    
        readyToJump = true;
        startYScale = transform.localScale.y;
        jumpsRemaining = maxJumps;
    }

    void Update()
    {
        float currentHeight = playerHeight * transform.localScale.y;
        grounded = Physics.Raycast(transform.position, Vector3.down, currentHeight * 0.5f + 0.2f, whatIsGround) || onSlope;

        // Reset jumps when we touch the ground (preserve bhop when holding jump)
        if (grounded && !wasGrounded)
        {
            jumpsRemaining = maxJumps;
        }
        wasGrounded = grounded;

        // Reset jumps when starting a wall run
        if (wallRunning && !wasWallRunning)
        {
            jumpsRemaining = maxJumps - 1;
        }

        wasWallRunning = wallRunning;


        GetInput();
        HandleDrag();
        StateHandler();
        UpdateAnimations();
    }

    void FixedUpdate()
    {
        onSlope = OnSlope();
        Move();
        HandleJump();
        ApplySlopeStick();
    }

    void GetInput()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        wishJump = Input.GetKey(jumpKey);

        jumpHeld = Input.GetKey(jumpKey);

        if (Input.GetKeyDown(jumpKey))
        {
            jumpQueued = true;
        }


        //start crouch
        if (Input.GetKeyDown(crouchKey))
        {
            transform.localScale = new Vector3(transform.localScale.x, crouchYScale, transform.localScale.z);
            rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);
        }

        if(Input.GetKeyUp(crouchKey))
        {
            transform.localScale = new Vector3(transform.localScale.x, startYScale, transform.localScale.z);
        }

        //DEBUG - reset scene
        if (Input.GetKeyDown(KeyCode.R))
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
    }

    void HandleDrag()
    {
        rb.linearDamping = grounded ? groundDrag : 0f;
    }

    void Move()
    {
        wishDir = orientation.forward * verticalInput + orientation.right * horizontalInput;

        if (wishDir.magnitude > 1f)
            wishDir.Normalize();

        if (OnSlope())
        {
            GroundMove(GetSlopeMoveDirection(wishDir));
        }
        else if (grounded)
        {
            GroundMove(wishDir);
        }
        else
        {
            AirMove(wishDir);
        }
    }

    void GroundMove(Vector3 wishDir)
    {
        Accelerate(wishDir, maxGroundSpeed, groundAcceleration);
    }

    void AirMove(Vector3 wishDir)
    {
        Accelerate(wishDir, maxAirSpeed, airAcceleration);
    }

    public void Accelerate(Vector3 wishDir, float maxSpeed, float accel)
    {
        float currentSpeed = Vector3.Dot(rb.linearVelocity, wishDir);
        float addSpeed = maxSpeed - currentSpeed;

        if (addSpeed <= 0)
            return;

        float accelSpeed = accel * Time.fixedDeltaTime * maxSpeed;
        if (accelSpeed > addSpeed)
            accelSpeed = addSpeed;

        rb.linearVelocity += wishDir * accelSpeed;
    }

    void HandleJump()
    {
        if (wallRunning)
            return;
            
        // Ground jump (bhop)
        if (grounded && jumpHeld && readyToJump)
        {
            readyToJump = false;

            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

            // Cancel attack on jump
            combat.CancelAttack();

            Invoke(nameof(ResetJump), jumpCooldown);

            jumpsRemaining = maxJumps - 1;
            jumpQueued = false; // consume

            return;
        }

        // Double jump (air tap)
        if (!grounded && jumpQueued && jumpsRemaining > 0)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

            // Cancel attack on double jump
            combat.CancelAttack();

            jumpsRemaining--;
            jumpQueued = false; // consume
            GetComponent<GroundSlam>().ResetSlam();
        }
    }


    private void StateHandler()
    {
        //Mode - Light Attacking
        if (lightAttacking)
        {
            state = MovementState.lightAttacking;
            maxGroundSpeed = walkSpeed;
        }

        //Mode - Wallrunning
        else if (wallRunning)
        {
            state = MovementState.wallrunning;

            maxGroundSpeed = wallRunSpeed;
        }

        //Mode - Dashing
        else if (dashing)
        {
            state = MovementState.dashing;

            maxGroundSpeed = dashspeed;
        }

        //Mode - Superslamming
        else if (superSlamming)
        {
            state = MovementState.superslamming;
        }

        //Mode - Slamming
        else if (slamming)
        {
            state = MovementState.slamming;
        }

        // Mode - Air
        else if (!grounded)
        {
            state = MovementState.air;
        }

        // Mode - Sliding
        else if (sliding)
        {
            state = MovementState.sliding;

            maxGroundSpeed = sprintSpeed;
        }

        // Mode - crouching
        else if (Input.GetKey(crouchKey))
        {
            state = MovementState.crouching;
            maxGroundSpeed = crouchSpeed;
        }

        // Mode - idle
        else if (grounded && horizontalInput == 0 && verticalInput == 0 && !jumpQueued)
        {
            state = MovementState.idle;
        }

        // Mode - sprinting
        else if (grounded && Input.GetKey(sprintKey) && !jumpQueued)
        {
            state = MovementState.sprinting;
            maxGroundSpeed = sprintSpeed;
        }

        //Mode - walking
        else if (grounded && !jumpQueued)
        {
            state = MovementState.walking;
            maxGroundSpeed = walkSpeed;
        }
        //Mode - unkown
        else
        {
            state = MovementState.unknown;
        }
    }

    void UpdateAnimations()
    {
        anim.SetBool("isRunning", state == MovementState.sprinting);

        anim.SetBool("isWalking", state == MovementState.walking);

        anim.SetBool("isSliding", state == MovementState.sliding);

        anim.SetBool("isJumping", !grounded && jumpsRemaining == maxJumps - 1);

        anim.SetBool("isDoubleJumping", !grounded && jumpsRemaining == maxJumps - 2);

        anim.SetBool("isLAttacking", state == MovementState.lightAttacking);
    }

    private void ResetJump()
    {
        readyToJump = true;
    }

    public void ClearJumpQueue()
    {
        jumpQueued = false;
    }

    public bool OnSlope()
    {
        float currentHeight = playerHeight * transform.localScale.y;
        if(Physics.Raycast(transform.position, Vector3.down, out slopeHit, currentHeight * 0.5f + 0.3f))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return angle < maxSlopeAngle && angle != 0;
        }
        return false;
    }

    public Vector3 GetSlopeMoveDirection(Vector3 direction)
    {
        return Vector3.ProjectOnPlane(direction, slopeHit.normal).normalized;
    }

    void ApplySlopeStick() // prevent sliding down slopes when idle
    {
        if (!grounded || !OnSlope() || sliding)
            return;

        // If player is holding jump but hasn't jumped yet, don't apply slope correction
        if (wishJump)
            return;

        Vector3 gravity = Physics.gravity;
        Vector3 slopeGravity = Vector3.ProjectOnPlane(gravity, slopeHit.normal);

        rb.AddForce(-slopeGravity, ForceMode.Acceleration);
    }

    public void AnimationHandler(string parameter)
    {
        if(parameter != "idle")
            anim.SetBool(parameter, true);

        string[] otherParams = new string[] { "isRunning", "isSliding", "isJumping", "isDoubleJumping" };
        foreach (string other in otherParams)        {
            if (other != parameter)
                anim.SetBool(other, false);
        }
    }

    //DEBUG - GUI
    void OnGUI()
    {
        if (!debugMode.isOn)
            return;
        GUIStyle style = new GUIStyle();
        style.fontSize = 20;
        float speed = new Vector3(rb.linearVelocity.x, rb.linearVelocity.y, rb.linearVelocity.z).magnitude;
        GUI.Label(new Rect(10, 10, 200, 30), $"Speed: {speed:F2}", style);
        GUI.Label(new Rect(10, 30, 200, 30), $"State: {state}", style);
        GUI.Label(new Rect(10, 50, 200, 30), $"Jumps Remaining: {jumpsRemaining}", style);
        GUI.Label(new Rect(10, 70, 200, 30), $"Horizontal Input: {horizontalInput}", style);
        GUI.Label(new Rect(10, 90, 200, 30), $"Vertical Input: {verticalInput}", style);
        style.normal.textColor = Color.green;
    }
}
