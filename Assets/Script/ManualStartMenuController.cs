using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class ManualStartMenuController : MonoBehaviour
{
    private static readonly string[] DefaultGameplayUiRootNames =
    {
        "HUD_Canvas",
        "QTE_Runtime_Canvas",
        "P_LPSP_UI_Canvas",
        "P_LPSP_UI_Canvas(Clone)"
    };

    [Header("UI")]
    [SerializeField] private GameObject startMenuRoot;
    [SerializeField] private GameObject controlsPanel;
    [SerializeField] private Image controlsImage;
    [SerializeField] private Button startButton;
    [SerializeField] private Button controlsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button closeControlsButton;

    [Header("Controls Content")]
    [SerializeField] private Sprite controlsSprite;
    [SerializeField] private bool preserveControlsImageAspect = true;

    [Header("Homepage Feedback")]
    [SerializeField] private AudioClip startButtonClickClip;
    [SerializeField] private AudioClip controlsButtonClickClip;
    [SerializeField] private AudioClip quitButtonClickClip;
    [SerializeField] private AudioClip closeControlsButtonClickClip;
    [SerializeField, Range(0f, 1f)] private float homepageClickVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float homepageRumbleLowFrequency = 0.35f;
    [SerializeField, Range(0f, 1f)] private float homepageRumbleHighFrequency = 0.65f;
    [SerializeField] private float homepageRumbleDuration = 0.12f;

    [Header("Start Selection Audio")]
    [SerializeField] private AudioClip startSelectionStartFocusClip;
    [SerializeField] private AudioClip startSelectionTutorialFocusClip;
    [SerializeField, Range(0f, 1f)] private float startSelectionFocusVolume = 1f;

    [Header("Gameplay UI")]
    [SerializeField] private GameObject[] gameplayUiRoots;
    [SerializeField] private bool includeBullfightHudController = true;

    [Header("Button Setup")]
    [SerializeField] private bool configureButtonsOnStart = true;
    [SerializeField] private bool loopMainButtonNavigation = true;
    [SerializeField] private Color normalButtonColor = Color.white;
    [SerializeField] private Color highlightedButtonColor = new Color(0.95f, 0.84f, 0.45f, 1f);
    [SerializeField] private Color pressedButtonColor = new Color(0.78f, 0.33f, 0.18f, 1f);
    [SerializeField] private Color selectedButtonColor = new Color(0.98f, 0.76f, 0.2f, 1f);
    [SerializeField] private Color disabledButtonColor = new Color(0.75f, 0.75f, 0.75f, 0.5f);
    [SerializeField] private float buttonColorMultiplier = 1f;
    [SerializeField] private float buttonFadeDuration = 0.1f;

    [Header("Cameras")]
    [SerializeField] private Camera startMenuCamera;
    [SerializeField] private Camera playerCamera;

    [Header("Audio")]
    [SerializeField] private AudioListener startMenuAudioListener;
    [SerializeField] private AudioListener playerAudioListener;

    [Header("Game Flow")]
    [SerializeField] private BullfightGameFlow gameFlow;

    private BullfightPauseSettingsUI pauseSettingsUI;
    private BullfightStartMenu startSelectionMenu;
    private AudioSource homepageFeedbackAudioSource;
    private bool listenersBound;
    private Coroutine gameplayUiRestoreRoutine;
    private Coroutine homepageRumbleRoutine;

    public bool IsFrontendVisible => IsObjectVisible(startMenuRoot) || IsObjectVisible(controlsPanel);

    private void Start()
    {
        ResolvePauseSettingsUi();

        if (configureButtonsOnStart)
            ConfigureButtons();

        ApplyControlsImage();
        BindButtonListeners();
        EnterHomeMenu();
    }

    private void Update()
    {
        if (!IsFrontendVisible)
            return;

        bool selectionMissingAtFrameStart = EventSystem.current == null || !IsValidFrontendSelection(EventSystem.current.currentSelectedGameObject);
        bool confirmRequested = WasFrontendConfirmRequestedThisFrame();
        bool cancelRequested = WasFrontendCancelRequestedThisFrame();

        EnsureFrontendSelection();

        if (cancelRequested && controlsPanel != null && controlsPanel.activeInHierarchy)
        {
            HideControls();
            return;
        }

        if (confirmRequested && selectionMissingAtFrameStart)
            InvokeSelectedFrontendButton();
    }

    private void OnDestroy()
    {
        UnbindButtonListeners();
    }

    public void EnterHomeMenu()
    {
        ResolvePauseSettingsUi();
        ResolveStartSelectionMenu();
        pauseSettingsUI?.SetFrontendBlocked(true);
        SetGameplayUiVisible(false);
        startSelectionMenu?.PrepareForDeferredShow();

        if (startMenuRoot != null)
            startMenuRoot.SetActive(true);

        if (controlsPanel != null)
            controlsPanel.SetActive(false);

        SetCameraState(startMenuCamera, true);
        SetCameraState(playerCamera, false);
        SetAudioListenerState(startMenuAudioListener, true);
        SetAudioListenerState(playerAudioListener, false);

        gameFlow?.audioController?.StopAllAudio();
        gameFlow?.SetMainMenuGameplayLocked(true);
        SelectButton(startButton);
    }

    public void ReturnToMenu()
    {
        EnterHomeMenu();
    }

    public void ExitHomeMenu()
    {
        ResolvePauseSettingsUi();
        pauseSettingsUI?.SetFrontendBlocked(false);

        if (startMenuRoot != null)
            startMenuRoot.SetActive(false);

        if (controlsPanel != null)
            controlsPanel.SetActive(false);

        SetCameraState(startMenuCamera, false);
        SetAudioListenerState(startMenuAudioListener, false);
        ClearSelectedButton();
    }

    public void StartGame()
    {
        ExitHomeMenu();
        SetCameraState(playerCamera, true);
        SetAudioListenerState(playerAudioListener, true);
        SetGameplayUiVisible(false);
        ShowStartSelectionMenu();
    }

    public void SetGameplayUiVisibleForGameplay(bool visible)
    {
        SetGameplayUiVisible(visible);
    }

    public void ShowStartSelectionMenu()
    {
        ResolveStartSelectionMenu();
        startSelectionMenu?.ConfigureSelectionAudio(
            startSelectionStartFocusClip,
            startSelectionTutorialFocusClip,
            startSelectionFocusVolume);
        startSelectionMenu?.ResetAndShowMenu();

        if (gameplayUiRestoreRoutine != null)
            StopCoroutine(gameplayUiRestoreRoutine);
    }

    public void ShowControls()
    {
        ResolvePauseSettingsUi();
        pauseSettingsUI?.SetFrontendBlocked(true);

        if (controlsPanel != null)
            controlsPanel.SetActive(true);

        SelectButton(closeControlsButton);
    }

    public void HideControls()
    {
        ResolvePauseSettingsUi();
        pauseSettingsUI?.SetFrontendBlocked(true);

        if (controlsPanel != null)
            controlsPanel.SetActive(false);

        SelectButton(controlsButton != null ? controlsButton : startButton);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void ConfigureButtons()
    {
        ConfigureButtonAppearance(startButton);
        ConfigureButtonAppearance(controlsButton);
        ConfigureButtonAppearance(quitButton);
        ConfigureButtonAppearance(closeControlsButton);

        ConfigureMenuNavigation();
        ConfigureControlsNavigation();
    }

    private void ApplyControlsImage()
    {
        if (controlsImage == null)
            return;

        controlsImage.sprite = controlsSprite;
        controlsImage.preserveAspect = preserveControlsImageAspect;
        controlsImage.enabled = controlsSprite != null;
    }

    private void SetGameplayUiVisible(bool visible)
    {
        HashSet<GameObject> resolvedRoots = new HashSet<GameObject>();

        if (gameplayUiRoots != null)
        {
            for (int i = 0; i < gameplayUiRoots.Length; i++)
            {
                GameObject uiRoot = gameplayUiRoots[i];
                if (uiRoot != null)
                    resolvedRoots.Add(uiRoot);
            }
        }

        for (int i = 0; i < DefaultGameplayUiRootNames.Length; i++)
        {
            Transform sceneTransform = BullfightSceneCache.FindSceneObjectByName<Transform>(DefaultGameplayUiRootNames[i]);
            if (sceneTransform != null)
                resolvedRoots.Add(sceneTransform.gameObject);
        }

        foreach (GameObject uiRoot in resolvedRoots)
            SetUiRootState(uiRoot, visible);

        if (!includeBullfightHudController)
            return;

        BullfightHudController hudController = FindObjectOfType<BullfightHudController>(true);
        if (hudController != null)
            hudController.gameObject.SetActive(visible);
    }

    private IEnumerator RestoreGameplayUiAfterStart()
    {
        yield return null;
        SetGameplayUiVisible(true);
        gameplayUiRestoreRoutine = null;
    }

    private static void SetUiRootState(GameObject uiRoot, bool visible)
    {
        if (uiRoot == null)
            return;

        uiRoot.SetActive(visible);

        Canvas canvas = uiRoot.GetComponent<Canvas>();
        if (canvas != null)
            canvas.enabled = visible;

        RectTransform rectTransform = uiRoot.GetComponent<RectTransform>();
        if (rectTransform != null && visible)
        {
            rectTransform.localScale = Vector3.one;

            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
            }
        }
    }

    private void BindButtonListeners()
    {
        if (listenersBound)
            return;

        startButton?.onClick.AddListener(OnStartButtonPressed);
        controlsButton?.onClick.AddListener(OnControlsButtonPressed);
        quitButton?.onClick.AddListener(OnQuitButtonPressed);
        closeControlsButton?.onClick.AddListener(OnCloseControlsButtonPressed);
        listenersBound = true;
    }

    private void UnbindButtonListeners()
    {
        if (!listenersBound)
            return;

        startButton?.onClick.RemoveListener(OnStartButtonPressed);
        controlsButton?.onClick.RemoveListener(OnControlsButtonPressed);
        quitButton?.onClick.RemoveListener(OnQuitButtonPressed);
        closeControlsButton?.onClick.RemoveListener(OnCloseControlsButtonPressed);
        listenersBound = false;
    }

    private void OnStartButtonPressed()
    {
        PlayHomepageFeedback(startButtonClickClip);
        StartGame();
    }

    private void OnControlsButtonPressed()
    {
        PlayHomepageFeedback(controlsButtonClickClip);
        ShowControls();
    }

    private void OnQuitButtonPressed()
    {
        PlayHomepageFeedback(quitButtonClickClip);
        QuitGame();
    }

    private void OnCloseControlsButtonPressed()
    {
        PlayHomepageFeedback(closeControlsButtonClickClip);
        HideControls();
    }

    private void PlayHomepageFeedback(AudioClip clip)
    {
        PlayOneShot(ref homepageFeedbackAudioSource, clip, homepageClickVolume);
        TriggerRumble(
            ref homepageRumbleRoutine,
            homepageRumbleLowFrequency,
            homepageRumbleHighFrequency,
            homepageRumbleDuration);
    }

    private void PlayOneShot(ref AudioSource audioSource, AudioClip clip, float volume)
    {
        if (clip == null)
            return;

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
        }

        audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
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

    private void ConfigureMenuNavigation()
    {
        ConfigureNavigation(
            startButton,
            loopMainButtonNavigation ? quitButton : null,
            controlsButton,
            null,
            null);

        ConfigureNavigation(
            controlsButton,
            startButton,
            quitButton,
            null,
            null);

        ConfigureNavigation(
            quitButton,
            controlsButton,
            loopMainButtonNavigation ? startButton : null,
            null,
            null);
    }

    private void ConfigureControlsNavigation()
    {
        if (closeControlsButton == null)
            return;

        ConfigureNavigation(closeControlsButton, closeControlsButton, closeControlsButton, null, null);
    }

    private void ConfigureButtonAppearance(Button button)
    {
        if (button == null)
            return;

        ColorBlock colors = button.colors;
        colors.normalColor = normalButtonColor;
        colors.highlightedColor = highlightedButtonColor;
        colors.pressedColor = pressedButtonColor;
        colors.selectedColor = selectedButtonColor;
        colors.disabledColor = disabledButtonColor;
        colors.colorMultiplier = buttonColorMultiplier;
        colors.fadeDuration = buttonFadeDuration;
        button.colors = colors;
    }

    private static void ConfigureNavigation(Button button, Selectable selectOnUp, Selectable selectOnDown, Selectable selectOnLeft, Selectable selectOnRight)
    {
        if (button == null)
            return;

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.Explicit;
        navigation.selectOnUp = selectOnUp;
        navigation.selectOnDown = selectOnDown;
        navigation.selectOnLeft = selectOnLeft;
        navigation.selectOnRight = selectOnRight;
        button.navigation = navigation;
    }

    private static bool IsObjectVisible(GameObject target)
    {
        return target != null && target.activeInHierarchy;
    }

    private static void SetCameraState(Camera cameraTarget, bool enabledState)
    {
        if (cameraTarget != null)
            cameraTarget.enabled = enabledState;
    }

    private static void SetAudioListenerState(AudioListener audioListenerTarget, bool enabledState)
    {
        if (audioListenerTarget != null)
            audioListenerTarget.enabled = enabledState;
    }

    private static void ClearSelectedButton()
    {
        if (EventSystem.current == null)
            return;

        EventSystem.current.SetSelectedGameObject(null);
    }

    private static void SelectButton(Button button)
    {
        if (button == null || EventSystem.current == null)
            return;

        EventSystem.current.firstSelectedGameObject = button.gameObject;
        EventSystem.current.SetSelectedGameObject(button.gameObject);
        button.Select();
    }

    private void EnsureFrontendSelection()
    {
        Button desiredButton = GetDesiredFrontendButton();
        if (desiredButton == null)
            return;

        if (EventSystem.current == null)
            return;

        EventSystem.current.firstSelectedGameObject = desiredButton.gameObject;
        if (IsValidFrontendSelection(EventSystem.current.currentSelectedGameObject))
            return;

        SelectButton(desiredButton);
    }

    private Button GetDesiredFrontendButton()
    {
        if (controlsPanel != null && controlsPanel.activeInHierarchy)
            return closeControlsButton != null ? closeControlsButton : startButton;

        if (startMenuRoot != null && startMenuRoot.activeInHierarchy)
        {
            if (startButton != null)
                return startButton;

            if (controlsButton != null)
                return controlsButton;

            return quitButton;
        }

        return null;
    }

    private bool IsValidFrontendSelection(GameObject selectedObject)
    {
        if (selectedObject == null || !selectedObject.activeInHierarchy)
            return false;

        if (controlsPanel != null && controlsPanel.activeInHierarchy)
            return selectedObject == closeControlsButton?.gameObject;

        if (startMenuRoot != null && startMenuRoot.activeInHierarchy)
            return selectedObject == startButton?.gameObject ||
                   selectedObject == controlsButton?.gameObject ||
                   selectedObject == quitButton?.gameObject;

        return false;
    }

    private void InvokeSelectedFrontendButton()
    {
        if (controlsPanel != null && controlsPanel.activeInHierarchy)
        {
            closeControlsButton?.onClick.Invoke();
            return;
        }

        if (EventSystem.current == null)
        {
            startButton?.onClick.Invoke();
            return;
        }

        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
        if (selectedObject == controlsButton?.gameObject)
        {
            controlsButton.onClick.Invoke();
            return;
        }

        if (selectedObject == quitButton?.gameObject)
        {
            quitButton.onClick.Invoke();
            return;
        }

        startButton?.onClick.Invoke();
    }

    private static bool WasFrontendConfirmRequestedThisFrame()
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

    private static bool WasFrontendCancelRequestedThisFrame()
    {
        bool keyboardRequested = Keyboard.current != null &&
                                 (Keyboard.current.escapeKey.wasPressedThisFrame ||
                                  Keyboard.current.backspaceKey.wasPressedThisFrame);

        bool gamepadRequested = Gamepad.current != null &&
                                (Gamepad.current.buttonEast.wasPressedThisFrame ||
                                 Gamepad.current.selectButton.wasPressedThisFrame);

        return keyboardRequested || gamepadRequested;
    }

    private void ResolvePauseSettingsUi()
    {
        if (pauseSettingsUI == null)
            pauseSettingsUI = FindObjectOfType<BullfightPauseSettingsUI>(true);
    }

    private void ResolveStartSelectionMenu()
    {
        if (startSelectionMenu == null)
            startSelectionMenu = FindObjectOfType<BullfightStartMenu>(true);

        if (startSelectionMenu != null)
            return;

        GameObject startMenuObject = new GameObject("BullfightStartMenu");
        startSelectionMenu = startMenuObject.AddComponent<BullfightStartMenu>();
    }

    private void OnValidate()
    {
        ApplyControlsImage();
    }
}
