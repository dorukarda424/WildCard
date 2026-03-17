using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

/// <summary>
/// Manages the Teleportation (Hiraishin) ability for one player.
/// Press Ability2 (Q) to throw a kunai projectile via raycast.
/// The kunai places a NetworkTeleportMark at the hit point.
/// Press Ability2 again to teleport to the mark closest to the crosshair.
/// Requires the Teleportation SpecialEffect from the card system.
/// </summary>
public class NetworkTeleportManager : MonoBehaviourPunCallbacks
{
    [Header("Teleport Settings")]
    [SerializeField] private string markPrefabName = "NetworkTeleportMark";
    [SerializeField] private float kunaiRange = 100f;
    [SerializeField] private float cooldown = 8f;
    [SerializeField] private Vector3 teleportOffset = new Vector3(0, 1.0f, 0);
    [SerializeField] private float maxTeleportDistance = -1f; // -1 = infinite

    [Header("VFX")]
    [SerializeField] private GameObject teleportVfxPrefab;

    private List<NetworkTeleportMark> _activeMarks = new List<NetworkTeleportMark>();
    private float _cooldownTimer;
    private bool _hasKunaiInFlight; // prevents spam

    private PlayerStats _stats;
    private PlayerCamera _playerCamera;
    private PlayerMovement _playerMovement;

    public float CooldownRemaining => Mathf.Max(0f, _cooldownTimer);
    public int ActiveMarkCount => _activeMarks.Count;

    private void Awake()
    {
        _stats = GetComponent<PlayerStats>();
        _playerCamera = GetComponent<PlayerCamera>();
        _playerMovement = GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        if (photonView != null && !photonView.IsMine) return;

        _cooldownTimer -= Time.deltaTime;

        // Cleanup destroyed marks
        _activeMarks.RemoveAll(m => m == null);

        if (!CanUseAbility()) return;

        if (InputManager.Instance != null && InputManager.Instance.IsAbility2Pressed)
        {
            InputManager.Instance.ConsumeAbility2();
            HandleTeleportAction();
        }
    }

    private bool CanUseAbility()
    {
        if (_stats == null) return false;
        if (!_stats.HasEffect(SpecialEffect.Teleportation)) return false;
        if (_cooldownTimer > 0f) return false;
        return true;
    }

    private void HandleTeleportAction()
    {
        // If we have marks, try to teleport to the one closest to crosshair
        if (_activeMarks.Count > 0)
        {
            TeleportToClosestMark();
        }
        else
        {
            // No marks — throw a kunai
            ThrowKunai();
        }
    }

    /// <summary>
    /// Fires a raycast from the camera center and places a mark at the hit point.
    /// </summary>
    private void ThrowKunai()
    {
        Camera cam = _playerCamera != null ? _playerCamera.GetCamera() : Camera.main;
        if (cam == null) return;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        int actorNumber = photonView.Owner != null ? photonView.Owner.ActorNumber : -1;

        if (Physics.Raycast(ray, out RaycastHit hit, kunaiRange))
        {
            object[] data = new object[] { actorNumber };
            GameObject markObj = PhotonNetwork.Instantiate(markPrefabName, hit.point, Quaternion.identity, 0, data);
            NetworkTeleportMark mark = markObj.GetComponent<NetworkTeleportMark>();
            if (mark != null)
            {
                _activeMarks.Add(mark);
            }

            Debug.Log($"[TeleportManager] Kunai placed mark at {hit.point}");
        }
        else
        {
            // If nothing was hit, place mark at max range
            Vector3 farPoint = ray.GetPoint(kunaiRange);
            object[] data = new object[] { actorNumber };
            GameObject markObj = PhotonNetwork.Instantiate(markPrefabName, farPoint, Quaternion.identity, 0, data);
            NetworkTeleportMark mark = markObj.GetComponent<NetworkTeleportMark>();
            if (mark != null)
            {
                _activeMarks.Add(mark);
            }

            Debug.Log($"[TeleportManager] Kunai placed mark at far point {farPoint}");
        }
    }

    /// <summary>
    /// Teleports to the mark closest to the player's crosshair aim direction.
    /// </summary>
    private void TeleportToClosestMark()
    {
        Camera cam = _playerCamera != null ? _playerCamera.GetCamera() : Camera.main;
        if (cam == null)
        {
            TeleportToLatestMark();
            return;
        }

        NetworkTeleportMark bestMark = null;
        float smallestAngle = float.MaxValue;

        foreach (var mark in _activeMarks)
        {
            if (mark == null) continue;

            // Check distance if limited
            if (maxTeleportDistance > 0)
            {
                float dist = Vector3.Distance(transform.position, mark.transform.position);
                if (dist > maxTeleportDistance) continue;
            }

            Vector3 dirToMark = (mark.transform.position - cam.transform.position).normalized;
            float angle = Vector3.Angle(cam.transform.forward, dirToMark);

            if (angle < smallestAngle)
            {
                smallestAngle = angle;
                bestMark = mark;
            }
        }

        if (bestMark == null)
        {
            Debug.Log("[TeleportManager] No valid mark within distance.");
            return;
        }

        PerformTeleport(bestMark.transform.position + teleportOffset);

        // Destroy all marks after teleporting
        CleanupAllMarks();
    }

    private void TeleportToLatestMark()
    {
        NetworkTeleportMark latest = null;
        for (int i = _activeMarks.Count - 1; i >= 0; i--)
        {
            if (_activeMarks[i] != null)
            {
                latest = _activeMarks[i];
                break;
            }
        }

        if (latest == null) return;

        PerformTeleport(latest.transform.position + teleportOffset);
        CleanupAllMarks();
    }

    private void PerformTeleport(Vector3 destination)
    {
        // VFX at departure
        SpawnTeleportVFX(transform.position);

        // Disable CharacterController, move, re-enable
        // Same pattern as PlayerHealth.Respawn()
        var cc = GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
            transform.position = destination;
            cc.enabled = true;
        }
        else
        {
            transform.position = destination;
        }

        // VFX at arrival
        SpawnTeleportVFX(destination);

        // Sync to other clients
        photonView.RPC(nameof(RPC_TeleportEffect), RpcTarget.Others, destination);

        _cooldownTimer = cooldown;

        Debug.Log($"[TeleportManager] Teleported to {destination}");
    }

    [PunRPC]
    private void RPC_TeleportEffect(Vector3 destination)
    {
        // Remote clients see VFX at both positions
        SpawnTeleportVFX(transform.position);
        transform.position = destination;
        SpawnTeleportVFX(destination);
    }

    private void SpawnTeleportVFX(Vector3 position)
    {
        if (teleportVfxPrefab != null)
        {
            GameObject vfx = Instantiate(teleportVfxPrefab, position, Quaternion.identity);
            Destroy(vfx, 2.0f);
        }
        else
        {
            // Fallback: create a simple procedural burst
            CreateProceduralTeleportBurst(position);
        }
    }

    /// <summary>
    /// Creates a quick golden particle burst at the given position (no prefab needed).
    /// </summary>
    private void CreateProceduralTeleportBurst(Vector3 position)
    {
        GameObject burstObj = new GameObject("TeleportBurst");
        burstObj.transform.position = position;

        ParticleSystem ps = burstObj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystemRenderer psRenderer = burstObj.GetComponent<ParticleSystemRenderer>();

        // Soft circle texture
        Texture2D tex = new Texture2D(16, 16);
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(8, 8));
                float alpha = Mathf.Clamp01(1.0f - (dist / 8.0f));
                tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }
        tex.Apply();

        if (psRenderer != null)
        {
            psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            Shader shader = Shader.Find("Particles/Standard Unlit")
                            ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply");
            if (shader != null)
            {
                Material mat = new Material(shader);
                mat.mainTexture = tex;
                psRenderer.sharedMaterial = mat;
            }
        }

        var main = ps.main;
        main.startColor = new Color(1f, 0.85f, 0.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.duration = 0.2f;
        main.loop = false;
        main.playOnAwake = false;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.5f;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 30) });

        ps.Play();

        Destroy(burstObj, 2f);
    }

    private void CleanupAllMarks()
    {
        foreach (var mark in _activeMarks)
        {
            if (mark != null && mark.photonView.IsMine)
            {
                PhotonNetwork.Destroy(mark.gameObject);
            }
        }
        _activeMarks.Clear();
    }
}
