using Photon.Pun;
using UnityEngine;

public class InputManager : MonoBehaviourPunCallbacks
{
    public static InputManager Instance;

    public InputSystem_Actions InputActions { get; private set; }

    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsCrouching { get; private set; }
    public bool IsJumpPressed { get; private set; }
    public bool IsShooting { get; private set; }
    public bool IsReloadPressed { get; private set; }

    private void Awake()
    {
        // Only the local player should run InputManager
        PhotonView pv = GetComponent<PhotonView>();
        if (pv != null && !pv.IsMine)
        {
            // Remote player: destroy only this component, NOT the gameObject!
            Destroy(this);
            return;
        }

        if (Instance != null && Instance != this)
        {
            // Duplicate local InputManager: destroy only this component
            Destroy(this);
            return;
        }

        Instance = this;

        InputActions = new InputSystem_Actions();

        InputActions.Player.Move.performed += ctx => MoveInput = ctx.ReadValue<Vector2>();
        InputActions.Player.Move.canceled += ctx => MoveInput = Vector2.zero;

        InputActions.Player.Look.performed += ctx => LookInput = ctx.ReadValue<Vector2>();
        InputActions.Player.Look.canceled += ctx => LookInput = Vector2.zero;

        InputActions.Player.Sprint.performed += ctx => IsRunning = true;
        InputActions.Player.Sprint.canceled += ctx => IsRunning = false;

        InputActions.Player.Crouch.performed += ctx => IsCrouching = true;
        InputActions.Player.Crouch.canceled += ctx => IsCrouching = false;

        InputActions.Player.Jump.performed += ctx => IsJumpPressed = true;

        InputActions.Player.Attack.performed += ctx => IsShooting = true;
        InputActions.Player.Attack.canceled += ctx => IsShooting = false;

        InputActions.Player.Reload.performed += ctx => IsReloadPressed = true;
        InputActions.Player.Reload.canceled += ctx => IsReloadPressed = false;
    }

    public override void OnEnable() => InputActions?.Enable();
    public override void OnDisable() => InputActions?.Disable();

    
    public void ConsumeJump() => IsJumpPressed = false;
    
    public void ResetReload() => IsReloadPressed = false;
    
    public void SetInputEnabled(bool enabled)
    {
        if (enabled) InputActions?.Enable();
        else InputActions?.Disable();
    }
}
