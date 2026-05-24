using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class BullfightStartMenu : MonoBehaviour
{
    private static Font cachedFont;
    private static Font cachedCjkFont;

    [Header("Text")]
    [SerializeField] private string titleText = "西班牙鬥牛";
    [SerializeField] private string subtitleText = "第一人稱鬥牛體驗";
    [SerializeField] private string startButtonText = "開始遊戲";
    [SerializeField] private string tutorialButtonText = "新手教學";
    [SerializeField] private string hintText = "選擇「開始遊戲」或「新手教學」";

    [Header("Layout")]
    [SerializeField] private Vector2 panelSize = new Vector2(760f, 430f);
    [SerializeField] private Vector2 titlePosition = new Vector2(0f, 110f);
    [SerializeField] private Vector2 subtitlePosition = new Vector2(0f, 40f);
    [SerializeField] private Vector2 hintPosition = new Vector2(0f, -26f);
    [SerializeField] private Vector2 buttonCenterPosition = new Vector2(0f, -126f);
    [SerializeField] private Vector2 buttonSize = new Vector2(260f, 72f);
    [SerializeField] private float buttonSpacing = 36f;
    [SerializeField] private int canvasSortingOrder = 4000;
    [SerializeField] private Vector2 canvasReferenceResolution = new Vector2(1920f, 1080f);
    [SerializeField, Range(0f, 1f)] private float canvasMatchWidthOrHeight = 0.5f;
    [SerializeField] private float panelBorderInset = 10f;
    [SerializeField] private float panelBorderThickness = 4f;
    [SerializeField] private float panelBorderTrim = 26f;
    [SerializeField] private Vector2 accentBandPosition = new Vector2(0f, 82f);
    [SerializeField] private Vector2 accentBandSize = new Vector2(680f, 54f);
    [SerializeField] private int titleFontSize = 54;
    [SerializeField] private int subtitleFontSize = 24;
    [SerializeField] private int hintFontSize = 20;
    [SerializeField] private int buttonFontSize = 30;

    [Header("Colors")]
    [SerializeField] private Color backdropColor = new Color(0.07f, 0.02f, 0.02f, 0.72f);
    [SerializeField] private Color panelColor = new Color(0.14f, 0.04f, 0.04f, 0.9f);
    [SerializeField] private Color borderColor = new Color(0.84f, 0.71f, 0.49f, 0.9f);
    [SerializeField] private Color accentColor = new Color(0.72f, 0.12f, 0.12f, 1f);
    [SerializeField] private Color titleColor = new Color(0.98f, 0.92f, 0.78f, 1f);
    [SerializeField] private Color subtitleColor = new Color(0.9f, 0.82f, 0.66f, 1f);
    [SerializeField] private Color hintColor = new Color(0.86f, 0.84f, 0.8f, 0.92f);
    [SerializeField] private Color buttonColor = new Color(0.56f, 0.09f, 0.09f, 1f);
    [SerializeField] private Color buttonTextColor = new Color(1f, 0.95f, 0.86f, 1f);
    [SerializeField] private Color buttonHighlightColor = new Color(0.68f, 0.16f, 0.14f, 1f);
    [SerializeField] private Color buttonPressedColor = new Color(0.42f, 0.06f, 0.06f, 1f);

    [Header("Press Feedback")]
    [SerializeField, Range(0f, 1f)] private float pressRumbleLowFrequency = 0.35f;
    [SerializeField, Range(0f, 1f)] private float pressRumbleHighFrequency = 0.65f;
    [SerializeField] private float pressRumbleDuration = 0.12f;
    [SerializeField] private float confirmSuppressionDuration = 0.15f;
    [SerializeField] private float selectionClipRetriggerCooldown = 0.12f;

    private Canvas canvas;
    private GameObject root;
    private Button startButton;
    private Button tutorialButton;
    private AudioSource selectionAudioSource;
    private AudioClip startFocusClip;
    private AudioClip tutorialFocusClip;
    private float focusClipVolume = 1f;
    private GameObject lastSelectedObject;
    private bool started;
    private Coroutine pressRumbleRoutine;
    private float suppressConfirmUntilUnscaledTime = -1f;
    private AudioClip lastSelectionClip;
    private float lastSelectionClipPlayedAt = -999f;

    public bool IsMenuVisible => canvas != null && canvas.gameObject.activeSelf;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureStartMenuExists()
    {
        if (FindObjectOfType<ManualStartMenuController>(true) != null)
            return;

        if (FindObjectOfType<BullfightStartMenu>(true) != null)
            return;

        GameObject startMenuObject = new("BullfightStartMenu");
        _ = startMenuObject.AddComponent<BullfightStartMenu>();
    }

    private void Start()
    {
        if (FindObjectOfType<ManualStartMenuController>(true) != null)
        {
            PrepareForDeferredShow();
            return;
        }

        BuildMenu();
        ShowMenu();
    }

    private void Update()
    {
        if (started || canvas == null || !canvas.gameObject.activeSelf)
            return;

        EnsureButtonSelected();
        UpdateSelectionAudio();

        if (Time.unscaledTime < suppressConfirmUntilUnscaledTime)
            return;

        if (!WasConfirmRequestedThisFrame() || EventSystem.current == null)
            return;

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == tutorialButton?.gameObject)
            OnTutorialButtonPressed();
        else
            OnStartButtonPressed();
    }

    public void BeginGame()
    {
        if (started)
            return;

        started = true;
        HideMenu();

        ManualStartMenuController manualMenu = FindObjectOfType<ManualStartMenuController>(true);
        manualMenu?.SetGameplayUiVisibleForGameplay(true);

        BullfightGameFlow gameFlow = FindObjectOfType<BullfightGameFlow>(true);
        if (gameFlow == null)
        {
            GameObject gameFlowObject = new("BullfightGameFlow");
            gameFlow = gameFlowObject.AddComponent<BullfightGameFlow>();
        }

        gameFlow.SetMainMenuGameplayLocked(false);
        gameFlow.StartPhaseOneDirect(true);
    }

    public void BeginTutorial()
    {
        if (started)
            return;

        started = true;
        HideMenu();

        ManualStartMenuController manualMenu = FindObjectOfType<ManualStartMenuController>(true);
        manualMenu?.SetGameplayUiVisibleForGameplay(true);

        BullfightGameFlow gameFlow = FindObjectOfType<BullfightGameFlow>(true);
        if (gameFlow == null)
        {
            GameObject gameFlowObject = new("BullfightGameFlow");
            gameFlow = gameFlowObject.AddComponent<BullfightGameFlow>();
        }

        gameFlow.SetMainMenuGameplayLocked(false);
        gameFlow.BeginTutorial();
    }

    public void ReturnToStartSelectionMenu()
    {
        ResetAndShowMenu();
    }

    public void ConfigureSelectionAudio(AudioClip startClip, AudioClip tutorialClip, float volume)
    {
        startFocusClip = startClip;
        tutorialFocusClip = tutorialClip;
        focusClipVolume = Mathf.Clamp01(volume);
    }

    public void ReturnToMenu()
    {
        ResetAndShowMenu();
    }

    public void ResetAndShowMenu()
    {
        BuildMenu();

        if (canvas != null)
            canvas.gameObject.SetActive(true);

        ShowMenu();
    }

    public void PrepareForDeferredShow()
    {
        BuildMenu();
        started = false;
        lastSelectedObject = null;
        ResetSelectionAudioState();
        suppressConfirmUntilUnscaledTime = Time.unscaledTime + Mathf.Max(0.05f, confirmSuppressionDuration);

        if (root != null)
            root.SetActive(true);

        if (canvas != null)
            canvas.gameObject.SetActive(false);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void HideMenu()
    {
        if (canvas != null)
            canvas.gameObject.SetActive(false);

        lastSelectedObject = null;
        ResetSelectionAudioState();
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void BuildMenu()
    {
        if (canvas == null)
        {
            Transform existingCanvas = transform.Find("BullfightStartMenuCanvas");
            if (existingCanvas != null)
                canvas = existingCanvas.GetComponent<Canvas>();
        }

        if (canvas != null && root == null)
        {
            Transform existingRoot = canvas.transform.Find("MenuPanel");
            if (existingRoot != null)
                root = existingRoot.gameObject;
        }

        if (root != null && startButton == null)
        {
            Transform existingStartButton = root.transform.Find("StartButton");
            if (existingStartButton != null)
                startButton = existingStartButton.GetComponent<Button>();
        }

        if (root != null && tutorialButton == null)
        {
            Transform existingTutorialButton = root.transform.Find("TutorialButton");
            if (existingTutorialButton != null)
                tutorialButton = existingTutorialButton.GetComponent<Button>();
        }

        if (canvas != null && root != null && startButton != null && tutorialButton != null)
            return;

        GameObject canvasObject = new GameObject("BullfightStartMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = canvasReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = canvasMatchWidthOrHeight;

        GameObject backdrop = CreateImage("Backdrop", canvasObject.transform, backdropColor);
        StretchToFullScreen(backdrop.GetComponent<RectTransform>());

        root = CreateImage("MenuPanel", canvasObject.transform, panelColor);
        RectTransform panelRect = root.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = panelSize;
        panelRect.anchoredPosition = Vector2.zero;

        CreateBorder(root.transform, new Vector2(0f, panelSize.y * 0.5f - panelBorderInset), new Vector2(panelSize.x - panelBorderTrim, panelBorderThickness));
        CreateBorder(root.transform, new Vector2(0f, -panelSize.y * 0.5f + panelBorderInset), new Vector2(panelSize.x - panelBorderTrim, panelBorderThickness));
        CreateBorder(root.transform, new Vector2(-panelSize.x * 0.5f + panelBorderInset, 0f), new Vector2(panelBorderThickness, panelSize.y - panelBorderTrim));
        CreateBorder(root.transform, new Vector2(panelSize.x * 0.5f - panelBorderInset, 0f), new Vector2(panelBorderThickness, panelSize.y - panelBorderTrim));
        CreateImageBand(root.transform, accentBandPosition, accentBandSize, accentColor);

        Text title = CreateText("Title", root.transform, titleText, titleFontSize, titleColor, FontStyle.Bold);
        ConfigureTextRect(title.rectTransform, titlePosition, new Vector2(660f, 84f));

        Text subtitle = CreateText("Subtitle", root.transform, subtitleText, subtitleFontSize, subtitleColor, FontStyle.Normal);
        ConfigureTextRect(subtitle.rectTransform, subtitlePosition, new Vector2(620f, 42f));

        Text hint = CreateText("Hint", root.transform, hintText, hintFontSize, hintColor, FontStyle.Italic);
        ConfigureTextRect(hint.rectTransform, hintPosition, new Vector2(660f, 50f));

        float horizontalOffset = (buttonSize.x * 0.5f) + (buttonSpacing * 0.5f);
        startButton = CreateMenuButton(root.transform, "StartButton", startButtonText, buttonCenterPosition + new Vector2(-horizontalOffset, 0f));
        tutorialButton = CreateMenuButton(root.transform, "TutorialButton", tutorialButtonText, buttonCenterPosition + new Vector2(horizontalOffset, 0f));

        ConfigureButtonNavigation(startButton, tutorialButton, selectOnLeft: null, selectOnRight: tutorialButton);
        ConfigureButtonNavigation(tutorialButton, startButton, selectOnLeft: startButton, selectOnRight: null);

        startButton.onClick.RemoveAllListeners();
        startButton.onClick.AddListener(OnStartButtonPressed);

        tutorialButton.onClick.RemoveAllListeners();
        tutorialButton.onClick.AddListener(OnTutorialButtonPressed);
    }

    private void ShowMenu()
    {
        started = false;
        lastSelectedObject = null;
        ResetSelectionAudioState();
        suppressConfirmUntilUnscaledTime = Time.unscaledTime + Mathf.Max(0.05f, confirmSuppressionDuration);

        ManualStartMenuController manualMenu = FindObjectOfType<ManualStartMenuController>(true);
        manualMenu?.SetGameplayUiVisibleForGameplay(false);

        BullfightGameFlow gameFlow = FindObjectOfType<BullfightGameFlow>(true);
        gameFlow?.ResetSceneForMainMenu();
        gameFlow?.SetMainMenuGameplayLocked(true);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (canvas != null)
            canvas.gameObject.SetActive(true);

        if (root != null)
            root.SetActive(true);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        EnsureButtonSelected();
    }

    private void EnsureButtonSelected()
    {
        if (startButton == null || EventSystem.current == null)
            return;

        if (EventSystem.current.currentSelectedGameObject != null)
            return;

        EventSystem.current.SetSelectedGameObject(startButton.gameObject);
    }

    private void UpdateSelectionAudio()
    {
        if (EventSystem.current == null)
            return;

        GameObject currentSelectedObject = EventSystem.current.currentSelectedGameObject;
        if (currentSelectedObject == lastSelectedObject)
            return;

        lastSelectedObject = currentSelectedObject;

        if (currentSelectedObject == startButton?.gameObject)
            PlaySelectionClip(startFocusClip);
        else if (currentSelectedObject == tutorialButton?.gameObject)
            PlaySelectionClip(tutorialFocusClip);
    }

    private void PlaySelectionClip(AudioClip clip)
    {
        if (clip == null)
            return;

        if (selectionAudioSource == null)
        {
            selectionAudioSource = GetComponent<AudioSource>();
            if (selectionAudioSource == null)
                selectionAudioSource = gameObject.AddComponent<AudioSource>();

            selectionAudioSource.playOnAwake = false;
            selectionAudioSource.loop = false;
            selectionAudioSource.spatialBlend = 0f;
        }

        if (selectionAudioSource.isPlaying &&
            lastSelectionClip == clip &&
            Time.unscaledTime - lastSelectionClipPlayedAt < selectionClipRetriggerCooldown)
        {
            return;
        }

        selectionAudioSource.Stop();
        selectionAudioSource.clip = clip;
        selectionAudioSource.volume = focusClipVolume;
        selectionAudioSource.pitch = 1f;
        selectionAudioSource.Play();
        lastSelectionClip = clip;
        lastSelectionClipPlayedAt = Time.unscaledTime;
    }

    private void ResetSelectionAudioState()
    {
        lastSelectionClip = null;
        lastSelectionClipPlayedAt = -999f;
        if (selectionAudioSource != null)
            selectionAudioSource.Stop();
    }

    private void OnStartButtonPressed()
    {
        TriggerPressRumble();
        BeginGame();
    }

    private void OnTutorialButtonPressed()
    {
        TriggerPressRumble();
        BeginTutorial();
    }

    private void TriggerPressRumble()
    {
        TriggerRumble(
            ref pressRumbleRoutine,
            pressRumbleLowFrequency,
            pressRumbleHighFrequency,
            pressRumbleDuration);
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

    private static bool WasConfirmRequestedThisFrame()
    {
        bool keyboardRequested = Keyboard.current != null &&
                                 (Keyboard.current.enterKey.wasPressedThisFrame ||
                                  Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
                                  Keyboard.current.spaceKey.wasPressedThisFrame);

        bool gamepadRequested = Gamepad.current != null &&
                                (Gamepad.current.startButton.wasPressedThisFrame ||
                                 Gamepad.current.buttonSouth.wasPressedThisFrame);

        return keyboardRequested || gamepadRequested;
    }

    private Button CreateMenuButton(Transform parent, string objectName, string label, Vector2 position)
    {
        GameObject buttonObject = CreateImage(objectName, parent, buttonColor);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = buttonSize;
        buttonRect.anchoredPosition = position;

        CreateBorder(buttonObject.transform, new Vector2(0f, buttonSize.y * 0.5f - 2f), new Vector2(buttonSize.x - 10f, 3f));
        CreateBorder(buttonObject.transform, new Vector2(0f, -buttonSize.y * 0.5f + 2f), new Vector2(buttonSize.x - 10f, 3f));

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = buttonColor;
        colors.highlightedColor = buttonHighlightColor;
        colors.pressedColor = buttonPressedColor;
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        Text buttonLabel = CreateText("ButtonLabel", buttonObject.transform, label, buttonFontSize, buttonTextColor, FontStyle.Bold);
        ConfigureTextRect(buttonLabel.rectTransform, Vector2.zero, buttonSize - new Vector2(24f, 12f));
        return button;
    }

    private static void ConfigureButtonNavigation(Button button, Button fallback, Selectable selectOnLeft, Selectable selectOnRight)
    {
        if (button == null)
            return;

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.Explicit;
        navigation.selectOnLeft = selectOnLeft ?? fallback;
        navigation.selectOnRight = selectOnRight ?? fallback;
        navigation.selectOnUp = button;
        navigation.selectOnDown = button;
        button.navigation = navigation;
    }

    private static GameObject CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        gameObject.transform.SetParent(parent, false);
        Image image = gameObject.GetComponent<Image>();
        image.color = color;
        return gameObject;
    }

    private static Text CreateText(string objectName, Transform parent, string content, int fontSize, Color color)
    {
        return CreateText(objectName, parent, content, fontSize, color, FontStyle.Normal);
    }

    private static Text CreateText(string objectName, Transform parent, string content, int fontSize, Color color, FontStyle fontStyle)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        gameObject.transform.SetParent(parent, false);
        Text text = gameObject.GetComponent<Text>();
        text.text = content;
        text.font = GetUiFont();
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        ApplyLocalizedFont(text, content, fontSize, wrap: true, VerticalWrapMode.Truncate);
        return text;
    }

    private static Font GetUiFont()
    {
        if (cachedFont != null)
            return cachedFont;

        cachedFont = Font.CreateDynamicFontFromOSFont(new[]
        {
            "Microsoft JhengHei UI",
            "Microsoft JhengHei",
            "Segoe UI",
            "Arial"
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

    private static void ApplyLocalizedFont(Text text, string value, int fontSize, bool wrap, VerticalWrapMode verticalOverflow)
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

        if (useCjkFont)
        {
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(12, Mathf.RoundToInt(fontSize * 0.72f));
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

    private GameObject CreateBorder(Transform parent, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject border = CreateImage("Border", parent, borderColor);
        RectTransform rect = border.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return border;
    }

    private GameObject CreateImageBand(Transform parent, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        GameObject band = CreateImage("AccentBand", parent, color);
        RectTransform rect = band.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return band;
    }

    private static void ConfigureTextRect(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 size)
    {
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;
    }

    private static void StretchToFullScreen(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}
