using UnityEngine;

public class Dashing : MonoBehaviour
{
    [Header("References")]
    public Transform orientation;
    public Transform playerCam;
    private Rigidbody rb;
    private PlayerScript ps;
    public PlayerCam cam;

    [Header("Dashing")]
    public float dashForce;
    public float dashUpwardForce;
    public float dashDuration;

    [Header("cooldown")]
    public float dashCd;
    private float dashCdTimer;

    [Header("Input")]
    public KeyCode dashKey = KeyCode.E;

    [Header("Settings")]
    public bool useCameraForward = true;
    public bool allowAllDirections = false;
    public bool disableGravityDuringDash = false;

    private float startFOV;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        ps = GetComponent<PlayerScript>();
        startFOV = cam.cam.fieldOfView;
    }

    private void Update()
    {
        if(Input.GetKeyDown(dashKey))
            Dash();
        
        if(dashCdTimer > 0)
            dashCdTimer -= Time.deltaTime;
    }

    private void Dash()
    {
        if(dashCdTimer > 0)
            return;
        else
            dashCdTimer = dashCd;

        ps.dashing = true;
        GetComponent<GroundSlam>().ResetSlam();

        cam.DoFov(startFOV + 20f);

        if (disableGravityDuringDash)
            rb.useGravity = false;

        Invoke(nameof(DelayedDashForce), 0.025f);
        Invoke(nameof(ResetDash), dashDuration);

        if (ps.sliding)
            GetComponent<Sliding>().StopSlide();
    }

    private void DelayedDashForce()
    {
        Transform forwarT = useCameraForward ? playerCam : orientation;

        Vector3 direction = GetDirection(forwarT);
        Vector3 forceToApply = direction * dashForce + orientation.up * dashUpwardForce;

        // Keep velocity only if it's already moving in the dash direction
        float velInDashDir = Vector3.Dot(rb.linearVelocity, direction);
        velInDashDir = Mathf.Max(0f, velInDashDir);
        rb.linearVelocity = velInDashDir * direction;

        rb.AddForce(forceToApply, ForceMode.Impulse);
    }


    private void ResetDash()
    {
        ps.dashing = false;

        cam.DoFov(startFOV);

        if (disableGravityDuringDash)
            rb.useGravity = true;
    }

    private Vector3 GetDirection(Transform forwarT)
    {
        float horizontalInput = Input.GetAxisRaw("Horizontal");
        float verticalInput = Input.GetAxisRaw("Vertical");

        Vector3 direction = new Vector3();

        if (allowAllDirections)
            direction = forwarT.forward * verticalInput + forwarT.right * horizontalInput;
        else
            direction = forwarT.forward;

        if (verticalInput == 0 && horizontalInput == 0)
            direction = forwarT.forward;

        return direction.normalized;
    }
}
