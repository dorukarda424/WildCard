using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

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
    public bool IsAiming { get; private set; }

    // Ability inputs (polled directly from keyboard)
    public bool IsAbility1Pressed { get; private set; }  // Z — Black Hole
    public bool IsAbility2Pressed { get; private set; }  // Q — Teleport

    private void Awake()
    {
        PhotonView pv = GetComponent<PhotonView>();
        if (pv != null && !pv.IsMine)
        {
            Destroy(this);
            return;
        }

        if (Instance != null && Instance != this)
        {
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
        
        InputActions.Player.ADS.performed += ctx => IsAiming = true;
        InputActions.Player.ADS.canceled  += ctx => IsAiming = false;
    }

    public override void OnEnable() => InputActions?.Enable();
    public override void OnDisable() => InputActions?.Disable();

    private void Update()
    {
        // Poll ability keys directly (avoids needing to regenerate InputSystem_Actions)
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.zKey.wasPressedThisFrame) IsAbility1Pressed = true;
            if (kb.qKey.wasPressedThisFrame) IsAbility2Pressed = true;
        }
    }

    public void ConsumeJump() => IsJumpPressed = false;
    
    public void ResetReload() => IsReloadPressed = false;

    public void ConsumeAbility1() => IsAbility1Pressed = false;
    public void ConsumeAbility2() => IsAbility2Pressed = false;
    
    public void SetInputEnabled(bool enabled)
    {
        if (enabled) InputActions?.Enable();
        else InputActions?.Disable();
    }
}
