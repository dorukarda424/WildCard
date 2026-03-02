using System;
using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviourPunCallbacks
{
    public enum PlayerState { Idle, Walking, Sprinting, Crouching, Airborne, Latched, Stunned, Frozen }
    public PlayerState CurrentState { get; private set; } = PlayerState.Idle;
    
    [Header("Movement")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 10f;
    public float crouchSpeed = 2.5f;
    
    [Header("Jumping")]
    public float jumpForce = 10f;
    public float maxFallSpeed = -30f;
    public int maxJumps = 1;
    
    [Header("Physics")]
    public float gravity = -20f;
    
    [Header("Latching")]
    public LayerMask latchMask;
    public float latchDistance = 0.8f;
    public float latchGravity = -2f;
    public float latchCooldown = 0.2f;
    
    [Header("References")]
    public Transform camHolder;

    [Header("Debug")]
    public bool testing;
    
    public event Action OnPlayerJump;
    public event Action OnPlayerLand;
    public event Action OnPlayerLatch;
    public event Action OnPlayerUnlatch;
    
    public bool IsGrounded { get; private set; }
    public bool IsMoving { get; private set; }
    public Vector3 Velocity => _velocity;
    
    private CharacterController _cc;
    private Vector3 _velocity;
    private Vector2 _moveInput;
    private bool _isJumpPressed;
    private bool _isSprinting;
    private bool _isCrouching;
    private int _jumpsRemaining;
    private float _latchTimer;
    private float _airborneTime;
    private Vector3 _latchDirection;
    private Vector3 _networkPosition;
    
    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _jumpsRemaining = maxJumps;
    }

    private void Update()
    {
        if (!testing && (photonView == null || !photonView.IsMine))
        {
            transform.position = Vector3.Lerp(transform.position, _networkPosition, Time.deltaTime * 10f);
            return;
        }
        
        if (InputManager.Instance != null)
        {
            _moveInput = InputManager.Instance.MoveInput;
            _isSprinting = InputManager.Instance.IsRunning;
            _isCrouching = InputManager.Instance.IsCrouching;

            if (InputManager.Instance.IsJumpPressed)
            {
                _isJumpPressed = true;
                InputManager.Instance.ConsumeJump();
            }
        }
    }

    private void FixedUpdate()
    {
        if (!testing && (photonView == null || !photonView.IsMine)) return;
        if (_latchTimer > 0) _latchTimer -= Time.fixedDeltaTime;
        HandleMovement();
    }
    
    public void SetState(PlayerState newState) => CurrentState = newState;
    
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
        IsGrounded = CheckGrounded();
        
        if (!wasGrounded && IsGrounded)
        {
            _velocity.x = 0f;
            _velocity.z = 0f;
            _jumpsRemaining = maxJumps;
            _airborneTime = 0f;
            OnPlayerLand?.Invoke();
        }
        
        if (!IsGrounded)
            _airborneTime += Time.fixedDeltaTime;
        else
            _airborneTime = 0f;
        
        if (!IsGrounded && _latchTimer <= 0 && _airborneTime > 0.15f && CanLatch())
        {
            CurrentState = PlayerState.Latched;
            _velocity = Vector3.zero;
            OnPlayerLatch?.Invoke();
            return;
        }
        
        Vector3 moveDir = camHolder.forward * _moveInput.y + camHolder.right * _moveInput.x;
        moveDir.y = 0f;
        if (moveDir.sqrMagnitude > 0.01f) moveDir.Normalize();
        IsMoving = moveDir.sqrMagnitude > 0.01f;
        
        float speed = _isCrouching ? crouchSpeed : _isSprinting ? sprintSpeed : walkSpeed;
        
        if (IsGrounded)
        {
            if (!IsMoving) CurrentState = PlayerState.Idle;
            else if (_isSprinting) CurrentState = PlayerState.Sprinting;
            else if (_isCrouching) CurrentState = PlayerState.Crouching;
            else CurrentState = PlayerState.Walking;
        }
        else CurrentState = PlayerState.Airborne;
        
        if (_isJumpPressed && _jumpsRemaining > 0)
        {
            _velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
            _jumpsRemaining--;
            _latchTimer = latchCooldown;
            _isJumpPressed = false;
            OnPlayerJump?.Invoke();
        }
        
        if (IsGrounded)
        {
            if (_velocity.y < 0) _velocity.y = -2f;
        }
        else
        {
            _velocity.y += gravity * Time.fixedDeltaTime;
            if (_velocity.y < maxFallSpeed) _velocity.y = maxFallSpeed;
        }

        _cc.Move((moveDir * speed + _velocity) * Time.fixedDeltaTime);
        _isJumpPressed = false;
    }

    private void HandleLatchedMovement()
    {
        if (CheckGrounded())
        {
            _velocity = Vector3.zero;
            _jumpsRemaining = maxJumps;
            CurrentState = PlayerState.Idle;
            OnPlayerUnlatch?.Invoke();
            return;
        }
        
        _velocity.y += latchGravity * Time.fixedDeltaTime;
        if (_velocity.y < maxFallSpeed) _velocity.y = maxFallSpeed;
        _cc.Move(_velocity * Time.fixedDeltaTime);
        
        if (_isJumpPressed)
        {
            _latchTimer = latchCooldown;
            CurrentState = PlayerState.Airborne;
            OnPlayerUnlatch?.Invoke();

            Vector3 inputDir = camHolder.forward * _moveInput.y + camHolder.right * _moveInput.x;
            Vector3 jumpDir = inputDir.sqrMagnitude > 0.01f
                ? (inputDir.normalized + Vector3.up).normalized
                : (_latchDirection + Vector3.up).normalized;

            _velocity = jumpDir * Mathf.Sqrt(jumpForce * -2f * gravity);
            _isJumpPressed = false;
            OnPlayerJump?.Invoke();
        }
    }

    private void ApplyGravityOnly()
    {
        if (IsGrounded && _velocity.y < 0) _velocity.y = -2f;
        _velocity.y += gravity * Time.fixedDeltaTime;
        if (_velocity.y < maxFallSpeed) _velocity.y = maxFallSpeed;
        _cc.Move(_velocity * Time.fixedDeltaTime);
    }
    
    private bool CanLatch()
    {
        Vector3 origin = transform.position + Vector3.up * 1.5f;
        RaycastHit hit;

        if (Physics.Raycast(origin, transform.forward, out hit, latchDistance, latchMask) ||
            Physics.Raycast(origin, -transform.right, out hit, latchDistance, latchMask) ||
            Physics.Raycast(origin, transform.right, out hit, latchDistance, latchMask))
        {
            _latchDirection = hit.normal;
            return true;
        }
        return false;
    }

    private bool CheckGrounded()
    {
        // float checkDistance = (_cc.height / 2f) + 0.1f;
        // bool grounded = Physics.Raycast(transform.position, Vector3.down, checkDistance, ~latchMask);
        // Debug.DrawRay(transform.position, Vector3.down * checkDistance, grounded ? Color.blue : Color.yellow);
        return _cc.isGrounded;
    }
    
}
