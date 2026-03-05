using UnityEngine;

public class GroundSlam : MonoBehaviour
{
    [Header("References")]
    public Transform orientation;
    public Transform playerCam;
    private Rigidbody rb;
    private PlayerScript ps;
    public PlayerCam cam;

    [Header("Ground Slam")]
    public float slamForce;

    [Header("Input")]
    public KeyCode slamKey = KeyCode.LeftControl;

    [Header("Settings")]
    public bool disableGravityDuringSlam = false;
    public float keyTapTimeMax = 0.2f;
    public float slamCooldown = 2f;
    private float slamCooldownTimer;

    private bool superSlam = false;
    private Vector3 originalVelocity;
    private float keyTapTime;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        ps = GetComponent<PlayerScript>();
        slamCooldownTimer = 0f;
    }

    private void Update()
    {
        bool keyTapped = Input.GetKeyDown(slamKey);
        bool keyHeld = Input.GetKey(slamKey);

        if(keyTapped && !ps.sliding && !ps.slamming && !ps.superSlamming && !ps.grounded)
        {
            if(slamCooldownTimer <= 0)
            {
                slamCooldownTimer = slamCooldown;
                keyTapTime = Time.time;
                Slam();
            }
        }
        else if(keyHeld && (Time.time - keyTapTime) > keyTapTimeMax && !ps.sliding  && !ps.superSlamming && !ps.grounded)
        {
            if(slamCooldownTimer <= 0)
            {
                slamCooldownTimer = slamCooldown;
                superSlam = true;
                Slam();
            }
        }

        if((ps.slamming || ps.superSlamming) && ps.grounded)
            ResetSlam();
        
        if(slamCooldownTimer > 0)
            slamCooldownTimer -= Time.deltaTime;
    }

    private void Slam()
    {
        cam.DoFov(90f);

        if (disableGravityDuringSlam)
            rb.useGravity = false;

        originalVelocity = rb.linearVelocity;
        
        if(superSlam)
        {
            ps.superSlamming = true;
            ps.slamming = false;
            rb.linearVelocity = Vector3.zero;
        }
        else
        {
            ps.slamming = true;
            ps.superSlamming = false;
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }

        Vector3 forceToApply = Vector3.down * slamForce;
        rb.AddForce(forceToApply, ForceMode.Impulse);

        // if (ps.sliding)
        //     GetComponent<Sliding>().StopSlide();
    }


    public void ResetSlam()
    {
        ps.slamming = false;
        ps.superSlamming = false;

        cam.DoFov(80f);

        if (disableGravityDuringSlam)
            rb.useGravity = true;

        if(superSlam)
            rb.linearVelocity = originalVelocity;

        superSlam = false;
    }
}
