using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class NetworkPlayerInputState : MonoBehaviour
{
    private PlayerInput _playerInput;
    private InputAction _jumpAction;
    private InputAction _crouchAction;

    public Vector2 MoveInput { get; private set; }
    public bool JumpHeld { get; private set; }
    public bool JumpPressedThisStep { get; private set; }
    public bool CrouchHeld { get; private set; }
    public bool CrouchPressedThisStep { get; private set; }
    public bool CrouchReleasedThisStep { get; private set; }
    public bool DashPressedThisStep { get; private set; }
    public float CrouchHoldTime { get; private set; }

    private void Awake()
    {
        _playerInput = GetComponent<PlayerInput>();
        if (_playerInput == null || _playerInput.actions == null)
            return;

        _jumpAction = _playerInput.actions["Jump"];
        _crouchAction = _playerInput.actions["Crouch"];
    }

    public void OnMove(InputValue value)
    {
        if (GameUiEscapeMenuController.IsPauseMenuOpen)
        {
            MoveInput = Vector2.zero;
            return;
        }

        MoveInput = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
    }

    public void OnCrouch(InputValue value)
    {
    }

    public void OnDash(InputValue value)
    {
        if (GameUiEscapeMenuController.IsPauseMenuOpen)
            return;

        if (value.isPressed)
            DashPressedThisStep = true;
    }

    private void Update()
    {
        if (GameUiEscapeMenuController.IsPauseMenuOpen)
        {
            MoveInput = Vector2.zero;
            JumpHeld = false;
            JumpPressedThisStep = false;
            CrouchHeld = false;
            CrouchPressedThisStep = false;
            CrouchReleasedThisStep = false;
            DashPressedThisStep = false;
            CrouchHoldTime = 0f;
            return;
        }

        if (_playerInput == null || !_playerInput.enabled)
        {
            MoveInput = Vector2.zero;
            JumpHeld = false;
            CrouchHeld = false;
            CrouchHoldTime = 0f;
            return;
        }

        bool jumpHeld = IsActionPressed(_jumpAction);
        if (jumpHeld && !JumpHeld)
            JumpPressedThisStep = true;
        JumpHeld = jumpHeld;

        bool crouchHeld = IsActionPressed(_crouchAction);
        if (crouchHeld && !CrouchHeld)
        {
            CrouchPressedThisStep = true;
            CrouchHoldTime = 0f;
        }
        else if (!crouchHeld && CrouchHeld)
        {
            CrouchReleasedThisStep = true;
            CrouchHoldTime = 0f;
        }

        CrouchHeld = crouchHeld;

        if (CrouchHeld)
            CrouchHoldTime += Time.deltaTime;
    }

    public void ClearTransientFlags()
    {
        JumpPressedThisStep = false;
        CrouchPressedThisStep = false;
        CrouchReleasedThisStep = false;
        DashPressedThisStep = false;

        if (!CrouchHeld)
            CrouchHoldTime = 0f;
    }

    private static bool IsActionPressed(InputAction action)
    {
        return action != null && action.IsPressed();
    }
}
