using System;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerStats))]
public class PlayerController : MonoBehaviourPunCallbacks, IPunObservable
{
    // ────────── State Machine ──────────

    public enum PlayerState { Idle, Walking, Sprinting, Crouching, Airborne, Latched, Stunned, Frozen }
    public PlayerState CurrentState { get; private set; } = PlayerState.Idle;

    private PlayerStats _playerStats;
    public float gravity;
    
    [Header("Movement")] 
    public float walkSpeed;     public float runSpeed;
    private bool _isRunning;
    private bool _isCrouching;
    
    [Header("Crouching/Sliding")]
    public float crouchHeight = 0.5f;     public float standHeight = 2f;
    public float crouchSpeed;             public float slideSpeed;
    public float slideDuration = 1.0f;    private bool _isSliding;
    private float _slideTimer;
    
    [Header("Jumping")] 
    public float jumpForce = 10.0f;
    public float maxFallSpeed = -30f;
    public int maxJumps = 1;
    private bool _isJumpPressed;
    private int _jumpsRemaining;
    
    [Header("Latching")]
    public float latchGravity;
    private bool _isLatched;
    public float latchCheckDistance = 0.8f;
    public LayerMask latchLayers;
    public float latchCooldown = 0.2f;
    private float _latchTimer;
    private float _airborneTime;
    private Vector3 _latchDirection;

    [Header("Testing")] 
    public bool _testing;

    // ────────── Internal ──────────

    private CharacterController _cc;
    private Vector2 _moveInput;
    private Vector3 _velocity;
    private bool _hasDoubleJumped;

    public bool IsGrounded { get; private set; }
    public bool IsMoving { get; private set; }
    public Vector3 Velocity => _velocity;

    // ────────── Events ──────────

    public event Action OnPlayerJump;
    public event Action OnPlayerLand;
    public event Action OnPlayerLatch;
    public event Action OnPlayerUnlatch;

    // ────────── Lifecycle ──────────

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _playerStats = GetComponent<PlayerStats>();
        _jumpsRemaining = maxJumps;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(_isCrouching);
        }
        else
        {
            transform.position = (Vector3)stream.ReceiveNext();
            transform.rotation = (Quaternion)stream.ReceiveNext();
            if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.PlayerCount > 1)
                _isCrouching = (bool)stream.ReceiveNext();
        }
    }
    
    private void Update()
    {
        if (_testing || (photonView != null && photonView.IsMine))
        {
            ReadInput();
            HandleMovement();
        }
    }
    
    // ────────── Input ──────────

    private void ReadInput()
    {
        if (InputManager.Instance != null)
        {
            _moveInput = InputManager.Instance.MoveInput;
            _isRunning = InputManager.Instance.IsRunning;
            _isCrouching = InputManager.Instance.IsCrouching;

            if (InputManager.Instance.IsJumpPressed)
            {
                _isJumpPressed = true;
                InputManager.Instance.ConsumeJump();
            }
        }
    }
    
    // ────────── State Helpers ──────────

    public void SetState(PlayerState newState) => CurrentState = newState;

    // ────────── Movement ──────────

    private void HandleMovement()
    {
        if (CurrentState == PlayerState.Stunned || CurrentState == PlayerState.Frozen)
        {
            ApplyGravityOnly();
            return;
        }

        if (CurrentState == PlayerState.Latched)
        {
            HandleLatchedMovement();
            return;
        }

        bool wasGrounded = IsGrounded;
        IsGrounded = _cc.isGrounded;
        
        // Landing detection
        if (!wasGrounded && IsGrounded)
        {
            _velocity.x = 0f;
            _velocity.z = 0f;
            _isLatched = false;
            _jumpsRemaining = maxJumps;
            _hasDoubleJumped = false;
            _airborneTime = 0f;
            OnPlayerLand?.Invoke();
        }

        // Track time in air
        if (!IsGrounded)
            _airborneTime += Time.deltaTime;
        else
            _airborneTime = 0f;

        // Latch check — multi-directional (forward, left, right)
        if (!IsGrounded && !_isLatched && _latchTimer <= 0 && _airborneTime > 0.15f && CanLatch())
        {
            _isLatched = true;
            CurrentState = PlayerState.Latched;
            _velocity = Vector3.zero;
            OnPlayerLatch?.Invoke();
            return;
        }

        if (IsGrounded)
        {
            _isLatched = false;
        }

        if (_latchTimer > 0) _latchTimer -= Time.deltaTime;
        
        // Calculate horizontal movement
        var move = transform.forward * _moveInput.y + transform.right * _moveInput.x;
        float statsSpeed = _playerStats != null ? _playerStats.MoveSpeed : walkSpeed;
        var currentSpeed = _isRunning ? statsSpeed * 1.5f : statsSpeed;

        IsMoving = _moveInput.sqrMagnitude > 0.001f;

        // Update state
        if (IsGrounded)
        {
            if (!IsMoving) CurrentState = PlayerState.Idle;
            else if (_isRunning) CurrentState = PlayerState.Sprinting;
            else if (_isCrouching) CurrentState = PlayerState.Crouching;
            else CurrentState = PlayerState.Walking;

            _hasDoubleJumped = false;
        }
        else
        {
            CurrentState = PlayerState.Airborne;
        }

        // Jump (with DoubleJump card support)
        if (_isJumpPressed)
        {
            if (IsGrounded || _isLatched)
            {
                _velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
                _jumpsRemaining--;
                _latchTimer = latchCooldown;
                _isJumpPressed = false;
                OnPlayerJump?.Invoke();
            }
            else if (!_hasDoubleJumped && _playerStats != null && _playerStats.HasEffect(SpecialEffect.DoubleJump))
            {
                _velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
                _hasDoubleJumped = true;
                _isJumpPressed = false;
                OnPlayerJump?.Invoke();
            }
        }
        
        // Apply gravity
        if (IsGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f;
        }
        _velocity.y += gravity * Time.deltaTime;
        if (_velocity.y < maxFallSpeed) _velocity.y = maxFallSpeed;

        // Combine horizontal and vertical movement
        Vector3 finalMove = (move * currentSpeed) + _velocity;
        _cc.Move(finalMove * Time.deltaTime);
        _isJumpPressed = false;
    }

    // ────────── Latched Movement ──────────

    private void HandleLatchedMovement()
    {
        if (_cc.isGrounded)
        {
            _velocity = Vector3.zero;
            _isLatched = false;
            _jumpsRemaining = maxJumps;
            CurrentState = PlayerState.Idle;
            OnPlayerUnlatch?.Invoke();
            return;
        }

        _velocity.y += latchGravity * Time.deltaTime;
        if (_velocity.y < maxFallSpeed) _velocity.y = maxFallSpeed;
        _cc.Move(_velocity * Time.deltaTime);

        if (_isJumpPressed)
        {
            _isLatched = false;
            _latchTimer = latchCooldown;
            CurrentState = PlayerState.Airborne;
            OnPlayerUnlatch?.Invoke();

            // Jump away — use input direction or wall normal
            Vector3 inputDir = transform.forward * _moveInput.y + transform.right * _moveInput.x;
            Vector3 jumpDir = inputDir.sqrMagnitude > 0.01f
                ? (inputDir.normalized + Vector3.up).normalized
                : (_latchDirection + Vector3.up).normalized;

            _velocity = jumpDir * Mathf.Sqrt(jumpForce * -2f * gravity);
            _isJumpPressed = false;
            OnPlayerJump?.Invoke();
        }
    }

    // ────────── Gravity Only (Stunned/Frozen) ──────────

    private void ApplyGravityOnly()
    {
        if (IsGrounded && _velocity.y < 0) _velocity.y = -2f;
        _velocity.y += gravity * Time.deltaTime;
        if (_velocity.y < maxFallSpeed) _velocity.y = maxFallSpeed;
        _cc.Move(_velocity * Time.deltaTime);
    }

    // ────────── Latch Detection (Multi-Directional) ──────────
    
    private bool CanLatch()
    {
        Vector3 origin = transform.position + Vector3.up * 1.2f;
        RaycastHit hit;

        // Check forward, left, and right
        if (Physics.Raycast(origin, transform.forward, out hit, latchCheckDistance, latchLayers) ||
            Physics.Raycast(origin, -transform.right, out hit, latchCheckDistance, latchLayers) ||
            Physics.Raycast(origin, transform.right, out hit, latchCheckDistance, latchLayers))
        {
            _latchDirection = hit.normal;
            return true;
        }
        return false;
    }
}