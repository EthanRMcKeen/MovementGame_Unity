using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

public class PlayerScript : MonoBehaviour
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
    public float crouchScale = 0.5f;
    private float startHeight;

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
    private WallRunning wr;
    public CapsuleCollider env_col;

    float horizontalInput;
    float verticalInput;

    bool readyToJump;
    bool wishJump;
    bool onSlope;
    bool wasGrounded;
    bool wasWallRunning;

    Rigidbody rb;
    Vector3 wishDir;
    CapsuleCollider atk_col;

    private string[] currentState = new string[2];
    public int currentComboStep = 0;

    [Header("States")]
    private bool crouching;
    public bool sliding;
    public bool wallRunning;
    public bool dashing;
    public bool slamming;
    public bool superSlamming;
    public bool lightAttacking;
    public bool blocking;
    public bool parrying;
    public bool dodging;

    public MovementState mstate;
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
        dodging,
        unknown
    }

    public CombatState cstate;
    public enum CombatState
    {
        idle,
        lightAttacking, 
        blocking,
        parrying,
        unknown
    }

    private enum AnimationLayer
    {
        fullBody = 0,
        upperBody = 1
    }
    

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        mstate = MovementState.idle;
        cstate = CombatState.idle;

        //anim = GetComponentInChildren<Animator>();
        combat = GetComponent<PlayerCombat>();
        wr = GetComponent<WallRunning>();
        atk_col = GetComponent<CapsuleCollider>();
    
        readyToJump = true;
        startHeight = atk_col.height;
        jumpsRemaining = maxJumps;

        // initialize current state array
        currentState[(int)AnimationLayer.fullBody] = "idle";
        currentState[(int)AnimationLayer.upperBody] = "empty";
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
        //UpdateAnimations();
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

        anim.SetFloat("x", horizontalInput);
        anim.SetFloat("y", verticalInput);

        wishJump = Input.GetKey(jumpKey);

        jumpHeld = Input.GetKey(jumpKey);

        if (Input.GetKeyDown(jumpKey))
        {
            jumpQueued = true;
        }


        //start crouch
        if (Input.GetKeyDown(crouchKey))
        {
            float crouchHeight = startHeight * crouchScale;
            float heightDiff = startHeight - crouchHeight;

            env_col.height = crouchHeight;
            env_col.center = new Vector3(
                env_col.center.x,
                - heightDiff / 2f,
                env_col.center.z
            );

            atk_col.height = crouchHeight;
            atk_col.center = new Vector3(
                atk_col.center.x,
                - heightDiff / 2f,
                atk_col.center.z
            );

            //rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);
            crouching = true;
        }

        if(Input.GetKeyUp(crouchKey))
        {
            atk_col.height = startHeight;
            atk_col.center = new Vector3(atk_col.center.x, 0f, atk_col.center.z);

            env_col.height = startHeight;
            env_col.center = new Vector3(env_col.center.x, 0f, env_col.center.z);
            crouching = false;
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

            // Cancel attack on jump (only if we were attacking)
            if (combat != null && combat.IsHitboxActive())
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

            // Cancel attack on double jump (only if we were attacking)
            if (combat != null && combat.IsHitboxActive())
                combat.CancelAttack();

            jumpsRemaining--;
            jumpQueued = false; // consume
            GetComponent<GroundSlam>().ResetSlam();
        }
    }


    private void StateHandler()
    {
        // MOVEMENT STATES
        // Mode - Dodging
        if (dodging)
        {
            mstate = MovementState.dodging;
            maxGroundSpeed = sprintSpeed;
            ChangeAnimationState("dodge", 0);
        }

        //Mode - Wallrunning
        else if (wallRunning)
        {
            mstate = MovementState.wallrunning;

            maxGroundSpeed = wallRunSpeed;
            if (wr.wallLeft)
                ChangeAnimationState("wallrun_l");
            else if (wr.wallRight)
                ChangeAnimationState("wallrun_r");
        }

        //Mode - Dashing
        else if (dashing)
        {
            mstate = MovementState.dashing;

            maxGroundSpeed = dashspeed;
        }

        //Mode - Superslamming
        else if (superSlamming)
        {
            mstate = MovementState.superslamming;
        }

        //Mode - Slamming
        else if (slamming)
        {
            mstate = MovementState.slamming;
        }

        // Mode - Jump
        else if(!grounded && jumpsRemaining == maxJumps - 1 && mstate != MovementState.wallrunning)
        {
            mstate = MovementState.air;
            ChangeAnimationState("jump");
        }

        // Mode - Double Jump
        else if(!grounded && jumpsRemaining == maxJumps - 2 && mstate != MovementState.wallrunning)
        {
            mstate = MovementState.air;
            ChangeAnimationState("double_jump");
        }

        // Mode - Air
        else if (!grounded)
        {
            mstate = MovementState.air;
        }

        // Mode - Sliding
        else if (sliding)
        {
            mstate = MovementState.sliding;

            maxGroundSpeed = sprintSpeed;
            ChangeAnimationState("slide");
        }

        // Mode - crouching
        else if (crouching)
        {
            mstate = MovementState.crouching;
            maxGroundSpeed = crouchSpeed;
            if (horizontalInput != 0 || verticalInput != 0)
            {
                ChangeAnimationState("crouch_walk");
            }else
            {
                ChangeAnimationState("crouch_idle");
            }
        }

        // Mode - idle
        else if (grounded && horizontalInput == 0 && verticalInput == 0)
        {
            mstate = MovementState.idle;
            ChangeAnimationState("locomotion");
        }

        // Mode - Running
        else if (grounded && Input.GetKey(sprintKey))
        {
            mstate = MovementState.sprinting;
            maxGroundSpeed = sprintSpeed;
            ChangeAnimationState("run");
        }

        //Mode - walking
        else if (grounded)
        {
            mstate = MovementState.walking;
            maxGroundSpeed = walkSpeed;
            ChangeAnimationState("locomotion");
        }
        //Mode - unkown
        else
        {
            mstate = MovementState.unknown;
            if(debugMode.isOn)
                Debug.Log("State is unknown. Check StateHandler conditions.");
        }


        // COMBAT STATES
        // Mode - Parrying
        if (parrying)
        {
            cstate = CombatState.parrying;
            maxGroundSpeed = walkSpeed;
        }
        
        //Mode - Light Attacking
        else if (lightAttacking)
        {
            cstate = CombatState.lightAttacking;
            maxGroundSpeed = walkSpeed;
            string attackAnim = $"Lattack{currentComboStep}";
            ChangeAnimationState(attackAnim, AnimationLayer.upperBody);
        }

        else if (blocking)
        {
            cstate = CombatState.blocking;
            maxGroundSpeed = crouchSpeed;
            ChangeAnimationState("block", AnimationLayer.upperBody);
        }

        // Mode - idle
        else
        {
            cstate = CombatState.idle;
            ChangeAnimationState("empty", AnimationLayer.upperBody);
        }
    }

    private void ChangeAnimationState(string newState, AnimationLayer layer = AnimationLayer.fullBody)
    {
        int layerIndex = (int)layer;

        if (currentState[layerIndex] == newState)
            return;

        anim.Play(newState, layerIndex);

        currentState[layerIndex] = newState;
    }

    // void UpdateAnimations()
    // {
    //     anim.SetBool("isRunning", state == MovementState.sprinting);

    //     anim.SetBool("isWalking", state == MovementState.walking);

    //     anim.SetBool("isSliding", state == MovementState.sliding);

    //     anim.SetBool("isJumping", !grounded && jumpsRemaining == maxJumps - 1 && state != MovementState.wallrunning);

    //     anim.SetBool("isDoubleJumping", !grounded && jumpsRemaining == maxJumps - 2 && state != MovementState.wallrunning);

    //     //anim.SetBool("isLAttacking", state == MovementState.lightAttacking);

    //     anim.SetBool("isBlocking", state == MovementState.blocking);

    //     anim.SetBool("isLWallRunning", state == MovementState.wallrunning && wr.wallLeft);

    //     anim.SetBool("isRWallRunning", state == MovementState.wallrunning && wr.wallRight);

    //     anim.SetBool("isDodging", state == MovementState.dodging);

    //     anim.SetBool("isIdle", state == MovementState.idle);
    //     if (state == MovementState.lightAttacking){
    //         anim.SetTrigger("isLAttacking");
    //         combat.CancelAttack();
    //     }
    // }

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
        GUI.Label(new Rect(10, 30, 200, 30), $"MState: {mstate}", style);
        GUI.Label(new Rect(10, 50, 200, 30), $"CState: {cstate}", style);
        GUI.Label(new Rect(10, 70, 200, 30), $"Jumps Remaining: {jumpsRemaining}", style);
        GUI.Label(new Rect(10, 90, 200, 30), $"Horizontal Input: {horizontalInput}", style);
        GUI.Label(new Rect(10, 110, 200, 30), $"Vertical Input: {verticalInput}", style);
        style.normal.textColor = Color.green;
    }
}
