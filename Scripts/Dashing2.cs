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
    private PlayerCombat combat;
    public CapsuleCollider col;

    [Header("Dashing")]
    public float dashTime;
    public float dashDistance;
    public LayerMask dashMask;
    [Tooltip("Small distance to stay away from obstacles when dashing")]
    public float skinWidth = 0.1f;
    public float postDashVelocity;

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
        combat = GetComponent<PlayerCombat>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
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
        combat.CancelAttack();

        Transform forwarT = useCameraForward ? playerCam : orientation;

        Vector3 dashDirection = GetDirection(forwarT);

        if (IsDashDirectionBlocked(dashDirection))
        {
            pm.dashing = false;
            cam.DoFov(80f);
            yield break;
        }

        // Check for obstacle in the dash path and clamp the target position to it
        Vector3 startPosition = transform.position - dashDirection * skinWidth;
        float maxDistance = dashDistance;
        Vector3 targetPosition = startPosition + dashDirection * maxDistance;

        GetCapsulePoints(out Vector3 c1, out Vector3 c2, out float radius);

        if (Physics.CapsuleCast(
                c1,
                c2,
                radius,
                dashDirection,
                out RaycastHit hit,
                maxDistance,
                dashMask))
        {
            targetPosition = startPosition + dashDirection * (hit.distance - skinWidth);
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
                GetCapsulePoints(out Vector3 s1, out Vector3 s2, out float r);

                if (Physics.CapsuleCast(
                        s1,
                        s2,
                        r,
                        moveDir.normalized,
                        out RaycastHit stepHit,
                        moveDist + skinWidth,
                        dashMask))
                {
                    Vector3 safePos = transform.position +
                                    moveDir.normalized * (stepHit.distance - skinWidth);

                    rb.MovePosition(safePos);
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

        rb.linearVelocity = dashDirection * postDashVelocity;

        pm.dashing = false;
        cam.DoFov(80f);
    }

    void GetCapsulePoints(out Vector3 p1, out Vector3 p2, out float radius)
    {
        radius = col.radius * Mathf.Abs(transform.localScale.x);

        float height = Mathf.Max(col.height * Mathf.Abs(transform.localScale.y), radius * 2f);
        float halfHeight = height / 2f - radius;

        Vector3 center = transform.TransformPoint(col.center);

        p1 = center + Vector3.up * halfHeight;
        p2 = center - Vector3.up * halfHeight;
    }

    bool IsDashDirectionBlocked(Vector3 dashDir)
    {
        GetCapsulePoints(out Vector3 p1, out Vector3 p2, out float radius);

        Collider[] hits = Physics.OverlapCapsule(
            p1,
            p2,
            radius + skinWidth,
            dashMask,
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider hit in hits)
        {
            if (hit == col) continue;

            if (Physics.ComputePenetration(
                col, transform.position, transform.rotation,
                hit, hit.transform.position, hit.transform.rotation,
                out Vector3 depenDir,
                out float depenDist))
            {
                // If penetration normal opposes dash direction, we're blocked
                if (Vector3.Dot(dashDir, depenDir) < -0.1f)
                    return true;
            }
        }

        return false;
    }
}
