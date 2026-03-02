using UnityEngine;
using Photon.Pun;

public class PlayerCamera : MonoBehaviourPunCallbacks
{
    [Header("Camera Prefab")]
    [SerializeField] private GameObject cameraHolderPrefab;

    [Header("Settings")]
    public Vector3 camOffset = new Vector3(0f, 0.8f, 0f);
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

    // Runtime references (set after spawn)
    private Transform _cameraHolder;
    private Camera _cam;
    private PlayerMovement _movement;
    private float _xRotation;
    private float _bobTimer;
    private Vector3 _bobOffset;
    private Vector3 _originalCamLocalPos;
    private float _recoilOffset;

    private void Awake()
    {
        _movement = GetComponent<PlayerMovement>();
    }

    private void Start()
    {
        // Only spawn camera for local player (or testing)
        if (testing || (photonView != null && photonView.IsMine))
        {
            SpawnCamera();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void SpawnCamera()
    {
        if (cameraHolderPrefab == null)
        {
            Debug.LogError("[PlayerCamera] cameraHolderPrefab atanmamış!");
            return;
        }

        // Kamerayı player'dan bağımsız olarak spawn et
        GameObject camObj = Instantiate(cameraHolderPrefab, transform.position + camOffset, Quaternion.identity);
        camObj.name = "CameraHolder_Local";

        _cameraHolder = camObj.transform;
        _cam = camObj.GetComponentInChildren<Camera>();

        if (_cam != null)
        {
            _originalCamLocalPos = _cam.transform.localPosition;
        }

        // Diğer oyuncuların kameralarını kapat
        DisableOtherCameras();

        // PlayerMovement'a kamera referansını ver (hareket yönü için)
        if (_movement != null)
        {
            _movement.SetCamHolder(_cameraHolder);
        }

        Debug.Log("[PlayerCamera] Kamera spawn edildi!");
    }

    private void DisableOtherCameras()
    {
        // Sahndeki diğer kameraları kapat (menu camera vs)
        Camera[] allCameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (var cam in allCameras)
        {
            if (cam != _cam)
            {
                cam.enabled = false;
                AudioListener listener = cam.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = false;
            }
        }
    }

    private void LateUpdate()
    {
        if (!testing && (photonView == null || !photonView.IsMine)) return;
        if (_cameraHolder == null) return;

        PlayerFollow();
        HandleLook();
        HandleHeadBob();
        HandleRecoilReturn();
    }

    private void PlayerFollow()
    {
        _cameraHolder.position = transform.position + camOffset;
    }

    private void HandleLook()
    {
        if (InputManager.Instance == null) return;

        Vector2 rawInput = InputManager.Instance.LookInput;

        float lookX, lookY;

        // Mouse delta is already frame-rate independent (pixels moved)
        // Gamepad sticks need Time.deltaTime to be frame-rate independent
        if (IsMouseInput())
        {
            float mouseMultiplier = 0.1f;
            lookX = rawInput.x * mouseSensitivity * mouseMultiplier;
            lookY = rawInput.y * mouseSensitivity * mouseMultiplier;
        }
        else
        {
            lookX = rawInput.x * mouseSensitivity * Time.deltaTime;
            lookY = rawInput.y * mouseSensitivity * Time.deltaTime;
        }

        _xRotation -= lookY;
        _xRotation = Mathf.Clamp(_xRotation, -maxLookAngle, maxLookAngle);

        // Player body rotates horizontally
        transform.Rotate(Vector3.up * lookX);
        // CameraHolder is SEPARATE — set full world rotation
        _cameraHolder.rotation = Quaternion.Euler(_xRotation + _recoilOffset, transform.eulerAngles.y, 0f);
    }

    private bool IsMouseInput()
    {
        if (InputManager.Instance == null || InputManager.Instance.InputActions == null) return false;
        var control = InputManager.Instance.InputActions.Player.Look.activeControl;
        return control != null && control.device is UnityEngine.InputSystem.Mouse;
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

    /// <summary>
    /// Returns the spawned camera (for other scripts that need it)
    /// </summary>
    public Camera GetCamera() => _cam;

    private void OnDestroy()
    {
        // Oyuncu yok olunca kamerayı da temizle
        if (_cameraHolder != null)
        {
            Destroy(_cameraHolder.gameObject);
        }
    }
}
