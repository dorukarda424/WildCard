using UnityEngine;
using Photon.Pun;

public class AimIK : MonoBehaviourPunCallbacks
{
    [Header("Aim IK")]
    public PlayerCamera playerCamera;
    [SerializeField] private Transform spine1;
    [SerializeField] private Transform spine2;
    [SerializeField] private Transform neck;
    [SerializeField] private float maxPitch = 85f;
    [SerializeField] private float aimLerpSpeed = 12f;
    public bool testing = false;

    private float _currentPitch;
    private bool _restCaptured = false;
    private Quaternion _restSpine1, _restSpine2, _restNeck;

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInParent<PlayerCamera>();
    }

    private void LateUpdate()
    {
        if (playerCamera == null) return;
        if (!testing && photonView != null && !photonView.IsMine) return;

        // Capture rest pose on first frame AFTER Animator has initialized
        if (!_restCaptured)
        {
            if (spine1 != null) _restSpine1 = spine1.localRotation;
            if (spine2 != null) _restSpine2 = spine2.localRotation;
            if (neck   != null) _restNeck   = neck.localRotation;
            _restCaptured = true;
            return;
        }

        float targetPitch = Mathf.Clamp(playerCamera.GetPitch(), -maxPitch, maxPitch);
        _currentPitch = Mathf.Lerp(_currentPitch, targetPitch, Time.deltaTime * aimLerpSpeed);

        // Always set from rest pose — no accumulation possible
        if (spine1 != null) spine1.localRotation = _restSpine1 * Quaternion.Euler(_currentPitch * 0.3f, 0f, 0f);
        if (spine2 != null) spine2.localRotation = _restSpine2 * Quaternion.Euler(_currentPitch * 0.4f, 0f, 0f);
        if (neck   != null) neck.localRotation   = _restNeck   * Quaternion.Euler(_currentPitch * 0.3f, 0f, 0f);
    }
}