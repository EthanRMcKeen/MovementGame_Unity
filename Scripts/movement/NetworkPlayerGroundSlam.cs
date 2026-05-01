using UnityEngine;

[DisallowMultipleComponent]
public class NetworkPlayerGroundSlam : MonoBehaviour
{
    [Header("Ground Slam")]
    [SerializeField] private float _slamForce = 18f;
    [SerializeField] private float _slamCooldown = 1.5f;
    [SerializeField] private float _superSlamHoldTime = 0.2f;
    [SerializeField] private float _superSlamForceMultiplier = 1.5f;

    private NetworkPlayerMotor _motor;
    private NetworkPlayerInputState _input;

    private float _slamCooldownTimer;
    private bool _isSlamming;
    private bool _isSuperSlamming;

    public bool IsSlamming { get { return _isSlamming; } }
    public bool IsSuperSlamming { get { return _isSuperSlamming; } }
    public bool IsActive { get { return _isSlamming || _isSuperSlamming; } }

    public void Initialize(NetworkPlayerMotor motor, NetworkPlayerInputState input)
    {
        _motor = motor;
        _input = input;
    }

    public void TickCooldown(float deltaTime)
    {
        _slamCooldownTimer = Mathf.Max(0f, _slamCooldownTimer - deltaTime);
    }

    public bool TickSlam()
    {
        if (_motor == null || _input == null)
            return false;

        if (!IsActive)
        {
            if (!_input.CrouchPressedThisStep || _motor.IsGrounded || _slamCooldownTimer > 0f)
                return false;

            StartSlam(false);
        }

        if (_motor.IsGrounded)
        {
            ForceStop();
            return false;
        }

        if (!_isSuperSlamming && _input.CrouchHeld && _input.CrouchHoldTime >= _superSlamHoldTime)
            StartSlam(true);

        Vector3 velocity = _motor.Body.linearVelocity;
        if (_isSuperSlamming)
            velocity = Vector3.zero;

        _motor.SetVelocity(new Vector3(
            _isSuperSlamming ? 0f : velocity.x,
            _motor.VerticalVelocity,
            _isSuperSlamming ? 0f : velocity.z));

        return true;
    }

    public void ForceStop()
    {
        _isSlamming = false;
        _isSuperSlamming = false;
    }

    private void StartSlam(bool superSlam)
    {
        if (superSlam && _isSuperSlamming)
            return;
        if (!superSlam && IsActive)
            return;

        if (!IsActive)
            _slamCooldownTimer = _slamCooldown;

        _isSlamming = !superSlam;
        _isSuperSlamming = superSlam;

        if (superSlam)
            _motor.SetVelocity(Vector3.zero);
        else
            _motor.SetVelocity(new Vector3(_motor.Body.linearVelocity.x, 0f, _motor.Body.linearVelocity.z));

        _motor.VerticalVelocity = -_slamForce * (superSlam ? _superSlamForceMultiplier : 1f);
    }
}
