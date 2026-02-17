using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviourPunCallbacks, IPunObservable, IDamageable
{
    public float gravity;

    [Header("Health")]
    public float maxHealth = 100f;
    private float _currentHealth;
    
    [Header("Movement")]
    public float walkSpeed;
    public float runSpeed;
    private bool _isMoving;
    private bool _isRunning;
    private bool _isGrounded;

    [Header("Crouching/Sliding")]
    public float crouchHeight = 0.5f;
    public float standHeight = 2f;
    public float crouchSpeed;
    public float slideSpeed;
    public float slideDuration = 1.0f;
    private bool _isSliding;
    private bool _isCrouching;
    private float _slideTimer;

    [Header("Jumping")]
    public float jumpForce = 10.0f;
    public float maxFallSpeed = -30f;
    private bool _isJumpPressed;
    private bool _wasGroundedLastFrame;

    [Header("Latching")]
    public float latchGravity;
    private bool _isLatched;
    public float latchCheckDistance = 0.8f;
    public LayerMask latchLayers;

    [Header("Camera")]
    public Transform cameraTransform;
    public Transform camHolder;
    public float sensitivity = 2f;
    public float lookLimit = 90f;

    [Header("Head Bob")]
    public float bobAmplitude = 0.05f;
    public float bobFrequency = 12f;
    private float _bobTime;
    private Vector3 _camDefaultPos;

    [Header("References")]
    private CharacterController _cc;
    private InputSystem_Actions _inputActions;
    private Vector2 _moveInput;
    private Vector2 _lookInput;
    private Vector3 _velocity;
    private float _xRotation;
    private float _yRotation;
    private Vector3 _networkPosition;

    public bool testing;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _inputActions = new InputSystem_Actions();

        _inputActions.Player.Move.performed += ctx => _moveInput = ctx.ReadValue<Vector2>();
        _inputActions.Player.Move.canceled += ctx => _moveInput = Vector2.zero;

        _inputActions.Player.Look.performed += ctx => _lookInput = ctx.ReadValue<Vector2>();
        _inputActions.Player.Look.canceled += ctx => _lookInput = Vector2.zero;

        _inputActions.Player.Sprint.performed += ctx => _isRunning = true;
        _inputActions.Player.Sprint.canceled += ctx => _isRunning = false;

        _inputActions.Player.Crouch.performed += ctx => _isCrouching = true;
        _inputActions.Player.Crouch.canceled += ctx => _isCrouching = false;

        _inputActions.Player.Jump.performed += ctx => _isJumpPressed = true;
        _inputActions.Player.Jump.canceled += ctx => _isJumpPressed = false;

        _camDefaultPos = cameraTransform.localPosition;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Start()
    {

        if (!testing&&!photonView.IsMine)
        {
            Camera cam = cameraTransform.GetComponent<Camera>();
            AudioListener listener = cameraTransform.GetComponent<AudioListener>();

            if (cam != null) cam.enabled = false;
            if (listener != null) listener.enabled = false;
        }
        if(testing) photonView.enabled = false;
        _currentHealth = maxHealth;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(_yRotation);
            stream.SendNext(_xRotation);
            stream.SendNext(_isCrouching);
        }
        else
        {
            // Simple interpolation could be added here for remote players
            _networkPosition = (Vector3)stream.ReceiveNext();
            _yRotation = (float)stream.ReceiveNext();
            camHolder.localRotation = Quaternion.Euler(0f, _yRotation, 0f);
            _xRotation = (float)stream.ReceiveNext();
            cameraTransform.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);
            _isCrouching = (bool)stream.ReceiveNext();
        }
    }

    public override void OnEnable()
    {
        base.OnEnable();
        _inputActions.Player.Enable();
    }

    public override void OnDisable()
    {
        base.OnDisable();
        _inputActions.Player.Disable();
    }

    public void OnDestroy()
    {
        _inputActions.Dispose();
    }

    private void Update()
    {
        if (!testing && !photonView.IsMine)
        {
            transform.position = Vector3.Lerp(transform.position, _networkPosition, Time.deltaTime * 10f);
            return;
        }
        HandleHeadBob();
    }

    private void FixedUpdate()
    {
        if (!testing && !photonView.IsMine) return;
        HandleMovement();
    }

    private void LateUpdate()
    {
        if (testing || photonView.IsMine) HandleCameraLook();
    }



    private void HandleMovement()
    {
        if (_isLatched)
        {
            HandleLatchedMovement();
            return;
        }

        _isGrounded = CheckGrounded();

        if (!_isGrounded && !_isLatched && CanLatch())
        {
            _isLatched = true;
            _velocity = Vector3.zero;
        }

        if (_isGrounded)
        {
            if (!_wasGroundedLastFrame)
            {
                _velocity.x = 0f;
                _velocity.z = 0f;
            }
        }
        _wasGroundedLastFrame = _isGrounded;

        Vector3 movementDirection = camHolder.forward * _moveInput.y + camHolder.right * _moveInput.x;
        movementDirection.y = 0f;
        if (movementDirection.sqrMagnitude > 0.01f) movementDirection.Normalize();
        var currentSpeed = _isRunning ? runSpeed : walkSpeed;

        if ((_isGrounded || _isLatched) && _isJumpPressed)
        {
            _velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
            _isJumpPressed = false; // Reset jump after applying force in FixedUpdate
        }

        if (_isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f;
        }
        _velocity.y += gravity * Time.fixedDeltaTime;
        if (_velocity.y < maxFallSpeed) _velocity.y = maxFallSpeed;
        Vector3 finalMove = movementDirection * currentSpeed + _velocity;
        _cc.Move(finalMove * Time.fixedDeltaTime);
    }

    private void HandleCameraLook()
    {
        float mouseX = _lookInput.x * sensitivity;
        float mouseY = _lookInput.y * sensitivity;
        _yRotation += mouseX;
        camHolder.localRotation = Quaternion.Euler(0f, _yRotation, 0f);
        _xRotation -= mouseY;
        _xRotation = Mathf.Clamp(_xRotation, -lookLimit, lookLimit);
        cameraTransform.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);
    }

    private void HandleHeadBob()
    {
        _isMoving = _cc.velocity.sqrMagnitude > 0.01f;
        if (_isMoving && _isGrounded)
        {
            _bobTime += Time.deltaTime * bobFrequency * (_isRunning ? 1.5f : 1f);

            var verticalOffset = Mathf.Sin(_bobTime) * bobAmplitude;
            var horizontalOffset = Mathf.Cos(_bobTime * 0.5f) * bobAmplitude * 0.5f;

            Vector3 targetPos = _camDefaultPos + new Vector3(horizontalOffset, verticalOffset, 0f);
            cameraTransform.localPosition = Vector3.Lerp(
            cameraTransform.localPosition,
            targetPos,
            Time.deltaTime * 10f
        );
        }
        else
        {
            _bobTime = Mathf.Lerp(_bobTime, 0f, Time.deltaTime * 5f);
            cameraTransform.localPosition = Vector3.Lerp(
            cameraTransform.localPosition,
            _camDefaultPos,
            Time.deltaTime * 8f
            );
        }
    }

    private bool CanLatch()
    {
        Vector3 origin = transform.position + Vector3.up * 1.5f;

        // Check Front
        if (Physics.Raycast(origin, transform.forward, latchCheckDistance, latchLayers)) return true;

        // Check Left
        if (Physics.Raycast(origin, -transform.right, latchCheckDistance, latchLayers)) return true;

        // Check Right
        if (Physics.Raycast(origin, transform.right, latchCheckDistance, latchLayers)) return true;

        return false;
    }

    private void HandleLatchedMovement()
    {
        if (CheckGrounded())
        {
            _isLatched = false;
            _velocity.x = 0f;
            _velocity.z = 0f;
            return;
        }

        _velocity.y += latchGravity * Time.fixedDeltaTime;
        if (_velocity.y < maxFallSpeed) _velocity.y = maxFallSpeed;
        _cc.Move(_velocity * Time.fixedDeltaTime);

        if (_isJumpPressed)
        {
            _isLatched = false;

            Vector3 jumpDir = ( -camHolder.forward + Vector3.up ).normalized;
            _velocity = jumpDir * Mathf.Sqrt(jumpForce * -2f * gravity);
            _isJumpPressed = false;
        }
    }

    private bool CheckGrounded()
    {
        Vector3 origin = transform.position;
        float checkDistance = (_cc.height / 2f) + 0.1f;

        bool grounded = Physics.Raycast(origin, Vector3.down, checkDistance, ~latchLayers);

        Debug.DrawRay(origin, Vector3.down * checkDistance, grounded ? Color.blue : Color.yellow);

        return grounded;
    }

    [PunRPC]
    public void TakeDamage(float damage, int attackerViewID)
    {
        // Only the owner processes health changes
        if (!photonView.IsMine) return;

        _currentHealth -= damage;
        Debug.Log($"Player {photonView.ViewID} took {damage} damage from {attackerViewID}. HP: {_currentHealth}/{maxHealth}");

        if (_currentHealth <= 0f)
        {
            Die(attackerViewID);
        }
    }
    
    private void Die(int killerViewID)
    {
        Debug.Log($"Player {photonView.ViewID} killed by {killerViewID}");
        
    }
}