using UnityEngine;
using Photon.Pun;

public class PlayerCamera : MonoBehaviourPunCallbacks
{
    [Header("References")]
    public Transform playerBody;
    public Transform cameraHolder;
    public Vector3 camOffset;

    [Header("Look Settings")]
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 85f;

    [Header("Head Bob")]
    public float bobFrequency = 10f;
    public float bobAmplitude = 0.05f;
    public float bobReturnSpeed = 5f;

    [Header("Recoil")]
    public float recoilReturnSpeed = 5f;

    [Header("Debug")]
    public bool testing;

    // Private
    private float _xRotation;
    private float _bobTimer;
    private Vector3 _bobOffset;
    private Vector3 _originalCamLocalPos;
    private float _recoilOffset;
    private Camera _cam;
    private PlayerMovement _movement;

    private void Awake()
    {
        _cam = GetComponentInChildren<Camera>();
        _movement = GetComponent<PlayerMovement>();

        if (_cam != null)
            _originalCamLocalPos = _cam.transform.localPosition;
    }

    private void Start()
    {
        if (testing || (photonView == null || photonView.IsMine))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void LateUpdate()
    {
        if (!testing || (photonView == null || !photonView.IsMine)) return;
        PlayerFollow();
        HandleLook();
        HandleHeadBob();
        HandleRecoilReturn();
    }
    private void PlayerFollow()
    {
        if (playerBody == null) return;
        cameraHolder.position = playerBody.position + camOffset;
    }
    
    private void HandleLook()
    {
        if (InputManager.Instance == null) return;

        Vector2 lookInput = InputManager.Instance.LookInput * mouseSensitivity * Time.deltaTime;
        
        _xRotation -= lookInput.y;
        _xRotation = Mathf.Clamp(_xRotation, -maxLookAngle, maxLookAngle);
        
        //cameraHolder.localRotation = Quaternion.Euler(_xRotation + _recoilOffset, 0f, 0f);
        
        playerBody.Rotate(Vector3.up * lookInput.x);
        cameraHolder.rotation = Quaternion.Euler(_xRotation+_recoilOffset,playerBody.eulerAngles.y,0);
    }
    
    private void HandleHeadBob()
    {
        if (_cam == null) return;

        bool isMoving = _movement != null && _movement.IsMoving && _movement.IsGrounded;

        if (isMoving)
        {
            
            float speed = _movement.CurrentState == PlayerMovement.PlayerState.Sprinting ? 1.5f : 1f;
            _bobTimer += Time.deltaTime * bobFrequency * speed;

            _bobOffset = new Vector3(
                Mathf.Sin(_bobTimer * 0.5f) * bobAmplitude,
                Mathf.Sin(_bobTimer) * bobAmplitude,
                0f
            );
        }
        else
        {
            
            _bobTimer = 0f;
            _bobOffset = Vector3.Lerp(_bobOffset, Vector3.zero, Time.deltaTime * bobReturnSpeed);
        }

        _cam.transform.localPosition = _originalCamLocalPos + _bobOffset;
    }

    
    public void AddRecoil(float amount)
    {
        _recoilOffset -= amount;
    }

    private void HandleRecoilReturn()
    {
        if (_recoilOffset != 0f)
        {
            _recoilOffset = Mathf.Lerp(_recoilOffset, 0f, Time.deltaTime * recoilReturnSpeed);
            if (Mathf.Abs(_recoilOffset) < 0.01f) _recoilOffset = 0f;
        }
    }
    
    public void SetSensitivity(float value) => mouseSensitivity = value;

    public void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
