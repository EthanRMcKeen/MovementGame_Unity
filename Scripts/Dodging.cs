using UnityEngine;

public class Dodging : MonoBehaviour
{
    [Header("References")]
    public Transform orientation;
    public Transform playerCam;
    private Rigidbody rb;
    private PlayerScript ps;
    private PlayerCombat pc;
    public PlayerCam cam;

    [Header("Dodging")]
    public float dodgeForce;
    public float dodgeDuration;

    [Header("cooldown")]
    public float dodgeCd;
    private float dodgeCdTimer;

    [Header("Input")]
    public KeyCode dodgeKey = KeyCode.LeftAlt;

    [Header("Settings")]
    public bool disableGravityDuringDodge = false;

    private float startFOV;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        ps = GetComponent<PlayerScript>();
        pc = GetComponent<PlayerCombat>();
        startFOV = cam.cam.fieldOfView;
    }

    private void Update()
    {
        if(Input.GetKeyDown(dodgeKey) && ps.grounded && !ps.lightAttacking)
            if(dodgeCdTimer <= 0)
            {
                dodgeCdTimer = dodgeCd;
                Dodge();
            }
        
        if(dodgeCdTimer > 0)
            dodgeCdTimer -= Time.deltaTime;
    }

    private void Dodge()
    {   
        if (ps.sliding)
            GetComponent<Sliding>().StopSlide();

        ps.dodging = true;
        cam.DoFov(startFOV + 20f);

        pc.isDamageable = false;

        if (disableGravityDuringDodge)
            rb.useGravity = false;

        Invoke(nameof(DelayedDodgeForce), 0.025f);
        Invoke(nameof(ResetDodge), dodgeDuration);
    }

    private void DelayedDodgeForce()
    {
        Transform forwarT = orientation;

        Vector3 direction = GetDirection(forwarT);
        Vector3 forceToApply = direction * dodgeForce;

        // Keep velocity only if it's already moving in the dash direction
        // float velInDashDir = Vector3.Dot(rb.linearVelocity, direction);
        // velInDashDir = Mathf.Max(0f, velInDashDir);
        // rb.linearVelocity = velInDashDir * direction;

        rb.AddForce(forceToApply, ForceMode.Impulse);
    }


    private void ResetDodge()
    {
        ps.dodging = false;

        cam.DoFov(startFOV);

        pc.isDamageable = true;

        if (disableGravityDuringDodge)
            rb.useGravity = true;
    }

    private Vector3 GetDirection(Transform forwarT)
    {
        float horizontalInput = Input.GetAxisRaw("Horizontal");
        float verticalInput = Input.GetAxisRaw("Vertical");

        Vector3 direction = new Vector3();

        direction = forwarT.forward * verticalInput + forwarT.right * horizontalInput;

        if (verticalInput == 0 && horizontalInput == 0)
            direction = forwarT.forward;

        return direction.normalized;
    }
}
