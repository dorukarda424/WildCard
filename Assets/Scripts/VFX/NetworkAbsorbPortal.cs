using UnityEngine;
using Photon.Pun;

/// <summary>
/// Networked Black Hole portal that absorbs incoming bullets.
/// Spawned via PhotonNetwork.Instantiate by NetworkAbsorbPortalManager.
/// Contains a procedural particle VFX that intensifies as it absorbs damage.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Rigidbody))]
public class NetworkAbsorbPortal : MonoBehaviourPunCallbacks
{
    [Header("Portal Settings")]
    [SerializeField] private float lifetime = 10f;
    [SerializeField] private float absorptionRadius = 3f;

    [Header("VFX Settings")]
    [SerializeField] private float baseEmissionRate = 500f;
    [SerializeField] private float baseSpeed = -10f;

    private ParticleSystem _particleSystem;
    private float _totalAbsorbedDamage;

    // Owner actor number — only the owner's manager tracks absorbed damage
    private int _ownerActorNumber = -1;

    public int OwnerActorNumber => _ownerActorNumber;

    private void Awake()
    {
        // Parse owner from instantiation data
        if (photonView.InstantiationData != null && photonView.InstantiationData.Length >= 1)
        {
            _ownerActorNumber = (int)photonView.InstantiationData[0];
        }

        // Setup kinematic Rigidbody for trigger detection
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // Setup trigger collider for bullet absorption
        SphereCollider col = GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = absorptionRadius;

        // Create the procedural black hole VFX
        CreateBlackHoleVFX();

        // Auto-destroy after lifetime
        if (photonView.IsMine)
        {
            Invoke(nameof(DestroyPortal), lifetime);
        }
    }

    /// <summary>
    /// Creates the swirling black hole particle system procedurally (no prefab dependencies).
    /// Ported from PortalVfxController.
    /// </summary>
    private void CreateBlackHoleVFX()
    {
        // Add ParticleSystem component
        _particleSystem = gameObject.AddComponent<ParticleSystem>();

        // Stop it first so we can configure
        _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystemRenderer psRenderer = GetComponent<ParticleSystemRenderer>();

        // Generate a soft circular texture
        Texture2D circleTex = new Texture2D(32, 32);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(16, 16));
                float alpha = Mathf.Clamp01(1.0f - (dist / 16.0f));
                alpha = Mathf.Pow(alpha, 2.0f);
                circleTex.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }
        circleTex.Apply();

        // Material setup
        if (psRenderer != null)
        {
            psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            psRenderer.velocityScale = 0;
            psRenderer.lengthScale = 0;

            Shader shader = Shader.Find("Particles/Standard Unlit")
                            ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply");
            if (shader != null)
            {
                Material mat = new Material(shader);
                mat.mainTexture = circleTex;
                psRenderer.sharedMaterial = mat;
            }
        }

        // Main module
        var main = _particleSystem.main;
        main.startColor = Color.white;
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(baseSpeed - 5f, baseSpeed + 2f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;

        // Shape
        var shape = _particleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 2.5f;
        shape.radiusThickness = 0.3f;

        // Emission
        var emission = _particleSystem.emission;
        emission.rateOverTime = baseEmissionRate;

        // Orbital velocity for swirl
        var vel = _particleSystem.velocityOverLifetime;
        vel.enabled = true;
        vel.orbitalZ = new ParticleSystem.MinMaxCurve(8f);

        // Size over lifetime
        var sol = _particleSystem.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.2f, 1f),
                new Keyframe(1f, 0f)));

        // Noise
        var noise = _particleSystem.noise;
        noise.enabled = true;
        noise.strength = 0.5f;
        noise.frequency = 1.0f;

        // Color over lifetime: cyan → blue → black
        var col = _particleSystem.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(Color.cyan, 0.0f),
                new GradientColorKey(Color.blue, 0.6f),
                new GradientColorKey(Color.black, 1.0f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1.0f, 0.0f),
                new GradientAlphaKey(1.0f, 0.8f),
                new GradientAlphaKey(0.0f, 1.0f)
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        _particleSystem.Play();
    }

    /// <summary>
    /// Called by NetworkBullet when it enters the portal trigger.
    /// Only the bullet owner calls this so damage is tracked once.
    /// </summary>
    public void AbsorbBullet(float damage)
    {
        // Notify the owning player's manager
        photonView.RPC(nameof(RPC_OnAbsorbed), RpcTarget.All, damage);
    }

    [PunRPC]
    private void RPC_OnAbsorbed(float damage)
    {
        _totalAbsorbedDamage += damage;

        // VFX intensification
        if (_particleSystem != null)
        {
            float factor = 1f + (_totalAbsorbedDamage * 0.01f);

            var emission = _particleSystem.emission;
            emission.rateOverTime = Mathf.Min(baseEmissionRate * factor, 1500f);

            var main = _particleSystem.main;
            main.startSpeed = baseSpeed * Mathf.Clamp(factor, 1f, 4f);

            // Flash burst
            _particleSystem.Emit(80);
        }

        Debug.Log($"[NetworkAbsorbPortal] Absorbed {damage} damage! Total: {_totalAbsorbedDamage}");
    }

    private void DestroyPortal()
    {
        if (photonView.IsMine)
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }
}
