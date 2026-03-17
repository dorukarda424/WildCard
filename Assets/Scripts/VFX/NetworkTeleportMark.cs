using UnityEngine;
using Photon.Pun;

/// <summary>
/// Networked teleport mark. Placed at the point where a kunai projectile hits.
/// Has an optional lifetime and a visual particle indicator.
/// </summary>
public class NetworkTeleportMark : MonoBehaviourPunCallbacks
{
    [Header("Mark Settings")]
    [SerializeField] private float lifetime = 15f;

    private ParticleSystem _indicatorVFX;
    private int _ownerActorNumber = -1;

    public int OwnerActorNumber => _ownerActorNumber;

    private void Awake()
    {
        // Parse owner from instantiation data
        if (photonView.InstantiationData != null && photonView.InstantiationData.Length >= 1)
        {
            _ownerActorNumber = (int)photonView.InstantiationData[0];
        }

        CreateIndicatorVFX();

        if (photonView.IsMine && lifetime > 0)
        {
            Invoke(nameof(DestroyMark), lifetime);
        }
    }

    /// <summary>
    /// Creates a subtle glowing ground marker particle effect.
    /// </summary>
    private void CreateIndicatorVFX()
    {
        _indicatorVFX = gameObject.AddComponent<ParticleSystem>();
        _indicatorVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystemRenderer psRenderer = GetComponent<ParticleSystemRenderer>();

        // Generate a soft circle texture
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

        if (psRenderer != null)
        {
            psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            Shader shader = Shader.Find("Particles/Standard Unlit")
                            ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply");
            if (shader != null)
            {
                Material mat = new Material(shader);
                mat.mainTexture = circleTex;
                psRenderer.sharedMaterial = mat;
            }
        }

        var main = _indicatorVFX.main;
        main.startColor = new Color(1f, 0.8f, 0.2f, 0.8f); // Golden glow
        main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;

        var shape = _indicatorVFX.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.3f;

        var emission = _indicatorVFX.emission;
        emission.rateOverTime = 20f;

        var col = _indicatorVFX.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 0.8f, 0.2f), 0.0f),
                new GradientColorKey(new Color(1f, 0.5f, 0.0f), 1.0f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1.0f, 0.0f),
                new GradientAlphaKey(0.0f, 1.0f)
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        _indicatorVFX.Play();
    }

    private void DestroyMark()
    {
        if (photonView.IsMine)
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }
}
