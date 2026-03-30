using UnityEngine;
using Unity.FPS.Game;

namespace Unity.FPS.Gameplay
{
    public class HiraishinKunai : ProjectileStandard
    {
        [Header("Hiraishin")]
        [Tooltip("Prefab of the mark to spawn on hit")]
        public GameObject MarkPrefab;

        [Tooltip("Should the kunai stick to the surface or spawn a separate mark?")]
        public bool StickToSurface = true;

        protected override void OnHit(Vector3 point, Vector3 normal, Collider collider)
        {
            // Custom hit logic for Hiraishin
            if (StickToSurface)
            {
                // Instantiate the MarkPrefab at the hit point
                if (MarkPrefab)
                {
                    GameObject markInstance = Instantiate(MarkPrefab, point, Quaternion.LookRotation(normal));
                    
                    // Parent to the hit object so it moves with it (platforms, etc.)
                    markInstance.transform.parent = collider.transform;

                    HiraishinMark mark = markInstance.GetComponent<HiraishinMark>();
                    if (mark == null) mark = markInstance.AddComponent<HiraishinMark>();

                    // Notify manager
                    if (HiraishinManager.Instance)
                    {
                        HiraishinManager.Instance.RegisterMark(mark);
                    }
                }
                
                // Base hit (handles destruction of the projectile itself)
                base.OnHit(point, normal, collider);
            }
            else
            {
                if (MarkPrefab)
                {
                    GameObject markInstance = Instantiate(MarkPrefab, point, Quaternion.LookRotation(normal));
                    if (HiraishinManager.Instance)
                    {
                        HiraishinManager.Instance.RegisterMark(markInstance.GetComponent<HiraishinMark>());
                    }
                }
                
                // Base hit
                base.OnHit(point, normal, collider);
            }
        }
    }
}
