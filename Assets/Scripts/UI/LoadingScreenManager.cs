using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

/// <summary>
/// Canvas-based loading screen overlay that persists across scene transitions.
/// Auto-creates itself via [RuntimeInitializeOnLoadMethod] — no prefab, no scene setup.
/// 
/// Usage:
///   LoadingScreenManager.Show();             // Call before any LoadLevel / LoadScene
///   Hide is AUTOMATIC via SceneManager.sceneLoaded callback.
///
/// Design: Screen Space Overlay canvas (no camera needed), so it stays visible
/// even when all scene cameras are destroyed during transitions.
/// 
/// Network impact: ZERO — purely local visual overlay.
/// </summary>
public class LoadingScreenManager : MonoBehaviour
{
    public static LoadingScreenManager Instance { get; private set; }

    // ── UI References (created at runtime) ──
    private Canvas _canvas;
    private CanvasGroup _canvasGroup;
    private TextMeshProUGUI _loadingText;
    private TextMeshProUGUI _tipText;
    private Image _progressBarFill;
    private RectTransform[] _dots;

    // ── Configuration ──
    // Place your background image at: Assets/Resources/LoadingBackground.png (or .jpg)
    // It will be loaded automatically. If not found, a solid dark color is used.
    private const string BG_SPRITE_PATH = "LoadingBackground";

    // ── State ──
    private bool _isShowing;
    private Coroutine _fadeCoroutine;
    private Coroutine _animCoroutine;

    // ── Tips shown randomly during loading ──
    private static readonly string[] Tips = new string[]
    {
        "Cards can change the tide of battle...",
        "Use the warmup lobby to practice your aim!",
        "Sprinting + Crouching starts a slide.",
        "Double jump cards let you reach new heights.",
        "Shield cards block one hit of damage.",
        "Wall latching gives you a tactical advantage.",
        "Reload during downtime, not during a fight!"
    };

    // ──────────────────────────────────────────
    //  AUTO CREATION
    // ──────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;

        var go = new GameObject("[LoadingScreen]");
        go.AddComponent<LoadingScreenManager>();
        DontDestroyOnLoad(go);
    }

    // ──────────────────────────────────────────
    //  LIFECYCLE
    // ──────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        CreateUI();

        // Start fully hidden
        _canvasGroup.alpha = 0f;
        _canvas.gameObject.SetActive(false);
        _isShowing = false;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    // ──────────────────────────────────────────
    //  PUBLIC API
    // ──────────────────────────────────────────

    /// <summary>
    /// Show the loading screen with a fade-in. Call this before LoadLevel/LoadScene.
    /// </summary>
    public static void Show()
    {
        if (Instance != null) Instance.ShowInternal();
    }

    /// <summary>
    /// Manually hide the loading screen. Usually not needed — auto-hides on scene load.
    /// </summary>
    public static void Hide()
    {
        if (Instance != null) Instance.HideInternal();
    }

    // ──────────────────────────────────────────
    //  INTERNAL
    // ──────────────────────────────────────────

    private void ShowInternal()
    {
        if (_isShowing) return;
        _isShowing = true;

        // Pick a random tip
        if (_tipText != null)
        {
            _tipText.text = Tips[Random.Range(0, Tips.Length)];
        }

        _canvas.gameObject.SetActive(true);

        StopAllAnimations();
        _fadeCoroutine = StartCoroutine(Fade(1f, 0.25f));
        _animCoroutine = StartCoroutine(AnimateLoop());
    }

    private void HideInternal()
    {
        if (!_isShowing) return;

        StopAllAnimations();
        _fadeCoroutine = StartCoroutine(FadeOutAndDisable());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_isShowing)
        {
            // Brief delay so the scene has time to initialize (cameras, etc.)
            StartCoroutine(DelayedHide(0.4f));
        }
    }

    private IEnumerator DelayedHide(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        HideInternal();
    }

    private void StopAllAnimations()
    {
        if (_fadeCoroutine != null) { StopCoroutine(_fadeCoroutine); _fadeCoroutine = null; }
        if (_animCoroutine != null) { StopCoroutine(_animCoroutine); _animCoroutine = null; }
    }

    // ──────────────────────────────────────────
    //  FADE COROUTINES
    // ──────────────────────────────────────────

    private IEnumerator Fade(float target, float duration)
    {
        float start = _canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }
        _canvasGroup.alpha = target;
    }

    private IEnumerator FadeOutAndDisable()
    {
        yield return Fade(0f, 0.5f);

        _canvas.gameObject.SetActive(false);
        _isShowing = false;
    }

    // ──────────────────────────────────────────
    //  ANIMATION LOOP
    // ──────────────────────────────────────────

    private IEnumerator AnimateLoop()
    {
        float time = 0f;

        while (_isShowing)
        {
            time += Time.unscaledDeltaTime;

            // Animate loading dots (wave pulse with cyan↔magenta color shift)
            if (_dots != null)
            {
                Color cyan = new Color(0f, 0.9f, 1f, 1f);
                Color magenta = new Color(1f, 0.2f, 0.8f, 1f);

                for (int i = 0; i < _dots.Length; i++)
                {
                    if (_dots[i] == null) continue;
                    float phase = time * 4f + i * 0.7f;
                    float y = Mathf.Sin(phase) * 6f;
                    float alpha = 0.4f + Mathf.Sin(phase) * 0.6f;

                    _dots[i].anchoredPosition = new Vector2(_dots[i].anchoredPosition.x, y);

                    // Smoothly shift color between cyan and magenta
                    float colorT = (Mathf.Sin(time * 2f + i * 1.2f) + 1f) * 0.5f;
                    Color dotColor = Color.Lerp(cyan, magenta, colorT);
                    dotColor.a = Mathf.Clamp01(alpha);

                    var img = _dots[i].GetComponent<Image>();
                    if (img != null) img.color = dotColor;
                }
            }

            // Animate progress bar (indeterminate shimmer + color shift)
            if (_progressBarFill != null)
            {
                _progressBarFill.fillAmount = Mathf.PingPong(time * 0.35f, 0.8f) + 0.15f;

                // Shift bar color cyan → magenta → cyan
                float barColorT = (Mathf.Sin(time * 1.5f) + 1f) * 0.5f;
                Color barCyan = new Color(0f, 0.9f, 1f, 0.85f);
                Color barMagenta = new Color(1f, 0.2f, 0.8f, 0.85f);
                _progressBarFill.color = Color.Lerp(barCyan, barMagenta, barColorT);
            }

            // Animate loading text dots
            if (_loadingText != null)
            {
                int dotCount = ((int)(time * 2f)) % 4;
                _loadingText.text = "LOADING" + new string('.', dotCount);
            }

            yield return null;
        }
    }

    // ══════════════════════════════════════════════════════════
    //  UI CREATION (all runtime, no prefab needed)
    // ══════════════════════════════════════════════════════════

    private void CreateUI()
    {
        // ── Root Canvas ──
        GameObject canvasObj = new GameObject("LoadingCanvas");
        canvasObj.transform.SetParent(transform, false);

        _canvas = canvasObj.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 9999;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        _canvasGroup = canvasObj.AddComponent<CanvasGroup>();
        _canvasGroup.blocksRaycasts = true;  // Block input during loading
        _canvasGroup.interactable = false;

        // ── Background ──
        GameObject bgObj = MakeUIObj("Bg", canvasObj.transform);
        var bg = bgObj.AddComponent<Image>();

        // Try to load a custom background image from Resources
        Sprite bgSprite = Resources.Load<Sprite>(BG_SPRITE_PATH);
        if (bgSprite != null)
        {
            bg.sprite = bgSprite;
            bg.type = Image.Type.Simple;
            bg.preserveAspect = false;  // Stretch to fill screen
            bg.color = Color.white;     // Show image at full brightness
            Debug.Log("[LoadingScreen] Custom background loaded.");
        }
        else
        {
            bg.color = new Color(0.04f, 0.04f, 0.08f, 1f); // Fallback: dark solid
        }
        Stretch(bgObj);

        // ── Accent line (top) ──
        GameObject accentObj = MakeUIObj("Accent", canvasObj.transform);
        var accent = accentObj.AddComponent<Image>();
        accent.color = new Color(0f, 0.9f, 1f, 0.9f); // Neon cyan
        RectTransform accentRect = accentObj.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 1f);
        accentRect.anchorMax = new Vector2(1f, 1f);
        accentRect.pivot = new Vector2(0.5f, 1f);
        accentRect.sizeDelta = new Vector2(0, 3);
        accentRect.anchoredPosition = Vector2.zero;

        // ── Center Container ──
        GameObject centerObj = MakeUIObj("Center", canvasObj.transform);
        RectTransform centerRect = centerObj.GetComponent<RectTransform>();
        centerRect.anchorMin = new Vector2(0.5f, 0.5f);
        centerRect.anchorMax = new Vector2(0.5f, 0.5f);
        centerRect.pivot = new Vector2(0.5f, 0.5f);
        centerRect.sizeDelta = new Vector2(500, 200);

        // ── Loading Text ──
        GameObject textObj = MakeUIObj("LoadingText", centerObj.transform);
        _loadingText = textObj.AddComponent<TextMeshProUGUI>();
        _loadingText.text = "LOADING";
        _loadingText.fontSize = 32;
        _loadingText.fontStyle = FontStyles.Bold;
        _loadingText.color = new Color(0.85f, 0.92f, 1f, 0.95f); // Bright white-cyan
        _loadingText.alignment = TextAlignmentOptions.Center;
        _loadingText.characterSpacing = 10f;
        _loadingText.raycastTarget = false;
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(400, 50);
        textRect.anchoredPosition = new Vector2(0, 10);

        // ── Animated Dots ──
        _dots = new RectTransform[3];
        float spacing = 22f;
        float startX = -(spacing * (_dots.Length - 1)) / 2f;

        for (int i = 0; i < _dots.Length; i++)
        {
            GameObject dotObj = MakeUIObj($"Dot{i}", centerObj.transform);
            var dotImg = dotObj.AddComponent<Image>();
            // Alternate dots between cyan and magenta
            Color dotColor = (i % 2 == 0)
                ? new Color(0f, 0.9f, 1f, 0.85f)    // Cyan
                : new Color(1f, 0.2f, 0.8f, 0.85f);  // Magenta
            dotImg.color = dotColor;

            RectTransform dotRect = dotObj.GetComponent<RectTransform>();
            dotRect.anchorMin = new Vector2(0.5f, 0.5f);
            dotRect.anchorMax = new Vector2(0.5f, 0.5f);
            dotRect.pivot = new Vector2(0.5f, 0.5f);
            dotRect.sizeDelta = new Vector2(10, 10);
            dotRect.anchoredPosition = new Vector2(startX + i * spacing, -25f);

            _dots[i] = dotRect;
        }

        // ── Progress Bar Background ──
        GameObject barBgObj = MakeUIObj("BarBg", canvasObj.transform);
        var barBg = barBgObj.AddComponent<Image>();
        barBg.color = new Color(0.08f, 0.06f, 0.15f, 0.7f); // Dark purple
        RectTransform barBgRect = barBgObj.GetComponent<RectTransform>();
        barBgRect.anchorMin = new Vector2(0.3f, 0.38f);
        barBgRect.anchorMax = new Vector2(0.7f, 0.38f);
        barBgRect.pivot = new Vector2(0.5f, 0.5f);
        barBgRect.sizeDelta = new Vector2(0, 4);

        // ── Progress Bar Fill ──
        GameObject barFillObj = MakeUIObj("BarFill", barBgObj.transform);
        _progressBarFill = barFillObj.AddComponent<Image>();
        _progressBarFill.color = new Color(0f, 0.9f, 1f, 0.85f); // Neon cyan
        _progressBarFill.type = Image.Type.Filled;
        _progressBarFill.fillMethod = Image.FillMethod.Horizontal;
        _progressBarFill.fillAmount = 0.1f;
        Stretch(barFillObj);

        // ── Tip Text (bottom) ──
        GameObject tipObj = MakeUIObj("TipText", canvasObj.transform);
        _tipText = tipObj.AddComponent<TextMeshProUGUI>();
        _tipText.text = "";
        _tipText.fontSize = 18;
        _tipText.fontStyle = FontStyles.Italic;
        _tipText.color = new Color(0.7f, 0.6f, 0.85f, 0.8f); // Soft lavender
        _tipText.alignment = TextAlignmentOptions.Center;
        _tipText.raycastTarget = false;
        RectTransform tipRect = tipObj.GetComponent<RectTransform>();
        tipRect.anchorMin = new Vector2(0.2f, 0.15f);
        tipRect.anchorMax = new Vector2(0.8f, 0.15f);
        tipRect.pivot = new Vector2(0.5f, 0.5f);
        tipRect.sizeDelta = new Vector2(0, 40);
    }

    // ── Helpers ──

    private GameObject MakeUIObj(string name, Transform parent)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private void Stretch(GameObject obj)
    {
        var rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
