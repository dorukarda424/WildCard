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

    [Header("Debug")]
    public bool testing = false;

    private PlayerMovement _movement;
    private List<Animator> _animators = new List<Animator>();
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
    private enum KillCamStage { Victim, Killer }
    private KillCamStage _currentKillCamStage;
    private int _victimActorNumber;
    private int _killerActorNumber;
    private float _victimReplayDuration = 2f; // Show last 2 seconds of victim's life

    [Header("Spectator")]
    [SerializeField] private float spectatorSpeed = 10f;
    private bool _isSpectatorMode;

    public float CurrentCameraY
    {
        get
        {
            if (_movement != null) return _movement.CurrentCameraY;
            return camOffset.y; // Fallback
        }
    }

    public Transform CameraHolder => cameraHolder;
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

        // Find all child Animators (on both local and global models)
        RefreshAnimators();

        if (cam != null)
        {
            _originalCamLocalPos = cam.transform.localPosition;
            _defaultFov = cam.fieldOfView;
        }

        _defaultSensitivity = mouseSensitivity;
    }

    public void RefreshAnimators()
    {
        _animators.Clear();
        var allAnims = GetComponentsInChildren<Animator>(true); // Include inactive
        foreach (var a in allAnims)
        {
            if (a.runtimeAnimatorController != null)
            {
                _animators.Add(a);
            }
        }
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

        if (_isSpectatorMode)
        {
            UpdateSpectatorMode();
            HandleLook();
            return;
        }

        PlayerFollow();
        HandleLook();
        HandleHeadBob();
        HandleRecoilReturn();
        HandleAds();
    }

    public void StartKillCam(int victimActorNumber, int killerActorNumber)
    {
        if (KillCamManager.Instance == null) return;

        _victimActorNumber = victimActorNumber;
        _killerActorNumber = killerActorNumber;

        // Try to start with victim replay
        _killCamBuffer = KillCamManager.Instance.GetKillerBuffer(victimActorNumber);
        
        if (_killCamBuffer != null && _killCamBuffer.Count > 0)
        {
            _currentKillCamStage = KillCamStage.Victim;
            // Only play the last 2 seconds of the victim's buffer
            float totalBufferTime = _killCamBuffer[_killCamBuffer.Count - 1].timestamp - _killCamBuffer[0].timestamp;
            if (totalBufferTime > _victimReplayDuration)
            {
                // Find index to start from
                float startTime = _killCamBuffer[_killCamBuffer.Count - 1].timestamp - _victimReplayDuration;
                _killCamIndex = 0;
                while (_killCamIndex < _killCamBuffer.Count - 1 && _killCamBuffer[_killCamIndex].timestamp < startTime)
                {
                    _killCamIndex++;
                }
            }
            else
            {
                _killCamIndex = 0;
            }
        }
        else
        {
            // Skip to killer if victim buffer missing
            _killCamBuffer = KillCamManager.Instance.GetKillerBuffer(killerActorNumber);
            if (_killCamBuffer == null || _killCamBuffer.Count == 0)
            {
                Debug.LogWarning($"[PlayerCamera] No record found for victim {victimActorNumber} or killer {killerActorNumber}");
                return;
            }
            _currentKillCamStage = KillCamStage.Killer;
            _killCamIndex = 0;
        }

        _isKillCamActive = true;
        _killCamStartTime = Time.time;
    }

    private void UpdateKillCam()
    {
        if (_killCamBuffer == null || _killCamIndex >= _killCamBuffer.Count)
        {
            SwitchKillCamStage();
            return;
        }

        float elapsedTime = Time.time - _killCamStartTime;
        float bufferStartTime = _killCamBuffer[0].timestamp;
        
        // Find the correct frame based on elapsed time
        while (_killCamIndex < _killCamBuffer.Count - 1 && 
               (_killCamBuffer[_killCamIndex].timestamp - bufferStartTime) < elapsedTime)
        {
            _killCamIndex++;
        }

        var frame = _killCamBuffer[_killCamIndex];
        
        cameraHolder.position = frame.position;
        transform.rotation = frame.rotation;
        cameraHolder.localRotation = Quaternion.Euler(frame.cameraPitch, 0f, 0f);

        float currentStageMaxDuration = (_currentKillCamStage == KillCamStage.Victim) ? _victimReplayDuration : _killCamDuration;

        if (elapsedTime >= currentStageMaxDuration)
        {
            SwitchKillCamStage();
        }
    }

    private void SwitchKillCamStage()
    {
        if (_currentKillCamStage == KillCamStage.Victim)
        {
            // Switch to Killer
            _killCamBuffer = KillCamManager.Instance.GetKillerBuffer(_killerActorNumber);
            if (_killCamBuffer != null && _killCamBuffer.Count > 0)
            {
                _currentKillCamStage = KillCamStage.Killer;
                _killCamIndex = 0;
                _killCamStartTime = Time.time;
                return;
            }
        }
        
        // If we finished Killer stage or no Killer buffer found
        StopKillCam();
    }

    private void UpdateSpectatorMode()
    {
        if (InputManager.Instance == null) return;

        Vector2 moveInput = InputManager.Instance.MoveInput;
        
        Vector3 moveDir = transform.forward * moveInput.y + transform.right * moveInput.x;
        
        float vertical = 0f;
        if (InputManager.Instance.IsJumpPressed) vertical += 1f;
        if (InputManager.Instance.IsCrouching) vertical -= 1f;
        
        moveDir.y += vertical;

        transform.position += moveDir * spectatorSpeed * Time.deltaTime;

        cameraHolder.position = transform.position + camOffset;
        
        if (InputManager.Instance.IsJumpPressed) InputManager.Instance.ConsumeJump();
    }

    private void StopKillCam()
    {
        _isKillCamActive = false;
        _killCamBuffer = null;
        
        _isSpectatorMode = true;
    }

    public void StopSpectatorMode()
    {
        _isSpectatorMode = false;
    }

    private void PlayerFollow()
    {
        if (cameraHolder != null)
        {
            cameraHolder.position = transform.position + Vector3.up * CurrentCameraY;
        }
    }

    private void HandleLook()
    {
        if (InputManager.Instance == null || InputManager.Instance.InputActions == null) return;

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

        // drive AimPitch for upper-body layers on all animators
        if (_animators.Count > 0)
        {
            float t = Mathf.InverseLerp(-maxLookAngle * 1.5f, maxLookAngle * 1.5f, _xRotation);
            float aimPitch = t * 2f - 1f;
            foreach (var anim in _animators)
            {
                if (anim != null) anim.SetFloat("AimPitch", aimPitch, 0.1f, Time.deltaTime);
            }
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