using UnityEngine;

public class ZoneController : MonoBehaviour
{
    [Header("Zone Settings")]
    [Tooltip("How much the zone shrinks per second")]
    public float shrinkSpeed = 5f;

    [Tooltip("Minimum scale the zone can reach")]
    public float minimumScale = 10f;

    [Header("Boundary Wall")]
    [Tooltip("Height of the visible zone boundary wall")]
    public float wallHeight = 30f;

    [Tooltip("Wall color")]
    public Color wallColor = new Color(0.2f, 0.9f, 0.3f, 0.35f);

    [Tooltip("Wall edge glow color")]
    public Color wallEdgeColor = new Color(0.3f, 1.0f, 0.4f, 0.8f);

    [Header("Poison Gas VFX")]
    [Tooltip("The gas cylinder that covers the whole map (assign in Inspector)")]
    public Transform gasCylinder;

    [Tooltip("Particle system for gas at the zone boundary (optional)")]
    public ParticleSystem boundaryParticles;

    private Vector3 _initialScale;
    private bool _isShrinking;

    // Boundary wall
    private GameObject _boundaryWall;
    private Material _boundaryMaterial;

    void Start()
    {
        _initialScale = new(200,50,200);
        _isShrinking = false;

        CreateBoundaryWall();

        // Subscribe to RoundManager events
        if (RoundManager.Instance != null)
        {
            RoundManager.Instance.OnStateChanged += OnRoundStateChanged;
        }
    }

    void OnDestroy()
    {
        if (RoundManager.Instance != null)
        {
            RoundManager.Instance.OnStateChanged -= OnRoundStateChanged;
        }

        if (_boundaryMaterial != null)
            Destroy(_boundaryMaterial);
    }

    private void OnRoundStateChanged(RoundManager.RoundState state)
    {
        switch (state)
        {
            case RoundManager.RoundState.Countdown:
                // Reset zone to full size at the start of each round
                transform.localScale = _initialScale;
                _isShrinking = false;
                SetGasActive(false);
                SetBoundaryActive(true); // Wall visible during countdown too
                break;

            case RoundManager.RoundState.Fighting:
                // Start shrinking when the fight begins
                _isShrinking = true;
                SetGasActive(true);
                SetBoundaryActive(true);
                break;

            case RoundManager.RoundState.RoundOver:
            case RoundManager.RoundState.CardSelection:
            case RoundManager.RoundState.MatchOver:
                _isShrinking = false;
                break;
        }
    }

    void Update()
    {
        if (!_isShrinking) return;

        Vector3 currentScale = transform.localScale;

        if (currentScale.x > minimumScale)
        {
            float newScaleX = currentScale.x - shrinkSpeed * Time.deltaTime;
            float newScaleZ = currentScale.z - shrinkSpeed * Time.deltaTime;
            newScaleX = Mathf.Max(newScaleX, minimumScale);
            newScaleZ = Mathf.Max(newScaleZ, minimumScale);
            transform.localScale = new Vector3(newScaleX, currentScale.y, newScaleZ);

            // Boundary wall follows the zone scale
            UpdateBoundaryScale(newScaleX, newScaleZ);

            // Update boundary particles to follow the zone edge
            if (boundaryParticles != null)
            {
                var shape = boundaryParticles.shape;
                shape.radius = newScaleX / 2f;
            }
        }
    }

    // ────────── Boundary Wall ──────────

    private void CreateBoundaryWall()
    {
        // Find the shader
        Shader shader = Shader.Find("WildCard/ZoneBoundary");
        if (shader == null)
        {
            Debug.LogWarning("[ZoneController] ZoneBoundary shader not found! " +
                             "Add it to Project Settings → Graphics → Always Included Shaders.");
            return;
        }

        // Create material
        _boundaryMaterial = new Material(shader);
        _boundaryMaterial.SetColor("_Color", wallColor);
        _boundaryMaterial.SetColor("_EdgeColor", wallEdgeColor);

        // Create the wall cylinder as a child of the zone
        _boundaryWall = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _boundaryWall.name = "ZoneBoundaryWall";
        _boundaryWall.transform.SetParent(transform.parent); // Same parent as zone, NOT child of zone (avoid double-scaling)
        _boundaryWall.transform.position = transform.position;

        // Remove collider — it's purely visual
        var col = _boundaryWall.GetComponent<Collider>();
        if (col != null) Destroy(col);

        // Apply material
        var renderer = _boundaryWall.GetComponent<MeshRenderer>();
        renderer.material = _boundaryMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        // Match zone scale — cylinder primitive is 2 units tall, 1 unit diameter
        UpdateBoundaryScale(transform.localScale.x, transform.localScale.z);

        Debug.Log("[ZoneController] Boundary wall created.");
    }

    private void UpdateBoundaryScale(float zoneScaleX, float zoneScaleZ)
    {
        if (_boundaryWall == null) return;

        // Unity cylinder: 1 unit diameter, 2 units tall
        // Zone cylinder scale.x = diameter, so boundary should match
        _boundaryWall.transform.position = transform.position;
        _boundaryWall.transform.localScale = new Vector3(
            zoneScaleX,         // Match zone diameter
            wallHeight / 2f,    // Cylinder is 2 units tall, so height/2
            zoneScaleZ          // Match zone diameter
        );
    }

    private void SetBoundaryActive(bool active)
    {
        if (_boundaryWall != null)
            _boundaryWall.SetActive(active);
    }

    // ────────── Gas VFX ──────────

    private void SetGasActive(bool active)
    {
        if (gasCylinder != null)
            gasCylinder.gameObject.SetActive(active);

        if (boundaryParticles != null)
        {
            if (active) boundaryParticles.Play();
            else boundaryParticles.Stop();
        }
    }
}
