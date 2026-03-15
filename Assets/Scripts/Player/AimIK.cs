using UnityEngine;

public class AimIK : MonoBehaviour
{
    [SerializeField] private PlayerCamera playerCamera;
    [SerializeField] private Transform aimIKTarget;   // Spine2 or Neck
    [SerializeField] private float maxPitch = 60f;
    [SerializeField] private float spineWeight = 1f;
    [SerializeField] private float aimLerpSpeed = 20f;

    private Quaternion _defaultRot;
    private Animator _anim;

    private void Awake()
    {
        _anim = GetComponent<Animator>();
        
        if (playerCamera == null)
            playerCamera = GetComponent<PlayerCamera>();  
        
        if (aimIKTarget != null)
            _defaultRot = aimIKTarget.localRotation;
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (playerCamera == null || aimIKTarget == null) return;
        if (!playerCamera.photonView.IsMine && !playerCamera.testing) return;

        float pitch = Mathf.Clamp(playerCamera.GetPitch(), -maxPitch, maxPitch);

        Quaternion target = _defaultRot * Quaternion.Euler(pitch * spineWeight, 0f, 0f);
        aimIKTarget.localRotation = Quaternion.Slerp(
            aimIKTarget.localRotation,
            target,
            Time.deltaTime * aimLerpSpeed
        );
    }
}
