using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using TMPro;

/// <summary>
/// Displays a floating name tag above the player's head.
/// Uses a world-space Canvas that billboards toward the active camera.
/// Only visible on REMOTE players — the local player never sees their own tag.
/// 
/// Attach this to the Player prefab. It creates its own Canvas at runtime,
/// so no prefab modifications or extra assets are needed.
/// 
/// Network impact: ZERO — reads PhotonView.Owner.NickName, no RPCs or synced data.
/// </summary>
public class PlayerNameTag : MonoBehaviourPunCallbacks
{
    [Header("Positioning")]
    [Tooltip("Height offset above the player's transform origin.")]
    [SerializeField] private float heightOffset = 2.3f;

    [Header("Appearance")]
    [Tooltip("Font size for the name text (world-space units scaled).")]
    [SerializeField] private float fontSize = 3f;
    [Tooltip("Background padding around the text.")]
    [SerializeField] private Vector2 backgroundPadding = new Vector2(0.4f, 0.15f);

    [Header("Visibility")]
    [Tooltip("Maximum distance (meters) at which the name tag is visible.")]
    [SerializeField] private float maxVisibleDistance = 30f;
    [Tooltip("Distance at which the name tag starts fading out.")]
    [SerializeField] private float fadeStartDistance = 20f;

    [Header("Debug")]
    [Tooltip("Enable in editor without Photon to test the name tag.")]
    [SerializeField] private bool testing = false;
    [SerializeField] private string testName = "TestPlayer";

    // Runtime references
    private Canvas _canvas;
    private CanvasGroup _canvasGroup;
    private TextMeshProUGUI _nameText;
    private Image _background;
    private RectTransform _canvasRect;
    private Transform _cameraTransform;
    private bool _isLocalPlayer;
    private bool _initialized;

    private void Start()
    {
        _isLocalPlayer = !testing && photonView != null && photonView.IsMine;

        // Local player should never see their own name tag
        if (_isLocalPlayer)
        {
            enabled = false;
            return;
        }

        CreateNameTagUI();
        SetPlayerName();
        _initialized = true;
    }

    private void LateUpdate()
    {
        if (!_initialized || _canvas == null) return;

        // Cache the camera transform (may change if cameras switch)
        if (_cameraTransform == null || !_cameraTransform.gameObject.activeInHierarchy)
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                // Try to find any active camera
                Camera[] cams = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                foreach (Camera c in cams)
                {
                    if (c.enabled && c.gameObject.activeInHierarchy)
                    {
                        mainCam = c;
                        break;
                    }
                }
            }

            if (mainCam == null)
            {
                _canvas.gameObject.SetActive(false);
                return;
            }

            _cameraTransform = mainCam.transform;
        }

        // Position the canvas above the player
        _canvasRect.position = transform.position + Vector3.up * heightOffset;

        // Billboard: face the camera
        _canvasRect.rotation = Quaternion.LookRotation(
            _canvasRect.position - _cameraTransform.position
        );

        // Distance-based fade
        float distance = Vector3.Distance(_cameraTransform.position, _canvasRect.position);

        if (distance > maxVisibleDistance)
        {
            _canvas.gameObject.SetActive(false);
        }
        else
        {
            _canvas.gameObject.SetActive(true);

            float alpha = 1f;
            if (distance > fadeStartDistance)
            {
                alpha = 1f - Mathf.InverseLerp(fadeStartDistance, maxVisibleDistance, distance);
            }
            _canvasGroup.alpha = alpha;
        }
    }

    /// <summary>
    /// Creates the world-space Canvas, background panel, and text at runtime.
    /// No prefab or asset dependencies — everything is generated via code.
    /// </summary>
    private void CreateNameTagUI()
    {
        // ── Root Canvas Object ──
        GameObject canvasObj = new GameObject("NameTagCanvas");
        canvasObj.transform.SetParent(transform, false);
        canvasObj.transform.localPosition = Vector3.up * heightOffset;

        _canvas = canvasObj.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.sortingOrder = 100; // Render above most world objects

        _canvasRect = _canvas.GetComponent<RectTransform>();
        _canvasRect.sizeDelta = new Vector2(4f, 0.6f);
        _canvasRect.localScale = Vector3.one * 0.05f; // Scale down to world-space units

        _canvasGroup = canvasObj.AddComponent<CanvasGroup>();
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        // ── Background Panel ──
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);

        _background = bgObj.AddComponent<Image>();
        _background.color = new Color(0.1f, 0.1f, 0.1f, 0.65f); // Semi-transparent dark

        RectTransform bgRect = _background.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.5f, 0.5f);
        bgRect.anchorMax = new Vector2(0.5f, 0.5f);
        bgRect.pivot = new Vector2(0.5f, 0.5f);
        bgRect.anchoredPosition = Vector2.zero;

        // ── Name Text ──
        GameObject textObj = new GameObject("NameText");
        textObj.transform.SetParent(canvasObj.transform, false);

        _nameText = textObj.AddComponent<TextMeshProUGUI>();
        _nameText.text = "Player";
        _nameText.fontSize = fontSize;
        _nameText.fontStyle = FontStyles.Bold;
        _nameText.color = Color.white;
        _nameText.alignment = TextAlignmentOptions.Center;
        _nameText.enableWordWrapping = false;
        _nameText.overflowMode = TextOverflowModes.Overflow;
        _nameText.raycastTarget = false;

        RectTransform textRect = _nameText.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(4f, 0.6f);

        // Size the background to fit the text + padding
        // We'll update this once the text is set
        bgRect.sizeDelta = new Vector2(4f, 0.6f);
    }

    /// <summary>
    /// Sets the displayed name from the Photon player's NickName.
    /// Falls back to "Player X" using the actor number.
    /// </summary>
    private void SetPlayerName()
    {
        string displayName;

        if (testing)
        {
            displayName = testName;
        }
        else if (photonView != null && photonView.Owner != null)
        {
            displayName = photonView.Owner.NickName;
            if (string.IsNullOrEmpty(displayName))
            {
                displayName = $"Player {photonView.Owner.ActorNumber}";
            }
        }
        else
        {
            displayName = "Unknown";
        }

        if (_nameText != null)
        {
            _nameText.text = displayName;

            // Auto-size the background to fit the text
            _nameText.ForceMeshUpdate();
            Vector2 textSize = _nameText.GetPreferredValues(displayName);
            if (_background != null)
            {
                RectTransform bgRect = _background.GetComponent<RectTransform>();
                bgRect.sizeDelta = new Vector2(
                    textSize.x + backgroundPadding.x * 2f,
                    textSize.y + backgroundPadding.y * 2f
                );
            }
        }

        Debug.Log($"[PlayerNameTag] Set name tag: '{displayName}' on {gameObject.name}");
    }

    /// <summary>
    /// If the player's NickName changes mid-game (e.g. via OnPlayerPropertiesUpdate),
    /// call this to refresh the displayed name.
    /// </summary>
    public void RefreshName()
    {
        if (_initialized)
        {
            SetPlayerName();
        }
    }

    /// <summary>
    /// Shows or hides the name tag. Called by PlayerHealth.SetPlayerActive
    /// so dead players' name tags disappear.
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (_canvas != null)
        {
            _canvas.gameObject.SetActive(visible);
        }
    }
}
