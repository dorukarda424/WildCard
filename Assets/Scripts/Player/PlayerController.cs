using UnityEngine;
//using Photon.Pun;

namespace Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public float gravity;
        
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
        
        [Header("Latching")]
        public float latchGravity;
        private bool _isLatched;
        public float latchCheckDistance = 0.8f;
        public LayerMask latchLayers; 
        
        [Header("Camera")]
        public Transform cameraTransform;
        public float sensitivity = 100f;
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

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _inputActions = new InputSystem_Actions();
            
            _camDefaultPos = cameraTransform.localPosition;
            
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnEnable()
        {
            _inputActions.Player.Enable();
        }
        
        private void OnDisable()
        {
            _inputActions.Player.Disable();
        }
        
        private void Update()
        {
            ReadInput(); // Read player input each frame (condition later for PUN)
            HandleMovement();
            HandleCameraLook();
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
            
            var move = transform.forward * _moveInput.y + transform.right * _moveInput.x;
            var currentSpeed = _isRunning ? runSpeed : walkSpeed;
            _cc.Move(move * (currentSpeed * Time.deltaTime));
            
            if ((_isGrounded || _isLatched) && _isJumpPressed)
            {
                _velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
            }
            
            if (_isGrounded && _velocity.y < 0)
            {
                _velocity.y = -2f;
            }
            _velocity.y += gravity * Time.deltaTime;
            if (_velocity.y < maxFallSpeed) _velocity.y = maxFallSpeed;
            _cc.Move(_velocity * Time.deltaTime);
        }
        private void HandleCameraLook()
        {
            var mouseX = _lookInput.x * sensitivity * Time.deltaTime;
            transform.Rotate(Vector3.up* mouseX);
            
            var mouseY = _lookInput.y * sensitivity * Time.deltaTime;
            _xRotation -= mouseY;
            _xRotation = Mathf.Clamp(_xRotation, -lookLimit, lookLimit);
            cameraTransform.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);
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
            if (_isJumpPressed)
            {
                _isLatched = false;
                
                Vector3 jumpDir = ( -transform.forward + Vector3.up ).normalized;
                _velocity = jumpDir * Mathf.Sqrt(jumpForce * -2f * gravity);
            }
        }
    }
}