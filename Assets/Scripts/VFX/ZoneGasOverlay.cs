using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

/// <summary>
/// Creates a full-screen UI overlay that shows poison gas effects
/// when the local player is outside the safe zone.
/// Attach to the Player prefab — auto-creates its own Canvas + UI at runtime.
/// Works in ALL render pipelines (Built-in, URP, HDRP).
/// </summary>
[RequireComponent(typeof(PlayerZoneCheck))]
public class ZoneGasOverlay : MonoBehaviour
{
    [Header("Overlay Settings")]
    [Tooltip("How fast the overlay fades in/out")]
    public float fadeSpeed = 3f;

    [Tooltip("Maximum overlay opacity (0-1)")]
    [Range(0f, 1f)]
    public float maxAlpha = 0.35f;

    [Header("Colors")]
    public Color gasColorCenter = new Color(0.1f, 0.5f, 0.05f, 0.2f);
    public Color gasColorEdge = new Color(0.05f, 0.35f, 0.02f, 0.6f);

    [Header("Pulse")]
    [Tooltip("Breathing pulse speed")]
    public float pulseSpeed = 1.5f;
    [Range(0f, 0.2f)]
    public float pulseIntensity = 0.08f;

    [Header("Damage Flash")]
    public Color damageFlashColor = new Color(0.8f, 0.2f, 0.0f, 0.3f);
    public float damageFlashDuration = 0.15f;

    private PlayerZoneCheck _zoneCheck;
    private PlayerHealth _playerHealth;
    private bool _isLocalPlayer;
    private float _currentAlpha;

    // UI elements (created at runtime)
    private Canvas _overlayCanvas;
    private RawImage _vignetteImage;
    private RawImage _noiseImage;
    private Texture2D _vignetteTexture;
    private Texture2D _noiseTexture;

    // Damage flash
    private float _flashTimer;
    private Image _flashImage;

    void Start()
    {
        _zoneCheck = GetComponent<PlayerZoneCheck>();
        _playerHealth = GetComponent<PlayerHealth>();

        _isLocalPlayer = !PhotonNetwork.InRoom
                      || (GetComponent<PhotonView>() is PhotonView pv && pv.IsMine);

        if (_isLocalPlayer)
        {
            CreateOverlayUI();

            if (_playerHealth != null)
                _playerHealth.OnHealthChanged += OnHealthChanged;
        }
    }

    void Update()
    {
        if (!_isLocalPlayer || _vignetteImage == null) return;

        // Target alpha
        float targetAlpha = _zoneCheck.IsOutsideZone ? maxAlpha : 0f;

        // Smooth fade
        _currentAlpha = Mathf.MoveTowards(_currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);

        // Breathing pulse
        float finalAlpha = _currentAlpha;
        if (_currentAlpha > 0.01f)
        {
            float pulse = Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f;
            finalAlpha += pulse * pulseIntensity;
        }

        // Apply vignette alpha
        Color vigColor = _vignetteImage.color;
        vigColor.a = finalAlpha;
        _vignetteImage.color = vigColor;

        // Scroll noise texture for organic movement
        if (_noiseImage != null && _currentAlpha > 0.01f)
        {
            _noiseImage.gameObject.SetActive(true);
            Color noiseColor = _noiseImage.color;
            noiseColor.a = finalAlpha * 0.4f;
            _noiseImage.color = noiseColor;
            _noiseImage.uvRect = new Rect(
                Time.time * 0.03f,
                Time.time * 0.02f,
                1f, 1f
            );
        }
        else if (_noiseImage != null)
        {
            _noiseImage.gameObject.SetActive(false);
        }

        // Damage flash
        if (_flashTimer > 0f)
        {
            _flashTimer -= Time.deltaTime;
            float flashAlpha = (_flashTimer / damageFlashDuration) * damageFlashColor.a;
            Color fc = damageFlashColor;
            fc.a = flashAlpha;
            _flashImage.color = fc;
        }
        else if (_flashImage != null)
        {
            Color fc = _flashImage.color;
            fc.a = 0f;
            _flashImage.color = fc;
        }
    }

    private void OnHealthChanged(float current, float max)
    {
        // Flash red when taking damage while in gas
        if (_zoneCheck != null && _zoneCheck.IsOutsideZone)
        {
            _flashTimer = damageFlashDuration;
        }
    }

    private void CreateOverlayUI()
    {
        // Create an overlay Canvas on the player camera
        var canvasObj = new GameObject("ZoneGasOverlay_Canvas");

        // Find the camera
        var playerCam = GetComponent<PlayerCamera>();
        Camera cam = playerCam != null ? playerCam.GetCamera() : Camera.main;

        if (cam == null)
        {
            Debug.LogWarning("[ZoneGasOverlay] No camera found, retrying in 0.5s...");
            Invoke(nameof(RetryCreateUI), 0.5f);
            Destroy(canvasObj);
            return;
        }

        canvasObj.transform.SetParent(cam.transform, false);

        _overlayCanvas = canvasObj.AddComponent<Canvas>();
        _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _overlayCanvas.sortingOrder = 90; // High sort order, below HUD

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Don't block raycasts
        canvasObj.AddComponent<GraphicRaycaster>().enabled = false;

        // ── Vignette Layer ──
        _vignetteTexture = CreateVignetteTexture(512);
        var vigObj = new GameObject("Vignette");
        vigObj.transform.SetParent(canvasObj.transform, false);

        _vignetteImage = vigObj.AddComponent<RawImage>();
        _vignetteImage.texture = _vignetteTexture;
        _vignetteImage.color = new Color(1f, 1f, 1f, 0f); // Start invisible
        _vignetteImage.raycastTarget = false;

        var vigRect = vigObj.GetComponent<RectTransform>();
        vigRect.anchorMin = Vector2.zero;
        vigRect.anchorMax = Vector2.one;
        vigRect.offsetMin = Vector2.zero;
        vigRect.offsetMax = Vector2.zero;

        // ── Noise Layer (scrolling organic texture) ──
        _noiseTexture = CreateNoiseTexture(256);
        var noiseObj = new GameObject("Noise");
        noiseObj.transform.SetParent(canvasObj.transform, false);

        _noiseImage = noiseObj.AddComponent<RawImage>();
        _noiseImage.texture = _noiseTexture;
        _noiseImage.color = new Color(1f, 1f, 1f, 0f);
        _noiseImage.raycastTarget = false;
        noiseObj.SetActive(false);

        var noiseRect = noiseObj.GetComponent<RectTransform>();
        noiseRect.anchorMin = Vector2.zero;
        noiseRect.anchorMax = Vector2.one;
        noiseRect.offsetMin = Vector2.zero;
        noiseRect.offsetMax = Vector2.zero;

        // ── Damage Flash Layer ──
        var flashObj = new GameObject("DamageFlash");
        flashObj.transform.SetParent(canvasObj.transform, false);

        _flashImage = flashObj.AddComponent<Image>();
        _flashImage.color = new Color(damageFlashColor.r, damageFlashColor.g, damageFlashColor.b, 0f);
        _flashImage.raycastTarget = false;

        var flashRect = flashObj.GetComponent<RectTransform>();
        flashRect.anchorMin = Vector2.zero;
        flashRect.anchorMax = Vector2.one;
        flashRect.offsetMin = Vector2.zero;
        flashRect.offsetMax = Vector2.zero;

        Debug.Log("[ZoneGasOverlay] UI overlay created successfully.");
    }

    private void RetryCreateUI()
    {
        CreateOverlayUI();
    }

    /// <summary>
    /// Creates a radial vignette texture — dark green edges, transparent center.
    /// </summary>
    private Texture2D CreateVignetteTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float halfSize = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - halfSize) / halfSize;
                float dy = (y - halfSize) / halfSize;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // Vignette: transparent in center, colored at edges
                float vignette = Mathf.SmoothStep(0.3f, 1.0f, dist);

                Color c = Color.Lerp(gasColorCenter, gasColorEdge, vignette);
                c.a = vignette;
                tex.SetPixel(x, y, c);
            }
        }

        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    /// <summary>
    /// Creates a tileable green noise texture for organic gas movement.
    /// </summary>
    private Texture2D CreateNoiseTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = x / (float)size;
                float ny = y / (float)size;

                // Multi-octave noise
                float n = 0f;
                n += PerlinLike(nx * 4f, ny * 4f) * 0.5f;
                n += PerlinLike(nx * 8f + 5.2f, ny * 8f + 1.3f) * 0.25f;
                n += PerlinLike(nx * 16f + 9.7f, ny * 16f + 3.1f) * 0.125f;

                // Green-tinted noise
                float r = n * gasColorEdge.r * 1.5f;
                float g = n * gasColorEdge.g * 2f;
                float b = n * gasColorEdge.b * 1.5f;
                float a = n * 0.7f;

                tex.SetPixel(x, y, new Color(r, g, b, a));
            }
        }

        tex.Apply();
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }

    /// <summary>
    /// Simple hash-based noise (Perlin-like, not actual Perlin).
    /// </summary>
    private float PerlinLike(float x, float y)
    {
        // Simple value noise
        int ix = Mathf.FloorToInt(x);
        int iy = Mathf.FloorToInt(y);
        float fx = x - ix;
        float fy = y - iy;

        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);

        float a = Hash(ix, iy);
        float b = Hash(ix + 1, iy);
        float c = Hash(ix, iy + 1);
        float d = Hash(ix + 1, iy + 1);

        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    private float Hash(int x, int y)
    {
        int n = x * 73856093 ^ y * 19349663;
        n = (n << 13) ^ n;
        return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7FFFFFFF) / (float)0x7FFFFFFF;
    }

    void OnDestroy()
    {
        if (_playerHealth != null)
            _playerHealth.OnHealthChanged -= OnHealthChanged;

        if (_vignetteTexture != null) Destroy(_vignetteTexture);
        if (_noiseTexture != null) Destroy(_noiseTexture);
        if (_overlayCanvas != null) Destroy(_overlayCanvas.gameObject);
    }
}
