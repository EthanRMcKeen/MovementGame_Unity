using UnityEngine;

[DisallowMultipleComponent]
public class NetworkPlayerDash : MonoBehaviour
{
    [Header("Dash")]
    [SerializeField] private float _dashDistance = 25f;
    [SerializeField] private float _dashTime = 0.25f;
    [SerializeField] private float _dashCooldown = 1.5f;
    [SerializeField] private float _dashPostVelocity = 20f;
    [SerializeField] private float _dashSkinWidth = 0.08f;
    [SerializeField] private LayerMask _dashMask = ~0;
    [SerializeField] private bool _dashUsesCameraForward = true;
    [SerializeField] private bool _dashOnlyUpward = true;
    [SerializeField] private bool _dashAllowAllDirections = false;

    private NetworkPlayerMotor _motor;
    private NetworkPlayerInputState _input;

    private bool _isDashing;
    private float _dashCooldownTimer;
    private float _dashElapsed;
    private Vector3 _dashStartPosition;
    private Vector3 _dashTargetPosition;
    private Vector3 _dashDirection;

    public bool IsDashing { get { return _isDashing; } }

    public void Initialize(NetworkPlayerMotor motor, NetworkPlayerInputState input)
    {
        _motor = motor;
        _input = input;
    }

    public void TickCooldown(float deltaTime)
    {
        _dashCooldownTimer = Mathf.Max(0f, _dashCooldownTimer - deltaTime);
    }

    public bool TryStart()
    {
        if (_motor == null || _input == null || !_input.DashPressedThisStep)
            return false;
        if (_dashCooldownTimer > 0f || _isDashing)
            return false;

        Vector3 dashDirection = GetDashDirection();
        if (dashDirection.sqrMagnitude < 0.0001f || IsDashDirectionBlocked(dashDirection))
            return false;

        _dashDirection = dashDirection;
        _dashStartPosition = _motor.transform.position;
        _dashTargetPosition = GetDashTargetPosition(dashDirection);
        _dashElapsed = 0f;
        _dashCooldownTimer = _dashCooldown;
        _isDashing = true;
        _motor.SetCrouchCollider(false);
        _motor.VerticalVelocity = 0f;
        _motor.SetVelocity(Vector3.zero);
        return true;
    }

    public void TickDash(float deltaTime)
    {
        if (!_isDashing || _motor == null)
            return;

        _dashElapsed += deltaTime;
        float dashDuration = Mathf.Max(0.01f, _dashTime);
        float t = Mathf.Clamp01(_dashElapsed / dashDuration);
        Vector3 nextPosition = Vector3.Lerp(_dashStartPosition, _dashTargetPosition, t);
        Vector3 moveDirection = nextPosition - _motor.transform.position;

        if (moveDirection.sqrMagnitude > 0.0001f)
        {
            float moveDistance = moveDirection.magnitude;
            GetCapsulePoints(out Vector3 p1, out Vector3 p2, out float radius);

            if (Physics.CapsuleCast(
                p1,
                p2,
                radius,
                moveDirection.normalized,
                out RaycastHit hit,
                moveDistance + _dashSkinWidth,
                _dashMask,
                QueryTriggerInteraction.Ignore))
            {
                Vector3 safePosition = _motor.transform.position + (moveDirection.normalized * Mathf.Max(0f, hit.distance - _dashSkinWidth));
                _motor.Body.MovePosition(safePosition);
                FinishDash(true);
                return;
            }
        }

        _motor.Body.MovePosition(nextPosition);
        if (t >= 1f)
            FinishDash(false);
    }

    public void ForceStop()
    {
        _isDashing = false;
    }

    private void FinishDash(bool collided)
    {
        _isDashing = false;
        _motor.VerticalVelocity = 0f;
        _motor.SetVelocity(collided
            ? Vector3.zero
            : new Vector3(_dashDirection.x * _dashPostVelocity, 0f, _dashDirection.z * _dashPostVelocity));
    }

    private Vector3 GetDashDirection()
    {
        Transform viewTransform = (_dashUsesCameraForward && _motor.PlayerCameraComponent != null && _motor.PlayerCameraComponent.CameraHolderTransform != null)
            ? _motor.PlayerCameraComponent.CameraHolderTransform
            : _motor.transform;

        Vector3 forward = (_dashUsesCameraForward && _motor.PlayerCameraComponent != null && _motor.PlayerCameraComponent.CameraHolderTransform != null)
            ? ((_dashOnlyUpward) ? new Vector3(viewTransform.forward.x, Mathf.Max(0f, viewTransform.forward.y), viewTransform.forward.z).normalized : viewTransform.forward.normalized) // Include upward component if _dashUsesCameraForward is true
            : Vector3.ProjectOnPlane(viewTransform.forward, Vector3.up).normalized;

        Vector3 right = Vector3.ProjectOnPlane(viewTransform.right, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.0001f)
            forward = _motor.transform.forward;
        if (right.sqrMagnitude < 0.0001f)
            right = _motor.transform.right;

        Vector3 direction = _dashAllowAllDirections
            ? (forward * _input.MoveInput.y) + (right * _input.MoveInput.x)
            : forward;

        if (direction.sqrMagnitude < 0.0001f)
            direction = forward;

        return direction.normalized;
    }

    private Vector3 GetDashTargetPosition(Vector3 dashDirection)
    {
        Vector3 startPosition = _motor.transform.position - (dashDirection * _dashSkinWidth);
        Vector3 targetPosition = startPosition + (dashDirection * Mathf.Max(_dashDistance, _dashSkinWidth));

        GetCapsulePoints(out Vector3 p1, out Vector3 p2, out float radius);
        if (Physics.CapsuleCast(
            p1,
            p2,
            radius,
            dashDirection,
            out RaycastHit hit,
            _dashDistance,
            _dashMask,
            QueryTriggerInteraction.Ignore))
        {
            float clampedDistance = Mathf.Max(0f, hit.distance - _dashSkinWidth);
            targetPosition = startPosition + (dashDirection * clampedDistance);
        }

        return targetPosition;
    }

    private void GetCapsulePoints(out Vector3 p1, out Vector3 p2, out float radius)
    {
        radius = _motor.Capsule.radius * Mathf.Abs(_motor.transform.localScale.x);
        float height = Mathf.Max(_motor.Capsule.height * Mathf.Abs(_motor.transform.localScale.y), radius * 2f);
        float halfHeight = (height * 0.5f) - radius;
        Vector3 center = _motor.transform.TransformPoint(_motor.Capsule.center);

        p1 = center + (Vector3.up * halfHeight);
        p2 = center - (Vector3.up * halfHeight);
    }

    private bool IsDashDirectionBlocked(Vector3 dashDirection)
    {
        GetCapsulePoints(out Vector3 p1, out Vector3 p2, out float radius);
        Collider[] hits = Physics.OverlapCapsule(
            p1,
            p2,
            radius + _dashSkinWidth,
            _dashMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null || hit == _motor.Capsule)
                continue;

            if (!Physics.ComputePenetration(
                _motor.Capsule,
                _motor.transform.position,
                _motor.transform.rotation,
                hit,
                hit.transform.position,
                hit.transform.rotation,
                out Vector3 depenetrationDirection,
                out float _))
            {
                continue;
            }

            if (Vector3.Dot(dashDirection, depenetrationDirection) < -0.1f)
                return true;
        }

        return false;
    }
}
