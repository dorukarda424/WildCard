using UnityEngine;

[DefaultExecutionOrder(-10)] // Ensure this runs before AimIK and Animator if possible, though we want it before Animator starts sampling
public class NeckBoneOverride : MonoBehaviour
{
    [Header("Bone References")]
    [Tooltip("The existing Neck bone in the character's hierarchy.")]
    public Transform neckBone;
    [Tooltip("The existing Head bone that should follow Neck1.")]
    public Transform headBone;

    private Transform _neck1;

    private void Awake()
    {
        if (neckBone == null)
        {
            Debug.LogError("[NeckBoneOverride] Neck bone is not assigned!", this);
            return;
        }

        // Check if neck1 already exists (e.g., if this script was already run or manually added)
        _neck1 = neckBone.Find("neck1");
        
        if (_neck1 == null)
        {
            // Create the dummy neck1 bone that the animations are looking for
            GameObject neck1Go = new GameObject("neck1");
            _neck1 = neck1Go.transform;
            
            // Parent it to the existing neck bone
            _neck1.SetParent(neckBone);
            
            // Reset local transform so it starts at the same position as the neck
            _neck1.localPosition = Vector3.zero;
            _neck1.localRotation = Quaternion.identity;
            _neck1.localScale = Vector3.one;

            Debug.Log($"[NeckBoneOverride] Created dummy 'neck1' bone as a child of '{neckBone.name}'", this);
        }

        // If we have a head bone, make it a child of the new neck1 bone
        // This ensures that when the animation rotates neck1, the head follows.
        if (headBone != null && headBone.parent != _neck1)
        {
            headBone.SetParent(_neck1, true);
            Debug.Log($"[NeckBoneOverride] Reparented '{headBone.name}' to 'neck1'", this);
        }
        else if (headBone == null)
        {
            Debug.LogWarning("[NeckBoneOverride] Head bone is not assigned. 'neck1' was created but no child bone was reparented to it.", this);
        }
    }
}
