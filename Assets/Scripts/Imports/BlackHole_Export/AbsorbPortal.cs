using UnityEngine;

namespace Unity.FPS.Gameplay
{
    public class AbsorbPortal : MonoBehaviour
    {
        [Tooltip("Lifetime of the portal before disappearing naturally")]
        public float Lifetime = 10f;

        private void Start()
        {
            if (Lifetime > 0)
            {
                Destroy(gameObject, Lifetime);
            }
        }

        public void AbsorbProjectile(ProjectileStandard proj)
        {
            if (AbsorbPortalManager.Instance != null)
            {
                AbsorbPortalManager.Instance.OnAttackAbsorbed(proj.Damage);
                
                // Visual feedback: Make the black hole stronger!
                PortalVfxController vfx = GetComponentInChildren<PortalVfxController>();
                if (vfx != null)
                {
                    vfx.PlayAbsorptionFlash();
                    vfx.IntensifyEffect(proj.Damage);
                }
                
                // Audio effect
                Debug.Log($"Portal absorbed a projectile with {proj.Damage} damage!");
            }
        }
    }
}
