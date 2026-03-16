using UnityEngine;

namespace Unity.FPS.Gameplay
{
    public class HiraishinMark : MonoBehaviour
    {
        [Tooltip("Lifetime of the mark (-1 for infinite)")]
        public float Lifetime = -1f;

        void Start()
        {
            if (Lifetime > 0)
            {
                Destroy(gameObject, Lifetime);
            }
        }
    }
}
