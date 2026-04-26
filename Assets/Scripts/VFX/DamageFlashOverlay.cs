using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

/// <summary>
/// Full-screen red damage flash overlay.
/// Attach to the Player prefab — auto-creates its own Canvas + UI at runtime.
/// Flashes red whenever the local player takes damage (any source).
/// Inspired by ZoneGasOverlay's damage flash system.
/// </summary>
public class DamageFlashOverlay : MonoBehaviour
{
    [Header("Flash Settings")]
    [Tooltip("Flash color — red tint for bullet hits")]
    public Color flashColor = new Color(0.8f, 0.05f, 0.05f, 0.35f);

    [Tooltip("How long the flash lasts (seconds)")]
    public float flashDuration = 0.25f;

    [Tooltip("Flash intensity scales with damage relative to max health")]
    public bool scaleWithDamage = true;

    [Header("Vignette")]
    [Tooltip("If true, flash is a vignette (edges only). If false, full screen.")]
    public bool useVignette = true;

    private PlayerHealth _playerHealth;
    private bool _isLocalPlayer;
    private float _previousHealth;

    // UI elements
    private Canvas _overlayCanvas;
    private RawImage _flashImage;
    private Texture2D _vignetteTexture;
    private float _flashTimer;
    private float _flashIntensity;

    void Start()
    {
        _playerHealth = GetComponent<PlayerHealth>();
        if (_playerHealth == null) return;

        _isLocalPlayer = !PhotonNetwork.InRoom
                      || (GetComponent<PhotonView>() is PhotonView pv && pv.IsMine);

        if (_isLocalPlayer)
        {
            _previousHealth = _playerHealth.CurrentHealth;
            CreateOverlayUI();
            _playerHealth.OnHealthChanged += OnHealthChanged;
        }
    }

    private void OnHealthChanged(float current, float max)
    {
        float delta = _previousHealth - current;
        _previousHealth = current;

        // Only flash on damage (not healing)
        if (delta <= 0f) return;

        // Scale intensity based on damage percentage
        if (scaleWithDamage && max > 0f)
        {
            _flashIntensity = Mathf.Clamp01(delta / max) * 2f; // 50% health hit = full intensity
            _flashIntensity = Mathf.Clamp01(_flashIntensity);
        }
        else
        {
            _flashIntensity = 1f;
        }

        // Minimum visible flash
        _flashIntensity = Mathf.Max(_flashIntensity, 0.4f);
        _flashTimer = flashDuration;
    }

    void Update()
    {
        if (!_isLocalPlayer || _flashImage == null) return;

        if (_flashTimer > 0f)
        {
            _flashTimer -= Time.deltaTime;
            float t = Mathf.Clamp01(_flashTimer / flashDuration);

            // Ease out curve for smooth fade
            float alpha = t * t * flashColor.a * _flashIntensity;

            Color c = flashColor;
            c.a = alpha;
            _flashImage.color = c;
        }
        else
        {
            Color c = _flashImage.color;
            if (c.a > 0f)
            {
                c.a = 0f;
                _flashImage.color = c;
            }
        }
    }

    private void CreateOverlayUI()
    {
        var canvasObj = new GameObject("DamageFlashOverlay_Canvas");

        // Find the camera
        var playerCam = GetComponent<PlayerCamera>();
        Camera cam = playerCam != null ? playerCam.GetCamera() : Camera.main;

        if (cam == null)
        {
            Debug.LogWarning("[DamageFlashOverlay] No camera found, retrying in 0.5s...");
            Invoke(nameof(RetryCreateUI), 0.5f);
            Destroy(canvasObj);
            return;
        }

        canvasObj.transform.SetParent(cam.transform, false);

        _overlayCanvas = canvasObj.AddComponent<Canvas>();
        _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _overlayCanvas.sortingOrder = 95; // Above gas overlay (90), below main HUD

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Don't block raycasts
        canvasObj.AddComponent<GraphicRaycaster>().enabled = false;

        // Create flash image
        if (useVignette)
        {
            _vignetteTexture = CreateDamageVignetteTexture(512);
            var flashObj = new GameObject("DamageVignette");
            flashObj.transform.SetParent(canvasObj.transform, false);

            _flashImage = flashObj.AddComponent<RawImage>();
            _flashImage.texture = _vignetteTexture;
            _flashImage.color = new Color(1f, 1f, 1f, 0f); // Start invisible
            _flashImage.raycastTarget = false;

            var rect = flashObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
        else
        {
            var flashObj = new GameObject("DamageFlash");
            flashObj.transform.SetParent(canvasObj.transform, false);

            _flashImage = flashObj.AddComponent<RawImage>();
            _flashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
            _flashImage.raycastTarget = false;

            var rect = flashObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        Debug.Log("[DamageFlashOverlay] UI overlay created successfully.");
    }

    private void RetryCreateUI()
    {
        CreateOverlayUI();
    }

    /// <summary>
    /// Creates a radial red vignette — dark red edges, transparent center.
    /// </summary>
    private Texture2D CreateDamageVignetteTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float halfSize = size / 2f;

        Color edgeColor = new Color(0.7f, 0.0f, 0.0f, 0.9f);
        Color centerColor = new Color(0.5f, 0.0f, 0.0f, 0.0f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - halfSize) / halfSize;
                float dy = (y - halfSize) / halfSize;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // More aggressive vignette — visible from edges inward
                float vignette = Mathf.SmoothStep(0.4f, 1.0f, dist);

                Color c = Color.Lerp(centerColor, edgeColor, vignette);
                c.a = vignette;
                tex.SetPixel(x, y, c);
            }
        }

        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    void OnDestroy()
    {
        if (_playerHealth != null)
            _playerHealth.OnHealthChanged -= OnHealthChanged;

        if (_vignetteTexture != null) Destroy(_vignetteTexture);
        if (_overlayCanvas != null) Destroy(_overlayCanvas.gameObject);
    }
}
