using UnityEngine;

[DefaultExecutionOrder(-10)]
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
        
        const string neck1Name = "mixamorig:Neck1";

        _neck1 = neckBone.Find(neck1Name);
        if (_neck1 == null)
        {
            GameObject neck1Go = new GameObject(neck1Name);
            _neck1 = neck1Go.transform;
            
            _neck1.SetParent(neckBone, false);
            _neck1.localPosition = Vector3.zero;
            _neck1.localRotation = Quaternion.identity;
            _neck1.localScale    = Vector3.one;

            Debug.Log($"[NeckBoneOverride] Created dummy '{neck1Name}' bone as a child of '{neckBone.name}'", this);
        }
        
        if (headBone != null && headBone.parent != _neck1)
        {
            Vector3     localPos  = headBone.localPosition;
            Quaternion  localRot  = headBone.localRotation;
            Vector3     localScale = headBone.localScale;

            headBone.SetParent(_neck1, false);
            headBone.localPosition = localPos;
            headBone.localRotation = localRot;
            headBone.localScale    = localScale;

            Debug.Log($"[NeckBoneOverride] Reparented '{headBone.name}' to '{neck1Name}' (preserving local pose)", this);
        }
        else if (headBone == null)
        {
            Debug.LogWarning("[NeckBoneOverride] Head bone is not assigned. 'neck1' was created but no child bone was reparented to it.", this);
        }
    }
}