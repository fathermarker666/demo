using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(-250)]
public partial class BullfightHudController : MonoBehaviour
{
    private enum ArcadeAccentSizeTier
    {
        Small,
        Medium,
        Large,
        Hero
    }

    private static Font cachedUiFont;
    private static Font cachedCjkUiFont;
    [SerializeField] private Font arcadeAccentFont;
    [SerializeField] private string hudCanvasName = "HUD_Canvas";
    [SerializeField] private string legacyHudCanvasName = "P_LPSP_UI_Canvas";
    [SerializeField] private string legacyHudCanvasCloneName = "P_LPSP_UI_Canvas(Clone)";
    [SerializeField] private string healthBarName = "HealthBar";
    [SerializeField] private string staminaBarName = "StaminaBar";
    [SerializeField] private string playerHealthLabelName = "PlayerHealthLabel";
    [SerializeField] private string playerStaminaLabelName = "PlayerStaminaLabel";
    [SerializeField] private string phaseTwoForceBarName = "PhaseTwoForceBar";
    [SerializeField] private string phaseTwoForceLabelName = "PhaseTwoForceLabel";
    [SerializeField] private string phaseTwoForceValueName = "PhaseTwoForceValue";
    [SerializeField] private string phaseTwoForceThresholdMarkerName = "PhaseTwoForceThresholdMarker";
    [SerializeField] private string bullHealthBarName = "BullHealthBar";
    [SerializeField] private string weaponAmmoName = "Weapon & Ammo";
    [SerializeField] private string ammoName = "Ammo";
    [SerializeField] private string crosshairName = "Crosshair";
    [SerializeField] private string tutorialName = "Tutorial";
    [SerializeField] private string promptName = "Prompt";
    [SerializeField] private string textTimescaleName = "Text Timescale";
    [SerializeField] private string textTutorialPromptName = "Text Tutorial Prompt";
    [SerializeField] private string textTutorialTextName = "Text Tutorial Text";
    [SerializeField] private string textTutorialName = "Text Tutorial";
    [SerializeField] private string textAmmunitionCurrentName = "Text Ammunition Current";
    [SerializeField] private string textAmmunitionTotalName = "Text Ammunition Total";
    [SerializeField] private string textAmmunitionDividerName = "Text Ammunition Divider";
    [SerializeField] private string crosshairClassicName = "Crosshair Classic";
    [SerializeField] private string crosshairDotAdjusterName = "Crosshair Dot Adjuster";
    [SerializeField] private string bullBossRootName = "BullBossHudRoot";
    [SerializeField] private string bullBossTitleName = "BullBossTitle";
    [SerializeField] private string bullBossTopLineName = "BullBossTopLine";
    [SerializeField] private string bullBossLeftWingName = "BullBossLeftWing";
    [SerializeField] private string bullBossRightWingName = "BullBossRightWing";
    [SerializeField] private string bullBossLeftCapName = "BullBossLeftCap";
    [SerializeField] private string bullBossRightCapName = "BullBossRightCap";
    [SerializeField] private string bullBossSegmentLeftName = "BullBossSegmentLeft";
    [SerializeField] private string bullBossSegmentRightName = "BullBossSegmentRight";
    [SerializeField] private string phaseRootName = "BullfightPhaseRoot";
    [SerializeField] private string phaseLabelName = "BullfightPhaseLabel";
    [SerializeField] private string phaseAccentName = "BullfightPhaseAccent";
    [SerializeField] private string phaseTwoOverlayRootName = "BullfightPhaseTwoOverlayRoot";
    [SerializeField] private string phaseTwoPanelName = "BullfightPhaseTwoPanel";
    [SerializeField] private string phaseTwoAccentBandName = "BullfightPhaseTwoAccentBand";
    [SerializeField] private string phaseTwoTopBorderName = "BullfightPhaseTwoTopBorder";
    [SerializeField] private string phaseTwoBottomBorderName = "BullfightPhaseTwoBottomBorder";
    [SerializeField] private string phaseTwoLeftBorderName = "BullfightPhaseTwoLeftBorder";
    [SerializeField] private string phaseTwoRightBorderName = "BullfightPhaseTwoRightBorder";
    [SerializeField] private string phaseTwoTitleName = "BullfightPhaseTwoTitle";
    [SerializeField] private string phaseTwoSubtitleName = "BullfightPhaseTwoSubtitle";
    [SerializeField] private string phaseTwoStatusName = "BullfightPhaseTwoStatus";
    [SerializeField] private string phaseTwoRoundName = "BullfightPhaseTwoRound";
    [SerializeField] private string phaseTwoScoreName = "BullfightPhaseTwoScore";
    [SerializeField] private string tutorialOverlayRootName = "BullfightTutorialOverlayRoot";
    [SerializeField] private string tutorialTitleName = "BullfightTutorialTitle";
    [SerializeField] private string tutorialInstructionName = "BullfightTutorialInstruction";
    [SerializeField] private string tutorialStatusName = "BullfightTutorialStatus";
    [SerializeField] private string tutorialBodyName = "BullfightTutorialBody";
    [SerializeField] private string tutorialBackdropName = "BullfightTutorialBackdrop";
    [SerializeField] private string tutorialPanelName = "BullfightTutorialPanel";
    [SerializeField] private string tutorialAccentBandName = "BullfightTutorialAccentBand";
    [SerializeField] private string tutorialTopBorderName = "BullfightTutorialTopBorder";
    [SerializeField] private string tutorialBottomBorderName = "BullfightTutorialBottomBorder";
    [SerializeField] private string tutorialLeftBorderName = "BullfightTutorialLeftBorder";
    [SerializeField] private string tutorialRightBorderName = "BullfightTutorialRightBorder";

    [SerializeField] private Vector2 healthBarOffset = new Vector2(24f, 73f);
    [SerializeField] private Vector2 staminaBarOffset = new Vector2(24f, 22f);
    [SerializeField] private Vector2 playerHealthLabelOffset = new Vector2(0f, 8f);
    [SerializeField] private Vector2 playerStaminaLabelOffset = new Vector2(0f, 2.5f);
    [SerializeField] private Vector2 phaseTwoForceBarSize = new Vector2(188f, 26f);
    [SerializeField] private Vector2 phaseTwoForceLabelOffset = new Vector2(0f, 2f);
    [SerializeField] private Vector2 phaseTwoForceValueOffset = new Vector2(0f, 0f);
    [SerializeField] private Vector2 phaseTwoForceValueSize = new Vector2(188f, 52f);
    [SerializeField] private Vector2 bullBossRootSize = new Vector2(860f, 84f);
    [SerializeField] private Vector2 bullBossRootOffset = new Vector2(0f, -10f);
    [SerializeField] private Vector2 bullHealthBarSize = new Vector2(720f, 30f);
    [SerializeField] private Vector2 bullHealthBarOffset = new Vector2(0f, -12f);
    [SerializeField] private int bullBossTitleFontSize = 28;
    [SerializeField] private int playerBarLabelFontSize = 16;
    [SerializeField] private int phaseTwoForceLabelFontSize = 19;
    [SerializeField] private int phaseTwoForceValueFontSize = 24;
    [SerializeField] private Vector2 phaseRootSize = new Vector2(220f, 52f);
    [SerializeField] private Vector2 phaseRootOffset = new Vector2(-28f, -24f);
    [SerializeField] private Vector2 phaseAccentSize = new Vector2(140f, 3f);
    [SerializeField] private Vector2 phaseAccentOffset = new Vector2(0f, -36f);
    [SerializeField] private int phaseFontSize = 22;
    [SerializeField] private Vector2 phaseTwoOverlaySize = new Vector2(1080f, 280f);
    [SerializeField] private Vector2 phaseTwoTitlePosition = new Vector2(0f, 44f);
    [SerializeField] private Vector2 phaseTwoSubtitlePosition = new Vector2(0f, -20f);
    [SerializeField] private Vector2 phaseTwoStatusPosition = new Vector2(0f, -64f);
    [SerializeField] private Vector2 phaseTwoRoundPosition = new Vector2(0f, -74f);
    [SerializeField] private Vector2 phaseTwoScorePosition = new Vector2(0f, -102f);
    [SerializeField] private Vector2 tutorialOverlaySize = new Vector2(1196f, 238f);
    [SerializeField] private Vector2 tutorialRulesOverlaySize = new Vector2(2162f, 1060f);
    [SerializeField] private Vector2 tutorialTitlePosition = new Vector2(0f, 44f);
    [SerializeField] private Vector2 tutorialInstructionPosition = new Vector2(0f, -10f);
    [SerializeField] private Vector2 tutorialStatusPosition = new Vector2(0f, -58f);
    [SerializeField] private Vector2 tutorialBodyPosition = new Vector2(0f, -112f);
    [SerializeField] private Vector2 tutorialBodySize = new Vector2(1740f, 790f);
    [SerializeField] private int phaseTwoTitleFontSize = 58;
    [SerializeField] private int phaseTwoSubtitleFontSize = 30;
    [SerializeField] private int phaseTwoStatusFontSize = 24;
    [SerializeField] private int phaseTwoInfoFontSize = 20;
    [SerializeField] private int tutorialTitleFontSize = 50;
    [SerializeField] private int tutorialInstructionFontSize = 28;
    [SerializeField] private int tutorialStatusFontSize = 22;
    [SerializeField] private int tutorialBodyFontSize = 22;
    [SerializeField] private Color bullBossGold = new Color(0.87f, 0.67f, 0.18f, 1f);
    [SerializeField] private Color bullBossTitleColor = new Color(0.97f, 0.93f, 0.82f, 1f);
    [SerializeField] private Color phaseTextColor = new Color(0.97f, 0.93f, 0.82f, 1f);
    [SerializeField] private Color playerHealthLabelColor = new Color(0.92f, 0.16f, 0.16f, 1f);
    [SerializeField] private Color playerStaminaLabelColor = new Color(0.2f, 0.52f, 0.96f, 1f);
    [SerializeField] private Color phaseTwoStatusColor = new Color(0.95f, 0.83f, 0.88f, 1f);
    [SerializeField] private Color bullPhaseTwoFillColor = new Color(0.56f, 0.2f, 0.92f, 1f);
    [SerializeField] private Color tutorialBackdropColor = new Color(0.07f, 0.02f, 0.02f, 0.72f);
    [SerializeField] private Color tutorialPanelColor = new Color(0.14f, 0.04f, 0.04f, 0.92f);
    [SerializeField] private Color tutorialBorderColor = new Color(0.84f, 0.71f, 0.49f, 0.9f);
    [SerializeField] private Color tutorialAccentColor = new Color(0.72f, 0.12f, 0.12f, 1f);
    [SerializeField] private float playerHudScale = 2f;
    [SerializeField] private Vector2 playerBarBaseSize = new Vector2(160f, 20f);
    [SerializeField] private Vector2 playerBarLabelBaseSize = new Vector2(160f, 24f);

    private BullfightGameFlow gameFlow;
    private Canvas hudCanvas;
    private RectTransform hudCanvasRect;
    private Slider playerHealthSlider;
    private Slider playerStaminaSlider;
    private Slider phaseTwoForceSlider;
    private Slider bullHealthSlider;
    private RectTransform bullBossRoot;
    private Image bullBossSegmentLeft;
    private Image bullBossSegmentRight;
    private RectTransform phaseRoot;
    private Text phaseLabel;
    private Image phaseAccent;
    private RectTransform phaseTwoOverlayRoot;
    private Image phaseTwoPanel;
    private Image phaseTwoAccentBand;
    private Image phaseTwoTopBorder;
    private Image phaseTwoBottomBorder;
    private Image phaseTwoLeftBorder;
    private Image phaseTwoRightBorder;
    private Text phaseTwoTitle;
    private Text phaseTwoSubtitle;
    private Text phaseTwoStatus;
    private Text phaseTwoRound;
    private Text phaseTwoScore;
    private RectTransform tutorialOverlayRoot;
    private Image tutorialBackdrop;
    private Image tutorialPanel;
    private Image tutorialAccentBand;
    private Image tutorialTopBorder;
    private Image tutorialBottomBorder;
    private Image tutorialLeftBorder;
    private Image tutorialRightBorder;
    private Text tutorialTitle;
    private Text tutorialInstruction;
    private Text tutorialStatus;
    private Text tutorialBody;
    private Text phaseTwoForceLabel;
    private Text phaseTwoForceValue;
    private Image phaseTwoForceThresholdMarker;
    private Sprite cachedBullFillSprite;
    private Sprite phaseTwoBullFillSprite;
    private Color cachedBullFillColor;
    private Color cachedPlayerStaminaFillColor;
    private bool hasCachedBullFillColor;
    private bool hasResolvedBullPhaseTwoFillSprite;
    private bool hasCachedPlayerStaminaFillColor;
    private bool layoutDirty = true;
    private bool legacyUiDisabled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindObjectOfType<BullfightHudController>(true) != null)
            return;

        GameObject host = new GameObject("BullfightHudController");
        host.AddComponent<BullfightHudController>();
    }

    private void Awake()
    {
        Instance = this;
        Application.targetFrameRate = 60;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        MarkLayoutDirty();
        RebuildHudLayout();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void LateUpdate()
    {
        if (layoutDirty || !HasValidHudLayoutReferences())
            RebuildHudLayout();

        RefreshHudState();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        MarkLayoutDirty();
        RebuildHudLayout();
    }

    private void MarkLayoutDirty()
    {
        hudCanvas = null;
        hudCanvasRect = null;
        playerHealthSlider = null;
        playerStaminaSlider = null;
        phaseTwoForceSlider = null;
        bullHealthSlider = null;
        bullBossRoot = null;
        bullBossSegmentLeft = null;
        bullBossSegmentRight = null;
        phaseRoot = null;
        phaseLabel = null;
        phaseAccent = null;
        phaseTwoOverlayRoot = null;
        phaseTwoPanel = null;
        phaseTwoAccentBand = null;
        phaseTwoTopBorder = null;
        phaseTwoBottomBorder = null;
        phaseTwoLeftBorder = null;
        phaseTwoRightBorder = null;
        phaseTwoTitle = null;
        phaseTwoSubtitle = null;
        phaseTwoStatus = null;
        phaseTwoRound = null;
        phaseTwoScore = null;
        tutorialOverlayRoot = null;
        tutorialBackdrop = null;
        tutorialPanel = null;
        tutorialAccentBand = null;
        tutorialTopBorder = null;
        tutorialBottomBorder = null;
        tutorialLeftBorder = null;
        tutorialRightBorder = null;
        tutorialTitle = null;
        tutorialInstruction = null;
        tutorialStatus = null;
        tutorialBody = null;
        phaseTwoForceLabel = null;
        phaseTwoForceValue = null;
        phaseTwoForceThresholdMarker = null;
        cachedBullFillSprite = null;
        phaseTwoBullFillSprite = null;
        hasCachedBullFillColor = false;
        hasResolvedBullPhaseTwoFillSprite = false;
        hasCachedPlayerStaminaFillColor = false;
        legacyUiDisabled = false;
        ResetArcadeHudRuntime();
        layoutDirty = true;
    }

    private bool HasValidHudLayoutReferences()
    {
        return hudCanvas != null &&
               hudCanvasRect != null &&
               playerHealthSlider != null &&
               playerStaminaSlider != null &&
               bullHealthSlider != null;
    }

    private void EnsureGameFlowReference()
    {
        if (gameFlow == null)
            gameFlow = BullfightSceneCache.FindObject<BullfightGameFlow>();
    }

    private void RebuildHudLayout()
    {
        EnsureGameFlowReference();

        hudCanvas = BullfightSceneCache.FindSceneObjectByName<Canvas>(hudCanvasName);
        hudCanvasRect = hudCanvas != null ? hudCanvas.GetComponent<RectTransform>() : null;
        if (hudCanvas == null || hudCanvasRect == null)
            return;

        playerHealthSlider = BullfightSceneCache.FindSceneObjectByName<Slider>(healthBarName);
        playerStaminaSlider = BullfightSceneCache.FindSceneObjectByName<Slider>(staminaBarName);
        bullHealthSlider = BullfightSceneCache.FindSceneObjectByName<Slider>(bullHealthBarName);
        if (playerHealthSlider == null || playerStaminaSlider == null || bullHealthSlider == null)
            return;

        ConfigureCanvas(hudCanvas, hudCanvasRect);

        PlaceSlider(playerHealthSlider, healthBarOffset * playerHudScale, playerBarBaseSize * playerHudScale);
        PlaceSlider(playerStaminaSlider, staminaBarOffset * playerHudScale, playerBarBaseSize * playerHudScale);
        RemoveStaminaSegments(playerStaminaSlider);
        HidePlayerBarLabel(playerHealthSlider, playerHealthLabelName);
        PlacePlayerBarLabel(playerStaminaSlider, playerStaminaLabelName, "\u73a9\u5bb6\u9ad4\u529b", playerStaminaLabelColor, playerStaminaLabelOffset * playerHudScale);
        CachePlayerStaminaFillColor();
        EnsurePhaseTwoForceUi();
        CacheBullPhaseTwoFillSprite();
        PlaceBullHealthBar(bullHealthSlider);
        EnsurePhaseDisplayUi();
        EnsurePhaseTwoOverlayUi();
        EnsureTutorialOverlayUi();
        EnsureArcadeUi();
        DisableLegacyShooterUiOnce();
        layoutDirty = false;
        RefreshHudState();
    }

    private static void ConfigureCanvas(Canvas canvas, RectTransform canvasRect)
    {
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
        canvas.targetDisplay = 0;

        canvasRect.localScale = Vector3.one;
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            return;

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void RefreshHudState()
    {
        EnsureGameFlowReference();
        UpdatePhaseDisplay();
        UpdatePhaseTwoHud();
        UpdateTutorialHud();
        UpdateArcadeHud();
    }

    private static void PlaceSlider(Slider slider, Vector2 offset, Vector2 size)
    {
        if (slider == null)
            return;

        RectTransform rect = slider.GetComponent<RectTransform>();
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = offset;
        rect.sizeDelta = size;
    }

    private void PlacePlayerBarLabel(Slider slider, string labelObjectName, string labelText, Color labelColor, Vector2 labelOffset)
    {
        if (slider == null)
            return;

        Text label = GetOrCreateUiText(slider.transform as RectTransform, labelObjectName);
        if (label == null)
            return;

        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.pivot = new Vector2(0f, 0f);
        labelRect.anchoredPosition = labelOffset;
        labelRect.sizeDelta = playerBarLabelBaseSize * playerHudScale;
        labelRect.localScale = Vector3.one;
        labelRect.localRotation = Quaternion.identity;

        label.alignment = TextAnchor.LowerLeft;
        label.fontSize = Mathf.RoundToInt(playerBarLabelFontSize * playerHudScale);
        label.fontStyle = FontStyle.Bold;
        label.color = labelColor;
        label.text = labelText;
        ApplyLocalizedUiFont(label, labelText, label.fontSize, wrap: true, VerticalWrapMode.Truncate, minBestFitSize: Mathf.Max(12, label.fontSize - 6));
    }

    private static void HidePlayerBarLabel(Slider slider, string labelObjectName)
    {
        if (slider == null)
            return;

        Transform existingLabel = slider.transform.Find(labelObjectName);
        if (existingLabel != null)
            existingLabel.gameObject.SetActive(false);
    }

    private void CachePlayerStaminaFillColor()
    {
        if (playerStaminaSlider == null || hasCachedPlayerStaminaFillColor)
            return;

        Image fillImage = playerStaminaSlider.fillRect != null ? playerStaminaSlider.fillRect.GetComponent<Image>() : null;
        if (fillImage == null)
            return;

        cachedPlayerStaminaFillColor = fillImage.color;
        hasCachedPlayerStaminaFillColor = true;
    }

    private void CacheBullPhaseTwoFillSprite()
    {
        if (bullHealthSlider == null || hasResolvedBullPhaseTwoFillSprite)
            return;

        phaseTwoBullFillSprite = ResolveBullPhaseTwoFillSprite(bullHealthSlider);
        hasResolvedBullPhaseTwoFillSprite = phaseTwoBullFillSprite != null;
    }

    private void EnsurePhaseTwoForceUi()
    {
        if (hudCanvasRect == null || playerStaminaSlider == null)
            return;

        if (phaseTwoForceSlider == null)
            phaseTwoForceSlider = GetOrCloneSlider(hudCanvasRect, playerStaminaSlider, phaseTwoForceBarName);

        if (phaseTwoForceSlider == null)
            return;

        PlaceSlider(phaseTwoForceSlider, staminaBarOffset * playerHudScale, phaseTwoForceBarSize * playerHudScale);
        phaseTwoForceSlider.interactable = false;
        phaseTwoForceSlider.direction = Slider.Direction.LeftToRight;
        phaseTwoForceSlider.minValue = 0f;
        phaseTwoForceSlider.maxValue = 1f;
        phaseTwoForceSlider.value = 0f;

        Transform clonedStaminaLabel = phaseTwoForceSlider.transform.Find(playerStaminaLabelName);
        if (clonedStaminaLabel != null && clonedStaminaLabel.name != phaseTwoForceLabelName)
            clonedStaminaLabel.gameObject.SetActive(false);

        Image fillImage = phaseTwoForceSlider.fillRect != null ? phaseTwoForceSlider.fillRect.GetComponent<Image>() : null;
        if (fillImage != null)
            fillImage.color = hasCachedPlayerStaminaFillColor ? cachedPlayerStaminaFillColor : playerStaminaLabelColor;

        phaseTwoForceThresholdMarker = GetOrCreateUiImage(phaseTwoForceSlider.transform as RectTransform, phaseTwoForceThresholdMarkerName);
        if (phaseTwoForceThresholdMarker != null)
        {
            RectTransform markerRect = phaseTwoForceThresholdMarker.rectTransform;
            float thresholdRatio = Mathf.Clamp01(35f / 50f);
            Vector2 scaledBarSize = phaseTwoForceBarSize * playerHudScale;
            markerRect.anchorMin = Vector2.zero;
            markerRect.anchorMax = Vector2.zero;
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.anchoredPosition = new Vector2(scaledBarSize.x * thresholdRatio, scaledBarSize.y * 0.5f);
            markerRect.sizeDelta = new Vector2(8f, scaledBarSize.y + 16f);
            markerRect.localScale = Vector3.one;
            markerRect.localRotation = Quaternion.identity;
            phaseTwoForceThresholdMarker.color = new Color(1f, 0.82f, 0.16f, 1f);
            phaseTwoForceThresholdMarker.raycastTarget = false;
        }

        phaseTwoForceLabel = GetOrCreateUiText(phaseTwoForceSlider.transform as RectTransform, phaseTwoForceLabelName);
        if (phaseTwoForceLabel != null)
        {
            RectTransform labelRect = phaseTwoForceLabel.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 0f);
            labelRect.anchoredPosition = phaseTwoForceLabelOffset * playerHudScale;
            labelRect.sizeDelta = playerBarLabelBaseSize * playerHudScale;
            labelRect.localScale = Vector3.one;
            labelRect.localRotation = Quaternion.identity;
            phaseTwoForceLabel.alignment = TextAnchor.LowerLeft;
            phaseTwoForceLabel.fontSize = Mathf.RoundToInt(phaseTwoForceLabelFontSize * playerHudScale);
            phaseTwoForceLabel.fontStyle = FontStyle.Bold;
            phaseTwoForceLabel.color = playerStaminaLabelColor;
            phaseTwoForceLabel.text = "\u529b\u9053";
            ApplyLocalizedUiFont(phaseTwoForceLabel, phaseTwoForceLabel.text, phaseTwoForceLabel.fontSize, wrap: true, VerticalWrapMode.Truncate, minBestFitSize: Mathf.Max(12, phaseTwoForceLabel.fontSize - 4));
        }

        phaseTwoForceValue = GetOrCreateUiText(phaseTwoForceSlider.transform as RectTransform, phaseTwoForceValueName);
        if (phaseTwoForceValue != null)
        {
            RectTransform valueRect = phaseTwoForceValue.rectTransform;
            valueRect.anchorMin = new Vector2(0.5f, 0.5f);
            valueRect.anchorMax = new Vector2(0.5f, 0.5f);
            valueRect.pivot = new Vector2(0.5f, 0.5f);
            valueRect.anchoredPosition = phaseTwoForceValueOffset * playerHudScale;
            valueRect.sizeDelta = phaseTwoForceValueSize * playerHudScale;
            valueRect.localScale = Vector3.one;
            valueRect.localRotation = Quaternion.identity;
            phaseTwoForceValue.alignment = TextAnchor.MiddleCenter;
            phaseTwoForceValue.fontSize = Mathf.RoundToInt(phaseTwoForceValueFontSize * playerHudScale);
            phaseTwoForceValue.fontStyle = FontStyle.Bold;
            phaseTwoForceValue.color = playerStaminaLabelColor;
            phaseTwoForceValue.text = "0";
        }
    }

    private void PlaceBullHealthBar(Slider slider)
    {
        if (slider == null || hudCanvasRect == null)
            return;

        Vector2 resolvedBullHealthBarSize = new Vector2(720f, 60f);
        Vector2 resolvedBullHealthBarOffset = new Vector2(2.8f, 11.5f);

        bullBossRoot = GetOrCreateUiRect(hudCanvasRect, bullBossRootName);
        bullBossRoot.anchorMin = new Vector2(0.5f, 1f);
        bullBossRoot.anchorMax = new Vector2(0.5f, 1f);
        bullBossRoot.pivot = new Vector2(0.5f, 1f);
        bullBossRoot.sizeDelta = bullBossRootSize;
        bullBossRoot.anchoredPosition = bullBossRootOffset;
        bullBossRoot.localScale = Vector3.one;
        bullBossRoot.localRotation = Quaternion.identity;

        RectTransform rect = slider.GetComponent<RectTransform>();
        if (rect == null)
            return;

        rect.SetParent(bullBossRoot, false);
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = resolvedBullHealthBarSize;
        rect.anchoredPosition = resolvedBullHealthBarOffset;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        slider.direction = Slider.Direction.LeftToRight;

        Text title = GetOrCreateUiText(bullBossRoot, bullBossTitleName);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(220f, 28f);
        titleRect.anchoredPosition = new Vector2(0f, -46f);
        titleRect.localScale = Vector3.one;
        titleRect.localRotation = Quaternion.identity;
        title.alignment = TextAnchor.MiddleCenter;
        title.fontSize = bullBossTitleFontSize;
        title.fontStyle = FontStyle.Bold;
        title.color = bullBossTitleColor;
        title.text = "BULL HP";

        ConfigureDecorLine(GetOrCreateUiImage(bullBossRoot, bullBossTopLineName), new Vector2(0f, -2f), new Vector2(742f, 3f));
        ConfigureDecorLine(GetOrCreateUiImage(bullBossRoot, bullBossLeftWingName), new Vector2(-395f, -13f), new Vector2(70f, 6f));
        ConfigureDecorLine(GetOrCreateUiImage(bullBossRoot, bullBossRightWingName), new Vector2(395f, -13f), new Vector2(70f, 6f));
        ConfigureDecorLine(GetOrCreateUiImage(bullBossRoot, bullBossLeftCapName), new Vector2(-364f, -13f), new Vector2(6f, 18f));
        ConfigureDecorLine(GetOrCreateUiImage(bullBossRoot, bullBossRightCapName), new Vector2(364f, -13f), new Vector2(6f, 18f));
        bullBossSegmentLeft = GetOrCreateUiImage(bullBossRoot, bullBossSegmentLeftName);
        bullBossSegmentRight = GetOrCreateUiImage(bullBossRoot, bullBossSegmentRightName);
        ConfigureBullSegmentLine(bullBossSegmentLeft, -1f);
        ConfigureBullSegmentLine(bullBossSegmentRight, 1f);
    }

    private void EnsurePhaseDisplayUi()
    {
        if (hudCanvasRect == null)
            return;

        phaseRoot = GetOrCreateUiRect(hudCanvasRect, phaseRootName);
        phaseRoot.anchorMin = new Vector2(1f, 1f);
        phaseRoot.anchorMax = new Vector2(1f, 1f);
        phaseRoot.pivot = new Vector2(1f, 1f);
        phaseRoot.sizeDelta = phaseRootSize;
        phaseRoot.anchoredPosition = phaseRootOffset;
        phaseRoot.localScale = Vector3.one;
        phaseRoot.localRotation = Quaternion.identity;

        phaseLabel = GetOrCreateUiText(phaseRoot, phaseLabelName);
        RectTransform phaseLabelRect = phaseLabel.rectTransform;
        phaseLabelRect.anchorMin = new Vector2(1f, 1f);
        phaseLabelRect.anchorMax = new Vector2(1f, 1f);
        phaseLabelRect.pivot = new Vector2(1f, 1f);
        phaseLabelRect.sizeDelta = phaseRootSize;
        phaseLabelRect.anchoredPosition = Vector2.zero;
        phaseLabelRect.localScale = Vector3.one;
        phaseLabelRect.localRotation = Quaternion.identity;
        int resolvedPhaseFontSize = Mathf.Max(24, phaseFontSize);
        phaseLabel.alignment = TextAnchor.UpperRight;
        phaseLabel.fontSize = resolvedPhaseFontSize;
        phaseLabel.fontStyle = FontStyle.Normal;
        phaseLabel.color = phaseTextColor;

        phaseAccent = GetOrCreateUiImage(phaseRoot, phaseAccentName);
        RectTransform accentRect = phaseAccent.rectTransform;
        accentRect.anchorMin = new Vector2(1f, 1f);
        accentRect.anchorMax = new Vector2(1f, 1f);
        accentRect.pivot = new Vector2(1f, 1f);
        accentRect.sizeDelta = phaseAccentSize;
        accentRect.anchoredPosition = phaseAccentOffset;
        accentRect.localScale = Vector3.one;
        accentRect.localRotation = Quaternion.identity;
        phaseAccent.color = bullBossGold;
        phaseAccent.raycastTarget = false;
    }

    private void EnsurePhaseTwoOverlayUi()
    {
        if (hudCanvasRect == null)
            return;

        phaseTwoOverlayRoot = GetOrCreateUiRect(hudCanvasRect, phaseTwoOverlayRootName);
        phaseTwoOverlayRoot.anchorMin = new Vector2(0.5f, 0.5f);
        phaseTwoOverlayRoot.anchorMax = new Vector2(0.5f, 0.5f);
        phaseTwoOverlayRoot.pivot = new Vector2(0.5f, 0.5f);
        phaseTwoOverlayRoot.sizeDelta = phaseTwoOverlaySize;
        phaseTwoOverlayRoot.anchoredPosition = new Vector2(0f, 150f);
        phaseTwoOverlayRoot.localScale = Vector3.one;
        phaseTwoOverlayRoot.localRotation = Quaternion.identity;

        phaseTwoPanel = GetOrCreateUiImage(phaseTwoOverlayRoot, phaseTwoPanelName);
        phaseTwoAccentBand = GetOrCreateUiImage(phaseTwoOverlayRoot, phaseTwoAccentBandName);
        phaseTwoTopBorder = GetOrCreateUiImage(phaseTwoOverlayRoot, phaseTwoTopBorderName);
        phaseTwoBottomBorder = GetOrCreateUiImage(phaseTwoOverlayRoot, phaseTwoBottomBorderName);
        phaseTwoLeftBorder = GetOrCreateUiImage(phaseTwoOverlayRoot, phaseTwoLeftBorderName);
        phaseTwoRightBorder = GetOrCreateUiImage(phaseTwoOverlayRoot, phaseTwoRightBorderName);

        phaseTwoPanel.transform.SetAsFirstSibling();
        phaseTwoAccentBand.transform.SetAsLastSibling();
        phaseTwoOverlayRoot.SetAsLastSibling();

        phaseTwoTitle = GetOrCreateUiText(phaseTwoOverlayRoot, phaseTwoTitleName);
        phaseTwoSubtitle = GetOrCreateUiText(phaseTwoOverlayRoot, phaseTwoSubtitleName);
        phaseTwoStatus = GetOrCreateUiText(phaseTwoOverlayRoot, phaseTwoStatusName);
        phaseTwoTitle.transform.SetAsLastSibling();
        phaseTwoSubtitle.transform.SetAsLastSibling();
        phaseTwoStatus.transform.SetAsLastSibling();

        if (bullBossRoot != null)
        {
            phaseTwoRound = GetOrCreateUiText(bullBossRoot, phaseTwoRoundName);
            phaseTwoScore = GetOrCreateUiText(bullBossRoot, phaseTwoScoreName);
        }
    }

    private void EnsureTutorialOverlayUi()
    {
        if (hudCanvasRect == null)
            return;

        tutorialBackdrop = GetOrCreateUiImage(hudCanvasRect, tutorialBackdropName);
        StretchToFullScreen(tutorialBackdrop.rectTransform);
        tutorialBackdrop.color = tutorialBackdropColor;
        tutorialBackdrop.raycastTarget = false;

        tutorialOverlayRoot = GetOrCreateUiRect(hudCanvasRect, tutorialOverlayRootName);
        tutorialOverlayRoot.anchorMin = new Vector2(0.5f, 0.5f);
        tutorialOverlayRoot.anchorMax = new Vector2(0.5f, 0.5f);
        tutorialOverlayRoot.pivot = new Vector2(0.5f, 0.5f);
        tutorialOverlayRoot.sizeDelta = tutorialOverlaySize;
        tutorialOverlayRoot.anchoredPosition = new Vector2(0f, 150f);
        tutorialOverlayRoot.localScale = Vector3.one;
        tutorialOverlayRoot.localRotation = Quaternion.identity;

        tutorialPanel = GetOrCreateUiImage(tutorialOverlayRoot, tutorialPanelName);
        tutorialAccentBand = GetOrCreateUiImage(tutorialOverlayRoot, tutorialAccentBandName);
        tutorialTopBorder = GetOrCreateUiImage(tutorialOverlayRoot, tutorialTopBorderName);
        tutorialBottomBorder = GetOrCreateUiImage(tutorialOverlayRoot, tutorialBottomBorderName);
        tutorialLeftBorder = GetOrCreateUiImage(tutorialOverlayRoot, tutorialLeftBorderName);
        tutorialRightBorder = GetOrCreateUiImage(tutorialOverlayRoot, tutorialRightBorderName);

        tutorialPanel.transform.SetAsFirstSibling();
        tutorialAccentBand.transform.SetAsLastSibling();
        tutorialBackdrop.transform.SetAsLastSibling();
        tutorialOverlayRoot.SetAsLastSibling();

        tutorialTitle = GetOrCreateUiText(tutorialOverlayRoot, tutorialTitleName);
        tutorialInstruction = GetOrCreateUiText(tutorialOverlayRoot, tutorialInstructionName);
        tutorialStatus = GetOrCreateUiText(tutorialOverlayRoot, tutorialStatusName);
        tutorialBody = GetOrCreateUiText(tutorialOverlayRoot, tutorialBodyName);
        tutorialTitle.transform.SetAsLastSibling();
        tutorialInstruction.transform.SetAsLastSibling();
        tutorialStatus.transform.SetAsLastSibling();
        tutorialBody.transform.SetAsLastSibling();
    }

    private void ConfigureDecorLine(Image image, Vector2 anchoredPosition, Vector2 size)
    {
        if (image == null)
            return;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        image.color = bullBossGold;
        image.raycastTarget = false;
    }

    private static void RemoveStaminaSegments(Slider slider)
    {
        if (slider == null)
            return;

        RectTransform sliderRect = slider.GetComponent<RectTransform>();
        if (sliderRect == null)
            return;

        for (int childIndex = sliderRect.childCount - 1; childIndex >= 0; childIndex--)
        {
            Transform child = sliderRect.GetChild(childIndex);
            if (child != null && child.name.StartsWith("StaminaSegmentDivider_"))
                Destroy(child.gameObject);
        }
    }

    private void ConfigureBullSegmentLine(Image image, float direction)
    {
        if (image == null)
            return;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2((720f / 6f) * direction, 11.5f - (60f * 0.5f));
        rect.sizeDelta = new Vector2(4f, 70f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        image.color = bullBossTitleColor;
        image.raycastTarget = false;
        image.gameObject.SetActive(false);
    }

    private void UpdatePhaseDisplay()
    {
        if (phaseLabel == null || gameFlow == null)
            return;

        int resolvedPhaseFontSize = Mathf.Max(24, phaseFontSize);
        phaseLabel.text = GetPhaseLabel(gameFlow.currentPhase, gameFlow.currentEnding);
        phaseLabel.fontStyle = FontStyle.Normal;
        ApplyLocalizedUiFont(phaseLabel, phaseLabel.text, resolvedPhaseFontSize, wrap: false, VerticalWrapMode.Truncate, minBestFitSize: Mathf.Max(12, resolvedPhaseFontSize - 4));
    }

    private void UpdatePhaseTwoHud()
    {
        UpdatePhaseTwoOverlay();
        UpdateBossRoundInfo();
        UpdatePhaseTwoBars();
        UpdatePhaseTwoForceUi();
        UpdateBullHealthStyling();
    }

    private void UpdateTutorialHud()
    {
        if (tutorialOverlayRoot == null)
            return;

        bool shouldShow = gameFlow != null &&
                          gameFlow.ShouldShowTutorialOverlay() &&
                          !gameFlow.IsTutorialCompletionVideoPlaybackActive;
        tutorialOverlayRoot.gameObject.SetActive(shouldShow);
        if (tutorialBackdrop != null)
            tutorialBackdrop.gameObject.SetActive(shouldShow);
        if (!shouldShow)
            return;

        bool showRules = gameFlow.IsTutorialRulesStep;
        Vector2 overlaySize = showRules ? tutorialRulesOverlaySize : tutorialOverlaySize;
        tutorialOverlayRoot.sizeDelta = overlaySize;
        tutorialOverlayRoot.anchoredPosition = showRules ? Vector2.zero : new Vector2(0f, 150f);
        ConfigureTutorialPanel(overlaySize, showRules);

        Vector2 titlePosition = showRules ? new Vector2(0f, 438f) : tutorialTitlePosition;
        Vector2 instructionPosition = showRules ? new Vector2(0f, 0f) : tutorialInstructionPosition;
        Vector2 statusPosition = showRules ? new Vector2(0f, -474f) : tutorialStatusPosition;

        ConfigureTitleText(tutorialTitle, titlePosition, tutorialTitleFontSize, bullBossTitleColor, FontStyle.Bold, gameFlow.CurrentTutorialTitle);
        ConfigureCenteredText(tutorialInstruction, instructionPosition, tutorialInstructionFontSize, bullBossTitleColor, FontStyle.Normal, showRules ? string.Empty : gameFlow.CurrentTutorialInstruction);
        ConfigureCenteredText(tutorialStatus, statusPosition, tutorialStatusFontSize, bullBossGold, FontStyle.Normal, gameFlow.CurrentTutorialStatus);
        ConfigureTutorialBodyText(tutorialBody, tutorialBodyPosition, tutorialBodySize, tutorialBodyFontSize, bullBossTitleColor, showRules ? gameFlow.CurrentTutorialBody : string.Empty, showRules);
    }

    private void ConfigureTutorialPanel(Vector2 panelSize, bool showRules)
    {
        if (tutorialPanel == null ||
            tutorialAccentBand == null ||
            tutorialTopBorder == null ||
            tutorialBottomBorder == null ||
            tutorialLeftBorder == null ||
            tutorialRightBorder == null)
            return;

        if (tutorialBackdrop != null)
            tutorialBackdrop.color = new Color(tutorialBackdropColor.r, tutorialBackdropColor.g, tutorialBackdropColor.b, showRules ? tutorialBackdropColor.a : 0.34f);

        RectTransform panelRect = tutorialPanel.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = panelSize;
        panelRect.localScale = Vector3.one;
        panelRect.localRotation = Quaternion.identity;
        tutorialPanel.color = tutorialPanelColor;
        tutorialPanel.raycastTarget = false;

        RectTransform accentRect = tutorialAccentBand.rectTransform;
        accentRect.anchorMin = new Vector2(0.5f, 0.5f);
        accentRect.anchorMax = new Vector2(0.5f, 0.5f);
        accentRect.pivot = new Vector2(0.5f, 0.5f);
        accentRect.anchoredPosition = new Vector2(0f, panelSize.y * 0.5f - 54f);
        accentRect.sizeDelta = new Vector2(panelSize.x - 80f, showRules ? 58f : 44f);
        accentRect.localScale = Vector3.one;
        accentRect.localRotation = Quaternion.identity;
        tutorialAccentBand.color = tutorialAccentColor;
        tutorialAccentBand.raycastTarget = false;

        ConfigureTutorialPanelBorder(tutorialTopBorder, new Vector2(0f, panelSize.y * 0.5f - 10f), new Vector2(panelSize.x - 26f, 4f));
        ConfigureTutorialPanelBorder(tutorialBottomBorder, new Vector2(0f, -panelSize.y * 0.5f + 10f), new Vector2(panelSize.x - 26f, 4f));
        ConfigureTutorialPanelBorder(tutorialLeftBorder, new Vector2(-panelSize.x * 0.5f + 10f, 0f), new Vector2(4f, panelSize.y - 26f));
        ConfigureTutorialPanelBorder(tutorialRightBorder, new Vector2(panelSize.x * 0.5f - 10f, 0f), new Vector2(4f, panelSize.y - 26f));
    }

    private void ConfigureTutorialPanelBorder(Image border, Vector2 anchoredPosition, Vector2 size)
    {
        if (border == null)
            return;

        RectTransform rect = border.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        border.color = tutorialBorderColor;
        border.raycastTarget = false;
    }

    private void UpdatePhaseTwoOverlay()
    {
        if (phaseTwoOverlayRoot == null)
            return;

        bool shouldShow = gameFlow != null && gameFlow.ShouldShowPhaseTwoOverlay();
        phaseTwoOverlayRoot.gameObject.SetActive(shouldShow);
        if (!shouldShow)
            return;

        ConfigurePhaseTwoPanel();
        GetPhaseTwoOverlayContent(out string titleText, out string subtitleText, out string statusText);
        ConfigureTitleText(phaseTwoTitle, phaseTwoTitlePosition, phaseTwoTitleFontSize, bullBossTitleColor, FontStyle.Bold, titleText);
        ConfigureCenteredText(phaseTwoSubtitle, phaseTwoSubtitlePosition, phaseTwoSubtitleFontSize, bullBossTitleColor, FontStyle.Normal, subtitleText);
        ConfigureCenteredText(phaseTwoStatus, phaseTwoStatusPosition, phaseTwoStatusFontSize, phaseTwoStatusColor, FontStyle.Italic, statusText);
    }

    private void ConfigurePhaseTwoPanel()
    {
        if (phaseTwoPanel == null ||
            phaseTwoAccentBand == null ||
            phaseTwoTopBorder == null ||
            phaseTwoBottomBorder == null ||
            phaseTwoLeftBorder == null ||
            phaseTwoRightBorder == null)
            return;

        Vector2 panelSize = new Vector2(
            Mathf.Max(360f, phaseTwoOverlaySize.x - 120f),
            Mathf.Max(160f, phaseTwoOverlaySize.y - 64f));

        RectTransform panelRect = phaseTwoPanel.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = panelSize;
        panelRect.localScale = Vector3.one;
        panelRect.localRotation = Quaternion.identity;
        phaseTwoPanel.color = new Color(tutorialPanelColor.r, tutorialPanelColor.g, tutorialPanelColor.b, 0.88f);
        phaseTwoPanel.raycastTarget = false;

        RectTransform accentRect = phaseTwoAccentBand.rectTransform;
        accentRect.anchorMin = new Vector2(0.5f, 0.5f);
        accentRect.anchorMax = new Vector2(0.5f, 0.5f);
        accentRect.pivot = new Vector2(0.5f, 0.5f);
        accentRect.anchoredPosition = new Vector2(0f, panelSize.y * 0.5f - 54f);
        accentRect.sizeDelta = new Vector2(panelSize.x - 72f, 41f);
        accentRect.localScale = Vector3.one;
        accentRect.localRotation = Quaternion.identity;
        phaseTwoAccentBand.color = tutorialAccentColor;
        phaseTwoAccentBand.raycastTarget = false;

        ConfigureTutorialPanelBorder(phaseTwoTopBorder, new Vector2(0f, panelSize.y * 0.5f - 10f), new Vector2(panelSize.x - 26f, 4f));
        ConfigureTutorialPanelBorder(phaseTwoBottomBorder, new Vector2(0f, -panelSize.y * 0.5f + 10f), new Vector2(panelSize.x - 26f, 4f));
        ConfigureTutorialPanelBorder(phaseTwoLeftBorder, new Vector2(-panelSize.x * 0.5f + 10f, 0f), new Vector2(4f, panelSize.y - 26f));
        ConfigureTutorialPanelBorder(phaseTwoRightBorder, new Vector2(panelSize.x * 0.5f - 10f, 0f), new Vector2(4f, panelSize.y - 26f));
    }

    private void UpdateBossRoundInfo()
    {
        if (phaseTwoRound == null || phaseTwoScore == null)
            return;

        bool shouldShow = gameFlow != null &&
                          gameFlow.ShouldShowPhaseTwoOverlay() &&
                          gameFlow.IsPhaseTwoCalibrated &&
                          gameFlow.CurrentPhaseTwoState != BullfightGameFlow.PhaseTwoState.Intro &&
                          gameFlow.CurrentPhaseTwoState != BullfightGameFlow.PhaseTwoState.Calibration &&
                          gameFlow.CurrentPhaseTwoState != BullfightGameFlow.PhaseTwoState.Standoff;

        phaseTwoRound.gameObject.SetActive(shouldShow);
        phaseTwoScore.gameObject.SetActive(shouldShow);
        if (!shouldShow)
            return;

        ConfigureBossInfoText(phaseTwoRound, phaseTwoRoundPosition, $"\u7b2c {gameFlow.PhaseTwoRoundIndex} / {gameFlow.PhaseTwoMaxRounds} \u56de\u5408");
        ConfigureBossInfoText(phaseTwoScore, phaseTwoScorePosition, $"\u4f60 {gameFlow.PhaseTwoPlayerHitCount} : {gameFlow.PhaseTwoBullHitCount} \u725b");
    }

    private void UpdatePhaseTwoBars()
    {
        if (gameFlow == null || !gameFlow.ShouldShowPhaseTwoOverlay())
            return;

        if (playerHealthSlider != null)
            playerHealthSlider.value = gameFlow.GetPhaseTwoPlayerHealthNormalized();

        if (bullHealthSlider != null)
            bullHealthSlider.value = gameFlow.GetPhaseTwoBullHealthNormalized();
    }

    private void UpdatePhaseTwoForceUi()
    {
        bool showForceUi = gameFlow != null && gameFlow.ShouldShowPhaseTwoOverlay();

        if (playerStaminaSlider != null)
            playerStaminaSlider.gameObject.SetActive(!showForceUi);

        Transform playerStaminaLabelTransform = playerStaminaSlider != null
            ? playerStaminaSlider.transform.Find(playerStaminaLabelName)
            : null;
        if (playerStaminaLabelTransform != null)
            playerStaminaLabelTransform.gameObject.SetActive(!showForceUi);

        if (phaseTwoForceSlider != null)
            phaseTwoForceSlider.gameObject.SetActive(showForceUi);

        if (phaseTwoForceLabel != null)
            phaseTwoForceLabel.gameObject.SetActive(showForceUi);

        if (phaseTwoForceValue != null)
            phaseTwoForceValue.gameObject.SetActive(showForceUi);

        if (phaseTwoForceThresholdMarker != null)
            phaseTwoForceThresholdMarker.gameObject.SetActive(showForceUi);

        if (!showForceUi || gameFlow == null || phaseTwoForceSlider == null)
            return;

        phaseTwoForceSlider.value = gameFlow.PhaseTwoCurrentForceNormalized;

        if (phaseTwoForceValue != null)
            phaseTwoForceValue.text = Mathf.RoundToInt(gameFlow.PhaseTwoCurrentForce).ToString();
    }

    private void UpdateBullHealthStyling()
    {
        if (bullHealthSlider == null)
            return;

        Image fillImage = bullHealthSlider.fillRect != null ? bullHealthSlider.fillRect.GetComponent<Image>() : null;
        if (fillImage != null && !hasCachedBullFillColor)
        {
            cachedBullFillSprite = fillImage.sprite;
            cachedBullFillColor = fillImage.color;
            hasCachedBullFillColor = true;
        }

        bool phaseTwoActive = gameFlow != null && gameFlow.ShouldShowPhaseTwoOverlay();
        if (fillImage != null && hasCachedBullFillColor)
        {
            fillImage.sprite = phaseTwoActive && phaseTwoBullFillSprite != null ? phaseTwoBullFillSprite : cachedBullFillSprite;
            fillImage.color = phaseTwoActive ? bullPhaseTwoFillColor : cachedBullFillColor;
        }

        SetBossSegmentVisible(bullBossSegmentLeft, phaseTwoActive);
        SetBossSegmentVisible(bullBossSegmentRight, phaseTwoActive);
    }

    private static Sprite ResolveBullPhaseTwoFillSprite(Slider slider)
    {
        if (slider == null)
            return null;

        Transform originalParent = slider.transform.parent;
        if (originalParent == null)
            return null;

        foreach (Transform child in originalParent)
        {
            if (child == slider.transform)
                continue;

            Image siblingImage = child.GetComponent<Image>();
            if (siblingImage != null && siblingImage.sprite != null)
                return siblingImage.sprite;
        }

        return null;
    }

    private void GetPhaseTwoOverlayContent(out string titleText, out string subtitleText, out string statusText)
    {
        titleText = string.Empty;
        subtitleText = string.Empty;
        statusText = string.Empty;

        if (gameFlow == null)
            return;

        switch (gameFlow.CurrentPhaseTwoState)
        {
            case BullfightGameFlow.PhaseTwoState.Intro:
                titleText = "\u968e\u6bb5\u4e8c";
                subtitleText = gameFlow.IsPhaseTwoQuestionVisible ? "\u771f\u7684\u53ea\u6709\u6bba\u4e86\u4ed6\u9019\u500b\u8fa6\u6cd5\u55ce?" : string.Empty;
                break;
            case BullfightGameFlow.PhaseTwoState.Tutorial:
                titleText = "\u968e\u6bb5\u4e8c\uff1a\u523a\u64ca\u6559\u5b78";
                subtitleText = string.IsNullOrWhiteSpace(gameFlow.CurrentPhaseTwoTutorialInstruction)
                    ? "\u5148\u7a69\u5b9a\u6301\u528d\u5b8c\u6210\u6821\u6e96\uff0c\u6821\u6e96\u5f8c\u8981\u5728\u523a\u64ca\u6642\u6a5f\u5167\u5411\u524d\u523a\u51fa\u3002\u6210\u529f\u8207\u5426\u4ecd\u4ee5\u529b\u9053\u9580\u6abb\u8207 QTE \u5224\u5b9a\u70ba\u6e96\u3002"
                    : gameFlow.CurrentPhaseTwoTutorialInstruction;
                statusText = gameFlow.IsPhaseTwoTutorialAdvanceReady
                    ? "\u8b80\u5b8c\u5f8c\u6309\u78ba\u8a8d\u7e7c\u7e8c\u6821\u6e96"
                    : $"\u8acb\u5148\u8b80\u5b8c\u523a\u64ca\u6d41\u7a0b... {Mathf.CeilToInt(gameFlow.PhaseTwoTutorialSecondsRemaining)}s";
                break;
            case BullfightGameFlow.PhaseTwoState.Calibration:
                titleText = $"\u7b2c {gameFlow.PhaseTwoUpcomingRoundIndex} / {gameFlow.PhaseTwoMaxRounds} \u56de\u5408\u6821\u6e96";
                subtitleText = string.IsNullOrWhiteSpace(gameFlow.CurrentPhaseTwoCalibrationLine)
                    ? "\u6301\u528d\u4e0d\u52d5 5 \u79d2\u6821\u6e96"
                    : gameFlow.CurrentPhaseTwoCalibrationLine;
                statusText = string.IsNullOrWhiteSpace(gameFlow.CurrentPhaseTwoCalibrationStatus)
                    ? $"\u6301\u528d\u4e0d\u52d5 5 \u79d2\u6821\u6e96  {Mathf.RoundToInt(gameFlow.PhaseTwoCalibrationProgress * 100f)}%"
                    : gameFlow.CurrentPhaseTwoCalibrationStatus;
                break;
            case BullfightGameFlow.PhaseTwoState.Standoff:
                titleText = "\u5c0d\u5cd9";
                subtitleText = string.IsNullOrWhiteSpace(gameFlow.CurrentPhaseTwoStandoffInstruction)
                    ? "\u6309 E \u6216\u624b\u628a\u523a\u64ca\u9375\uff0c\u6216\u529b\u9053\u8d85\u904e 35 \u6253\u7834\u5c0d\u5cd9"
                    : gameFlow.CurrentPhaseTwoStandoffInstruction;
                statusText = $"\u5012\u6578 {Mathf.CeilToInt(gameFlow.PhaseTwoMercyTimeRemaining)}s";
                break;
            case BullfightGameFlow.PhaseTwoState.RoundPrepare:
            case BullfightGameFlow.PhaseTwoState.RoundWindow:
                titleText = gameFlow.CurrentRoundHasPerfectAdvantage ? "\u7834\u7dbb" : "\u5c0d\u6c7a";
                if (gameFlow.CurrentPhaseTwoState == BullfightGameFlow.PhaseTwoState.RoundPrepare)
                {
                    if (gameFlow.IsPhaseTwoRoundStanceConfirming)
                    {
                        titleText = "\u5b9a\u52e2";
                        subtitleText = "\u6bcf\u4e00\u64ca\u4e4b\u524d\uff0c\u5148\u7a69\u4f4f\u81ea\u5df1";
                        statusText = $"{Mathf.RoundToInt(gameFlow.PhaseTwoRoundStanceProgress * 100f)}%";
                    }
                    else
                    {
                        subtitleText = string.IsNullOrWhiteSpace(gameFlow.CurrentPhaseTwoReflectionLine)
                            ? "\u7b49\u5f85\u725b\u9732\u51fa\u7834\u7dbb\uff0c\u7136\u5f8c\u529b\u9053\u8d85\u904e 35 \u523a\u64ca"
                            : gameFlow.CurrentPhaseTwoReflectionLine;
                        statusText = gameFlow.CurrentRoundHasPerfectAdvantage
                            ? "\u4e0a\u4e00\u64ca\u7559\u4e0b\u7684\u7834\u7dbb\u9084\u5728\u64f4\u5927..."
                            : "\u725b\u6b63\u5728\u84c4\u529b...";
                    }
                }
                else
                {
                    subtitleText = "\u73fe\u5728\u529b\u9053\u8d85\u904e 35 \u523a\u64ca";
                    statusText = "\u73fe\u5728\u51fa\u624b";
                }
                break;
            case BullfightGameFlow.PhaseTwoState.RoundResolve:
                titleText = gameFlow.LastPhaseTwoResult switch
                {
                    "Perfect!" => "PERFECT",
                    "Good" => "GOOD",
                    "Miss" => "MISS",
                    _ => string.Empty
                };
                subtitleText = gameFlow.LastPhaseTwoResult switch
                {
                    "Perfect!" => "\u4e0b\u4e00\u56de\u5408\u7834\u7dbb\u66f4\u5927",
                    "Good" => "\u4f60\u6210\u529f\u963b\u6b62\u4e86\u725b\u7684\u653b\u64ca",
                    "Miss" => gameFlow.IsPhaseTwoMissPauseActive
                        ? gameFlow.CurrentPhaseTwoResolveNarration
                        : "\u4f60\u6c92\u80fd\u963b\u6b62\u725b\u7684\u653b\u64ca",
                    _ => string.Empty
                };
                if (gameFlow.IsPhaseTwoMissPauseActive)
                    statusText = "\u51b7\u975c 3 \u79d2\uff0c\u4e0b\u4e00\u56de\u5408\u5373\u5c07\u958b\u59cb";
                break;
        }
    }

    private void ConfigureBossInfoText(Text text, Vector2 anchoredPosition, string value)
    {
        if (text == null)
            return;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(560f, 28f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = phaseTwoInfoFontSize;
        text.fontStyle = FontStyle.Bold;
        text.color = bullBossTitleColor;
        text.text = value;
        ApplyLocalizedUiFont(text, value, phaseTwoInfoFontSize, wrap: false, VerticalWrapMode.Truncate, minBestFitSize: Mathf.Max(12, phaseTwoInfoFontSize - 4));
    }

    private void ConfigureCenteredText(Text text, Vector2 anchoredPosition, int fontSize, Color color, FontStyle style, string value)
    {
        if (text == null)
            return;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(980f, 48f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.text = value;
        ApplyLocalizedUiFont(text, value, fontSize, wrap: true, VerticalWrapMode.Truncate, minBestFitSize: Mathf.Max(12, fontSize - 8));
        text.gameObject.SetActive(!string.IsNullOrEmpty(value));
    }

    private void ConfigureTitleText(Text text, Vector2 anchoredPosition, int fontSize, Color color, FontStyle style, string value)
    {
        if (text == null)
            return;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(980f, Mathf.Max(96f, fontSize * 1.8f));
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.text = value;
        ApplyLocalizedUiFont(
            text,
            value,
            fontSize,
            wrap: false,
            VerticalWrapMode.Truncate,
            minBestFitSize: Mathf.Max(18, fontSize - 24));
        text.gameObject.SetActive(!string.IsNullOrEmpty(value));
    }

    private void ConfigureTutorialBodyText(Text text, Vector2 anchoredPosition, Vector2 size, int fontSize, Color color, string value, bool useBestFit)
    {
        if (text == null)
            return;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        text.alignment = TextAnchor.UpperLeft;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Normal;
        text.color = color;
        text.text = value;
        ApplyLocalizedUiFont(
            text,
            value,
            fontSize,
            wrap: true,
            useBestFit ? VerticalWrapMode.Truncate : VerticalWrapMode.Overflow,
            allowBestFit: useBestFit || ContainsCjk(value),
            minBestFitSize: useBestFit ? 18 : Mathf.Max(14, fontSize - 6),
            cjkLineSpacing: useBestFit ? 0.88f : 0.94f);
        text.gameObject.SetActive(!string.IsNullOrEmpty(value));
    }

    private static void SetBossSegmentVisible(Image segment, bool visible)
    {
        if (segment != null)
            segment.gameObject.SetActive(visible);
    }

    private static string GetPhaseLabel(BullfightGameFlow.GamePhase phase, BullfightGameFlow.EndingType ending)
    {
        return phase switch
        {
            BullfightGameFlow.GamePhase.PhaseZeroTutorial => "\u7b2c0\u968e\u6bb5",
            BullfightGameFlow.GamePhase.PhaseOne => "\u968e\u6bb5\u4e00",
            BullfightGameFlow.GamePhase.PhaseTwo => "\u968e\u6bb5\u4e8c",
            BullfightGameFlow.GamePhase.Ending => ending switch
            {
                BullfightGameFlow.EndingType.Glory => "\u7d50\u5c40\u4e00\uff08\u725b\u6b7b\uff09",
                BullfightGameFlow.EndingType.Tragedy => "\u7d50\u5c40\u4e8c\uff08\u73a9\u5bb6\u6b7b\uff09",
                BullfightGameFlow.EndingType.Mercy => "\u7d50\u5c40\u4e09\uff0815\u79d2\u50f5\u6301\uff09",
                _ => "\u7d50\u5c40\u6f14\u51fa"
            },
            _ => "\u672a\u77e5\u968e\u6bb5"
        };
    }

    private static RectTransform GetOrCreateUiRect(RectTransform parent, string objectName)
    {
        if (parent == null)
            return null;

        Transform existing = parent.Find(objectName);
        if (existing != null)
            return existing as RectTransform;

        GameObject go = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static Image GetOrCreateUiImage(RectTransform parent, string objectName)
    {
        if (parent == null)
            return null;

        Transform existing = parent.Find(objectName);
        if (existing != null)
            return existing.GetComponent<Image>();

        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return go.GetComponent<Image>();
    }

    private static Text GetOrCreateUiText(RectTransform parent, string objectName)
    {
        if (parent == null)
            return null;

        Transform existing = parent.Find(objectName);
        if (existing != null)
            return existing.GetComponent<Text>();

        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        Text text = go.GetComponent<Text>();
        text.font = GetUiFont();
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static Font GetUiFont()
    {
        if (cachedUiFont != null)
            return cachedUiFont;

        cachedUiFont = Font.CreateDynamicFontFromOSFont(new[]
        {
            "Microsoft JhengHei UI",
            "Microsoft JhengHei",
            "Arial",
            "Segoe UI"
        }, 18);

        return cachedUiFont;
    }

    private static Font GetCjkUiFont()
    {
        if (cachedCjkUiFont != null)
            return cachedCjkUiFont;

        cachedCjkUiFont = Resources.Load<Font>("Cubic_11");
        if (cachedCjkUiFont == null)
            cachedCjkUiFont = GetUiFont();

        return cachedCjkUiFont;
    }

    private static void ApplyLocalizedUiFont(
        Text text,
        string value,
        int fontSize,
        bool wrap,
        VerticalWrapMode verticalOverflow,
        bool allowBestFit = true,
        int minBestFitSize = 0,
        float cjkLineSpacing = 1f)
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

        if (useCjkFont && allowBestFit)
        {
            int resolvedMinSize = minBestFitSize > 0
                ? minBestFitSize
                : Mathf.Max(10, Mathf.RoundToInt(fontSize * 0.72f));
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Min(resolvedMinSize, fontSize);
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

    private void ApplyArcadeAccentFont(Text text)
    {
        if (text == null)
            return;

        Font accentFont = ResolveArcadeAccentFont();
        if (accentFont == null)
            return;

        text.font = accentFont;
    }

    private void ApplyArcadeAccentStyle(
        Text text,
        ArcadeAccentSizeTier sizeTier,
        bool wrap = false,
        int? maxOverride = null,
        int? minOverride = null,
        float? legacyScaleOverride = null)
    {
        if (text == null)
            return;

        ApplyReadableFallbackStyle(text, text.fontSize, wrap);
    }

    private void ApplyReadableFallbackStyle(Text text, int fontSize, bool wrap = false)
    {
        if (text == null)
            return;

        text.font = GetUiFont();
        text.fontSize = fontSize;
        text.alignByGeometry = false;
        text.resizeTextForBestFit = false;
        text.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
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

    private static Slider GetOrCloneSlider(RectTransform parent, Slider template, string objectName)
    {
        if (parent == null || template == null)
            return null;

        Transform existing = parent.Find(objectName);
        if (existing != null)
            return existing.GetComponent<Slider>();

        GameObject clone = Instantiate(template.gameObject, parent, false);
        clone.name = objectName;
        return clone.GetComponent<Slider>();
    }

    private static void StretchToFullScreen(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private void DisableLegacyShooterUiOnce()
    {
        if (legacyUiDisabled)
            return;

        var sceneTransforms = BullfightSceneCache.GetSceneObjects<Transform>();
        for (int i = 0; i < sceneTransforms.Count; i++)
        {
            Transform sceneTransform = sceneTransforms[i];
            if (sceneTransform == null)
                continue;

            if (!IsLegacyShooterUiObject(sceneTransform.name) && !IsChildOfLegacyShooterCanvas(sceneTransform))
                continue;

            sceneTransform.gameObject.SetActive(false);
        }

        legacyUiDisabled = true;
    }

    private bool IsLegacyShooterUiObject(string objectName)
    {
        return objectName == legacyHudCanvasName ||
               objectName == legacyHudCanvasCloneName ||
               objectName == weaponAmmoName ||
               objectName == ammoName ||
               objectName == crosshairName ||
               objectName == tutorialName ||
               objectName == promptName ||
               objectName == textTimescaleName ||
               objectName == textTutorialPromptName ||
               objectName == textTutorialTextName ||
               objectName == textTutorialName ||
               objectName == textAmmunitionCurrentName ||
               objectName == textAmmunitionTotalName ||
               objectName == textAmmunitionDividerName ||
               objectName == crosshairClassicName ||
               objectName == crosshairDotAdjusterName;
    }

    private bool IsChildOfLegacyShooterCanvas(Transform candidate)
    {
        Transform current = candidate;
        while (current != null)
        {
            if (current.name == legacyHudCanvasName || current.name == legacyHudCanvasCloneName)
                return true;

            current = current.parent;
        }

        return false;
    }
}
