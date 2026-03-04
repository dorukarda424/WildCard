using System;
using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviourPunCallbacks, IPunObservable
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
    private Animator _animator;
    private PlayerStats _stats;
    private Vector3 _velocity;
    private Vector2 _moveInput;
    private bool _isJumpPressed;
    private bool _isSprinting;
    private bool _isCrouching;
    private int _jumpsRemaining;
    private float _latchTimer;
    private float _airborneTime;
    private Vector3 _latchDirection;

    // ── Network sync fields ──
    private Vector3 _networkPosition;
    private float _networkRotationY;
    private bool _isRemotePlayer;

    private static readonly int IsWalking = Animator.StringToHash("IsWalking");

    // ── Effective stats: read from PlayerStats if available, fallback to inspector values ──
    private float EffWalkSpeed   => _stats != null ? _stats.MoveSpeed    : walkSpeed;
    private float EffSprintSpeed => _stats != null ? _stats.SprintSpeed  : sprintSpeed;
    private float EffCrouchSpeed => _stats != null ? _stats.CrouchSpeed  : crouchSpeed;
    private float EffJumpForce   => _stats != null ? _stats.JumpForce    : jumpForce;
    private float EffGravity     => _stats != null ? _stats.Gravity      : gravity;
    private int   EffMaxJumps    => _stats != null ? _stats.EffectiveMaxJumps : maxJumps;
    private float EffMaxFallSpeed=> _stats != null ? _stats.MaxFallSpeed : maxFallSpeed;
    
    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _animator = GetComponentInChildren<Animator>();
        _stats = GetComponent<PlayerStats>();
        _jumpsRemaining = EffMaxJumps;
    }

    private void Start()
    {
        _isRemotePlayer = !testing && photonView != null && !photonView.IsMine;

        if (_isRemotePlayer)
        {
            // Disable CharacterController on remote players entirely.
            // CC blocks direct transform.position changes, breaking network sync.
            // But we need a collider for bullet hits, so add a matching CapsuleCollider.
            CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.center = _cc.center;
            capsule.radius = _cc.radius;
            capsule.height = _cc.height;

            _cc.enabled = false;

            _networkPosition = transform.position;
            _networkRotationY = transform.eulerAngles.y;
            Debug.Log($"[PlayerMovement] Remote player initialized at {transform.position}");
            StartCoroutine(VisibilityDebugRoutine());
        }
    }

    private void Update()
    {
        if (_isRemotePlayer)
        {
            // Smoothly interpolate remote player to network position/rotation
            transform.position = Vector3.Lerp(transform.position, _networkPosition, Time.deltaTime * 15f);
            float smoothY = Mathf.LerpAngle(transform.eulerAngles.y, _networkRotationY, Time.deltaTime * 15f);
            transform.rotation = Quaternion.Euler(0f, smoothY, 0f);
            return;
        }

        if (!testing && (photonView == null || !photonView.IsMine))
        {
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

    /// <summary>
    /// Called by PlayerCamera after spawning the camera at runtime.
    /// </summary>
    public void SetCamHolder(Transform cam) => camHolder = cam;
    
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
            _jumpsRemaining = EffMaxJumps;
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
        
        Vector3 moveDir;
        if (camHolder != null)
        {
            moveDir = camHolder.forward * _moveInput.y + camHolder.right * _moveInput.x;
        }
        else
        {
            moveDir = transform.forward * _moveInput.y + transform.right * _moveInput.x;
        }
        moveDir.y = 0f;
        if (moveDir.sqrMagnitude > 0.01f) moveDir.Normalize();
        IsMoving = moveDir.sqrMagnitude > 0.01f;

        // Animator
        if (_animator != null)
            _animator.SetBool(IsWalking, IsMoving && IsGrounded);
        
        float speed = _isCrouching ? EffCrouchSpeed : _isSprinting ? EffSprintSpeed : EffWalkSpeed;
        
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
            _velocity.y = Mathf.Sqrt(EffJumpForce * -2f * EffGravity);
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
            _velocity.y += EffGravity * Time.fixedDeltaTime;
            if (_velocity.y < EffMaxFallSpeed) _velocity.y = EffMaxFallSpeed;
        }

        _cc.Move((moveDir * speed + _velocity) * Time.fixedDeltaTime);
        _isJumpPressed = false;
    }

    private void HandleLatchedMovement()
    {
        if (CheckGrounded())
        {
            _velocity = Vector3.zero;
            _jumpsRemaining = EffMaxJumps;
            CurrentState = PlayerState.Idle;
            OnPlayerUnlatch?.Invoke();
            return;
        }
        
        _velocity.y += latchGravity * Time.fixedDeltaTime;
        if (_velocity.y < EffMaxFallSpeed) _velocity.y = EffMaxFallSpeed;
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

            _velocity = jumpDir * Mathf.Sqrt(EffJumpForce * -2f * EffGravity);
            _isJumpPressed = false;
            OnPlayerJump?.Invoke();
        }
    }

    private void ApplyGravityOnly()
    {
        if (IsGrounded && _velocity.y < 0) _velocity.y = -2f;
        _velocity.y += EffGravity * Time.fixedDeltaTime;
        if (_velocity.y < EffMaxFallSpeed) _velocity.y = EffMaxFallSpeed;
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

    // ────────── Network Sync ──────────

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.eulerAngles.y);
        }
        else
        {
            _networkPosition = (Vector3)stream.ReceiveNext();
            _networkRotationY = (float)stream.ReceiveNext();
        }
    }

    private System.Collections.IEnumerator VisibilityDebugRoutine()
    {
        while (true)
        {
            if (_isRemotePlayer)
            {
                var renderers = GetComponentsInChildren<Renderer>();
                int enabledCount = 0;
                foreach (var r in renderers)
                {
                    if (r.enabled) enabledCount++;
                }

                Debug.Log($"[VisibilityDebug] {gameObject.name}: Pos={transform.position}, Scale={transform.localScale}, Renderers={enabledCount}/{renderers.Length} enabled, ActiveSelf={gameObject.activeSelf}");
            }
            yield return new WaitForSeconds(5f);
        }
    }
}
