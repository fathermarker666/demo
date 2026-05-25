using System;
using InfimaGames.LowPolyShooterPack;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[DefaultExecutionOrder(250)]
public class BullfightPauseSettingsUI : MonoBehaviour
{
    private enum ArcadeAccentSizeTier
    {
        Small,
        Medium,
        Large,
        Hero
    }

    private const float StickDeadzone = 0.2f;
    private const float VolumeAdjustSpeed = 0.65f;
    private const int CanvasSortingOrder = 6000;
    private const float ResetConfirmationDuration = 4f;

    [Header("Overlay")]
    [SerializeField] private Font arcadeAccentFont;
    [SerializeField] private Sprite controlsOverlaySprite;

    [Header("Press Feedback")]
    [SerializeField] private AudioClip actionClickClip;
    [SerializeField, Range(0f, 1f)] private float actionClickVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float actionRumbleLowFrequency = 0.35f;
    [SerializeField, Range(0f, 1f)] private float actionRumbleHighFrequency = 0.65f;
    [SerializeField] private float actionRumbleDuration = 0.12f;

    private CameraLook cameraLook;
    private bool cameraLookWasEnabled;
    private static Font cachedFont;
    private static Font cachedCjkFont;

    private Canvas canvas;
    private GameObject panelRoot;
    private GameObject controlsOverlayRoot;
    private Image controlsOverlayImage;
    private Text controlsOverlayPlaceholderLabel;
    private RectTransform bgmFillRect;
    private RectTransform sfxFillRect;
    private Text bgmValueLabel;
    private Text sfxValueLabel;
    private Text helpLabel;
    private Text resetActionLabel;
    private Text toggleHintLabel;

    private BullfightAudioController audioController;
    private BullfightPlayerController playerController;
    private BullfightGameFlow gameFlow;
    private BullfightStartMenu startMenu;
    private ManualStartMenuController manualStartMenu;
    private float previousTimeScale = 1f;
    private float previousFixedDeltaTime = 0.02f;
    private bool menuOpen;
    private bool controlsOverlayOpen;
    private CursorLockMode previousCursorLockMode = CursorLockMode.Locked;
    private bool previousCursorVisible;
    private bool playerControllerWasEnabled;
    private bool frontendBlocked;
    private bool resetConfirmationArmed;
    private bool resetInProgress;
    private float resetConfirmationExpireAt = -1f;
    private AudioSource actionClickAudioSource;
    private Coroutine actionRumbleRoutine;

    public static BullfightPauseSettingsUI Instance { get; private set; }
    public bool IsPauseMenuOpen => menuOpen;

    private void Awake()
    {
        Instance = this;
        previousFixedDeltaTime = Time.fixedDeltaTime;
        SceneManager.sceneLoaded += OnSceneLoaded;
        BuildUi();
        SetMenuVisible(false);
        SetControlsOverlayVisible(false);
        RefreshLabels();
    }

    private void OnDestroy()
    {
        if (ReferenceEquals(Instance, this))
            Instance = null;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (menuOpen)
            ResumeGameplay();
    }

    private void Update()
    {
        ResolveReferencesIfNeeded();
        if (frontendBlocked)
            return;

        if (resetConfirmationArmed && Time.unscaledTime >= resetConfirmationExpireAt)
            CancelResetConfirmation();

        UpdateClosedHintVisibility();

        if (IsAnyFrontendVisible())
            return;

        if (WasTogglePressedThisFrame())
        {
            if (controlsOverlayOpen)
            {
                CancelResetConfirmation();
                TriggerActionFeedback();
                ToggleControlsOverlay(false);
                return;
            }

            if (menuOpen)
            {
                CancelResetConfirmation();
                TriggerActionFeedback();
                CloseMenu();
            }
            else
                OpenMenu();

            return;
        }

        if (!menuOpen)
            return;

        if (cameraLook != null)
            cameraLook.enabled = false;

        if (controlsOverlayOpen)
        {
            if (WasControlsPressedThisFrame())
            {
                CancelResetConfirmation();
                TriggerActionFeedback();
                ToggleControlsOverlay(false);
            }

            return;
        }

        if (WasResetPressedThisFrame())
        {
            TriggerActionFeedback();
            HandleResetPressed();
            return;
        }

        if (WasRestartPressedThisFrame())
        {
            CancelResetConfirmation();
            TriggerActionFeedback();
            ReturnToStartSelectionMenu();
            return;
        }

        if (WasControlsPressedThisFrame())
        {
            CancelResetConfirmation();
            TriggerActionFeedback();
            ToggleControlsOverlay(true);
            return;
        }

        if (WasHomePressedThisFrame())
        {
            CancelResetConfirmation();
            TriggerActionFeedback();
            EnterHomeMenu();
            return;
        }

        if (WasResumePressedThisFrame())
        {
            CancelResetConfirmation();
            TriggerActionFeedback();
            CloseMenu();
            return;
        }

        UpdateVolumeInput();
        RefreshLabels();
    }

    public void ResumeGameplay()
    {
        controlsOverlayOpen = false;
        SetControlsOverlayVisible(false);

        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        Time.fixedDeltaTime = previousFixedDeltaTime > 0f ? previousFixedDeltaTime : 0.02f;
        Cursor.lockState = previousCursorLockMode;
        Cursor.visible = previousCursorVisible;

        if (cameraLook != null)
            cameraLook.enabled = cameraLookWasEnabled;

        if (playerController != null)
        {
            playerController.ClearInputBuffers();
            playerController.enabled = playerControllerWasEnabled;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        resetInProgress = false;
        CancelResetConfirmation();
        ResolveReferencesIfNeeded();
        RefreshLabels();
    }

    private void BuildUi()
    {
        GameObject canvasObject = new GameObject("BullfightSettingsCanvas");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortingOrder;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        toggleHintLabel = CreateText(
            canvasObject.transform,
            "ToggleHint",
            "LB 開啟設定",
            22,
            FontStyle.Normal,
            TextAnchor.MiddleRight,
            Vector2.zero,
            new Vector2(300f, 44f));
        AnchorToMiddleRight(toggleHintLabel.rectTransform, new Vector2(-40f, -212f));

        panelRoot = CreatePanel(canvasObject.transform, "SettingsPanel", new Color(0.08f, 0.02f, 0.02f, 0.92f), new Vector2(1500f, 700f));
        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;

        CreateText(panelRoot.transform, "Title", "SETTING", 44, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0f, 286f), new Vector2(420f, 64f));

        CreateVolumeColumn(panelRoot.transform, "BGM", "BGM", "左蘑菇頭", new Vector2(-470f, -18f), out bgmFillRect, out bgmValueLabel);
        CreateActionColumn(panelRoot.transform, new Vector2(0f, -20f));
        CreateVolumeColumn(panelRoot.transform, "SFX", "SFX", "右蘑菇頭", new Vector2(470f, -18f), out sfxFillRect, out sfxValueLabel);

        helpLabel = CreateText(
            panelRoot.transform,
            "Help",
            string.Empty,
            18,
            FontStyle.Normal,
            TextAnchor.MiddleCenter,
            new Vector2(0f, -294f),
            new Vector2(820f, 84f));

        controlsOverlayRoot = CreatePanel(canvasObject.transform, "ControlsOverlay", new Color(0f, 0f, 0f, 0.82f), Vector2.zero);
        StretchToFullScreen(controlsOverlayRoot.GetComponent<RectTransform>());

        GameObject overlayFrame = CreatePanel(controlsOverlayRoot.transform, "OverlayFrame", new Color(0.11f, 0.03f, 0.03f, 0.97f), new Vector2(1260f, 820f));
        RectTransform overlayFrameRect = overlayFrame.GetComponent<RectTransform>();
        overlayFrameRect.anchorMin = new Vector2(0.5f, 0.5f);
        overlayFrameRect.anchorMax = new Vector2(0.5f, 0.5f);
        overlayFrameRect.pivot = new Vector2(0.5f, 0.5f);
        overlayFrameRect.anchoredPosition = Vector2.zero;

        CreateText(overlayFrame.transform, "OverlayTitle", "操作說明", 38, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0f, 342f), new Vector2(520f, 56f));
        CreateText(overlayFrame.transform, "OverlayHelp", "B 返回設定", 22, FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(0f, -356f), new Vector2(420f, 40f));

        GameObject overlayImageBack = CreatePanel(overlayFrame.transform, "OverlayImageBack", new Color(0.04f, 0.04f, 0.04f, 0.92f), new Vector2(1080f, 620f));
        RectTransform overlayImageBackRect = overlayImageBack.GetComponent<RectTransform>();
        overlayImageBackRect.anchorMin = new Vector2(0.5f, 0.5f);
        overlayImageBackRect.anchorMax = new Vector2(0.5f, 0.5f);
        overlayImageBackRect.pivot = new Vector2(0.5f, 0.5f);
        overlayImageBackRect.anchoredPosition = new Vector2(0f, -4f);

        GameObject overlayImageObject = new GameObject("OverlayImage", typeof(RectTransform), typeof(Image));
        overlayImageObject.transform.SetParent(overlayImageBack.transform, false);
        controlsOverlayImage = overlayImageObject.GetComponent<Image>();
        controlsOverlayImage.preserveAspect = true;
        RectTransform overlayImageRect = controlsOverlayImage.rectTransform;
        overlayImageRect.anchorMin = new Vector2(0.5f, 0.5f);
        overlayImageRect.anchorMax = new Vector2(0.5f, 0.5f);
        overlayImageRect.pivot = new Vector2(0.5f, 0.5f);
        overlayImageRect.sizeDelta = new Vector2(1000f, 560f);
        overlayImageRect.anchoredPosition = Vector2.zero;

        controlsOverlayPlaceholderLabel = CreateText(
            overlayImageBack.transform,
            "OverlayPlaceholder",
            "請在 BullfightPauseSettingsUI 指定 controlsOverlaySprite\n把操作說明圖片拖進 Inspector 後就會顯示在這裡。",
            28,
            FontStyle.Normal,
            TextAnchor.MiddleCenter,
            Vector2.zero,
            new Vector2(760f, 160f));

        controlsOverlayRoot.transform.SetAsLastSibling();
        helpLabel.transform.SetAsLastSibling();
    }

    private void CreateActionColumn(Transform parent, Vector2 anchoredPosition)
    {
        GameObject column = CreatePanel(parent, "ActionColumn", new Color(0.16f, 0.05f, 0.05f, 0.95f), new Vector2(420f, 540f));
        RectTransform columnRect = column.GetComponent<RectTransform>();
        columnRect.anchorMin = new Vector2(0.5f, 0.5f);
        columnRect.anchorMax = new Vector2(0.5f, 0.5f);
        columnRect.pivot = new Vector2(0.5f, 0.5f);
        columnRect.anchoredPosition = anchoredPosition;

        CreateText(column.transform, "ActionTitle", "MENU", 32, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0f, 210f), new Vector2(280f, 44f));
        resetActionLabel = CreateActionButtonVisual(column.transform, "ResetAction", "重置遊戲 (RB)", new Vector2(0f, 134f));
        CreateActionButtonVisual(column.transform, "RestartAction", "開始選單 (Y)", new Vector2(0f, 62f));
        CreateActionButtonVisual(column.transform, "ControlsAction", "操作說明 (B)", new Vector2(0f, -10f));
        CreateActionButtonVisual(column.transform, "HomeAction", "回首頁 (X)", new Vector2(0f, -82f));
        CreateActionButtonVisual(column.transform, "ResumeAction", "返回遊戲 (A)", new Vector2(0f, -154f));
    }

    private Text CreateActionButtonVisual(Transform parent, string name, string label, Vector2 anchoredPosition)
    {
        GameObject buttonVisual = CreatePanel(parent, name, new Color(0.56f, 0.09f, 0.09f, 1f), new Vector2(340f, 66f));
        RectTransform rect = buttonVisual.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;

        return CreateText(buttonVisual.transform, name + "Label", label, 28, FontStyle.Bold, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(300f, 48f));
    }

    private void CreateVolumeColumn(Transform parent, string prefix, string title, string stickLabelText, Vector2 anchoredPosition, out RectTransform fillRect, out Text valueLabel)
    {
        GameObject column = CreatePanel(parent, prefix + "Column", new Color(0.18f, 0.05f, 0.05f, 0.95f), new Vector2(320f, 500f));
        RectTransform columnRect = column.GetComponent<RectTransform>();
        columnRect.anchorMin = new Vector2(0.5f, 0.5f);
        columnRect.anchorMax = new Vector2(0.5f, 0.5f);
        columnRect.pivot = new Vector2(0.5f, 0.5f);
        columnRect.anchoredPosition = anchoredPosition;

        CreateText(column.transform, prefix + "Title", title, 32, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0f, 188f), new Vector2(240f, 44f));

        GameObject barBack = CreatePanel(column.transform, prefix + "BarBack", new Color(0.12f, 0.12f, 0.12f, 1f), new Vector2(104f, 240f));
        RectTransform barRect = barBack.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0.5f, 0.5f);
        barRect.anchorMax = new Vector2(0.5f, 0.5f);
        barRect.pivot = new Vector2(0.5f, 0.5f);
        barRect.anchoredPosition = new Vector2(0f, 36f);

        GameObject fill = new GameObject(prefix + "Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(barBack.transform, false);
        Image fillImage = fill.GetComponent<Image>();
        fillImage.color = new Color(0.84f, 0.18f, 0.12f, 1f);

        fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = new Vector2(8f, 8f);
        fillRect.offsetMax = new Vector2(-8f, -8f);
        fillRect.pivot = new Vector2(0.5f, 0f);

        CreateText(column.transform, prefix + "Hint", stickLabelText, 18, FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(0f, -132f), new Vector2(220f, 54f));
        valueLabel = CreateText(column.transform, prefix + "Value", "100%", 26, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0f, -200f), new Vector2(160f, 40f));
    }

    private static GameObject CreatePanel(Transform parent, string name, Color color, Vector2 size)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        Image image = panel.GetComponent<Image>();
        image.color = color;
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        return panel;
    }

    private static Text CreateText(Transform parent, string name, string value, int fontSize, FontStyle style, TextAnchor alignment, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.text = value;
        text.font = GetUiFont();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = new Color(0.97f, 0.93f, 0.84f, 1f);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        ApplyLocalizedFont(text, value, fontSize, wrap: true, VerticalWrapMode.Truncate, Mathf.Max(12, fontSize - 8));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return text;
    }

    private static Font GetUiFont()
    {
        if (cachedFont != null)
            return cachedFont;

        cachedFont = Font.CreateDynamicFontFromOSFont(new[]
        {
            "Arial",
            "Microsoft JhengHei UI",
            "Microsoft JhengHei",
            "Segoe UI"
        }, 18);

        return cachedFont;
    }

    private static Font GetCjkUiFont()
    {
        if (cachedCjkFont != null)
            return cachedCjkFont;

        cachedCjkFont = Resources.Load<Font>("Cubic_11");
        if (cachedCjkFont == null)
            cachedCjkFont = GetUiFont();

        return cachedCjkFont;
    }

    private static void ApplyLocalizedFont(Text text, string value, int fontSize, bool wrap, VerticalWrapMode verticalOverflow, int minBestFitSize, float cjkLineSpacing = 1f)
    {
        if (text == null)
            return;

        bool useCjkFont = ContainsCjk(value);
        text.font = useCjkFont ? GetCjkUiFont() : GetUiFont();
        text.fontSize = fontSize;
        text.alignByGeometry = useCjkFont;
        text.supportRichText = false;
        text.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
        text.verticalOverflow = verticalOverflow;
        text.lineSpacing = useCjkFont ? cjkLineSpacing : 1f;

        if (useCjkFont)
        {
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Min(minBestFitSize, fontSize);
            text.resizeTextMaxSize = fontSize;
            return;
        }

        text.resizeTextForBestFit = false;
        text.resizeTextMinSize = fontSize;
        text.resizeTextMaxSize = fontSize;
    }

    private static bool ContainsCjk(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        for (int index = 0; index < value.Length; index++)
        {
            if (IsCjkCharacter(value[index]))
                return true;
        }

        return false;
    }

    private static bool IsCjkCharacter(char character)
    {
        int codePoint = character;
        return (codePoint >= 0x3400 && codePoint <= 0x9FFF) ||
               (codePoint >= 0x3000 && codePoint <= 0x303F) ||
               (codePoint >= 0x3100 && codePoint <= 0x312F) ||
               (codePoint >= 0x31A0 && codePoint <= 0x31BF) ||
               (codePoint >= 0xFF00 && codePoint <= 0xFFEF);
    }

    private void ApplyArcadeAccentFontIfSafe(Text text, string name, string value)
    {
        if (text == null || string.IsNullOrWhiteSpace(value) || !IsAsciiOnly(value))
            return;

        Font accentFont = ResolveArcadeAccentFont();
        if (accentFont == null)
            return;

        text.font = accentFont;

        if (name == "Title" && value == "SETTING")
        {
            ApplyArcadeAccentStyle(text, ArcadeAccentSizeTier.Large, 96, 144, 8f);
            return;
        }

        if (name == "ActionTitle" || (name.EndsWith("Title", StringComparison.Ordinal) && (value == "MENU" || value == "BGM" || value == "SFX")))
        {
            ApplyArcadeAccentStyle(text, ArcadeAccentSizeTier.Large, 96, 104);
            return;
        }

        if (name.EndsWith("Value", StringComparison.Ordinal))
            ApplyArcadeAccentStyle(text, ArcadeAccentSizeTier.Medium, 64, 88);
    }

    private static void ApplyArcadeAccentStyle(Text text, ArcadeAccentSizeTier sizeTier, int minOverride, int maxOverride, float? legacyScaleOverride = null)
    {
        if (text == null)
            return;

        if (IsLegacyBitmapFont(text.font))
        {
            text.resizeTextForBestFit = false;
            text.fontSize = 16;
            text.alignByGeometry = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            text.rectTransform.localScale = Vector3.one * (legacyScaleOverride ?? GetArcadeAccentLegacyScale(sizeTier));
            return;
        }

        (int minSize, int maxSize) = sizeTier switch
        {
            ArcadeAccentSizeTier.Small => (40, 84),
            ArcadeAccentSizeTier.Medium => (64, 120),
            ArcadeAccentSizeTier.Large => (96, 168),
            ArcadeAccentSizeTier.Hero => (160, 260),
            _ => (64, 120)
        };

        minSize = minOverride > 0 ? minOverride : minSize;
        maxSize = maxOverride > 0 ? maxOverride : maxSize;

        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = minSize;
        text.resizeTextMaxSize = Mathf.Max(minSize, maxSize);
        text.fontSize = text.resizeTextMaxSize;
        text.alignByGeometry = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = false;
        text.rectTransform.localScale = Vector3.one;
    }

    private static bool IsLegacyBitmapFont(Font font)
    {
        return font != null && font.fontSize == 0;
    }

    private static float GetArcadeAccentLegacyScale(ArcadeAccentSizeTier sizeTier)
    {
        return sizeTier switch
        {
            ArcadeAccentSizeTier.Small => 3f,
            ArcadeAccentSizeTier.Medium => 4.5f,
            ArcadeAccentSizeTier.Large => 6.5f,
            ArcadeAccentSizeTier.Hero => 11f,
            _ => 4.5f
        };
    }

    private Font ResolveArcadeAccentFont()
    {
        if (arcadeAccentFont != null)
            return arcadeAccentFont;

#if UNITY_EDITOR
        arcadeAccentFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Dadako/BitmapFonts/Pixel/Help-outline.fontsettings");
#endif
        if (arcadeAccentFont == null)
        {
            Font[] loadedFonts = Resources.FindObjectsOfTypeAll<Font>();
            for (int index = 0; index < loadedFonts.Length; index++)
            {
                Font candidate = loadedFonts[index];
                if (candidate != null && candidate.name == "Help-outline")
                {
                    arcadeAccentFont = candidate;
                    break;
                }
            }
        }

        return arcadeAccentFont;
    }

    private static bool IsAsciiOnly(string value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] > 127)
                return false;
        }

        return true;
    }

    private void ResolveReferencesIfNeeded()
    {
        if (cameraLook == null)
        {
            PlayerStats stats = FindObjectOfType<PlayerStats>(true);
            if (stats != null)
                cameraLook = stats.GetComponentInChildren<CameraLook>(true);
        }

        if (audioController == null)
            audioController = BullfightSceneCache.FindObject<BullfightAudioController>();

        if (playerController == null)
            playerController = BullfightSceneCache.FindObject<BullfightPlayerController>();

        if (gameFlow == null)
            gameFlow = BullfightSceneCache.FindObject<BullfightGameFlow>();

        if (startMenu == null)
            startMenu = FindObjectOfType<BullfightStartMenu>(true);

        if (manualStartMenu == null)
            manualStartMenu = FindObjectOfType<ManualStartMenuController>(true);
    }

    private bool IsAnyFrontendVisible()
    {
        bool startSelectionVisible = startMenu != null && startMenu.IsMenuVisible;
        bool homeMenuVisible = manualStartMenu != null && manualStartMenu.IsFrontendVisible;
        return startSelectionVisible || homeMenuVisible;
    }

    private static bool WasTogglePressedThisFrame()
    {
        bool keyboardToggle = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        bool gamepadToggle = Gamepad.current != null && Gamepad.current.leftShoulder.wasPressedThisFrame;
        return keyboardToggle || gamepadToggle;
    }

    private static bool WasRestartPressedThisFrame()
    {
        return (Keyboard.current != null && Keyboard.current.yKey.wasPressedThisFrame) ||
               (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame);
    }

    private static bool WasResetPressedThisFrame()
    {
        return (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ||
               (Gamepad.current != null && Gamepad.current.rightShoulder.wasPressedThisFrame);
    }

    private static bool WasControlsPressedThisFrame()
    {
        return (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame) ||
               (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
    }

    private static bool WasHomePressedThisFrame()
    {
        return (Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame) ||
               (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame);
    }

    private static bool WasResumePressedThisFrame()
    {
        return (Keyboard.current != null && Keyboard.current.aKey.wasPressedThisFrame) ||
               (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
    }

    private void OpenMenu()
    {
        if (frontendBlocked)
            return;

        resetInProgress = false;
        CancelResetConfirmation();
        ResolveReferencesIfNeeded();
        previousTimeScale = Time.timeScale;
        previousFixedDeltaTime = Time.fixedDeltaTime;
        previousCursorLockMode = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        playerControllerWasEnabled = playerController != null && playerController.enabled;
        cameraLookWasEnabled = cameraLook != null && cameraLook.enabled;

        if (cameraLook != null)
            cameraLook.enabled = false;

        if (playerController != null)
        {
            playerController.ClearInputBuffers();
            playerController.enabled = false;
        }

        Time.timeScale = 0f;
        Time.fixedDeltaTime = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        menuOpen = true;
        controlsOverlayOpen = false;
        SetMenuVisible(true);
        SetControlsOverlayVisible(false);
        RefreshLabels();
    }

    private void CloseMenu()
    {
        CancelResetConfirmation();
        ResumeGameplay();
        menuOpen = false;
        SetMenuVisible(false);
        RefreshLabels();
    }

    private void ReturnToStartSelectionMenu()
    {
        CancelResetConfirmation();
        ReloadToFrontendLanding(ManualStartMenuController.FrontendReloadLandingTarget.StartSelectionMenu);
    }

    private void EnterHomeMenu()
    {
        CancelResetConfirmation();
        ReloadToFrontendLanding(ManualStartMenuController.FrontendReloadLandingTarget.Home);
    }

    private void CloseMenuForFrontendTransition()
    {
        CancelResetConfirmation();
        playerController?.ClearInputBuffers();
        controlsOverlayOpen = false;
        menuOpen = false;
        SetControlsOverlayVisible(false);
        SetMenuVisible(false);
    }

    private void ToggleControlsOverlay(bool visible)
    {
        if (visible)
            CancelResetConfirmation();

        controlsOverlayOpen = visible;
        SetControlsOverlayVisible(visible);
        RefreshLabels();
    }

    private void HandleResetPressed()
    {
        if (resetInProgress)
            return;

        if (!resetConfirmationArmed)
        {
            resetConfirmationArmed = true;
            resetConfirmationExpireAt = Time.unscaledTime + ResetConfirmationDuration;
            RefreshLabels();
            return;
        }

        ExecuteResetToHomeMenu();
    }

    private void ReloadToFrontendLanding(ManualStartMenuController.FrontendReloadLandingTarget landingTarget)
    {
        ResolveReferencesIfNeeded();
        ManualStartMenuController.RequestFrontendReloadLanding(landingTarget);
        SetFrontendBlocked(true);

        Time.timeScale = 1f;
        Time.fixedDeltaTime = previousFixedDeltaTime > 0f ? previousFixedDeltaTime : 0.02f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (playerController != null)
        {
            playerController.ClearInputBuffers();
            playerController.ForceStopMovement();
        }

        if (gameFlow != null)
        {
            gameFlow.ReloadCurrentSceneToHomeMenu();
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
            return;

        if (activeScene.buildIndex >= 0)
            SceneManager.LoadScene(activeScene.buildIndex);
        else
            SceneManager.LoadScene(activeScene.name);
    }

    private void ExecuteResetToHomeMenu()
    {
        resetInProgress = true;
        CancelResetConfirmation();
        frontendBlocked = true;
        controlsOverlayOpen = false;
        menuOpen = false;
        SetControlsOverlayVisible(false);
        SetMenuVisible(false);

        if (canvas != null)
            canvas.gameObject.SetActive(false);

        Time.timeScale = 1f;
        Time.fixedDeltaTime = previousFixedDeltaTime > 0f ? previousFixedDeltaTime : 0.02f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (playerController != null)
        {
            playerController.ClearInputBuffers();
            playerController.ForceStopMovement();
        }

        if (gameFlow != null)
        {
            gameFlow.ReloadCurrentSceneToHomeMenu();
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
            return;

        if (activeScene.buildIndex >= 0)
            SceneManager.LoadScene(activeScene.buildIndex);
        else
            SceneManager.LoadScene(activeScene.name);
    }

    private void CancelResetConfirmation()
    {
        if (!resetConfirmationArmed)
            return;

        resetConfirmationArmed = false;
        resetConfirmationExpireAt = -1f;
        RefreshLabels();
    }

    private void UpdateVolumeInput()
    {
        if (audioController == null)
            return;

        float bgmInput = ReadAxis(
            Gamepad.current != null ? Gamepad.current.leftStick.ReadValue().y : 0f,
            (Keyboard.current != null && Keyboard.current.wKey.isPressed ? 1f : 0f) -
            (Keyboard.current != null && Keyboard.current.sKey.isPressed ? 1f : 0f));

        float sfxInput = ReadAxis(
            Gamepad.current != null ? Gamepad.current.rightStick.ReadValue().y : 0f,
            (Keyboard.current != null && Keyboard.current.upArrowKey.isPressed ? 1f : 0f) -
            (Keyboard.current != null && Keyboard.current.downArrowKey.isPressed ? 1f : 0f));

        if (Mathf.Abs(bgmInput) > 0.001f)
            audioController.SetBgmVolume(audioController.BgmVolume + bgmInput * VolumeAdjustSpeed * Time.unscaledDeltaTime);

        if (Mathf.Abs(sfxInput) > 0.001f)
            audioController.SetSfxVolume(audioController.SfxVolume + sfxInput * VolumeAdjustSpeed * Time.unscaledDeltaTime);
    }

    private static float ReadAxis(float stickValue, float keyboardValue)
    {
        if (Mathf.Abs(stickValue) >= StickDeadzone)
            return stickValue;

        return keyboardValue;
    }

    private void TriggerActionFeedback()
    {
        PlayActionClick();
        TriggerActionRumble();
    }

    private void PlayActionClick()
    {
        ResolveReferencesIfNeeded();
        MenuUiFeedback.PlayOneShot(this, ref actionClickAudioSource, ResolveActionClickClip(), actionClickVolume);
    }

    private AudioClip ResolveActionClickClip()
    {
        if (actionClickClip != null)
            return actionClickClip;

        return manualStartMenu != null ? manualStartMenu.DefaultButtonClickClip : null;
    }

    private void TriggerActionRumble()
    {
        TriggerRumble(
            ref actionRumbleRoutine,
            actionRumbleLowFrequency,
            actionRumbleHighFrequency,
            actionRumbleDuration);
    }

    private void TriggerRumble(ref Coroutine routine, float lowFrequency, float highFrequency, float duration)
    {
        if (Gamepad.current == null)
            return;

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(RumbleRoutine(
            Mathf.Clamp01(lowFrequency),
            Mathf.Clamp01(highFrequency),
            Mathf.Max(0f, duration)));
    }

    private IEnumerator RumbleRoutine(float lowFrequency, float highFrequency, float duration)
    {
        Gamepad gamepad = Gamepad.current;
        if (gamepad == null)
            yield break;

        gamepad.SetMotorSpeeds(lowFrequency, highFrequency);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        gamepad.ResetHaptics();
    }

    private void RefreshLabels()
    {
        if (toggleHintLabel != null)
        {
            toggleHintLabel.text = "LB 開啟設定";
            toggleHintLabel.fontStyle = FontStyle.Normal;
            ApplyLocalizedFont(toggleHintLabel, toggleHintLabel.text, 22, wrap: true, VerticalWrapMode.Truncate, 16);
        }

        if (audioController != null)
        {
            float bgmVolume = audioController.BgmVolume;
            float sfxVolume = audioController.SfxVolume;
            ApplyBarLevel(bgmFillRect, bgmVolume);
            ApplyBarLevel(sfxFillRect, sfxVolume);

            if (bgmValueLabel != null)
                bgmValueLabel.text = Mathf.RoundToInt(bgmVolume * 100f) + "%";
            if (sfxValueLabel != null)
                sfxValueLabel.text = Mathf.RoundToInt(sfxVolume * 100f) + "%";
        }

        if (helpLabel != null)
        {
            helpLabel.text = controlsOverlayOpen
                ? "B 關閉操作說明"
                : resetConfirmationArmed
                    ? "再按一次 RB 會完整重載並回到首頁，其他操作會取消重置確認。\n左蘑菇頭：BGM   右蘑菇頭：SFX"
                    : "RB：重置遊戲   Y：開始選單   B：操作說明   X：回首頁   A：返回遊戲\n左蘑菇頭：BGM   右蘑菇頭：SFX";
            ApplyLocalizedFont(helpLabel, helpLabel.text, 18, wrap: true, VerticalWrapMode.Truncate, 14, 0.94f);
        }

        if (resetActionLabel != null)
        {
            resetActionLabel.text = resetConfirmationArmed ? "確認重置 (RB)" : "重置遊戲 (RB)";
            ApplyLocalizedFont(resetActionLabel, resetActionLabel.text, 28, wrap: true, VerticalWrapMode.Truncate, 20);
        }

        if (controlsOverlayImage != null)
        {
            controlsOverlayImage.sprite = controlsOverlaySprite;
            controlsOverlayImage.enabled = controlsOverlaySprite != null;
        }

        if (controlsOverlayPlaceholderLabel != null)
            controlsOverlayPlaceholderLabel.gameObject.SetActive(controlsOverlaySprite == null);

        UpdateClosedHintVisibility();
    }

    private static void ApplyBarLevel(RectTransform rect, float normalized)
    {
        if (rect == null)
            return;

        float clamped = Mathf.Clamp01(normalized);
        rect.anchorMax = new Vector2(1f, clamped);
        rect.offsetMax = new Vector2(-8f, clamped <= 0.001f ? -140f : -8f);
    }

    public void SetFrontendBlocked(bool blocked)
    {
        frontendBlocked = blocked;

        if (blocked)
        {
            CancelResetConfirmation();
            controlsOverlayOpen = false;
            menuOpen = false;
            SetControlsOverlayVisible(false);
            SetMenuVisible(false);
        }

        if (canvas != null)
            canvas.gameObject.SetActive(!blocked);
    }


    private void SetMenuVisible(bool visible)
    {
        if (panelRoot != null)
            panelRoot.SetActive(visible);

        if (helpLabel != null)
            helpLabel.gameObject.SetActive(visible);
    }

    private void SetControlsOverlayVisible(bool visible)
    {
        if (controlsOverlayRoot != null)
        {
            controlsOverlayRoot.SetActive(visible);
            if (visible)
                controlsOverlayRoot.transform.SetAsLastSibling();
        }
    }

    private void UpdateClosedHintVisibility()
    {
        if (toggleHintLabel == null)
            return;

        toggleHintLabel.gameObject.SetActive(!frontendBlocked && !menuOpen && !IsAnyFrontendVisible());
    }

    private static void StretchToFullScreen(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static void AnchorToTopCenter(RectTransform rectTransform, Vector2 anchoredPosition)
    {
        rectTransform.anchorMin = new Vector2(0.5f, 1f);
        rectTransform.anchorMax = new Vector2(0.5f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 1f);
        rectTransform.anchoredPosition = anchoredPosition;
    }

    private static void AnchorToBottomCenter(RectTransform rectTransform, Vector2 anchoredPosition)
    {
        rectTransform.anchorMin = new Vector2(0.5f, 0f);
        rectTransform.anchorMax = new Vector2(0.5f, 0f);
        rectTransform.pivot = new Vector2(0.5f, 0f);
        rectTransform.anchoredPosition = anchoredPosition;
    }

    private static void AnchorToMiddleRight(RectTransform rectTransform, Vector2 anchoredPosition)
    {
        rectTransform.anchorMin = new Vector2(1f, 0.5f);
        rectTransform.anchorMax = new Vector2(1f, 0.5f);
        rectTransform.pivot = new Vector2(1f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
    }
}


