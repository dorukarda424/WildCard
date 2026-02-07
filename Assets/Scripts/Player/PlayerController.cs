using Photon.Pun;
using UnityEngine;

namespace Player
{
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float speed = 5f;
        public float jumpForce = 7f;
        public float gravity = 20f;

        [Header("Mouse Look Settings")]
        public float mouseSensitivity = 100f;
        public Transform playerCamera;
        private float _xRotation;

        private PhotonView _view;
        private CharacterController _cc;
        private Vector3 _velocity;

        private void Awake()
        {
            _view = GetComponent<PhotonView>();
            _cc = GetComponent<CharacterController>();
        }

        private void Start()
        {
            if (!_view.IsMine)
            {
                if (playerCamera != null)
                    playerCamera.gameObject.SetActive(false);
                return;
            }

            Cursor.lockState = CursorLockMode.Locked;
        }

        private Vector3 _moveDirection;

        private void Update()
        {
            if (_view == null || !_view.IsMine)
                return;

            Look();
            Move();
        }

        private void FixedUpdate()
        {
            if (_view == null || !_view.IsMine)
                return;

            Move();
        }

        private void Look()
        {
            var mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
            var mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

            _xRotation -= mouseY;
            _xRotation = Mathf.Clamp(_xRotation, -90f, 90f);

            if (playerCamera != null)
                playerCamera.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);
        
            transform.Rotate(Vector3.up * mouseX);
        }

        private void Move()
        {
            if (_cc.isGrounded && _velocity.y < 0)
            {
                _velocity.y = -2f;
            }
            
            float x = Input.GetAxisRaw("Horizontal");
            float z = Input.GetAxisRaw("Vertical");
            
            Vector3 move = transform.right * x + transform.forward * z;
            _cc.Move(move * (speed * Time.deltaTime));

            if (Input.GetButtonDown("Jump"))
            {
                _velocity.y = Mathf.Sqrt(jumpForce * 2f * gravity);
            }
            
            _velocity.y -= gravity * Time.deltaTime;
            _cc.Move(_velocity * Time.deltaTime);
        }
    }
}