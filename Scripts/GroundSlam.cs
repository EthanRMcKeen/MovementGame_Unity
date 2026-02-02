using UnityEngine;

public class GroundSlam : MonoBehaviour
{
    [Header("References")]
    public Transform orientation;
    public Transform playerCam;
    private Rigidbody rb;
    private PlayerMovementAdv pm;
    public PlayerCam cam;

    [Header("Ground Slam")]
    public float slamForce;

    [Header("Input")]
    public KeyCode slamKey = KeyCode.LeftControl;

    [Header("Settings")]
    public bool disableGravityDuringSlam = false;
    public float keyTapTimeMax = 0.2f;

    private bool superSlam = false;
    // private bool slamQueued = false;
    private Vector3 originalVelocity;
    private float keyTapTime;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        pm = GetComponent<PlayerMovementAdv>();
    }

    private void Update()
    {
        bool keyTapped = Input.GetKeyDown(slamKey);
        bool keyHeld = Input.GetKey(slamKey);

        if(keyTapped && !pm.sliding && !pm.slamming && !pm.superSlamming && !pm.grounded)
        {
            keyTapTime = Time.time;
            // slamQueued = true;
            Slam();
        }
        else if(keyHeld && (Time.time - keyTapTime) > keyTapTimeMax && !pm.sliding  && !pm.superSlamming && !pm.grounded)
        {
            superSlam = true;
            //slamQueued = false;
            Slam();
        }

        // if(slamQueued && !keyHeld)
        // {
        //     superSlam = false;
        //     slamQueued = false;
        //     Slam();
        // }

        if((pm.slamming || pm.superSlamming) && pm.grounded)
        {
            ResetSlam();
        }
    }

    private void Slam()
    {
        cam.DoFov(90f);

        if (disableGravityDuringSlam)
            rb.useGravity = false;

        originalVelocity = rb.linearVelocity;
        
        if(superSlam)
        {
            pm.superSlamming = true;
            pm.slamming = false;
            rb.linearVelocity = Vector3.zero;
        }
        else
        {
            pm.slamming = true;
            pm.superSlamming = false;
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }

        Vector3 forceToApply = Vector3.down * slamForce;
        rb.AddForce(forceToApply, ForceMode.Impulse);

        // if (pm.sliding)
        //     GetComponent<Sliding>().StopSlide();
    }


    public void ResetSlam()
    {
        pm.slamming = false;
        pm.superSlamming = false;

        cam.DoFov(80f);

        if (disableGravityDuringSlam)
            rb.useGravity = true;

        if(superSlam)
            rb.linearVelocity = originalVelocity;

        superSlam = false;
    }
}
