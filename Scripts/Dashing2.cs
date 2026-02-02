using System.Collections;
using UnityEngine;

public class Dashing2 : MonoBehaviour
{
    [Header("References")]
    public Transform orientation;
    public Transform playerCam;
    private Rigidbody rb;
    private PlayerMovementAdv pm;
    public PlayerCam cam;

    [Header("Dashing")]
    public float dashTime;
    public float dashDistance;
    public LayerMask dashMask;
    [Tooltip("Small distance to stay away from obstacles when dashing")]
    public float skinWidth = 0.1f;

    [Header("cooldown")]
    public float dashCd;
    private float dashCdTimer;

    [Header("Input")]
    public KeyCode dashKey = KeyCode.E;

    [Header("Settings")]
    public bool useCameraForward = true;
    public bool allowAllDirections = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        pm = GetComponent<PlayerMovementAdv>();
    }

    void Update()
    {
        if (Input.GetKeyDown(dashKey))
        {
            if(dashCdTimer <= 0)
            {
                dashCdTimer = dashCd;
                StartCoroutine(Dash());
            }
        }

        if(dashCdTimer > 0)
            dashCdTimer -= Time.deltaTime;
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

    private IEnumerator Dash()
    {
        pm.dashing = true;
        GetComponent<GroundSlam>().ResetSlam();
        cam.DoFov(90f);

        Transform forwarT = useCameraForward ? playerCam : orientation;

        Vector3 dashDirection = GetDirection(forwarT);


        // Check for obstacle in the dash path and clamp the target position to it
        Vector3 startPosition = transform.position;
        float maxDistance = dashDistance;
        Vector3 targetPosition = startPosition + dashDirection * maxDistance;

        if (Physics.Raycast(startPosition, dashDirection, out RaycastHit hit, maxDistance, dashMask))
        {
            targetPosition = hit.point - dashDirection * skinWidth;
        }

        float elapsedTime = 0f;
        bool collided = false;

        while (elapsedTime < dashTime)
        {
            float t = elapsedTime / dashTime;
            Vector3 nextPos = Vector3.Lerp(startPosition, targetPosition, t);

            // If moving to nextPos would hit something (dynamic obstacles), stop early
            Vector3 moveDir = nextPos - transform.position;
            float moveDist = moveDir.magnitude;
            if (moveDist > 0f)
            {
                if (Physics.Raycast(transform.position, moveDir.normalized, out RaycastHit stepHit, moveDist + 0.01f, dashMask))
                {
                    Vector3 collidePos = stepHit.point - moveDir.normalized * skinWidth;
                    rb.MovePosition(collidePos);
                    rb.linearVelocity = Vector3.zero;
                    collided = true;
                    break;
                }
            }

            rb.MovePosition(nextPos);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (!collided)
        {
            // ensure final position exactly matches target if not blocked
            rb.MovePosition(targetPosition);
        }

        rb.linearVelocity = Vector3.zero;

        pm.dashing = false;
        cam.DoFov(80f);
    }
}
