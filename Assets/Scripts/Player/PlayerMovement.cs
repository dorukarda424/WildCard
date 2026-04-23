using System;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviourPunCallbacks, IPunObservable
{
    public enum PlayerState { Idle, Walking, Sprinting, Crouching, Airborne, Latched, Sliding}
    public PlayerState CurrentState { get; private set; } = PlayerState.Idle;
    
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
    private List<Animator> _animators = new List<Animator>();
    private PlayerStats _stats;
    private Vector3 _velocity;
    private Vector2 _moveInput;
    private bool _isJumpPressed;
    private bool _isSprinting;
    private bool _isCrouching;
    private int _jumpsRemaining;
    private float _latchTimer;
    private float _airborneTime;
    private float _slideTimer;
    private Vector3 _slideDirection;
    private float _currentCrouchHeight;
    private float _currentCameraY;
    private Vector3 _latchDirection;
    
    private Vector3 _networkPosition;
    private float _networkRotationY;
    private bool _isRemotePlayer;
    
    private static readonly int IsSprinting = Animator.StringToHash("IsSprinting");
    private static readonly int IsCrouching = Animator.StringToHash("IsCrouching");
    private static readonly int IsAirborne = Animator.StringToHash("IsAirborne");
    private static readonly int IsLatched = Animator.StringToHash("IsLatched");
    private static readonly int IsAiming = Animator.StringToHash("IsAiming");
    private static readonly int IsSliding = Animator.StringToHash("IsSliding");
    
    private float EffWalkSpeed   => _stats.MoveSpeed;
    private float EffSprintSpeed => _stats.SprintSpeed;
    private float EffCrouchSpeed => _stats.CrouchSpeed;
    private float EffJumpForce   => _stats.JumpForce;
    private float EffGravity     => _stats.Gravity;
    private int   EffMaxJumps    => _stats.EffectiveMaxJumps;
    private float EffMaxFallSpeed=> _stats.MaxFallSpeed;
    private float EffSlideSpeed  => _stats.SlideSpeed;
    private float EffFallDamageThreshold => _stats.FallDamageThreshold;
    private float EffFallDamageMultiplier => _stats.FallDamageMultiplier;
    private float EffCrouchHeight => PlayerStats.CrouchHeight;
    private float EffStandHeight => PlayerStats.StandHeight;
    private float EffCrouchCameraY => PlayerStats.CrouchCameraY;
    private float EffStandCameraY => PlayerStats.StandCameraY;
    private float EffSlideDuration => PlayerStats.SlideDuration;
    private float EffCrouchTransitionSpeed => PlayerStats.CrouchTransitionSpeed;
    
    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _stats = GetComponent<PlayerStats>();
        _jumpsRemaining = EffMaxJumps;
        _currentCrouchHeight = EffStandHeight;
        _currentCameraY = EffStandCameraY;
        
        RefreshAnimators();
    }

    public void RefreshAnimators()
    {
        _animators.Clear();
        Animator[] foundAnimators = GetComponentsInChildren<Animator>(true); // Include inactive
        foreach (Animator a in foundAnimators)
        {
            if (a.runtimeAnimatorController != null)
            {
                _animators.Add(a);
                Debug.Log($"[PlayerMovement] Registered Animator: {a.runtimeAnimatorController.name} on {a.gameObject.name}");
            }
        }
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
        else
        {
            // Set initial camera height immediately to avoid ground-level spawn frames
            if (camHolder != null)
            {
                _currentCameraY = EffStandCameraY;
                camHolder.position = transform.position + Vector3.up * _currentCameraY;
            }

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
            UpdateAnimator(Vector3.zero, false); // fixed
            return;
        }

        if (CurrentState == PlayerState.Sliding)
        {
            HandleSlidingMovement();
            UpdateAnimator(_slideDirection, true);
            return;
        }

        bool wasGrounded = IsGrounded;
        IsGrounded = CheckGrounded();

        if (!wasGrounded && IsGrounded)
        {
            float fallSpeed = Mathf.Abs(_velocity.y);
            if (fallSpeed > EffFallDamageThreshold)
            {
                float damage = (fallSpeed - EffFallDamageThreshold) * EffFallDamageMultiplier;
                var health = GetComponent<PlayerHealth>();
                if (health != null) health.TakeDamageLocal(damage);
                Debug.Log($"[PlayerMovement] Fall damage: {damage} (FallSpeed: {fallSpeed})");
            }

            _velocity.x = 0f;
            _velocity.z = 0f;
            _airborneTime = 0f;
            OnPlayerLand?.Invoke();
        }

        if (IsGrounded) _jumpsRemaining = EffMaxJumps;
        if (!IsGrounded) _airborneTime += Time.deltaTime;
        else _airborneTime = 0f;

        if (!IsGrounded && _latchTimer <= 0 && _airborneTime > 0.15f && CanLatch())
        {
            CurrentState = PlayerState.Latched;
            _velocity = Vector3.zero;
            OnPlayerLatch?.Invoke();
            UpdateAnimator(Vector3.zero, false); // fixed
            return;
        }

        Vector3 moveDir = (camHolder != null)
            ? camHolder.forward * _moveInput.y + camHolder.right * _moveInput.x
            : transform.forward * _moveInput.y + transform.right * _moveInput.x;

        moveDir.y = 0f;

        // FIXED: check before normalize
        bool hasInput = moveDir.sqrMagnitude > 0.01f;
        if (hasInput) moveDir.Normalize();
        IsMoving = hasInput;

        HandleCrouchingHeight();

        if (IsGrounded)
        {
            if (_isCrouching && _isSprinting && IsMoving && CurrentState != PlayerState.Sliding)
            {
                StartSlide(moveDir);
                return;
            }

            if (!IsMoving && !_isCrouching) CurrentState = PlayerState.Idle;
            else if (_isCrouching)          CurrentState = PlayerState.Crouching;
            else if (_isSprinting)          CurrentState = PlayerState.Sprinting;
            else                            CurrentState = PlayerState.Walking;
        }
        else if (CurrentState != PlayerState.Latched)
        {
            CurrentState = PlayerState.Airborne;
        }

        float speed = CurrentState switch
        {
            PlayerState.Sprinting => EffSprintSpeed,
            PlayerState.Crouching => EffCrouchSpeed,
            _                     => EffWalkSpeed
        };

        if (_isJumpPressed && _jumpsRemaining > 0)
        {
            _velocity.y = Mathf.Sqrt(EffJumpForce * -2f * EffGravity);
            _jumpsRemaining--;
            _latchTimer = latchCooldown;
            OnPlayerJump?.Invoke();
        }
        _isJumpPressed = false;

        if (IsGrounded) { if (_velocity.y < 0) _velocity.y = -2f; }
        else
        {
            _velocity.y += EffGravity * Time.deltaTime;
            if (_velocity.y < EffMaxFallSpeed) _velocity.y = EffMaxFallSpeed;
        }

        _cc.Move((moveDir * speed + _velocity) * Time.deltaTime);
        UpdateAnimator(moveDir, hasInput); // fixed
    }
    
    private void UpdateAnimator(Vector3 moveDirWorld, bool hasInput)
    {
        if (_animators.Count == 0) return;

        Vector3 local = transform.InverseTransformDirection(moveDirWorld);
        float moveX = hasInput ? local.x : 0f;
        float moveY = hasInput ? local.z : 0f;
        float speed = hasInput ? Mathf.Clamp01(new Vector2(moveX, moveY).magnitude) : 0f;
        bool isAiming = InputManager.Instance != null && InputManager.Instance.IsAiming;

        foreach (Animator anim in _animators)
        {
            if (anim == null) continue;

            if (!hasInput)
            {
                anim.SetFloat("MoveX", 0f);
                anim.SetFloat("MoveY", 0f);
                anim.SetFloat("Speed", 0f);
            }
            else
            {
                anim.SetFloat("MoveX", moveX, 0.1f, Time.deltaTime);
                anim.SetFloat("MoveY", moveY, 0.1f, Time.deltaTime);
                anim.SetFloat("Speed", speed, 0.1f, Time.deltaTime);
            }

            anim.SetBool("IsGrounded", IsGrounded);
            anim.SetBool("IsCrouching", CurrentState == PlayerState.Crouching);
            anim.SetBool("IsLatched", CurrentState == PlayerState.Latched);
            anim.SetBool("IsAirborne", CurrentState == PlayerState.Airborne);
            anim.SetBool("IsSprinting", CurrentState == PlayerState.Sprinting);
            anim.SetBool("IsSliding", CurrentState == PlayerState.Sliding);
            anim.SetBool("IsAiming", isAiming);
        }
    }

    private void HandleCrouchingHeight()
    {
        bool targetCrouch = _isCrouching || CurrentState == PlayerState.Sliding;
        float targetHeight = targetCrouch ? EffCrouchHeight : EffStandHeight;
        float targetCamY = targetCrouch ? EffCrouchCameraY : EffStandCameraY;

        _currentCrouchHeight = Mathf.MoveTowards(_currentCrouchHeight, targetHeight, Time.deltaTime * EffCrouchTransitionSpeed);
        _cc.height = _currentCrouchHeight;
        // Keep the bottom of the character at the same position
        _cc.center = new Vector3(0, _currentCrouchHeight / 2f, 0);

        if (camHolder != null)
        {
            // If we are in the editor and testing, we might want to see changes to baseStandCameraY instantly
            if (Application.isEditor && testing)
            {
                _currentCameraY = targetCamY;
            }
            else
            {
                _currentCameraY = Mathf.MoveTowards(_currentCameraY, targetCamY, Time.deltaTime * EffCrouchTransitionSpeed);
            }
            
            Vector3 camPos = transform.position + Vector3.up * _currentCameraY;
            camHolder.position = camPos;
        }
    }

    private void StartSlide(Vector3 direction)
    {
        CurrentState = PlayerState.Sliding;
        _slideTimer = EffSlideDuration;
        _slideDirection = direction;
        // Keep some vertical velocity if we were falling slightly
        _velocity.x = 0;
        _velocity.z = 0;
    }

    private void HandleSlidingMovement()
    {
        _slideTimer -= Time.deltaTime;
        
        // Horizontal movement
        float speed = EffSlideSpeed * (_slideTimer / EffSlideDuration);
        Vector3 move = _slideDirection * speed;

        // Gravity
        if (IsGrounded) { if (_velocity.y < 0) _velocity.y = -2f; }
        else
        {
            _velocity.y += EffGravity * Time.deltaTime;
            if (_velocity.y < EffMaxFallSpeed) _velocity.y = EffMaxFallSpeed;
        }

        _cc.Move((move + _velocity) * Time.deltaTime);

        if (_slideTimer <= 0 || !IsGrounded)
        {
            CurrentState = IsGrounded ? PlayerState.Crouching : PlayerState.Airborne;
        }

        // Jump to cancel slide
        if (_isJumpPressed && _jumpsRemaining > 0)
        {
            _velocity.y = Mathf.Sqrt(EffJumpForce * -2f * EffGravity);
            _jumpsRemaining--;
            CurrentState = PlayerState.Airborne;
            _isJumpPressed = false;
            OnPlayerJump?.Invoke();
        }

        HandleCrouchingHeight();
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
