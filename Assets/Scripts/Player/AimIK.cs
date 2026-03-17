using UnityEngine;
using Photon.Pun;

public class AimIK : MonoBehaviourPunCallbacks
{
    [SerializeField] private PlayerCamera playerCamera;
    [SerializeField] private Transform aimIKTarget;   // Spine2 or Neck
    [SerializeField] private float maxPitch = 60f;
    [SerializeField] private float spineWeight = 1f;
    [SerializeField] private float aimLerpSpeed = 20f;

    private float _currentPitch;

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = GetComponent<PlayerCamera>();
    }

    /// <summary>
    /// Animator'ın bu frame'deki bone yazımından sonra çalışır.
    /// Animasyonun üzerine pitch offset'i katmanlıyoruz — sabit bir referans rotasyonu kullanmıyoruz.
    /// </summary>
    private void LateUpdate()
    {
        if (aimIKTarget == null || playerCamera == null) return;
        if (photonView != null && !photonView.IsMine) return;

        float targetPitch = Mathf.Clamp(playerCamera.GetPitch(), -maxPitch, maxPitch) * spineWeight;
        _currentPitch = Mathf.Lerp(_currentPitch, targetPitch, Time.deltaTime * aimLerpSpeed);

        aimIKTarget.localRotation = aimIKTarget.localRotation * Quaternion.Euler(_currentPitch, 0f, 0f);
    }
}
