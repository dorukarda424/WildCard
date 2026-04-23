using UnityEngine;
using UnityEngine.Animations.Rigging;

public class PlayerRigController : MonoBehaviour
{
    [Header("Global Rig Layers (Visible to others)")]
    [SerializeField] private Rig globalAimRig;
    [SerializeField] private Rig globalHandRig;

    [Header("Local Rig Layers (First Person Hands)")]
    [SerializeField] private Rig localAimRig;
    [SerializeField] private Rig localHandRig;

    [Header("Settings")]
    [SerializeField] private float weightTransitionSpeed = 10f;

    private PlayerCombat _combat;
    private float _targetAimWeight = 1f;
    private float _targetHandWeight = 1f;

    private void Awake()
    {
        _combat = GetComponentInParent<PlayerCombat>();
    }

    private void Update()
    {
        if (_combat == null) return;

        // Disable Hand IKs during reload to allow reload animation to play
        _targetHandWeight = _combat.IsReloading ? 0f : 1f;
        
        // Aim Rigs can usually stay on
        _targetAimWeight = 1f;

        // Smoothly interpolate weights for all assigned rigs
        UpdateRigWeight(globalHandRig, _targetHandWeight);
        UpdateRigWeight(localHandRig, _targetHandWeight);
        UpdateRigWeight(globalAimRig, _targetAimWeight);
        UpdateRigWeight(localAimRig, _targetAimWeight);
    }

    private void UpdateRigWeight(Rig rig, float targetWeight)
    {
        if (rig != null)
            rig.weight = Mathf.MoveTowards(rig.weight, targetWeight, Time.deltaTime * weightTransitionSpeed);
    }
}
