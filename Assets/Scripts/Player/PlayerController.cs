using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviourPunCallbacks, IPunObservable
{
    public float gravity;
    
    [Header("Movement")] 
    public float walkSpeed;     public float runSpeed;
    private bool _isMoving;     private bool _isRunning;
    private bool _isGrounded;
    
    [Header("Crouching/Sliding")]
    public float crouchHeight = 0.5f;     public float standHeight = 2f;
    public float crouchSpeed;             public float slideSpeed;
    public float slideDuration = 1.0f;    private bool _isSliding;
    private bool _isCrouching;            private float _slideTimer;
    
    [Header("Jumping")] 
    public float jumpForce = 10.0f;
    public float maxFallSpeed = -30f;
    private bool _isJumpPressed;
    
    [Header("Latching")]
    public float latchGravity;
    private bool _isLatched;
    public float latchCheckDistance = 0.8f;
    public LayerMask latchLayers; 
    
    [Header("Camera")]
    public Transform cameraTransform;
    public float sensitivity = 100f;
    public float lookLimit = 90f;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private AudioListener playerAudioListener;
    
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

    [Header("Testing")] 
    public bool _testing;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _inputActions = new InputSystem_Actions();
        
        _camDefaultPos = cameraTransform.localPosition;
        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Start()
    {
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
        if (playerAudioListener == null) playerAudioListener = GetComponentInChildren<AudioListener>();

        if (!photonView.IsMine)
        {
            if (playerCamera != null) playerCamera.enabled = false;
            if (playerAudioListener != null) playerAudioListener.enabled = false;
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Send our position to others
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(_isCrouching);  // if you add crouch later
        }
        else
        {
            // Receive others' position
            transform.position = (Vector3)stream.ReceiveNext();
            transform.rotation = (Quaternion)stream.ReceiveNext();
            if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.PlayerCount > 1)  // skip first frame
                _isCrouching = (bool)stream.ReceiveNext();
        }
    }

    public override void OnEnable()
    {
        _inputActions.Player.Enable();
    }

    public override void OnDisable()
    {
        _inputActions.Player.Disable();
    }
    
    private void Update()
    {
        if (_testing||photonView.IsMine)
        {
            ReadInput(); // Read player input each frame (condition later for PUN)
            HandleMovement();
            HandleCameraLook();
        }
        
        HandleHeadBob();
    }
    
    private void ReadInput()
    {
        _moveInput = _inputActions.Player.Move.ReadValue<Vector2>();
        _lookInput = _inputActions.Player.Look.ReadValue<Vector2>();
        _isRunning = _inputActions.Player.Sprint.IsPressed(); 
        _isJumpPressed = _inputActions.Player.Jump.triggered;
    }
    
    private void HandleMovement()
    {
        _isGrounded = _cc.isGrounded;
        
        if (!_isGrounded && !_isLatched && CanLatch())
        {
            _isLatched = true;
            _velocity = Vector3.zero;           // stop falling instantly
        }

        if (_isGrounded)
        {
            _velocity.x = 0f;
            _velocity.z = 0f;
            _isLatched = false;               // touching ground cancels latch
        }

        if (_isLatched)
        {
            HandleLatchedMovement();
            return;                           // skip normal gravity while latched
        }
        
        // Calculate horizontal movement
        var move = transform.forward * _moveInput.y + transform.right * _moveInput.x;
        var currentSpeed = _isRunning ? runSpeed : walkSpeed;
        
        // Apply jump forces
        if ((_isGrounded || _isLatched) && _isJumpPressed)
        {
            _velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
        }
        
        // Apply gravity
        if (_isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f;
        }
        _velocity.y += gravity * Time.deltaTime;
        if (_velocity.y < maxFallSpeed) _velocity.y = maxFallSpeed;

        // Combine horizontal and vertical movement into one Move call
        Vector3 finalMove = (move * currentSpeed) + _velocity;
        _cc.Move(finalMove * Time.deltaTime);
    }

    private void HandleCameraLook()
    {
        float mouseX = 0f;
        float mouseY = 0f;

        // Check if input is from Mouse or Gamepad
        // Mouse delta is already frame-rate independent (pixels moved), so we shouldn't multiply by Time.deltaTime
        // Gamepad sticks are values (-1 to 1), so they need Time.deltaTime to be frame-rate independent
        bool isMouse = IsMouseInput();

        if (isMouse)
        {
            // For mouse, sensitivity scales pixels to degrees directly
            // You might need to lower sensitivity in inspector if it's too fast now
            float mouseSensitivityMultiplier = 0.1f; // Adjust this to normalize with gamepad feeling if needed
            mouseX = _lookInput.x * sensitivity * mouseSensitivityMultiplier;
            mouseY = _lookInput.y * sensitivity * mouseSensitivityMultiplier;
        }
        else
        {
            // For gamepad, we need time scaling
            mouseX = _lookInput.x * sensitivity * Time.deltaTime;
            mouseY = _lookInput.y * sensitivity * Time.deltaTime;
        }

        transform.Rotate(Vector3.up * mouseX);
        
        _xRotation -= mouseY;
        _xRotation = Mathf.Clamp(_xRotation, -lookLimit, lookLimit);
        cameraTransform.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);
    }

    private bool IsMouseInput()
    {
        if (_inputActions == null || _inputActions.Player.Look == null) return false;
        
        // This is a simple check. For more robust checking, you'd check the active control's device.
        // However, Input System's ReadValue returns the value from the checks above.
        // A common way to assume 'mouse' is if we are checking the delta action and the active control is a mouse.
        // Since we bind both to 'Look', let's check the last updated control.
        
        var control = _inputActions.Player.Look.activeControl;
        if (control != null && control.device is UnityEngine.InputSystem.Mouse)
        {
            return true;
        }
        
        return false;
    }

    private void HandleHeadBob()
    {
        _isMoving = _moveInput.sqrMagnitude > 0.001f;
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
            _bobTime = 0f;
            cameraTransform.localPosition = Vector3.Lerp(
                cameraTransform.localPosition,
                _camDefaultPos,
                Time.deltaTime * 8f
            );
        }
    }
    
    private bool CanLatch()
    {
        
        Vector3 origin = transform.position + Vector3.up * 1.2f;
        return Physics.Raycast(origin, transform.forward, latchCheckDistance, latchLayers);
    }

    private void HandleLatchedMovement()
    {
        // Apply latch gravity so player slowly slides down or stays attached with force
        _velocity.y += latchGravity * Time.deltaTime;
        if (_velocity.y < maxFallSpeed) _velocity.y = maxFallSpeed;
        _cc.Move(_velocity * Time.deltaTime);

        if (_isJumpPressed)
        {
            _isLatched = false;
            
            Vector3 jumpDir = ( -transform.forward + Vector3.up ).normalized;
            _velocity = jumpDir * Mathf.Sqrt(jumpForce * -2f * gravity);
        }
    }
}