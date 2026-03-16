using System.Collections.Generic;
using UnityEngine;
using Unity.FPS.Game;

namespace Unity.FPS.Gameplay
{
    public class HiraishinManager : MonoBehaviour
    {
        public static HiraishinManager Instance { get; private set; }

        [Header("Teleport Settings")]
        [Tooltip("Offset from the mark's position where the player will teleport to (to avoid clipping)")]
        public Vector3 TeleportOffset = new Vector3(0, 1.0f, 0);

        [Tooltip("VFX to spawn at the player's old and new position during teleport")]
        public GameObject TeleportVfx;

        [Tooltip("Sound to play during teleport")]
        public AudioClip TeleportSfx;

        [Tooltip("Maximum distance for teleport (-1 for infinite)")]
        public float MaxTeleportDistance = -1f;

        private List<HiraishinMark> m_ActiveMarks = new List<HiraishinMark>();
        private PlayerCharacterController m_PlayerController;
        private GameFlowManager m_GameFlowManager;

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

        private void Start()
        {
            m_PlayerController = FindFirstObjectByType<PlayerCharacterController>();
            m_GameFlowManager = FindFirstObjectByType<GameFlowManager>();

            if (m_PlayerController == null) Debug.LogError("HiraishinManager: PlayerCharacterController not found!");
            else Debug.Log("HiraishinManager: PlayerCharacterController linked.");
        }

        private void Update()
        {
            // Guard: only allow teleport when mouse is locked (i.e., in gameplay, not in menus)
            bool canAct = Cursor.lockState == CursorLockMode.Locked
                          && (m_GameFlowManager == null || !m_GameFlowManager.GameIsEnding);

            if (canAct && UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
            {
                Debug.Log("Teleport input detected! (E key)");
                TeleportToLookedAtMark();
            }

            // Cleanup destroyed marks
            m_ActiveMarks.RemoveAll(mark => mark == null);
        }

        public void RegisterMark(HiraishinMark mark)
        {
            if (mark != null && !m_ActiveMarks.Contains(mark))
            {
                Debug.Log($"Mark registered at {mark.transform.position}");
                m_ActiveMarks.Add(mark);
            }
        }

        public void TeleportToLookedAtMark()
        {
            if (m_ActiveMarks.Count == 0)
            {
                Debug.Log("No active marks found.");
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogWarning("Main Camera not found, falling back to latest mark.");
                TeleportToLatestMark();
                return;
            }

            HiraishinMark bestMark = null;
            float smallestAngle = float.MaxValue;

            foreach (var mark in m_ActiveMarks)
            {
                if (mark == null) continue;

                // Check distance if applicable, ignore marks that are too far
                if (MaxTeleportDistance > 0)
                {
                    float distance = Vector3.Distance(m_PlayerController.transform.position, mark.transform.position);
                    if (distance > MaxTeleportDistance) continue;
                }

                Vector3 directionToMark = (mark.transform.position - mainCamera.transform.position).normalized;
                float angle = Vector3.Angle(mainCamera.transform.forward, directionToMark);

                if (angle < smallestAngle)
                {
                    smallestAngle = angle;
                    bestMark = mark;
                }
            }

            if (bestMark == null)
            {
                Debug.Log("No valid mark found within distance.");
                return;
            }

            Debug.Log($"Teleporting to mark at {bestMark.transform.position} with look angle {smallestAngle}");
            PerformTeleport(bestMark.transform.position + TeleportOffset);
        }

        public void TeleportToLatestMark()
        {
            if (m_ActiveMarks.Count == 0)
            {
                Debug.Log("No active marks found.");
                return;
            }

            // Get the latest valid mark
            HiraishinMark latestMark = null;
            for (int i = m_ActiveMarks.Count - 1; i >= 0; i--)
            {
                if (m_ActiveMarks[i] != null)
                {
                    latestMark = m_ActiveMarks[i];
                    break;
                }
            }

            if (latestMark == null)
            {
                Debug.Log("Latest mark is null.");
                return;
            }

            Debug.Log($"Teleporting to mark at {latestMark.transform.position}");

            // Check distance if applicable
            if (MaxTeleportDistance > 0)
            {
                float distance = Vector3.Distance(m_PlayerController.transform.position, latestMark.transform.position);
                if (distance > MaxTeleportDistance)
                {
                    Debug.Log($"Teleport failed: Distance too far ({distance} > {MaxTeleportDistance})");
                    return;
                }
            }

            PerformTeleport(latestMark.transform.position + TeleportOffset);
        }

        private void PerformTeleport(Vector3 destination)
        {
            if (m_PlayerController == null)
            {
                Debug.LogError("PlayerController is missing in HiraishinManager!");
                return;
            }

            // Effects at old position
            SpawnTeleportEffects(m_PlayerController.transform.position);

            // Sync character controller — disable/re-enable to let Unity move it cleanly
            var cc = m_PlayerController.GetComponent<CharacterController>();
            if (cc)
            {
                cc.enabled = false;
                m_PlayerController.transform.position = destination;
                cc.enabled = true;
            }
            else
            {
                m_PlayerController.transform.position = destination;
            }

            // Effects at new position
            SpawnTeleportEffects(destination);

            // SFX
            if (TeleportSfx)
            {
                AudioUtility.CreateSFX(TeleportSfx, destination, AudioUtility.AudioGroups.Impact, 1f);
            }
        }

        private void SpawnTeleportEffects(Vector3 position)
        {
            if (TeleportVfx)
            {
                GameObject vfx = Instantiate(TeleportVfx, position, Quaternion.identity);
                Destroy(vfx, 2.0f);
            }
        }
    }
}
