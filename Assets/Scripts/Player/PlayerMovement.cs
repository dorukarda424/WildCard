using System;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviourPunCallbacks, IPunObservable
{
    public enum PlayerState { Idle, Walking, Sprinting, Crouching, Airborne, Latched}
    public PlayerState CurrentState { get; private set; } = PlayerState.Idle;
    
    [Header("Latching")]
    public LayerMask latchMask;
    public float latchDistance = 0.8f;
    public float latchGravity = -2f;
    public float latchCooldown = 0.2f;
    
    [Header("References")]
    public Transform camHolder;
    [SerializeField] private Animator animator;

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
    
    private Vector3 _networkPosition;
    private float _networkRotationY;
    private bool _isRemotePlayer;
    private HashSet<string> _validAnimParams;
    private bool _animParamsCached;

    private static readonly int Hash_isGrounded = Animator.StringToHash("isGrounded");
    private static readonly int Hash_isSprinting = Animator.StringToHash("isSprinting");
    private static readonly int Hash_isCrouching = Animator.StringToHash("isCrouching");
    private static readonly int Hash_isAirborne = Animator.StringToHash("isAirborne");
    private static readonly int Hash_isLatched = Animator.StringToHash("isLatched");
    private static readonly int Hash_isAiming = Animator.StringToHash("isAiming");
    
    private float EffWalkSpeed   => _stats.MoveSpeed;
    private float EffSprintSpeed => _stats.SprintSpeed;
    private float EffCrouchSpeed => _stats.CrouchSpeed;
    private float EffJumpForce   => _stats.JumpForce;
    private float EffGravity     => _stats.Gravity;
    private int   EffMaxJumps    => _stats.EffectiveMaxJumps;
    private float EffMaxFallSpeed=> _stats.MaxFallSpeed;
    
    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _stats = GetComponent<PlayerStats>();
        _jumpsRemaining = EffMaxJumps;

        // Find the Animator that actually drives the mesh (on child model),
        // NOT the root Animator. GetComponentInChildren checks self first,
        // so we manually prefer the child.
        Animator rootAnimator = GetComponent<Animator>();
        Animator childAnimator = null;

        var allAnimators = GetComponentsInChildren<Animator>();
        foreach (var anim in allAnimators)
        {
            if (anim.gameObject != gameObject) // It's on a child, not root
            {
                childAnimator = anim;
                break;
            }
        }

        // Prefer child Animator (where the mesh/bones are)
        // Fall back to root Animator if no child found
        _animator = childAnimator != null ? childAnimator : rootAnimator;
        animator = _animator; // Keep serialized field in sync

        if (_animator != null)
            Debug.Log($"[PlayerMovement] Using Animator on '{_animator.gameObject.name}' (child={childAnimator != null})");
        else
            Debug.LogWarning("[PlayerMovement] No Animator found on player!");
    }

    private void Start()
    {
        // If we're not in a Photon room (direct scene entry / offline), this is always the local player
        _isRemotePlayer = !testing
                       && PhotonNetwork.InRoom
                       && photonView != null
                       && !photonView.IsMine;

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
        else
        {
            DynamicCrosshair crosshair = FindObjectOfType<DynamicCrosshair>();
            if (crosshair != null)
            {
                PlayerMovement movement = GetComponent<PlayerMovement>();
                PlayerCombat combat = GetComponent<PlayerCombat>();
                crosshair.SetPlayer(movement, combat);
            }
        }
    }

    private void Update()
    {
        if (_isRemotePlayer)
        {
            transform.position = Vector3.Lerp(transform.position, _networkPosition, Time.deltaTime * 15f);
            float smoothY = Mathf.LerpAngle(transform.eulerAngles.y, _networkRotationY, Time.deltaTime * 15f);
            transform.rotation = Quaternion.Euler(0f, smoothY, 0f);
            return;
        }

        // Use the cached _isRemotePlayer from Start() — don't recalculate!
        // PhotonNetwork.InRoom can change mid-game when Launcher connects.
        if (_isRemotePlayer)
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

        // Movement runs in Update (not FixedUpdate) so the CharacterController moves at
        // the same rate as the camera — eliminates the physics-tick stutter.
        if (_latchTimer > 0) _latchTimer -= Time.deltaTime;
        HandleMovement();
    }

    // FixedUpdate is no longer used for local player movement to avoid camera stutter.
    // Remote player interpolation still uses Update above.
    
    public void SetState(PlayerState newState) => CurrentState = newState;

    /// <summary>
    /// Called by PlayerCamera after spawning the camera at runtime.
    /// </summary>
    public void SetCamHolder(Transform cam) => camHolder = cam;
    
    private void HandleMovement()
{
    if (CurrentState == PlayerState.Latched)
    {
        HandleLatchedMovement();
        UpdateAnimator(Vector3.zero);
        return;
    }

    bool wasGrounded = IsGrounded;
    IsGrounded = CheckGrounded();

    if (!wasGrounded && IsGrounded)
    {
        _velocity.x = 0f;
        _velocity.z = 0f;
        _airborneTime = 0f;
        OnPlayerLand?.Invoke();
    }

    if (IsGrounded)
        _jumpsRemaining = EffMaxJumps;

    if (!IsGrounded) _airborneTime += Time.deltaTime;
    else _airborneTime = 0f;

    if (!IsGrounded && _latchTimer <= 0 && _airborneTime > 0.15f && CanLatch())
    {
        CurrentState = PlayerState.Latched;
        _velocity = Vector3.zero;
        OnPlayerLatch?.Invoke();
        UpdateAnimator(Vector3.zero);
        return;
    }

    // for movement direction
    Vector3 moveDir = (camHolder != null)
        ? camHolder.forward * _moveInput.y + camHolder.right * _moveInput.x
        : transform.forward * _moveInput.y + transform.right * _moveInput.x;

    moveDir.y = 0f;
    if (moveDir.sqrMagnitude > 0.01f) moveDir.Normalize();
    IsMoving = moveDir.sqrMagnitude > 0.01f;

    // to decide state from inputs
    if (IsGrounded)
    {
        if (!IsMoving && !_isCrouching)      CurrentState = PlayerState.Idle;
        else if (_isCrouching)               CurrentState = PlayerState.Crouching;
        else if (_isSprinting)               CurrentState = PlayerState.Sprinting;
        else                                 CurrentState = PlayerState.Walking;
    }
    else if (CurrentState != PlayerState.Latched)
    {
        CurrentState = PlayerState.Airborne;
    }

    float speed = CurrentState switch
    {
        PlayerState.Sprinting => EffSprintSpeed,
        PlayerState.Crouching => EffCrouchSpeed,
        PlayerState.Walking   => EffWalkSpeed,
        _                     => EffWalkSpeed
    };

    // for jump
    if (_isJumpPressed && _jumpsRemaining > 0)
    {
        _velocity.y = Mathf.Sqrt(EffJumpForce * -2f * EffGravity);
        _jumpsRemaining--;
        _latchTimer = latchCooldown;
        OnPlayerJump?.Invoke();
    }
    _isJumpPressed = false;

    // gravity
    if (IsGrounded)
    {
        if (_velocity.y < 0) _velocity.y = -2f;
    }
    else
    {
        _velocity.y += EffGravity * Time.deltaTime;
        if (_velocity.y < EffMaxFallSpeed) _velocity.y = EffMaxFallSpeed;
    }

    _cc.Move((moveDir * speed + _velocity) * Time.deltaTime);
    UpdateAnimator(moveDir);

}
    
    private void UpdateAnimator(Vector3 moveDirWorld)
    {
        if (_animator == null) return;
        EnsureAnimParamsCached();

        Vector3 local = transform.InverseTransformDirection(moveDirWorld);
        float moveX = local.x;
        float moveY = local.z;
        float speed01 = Mathf.Clamp01(new Vector2(moveX, moveY).magnitude);

        SafeSetFloat("MoveX", moveX, 0.1f);
        SafeSetFloat("MoveY", moveY, 0.1f);
        SafeSetFloat("Speed", speed01, 0.1f);

        SafeSetBool("isGrounded", IsGrounded);
        SafeSetBool("isCrouching", CurrentState == PlayerState.Crouching);
        SafeSetBool("isLatched", CurrentState == PlayerState.Latched);
        SafeSetBool("isAirborne", CurrentState == PlayerState.Airborne);
        SafeSetBool("isSprinting", CurrentState == PlayerState.Sprinting);
        SafeSetBool("isAiming", InputManager.Instance != null && InputManager.Instance.IsAiming);
    }

    private void EnsureAnimParamsCached()
    {
        if (_animParamsCached) return;

        _validAnimParams = new HashSet<string>();
        if (_animator != null && _animator.runtimeAnimatorController != null)
        {
            foreach (var param in _animator.parameters)
                _validAnimParams.Add(param.name);

            // Only mark as cached if we actually found parameters
            if (_validAnimParams.Count > 0)
            {
                _animParamsCached = true;
                Debug.Log($"[PlayerMovement] Cached {_validAnimParams.Count} animator params: {string.Join(", ", _validAnimParams)}");
            }
        }
    }

    private void SafeSetBool(string paramName, bool value)
    {
        if (_validAnimParams != null && _validAnimParams.Contains(paramName))
            _animator.SetBool(paramName, value);
    }

    private void SafeSetFloat(string paramName, float value, float dampTime)
    {
        if (_validAnimParams != null && _validAnimParams.Contains(paramName))
            _animator.SetFloat(paramName, value, dampTime, Time.deltaTime);
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
        
        _velocity.y += latchGravity * Time.deltaTime;
        if (_velocity.y < EffMaxFallSpeed) _velocity.y = EffMaxFallSpeed;
        _cc.Move(_velocity * Time.deltaTime);
        
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
        _velocity.y += EffGravity * Time.deltaTime;
        if (_velocity.y < EffMaxFallSpeed) _velocity.y = EffMaxFallSpeed;
        _cc.Move(_velocity * Time.deltaTime);
    }

    /// <summary>
    /// Resets the remaining jump count to the current max. Call after respawn.
    /// </summary>
    public void ResetJumps()
    {
        _jumpsRemaining = EffMaxJumps;
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
        return _cc.isGrounded;
    }

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
