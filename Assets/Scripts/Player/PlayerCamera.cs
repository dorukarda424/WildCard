using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;

[RequireComponent(typeof(PlayerMovement))]
public class PlayerCamera : MonoBehaviourPunCallbacks
{
    public static PlayerCamera Instance { get; private set; }
    [Header("References")]
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private Camera cam;

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

    [Header("ADS")]
    public float adsFov = 55f;
    public float adsSensitivityMultiplier = 0.6f;
    public float adsLerpSpeed = 12f;

    [Header("Weapon")]
    [SerializeField] private Transform weaponHolder;
    
    [Header("Debug")]
    public bool testing = false;

    private PlayerMovement _movement;
    private Animator _anim;
    private bool _isLocalPlayer;

    private float _xRotation;
    private float _yRotation; 
    private float _recoilOffset;

    private float _bobTimer;
    private Vector3 _bobOffset;
    private Vector3 _originalCamLocalPos;

    private float _defaultFov;
    private float _defaultSensitivity;

    private bool _isKillCamActive;
    private List<PlayerRecorder.PlayerStateFrame> _killCamBuffer;
    private int _killCamIndex;
    private float _killCamStartTime;
    private float _killCamDuration = 5f;

    public float GetPitch() => _xRotation;
    public Camera GetCamera() => cam;
    public bool IsKillCamActive => _isKillCamActive;

    private void Awake()
    {
        _isLocalPlayer = testing
                      || (photonView != null && photonView.IsMine)
                      || !Photon.Pun.PhotonNetwork.InRoom;

        if (_isLocalPlayer)
        {
            if (Instance == null)
                Instance = this;
            else if (Instance != this)
                Debug.LogWarning("[PlayerCamera] Multiple local PlayerCamera instances found!", this);
        }

        _movement = GetComponent<PlayerMovement>();

        // Find the child Animator (on the actual model), not the root one
        _anim = null;
        var allAnims = GetComponentsInChildren<Animator>();
        foreach (var a in allAnims)
        {
            if (a.gameObject != gameObject) { _anim = a; break; }
        }
        if (_anim == null) _anim = GetComponent<Animator>(); // fallback to root

        if (cam != null)
        {
            _originalCamLocalPos = cam.transform.localPosition;
            _defaultFov = cam.fieldOfView;
        }

        _defaultSensitivity = mouseSensitivity;
    }

    private void Start()
    {
        if (!_isLocalPlayer)
        {
            if (cam != null)
            {
                cam.enabled = false;
                var listener = cam.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = false;
            }
            enabled = false;
            return;
        }

        // Disable any other cameras in the scene so only the player camera renders
        DisableOtherCameras();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (_movement != null && cameraHolder != null)
            _movement.SetCamHolder(cameraHolder);
    }

    private void LateUpdate()
    {
        if (!_isLocalPlayer) return;
        if (cameraHolder == null || cam == null) return;

        if (_isKillCamActive)
        {
            UpdateKillCam();
            return;
        }

        PlayerFollow();
        HandleLook();
        HandleHeadBob();
        HandleRecoilReturn();
        HandleAds();
        //SyncWeaponRotation();
    }

    public void StartKillCam(int killerActorNumber)
    {
        if (KillCamManager.Instance == null) return;

        _killCamBuffer = KillCamManager.Instance.GetKillerBuffer(killerActorNumber);
        if (_killCamBuffer == null || _killCamBuffer.Count == 0)
        {
            Debug.LogWarning($"[PlayerCamera] No record found for killer {killerActorNumber}");
            return;
        }

        _isKillCamActive = true;
        _killCamStartTime = Time.time;
        _killCamIndex = 0;
        
        // Optionally disable HUD or show "Kill Cam" text here
    }

    private void UpdateKillCam()
    {
        if (_killCamBuffer == null || _killCamIndex >= _killCamBuffer.Count)
        {
            StopKillCam();
            return;
        }

        float elapsedTime = Time.time - _killCamStartTime;
        
        // Find the frame that corresponds to current elapsed time
        // The buffer starts ~5s ago, so we want to play from start to end
        while (_killCamIndex < _killCamBuffer.Count - 1 && 
               (_killCamBuffer[_killCamIndex].timestamp - _killCamBuffer[0].timestamp) < elapsedTime)
        {
            _killCamIndex++;
        }

        var frame = _killCamBuffer[_killCamIndex];
        
        // Apply frame to camera
        cameraHolder.position = frame.position;
        transform.rotation = frame.rotation;
        cameraHolder.localRotation = Quaternion.Euler(frame.cameraPitch, 0f, 0f);

        if (elapsedTime >= _killCamDuration)
        {
            StopKillCam();
        }
    }

    private void StopKillCam()
    {
        _isKillCamActive = false;
        _killCamBuffer = null;
        // Proceed to normal respawn flow if needed
    }

    private void PlayerFollow()
    {
        if (cameraHolder != null)
        {
            // Only update X and Z, Y is handled by PlayerMovement for crouching/sliding.
            // We follow the cameraHolder's current Y position which is managed by PlayerMovement.
            Vector3 targetPos = transform.position;
            targetPos.y = cameraHolder.position.y; 
            cameraHolder.position = targetPos;
        }
    }

    private void HandleLook()
    {
        if (InputManager.Instance == null || InputManager.Instance.InputActions == null) return;

        // read current frame delta directly from the action
        Vector2 raw = InputManager.Instance.InputActions.Player.Look.ReadValue<Vector2>();
        if (raw.sqrMagnitude < 0.01f) raw = Vector2.zero;
        float lookX, lookY;

        if (IsMouseInput())
        {
            const float mouseMultiplier = 0.1f;
            lookX = raw.x * mouseSensitivity * mouseMultiplier;
            lookY = raw.y * mouseSensitivity * mouseMultiplier;
        }
        else
        {
            lookX = raw.x * mouseSensitivity * Time.deltaTime;
            lookY = raw.y * mouseSensitivity * Time.deltaTime;
        }

        _xRotation -= lookY;
        _xRotation = Mathf.Clamp(_xRotation, -maxLookAngle, maxLookAngle);
        
        _yRotation += lookX;

        cameraHolder.localRotation=Quaternion.Euler(_xRotation + _recoilOffset, 0f, 0f);
        transform.rotation = Quaternion.Euler(0f, _yRotation, 0f);

        // drive AimPitch for upper-body layer
        if (_anim != null)
        {
            float t = Mathf.InverseLerp(-maxLookAngle * 1.5f, maxLookAngle * 1.5f, _xRotation);
            float aimPitch = t * 2f - 1f;
            _anim.SetFloat("AimPitch", aimPitch, 0.1f, Time.deltaTime);
        }
    }

    private bool IsMouseInput()
    {
        if (InputManager.Instance == null || InputManager.Instance.InputActions == null)
            return false;

        var control = InputManager.Instance.InputActions.Player.Look.activeControl;
        return control != null && control.device is UnityEngine.InputSystem.Mouse;
    }

    private void HandleHeadBob()
    {
        if (_movement == null) return;

        var state = _movement.CurrentState;

        // no bob while latched or in air
        if (state == PlayerMovement.PlayerState.Latched ||
            state == PlayerMovement.PlayerState.Airborne)
        {
            _bobTimer = 0f;
            _bobOffset = Vector3.Lerp(_bobOffset, Vector3.zero,
                                     Time.deltaTime * bobReturnSpeed);
            cam.transform.localPosition = _originalCamLocalPos + _bobOffset;
            return;
        }

        bool isMoving = _movement.IsMoving && _movement.IsGrounded;

        if (isMoving)
        {
            float speedMul = 1f;
            if (state == PlayerMovement.PlayerState.Sprinting) speedMul = 1.5f;
            if (state == PlayerMovement.PlayerState.Crouching) speedMul = 0.7f;

            _bobTimer += Time.deltaTime * bobFrequency * speedMul;

            _bobOffset = new Vector3(
                Mathf.Sin(_bobTimer * 0.5f) * bobAmplitude,
                Mathf.Sin(_bobTimer) * bobAmplitude,
                0f
            );
        }
        else
        {
            _bobTimer = 0f;
            _bobOffset = Vector3.Lerp(_bobOffset, Vector3.zero,
                                     Time.deltaTime * bobReturnSpeed);
        }

        cam.transform.localPosition = _originalCamLocalPos + _bobOffset;
    }

    public void AddRecoil(float amount)
    {
        _recoilOffset -= amount;
    }

    private void HandleRecoilReturn()
    {
        if (Mathf.Abs(_recoilOffset) <= 0.001f) return;

        _recoilOffset = Mathf.Lerp(_recoilOffset, 0f,
                                  Time.deltaTime * recoilReturnSpeed);

        if (Mathf.Abs(_recoilOffset) < 0.01f)
            _recoilOffset = 0f;
    }

    private void HandleAds()
    {
        if (cam == null || InputManager.Instance == null) return;

        bool isAiming = InputManager.Instance.IsAiming;

        // optional: disable ADS while sprinting or sliding
        if (_movement != null &&
            (_movement.CurrentState == PlayerMovement.PlayerState.Sprinting ||
             _movement.CurrentState == PlayerMovement.PlayerState.Sliding))
        {
            isAiming = false;
        }

        float targetFov = isAiming ? adsFov : _defaultFov;
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov,
                                     Time.deltaTime * adsLerpSpeed);

        float targetSens = isAiming
            ? _defaultSensitivity * adsSensitivityMultiplier
            : _defaultSensitivity;

        mouseSensitivity = Mathf.Lerp(mouseSensitivity, targetSens,
                                      Time.deltaTime * adsLerpSpeed);
    }

    public void SetSensitivity(float value) => mouseSensitivity = value;

    public void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
    
    private void SyncWeaponRotation()
    {
        if (weaponHolder == null) return;
        weaponHolder.rotation = cameraHolder.rotation;
    }

    /// <summary>
    /// Disables all other cameras in the scene so the player sees through their own camera.
    /// This handles the case where a scene camera (e.g. Main Camera) is left active.
    /// </summary>
    private void DisableOtherCameras()
    {
        if (cam == null) return;

        Camera[] allCameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Camera otherCam in allCameras)
        {
            if (otherCam == cam) continue; // Skip our own camera

            Debug.Log($"[PlayerCamera] Disabling competing camera: {otherCam.gameObject.name}");
            otherCam.enabled = false;

            var listener = otherCam.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}