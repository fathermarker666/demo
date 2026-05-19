using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BullTimingRing : MonoBehaviour
{
    public event Action<string> OnTimingResolved;

    public enum TimingMode
    {
        Capa,
        Estocada
    }

    [Header("Visual")]
    public float startScale = 3f;
    public float endScale = 0.72f;
    public float goodProgress = 0.58f;
    public float perfectProgress = 0.82f;
    public float goodDistanceMax = 2.2f;
    public float goodDistanceMin = 1.6f;
    public float perfectDistanceMax = 1.6f;
    public float perfectDistanceMin = 1.2f;
    public KeyCode prototypeInput = KeyCode.Space;
    public Color missColor = Color.red;
    public Color goodColor = new Color(1f, 0.5f, 0f, 1f);
    public Color perfectColor = Color.green;

    [Header("UI")]
    public TextMeshProUGUI feedbackText;
    public Image ringImage;
    public BullfightPlayerController playerController;
    public BullAI bullAI;

    private float currentProgress;
    private float currentScale;
    private float feedbackTimer;
    private float activeGoodProgress;
    private float activePerfectProgress;
    private bool isActive;
    private bool inputArmed;
    private RectTransform ringRect;
    private RectTransform feedbackRect;
    private Action<string> onResolved;
    private TimingMode currentMode;
    private Canvas overlayCanvas;
    private Canvas sourceWorldCanvas;
    private Image sourceRingImage;
    private TextMeshProUGUI sourceFeedbackText;
    private Image runtimeRingImage;
    private TextMeshProUGUI runtimeFeedbackText;
    private Sprite fallbackSprite;
    private readonly Vector2 capaScreenOffset = new Vector2(0f, 96f);
    private readonly Vector2 feedbackScreenOffset = new Vector2(0f, 84f);

    public bool IsActive => isActive;
    public bool IsInputArmed => isActive && inputArmed;
    public float CurrentProgress => currentProgress;
    public float DefaultGoodProgress => goodProgress;
    public float DefaultPerfectProgress => perfectProgress;
    public float LegacyChargeRevealDistance => Mathf.Max(goodDistanceMax, goodDistanceMin, perfectDistanceMax, perfectDistanceMin);
    public Vector2 FeedbackAnchoredPosition => feedbackRect != null ? feedbackRect.anchoredPosition : feedbackScreenOffset;

    private void Awake()
    {
        ResolveReferencesIfNeeded();
        ResetTimingWindow();
        ConfigurePresentationForMode(TimingMode.Estocada);
        HideAll();
    }

    private void Start()
    {
        ResolveReferencesIfNeeded();
    }

    private void Update()
    {
        if (HasMissingReferences())
            ResolveReferencesIfNeeded();

        UpdateFeedbackTimer();
        UpdateTrackedPresentation();

        if (!isActive || !inputArmed)
            return;

        bool pressed = ConsumeCurrentModeInput();
        if (!pressed)
            return;

        Resolve(EvaluateResult());
    }

    public void StartTiming(TimingMode mode, Action<string> callback = null, bool showRing = true, bool armInput = true)
    {
        if (HasMissingReferences())
            ResolveReferencesIfNeeded();

        currentMode = mode;
        ConfigurePresentationForMode(mode);
        onResolved = callback;
        isActive = true;
        inputArmed = armInput;
        SetTelegraphProgress(0f);

        feedbackTimer = 0f;

        if (ringImage != null)
            ringImage.enabled = showRing;

        if (feedbackText != null)
        {
            feedbackText.text = string.Empty;
            feedbackText.gameObject.SetActive(false);
        }
    }

    public void SetTelegraphProgress(float progress01)
    {
        currentProgress = Mathf.Clamp01(progress01);
        currentScale = Mathf.Lerp(startScale, endScale, currentProgress);

        if (ringRect != null)
            ringRect.localScale = Vector3.one * currentScale;

        UpdateRingColor();
    }

    public void HideImmediate()
    {
        isActive = false;
        inputArmed = false;
        onResolved = null;
        feedbackTimer = 0f;

        if (ringImage != null)
            ringImage.enabled = false;

        if (feedbackText != null)
        {
            feedbackText.text = string.Empty;
            feedbackText.gameObject.SetActive(false);
        }
    }

    public void HideRingKeepFeedback()
    {
        isActive = false;
        inputArmed = false;
        onResolved = null;

        if (ringImage != null)
            ringImage.enabled = false;
    }

    public void SetRingVisible(bool visible)
    {
        if (ringImage != null)
            ringImage.enabled = isActive && visible;
    }

    public void SetInputArmed(bool armed)
    {
        inputArmed = isActive && armed;
    }

    public void RevealAndArm()
    {
        ConfigurePresentationForMode(currentMode);
        SetRingVisible(true);
        SetInputArmed(true);

        if (ringImage != null)
            ringImage.gameObject.SetActive(true);
    }

    public void SetTimingWindow(float good, float perfect)
    {
        activeGoodProgress = Mathf.Clamp01(good);
        activePerfectProgress = Mathf.Clamp01(Mathf.Max(activeGoodProgress, perfect));
        UpdateRingColor();
    }

    public void ResetTimingWindow()
    {
        activeGoodProgress = Mathf.Clamp01(goodProgress);
        activePerfectProgress = Mathf.Clamp01(Mathf.Max(activeGoodProgress, perfectProgress));
        UpdateRingColor();
    }

    public void ResolveExternal(string result)
    {
        if (!isActive)
            return;

        Resolve(result);
    }

    public void ShowFeedback(string result)
    {
        if (feedbackText == null)
            return;

        feedbackText.text = result switch
        {
            "Perfect!" => "PERFECT",
            "Good" => "GOOD",
            _ => "MISS"
        };
        feedbackText.color = result switch
        {
            "Perfect!" => perfectColor,
            "Good" => goodColor,
            _ => missColor
        };
        feedbackText.gameObject.SetActive(true);
        feedbackTimer = 1.5f;

        PlayerStats playerStats = bullAI != null ? bullAI.playerStats : null;
        if (playerStats != null)
        {
            switch (result)
            {
                case "Perfect!":
                    if (currentMode == TimingMode.Capa)
                        playerStats.PlayGamepadRumble(0.85f, 1f, 0.3f);
                    else
                        playerStats.PlayGamepadRumble(0.2f, 0.55f, 0.14f);
                    break;
                case "Good":
                    if (currentMode == TimingMode.Capa)
                        playerStats.PlayGamepadRumble(0.24f, 0.62f, 0.16f);
                    else
                        playerStats.PlayGamepadRumble(0.12f, 0.32f, 0.1f);
                    break;
                default:
                    playerStats.PlayGamepadRumble(0.28f, 0.68f, 0.18f);
                    break;
            }
        }
    }

    private void Resolve(string result)
    {
        isActive = false;

        if (ringImage != null)
            ringImage.enabled = false;

        ShowFeedback(result);

        Action<string> callback = onResolved;
        onResolved = null;
        OnTimingResolved?.Invoke(result);
        callback?.Invoke(result);
    }

    private void UpdateRingColor()
    {
        if (ringImage == null)
            return;

        if (currentProgress < activeGoodProgress)
            ringImage.color = missColor;
        else if (currentProgress < activePerfectProgress)
            ringImage.color = goodColor;
        else
            ringImage.color = perfectColor;
    }

    private bool ConsumeCurrentModeInput()
    {
        if (playerController == null)
            return Input.GetKeyDown(prototypeInput);

        return currentMode switch
        {
            TimingMode.Estocada => playerController.ConsumePhaseTwoStabPressed(),
            _ => playerController.ConsumeCapaPressed()
        };
    }

    private void UpdateFeedbackTimer()
    {
        if (feedbackTimer <= 0f)
            return;

        feedbackTimer -= Time.unscaledDeltaTime;
        if (feedbackTimer <= 0f && feedbackText != null)
        {
            feedbackText.text = string.Empty;
            feedbackText.gameObject.SetActive(false);
        }
    }

    private bool HasMissingReferences()
    {
        return sourceRingImage == null ||
               sourceFeedbackText == null ||
               sourceWorldCanvas == null ||
               playerController == null ||
               bullAI == null;
    }

    private void ResolveReferencesIfNeeded()
    {
        if (sourceRingImage == null)
        {
            sourceRingImage = ringImage != null ? ringImage : GetComponent<Image>();
            if (sourceRingImage == null)
                sourceRingImage = GetComponentInChildren<Image>(true);
        }

        if (sourceFeedbackText == null)
        {
            sourceFeedbackText = feedbackText;
            if (sourceFeedbackText == null)
            {
                Transform sibling = transform.parent != null ? transform.parent.Find("feedbackText") : null;
                if (sibling != null)
                    sourceFeedbackText = sibling.GetComponent<TextMeshProUGUI>();
                if (sourceFeedbackText == null)
                    sourceFeedbackText = GetComponentInParent<TextMeshProUGUI>(true);
            }
        }

        if (sourceWorldCanvas == null)
        {
            if (sourceRingImage != null)
                sourceWorldCanvas = sourceRingImage.GetComponentInParent<Canvas>(true);
            else if (sourceFeedbackText != null)
                sourceWorldCanvas = sourceFeedbackText.GetComponentInParent<Canvas>(true);
        }

        if (runtimeRingImage != null)
        {
            ringImage = runtimeRingImage;
            ringRect = runtimeRingImage.rectTransform;
        }
        else if (sourceRingImage != null)
        {
            ringImage = sourceRingImage;
            ringRect = sourceRingImage.rectTransform;
        }

        if (runtimeFeedbackText != null)
        {
            feedbackText = runtimeFeedbackText;
            feedbackRect = runtimeFeedbackText.rectTransform;
        }
        else if (sourceFeedbackText != null)
        {
            feedbackText = sourceFeedbackText;
            feedbackRect = sourceFeedbackText.rectTransform;
        }

        if (playerController == null)
            playerController = BullfightSceneCache.FindObject<BullfightPlayerController>();

        if (bullAI == null)
            bullAI = BullfightSceneCache.FindObject<BullAI>();
    }

    private void HideAll()
    {
        if (runtimeRingImage != null)
            runtimeRingImage.enabled = false;

        if (runtimeFeedbackText != null)
            runtimeFeedbackText.gameObject.SetActive(false);

        if (sourceRingImage != null)
            sourceRingImage.enabled = false;

        if (ringImage != null)
            ringImage.enabled = false;

        if (feedbackText != null)
        {
            feedbackTimer = 0f;
            feedbackText.gameObject.SetActive(false);
        }

        if (sourceFeedbackText != null)
            sourceFeedbackText.gameObject.SetActive(false);
    }

    private void EnsureOverlayPresentation()
    {
        if (overlayCanvas == null)
        {
            GameObject runtimeCanvasObject = GameObject.Find("QTE_Runtime_Canvas");
            if (runtimeCanvasObject == null)
            {
                runtimeCanvasObject = new GameObject("QTE_Runtime_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                overlayCanvas = runtimeCanvasObject.GetComponent<Canvas>();
                CanvasScaler scaler = runtimeCanvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }
            else
            {
                overlayCanvas = runtimeCanvasObject.GetComponent<Canvas>();
            }

            if (overlayCanvas != null)
            {
                overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                overlayCanvas.sortingOrder = 5000;
            }
        }

        if (overlayCanvas == null)
            return;

        EnsureRuntimeUiObjects();

        ConfigureFeedbackRect();
    }

    private void EnsureRuntimeUiObjects()
    {
        if (overlayCanvas == null)
            return;

        if (runtimeRingImage == null)
        {
            GameObject ringObject = new GameObject("QTE_Ring", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = ringObject.GetComponent<RectTransform>();
            rect.SetParent(overlayCanvas.transform, false);

            runtimeRingImage = ringObject.GetComponent<Image>();
            runtimeRingImage.raycastTarget = false;
            if (sourceRingImage != null)
            {
                runtimeRingImage.sprite = sourceRingImage.sprite;
                runtimeRingImage.material = sourceRingImage.material;
                runtimeRingImage.type = sourceRingImage.type;
                runtimeRingImage.preserveAspect = sourceRingImage.preserveAspect;
                runtimeRingImage.fillMethod = sourceRingImage.fillMethod;
                runtimeRingImage.fillOrigin = sourceRingImage.fillOrigin;
                runtimeRingImage.fillClockwise = sourceRingImage.fillClockwise;
                runtimeRingImage.fillAmount = sourceRingImage.fillAmount;
            }
            else
            {
                runtimeRingImage.sprite = GetFallbackSprite();
                runtimeRingImage.type = Image.Type.Simple;
            }

            runtimeRingImage.color = missColor;
        }

        if (runtimeFeedbackText == null)
        {
            GameObject textObject = new GameObject("QTE_Feedback", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(overlayCanvas.transform, false);

            runtimeFeedbackText = textObject.GetComponent<TextMeshProUGUI>();
            runtimeFeedbackText.raycastTarget = false;
            runtimeFeedbackText.text = string.Empty;
            runtimeFeedbackText.font = sourceFeedbackText != null
                ? sourceFeedbackText.font
                : TMPro.TMP_Settings.defaultFontAsset;
            if (sourceFeedbackText != null && sourceFeedbackText.fontSharedMaterial != null)
                runtimeFeedbackText.fontSharedMaterial = sourceFeedbackText.fontSharedMaterial;
            runtimeFeedbackText.fontSize = sourceFeedbackText != null ? sourceFeedbackText.fontSize : 42f;
            runtimeFeedbackText.alignment = sourceFeedbackText != null ? sourceFeedbackText.alignment : TextAlignmentOptions.Center;
            runtimeFeedbackText.color = sourceFeedbackText != null ? sourceFeedbackText.color : Color.white;
        }

        if (sourceRingImage != null && sourceRingImage != runtimeRingImage)
            sourceRingImage.enabled = false;

        if (sourceFeedbackText != null && sourceFeedbackText != runtimeFeedbackText)
            sourceFeedbackText.enabled = false;

        DisableLegacyWorldSpacePresentation();

        ringImage = runtimeRingImage;
        ringRect = runtimeRingImage.rectTransform;
        feedbackText = runtimeFeedbackText;
        feedbackRect = runtimeFeedbackText.rectTransform;
        runtimeRingImage.transform.SetAsLastSibling();
        runtimeFeedbackText.transform.SetAsLastSibling();
        ConfigureRingRect();
    }

    private Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null)
            return fallbackSprite;

        Texture2D texture = Texture2D.whiteTexture;
        fallbackSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        return fallbackSprite;
    }

    private void ConfigureRingRect()
    {
        if (ringRect == null)
            return;

        Vector2 targetSize = ringRect.sizeDelta;
        if (targetSize.x <= 1f || targetSize.y <= 1f)
            targetSize = new Vector2(220f, 220f);

        ringRect.anchorMin = new Vector2(0.5f, 0.5f);
        ringRect.anchorMax = new Vector2(0.5f, 0.5f);
        ringRect.pivot = new Vector2(0.5f, 0.5f);
        ringRect.anchoredPosition = Vector2.zero;
        ringRect.sizeDelta = targetSize;
        ringRect.localScale = Vector3.one;
        ringRect.localRotation = Quaternion.identity;
    }

    private void ConfigureFeedbackRect()
    {
        if (feedbackRect == null || feedbackText == null)
            return;

        feedbackRect.anchorMin = new Vector2(0.5f, 0.5f);
        feedbackRect.anchorMax = new Vector2(0.5f, 0.5f);
        feedbackRect.pivot = new Vector2(0.5f, 0.5f);
        feedbackRect.anchoredPosition = feedbackScreenOffset;
        feedbackRect.sizeDelta = new Vector2(520f, 120f);
        feedbackRect.localScale = Vector3.one;
        feedbackRect.localRotation = Quaternion.identity;

        feedbackText.alignment = TextAlignmentOptions.Center;
        feedbackText.fontSize = Mathf.Max(feedbackText.fontSize, 54f);
        feedbackText.enableWordWrapping = false;
        feedbackText.overflowMode = TextOverflowModes.Overflow;
    }

    private void DisableLegacyWorldSpacePresentation()
    {
        if (sourceWorldCanvas == null || sourceWorldCanvas == overlayCanvas)
            return;

        sourceWorldCanvas.enabled = false;

        if (sourceRingImage != null && sourceRingImage != runtimeRingImage)
            sourceRingImage.enabled = false;

        if (sourceFeedbackText != null && sourceFeedbackText != runtimeFeedbackText)
            sourceFeedbackText.enabled = false;
    }

    private void EnsureLegacyWorldPresentation()
    {
        if (sourceWorldCanvas != null)
            sourceWorldCanvas.enabled = true;

        if (sourceRingImage != null)
        {
            sourceRingImage.gameObject.SetActive(true);
            sourceRingImage.enabled = false;
            ringImage = sourceRingImage;
            ringRect = sourceRingImage.rectTransform;
        }

        if (sourceFeedbackText != null)
        {
            sourceFeedbackText.gameObject.SetActive(false);
            sourceFeedbackText.enabled = true;
            feedbackText = sourceFeedbackText;
            feedbackRect = sourceFeedbackText.rectTransform;
        }

        if (runtimeRingImage != null)
            runtimeRingImage.enabled = false;

        if (runtimeFeedbackText != null)
            runtimeFeedbackText.gameObject.SetActive(false);
    }

    private void ConfigurePresentationForMode(TimingMode mode)
    {
        EnsureOverlayPresentation();
    }

    private void UpdateTrackedPresentation()
    {
        if ((isActive || feedbackTimer > 0f) && currentMode == TimingMode.Capa)
        {
            Vector2 anchor = GetCapaAnchorPosition();
            if (ringRect != null)
                ringRect.anchoredPosition = anchor;

            if (feedbackRect != null)
                feedbackRect.anchoredPosition = feedbackScreenOffset;
            return;
        }

        if (!isActive || currentMode == TimingMode.Capa)
            return;

        if (ringRect != null)
            ringRect.anchoredPosition = Vector2.zero;

        if (feedbackRect != null)
            feedbackRect.anchoredPosition = feedbackScreenOffset;
    }

    private Vector2 GetCapaAnchorPosition()
    {
        Camera referenceCamera = Camera.main;
        Vector3 screenPoint = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);

        if (TryGetCapaWorldAnchor(referenceCamera, out Vector3 worldAnchor))
        {
            if (referenceCamera != null)
            {
                Vector3 projected = referenceCamera.WorldToScreenPoint(worldAnchor);
                if (projected.z > 0.01f)
                    screenPoint = projected;
            }
        }

        return new Vector2(
            screenPoint.x - (Screen.width * 0.5f),
            screenPoint.y - (Screen.height * 0.5f)) + capaScreenOffset;
    }

    private bool TryGetCapaWorldAnchor(Camera referenceCamera, out Vector3 worldAnchor)
    {
        if (HasMissingReferences())
            ResolveReferencesIfNeeded();

        if (bullAI != null)
        {
            Collider bullCollider = bullAI.GetComponent<Collider>();
            if (bullCollider != null)
            {
                Bounds bounds = bullCollider.bounds;
                worldAnchor = new Vector3(bounds.center.x, bounds.max.y + 0.18f, bounds.center.z);
                return true;
            }

            Renderer bullRenderer = bullAI.GetComponentInChildren<Renderer>();
            if (bullRenderer != null)
            {
                Bounds bounds = bullRenderer.bounds;
                worldAnchor = new Vector3(bounds.center.x, bounds.max.y + 0.12f, bounds.center.z);
                return true;
            }

            worldAnchor = bullAI.transform.position + Vector3.up * 1.65f;
            return true;
        }

        Transform target = playerController != null ? playerController.transform : bullAI != null ? bullAI.player : null;
        if (target != null)
        {
            worldAnchor = target.position + Vector3.up * 2f;
            return true;
        }

        worldAnchor = Vector3.zero;
        return false;
    }

    private string EvaluateResult()
    {
        if (currentProgress >= activePerfectProgress)
            return "Perfect!";

        if (currentProgress >= activeGoodProgress)
            return "Good";

        return "Miss";
    }
}
