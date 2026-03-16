using System.Collections.Generic;
using UnityEngine;
using Unity.FPS.Game;

namespace Unity.FPS.Gameplay
{
    public class AbsorbPortalManager : MonoBehaviour
    {
        public static AbsorbPortalManager Instance { get; private set; }

        [Header("Portal Settings")]
        [Tooltip("Prefab for the portal that absorbs attacks")]
        public GameObject AbsorbPortalPrefab;

        [Tooltip("Prefab for the projectiles released by the portal")]
        public GameObject ReleasedProjectilePrefab;

        [Tooltip("Distance from the camera to spawn the portal")]
        public float SpawnDistance = 3f;

        [Tooltip("Key to trigger the portal")]
        public UnityEngine.InputSystem.Key PortalKey = UnityEngine.InputSystem.Key.Z;

        private GameObject m_CurrentPortal;
        private List<float> m_AbsorbedDamages = new List<float>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            // Only allow when mouse is locked (gameplay)
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                if (UnityEngine.InputSystem.Keyboard.current[PortalKey].wasPressedThisFrame)
                {
                    HandlePortalToggle();
                }
            }
        }

        private void HandlePortalToggle()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            // If we already have a portal and it has absorbed attacks, release them!
            if (m_CurrentPortal != null && m_AbsorbedDamages.Count > 0)
            {
                ReleaseAttacks(mainCam);
            }
            else
            {
                SpawnNewPortal(mainCam);
            }
        }

        private void SpawnNewPortal(Camera mainCam)
        {
            if (m_CurrentPortal != null)
            {
                Destroy(m_CurrentPortal);
            }

            m_AbsorbedDamages.Clear(); // reset absorbed

            if (AbsorbPortalPrefab != null)
            {
                Vector3 spawnPos = mainCam.transform.position + mainCam.transform.forward * SpawnDistance;
                m_CurrentPortal = Instantiate(AbsorbPortalPrefab, spawnPos, mainCam.transform.rotation);
                Debug.Log("Absorb Portal created! Shoot it to store attacks.");
            }
            else
            {
                Debug.LogWarning("AbsorbPortalPrefab is missing!");
            }
        }

        private void ReleaseAttacks(Camera mainCam)
        {
            if (m_CurrentPortal != null)
            {
                Destroy(m_CurrentPortal);
                m_CurrentPortal = null;
            }

            if (ReleasedProjectilePrefab != null)
            {
                Vector3 spawnPos = mainCam.transform.position + mainCam.transform.forward * SpawnDistance;
                Quaternion spawnRot = mainCam.transform.rotation;

                // Create a temporary "release" portal effect if you want, but here we just spawn the portal logic
                Debug.Log($"Releasing {m_AbsorbedDamages.Count} absorbed attacks!");

                // Fire all absorbed attacks with slight delays or spread
                StartCoroutine(ReleaseRoutine(spawnPos, spawnRot));
            }
            else
            {
                Debug.LogWarning("ReleasedProjectilePrefab is missing!");
            }
        }

        private System.Collections.IEnumerator ReleaseRoutine(Vector3 startPos, Quaternion startRot)
        {
            PlayerCharacterController player = FindFirstObjectByType<PlayerCharacterController>();
            PlayerWeaponsManager weaponsManager = player != null ? player.GetComponent<PlayerWeaponsManager>() : null;
            WeaponController activeWeapon = weaponsManager != null ? weaponsManager.GetActiveWeapon() : null;

            foreach (float dmg in m_AbsorbedDamages)
            {
                // Random slightly spread rotation
                float spreadAngle = 5f;
                Quaternion spreadRot = startRot * Quaternion.Euler(
                    Random.Range(-spreadAngle, spreadAngle),
                    Random.Range(-spreadAngle, spreadAngle),
                    0f);

                GameObject projInstance = Instantiate(ReleasedProjectilePrefab, startPos, spreadRot);
                
                // Let's set the damage if it's a standard projectile
                ProjectileStandard projStandard = projInstance.GetComponent<ProjectileStandard>();
                if (projStandard != null)
                {
                    projStandard.Damage = dmg; // Keep the original absorbed damage
                    
                    // We need to set the owner to the player so they don't damage themselves
                    ProjectileBase projBase = projInstance.GetComponent<ProjectileBase>();
                    if (projBase != null && activeWeapon != null)
                    {
                        // Fire it correctly using the player's weapon controller as the reference
                        projBase.Shoot(activeWeapon); 
                    }
                    else if (projBase != null)
                    {
                        Debug.LogWarning("Player active weapon not found, projectile might damage shooter!");
                    }
                }

                yield return new WaitForSeconds(0.1f); // Fire like a burst stream
            }

            m_AbsorbedDamages.Clear();
        }

        public void OnAttackAbsorbed(float damage)
        {
            m_AbsorbedDamages.Add(damage);
            Debug.Log($"Attack absorbed! Total stored: {m_AbsorbedDamages.Count}");
        }
    }
}
