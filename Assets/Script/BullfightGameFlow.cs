using InfimaGames.LowPolyShooterPack;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public partial class BullfightGameFlow : MonoBehaviour
{
    private void CacheDefaultLightingIfNeeded()
    {
        if (lightingDefaultsCached)
            return;

        defaultAmbientMode = RenderSettings.ambientMode;
        defaultAmbientLight = RenderSettings.ambientLight;
        defaultAmbientIntensity = RenderSettings.ambientIntensity;

        Light[] sceneLights = FindObjectsOfType<Light>();
        for (int i = 0; i < sceneLights.Length; i++)
        {
            Light light = sceneLights[i];
            if (light != null && light.type == LightType.Directional)
            {
                cachedMainDirectionalLight = light;
                defaultDirectionalIntensity = light.intensity;
                defaultDirectionalColor = light.color;
                break;
            }
        }

        lightingDefaultsCached = true;
    }

    private void RestoreDefaultLighting()
    {
        CacheDefaultLightingIfNeeded();

        RenderSettings.ambientMode = defaultAmbientMode;
        RenderSettings.ambientLight = defaultAmbientLight;
        RenderSettings.ambientIntensity = defaultAmbientIntensity;

        if (cachedMainDirectionalLight != null)
        {
            cachedMainDirectionalLight.intensity = defaultDirectionalIntensity;
            cachedMainDirectionalLight.color = defaultDirectionalColor;
        }
    }
    private bool lightingDefaultsCached;
    private UnityEngine.Rendering.AmbientMode defaultAmbientMode;
    private Color defaultAmbientLight;
    private float defaultAmbientIntensity;
    private Light cachedMainDirectionalLight;
    private float defaultDirectionalIntensity;
    private Color defaultDirectionalColor;
    private static Font cachedRuntimeUiFont;
    private static Font cachedRuntimeCjkFont;
    private void ApplyPhaseTwoLighting()
    {
        CacheDefaultLightingIfNeeded();

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.72f, 0.72f, 0.78f);
        RenderSettings.ambientIntensity = 1.15f;

        Light[] sceneLights = FindObjectsOfType<Light>();
        for (int i = 0; i < sceneLights.Length; i++)
        {
            Light light = sceneLights[i];
            if (light != null && light.type == LightType.Directional)
            {
                light.intensity = 1.45f;
                light.color = new Color(0.95f, 0.93f, 0.90f);
            }
        }
    }
    public enum GamePhase
    {
        PhaseZeroTutorial,
        PhaseOne,
        PhaseTwo,
        Ending
    }

    private void UpdatePhaseTwoChargeMotion(float progress)
    {
        if (bullAI == null || playerController == null)
            return;

        Vector3 playerPos = playerController.transform.position;
        Vector3 bullPos = bullAI.transform.position;

        Vector3 dir = (playerPos - bullPos);
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f)
            dir = bullAI.transform.forward;

        dir.Normalize();

        float distance = phaseTwoBullFrontDistance;
        float stopDistance = phaseTwoMinimumPlayerDistance;

        Vector3 startPos = playerPos - dir * distance;
        Vector3 targetPos = playerPos - dir * stopDistance;

        float eased = progress * progress; // ??部?賹?
        Vector3 pos = Vector3.Lerp(startPos, targetPos, eased);
        Quaternion rot = Quaternion.LookRotation(dir);

        bullAI.SetPhaseTwoPose(pos, rot);
        bullAI.PlayPhaseTwoWalkLoop();
    }
    public void StartPhaseOneDirect(bool arcadeMode = true)
    {
        ResolveReferencesIfNeeded();
        ResetState();
        ConfigureArcadeRun(arcadeMode);
        bullBleedVfx?.ClearBleeds();
        phaseTwoPresentation?.ExitPhaseTwo();
        RestoreDefaultLighting();


        Time.timeScale = 1f;
        ApplySkyboxForPhase(GamePhase.PhaseOne);

        if (playerController != null)
            playerController.enabled = true;

        if (bullAI != null)
        {
            bullAI.enabled = true;
            bullAI.ResetCombatState();
        }

        if (playerStats != null)
            playerStats.SetMainMenuFrozen(false);

        playerController?.ClearInputBuffers();
        playerController?.ForceStopMovement();

        playerStats?.ResetCombatState();
        bullStats?.ResetCombatState();

        spawnManager?.ResetPlayerToSpawn();
        spawnManager?.ResetBullToSpawn();

        bullAI?.ResetCombatState();
        bullAI?.SetTutorialControl(false);

        playerStats?.SetShooterGameplayEnabled(true);

        Debug.Log("[GameFlow] Enter PhaseOne (Direct)");

        if (audioController != null)
            audioController.PlayPhaseBGM(1);
    }
    private void ApplySkyboxForPhase(GamePhase phase)
    {
        // Skybox now follows the scene Lighting settings instead of runtime overrides.
    }
    public enum TutorialState
    {
        None,
        Intro,
        Move,
        Look,
        HoldCloth,
        Capa,
        Dash,
        Attack,
        Rules,
        Complete
    }


    public enum PhaseTwoState
    {
        None,
        Intro,
        Tutorial,
        Calibration,
        Standoff,
        RoundPrepare,
        RoundWindow,
        RoundResolve
    }

    public enum EndingType
    {
        None,
        Glory,
        Tragedy,
        Mercy
    }

    [Header("References")]
    public PlayerStats playerStats;
    public BullStats bullStats;
    public BullAI bullAI;
    public BullfightPlayerController playerController;
    public BullTimingRing timingRing;
    public BullfightSpawnManager spawnManager;
    public BullfightPhaseTwoPresentation phaseTwoPresentation;
    public BullfightAudioController audioController;
    public ArduinoTest arduinoTest;

    [Header("Ending Video")]
    public VideoPlayer endingVideoPlayer;
    public VideoClip gloryEndingClip;
    public VideoClip tragedyEndingClip;
    public VideoClip mercyEndingClip;
    public float endingVideoDelay = 1.5f;
    [Range(0f, 2f)] public float endingVideoVolume = 1.5f;
    public KeyCode endingSkipKey = KeyCode.B;

    [Header("Tutorial Completion Video")]
    public VideoClip tutorialCompletionVideoClip;
    [Range(0f, 2f)] public float tutorialCompletionVideoVolume = 1f;

    [Header("Phase One To Phase Two Video")]
    public VideoClip phaseOneToPhaseTwoVideoClip;
    [Range(0f, 2f)] public float phaseOneToPhaseTwoVideoVolume = 1f;
    public KeyCode phaseOneToPhaseTwoSkipCheatKey = KeyCode.Alpha6;

    [Header("Phase Two")]
    public float phaseTwoTimeScale = 0.75f;
    public float mercyEndingDelay = 15f;
    public float introDuration = 2.8f;
    public float introQuestionDelay = 0.75f;
    public float phaseTwoTutorialMinReadDuration = 3f;
    public float calibrationHoldDuration = 5f;
    public float calibrationAnchorDuration = 1f;
    public float calibrationDecayMultiplier = 1.35f;
    public float calibrationSensorGraceDuration = 1.5f;
    public float roundStanceConfirmDuration = 0.7f;
    public float roundPrepareDuration = 0.55f;
    public float roundWindowDuration = 1.35f;
    public float roundResolveDuration = 0.85f;
    public float interRoundFaceoffDuration = 0.75f;
    public int maxRounds = 5;
    public int winsToFinish = 3;
    public float perfectTimingEase = 0.08f;
    public float perfectTelegraphBonus = 0.2f;
    public float phaseTwoBullFrontDistance = 2.2f;
    public float phaseTwoBullSideOffset = 0f;
    public float phaseTwoRoundSideOffset = 1.2f;
    [Range(0f, 1f)] public float phaseTwoSideCutChance = 0.35f;
    [Range(0.5f, 2f)] public float phaseTwoSideOffsetAmount = 1.1f;
    [Range(0f, 1f)] public float pressureApproachChance = 0.42f;
    public float pressureApproachSpeed = 1.2f;
    public float postChargePauseDuration = 0.55f;
    public float reorientDelay = 0.35f;
    public float phaseTwoResolveWalkDelay = 0.45f;
    public float phaseTwoResolveWalkDuration = 1.2f;
    public float phaseTwoResolveWalkFrontDistance = 2.55f;
    public float phaseTwoShuttleRunSpeed = 1.55f;
    public float phaseTwoMissChargeDuration = 0.85f;
    public float phaseTwoMissPauseDuration = 3f;
    public float phaseTwoAmbientIntensity = 1.0f;
    public float phaseTwoDirectionalLightMultiplier = 0.9f;
    public float phaseTwoMinimumPlayerDistance = 1.55f;
    [Range(-1f, 1f)] public float phaseTwoVisibleViewDot = 0.15f;
    [TextArea(2, 3)] public string[] phaseTwoReflectionLines =
    {
        "\u4f60\u773c\u524d\u7684\uff0c\u771f\u7684\u662f\u6575\u4eba\u55ce\uff1f",
        "\u5982\u679c\u7260\u53ea\u662f\u60f3\u6d3b\u4e0b\u53bb\uff0c\u8ab0\u53c8\u5148\u8209\u8d77\u4e86\u6b66\u5668\uff1f",
        "\u89c0\u773e\u60f3\u770b\u7684\uff0c\u662f\u52dd\u5229\uff0c\u9084\u662f\u9bae\u8840\uff1f",
        "\u9019\u4e00\u64ca\u82e5\u6210\u529f\uff0c\u4f60\u771f\u7684\u6703\u6bd4\u8f03\u8f15\u9b06\u55ce\uff1f",
        "\u5982\u679c\u4f60\u5011\u90fd\u4e0d\u51fa\u624b\uff0c\u8ab0\u624d\u6709\u8cc7\u683c\u8aaa\u9019\u5834\u6c7a\u9b25\u5fc5\u9808\u7e7c\u7e8c\uff1f"
    };

    [Header("Tutorial")]
    public float tutorialIntroDuration = 2.25f;
    public float tutorialMoveDuration = 4.1f;
    public float tutorialLookDuration = 3.9f;
    public float tutorialHoldDuration = 0.5f;
    public float tutorialTransitionDelay = 0.9f;
    public float tutorialCompleteDelay = 1.5f;
    public float tutorialRulesMinReadDuration = 3f;
    public float tutorialCapaWindowDuration = 1.35f;
    public float tutorialChargeFrontDistance = 3.6f;
    public float tutorialDashFrontDistance = 3.4f;
    public float tutorialAttackFrontDistance = 3f;
    public float tutorialAttackResolveDelay = 1.1f;
    public float tutorialBullSideOffset = 0f;
    public int tutorialHoldRequiredCount = 3;
    public int tutorialDashRequiredCount = 3;
    public int tutorialAttackRequiredCount = 3;
    public float phaseOneGroundingSnapDuration = 1.2f;

    [Header("Debug Shortcuts")]
    public bool enableDebugShortcuts = false;
    public KeyCode debugRefillStaminaKey = KeyCode.Alpha7;
    public KeyCode debugPhaseTwoKey = KeyCode.Alpha8;
    public KeyCode debugKillBullKey = KeyCode.Alpha9;
    public KeyCode debugKillPlayerKey = KeyCode.Alpha0;

    [Header("Staff Shortcuts")]
    public bool enableStaffHighScoreReset = true;
    public KeyCode staffResetDailyHighScoreKey = KeyCode.BackQuote;
    public float debugPhaseTwoDamage = 200f;
    public float bullDeathEndingDelay = 1.5f;

    public GamePhase currentPhase = GamePhase.PhaseOne;
    public EndingType currentEnding = EndingType.None;

    public PhaseTwoState CurrentPhaseTwoState => phaseTwoState;
    public TutorialState CurrentTutorialState => tutorialState;
    public int PhaseTwoRoundIndex => phaseTwoRoundIndex;
    public int PhaseTwoBullHitCount => bullHitCount;
    public int PhaseTwoPlayerHitCount => playerHitCount;
    public int PhaseTwoMaxRounds => Mathf.Max(1, maxRounds);
    public int PhaseTwoWinsToFinish => Mathf.Max(1, winsToFinish);
    public int PhaseTwoUpcomingRoundIndex => Mathf.Clamp(phaseTwoRoundIndex + 1, 1, Mathf.Max(1, maxRounds));
    public bool IsPhaseTwoCalibrated => phaseTwoCalibrated;
    public bool CurrentRoundHasPerfectAdvantage => currentRoundHasPerfectAdvantage;
    public bool NextRoundHasPerfectAdvantage => nextRoundHasPerfectAdvantage;
    public string LastPhaseTwoResult => lastPhaseTwoResult;
    public float PhaseTwoCalibrationProgress => calibrationHoldDuration <= 0f ? 1f : Mathf.Clamp01(calibrationHoldTimer / calibrationHoldDuration);
    public float PhaseTwoCurrentForce => playerController != null ? playerController.GetPhaseTwoSensorForce() : 0f;
    public float PhaseTwoCurrentForceNormalized => playerController != null ? playerController.GetPhaseTwoSensorForceNormalized() : 0f;
    public float PhaseTwoForceCap => playerController != null ? playerController.PhaseTwoForceCap : 50f;
    public float PhaseTwoCalibrationForce => playerController != null ? playerController.GetPhaseTwoSensorCalibrationForce() : 0f;
    public float PhaseTwoRoundStanceProgress => GetRoundStanceProgress();
    public float PhaseTwoMercyTimeRemaining => Mathf.Max(0f, mercyEndingDelay - mercyTimer);
    public bool IsPhaseTwoMissPauseActive => currentPhase == GamePhase.PhaseTwo &&
                                             phaseTwoState == PhaseTwoState.RoundResolve &&
                                             phaseTwoResolveWasMiss &&
                                             phaseTwoStateElapsed >= phaseTwoMissChargeDuration &&
                                             phaseTwoStateElapsed < phaseTwoMissChargeDuration + phaseTwoMissPauseDuration;
    public string CurrentPhaseTwoResolveNarration => IsPhaseTwoMissPauseActive ? phaseTwoResolveNarrationLine : string.Empty;
    public bool IsPhaseTwoRoundStanceConfirming => currentPhase == GamePhase.PhaseTwo &&
                                                   phaseTwoState == PhaseTwoState.RoundPrepare &&
                                                   phaseTwoStateElapsed < GetRoundStanceDuration();
    public string CurrentPhaseTwoReflectionLine => GetPhaseTwoReflectionLine();
    public string CurrentPhaseTwoCalibrationLine => GetPhaseTwoCalibrationLine();
    public string CurrentPhaseTwoCalibrationStatus => GetPhaseTwoCalibrationStatus();
    public string CurrentPhaseTwoTutorialInstruction => GetPhaseTwoTutorialInstruction();
    public string CurrentPhaseTwoStandoffInstruction => GetPhaseTwoStandoffInstruction();
    public bool IsPhaseTwoTutorialAdvanceReady => currentPhase == GamePhase.PhaseTwo &&
                                                  phaseTwoState == PhaseTwoState.Tutorial &&
                                                  phaseTwoStateElapsed >= phaseTwoTutorialMinReadDuration;
    public float PhaseTwoTutorialSecondsRemaining => currentPhase == GamePhase.PhaseTwo &&
                                                     phaseTwoState == PhaseTwoState.Tutorial
        ? Mathf.Max(0f, phaseTwoTutorialMinReadDuration - phaseTwoStateElapsed)
        : 0f;
    public bool IsPhaseTwoQuestionVisible => currentPhase == GamePhase.PhaseTwo &&
                                            phaseTwoState == PhaseTwoState.Intro &&
                                            phaseTwoStateElapsed >= introQuestionDelay;
    public bool IsTutorialActive => currentPhase == GamePhase.PhaseZeroTutorial;
    public bool IsTutorialCompletionVideoPlaybackActive => tutorialCompletionVideoPlaybackActive;
    public bool IsTutorialCapaStepActive => currentPhase == GamePhase.PhaseZeroTutorial && tutorialState == TutorialState.Capa;
    public bool IsTutorialAttackStepActive => currentPhase == GamePhase.PhaseZeroTutorial && tutorialState == TutorialState.Attack;
    public bool IsTutorialRulesStep => currentPhase == GamePhase.PhaseZeroTutorial && tutorialState == TutorialState.Rules;
    public string CurrentTutorialTitle => GetTutorialTitle();
    public string CurrentTutorialInstruction => GetTutorialInstruction();
    public string CurrentTutorialStatus => GetTutorialStatus();
    public string CurrentTutorialBody => GetTutorialBody();

    private PhaseTwoState phaseTwoState = PhaseTwoState.None;
    private TutorialState tutorialState = TutorialState.None;
    private TutorialState queuedTutorialState = TutorialState.None;
    private BullBleedVfx bullBleedVfx;
    private float mercyTimer;
    private float bullDeathTimer;
    private bool isEnteringPhaseTwo;
    private float phaseTwoStateElapsed;
    private float calibrationHoldTimer;
    private float calibrationSignalWaitTimer;
    private float calibrationAnchorTimer;
    private float calibrationAnchorValue;
    private float activeRoundWindowDuration;
    private float tutorialStateElapsed;
    private float tutorialHoldTimer;
    private float tutorialTransitionTimer;
    private float tutorialCapaWindowTimer;
    private float tutorialDashStepStartedAt = -999f;
    private float tutorialAttackResolveAt = -1f;
    private float tutorialAttackHealthAtStepStart;
    private float tutorialMoveProgress;
    private float tutorialLookProgress;
    private float phaseOneGroundingSnapTimer;
    private int bullHitCount;
    private int playerHitCount;
    private int phaseTwoRoundIndex;
    private int tutorialHoldSuccessCount;
    private int tutorialDashSuccessCount;
    private int tutorialAttackSuccessCount;
    private bool phaseTwoCalibrated;
    private bool phaseTwoCalibrationUsingSensor;
    private bool phaseTwoCalibrationAnchorLocked;
    private bool phaseTwoHasCommittedAttack;
    private bool nextRoundHasPerfectAdvantage;
    private bool currentRoundHasPerfectAdvantage;
    private int phaseTwoRoundSideSign = 1;
    private int phaseTwoApproachSideSign = 1;
    private bool phaseTwoUseSideCut;
    private bool phaseTwoUsePressureApproach;
    private bool phaseTwoPrepareTelegraphStarted;
    private bool phaseTwoResolveBullResetApplied;
    private bool phaseTwoResolveWalkInitialized;
    private bool phaseTwoResolveWasMiss;
    private bool phaseTwoResolveMissImpactApplied;
    private bool phaseTwoResolveMissChargeStarted;
    private bool phaseTwoResolveCompleted;
    private bool phaseTwoAutoStartRoundAfterCalibration;
    private bool phaseTwoTutorialShown;
    private int phaseTwoResolveShuttleSegment;
    private bool tutorialChargeStarted;
    private bool tutorialCapaQteStarted;
    private bool tutorialCapaQteResolved;
    private bool tutorialHoldRegisteredThisAttempt;
    private bool tutorialAttackPerformed;
    private bool tutorialAttackCleanHitRegistered;
    private string lastPhaseTwoResult = string.Empty;
    private string tutorialFeedbackText = string.Empty;
    private string phaseTwoResolveNarrationLine = string.Empty;
    private string phaseTwoCalibrationStatusText = string.Empty;
    private Vector3 phaseTwoResolveCenterPoint;
    private Vector3 phaseTwoResolveLeftPoint;
    private Vector3 phaseTwoResolveRightPoint;
    private PlayerStats subscribedTutorialPlayerStats;
    private BullAI subscribedTutorialBullAI;
    private RectTransform endingSkipRoot;
    private Button endingSkipButton;
    private Text endingSkipLabel;
    private Text endingSkipForceLabel;
    private Image endingSkipForceBarBackground;
    private Image endingSkipForceBarFill;
    private RectTransform endingSkipForceBarFillRect;
    private RectTransform tutorialCompletionSkipRoot;
    private Text tutorialCompletionSkipLabel;
    private bool tutorialCompletionVideoPlaybackActive;
    private bool phaseOneToPhaseTwoVideoPlaybackActive;
    private bool endingVideoPlaybackActive;
    private float endingVideoStartDelayRemaining = -1f;
    private bool tutorialCompletionVideoFreezeStateCaptured;
    private float tutorialCompletionVideoSavedTimeScale = 1f;
    private float tutorialCompletionVideoSavedFixedDeltaTime = 0.02f;
    private bool tutorialCompletionVideoSavedPlayerControllerEnabled;
    private bool tutorialCompletionVideoSavedBullAiEnabled;
    private bool tutorialCompletionVideoSavedCameraLookEnabled;
    private CameraLook tutorialCompletionVideoCameraLook;
    private readonly System.Collections.Generic.List<GameObject> tutorialCompletionHiddenUiRoots = new System.Collections.Generic.List<GameObject>();
    private BullDebugOverlay endingHiddenDebugOverlay;
    private readonly System.Collections.Generic.List<GameObject> endingHiddenRootObjects = new System.Collections.Generic.List<GameObject>();
    private readonly System.Collections.Generic.List<Renderer> endingHiddenRenderers = new System.Collections.Generic.List<Renderer>();
    private readonly System.Collections.Generic.List<Canvas> endingHiddenCanvases = new System.Collections.Generic.List<Canvas>();

    private void ResetTimeScale()
    {
        Time.timeScale = phaseTwoTimeScale;
    }
    private void Awake()
    {
        if (audioController == null)
        {
            GameObject musicObject = GameObject.Find("Music");
            if (musicObject != null)
                audioController = musicObject.GetComponent<BullfightAudioController>();
        }
        ResetState();

        if (FindObjectOfType<ManualStartMenuController>(true) == null &&
            FindObjectOfType<BullfightStartMenu>(true) == null)
        {
            GameObject startMenuObject = new("BullfightStartMenu");
            _ = startMenuObject.AddComponent<BullfightStartMenu>();
        }

        ResolveReferencesIfNeeded();
        RefreshTutorialSubscriptions();

        if (bullAI != null)
            bullAI.enabled = true;
    }

    private void Start()
    {
        ResetState();
        ResolveReferencesIfNeeded();
        RefreshTutorialSubscriptions();
    }

    private void Update()
    {
        if (HasMissingReferences())
        {
            ResolveReferencesIfNeeded();
            RefreshTutorialSubscriptions();
        }

        HandleDebugShortcuts();
        if (UpdateArcadeSequences())
            return;

        if (tutorialCompletionVideoPlaybackActive)
        {
            UpdateTutorialCompletionVideoPlayback();
            return;
        }

        if (phaseOneToPhaseTwoVideoPlaybackActive)
        {
            UpdatePhaseOneToPhaseTwoVideoPlayback();
            return;
        }

        if (currentPhase == GamePhase.PhaseZeroTutorial)
        {
            UpdateTutorial();
            return;
        }

        UpdateEndingSkipUiVisibility();

        if (currentPhase == GamePhase.Ending)
        {
            UpdateEndingVideoPlayback();
            return;
        }

        if (playerStats != null && playerStats.IsDead)
        {
            SetEnding(EndingType.Tragedy);
            return;
        }

        if (bullStats != null && bullStats.currentHealth <= 0f)
        {
            if (currentPhase == GamePhase.PhaseOne)
            {
                if (IsArcadePhaseClearSequenceActive)
                    return;

                if (isEnteringPhaseTwo)
                {
                    BeginPhaseTwoEntrySequence();
                    return;
                }

                if (!TryBeginArcadePhaseOneClearSequence())
                    isEnteringPhaseTwo = true;

                return;
            }
            else if (!isEnteringPhaseTwo)
            {
                bullAI?.ForceDeathState();
                bullDeathTimer += Time.unscaledDeltaTime;

                if (bullDeathTimer >= bullDeathEndingDelay)
                    SetEnding(EndingType.Glory);

                return;
            }
        }

        if (currentPhase == GamePhase.PhaseOne &&
            !isEnteringPhaseTwo &&
            bullStats != null &&
            bullStats.currentHealth > 0f &&
            arcadeScoring != null &&
            arcadeScoring.IsPhaseOneTimeExpired)
        {
            if (!TryBeginArcadePhaseOneTimeUpSequence())
                isEnteringPhaseTwo = true;

            return;
        }

        if (isEnteringPhaseTwo)
        {
            BeginPhaseTwoEntrySequence();
            return;
        }

        bullDeathTimer = 0f;

        switch (currentPhase)
        {
            case GamePhase.PhaseOne:
                UpdatePhaseOne();
                break;
            case GamePhase.PhaseTwo:
                UpdatePhaseTwo();
                break;
        }
    }

    public void RegisterStabResult(string result)
    {
        if (currentPhase != GamePhase.PhaseTwo || phaseTwoState != PhaseTwoState.RoundWindow)
            return;

        HandlePhaseTwoStabResult(result);
    }

    public void BeginTutorial()
    {
        ResolveReferencesIfNeeded();
        ResetState();
        ConfigureArcadeRun(true);
        bullBleedVfx?.ClearBleeds();

        if (playerController != null)
            playerController.enabled = true;

        if (bullAI != null)
            bullAI.enabled = true;

        if (playerStats != null)
            playerStats.SetMainMenuFrozen(false);

        PrepareEncounterForTutorial(tutorialChargeFrontDistance);
        tutorialFeedbackText = string.Empty;
        Debug.Log("[GameFlow] BeginTutorial");
        if (audioController != null) audioController.PlayPhaseBGM(0);
        EnterTutorialState(TutorialState.Intro);
    }

    public void SetMainMenuGameplayLocked(bool locked)
    {
        ResolveReferencesIfNeeded();

        if (locked)
        {
            playerController?.ClearInputBuffers();
            playerController?.ForceStopMovement();
            if (playerController != null)
                playerController.enabled = false;

            if (playerStats != null)
                playerStats.SetMainMenuFrozen(true);

            if (bullAI != null)
            {
                bullAI.SetTutorialControl(false);
                bullAI.enabled = false;
            }

            if (bullStats != null)
                bullStats.ResetCombatState();
        }
        else
        {
            if (playerStats != null)
                playerStats.SetMainMenuFrozen(false);

            if (playerController != null)
                playerController.enabled = true;

            if (bullAI != null)
                bullAI.enabled = true;
        }
    }

    public float GetPhaseTwoBullHealthNormalized()
    {
        return 1f - (bullHitCount / (float)Mathf.Max(1, winsToFinish));
    }

    public float GetPhaseTwoPlayerHealthNormalized()
    {
        return 1f - (playerHitCount / (float)Mathf.Max(1, winsToFinish));
    }

    public bool ShouldShowPhaseTwoOverlay()
    {
        return currentPhase == GamePhase.PhaseTwo && currentEnding == EndingType.None;
    }

    public bool ShouldShowTutorialOverlay()
    {
        return currentPhase == GamePhase.PhaseZeroTutorial &&
               currentEnding == EndingType.None &&
               !tutorialCompletionVideoPlaybackActive;
    }

    private void UpdateTutorial()
    {
        if (tutorialTransitionTimer > 0f)
        {
            tutorialTransitionTimer = Mathf.Max(0f, tutorialTransitionTimer - Time.unscaledDeltaTime);
            if (tutorialTransitionTimer <= 0f)
                EnterTutorialState(queuedTutorialState);
            return;
        }

        tutorialStateElapsed += Time.unscaledDeltaTime;

        switch (tutorialState)
        {
            case TutorialState.Intro:
                if (tutorialStateElapsed >= tutorialIntroDuration)
                    EnterTutorialState(TutorialState.Move);
                break;
            case TutorialState.Move:
                UpdateTutorialMove();
                break;
            case TutorialState.Look:
                UpdateTutorialLook();
                break;
            case TutorialState.HoldCloth:
                UpdateTutorialHoldCloth();
                break;
            case TutorialState.Capa:
                UpdateTutorialCapa();
                break;
            case TutorialState.Dash:
                UpdateTutorialDash();
                break;
            case TutorialState.Attack:
                UpdateTutorialAttack();
                break;
            case TutorialState.Rules:
                UpdateTutorialRules();
                break;
            case TutorialState.Complete:
                if (tutorialStateElapsed >= tutorialCompleteDelay)
                    StartPhaseOneFromTutorial();
                break;
        }
    }

    private void UpdateTutorialMove()
    {
        if (playerController == null)
            return;

        tutorialMoveProgress += playerController.GetMovementInputMagnitude() >= 0.35f
            ? Time.unscaledDeltaTime
            : -Time.unscaledDeltaTime * 0.35f;
        tutorialMoveProgress = Mathf.Clamp(tutorialMoveProgress, 0f, tutorialMoveDuration);

        if (tutorialMoveProgress >= tutorialMoveDuration)
            QueueTutorialState(TutorialState.Look, 0.25f, "\u79fb\u52d5\u8a13\u7df4\u5b8c\u6210\uff0c\u63a5\u4e0b\u4f86\u8acb\u8f49\u52d5\u8996\u89d2\u89c0\u5bdf\u9b25\u725b\u5834\u3002");
    }

    private void UpdateTutorialLook()
    {
        if (playerController == null)
            return;

        tutorialLookProgress += playerController.GetLookInputMagnitude() >= 0.18f
            ? Time.unscaledDeltaTime
            : -Time.unscaledDeltaTime * 0.35f;
        tutorialLookProgress = Mathf.Clamp(tutorialLookProgress, 0f, tutorialLookDuration);

        if (tutorialLookProgress >= tutorialLookDuration)
            QueueTutorialState(TutorialState.HoldCloth, 0.25f, "\u8996\u89d2\u8a13\u7df4\u5b8c\u6210\uff0c\u73fe\u5728\u958b\u59cb\u6301\u5e03\u6559\u5b78\u3002");
    }

    private void UpdateTutorialHoldCloth()
    {
        if (playerStats == null)
            return;

        if (playerStats.isHoldingCloth)
        {
            tutorialHoldTimer += Time.unscaledDeltaTime;
            if (!tutorialHoldRegisteredThisAttempt && tutorialHoldTimer >= tutorialHoldDuration)
            {
                tutorialHoldRegisteredThisAttempt = true;
                tutorialHoldSuccessCount++;
            }
        }
        else
        {
            tutorialHoldTimer = 0f;
            tutorialHoldRegisteredThisAttempt = false;
        }

        if (tutorialHoldSuccessCount >= Mathf.Max(1, tutorialHoldRequiredCount))
            QueueTutorialState(TutorialState.Capa, 0.4f, "\u5f88\u597d\uff0c\u63a5\u4e0b\u4f86\u8981\u4ee5 Perfect \u5b8c\u6210\u4e00\u6b21\u63ee\u5e03\u3002");
    }

    private void UpdateTutorialCapa()
    {
        if (!tutorialChargeStarted)
        {
            PrepareEncounterForTutorial(tutorialChargeFrontDistance);
            tutorialChargeStarted = true;
            ResetTutorialCapaQteState();
            bullAI?.StartTutorialCharge(true, false);
        }

        if (!tutorialCapaQteStarted &&
            bullAI != null &&
            bullAI.CanReceiveChargeTimingInput)
        {
            ResetTutorialCapaQteState();
            tutorialCapaQteStarted = true;
            timingRing?.SetTimingWindow(0.5f, 0.7f);
            timingRing?.StartTiming(BullTimingRing.TimingMode.Capa, HandleTutorialCapaTimingResult);
            timingRing?.SetTelegraphProgress(0f);
        }

        if (tutorialCapaQteStarted && !tutorialCapaQteResolved)
        {
            tutorialCapaWindowTimer += Time.unscaledDeltaTime;
            float progress = tutorialCapaWindowDuration <= 0.0001f
                ? 1f
                : Mathf.Clamp01(tutorialCapaWindowTimer / tutorialCapaWindowDuration);
            timingRing?.SetTelegraphProgress(progress);

            if (tutorialCapaWindowTimer >= tutorialCapaWindowDuration && timingRing != null && timingRing.IsActive)
                timingRing.ResolveExternal("Miss");
        }

        if (bullAI == null || !bullAI.IsTutorialChargeSequenceComplete)
            return;

        bool success = bullAI.TutorialChargeResult == "Perfect!";
        ResetTutorialCapaQteState();
        if (success)
        {
            QueueTutorialState(TutorialState.Dash, tutorialTransitionDelay, "\u63ee\u5e03 Perfect\uff0c\u73fe\u5728\u9023\u7e8c\u5b8c\u6210 3 \u6b21\u9583\u907f\u3002");
            return;
        }

        QueueTutorialState(TutorialState.Capa, tutorialTransitionDelay, "\u9019\u6b21\u4e0d\u662f Perfect\uff0c\u518d\u4f86\u4e00\u6b21\u3002");
    }

    private void UpdateTutorialDash()
    {
        if (!tutorialChargeStarted)
        {
            PrepareEncounterForTutorial(tutorialDashFrontDistance);
            tutorialChargeStarted = true;
            tutorialDashStepStartedAt = Time.time;
            bullAI?.StartTutorialCharge(false, false);
        }

        if (bullAI == null || !bullAI.IsTutorialChargeSequenceComplete)
            return;

        bool dashedThisAttempt = playerStats != null && playerStats.LastDashTime > tutorialDashStepStartedAt;
        bool success = dashedThisAttempt && !bullAI.DidTutorialChargeHitPlayer;
        if (success)
        {
            tutorialDashSuccessCount++;
            if (tutorialDashSuccessCount >= Mathf.Max(1, tutorialDashRequiredCount))
            {
                QueueTutorialState(TutorialState.Attack, tutorialTransitionDelay, "\u9583\u907f\u5b8c\u6210\uff0c\u73fe\u5728\u9023\u7e8c 3 \u6b21\u6210\u529f\u653b\u64ca\u3002");
                return;
            }

            QueueTutorialState(TutorialState.Dash, tutorialTransitionDelay, $"\u9583\u907f\u6210\u529f {tutorialDashSuccessCount}/{Mathf.Max(1, tutorialDashRequiredCount)}\uff0c\u518d\u4f86\u4e00\u6b21\u3002");
            return;
        }

        QueueTutorialState(TutorialState.Dash, tutorialTransitionDelay, "\u9583\u907f\u6642\u6a5f\u4e0d\u5c0d\uff0c\u518d\u4f86\u4e00\u6b21\u3002");
    }

    private void UpdateTutorialAttack()
    {
        if (!tutorialChargeStarted)
        {
            PrepareEncounterForTutorial(tutorialAttackFrontDistance);
            tutorialChargeStarted = true;
            tutorialAttackHealthAtStepStart = bullStats != null ? bullStats.currentHealth : 0f;
            tutorialAttackResolveAt = -1f;
            tutorialAttackPerformed = false;
            tutorialAttackCleanHitRegistered = false;
            bullAI?.EnterTutorialIdle();
        }

        if (tutorialAttackCleanHitRegistered ||
            (bullStats != null && bullStats.currentHealth < tutorialAttackHealthAtStepStart - 0.01f))
        {
            tutorialAttackSuccessCount++;
            if (tutorialAttackSuccessCount >= Mathf.Max(1, tutorialAttackRequiredCount))
            {
                QueueTutorialState(TutorialState.Complete, tutorialTransitionDelay, "\u57fa\u790e\u64cd\u4f5c\u5b8c\u6210\uff0c\u6e96\u5099\u9032\u5165\u6b63\u5f0f\u6230\u9b25\u3002");
                return;
            }

            QueueTutorialState(TutorialState.Attack, tutorialTransitionDelay, $"\u653b\u64ca\u6210\u529f {tutorialAttackSuccessCount}/{Mathf.Max(1, tutorialAttackRequiredCount)}\uff0c\u518d\u88dc\u4e00\u64ca\u3002");
            return;
        }

        if (!tutorialAttackPerformed &&
            tutorialAttackResolveAt < 0f &&
            ((playerController != null && playerController.IsAttackPressedThisFrame()) || Input.GetKeyDown(KeyCode.F)))
        {
            tutorialAttackResolveAt = Time.time + tutorialAttackResolveDelay;
        }

        if (tutorialAttackResolveAt >= 0f && Time.time >= tutorialAttackResolveAt)
            QueueTutorialState(TutorialState.Attack, tutorialTransitionDelay, "\u8981\u5728\u6709\u6548\u8ddd\u96e2\u5167\u6210\u529f\u547d\u4e2d\uff0c\u518d\u8a66\u4e00\u6b21\u3002");
    }

    private void UpdateTutorialRules()
    {
        if (tutorialStateElapsed < tutorialRulesMinReadDuration)
            return;

        if (playerController != null && playerController.WasTutorialAdvancePressedThisFrame())
            QueueTutorialState(TutorialState.Complete, 0.2f, "\u6e96\u5099\u9032\u5165\u6b63\u5f0f\u6230\u9b25\u3002");
    }

    private void QueueTutorialState(TutorialState nextState, float delay, string feedback)
    {
        queuedTutorialState = nextState;
        tutorialTransitionTimer = Mathf.Max(0f, delay);
        tutorialFeedbackText = feedback ?? string.Empty;

        if (tutorialTransitionTimer <= 0f)
            EnterTutorialState(nextState);
    }

    private void EnterTutorialState(TutorialState nextState)
    {
        queuedTutorialState = TutorialState.None;
        tutorialTransitionTimer = 0f;
        tutorialState = nextState;
        tutorialStateElapsed = 0f;
        tutorialHoldTimer = 0f;
        tutorialMoveProgress = 0f;
        tutorialLookProgress = 0f;
        tutorialChargeStarted = false;
        ResetTutorialCapaQteState();
        tutorialHoldRegisteredThisAttempt = false;
        tutorialDashStepStartedAt = -999f;
        tutorialAttackResolveAt = -1f;
        tutorialAttackPerformed = false;
        tutorialAttackCleanHitRegistered = false;
        currentPhase = GamePhase.PhaseZeroTutorial;
        currentEnding = EndingType.None;

        bullAI?.SetTutorialControl(true);

        if (playerStats != null)
        {
            playerStats.SetHoldingCloth(false);
            playerStats.SetShooterGameplayEnabled(true);
        }

        playerController?.ClearInputBuffers();

        if (nextState == TutorialState.Complete)
            PrepareEncounterForTutorial(tutorialAttackFrontDistance);
    }

    private void PrepareEncounterForTutorial(float bullDistance)
    {
        ResolveReferencesIfNeeded();
        Time.timeScale = 1f;
        ApplySkyboxForPhase(GamePhase.PhaseOne);
        phaseTwoPresentation?.ExitPhaseTwo();
        playerStats?.ResetCombatState();
        playerStats?.SetShooterGameplayEnabled(true);
        bullStats?.ResetCombatState();
        playerController?.ClearInputBuffers();
        ResetTutorialCapaQteState();
        spawnManager?.ResetPlayerAndBullForTutorial(bullDistance, tutorialBullSideOffset);
        bullAI?.ResetCombatState();
        bullAI?.SetTutorialControl(true);
        bullAI?.EnterTutorialIdle();
    }

    private void StartPhaseOneFromTutorial()
    {
        if (TryBeginTutorialCompletionVideoPlayback())
            return;

        EnterPhaseOneFromTutorialImmediate();
    }

    private void BeginPhaseTwoEntrySequence()
    {
        if (TryBeginPhaseOneToPhaseTwoVideoPlayback())
            return;

        EnterPhaseTwo();
    }

    private bool TryBeginTutorialCompletionVideoPlayback()
    {
        if (tutorialCompletionVideoPlaybackActive)
            return true;

        if (endingVideoPlayer == null || tutorialCompletionVideoClip == null)
            return false;

        tutorialCompletionVideoPlaybackActive = true;
        LockGameplayForTutorialCompletionVideo();
        ShowEndingSkipUi(false);
        EnsureTutorialCompletionSkipUi();
        ShowTutorialCompletionSkipUi(true);

        endingVideoPlayer.loopPointReached -= HandleEndingVideoCompleted;
        endingVideoPlayer.loopPointReached -= HandleTutorialCompletionVideoCompleted;
        endingVideoPlayer.loopPointReached += HandleTutorialCompletionVideoCompleted;
        endingVideoPlayer.isLooping = false;
        endingVideoPlayer.playOnAwake = false;
        endingVideoPlayer.waitForFirstFrame = true;
        endingVideoPlayer.skipOnDrop = false;
        endingVideoPlayer.clip = tutorialCompletionVideoClip;
        ConfigureVideoAudio(tutorialCompletionVideoClip, tutorialCompletionVideoVolume);
        audioController?.StopAllAudio();

        if (endingVideoPlayer.gameObject != null)
            endingVideoPlayer.gameObject.SetActive(true);

        endingVideoPlayer.Stop();
        endingVideoPlayer.Play();
        return true;
    }

    private bool TryBeginPhaseOneToPhaseTwoVideoPlayback()
    {
        if (phaseOneToPhaseTwoVideoPlaybackActive)
            return true;

        if (endingVideoPlayer == null || phaseOneToPhaseTwoVideoClip == null)
            return false;

        phaseOneToPhaseTwoVideoPlaybackActive = true;
        LockGameplayForTutorialCompletionVideo();
        ShowEndingSkipUi(false);
        ShowTutorialCompletionSkipUi(false);

        endingVideoPlayer.loopPointReached -= HandleEndingVideoCompleted;
        endingVideoPlayer.loopPointReached -= HandleTutorialCompletionVideoCompleted;
        endingVideoPlayer.loopPointReached -= HandlePhaseOneToPhaseTwoVideoCompleted;
        endingVideoPlayer.loopPointReached += HandlePhaseOneToPhaseTwoVideoCompleted;
        endingVideoPlayer.isLooping = false;
        endingVideoPlayer.playOnAwake = false;
        endingVideoPlayer.waitForFirstFrame = true;
        endingVideoPlayer.skipOnDrop = false;
        endingVideoPlayer.clip = phaseOneToPhaseTwoVideoClip;
        ConfigureVideoAudio(phaseOneToPhaseTwoVideoClip, phaseOneToPhaseTwoVideoVolume);
        audioController?.StopAllAudio();

        if (endingVideoPlayer.gameObject != null)
            endingVideoPlayer.gameObject.SetActive(true);

        endingVideoPlayer.Stop();
        endingVideoPlayer.Play();
        return true;
    }

    private void LockGameplayForTutorialCompletionVideo()
    {
        ResolveReferencesIfNeeded();
        tutorialCompletionVideoCameraLook = ResolveTutorialCompletionVideoCameraLook();
        tutorialCompletionVideoSavedTimeScale = Time.timeScale;
        tutorialCompletionVideoSavedFixedDeltaTime = Time.fixedDeltaTime;
        tutorialCompletionVideoSavedPlayerControllerEnabled = playerController != null && playerController.enabled;
        tutorialCompletionVideoSavedBullAiEnabled = bullAI != null && bullAI.enabled;
        tutorialCompletionVideoSavedCameraLookEnabled = tutorialCompletionVideoCameraLook != null && tutorialCompletionVideoCameraLook.enabled;
        tutorialCompletionVideoFreezeStateCaptured = true;

        playerController?.ClearInputBuffers();
        playerController?.ForceStopMovement();

        if (tutorialCompletionVideoCameraLook != null)
            tutorialCompletionVideoCameraLook.enabled = false;

        if (playerController != null)
            playerController.enabled = false;

        if (bullAI != null)
        {
            bullAI.SetTutorialControl(false);
            bullAI.enabled = false;
        }

        if (playerStats != null)
        {
            playerStats.SetHoldingCloth(false);
            playerStats.SetShooterGameplayEnabled(false);
        }

        if (timingRing != null)
        {
            timingRing.HideImmediate();
            timingRing.ResetTimingWindow();
        }

        HideTutorialCompletionVideoUi();
        Time.timeScale = 0f;
        Time.fixedDeltaTime = 0f;
    }

    private void UpdateTutorialCompletionVideoPlayback()
    {
        if (IsTutorialCompletionVideoSkipPressedThisFrame())
        {
            SkipTutorialCompletionVideo();
            return;
        }

        if (tutorialCompletionVideoPlaybackActive && endingVideoPlayer == null)
            FinishTutorialCompletionVideoPlayback();
    }

    private void UpdatePhaseOneToPhaseTwoVideoPlayback()
    {
        if (IsPhaseOneToPhaseTwoVideoSkipCheatPressedThisFrame())
        {
            SkipPhaseOneToPhaseTwoVideo();
            return;
        }

        if (phaseOneToPhaseTwoVideoPlaybackActive && endingVideoPlayer == null)
            FinishPhaseOneToPhaseTwoVideoPlayback();
    }

    private void SkipTutorialCompletionVideo()
    {
        if (!tutorialCompletionVideoPlaybackActive)
            return;

        FinishTutorialCompletionVideoPlayback();
    }

    private void SkipPhaseOneToPhaseTwoVideo()
    {
        if (!phaseOneToPhaseTwoVideoPlaybackActive)
            return;

        FinishPhaseOneToPhaseTwoVideoPlayback();
    }

    private void FinishTutorialCompletionVideoPlayback()
    {
        StopTutorialCompletionVideoPlayback();
        EnterPhaseOneFromTutorialImmediate();
    }

    private void FinishPhaseOneToPhaseTwoVideoPlayback()
    {
        StopPhaseOneToPhaseTwoVideoPlayback();
        EnterPhaseTwo();
    }

    private void StopTutorialCompletionVideoPlayback()
    {
        if (!tutorialCompletionVideoPlaybackActive)
            return;

        tutorialCompletionVideoPlaybackActive = false;
        ShowTutorialCompletionSkipUi(false);

        if (endingVideoPlayer != null)
        {
            endingVideoPlayer.loopPointReached -= HandleTutorialCompletionVideoCompleted;
            endingVideoPlayer.Stop();

            if (endingVideoPlayer.gameObject != null)
                endingVideoPlayer.gameObject.SetActive(false);
        }

        RestoreTutorialCompletionVideoFreezeState();
    }

    private void StopPhaseOneToPhaseTwoVideoPlayback()
    {
        if (!phaseOneToPhaseTwoVideoPlaybackActive)
            return;

        phaseOneToPhaseTwoVideoPlaybackActive = false;
        ShowTutorialCompletionSkipUi(false);

        if (endingVideoPlayer != null)
        {
            endingVideoPlayer.loopPointReached -= HandlePhaseOneToPhaseTwoVideoCompleted;
            endingVideoPlayer.Stop();

            if (endingVideoPlayer.gameObject != null)
                endingVideoPlayer.gameObject.SetActive(false);
        }

        RestoreTutorialCompletionVideoFreezeState();
    }

    private void HandleTutorialCompletionVideoCompleted(VideoPlayer source)
    {
        if (source != null)
            source.loopPointReached -= HandleTutorialCompletionVideoCompleted;

        FinishTutorialCompletionVideoPlayback();
    }

    private void HandlePhaseOneToPhaseTwoVideoCompleted(VideoPlayer source)
    {
        if (source != null)
            source.loopPointReached -= HandlePhaseOneToPhaseTwoVideoCompleted;

        FinishPhaseOneToPhaseTwoVideoPlayback();
    }

    private void EnterPhaseOneFromTutorialImmediate()
    {
        ResolveReferencesIfNeeded();
        ConfigureArcadeRun(true);
        tutorialFeedbackText = string.Empty;
        currentEnding = EndingType.None;
        tutorialState = TutorialState.None;
        queuedTutorialState = TutorialState.None;
        tutorialTransitionTimer = 0f;
        tutorialStateElapsed = 0f;
        tutorialHoldTimer = 0f;
        tutorialChargeStarted = false;
        ResetTutorialCapaQteState();
        tutorialAttackPerformed = false;
        tutorialAttackCleanHitRegistered = false;
        Time.timeScale = 1f;
        ApplySkyboxForPhase(GamePhase.PhaseOne);
        phaseTwoPresentation?.ExitPhaseTwo();
        playerStats?.SetMainMenuFrozen(false);
        playerStats?.ResetCombatState();
        playerStats?.SetShooterGameplayEnabled(true);
        bullStats?.ResetCombatState();
        playerController?.ClearInputBuffers();
        if (playerController != null)
            playerController.enabled = true;
        if (bullAI != null)
            bullAI.enabled = true;
        bullAI?.SetTutorialControl(false);
        spawnManager?.ResetPlayerToSpawn();
        spawnManager?.ResetBullToSpawn();
        bullAI?.ResetCombatState();
        bullBleedVfx?.ClearBleeds();
        currentPhase = GamePhase.PhaseOne;
        Debug.Log("[GameFlow] Enter PhaseOne");
        if (audioController != null) audioController.PlayPhaseBGM(1);
        phaseOneGroundingSnapTimer = Mathf.Max(0f, phaseOneGroundingSnapDuration);
    }

    private string GetTutorialTitle()
    {
        return tutorialState switch
        {
            TutorialState.Intro => "\u7b2c0\u968e\u6bb5\uff1a\u65b0\u624b\u6559\u5b78",
            TutorialState.Move => "\u7b2c0\u968e\u6bb5\uff1a\u79fb\u52d5",
            TutorialState.Look => "\u7b2c0\u968e\u6bb5\uff1a\u8f49\u52d5\u8996\u89d2",
            TutorialState.HoldCloth => "\u7b2c0\u968e\u6bb5\uff1a\u6301\u5e03",
            TutorialState.Capa => "\u7b2c0\u968e\u6bb5\uff1a\u63ee\u5e03",
            TutorialState.Dash => "\u7b2c0\u968e\u6bb5\uff1a\u9583\u907f",
            TutorialState.Attack => "\u7b2c0\u968e\u6bb5\uff1a\u653b\u64ca",
            TutorialState.Rules => "\u4e0a\u5834\u524d\u898f\u5247\u8aaa\u660e",
            TutorialState.Complete => "\u6559\u5b78\u5b8c\u6210",
            _ => string.Empty
        };
    }

    private string GetTutorialInstruction()
    {
        return tutorialState switch
        {
            TutorialState.Intro => $"\u5148\u8a8d\u8b58\u57fa\u790e\u64cd\u4f5c\uff1a{GetMoveLabel()} \u79fb\u52d5\uff0c{GetLookLabel()} \u8f49\u8996\u89d2\uff0c{GetHoldLabel()} \u6301\u5e03\uff0c{GetSwingLabel()} \u63ee\u5e03\uff0c{GetDashLabel()} \u9583\u907f\uff0c{GetAttackLabel()} \u653b\u64ca\u3002",
            TutorialState.Move => $"\u8acb\u4f7f\u7528 {GetMoveLabel()} \u79fb\u52d5\u9b25\u725b\u58eb\u3002",
            TutorialState.Look => $"\u8acb\u4f7f\u7528 {GetLookLabel()} \u89c0\u5bdf\u9b25\u725b\u5834\u8207\u725b\u7684\u4f4d\u7f6e\u3002",
            TutorialState.HoldCloth => $"\u4f7f\u7528 {GetHoldLabel()} \u9032\u5165\u6301\u5e03\uff0c\u96e2\u958b\u5f8c\u6703\u653e\u4e0b\uff0c\u9023\u7e8c\u5b8c\u6210 3 \u6b21\u3002",
            TutorialState.Capa => $"\u5148\u4f7f\u7528 {GetHoldLabel()} \u9032\u5165\u6301\u5e03\uff0c\u518d\u5728 QTE \u74b0\u7e2e\u8fd1\u6642\u6309 {GetSwingLabel()} \uff0c\u4e26\u62ff\u5230 Perfect\u3002",
            TutorialState.Dash => $"\u7576\u725b\u76f4\u885d\u904e\u4f86\u6642\uff0c\u6309 {GetDashLabel()} \u9023\u7e8c\u5b8c\u6210 3 \u6b21\u6210\u529f\u9583\u907f\u3002",
            TutorialState.Attack => $"\u9760\u8fd1\u725b\u5f8c\u6309 {GetAttackLabel()} \u9032\u884c\u653b\u64ca\uff0c\u9023\u7e8c\u5b8c\u6210 3 \u6b21\u6709\u6548\u547d\u4e2d\u3002",
            TutorialState.Rules => "\u6e96\u5099\u9032\u5165\u6b63\u5f0f\u6230\u9b25\u3002",
            TutorialState.Complete => "\u5373\u5c07\u9032\u5165\u6b63\u5f0f\u7684\u7b2c\u4e00\u968e\u6bb5\u3002",
            _ => string.Empty
        };
    }

    private string GetTutorialStatus()
    {
        if (!string.IsNullOrWhiteSpace(tutorialFeedbackText) && tutorialTransitionTimer > 0f)
            return tutorialFeedbackText;

        return tutorialState switch
        {
            TutorialState.Intro => "\u6e96\u5099\u958b\u59cb",
            TutorialState.Move => $"{Mathf.RoundToInt(Mathf.Clamp01(tutorialMoveProgress / Mathf.Max(0.01f, tutorialMoveDuration)) * 100f)}%",
            TutorialState.Look => $"{Mathf.RoundToInt(Mathf.Clamp01(tutorialLookProgress / Mathf.Max(0.01f, tutorialLookDuration)) * 100f)}%",
            TutorialState.HoldCloth => $"{tutorialHoldSuccessCount}/{Mathf.Max(1, tutorialHoldRequiredCount)}",
            TutorialState.Capa => "\u6559\u5b78\u8981\u6c42\uff1aPerfect \u4e00\u6b21",
            TutorialState.Dash => $"{tutorialDashSuccessCount}/{Mathf.Max(1, tutorialDashRequiredCount)}",
            TutorialState.Attack => tutorialAttackPerformed
                ? "\u6b63\u5728\u78ba\u8a8d\u662f\u5426\u70ba CLEAN \u547d\u4e2d..."
                : $"{tutorialAttackSuccessCount}/{Mathf.Max(1, tutorialAttackRequiredCount)}",
            TutorialState.Rules => "\u6e96\u5099\u9032\u5165\u6b63\u5f0f\u6230\u9b25...",
            TutorialState.Complete => "\u9032\u5165\u6b63\u5f0f\u6230\u9b25...",
            _ => string.Empty
        };
    }

    private string GetTutorialBody()
    {
        return string.Empty;
    }

    private void UpdatePhaseOne()
    {
        if (phaseOneGroundingSnapTimer > 0f)
        {
            phaseOneGroundingSnapTimer = Mathf.Max(0f, phaseOneGroundingSnapTimer - Time.unscaledDeltaTime);
            bullAI?.ForceSnapToGround();
        }

        if (bullStats != null && bullStats.currentHealth <= 0f)
        {
            isEnteringPhaseTwo = true;
            return;
        }
    }

    private void UpdatePhaseTwo()
    {
        if (phaseTwoState == PhaseTwoState.RoundWindow)
        {
            float progress = activeRoundWindowDuration <= 0.0001f
                ? 1f
                : Mathf.Clamp01(phaseTwoStateElapsed / activeRoundWindowDuration);

            UpdatePhaseTwoChargeMotion(progress);
        }
        if (playerStats != null)
            playerStats.SetHoldingCloth(false);

        switch (phaseTwoState)
        {
            case PhaseTwoState.Intro:
                UpdatePhaseTwoIntro();
                break;
            case PhaseTwoState.Tutorial:
                UpdatePhaseTwoTutorial();
                break;
            case PhaseTwoState.Calibration:
                UpdateCalibration();
                break;
            case PhaseTwoState.Standoff:
                UpdateStandoff();
                break;
            case PhaseTwoState.RoundPrepare:
                UpdateRoundPrepare();
                break;
            case PhaseTwoState.RoundWindow:
                UpdateRoundWindow();
                break;
            case PhaseTwoState.RoundResolve:
                UpdateRoundResolve();
                break;
        }
    }

    private void UpdatePhaseTwoIntro()
    {
        phaseTwoStateElapsed += Time.unscaledDeltaTime;
        if (phaseTwoStateElapsed >= introDuration)
        {
            if (phaseTwoTutorialShown)
                EnterCalibration();
            else
                EnterPhaseTwoTutorial();
        }
    }

    private void EnterPhaseTwoTutorial()
    {
        phaseTwoState = PhaseTwoState.Tutorial;
        phaseTwoStateElapsed = 0f;
        phaseTwoHasCommittedAttack = false;
        phaseTwoTutorialShown = true;
        playerController?.ClearInputBuffers();
        timingRing?.HideImmediate();
        timingRing?.ResetTimingWindow();
    }

    private void UpdatePhaseTwoTutorial()
    {
        phaseTwoStateElapsed += Time.unscaledDeltaTime;
        if (!IsPhaseTwoTutorialAdvanceReady)
            return;

        if (playerController != null && playerController.WasTutorialAdvancePressedThisFrame())
            EnterCalibration();
    }

    private void UpdateCalibration()
    {
        ArduinoTest.SensorConnectionState sensorState = arduinoTest != null
            ? arduinoTest.ConnectionState
            : ArduinoTest.SensorConnectionState.Disconnected;
        bool sensorConnected = sensorState == ArduinoTest.SensorConnectionState.Active;
        bool sensorWaitingForSignal = sensorState == ArduinoTest.SensorConnectionState.PortOpenNoSignal;
        bool hasRecentForceReading = sensorConnected &&
                                     arduinoTest != null &&
                                     arduinoTest.HasRecentForcePacket &&
                                     playerController != null &&
                                     playerController.HasRecentPhaseTwoSensorReading();
        bool shouldUseSensorCalibration = sensorConnected && hasRecentForceReading;

        if (shouldUseSensorCalibration)
        {
            calibrationSignalWaitTimer = 0f;
            if (!phaseTwoCalibrationUsingSensor)
            {
                phaseTwoCalibrationUsingSensor = true;
                calibrationHoldTimer = 0f;
                ResetPhaseTwoCalibrationAnchor();
            }
        }
        else
        {
            calibrationSignalWaitTimer += Time.unscaledDeltaTime;
            if (phaseTwoCalibrationUsingSensor)
            {
                phaseTwoCalibrationUsingSensor = false;
                calibrationHoldTimer = 0f;
                ResetPhaseTwoCalibrationAnchor();
            }
        }

        int progressPercent = Mathf.RoundToInt(PhaseTwoCalibrationProgress * 100f);
        float stableThreshold = playerController != null ? playerController.PhaseTwoCalibrationStableThreshold : 2f;
        float anchorDuration = Mathf.Max(0.1f, calibrationAnchorDuration);
        float waitDuration = Mathf.Max(0f, calibrationSensorGraceDuration);
        bool shouldHoldForSensor = sensorWaitingForSignal && calibrationSignalWaitTimer < waitDuration;

        if (shouldHoldForSensor)
        {
            ResetPhaseTwoCalibrationAnchor();
            calibrationHoldTimer = 0f;
            int waitPercent = waitDuration <= 0.01f ? 100 : Mathf.RoundToInt(Mathf.Clamp01(calibrationSignalWaitTimer / waitDuration) * 100f);
            phaseTwoCalibrationStatusText = $"\u6e96\u5099\u6821\u6e96 {waitPercent}%";
            return;
        }

        if (shouldUseSensorCalibration && playerController != null)
        {
            float currentForce = playerController.GetPhaseTwoSensorForce();
            float calibrationForce = playerController.GetPhaseTwoSensorCalibrationForce();
            if (!phaseTwoCalibrationAnchorLocked)
            {
                if (calibrationAnchorTimer <= 0f)
                    calibrationAnchorValue = calibrationForce;

                if (Mathf.Abs(calibrationForce - calibrationAnchorValue) > stableThreshold)
                {
                    calibrationAnchorValue = calibrationForce;
                    calibrationAnchorTimer = 0f;
                    calibrationHoldTimer = 0f;
                    phaseTwoCalibrationStatusText = $"\u6821\u6e96\u8d77\u9ede\u504f\u79fb\uff0c\u91cd\u65b0\u7a69\u5b9a\u4e2d 0%\n\u76ee\u524d\u4f4d\u7f6e {calibrationForce:F1}   \u5bb9\u8a31 \u00b1{stableThreshold:F0}";
                    return;
                }

                calibrationAnchorTimer = Mathf.Min(anchorDuration, calibrationAnchorTimer + Time.unscaledDeltaTime);
                calibrationHoldTimer = Mathf.Min(calibrationHoldDuration, calibrationHoldTimer + Time.unscaledDeltaTime);
                progressPercent = Mathf.RoundToInt(PhaseTwoCalibrationProgress * 100f);
                int anchorPercent = Mathf.RoundToInt((calibrationAnchorTimer / anchorDuration) * 100f);

                if (calibrationAnchorTimer >= anchorDuration)
                {
                    phaseTwoCalibrationAnchorLocked = true;
                    calibrationAnchorValue = calibrationForce;
                    phaseTwoCalibrationStatusText = $"\u6821\u6e96\u57fa\u6e96\u5df2\u9396\u5b9a {progressPercent}%\n\u57fa\u6e96\u503c {calibrationAnchorValue:F1}   \u7576\u524d\u529b\u9053 {currentForce:F1}";
                }
                else
                {
                    phaseTwoCalibrationStatusText = $"\u5efa\u7acb\u6821\u6e96\u57fa\u6e96 {anchorPercent}%\n\u76ee\u524d\u4f4d\u7f6e {calibrationForce:F1}   \u5bb9\u8a31 \u00b1{stableThreshold:F0}";
                }
            }
            else
            {
                float deviation = Mathf.Abs(calibrationForce - calibrationAnchorValue);
                if (deviation > stableThreshold)
                {
                    calibrationAnchorValue = calibrationForce;
                    calibrationAnchorTimer = 0f;
                    calibrationHoldTimer = 0f;
                    phaseTwoCalibrationAnchorLocked = false;
                    phaseTwoCalibrationStatusText = $"\u504f\u79fb\u904e\u5927\uff0c\u91cd\u65b0\u6821\u6e96 0%\n\u504f\u79fb {deviation:F1} / \u5bb9\u8a31 \u00b1{stableThreshold:F0}   \u7576\u524d\u529b\u9053 {currentForce:F1}";
                    return;
                }

                calibrationHoldTimer = Mathf.Min(calibrationHoldDuration, calibrationHoldTimer + Time.unscaledDeltaTime);
                progressPercent = Mathf.RoundToInt(PhaseTwoCalibrationProgress * 100f);
                phaseTwoCalibrationStatusText = $"\u6301\u528d\u6821\u6e96 {progressPercent}%\n\u57fa\u6e96\u503c {calibrationAnchorValue:F1}   \u504f\u79fb {deviation:F1} / \u5bb9\u8a31 \u00b1{stableThreshold:F0}   \u7576\u524d\u529b\u9053 {currentForce:F1}";
            }
        }
        else
        {
            ResetPhaseTwoCalibrationAnchor();
            bool calibrating = playerController != null && playerController.IsPhaseTwoCalibrationHeld();
            calibrationHoldTimer = calibrating
                ? Mathf.Min(calibrationHoldDuration, calibrationHoldTimer + Time.unscaledDeltaTime)
                : 0f;
            progressPercent = Mathf.RoundToInt(PhaseTwoCalibrationProgress * 100f);
            phaseTwoCalibrationStatusText = GetPhaseTwoCalibrationFallbackStatus(progressPercent, calibrating);
        }

        if (calibrationHoldTimer < calibrationHoldDuration)
            return;

        phaseTwoCalibrated = true;
        mercyTimer = 0f;
        calibrationHoldTimer = calibrationHoldDuration;
        phaseTwoCalibrationStatusText = "\u6821\u6e96\u5b8c\u6210 100%";
        lastPhaseTwoResult = string.Empty;

        if (phaseTwoAutoStartRoundAfterCalibration)
        {
            phaseTwoAutoStartRoundAfterCalibration = false;
            StartNextPhaseTwoRound();
            return;
        }

        EnterStandoff();
    }

    private void UpdateStandoff()
    {
        phaseTwoStateElapsed += Time.unscaledDeltaTime;

        bool stabPressed = playerController != null && playerController.ConsumePhaseTwoStabPressed();
        bool sensorForceBreaksStandoff = playerController != null &&
                                         playerController.HasRecentPhaseTwoSensorReading() &&
                                         playerController.GetPhaseTwoSensorForce() >= playerController.PhaseTwoStabThreshold;

        if (stabPressed || sensorForceBreaksStandoff)
        {
            phaseTwoHasCommittedAttack = true;
            mercyTimer = 0f;
            StartNextPhaseTwoRound();
            return;
        }

        mercyTimer += Time.unscaledDeltaTime;
        if (mercyTimer >= mercyEndingDelay)
            SetEnding(EndingType.Mercy);
    }

    private void UpdateRoundPrepare()
    {
        phaseTwoStateElapsed += Time.unscaledDeltaTime;

        if (!phaseTwoPrepareTelegraphStarted && phaseTwoStateElapsed >= GetRoundStanceDuration())
        {
            phaseTwoPrepareTelegraphStarted = true;
            bullAI?.PlayPhaseTwoTelegraph();
        }

        float prepareRatio = phaseTwoStateElapsed / Mathf.Max(0.01f, GetActiveRoundPrepareDuration());

        if (false)
            UpdatePhaseTwoRoundApproach();

        //if ((phaseTwoUseSideCut || phaseTwoUsePressureApproach) && prepareRatio < 0.7f)
        //    UpdatePhaseTwoRoundApproach();

        if (phaseTwoStateElapsed >= GetActiveRoundPrepareDuration())
            BeginPhaseTwoAttackWindow();
    }
    private void UpdateRoundWindow()
    {
        if (bullAI != null && phaseTwoStateElapsed > 0.05f)
            bullAI.PlayPhaseTwoWalkLoop();
        phaseTwoStateElapsed += Time.unscaledDeltaTime;

        float progress = phaseTwoStateElapsed / Mathf.Max(0.01f, activeRoundWindowDuration);
        progress = Mathf.Clamp01(progress);

        if (timingRing != null)
            timingRing.SetTelegraphProgress(progress);

        UpdatePhaseTwoChargeMotion(progress);
        if (bullAI != null && phaseTwoStateElapsed > 0.05f)
        {
            bullAI.PlayPhaseTwoWalkLoop();
        }

        if (phaseTwoStateElapsed < activeRoundWindowDuration)
            return;

        if (!phaseTwoHasCommittedAttack)
        {
            if (timingRing != null)
            {
                timingRing.HideImmediate();
                timingRing.ResetTimingWindow();
            }

            lastPhaseTwoResult = string.Empty;
            phaseTwoState = PhaseTwoState.RoundPrepare;
            phaseTwoStateElapsed = 0f;
            return;
        }

        if (timingRing != null && timingRing.IsActive)
        {
            timingRing.ResolveExternal("Miss");
            return;
        }

        HandlePhaseTwoStabResult("Miss");
    }

    private void UpdateRoundResolve()
    {
        phaseTwoStateElapsed += Time.unscaledDeltaTime;

        if (!phaseTwoResolveBullResetApplied)
        {
            phaseTwoResolveBullResetApplied = true;
            phaseTwoResolveCompleted = false;
            phaseTwoResolveWalkInitialized = false;
        }

        if (phaseTwoResolveWasMiss)
            UpdatePhaseTwoMissResolve();
        else
            UpdatePhaseTwoResolveWalk();

        if (!phaseTwoResolveCompleted)
            return;

        if (CheckPhaseTwoVictory())
            return;

        EnterCalibration(true);
    }

    private void UpdatePhaseTwoResolveWalk()
    {
        if (bullAI == null || playerStats == null || phaseTwoState != PhaseTwoState.RoundResolve || !phaseTwoResolveBullResetApplied)
            return;

        if (!phaseTwoResolveWalkInitialized)
        {
            phaseTwoResolveWalkInitialized = true;
            phaseTwoResolveCenterPoint = playerStats.transform.position;
            phaseTwoResolveCenterPoint.y = bullAI.transform.position.y;
            ResolvePhaseTwoShuttlePoints(phaseTwoResolveCenterPoint);
            phaseTwoResolveShuttleSegment = 0;

            bullAI.PlayPhaseTwoWalkLoop();
        }

        Vector3 currentPosition = bullAI.transform.position;
        Vector3 targetPoint = phaseTwoResolveShuttleSegment switch
        {
            0 => phaseTwoRoundSideSign > 0 ? phaseTwoResolveRightPoint : phaseTwoResolveLeftPoint,
            1 => phaseTwoRoundSideSign > 0 ? phaseTwoResolveLeftPoint : phaseTwoResolveRightPoint,
            _ => phaseTwoResolveCenterPoint
        };

        Vector3 nextPos = Vector3.MoveTowards(
            currentPosition,
            targetPoint,
            Mathf.Max(0.5f, phaseTwoShuttleRunSpeed) * Time.unscaledDeltaTime
        );

        Vector3 moveDirection = targetPoint - currentPosition;
        moveDirection.y = 0f;

        Quaternion nextRot = bullAI.transform.rotation;
        if (moveDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection.normalized, Vector3.up);
            nextRot = Quaternion.Slerp(
                bullAI.transform.rotation,
                targetRotation,
                8f * Time.unscaledDeltaTime
            );
        }

        bullAI.SetPhaseTwoPose(nextPos, nextRot);
        bullAI.PlayPhaseTwoWalkLoop();

        if (Vector3.Distance(nextPos, targetPoint) > 0.04f)
            return;

        phaseTwoResolveShuttleSegment++;
        if (phaseTwoResolveShuttleSegment <= 2)
            return;

        phaseTwoResolveCompleted = true;
        bullAI.PlayPhaseTwoRoundResetIdle();
    }

    private void UpdatePhaseTwoMissResolve()
    {
        if (bullAI == null || playerStats == null)
            return;

        float chargeDuration = Mathf.Max(0.2f, phaseTwoMissChargeDuration);
        float pauseDuration = Mathf.Max(phaseTwoMissPauseDuration, postChargePauseDuration + reorientDelay);
        float totalDuration = chargeDuration + pauseDuration;

        if (phaseTwoStateElapsed <= chargeDuration)
        {
            if (!phaseTwoResolveMissChargeStarted)
            {
                phaseTwoResolveMissChargeStarted = true;
                bullAI.StartPhaseTwoChargeAtPlayer(chargeDuration);
            }

            if (!phaseTwoResolveMissImpactApplied && phaseTwoStateElapsed >= chargeDuration * 0.65f)
            {
                phaseTwoResolveMissImpactApplied = true;
                playerStats.TakeBullImpact(14f, bullAI.transform.position);
            }
        }
        else
        {
            float pauseElapsed = phaseTwoStateElapsed - chargeDuration;
            float reorientWindow = Mathf.Min(Mathf.Max(0f, reorientDelay), pauseDuration);
            float reorientStart = Mathf.Max(0f, pauseDuration - reorientWindow);
            Vector3 current = ClampPhaseTwoBullPositionToArena(bullAI.transform.position);

            if (pauseElapsed >= reorientStart && reorientWindow > 0f)
            {
                Vector3 toPlayer = playerStats.transform.position - current;
                toPlayer.y = 0f;
                Quaternion targetRotation = toPlayer.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(toPlayer.normalized, Vector3.up)
                    : bullAI.transform.rotation;
                float turnProgress = Mathf.Clamp01((pauseElapsed - reorientStart) / Mathf.Max(0.01f, reorientWindow));
                Quaternion nextRotation = Quaternion.Slerp(bullAI.transform.rotation, targetRotation, turnProgress);
                bullAI.SetPhaseTwoPose(current, nextRotation);
                bullAI.PlayPhaseTwoStandoffIdle();
            }
            else
            {
                bullAI.PlayPhaseTwoStandoffIdle();
            }
        }

        if (phaseTwoStateElapsed >= totalDuration)
            phaseTwoResolveCompleted = true;
    }

    private void HandlePhaseTwoStabResult(string result)
    {

        if (currentPhase != GamePhase.PhaseTwo || phaseTwoState != PhaseTwoState.RoundWindow)
            return;

        lastPhaseTwoResult = result;
        mercyTimer = 0f;
        phaseTwoHasCommittedAttack = true;

        bool isSuccess = result == "Perfect!" || result == "Good";
        phaseTwoResolveWasMiss = !isSuccess;
        phaseTwoResolveMissImpactApplied = false;
        phaseTwoResolveMissChargeStarted = false;
        phaseTwoResolveCompleted = false;
        phaseTwoResolveShuttleSegment = 0;
        phaseTwoResolveNarrationLine = string.Empty;
        arcadeScoring?.RegisterPhaseTwoStabResult(result);
        if (isSuccess)
        {
            bullHitCount++;
            if (result == "Perfect!")
                Time.timeScale = 0.2f;
            Invoke(nameof(ResetTimeScale), 0.25f);
            nextRoundHasPerfectAdvantage = true;

            PlayPhaseTwoStabPresentationFeedback();
            BullfightActionVfxController.PlayPhaseTwoSwordHitVfx();
            bullAI?.PlayPhaseTwoHitReaction(result == "Perfect!");
        }
        else
        {
            playerHitCount++;
            bullAI?.PlayPhaseTwoAttackFollowThrough();
            phaseTwoResolveNarrationLine = GetRandomPhaseTwoReflectionLine();
        }

        if (timingRing != null)
        {
            timingRing.HideRingKeepFeedback();
            timingRing.ResetTimingWindow();
        }

        if (CheckPhaseTwoVictory())
            return;

        phaseTwoState = PhaseTwoState.RoundResolve;
        phaseTwoStateElapsed = 0f;
        phaseTwoResolveBullResetApplied = false;
        phaseTwoResolveWalkInitialized = false;
    }

    private bool CheckPhaseTwoVictory()
    {
        if (bullHitCount >= Mathf.Max(1, winsToFinish))
        {
            if (bullAI != null)
                bullAI.ForceDeathState();

            if (bullStats != null && bullStats.currentHealth > 0f)
            {
                bullStats.SetHealth(0f);
                return true;
            }

            SetEnding(EndingType.Glory);
            return true;
        }

        if (playerHitCount >= Mathf.Max(1, winsToFinish))
        {
            SetEnding(EndingType.Tragedy);
            return true;
        }

        if (phaseTwoRoundIndex >= Mathf.Max(1, maxRounds))
        {
            SetEnding(bullHitCount > playerHitCount ? EndingType.Glory : EndingType.Tragedy);
            return true;
        }

        return false;
    }

    private void StartNextPhaseTwoRound()
    {
        if (currentPhase != GamePhase.PhaseTwo || CheckPhaseTwoVictory())
            return;

        phaseTwoRoundIndex++;
        phaseTwoState = PhaseTwoState.RoundPrepare;
        phaseTwoStateElapsed = 0f;
        activeRoundWindowDuration = GetActiveRoundWindowDuration();
        lastPhaseTwoResult = string.Empty;
        currentRoundHasPerfectAdvantage = nextRoundHasPerfectAdvantage;
        nextRoundHasPerfectAdvantage = false;
        phaseTwoPrepareTelegraphStarted = false;
        phaseTwoResolveBullResetApplied = false;
        phaseTwoResolveWalkInitialized = false;
        phaseTwoResolveWasMiss = false;
        phaseTwoResolveMissImpactApplied = false;
        phaseTwoResolveMissChargeStarted = false;
        phaseTwoResolveCompleted = false;
        phaseTwoResolveShuttleSegment = 0;
        phaseTwoResolveNarrationLine = string.Empty;
        phaseTwoCalibrationStatusText = string.Empty;

        if (currentRoundHasPerfectAdvantage)
            activeRoundWindowDuration += perfectTelegraphBonus;

        PreparePhaseTwoMovementPattern();
        ResetBullForPhaseTwoRound();
        phaseTwoRoundSideSign *= -1;

        if (timingRing != null)
        {
            timingRing.HideImmediate();
            timingRing.ResetTimingWindow();
        }

        playerController?.ClearInputBuffers();
        bullAI?.PlayPhaseTwoStanceConfirm();
    }

    private void BeginPhaseTwoAttackWindow()
    {
        phaseTwoState = PhaseTwoState.RoundWindow;
        phaseTwoStateElapsed = 0f;
        EnsureBullVisibleForPhaseTwoWindow();

        if (!phaseTwoPrepareTelegraphStarted)
        {
            phaseTwoPrepareTelegraphStarted = true;
            bullAI?.PlayPhaseTwoTelegraph();
        }

        if (timingRing == null)
            return;

        timingRing.ResetTimingWindow();
        if (currentRoundHasPerfectAdvantage)
        {
            float adjustedGood = timingRing.DefaultGoodProgress - perfectTimingEase;
            float adjustedPerfect = timingRing.DefaultPerfectProgress - perfectTimingEase;
            timingRing.SetTimingWindow(adjustedGood, adjustedPerfect);
        }

        timingRing.StartTiming(BullTimingRing.TimingMode.Estocada, HandlePhaseTwoStabResult);
        timingRing.SetTelegraphProgress(0f);
    }

    private void EnterPhaseTwo()
    {
        SetArcadeOverlayGameplayFrozen(false);
        isEnteringPhaseTwo = false;
        bullDeathTimer = 0f;
        ApplyPhaseTwoLighting();
        StopEndingVideoPlayback();
        bullAI?.SetTutorialControl(false);
        currentPhase = GamePhase.PhaseTwo;
        Debug.Log("[GameFlow] Enter PhaseTwo");
        if (audioController != null) audioController.PlayPhaseBGM(2);
        currentEnding = EndingType.None;
        phaseTwoState = PhaseTwoState.Intro;
        NotifyArcadePhaseTwoEntered();
        phaseTwoStateElapsed = 0f;
        calibrationHoldTimer = 0f;
        phaseTwoCalibrated = false;
        phaseTwoHasCommittedAttack = false;
        mercyTimer = 0f;
        phaseTwoRoundIndex = 0;
        bullHitCount = 0;
        playerHitCount = 0;
        activeRoundWindowDuration = GetActiveRoundWindowDuration();
        nextRoundHasPerfectAdvantage = false;
        currentRoundHasPerfectAdvantage = false;
        phaseTwoRoundSideSign = 1;
        phaseTwoPrepareTelegraphStarted = false;
        phaseTwoResolveBullResetApplied = false;
        phaseTwoResolveWalkInitialized = false;
        phaseTwoResolveWasMiss = false;
        phaseTwoResolveMissImpactApplied = false;
        phaseTwoResolveMissChargeStarted = false;
        phaseTwoResolveCompleted = false;
        phaseTwoAutoStartRoundAfterCalibration = false;
        phaseTwoResolveShuttleSegment = 0;
        phaseTwoResolveNarrationLine = string.Empty;
        phaseTwoCalibrationStatusText = string.Empty;
        lastPhaseTwoResult = string.Empty;
        Time.timeScale = phaseTwoTimeScale;
        ApplySkyboxForPhase(GamePhase.PhaseTwo);

        bullStats?.ResetCombatState();

        if (bullAI != null)
        {
            bullAI.enabled = true;
            bullAI.ResetCombatState();
        }

        bullBleedVfx?.ClearBleeds();
        PreparePhaseTwoMovementPattern();
        ResetBullForPhaseTwoRound();
        bullAI?.PlayPhaseTwoStandoffIdle();

        if (playerStats != null)
        {
            playerStats.SetHoldingCloth(false);
            playerStats.SetShooterGameplayEnabled(false);
        }

        playerController?.ClearInputBuffers();

        if (timingRing != null)
        {
            timingRing.HideImmediate();
            timingRing.ResetTimingWindow();
        }

        phaseTwoPresentation?.EnterPhaseTwo(phaseTwoAmbientIntensity, phaseTwoDirectionalLightMultiplier);

        Debug.Log("Phase 2 intro started. Keep the sword still for 5 seconds to calibrate, then thrust with the sensor.");
    }

    private void EnterCalibration(bool autoStartRoundAfterCalibration = false)
    {
        phaseTwoState = PhaseTwoState.Calibration;
        phaseTwoStateElapsed = 0f;
        calibrationHoldTimer = 0f;
        calibrationSignalWaitTimer = 0f;
        phaseTwoCalibrationUsingSensor = false;
        ResetPhaseTwoCalibrationAnchor();
        phaseTwoHasCommittedAttack = false;
        currentRoundHasPerfectAdvantage = false;
        phaseTwoPrepareTelegraphStarted = false;
        phaseTwoResolveBullResetApplied = false;
        phaseTwoResolveWalkInitialized = false;
        phaseTwoResolveWasMiss = false;
        phaseTwoResolveMissImpactApplied = false;
        phaseTwoResolveMissChargeStarted = false;
        phaseTwoResolveCompleted = false;
        phaseTwoAutoStartRoundAfterCalibration = autoStartRoundAfterCalibration;
        phaseTwoResolveShuttleSegment = 0;
        phaseTwoResolveNarrationLine = string.Empty;
        phaseTwoCalibrationStatusText = GetPhaseTwoCalibrationFallbackStatus(0f);
        lastPhaseTwoResult = string.Empty;
        bullAI?.PlayPhaseTwoStandoffIdle();
        RequestPhaseTwoSensorCalibration();
    }

    private void EnterStandoff()
    {
        phaseTwoState = PhaseTwoState.Standoff;
        phaseTwoStateElapsed = 0f;
        mercyTimer = 0f;
        phaseTwoHasCommittedAttack = false;
        currentRoundHasPerfectAdvantage = false;
        phaseTwoPrepareTelegraphStarted = false;
        phaseTwoResolveBullResetApplied = false;
        phaseTwoResolveWalkInitialized = false;
        phaseTwoResolveWasMiss = false;
        phaseTwoResolveMissImpactApplied = false;
        phaseTwoResolveMissChargeStarted = false;
        phaseTwoResolveCompleted = false;
        phaseTwoAutoStartRoundAfterCalibration = false;
        phaseTwoResolveShuttleSegment = 0;
        phaseTwoResolveNarrationLine = string.Empty;
        phaseTwoCalibrationStatusText = string.Empty;
        lastPhaseTwoResult = string.Empty;
        PreparePhaseTwoMovementPattern();
        ResetBullForPhaseTwoRound();
        bullAI?.PlayPhaseTwoStandoffIdle();

        if (timingRing != null)
        {
            timingRing.HideImmediate();
            timingRing.ResetTimingWindow();
        }

        playerController?.ClearInputBuffers();
    }

    private void HandleDebugShortcuts()
    {
        HandleStaffShortcuts();

        if (!AreDebugShortcutsAvailable())
            return;

        if (Input.GetKeyDown(debugPhaseTwoKey) && bullStats != null)
        {
            MarkArcadeRunDebugModified();
            bullStats.ApplyDebugDamage(debugPhaseTwoDamage);
            Debug.Log($"Debug shortcut: bull damaged for {debugPhaseTwoDamage}. Current health: {bullStats.currentHealth}");
        }

        if (Input.GetKeyDown(debugRefillStaminaKey) && playerStats != null)
        {
            MarkArcadeRunDebugModified();
            playerStats.RefillStaminaForDebug();
        }

        if (Input.GetKeyDown(debugKillBullKey) && bullStats != null)
        {
            MarkArcadeRunDebugModified();
            bullStats.SetHealth(0f);
            Debug.Log("Debug shortcut: bull health forced to 0.");
        }

        if (Input.GetKeyDown(debugKillPlayerKey) && playerStats != null)
        {
            MarkArcadeRunDebugModified();
            playerStats.ForceDeathForDebug();
        }
    }

    private void HandleStaffShortcuts()
    {
        if (!enableStaffHighScoreReset)
            return;

        bool shiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (!shiftPressed || !Input.GetKeyDown(staffResetDailyHighScoreKey))
            return;

        EnsureArcadeScoring();
        arcadeScoring?.ResetDailyHighScoreForStaff();
        Debug.Log("Staff shortcut: daily high score reset.");
    }

    private bool AreDebugShortcutsAvailable() => enableDebugShortcuts;

    private void SetEnding(EndingType ending)
    {
        audioController?.StopAllAudio();
        if (currentPhase == GamePhase.Ending)
            return;

        if (ending == EndingType.Mercy)
            arcadeScoring?.MarkRunUnranked("Mercy");

        if (ending == EndingType.Tragedy && playerStats != null)
            playerStats.ForceEndingDeathPresentation();

        currentPhase = GamePhase.Ending;
        currentEnding = ending;
        phaseTwoState = PhaseTwoState.None;
        tutorialState = TutorialState.None;
        queuedTutorialState = TutorialState.None;
        tutorialTransitionTimer = 0f;
        tutorialFeedbackText = string.Empty;
        phaseTwoCalibrationStatusText = string.Empty;
        Time.timeScale = 1f;
        ApplySkyboxForPhase(GamePhase.PhaseOne);
        phaseTwoPresentation?.ExitPhaseTwo();
        bullAI?.SetTutorialControl(false);
        LockGameplayForEnding();

        if (timingRing != null)
        {
            timingRing.HideImmediate();
            timingRing.ResetTimingWindow();
        }

        endingVideoStartDelayRemaining = Mathf.Max(0f, endingVideoDelay);
        Debug.Log($"Ending reached: {ending}");
    }

    private void ResetState()
    {
        StopTutorialCompletionVideoPlayback();
        StopPhaseOneToPhaseTwoVideoPlayback();
        StopEndingVideoPlayback();
        ResetArcadeRuntimeState();
        currentPhase = GamePhase.PhaseOne;
        currentEnding = EndingType.None;
        endingVideoStartDelayRemaining = -1f;
        phaseTwoState = PhaseTwoState.None;
        tutorialState = TutorialState.None;
        queuedTutorialState = TutorialState.None;
        mercyTimer = 0f;
        bullDeathTimer = 0f;
        isEnteringPhaseTwo = false;
        phaseTwoStateElapsed = 0f;
        calibrationHoldTimer = 0f;
        ResetPhaseTwoCalibrationAnchor();
        activeRoundWindowDuration = roundWindowDuration;
        tutorialStateElapsed = 0f;
        tutorialHoldTimer = 0f;
        tutorialTransitionTimer = 0f;
        tutorialCapaWindowTimer = 0f;
        tutorialDashStepStartedAt = -999f;
        tutorialAttackResolveAt = -1f;
        tutorialAttackHealthAtStepStart = 0f;
        tutorialMoveProgress = 0f;
        tutorialLookProgress = 0f;
        phaseTwoRoundIndex = 0;
        bullHitCount = 0;
        playerHitCount = 0;
        tutorialHoldSuccessCount = 0;
        tutorialDashSuccessCount = 0;
        tutorialAttackSuccessCount = 0;
        phaseTwoCalibrated = false;
        phaseTwoHasCommittedAttack = false;
        nextRoundHasPerfectAdvantage = false;
        currentRoundHasPerfectAdvantage = false;
        phaseTwoRoundSideSign = 1;
        phaseTwoPrepareTelegraphStarted = false;
        phaseTwoResolveBullResetApplied = false;
        phaseTwoResolveWalkInitialized = false;
        phaseTwoResolveWasMiss = false;
        phaseTwoResolveMissImpactApplied = false;
        phaseTwoResolveMissChargeStarted = false;
        phaseTwoResolveCompleted = false;
        phaseTwoAutoStartRoundAfterCalibration = false;
        phaseTwoResolveShuttleSegment = 0;
        tutorialChargeStarted = false;
        tutorialCapaQteStarted = false;
        tutorialCapaQteResolved = false;
        tutorialHoldRegisteredThisAttempt = false;
        tutorialAttackPerformed = false;
        tutorialAttackCleanHitRegistered = false;
        lastPhaseTwoResult = string.Empty;
        tutorialFeedbackText = string.Empty;
        phaseOneGroundingSnapTimer = 0f;
        phaseTwoResolveNarrationLine = string.Empty;
        phaseTwoCalibrationStatusText = string.Empty;
        phaseTwoResolveCenterPoint = Vector3.zero;
        phaseTwoResolveLeftPoint = Vector3.zero;
        phaseTwoResolveRightPoint = Vector3.zero;
        phaseTwoPresentation?.ExitPhaseTwo();
        bullAI?.SetTutorialControl(false);
        Time.timeScale = 1f;
        ApplySkyboxForPhase(GamePhase.PhaseOne);
        phaseTwoTutorialShown = false;
    }

    private void ResetTutorialCapaQteState()
    {
        tutorialCapaWindowTimer = 0f;
        tutorialCapaQteStarted = false;
        tutorialCapaQteResolved = false;
        timingRing?.HideImmediate();
        timingRing?.ResetTimingWindow();
        timingRing?.SetTelegraphProgress(0f);
    }

    private string GetMoveLabel()
    {
        return playerController != null ? playerController.GetMoveDisplayLabel() : "\u5de6\u8611\u83c7\u982d";
    }

    private string GetLookLabel()
    {
        return playerController != null ? playerController.GetLookDisplayLabel() : "\u53f3\u8611\u83c7\u982d";
    }

    private string GetHoldLabel()
    {
        return playerController != null ? playerController.GetHoldDisplayLabel() : "C";
    }

    private string GetSwingLabel()
    {
        return playerController != null ? playerController.GetSwingDisplayLabel() : "Space";
    }

    private string GetDashLabel()
    {
        return playerController != null ? playerController.GetDashDisplayLabel() : "Y";
    }

    private string GetAttackLabel()
    {
        return playerController != null ? playerController.GetAttackDisplayLabel() : "B";
    }

    private string GetPhaseTwoCalibrationLabel()
    {
        return playerController != null ? playerController.GetPhaseTwoCalibrationDisplayLabel() : "G";
    }

    private string GetPhaseTwoStabLabel()
    {
        return playerController != null ? playerController.GetPhaseTwoStabDisplayLabel() : "E";
    }

    private string GetPhaseTwoTutorialInstruction()
    {
        if (ShouldShowSensorPhaseTwoPrompt())
            return "\u5148\u4fdd\u6301\u528d\u4e0d\u52d5\u5b8c\u6210\u6821\u6e96\uff0c\u518d\u4ee5\u63ee\u528d\u8d85\u904e\u529b\u9053\u9580\u6abb\u5411\u524d\u523a\u51fa\u3002";

        return $"\u5148\u6309 {GetPhaseTwoCalibrationLabel()} \u5b8c\u6210\u6821\u6e96\uff0c\u518d\u6309 {GetPhaseTwoStabLabel()} \u5411\u524d\u523a\u51fa\u3002";
    }

    private string GetPhaseTwoStandoffInstruction()
    {
        return ShouldShowSensorPhaseTwoPrompt()
            ? "\u7dad\u6301\u5c0d\u5cd9\uff0c\u6293\u6e96\u6642\u6a5f\u4ee5\u63ee\u528d\u8d85\u904e\u529b\u9053\u9580\u6abb\u523a\u51fa\u6c7a\u5b9a\u6027\u4e00\u64ca\u3002"
            : $"\u7dad\u6301\u5c0d\u5cd9\uff0c\u6293\u6e96\u6642\u6a5f\u6309 {GetPhaseTwoStabLabel()} \u523a\u51fa\u6c7a\u5b9a\u6027\u4e00\u64ca\u3002";
    }

    private bool ShouldShowSensorPhaseTwoPrompt()
    {
        return playerController != null && playerController.HasRecentPhaseTwoSensorReading();
    }

    private bool IsPhaseTwoSensorActivelyDrivingInput()
    {
        return arduinoTest != null && arduinoTest.ConnectionState == ArduinoTest.SensorConnectionState.Active;
    }

    private bool IsWaitingForSensorCalibrationSignal()
    {
        return phaseTwoState == PhaseTwoState.Calibration &&
               arduinoTest != null &&
               arduinoTest.ConnectionState == ArduinoTest.SensorConnectionState.PortOpenNoSignal &&
               calibrationSignalWaitTimer < Mathf.Max(0f, calibrationSensorGraceDuration);
    }

    private string GetPhaseTwoCalibrationFallbackStatus(float progressPercent, bool calibrating = false)
    {
        return calibrating
            ? $"\u6301\u528d\u6821\u6e96 {progressPercent}%"
            : $"\u7a69\u5b9a\u6301\u528d\uff0c\u6e96\u5099\u958b\u59cb\u6821\u6e96 {progressPercent}%";
    }

    private bool HasMissingReferences()
    {
        return playerStats == null ||
               bullStats == null ||
               bullAI == null ||
               playerController == null ||
               timingRing == null ||
               spawnManager == null ||
               phaseTwoPresentation == null;
    }

    private void PlayPhaseTwoStabPresentationFeedback()
    {
        BullfightHandAnimatorController handAnimator = playerStats != null
            ? playerStats.GetComponent<BullfightHandAnimatorController>()
            : BullfightSceneCache.FindObject<BullfightHandAnimatorController>();

        handAnimator?.PlayPhaseTwoStabAnimation();
        audioController?.PlayPhaseTwoStabCue();
    }

    private void LockGameplayForEnding()
    {
        playerController?.ClearInputBuffers();
        playerController?.ForceStopMovement();

        if (playerStats != null)
        {
            playerStats.SetHoldingCloth(false);
            playerStats.SetShooterGameplayEnabled(false);
        }
    }

    private void BeginEndingVideoPlayback(EndingType ending)
    {
        StopTutorialCompletionVideoPlayback();
        StopEndingVideoPlayback();
        EnsureEndingSkipUi();
        ShowEndingSkipUi(true);
        HideSceneForEndingPlayback();

        VideoClip endingClip = GetEndingVideoClip(ending);
        if (endingVideoPlayer == null || endingClip == null)
        {
            RestoreSceneAfterEndingPlayback();
            ShowArcadeResultsOrReturnToMenu();
            return;
        }

        endingVideoPlaybackActive = true;
        endingVideoPlayer.loopPointReached -= HandleEndingVideoCompleted;
        endingVideoPlayer.loopPointReached += HandleEndingVideoCompleted;
        endingVideoPlayer.isLooping = false;
        endingVideoPlayer.playOnAwake = false;
        endingVideoPlayer.waitForFirstFrame = true;
        endingVideoPlayer.skipOnDrop = false;
        endingVideoPlayer.clip = endingClip;
        ConfigureVideoAudio(endingClip, endingVideoVolume);
        audioController?.StopBGM();

        if (endingVideoPlayer.gameObject != null)
            endingVideoPlayer.gameObject.SetActive(true);

        endingVideoPlayer.Stop();
        endingVideoPlayer.Play();
    }

    private void UpdateEndingVideoPlayback()
    {
        if (endingVideoStartDelayRemaining >= 0f)
        {
            endingVideoStartDelayRemaining -= Time.unscaledDeltaTime;
            if (endingVideoStartDelayRemaining > 0f)
                return;

            endingVideoStartDelayRemaining = -1f;
            BeginEndingVideoPlayback(currentEnding);
            return;
        }

        if (!endingVideoPlaybackActive)
            return;

        if (IsEndingSkipKeyboardPressedThisFrame())
        {
            SkipEndingVideo();
            return;
        }

        if (playerController == null || !playerController.HasRecentPhaseTwoSensorReading())
            return;

        float currentForce = playerController.GetPhaseTwoSensorForce();
        if (currentForce >= playerController.PhaseTwoStabThreshold)
            SkipEndingVideo();
    }

    private void SkipEndingVideo()
    {
        if (!endingVideoPlaybackActive)
            return;

        StopEndingVideoPlayback();
        ShowArcadeResultsOrReturnToMenu();
    }

    private void HandleSkipVideoButtonPressed()
    {
        if (tutorialCompletionVideoPlaybackActive)
        {
            SkipTutorialCompletionVideo();
            return;
        }

        if (phaseOneToPhaseTwoVideoPlaybackActive)
        {
            SkipPhaseOneToPhaseTwoVideo();
            return;
        }

        if (endingVideoPlaybackActive)
            SkipEndingVideo();
    }

    private void StopEndingVideoPlayback()
    {
        if (tutorialCompletionVideoPlaybackActive)
        {
            StopTutorialCompletionVideoPlayback();
            return;
        }

        if (phaseOneToPhaseTwoVideoPlaybackActive)
        {
            StopPhaseOneToPhaseTwoVideoPlayback();
            return;
        }

        endingVideoPlaybackActive = false;
        endingVideoStartDelayRemaining = -1f;
        ShowEndingSkipUi(false);

        if (endingVideoPlayer == null)
        {
            RestoreSceneAfterEndingPlayback();
            return;
        }

        endingVideoPlayer.loopPointReached -= HandleEndingVideoCompleted;
        endingVideoPlayer.Stop();

        if (endingVideoPlayer.gameObject != null)
            endingVideoPlayer.gameObject.SetActive(false);

        RestoreSceneAfterEndingPlayback();
    }

    private void ConfigureVideoAudio(VideoClip clip, float baseVolume)
    {
        if (endingVideoPlayer == null)
            return;

        endingVideoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;

        ushort audioTrackCount = clip != null ? clip.audioTrackCount : (ushort)0;
        endingVideoPlayer.controlledAudioTrackCount = audioTrackCount;

        float resolvedVolume = Mathf.Max(0f, baseVolume);
        if (audioController != null)
            resolvedVolume *= Mathf.Lerp(0.85f, 1f, Mathf.Clamp01(audioController.SfxVolume));

        for (ushort trackIndex = 0; trackIndex < audioTrackCount; trackIndex++)
        {
            endingVideoPlayer.EnableAudioTrack(trackIndex, true);
            endingVideoPlayer.SetDirectAudioMute(trackIndex, false);
            endingVideoPlayer.SetDirectAudioVolume(trackIndex, resolvedVolume);
        }
    }

    private void HandleEndingVideoCompleted(VideoPlayer source)
    {
        if (source != null)
            source.loopPointReached -= HandleEndingVideoCompleted;

        StopEndingVideoPlayback();
        ShowArcadeResultsOrReturnToMenu();
    }

    private VideoClip GetEndingVideoClip(EndingType ending)
    {
        return ending switch
        {
            EndingType.Glory => gloryEndingClip,
            EndingType.Tragedy => tragedyEndingClip,
            EndingType.Mercy => mercyEndingClip,
            _ => null
        };
    }

    private void EnsureEndingSkipUi()
    {
        if (endingSkipRoot != null)
        {
            if (endingSkipLabel != null)
                ConfigureEndingSkipLabelRect(endingSkipLabel.rectTransform);
            if (endingSkipForceLabel != null)
                ConfigureEndingSkipForceLabelRect(endingSkipForceLabel.rectTransform);
            return;
        }

        GameObject canvasObject = new GameObject("BullfightEndingSkipCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 6000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject rootObject = new GameObject("BullfightEndingSkipButton", typeof(RectTransform), typeof(Image), typeof(Button));
        rootObject.transform.SetParent(canvasObject.transform, false);

        endingSkipRoot = rootObject.GetComponent<RectTransform>();
        endingSkipRoot.anchorMin = new Vector2(1f, 0f);
        endingSkipRoot.anchorMax = new Vector2(1f, 0f);
        endingSkipRoot.pivot = new Vector2(1f, 0f);
        endingSkipRoot.sizeDelta = new Vector2(330f, 160f);
        endingSkipRoot.anchoredPosition = new Vector2(-28f, 28f);
        endingSkipRoot.localScale = Vector3.one;
        endingSkipRoot.localRotation = Quaternion.identity;

        Image background = rootObject.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.72f);
        background.raycastTarget = true;

        endingSkipButton = rootObject.GetComponent<Button>();
        endingSkipButton.targetGraphic = background;
        endingSkipButton.onClick.AddListener(HandleSkipVideoButtonPressed);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(rootObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        ConfigureEndingSkipLabelRect(labelRect);

        endingSkipLabel = labelObject.GetComponent<Text>();
        endingSkipLabel.alignment = TextAnchor.MiddleCenter;
        endingSkipLabel.font = GetRuntimeUiFont();
        endingSkipLabel.fontSize = 28;
        endingSkipLabel.fontStyle = FontStyle.Normal;
        endingSkipLabel.color = new Color(1f, 0.92f, 0.32f, 1f);
        endingSkipLabel.raycastTarget = false;
        endingSkipLabel.text = GetEndingSkipInstructionText(35f, false);
        ApplyLocalizedRuntimeFont(endingSkipLabel, endingSkipLabel.text, 28, wrap: true, VerticalWrapMode.Truncate, 20, 0.92f);

        Outline labelOutline = labelObject.AddComponent<Outline>();
        labelOutline.effectColor = new Color(0f, 0f, 0f, 0.95f);
        labelOutline.effectDistance = new Vector2(2f, -2f);

        Shadow labelShadow = labelObject.AddComponent<Shadow>();
        labelShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        labelShadow.effectDistance = new Vector2(0f, -3f);

        GameObject forceObject = new GameObject("ForceLabel", typeof(RectTransform), typeof(Text));
        forceObject.transform.SetParent(rootObject.transform, false);

        RectTransform forceRect = forceObject.GetComponent<RectTransform>();
        ConfigureEndingSkipForceLabelRect(forceRect);

        endingSkipForceLabel = forceObject.GetComponent<Text>();
        endingSkipForceLabel.alignment = TextAnchor.MiddleCenter;
        endingSkipForceLabel.font = GetRuntimeUiFont();
        endingSkipForceLabel.fontSize = 20;
        endingSkipForceLabel.fontStyle = FontStyle.Normal;
        endingSkipForceLabel.color = new Color(0.98f, 0.92f, 0.62f, 1f);
        endingSkipForceLabel.raycastTarget = false;
        endingSkipForceLabel.text = "\u529b\u9053 0% / \u9580\u6abb70%";
        ApplyLocalizedRuntimeFont(endingSkipForceLabel, endingSkipForceLabel.text, 20, wrap: false, VerticalWrapMode.Truncate, 14);

        Outline forceOutline = forceObject.AddComponent<Outline>();
        forceOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        forceOutline.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject barBackgroundObject = new GameObject("ForceBarBackground", typeof(RectTransform), typeof(Image));
        barBackgroundObject.transform.SetParent(rootObject.transform, false);

        RectTransform barBackgroundRect = barBackgroundObject.GetComponent<RectTransform>();
        barBackgroundRect.anchorMin = new Vector2(0f, 0.06f);
        barBackgroundRect.anchorMax = new Vector2(1f, 0.22f);
        barBackgroundRect.offsetMin = new Vector2(12f, 0f);
        barBackgroundRect.offsetMax = new Vector2(-12f, 0f);

        endingSkipForceBarBackground = barBackgroundObject.GetComponent<Image>();
        endingSkipForceBarBackground.color = new Color(0.12f, 0.12f, 0.12f, 0.9f);
        endingSkipForceBarBackground.raycastTarget = false;

        GameObject barFillObject = new GameObject("ForceBarFill", typeof(RectTransform), typeof(Image));
        barFillObject.transform.SetParent(barBackgroundObject.transform, false);

        RectTransform barFillRect = barFillObject.GetComponent<RectTransform>();
        barFillRect.anchorMin = Vector2.zero;
        barFillRect.anchorMax = new Vector2(0f, 1f);
        barFillRect.offsetMin = Vector2.zero;
        barFillRect.offsetMax = Vector2.zero;
        endingSkipForceBarFillRect = barFillRect;

        endingSkipForceBarFill = barFillObject.GetComponent<Image>();
        endingSkipForceBarFill.color = new Color(0.95f, 0.2f, 0.15f, 1f);
        endingSkipForceBarFill.raycastTarget = false;

        barBackgroundObject.transform.SetAsFirstSibling();
        forceObject.transform.SetAsLastSibling();
        labelObject.transform.SetAsLastSibling();

        ShowEndingSkipUi(false);
    }

    private static void ConfigureEndingSkipLabelRect(RectTransform labelRect)
    {
        if (labelRect == null)
            return;

        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.offsetMin = new Vector2(12f, 43f);
        labelRect.offsetMax = new Vector2(-12f, 27f);
        labelRect.localScale = Vector3.one;
        labelRect.localRotation = Quaternion.identity;
    }

    private static void ConfigureEndingSkipForceLabelRect(RectTransform forceRect)
    {
        if (forceRect == null)
            return;

        forceRect.anchorMin = new Vector2(0f, 0.28f);
        forceRect.anchorMax = new Vector2(1f, 0.56f);
        forceRect.pivot = new Vector2(0.5f, 0.5f);
        forceRect.offsetMin = new Vector2(10f, 0f);
        forceRect.offsetMax = new Vector2(-10f, -6f);
        forceRect.localScale = Vector3.one;
        forceRect.localRotation = Quaternion.identity;
    }

    private void UpdateEndingSkipUiVisibility()
    {
        if (endingSkipLabel == null && endingSkipForceLabel == null)
            return;

        float threshold = playerController != null ? playerController.PhaseTwoStabThreshold : 35f;
        float forceCap = playerController != null ? playerController.PhaseTwoForceCap : 50f;
        float currentForce = playerController != null ? playerController.GetPhaseTwoSensorForce() : 0f;
        int forcePercent = Mathf.RoundToInt(Mathf.Clamp01(currentForce / Mathf.Max(0.01f, forceCap)) * 100f);
        int thresholdPercent = Mathf.RoundToInt(Mathf.Clamp01(threshold / Mathf.Max(0.01f, forceCap)) * 100f);
        float normalizedForce = Mathf.Clamp01(currentForce / Mathf.Max(0.01f, forceCap));
        Color forceColor = Color.Lerp(new Color(0.92f, 0.18f, 0.16f, 1f), new Color(1f, 0.88f, 0.18f, 1f), normalizedForce);

        if (endingSkipLabel != null)
        {
            bool canSkip = forcePercent >= thresholdPercent;
            endingSkipLabel.text = GetEndingSkipInstructionText(threshold, canSkip);
            endingSkipLabel.color = canSkip
                ? new Color(1f, 0.98f, 0.45f, 1f)
                : new Color(0.98f, 0.88f, 0.42f, 1f);
            ApplyLocalizedRuntimeFont(endingSkipLabel, endingSkipLabel.text, 28, wrap: true, VerticalWrapMode.Truncate, 20, 0.92f);
        }

        if (endingSkipForceLabel != null)
        {
            endingSkipForceLabel.text = $"\u529b\u9053 {forcePercent}% / \u9580\u6abb{thresholdPercent}%";
            endingSkipForceLabel.color = forceColor;
            ApplyLocalizedRuntimeFont(endingSkipForceLabel, endingSkipForceLabel.text, 20, wrap: false, VerticalWrapMode.Truncate, 14);
        }

        if (endingSkipForceBarFill != null)
            endingSkipForceBarFill.color = forceColor;

        if (endingSkipForceBarFillRect != null)
            endingSkipForceBarFillRect.anchorMax = new Vector2(normalizedForce, 1f);
    }

    private bool IsTutorialCompletionVideoSkipPressedThisFrame()
    {
        return (Keyboard.current != null &&
                (Keyboard.current.enterKey.wasPressedThisFrame ||
                 Keyboard.current.numpadEnterKey.wasPressedThisFrame)) ||
               (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
    }

    private bool IsPhaseOneToPhaseTwoVideoSkipCheatPressedThisFrame()
    {
        if (phaseOneToPhaseTwoSkipCheatKey == KeyCode.None)
            return false;

        return Input.GetKeyDown(phaseOneToPhaseTwoSkipCheatKey);
    }

    private bool IsEndingSkipKeyboardPressedThisFrame()
    {
        return (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame) ||
               (endingSkipKey != KeyCode.None && Input.GetKeyDown(endingSkipKey));
    }

    private static string GetTutorialCompletionSkipInstructionText()
    {
        return "\u8df3\u904e\u5f71\u7247 / \u6309 A";
    }

    private string GetEndingSkipInstructionText(float threshold, bool canSkip)
    {
        string statusText = canSkip ? "\u53ef\u4ee5\u8df3\u904e\u5f71\u7247" : "\u8df3\u904e\u5f71\u7247";
        return $"{statusText}\n\u6309 A \u6216\u529b\u9053 > {threshold:0}";
    }

    private static string GetReadableKeyCodeLabel(KeyCode keyCode)
    {
        return keyCode switch
        {
            KeyCode.Space => "Space",
            KeyCode.LeftControl => "LeftCtrl",
            KeyCode.RightControl => "RightCtrl",
            KeyCode.LeftShift => "LeftShift",
            KeyCode.RightShift => "RightShift",
            KeyCode.Return => "Enter",
            KeyCode.KeypadEnter => "NumpadEnter",
            KeyCode.Escape => "Esc",
            _ => keyCode.ToString()
        };
    }

    private static Font GetRuntimeUiFont()
    {
        if (cachedRuntimeUiFont != null)
            return cachedRuntimeUiFont;

        cachedRuntimeUiFont = Font.CreateDynamicFontFromOSFont(new[]
        {
            "Microsoft JhengHei UI",
            "Microsoft JhengHei",
            "Arial",
            "Segoe UI"
        }, 18);

        return cachedRuntimeUiFont;
    }

    private static Font GetRuntimeCjkUiFont()
    {
        if (cachedRuntimeCjkFont != null)
            return cachedRuntimeCjkFont;

        cachedRuntimeCjkFont = Resources.Load<Font>("Cubic_11");
        if (cachedRuntimeCjkFont == null)
            cachedRuntimeCjkFont = GetRuntimeUiFont();

        return cachedRuntimeCjkFont;
    }

    private static void ApplyLocalizedRuntimeFont(Text text, string value, int fontSize, bool wrap, VerticalWrapMode verticalOverflow, int minBestFitSize, float cjkLineSpacing = 1f)
    {
        if (text == null)
            return;

        bool useCjkFont = ContainsCjk(value);
        text.font = useCjkFont ? GetRuntimeCjkUiFont() : GetRuntimeUiFont();
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

    private void ShowEndingSkipUi(bool visible)
    {
        if (endingSkipRoot != null)
            endingSkipRoot.gameObject.SetActive(visible);
    }

    private void EnsureTutorialCompletionSkipUi()
    {
        if (tutorialCompletionSkipRoot != null)
            return;

        GameObject canvasObject = new GameObject("BullfightTutorialVideoSkipCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 6100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject labelObject = new GameObject("TutorialVideoSkipLabel", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(canvasObject.transform, false);

        tutorialCompletionSkipRoot = labelObject.GetComponent<RectTransform>();
        tutorialCompletionSkipRoot.anchorMin = new Vector2(1f, 0f);
        tutorialCompletionSkipRoot.anchorMax = new Vector2(1f, 0f);
        tutorialCompletionSkipRoot.pivot = new Vector2(1f, 0f);
        tutorialCompletionSkipRoot.sizeDelta = new Vector2(520f, 48f);
        tutorialCompletionSkipRoot.anchoredPosition = new Vector2(-32f, 28f);
        tutorialCompletionSkipRoot.localScale = Vector3.one;
        tutorialCompletionSkipRoot.localRotation = Quaternion.identity;

        tutorialCompletionSkipLabel = labelObject.GetComponent<Text>();
        tutorialCompletionSkipLabel.alignment = TextAnchor.MiddleRight;
        tutorialCompletionSkipLabel.font = GetRuntimeUiFont();
        tutorialCompletionSkipLabel.fontSize = 24;
        tutorialCompletionSkipLabel.fontStyle = FontStyle.Normal;
        tutorialCompletionSkipLabel.color = new Color(1f, 0.98f, 0.45f, 1f);
        tutorialCompletionSkipLabel.raycastTarget = false;
        tutorialCompletionSkipLabel.text = GetTutorialCompletionSkipInstructionText();
        ApplyLocalizedRuntimeFont(tutorialCompletionSkipLabel, tutorialCompletionSkipLabel.text, 24, wrap: false, VerticalWrapMode.Truncate, 18);

        Outline outline = labelObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
        outline.effectDistance = new Vector2(2f, -2f);

        ShowTutorialCompletionSkipUi(false);
    }

    private void ShowTutorialCompletionSkipUi(bool visible)
    {
        if (tutorialCompletionSkipRoot != null)
            tutorialCompletionSkipRoot.gameObject.SetActive(visible);
    }

    private void HideSceneForEndingPlayback()
    {
        RestoreSceneAfterEndingPlayback();

        Transform[] allTransforms = FindObjectsOfType<Transform>(true);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform candidate = allTransforms[i];
            if (candidate == null || candidate.parent != null)
                continue;

            GameObject rootObject = candidate.gameObject;
            if (rootObject == null || !rootObject.activeSelf)
                continue;

            if (ShouldKeepRootVisibleDuringEnding(rootObject))
            {
                bool hasVideoPlayer = rootObject.GetComponentInChildren<VideoPlayer>(true) != null;
                bool hasCamera = rootObject.GetComponentInChildren<Camera>(true) != null;
                bool hasPlayerController = rootObject.GetComponentInChildren<BullfightPlayerController>(true) != null;
                bool hasSensorManager = rootObject.GetComponentInChildren<ArduinoTest>(true) != null;

                if (!hasVideoPlayer && (hasCamera || hasPlayerController || hasSensorManager))
                {
                    HideVisualComponentsUnderRoot(rootObject);
                }

                continue;
            }

            endingHiddenRootObjects.Add(rootObject);
            rootObject.SetActive(false);
        }

        endingHiddenDebugOverlay = FindObjectOfType<BullDebugOverlay>(true);
        if (endingHiddenDebugOverlay != null)
            endingHiddenDebugOverlay.enabled = false;
    }

    private void RestoreSceneAfterEndingPlayback()
    {
        if (endingHiddenDebugOverlay != null)
        {
            endingHiddenDebugOverlay.enabled = true;
            endingHiddenDebugOverlay = null;
        }

        for (int i = 0; i < endingHiddenRenderers.Count; i++)
        {
            Renderer hiddenRenderer = endingHiddenRenderers[i];
            if (hiddenRenderer != null)
                hiddenRenderer.enabled = true;
        }

        for (int i = 0; i < endingHiddenCanvases.Count; i++)
        {
            Canvas hiddenCanvas = endingHiddenCanvases[i];
            if (hiddenCanvas != null)
                hiddenCanvas.enabled = true;
        }

        for (int i = 0; i < endingHiddenRootObjects.Count; i++)
        {
            GameObject hiddenRoot = endingHiddenRootObjects[i];
            if (hiddenRoot != null)
                hiddenRoot.SetActive(true);
        }

        endingHiddenRootObjects.Clear();
        endingHiddenRenderers.Clear();
        endingHiddenCanvases.Clear();
    }

    private void RestoreTutorialCompletionVideoFreezeState()
    {
        if (!tutorialCompletionVideoFreezeStateCaptured)
            return;

        Time.timeScale = tutorialCompletionVideoSavedTimeScale <= 0f ? 1f : tutorialCompletionVideoSavedTimeScale;
        Time.fixedDeltaTime = tutorialCompletionVideoSavedFixedDeltaTime > 0f ? tutorialCompletionVideoSavedFixedDeltaTime : 0.02f;

        if (tutorialCompletionVideoCameraLook != null)
            tutorialCompletionVideoCameraLook.enabled = tutorialCompletionVideoSavedCameraLookEnabled;

        if (playerController != null)
            playerController.enabled = tutorialCompletionVideoSavedPlayerControllerEnabled;

        if (bullAI != null)
            bullAI.enabled = tutorialCompletionVideoSavedBullAiEnabled;

        RestoreTutorialCompletionVideoUi();
        tutorialCompletionVideoCameraLook = null;
        tutorialCompletionVideoFreezeStateCaptured = false;
    }

    private void HideTutorialCompletionVideoUi()
    {
        RestoreTutorialCompletionVideoUi();

        string[] uiRootNames =
        {
            "HUD_Canvas",
            "QTE_Runtime_Canvas",
            "P_LPSP_UI_Canvas",
            "P_LPSP_UI_Canvas(Clone)",
            "BullfightStunOverlay",
            "BullfightPerfectDodgeOverlay",
            "SK_FP_CH_Default_Cubic_v2",
            "SK_FP_CH_Default_Cubic"
        };

        System.Collections.Generic.HashSet<GameObject> resolvedRoots = new System.Collections.Generic.HashSet<GameObject>();
        for (int i = 0; i < uiRootNames.Length; i++)
        {
            Transform sceneTransform = BullfightSceneCache.FindSceneObjectByName<Transform>(uiRootNames[i]);
            if (sceneTransform != null)
                resolvedRoots.Add(sceneTransform.gameObject);
        }

        BullfightHudController hudController = BullfightHudController.Instance != null
            ? BullfightHudController.Instance
            : FindObjectOfType<BullfightHudController>(true);
        if (hudController != null)
            resolvedRoots.Add(hudController.gameObject);

        BullfightPauseSettingsUI pauseSettingsUi = BullfightPauseSettingsUI.Instance != null
            ? BullfightPauseSettingsUI.Instance
            : FindObjectOfType<BullfightPauseSettingsUI>(true);
        if (pauseSettingsUi != null)
            resolvedRoots.Add(pauseSettingsUi.gameObject);

        foreach (GameObject uiRoot in resolvedRoots)
        {
            if (uiRoot == null || !uiRoot.activeSelf)
                continue;

            tutorialCompletionHiddenUiRoots.Add(uiRoot);
            uiRoot.SetActive(false);
        }
    }

    private void RestoreTutorialCompletionVideoUi()
    {
        for (int i = 0; i < tutorialCompletionHiddenUiRoots.Count; i++)
        {
            GameObject hiddenRoot = tutorialCompletionHiddenUiRoots[i];
            if (hiddenRoot != null)
                hiddenRoot.SetActive(true);
        }

        tutorialCompletionHiddenUiRoots.Clear();
    }

    private bool ShouldKeepRootVisibleDuringEnding(GameObject rootObject)
    {
        if (rootObject == null)
            return true;

        if (rootObject == gameObject)
            return true;

        if (endingVideoPlayer != null && rootObject == endingVideoPlayer.transform.root.gameObject)
            return true;

        if (rootObject.GetComponentInChildren<BullfightGameFlow>(true) != null)
            return true;

        if (rootObject.GetComponentInChildren<BullfightStartMenu>(true) != null)
            return true;

        if (rootObject.GetComponentInChildren<VideoPlayer>(true) != null)
            return true;

        if (rootObject.GetComponentInChildren<Camera>(true) != null)
            return true;

        if (rootObject.GetComponentInChildren<BullfightPlayerController>(true) != null)
            return true;

        if (rootObject.GetComponentInChildren<ArduinoTest>(true) != null)
            return true;

        if (rootObject.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true) != null)
            return true;

        return false;
    }

    private void HideVisualComponentsUnderRoot(GameObject rootObject)
    {
        Renderer[] renderers = rootObject.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            endingHiddenRenderers.Add(renderer);
            renderer.enabled = false;
        }

        Canvas[] canvases = rootObject.GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || !canvas.enabled)
                continue;

            endingHiddenCanvases.Add(canvas);
            canvas.enabled = false;
        }
    }

    private void ReturnToStartMenuAfterEnding()
    {
        ReloadCurrentSceneToHomeMenu();
    }

    public void ResetSceneForMainMenu()
    {
        ResolveReferencesIfNeeded();
        ResetState();
        bullBleedVfx?.ClearBleeds();
        phaseTwoPresentation?.ExitPhaseTwo();
        audioController?.StopAllAudio();

        Time.timeScale = 1f;
        ApplySkyboxForPhase(GamePhase.PhaseOne);

        playerController?.ClearInputBuffers();
        playerController?.ForceStopMovement();

        if (spawnManager != null)
        {
            spawnManager.ResetPlayerToSpawn();
            spawnManager.ResetBullToSpawn();
        }

        bullStats?.ResetCombatState();
        bullAI?.ResetCombatState();
        bullAI?.SetTutorialControl(false);

        playerStats?.ResetCombatState();
        playerStats?.SetShooterGameplayEnabled(false);

        if (timingRing != null)
        {
            timingRing.HideImmediate();
            timingRing.ResetTimingWindow();
        }
    }

    public void ReloadCurrentSceneToHomeMenu()
    {
        ResolveReferencesIfNeeded();
        StopTutorialCompletionVideoPlayback();
        StopEndingVideoPlayback();
        phaseTwoPresentation?.ExitPhaseTwo();
        bullAI?.SetTutorialControl(false);
        ResetArcadeRuntimeState();

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        ApplySkyboxForPhase(GamePhase.PhaseOne);

        playerController?.ClearInputBuffers();
        playerController?.ForceStopMovement();

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
            return;

        if (activeScene.buildIndex >= 0)
            SceneManager.LoadScene(activeScene.buildIndex);
        else
            SceneManager.LoadScene(activeScene.name);
    }

    private void ResolveReferencesIfNeeded()
    {
        if (playerStats == null)
            playerStats = BullfightSceneCache.FindObject<PlayerStats>();

        if (bullStats == null)
            bullStats = BullfightSceneCache.FindObject<BullStats>();

        if (bullAI == null)
            bullAI = BullfightSceneCache.FindObject<BullAI>();

        if (bullBleedVfx == null && bullAI != null)
            bullBleedVfx = bullAI.GetComponent<BullBleedVfx>();

        if (playerController == null)
            playerController = BullfightSceneCache.FindObject<BullfightPlayerController>();

        if (timingRing == null)
            timingRing = BullfightSceneCache.FindObject<BullTimingRing>();

        if (spawnManager == null && playerStats != null)
            spawnManager = playerStats.GetComponent<BullfightSpawnManager>();

        if (spawnManager == null)
            spawnManager = BullfightSceneCache.FindObject<BullfightSpawnManager>();

        if (phaseTwoPresentation == null)
            phaseTwoPresentation = GetComponent<BullfightPhaseTwoPresentation>() ?? BullfightSceneCache.FindObject<BullfightPhaseTwoPresentation>();

        if (phaseTwoPresentation == null)
            phaseTwoPresentation = gameObject.AddComponent<BullfightPhaseTwoPresentation>();

        if (audioController == null)
            audioController = BullfightSceneCache.FindObject<BullfightAudioController>();

        if (endingVideoPlayer == null)
            endingVideoPlayer = FindObjectOfType<VideoPlayer>(true);

        if (arduinoTest == null)
            arduinoTest = BullfightSceneCache.FindObject<ArduinoTest>();

        RefreshTutorialSubscriptions();
    }

    private CameraLook ResolveTutorialCompletionVideoCameraLook()
    {
        if (playerStats != null)
        {
            CameraLook localCameraLook = playerStats.GetComponentInChildren<CameraLook>(true);
            if (localCameraLook != null)
                return localCameraLook;
        }

        return FindObjectOfType<CameraLook>(true);
    }

    private void ResetBullForPhaseTwoRound(int sideSign = 0)
    {
        float sideMagnitude = Mathf.Max(Mathf.Abs(phaseTwoRoundSideOffset), GetPhaseTwoActiveSideOffset());
        float resolvedSideOffset = sideSign == 0
            ? phaseTwoApproachSideSign * GetPhaseTwoActiveSideOffset()
            : sideMagnitude * sideSign;
        float frontDistance = Mathf.Max(phaseTwoBullFrontDistance, GetPhaseTwoMinimumPlayerDistance() + 0.95f);
        spawnManager?.ResetBullForPhaseTwoRound(frontDistance, resolvedSideOffset);
        bullAI?.ForceSnapToGround();
        EnsureBullVisibleForPhaseTwoWindow(true);
    }

    private float GetPhaseTwoMinimumPlayerDistance()
    {
        return Mathf.Max(1.35f, phaseTwoMinimumPlayerDistance);
    }

    private Vector3 ClampPhaseTwoBullPositionToArena(Vector3 position, float padding = 0.2f)
    {
        if (spawnManager == null)
            return position;

        Vector3 center = spawnManager.ArenaCenter;
        float radius = Mathf.Max(0.5f, spawnManager.ArenaRadius - Mathf.Max(0f, padding));
        Vector3 offset = new Vector3(position.x - center.x, 0f, position.z - center.z);
        if (offset.sqrMagnitude <= radius * radius)
            return position;

        offset = offset.normalized * radius;
        return new Vector3(center.x + offset.x, position.y, center.z + offset.z);
    }

    private Vector3 ClampPhaseTwoTargetToPlayerView(Vector3 target, float preferredDistance)
    {
        if (playerStats == null)
            return target;

        Vector3 playerPosition = playerStats.transform.position;
        Vector3 playerForward = playerStats.transform.forward;
        playerForward.y = 0f;
        if (playerForward.sqrMagnitude <= 0.0001f)
            playerForward = Vector3.forward;
        playerForward.Normalize();

        Vector3 flatOffset = target - playerPosition;
        flatOffset.y = 0f;
        float distance = Mathf.Max(GetPhaseTwoMinimumPlayerDistance(), flatOffset.magnitude > 0.001f ? flatOffset.magnitude : preferredDistance);
        Vector3 direction = flatOffset.sqrMagnitude > 0.0001f ? flatOffset.normalized : playerForward;
        float dot = Vector3.Dot(playerForward, direction);
        if (dot < phaseTwoVisibleViewDot)
            direction = Vector3.Slerp(playerForward, direction, 0.35f).normalized;

        Vector3 clamped = playerPosition + direction * distance;
        clamped.y = target.y;
        return clamped;
    }

    private void EnsureBullVisibleForPhaseTwoWindow(bool forceSnap = false)
    {
        if (bullAI == null || playerStats == null)
            return;

        Vector3 playerPosition = playerStats.transform.position;
        Vector3 toBull = bullAI.transform.position - playerPosition;
        toBull.y = 0f;

        Vector3 playerForward = playerStats.transform.forward;
        playerForward.y = 0f;
        if (playerForward.sqrMagnitude <= 0.0001f)
            playerForward = Vector3.forward;
        playerForward.Normalize();

        float distance = toBull.magnitude;
        float dot = toBull.sqrMagnitude > 0.0001f ? Vector3.Dot(playerForward, toBull.normalized) : -1f;
        if (!forceSnap && distance >= GetPhaseTwoMinimumPlayerDistance() && dot >= phaseTwoVisibleViewDot)
            return;

        float desiredDistance = Mathf.Max(GetPhaseTwoMinimumPlayerDistance() + 0.35f, phaseTwoBullFrontDistance * 0.92f);
        Vector3 desiredPosition = playerPosition + playerForward * desiredDistance;
        if (phaseTwoUseSideCut)
        {
            Vector3 right = new Vector3(playerForward.z, 0f, -playerForward.x).normalized;
            desiredPosition += right * (phaseTwoApproachSideSign * Mathf.Min(GetPhaseTwoActiveSideOffset() * 0.55f, desiredDistance * 0.45f));
        }

        desiredPosition.y = bullAI.transform.position.y;
        Vector3 lookDirection = playerPosition - desiredPosition;
        lookDirection.y = 0f;
        if (lookDirection.sqrMagnitude <= 0.0001f)
            lookDirection = -playerForward;

        bullAI.SetPhaseTwoPose(desiredPosition, Quaternion.LookRotation(lookDirection.normalized, Vector3.up));
    }
    private float GetActiveRoundPrepareDuration()
    {
        return Mathf.Max(roundPrepareDuration, 1.8f) + Mathf.Max(0.9f, GetRoundStanceDuration());
    }

    private float GetActiveRoundWindowDuration()
    {
        return Mathf.Max(roundWindowDuration, 1.7f);
    }

    private float GetRoundStanceDuration()
    {
        return Mathf.Max(0f, roundStanceConfirmDuration);
    }

    private float GetRoundStanceProgress()
    {
        if (phaseTwoState != PhaseTwoState.RoundPrepare)
            return 0f;

        float stanceDuration = GetRoundStanceDuration();
        if (stanceDuration <= 0f)
            return 1f;

        return Mathf.Clamp01(phaseTwoStateElapsed / stanceDuration);
    }

    private string GetPhaseTwoReflectionLine()
    {
        if (phaseTwoReflectionLines == null || phaseTwoReflectionLines.Length == 0)
            return string.Empty;

        int clampedIndex = Mathf.Clamp(Mathf.Max(phaseTwoRoundIndex, 1) - 1, 0, phaseTwoReflectionLines.Length - 1);
        return phaseTwoReflectionLines[clampedIndex] ?? string.Empty;
    }

    private string GetPhaseTwoCalibrationLine()
    {
        if (phaseTwoState != PhaseTwoState.Calibration)
            return string.Empty;

        if (!ShouldShowSensorPhaseTwoPrompt())
            return $"\u8acb\u4ee5 {GetPhaseTwoCalibrationLabel()} \u958b\u59cb\u6821\u6e96\u3002";

        if (IsWaitingForSensorCalibrationSignal())
            return "\u7a69\u5b9a\u6301\u528d\uff0c\u6e96\u5099\u958b\u59cb\u6821\u6e96\u3002";

        return "\u7a69\u5b9a\u6301\u528d 5 \u79d2\u5b8c\u6210\u6821\u6e96\u3002";
    }

    private string GetPhaseTwoCalibrationStatus()
    {
        return phaseTwoState == PhaseTwoState.Calibration ? phaseTwoCalibrationStatusText : string.Empty;
    }

    private void ResetPhaseTwoCalibrationAnchor()
    {
        calibrationAnchorTimer = 0f;
        calibrationAnchorValue = 0f;
        phaseTwoCalibrationAnchorLocked = false;
    }

    private string GetRandomPhaseTwoReflectionLine()
    {
        if (phaseTwoReflectionLines == null || phaseTwoReflectionLines.Length == 0)
            return string.Empty;

        int randomIndex = Random.Range(0, phaseTwoReflectionLines.Length);
        return phaseTwoReflectionLines[randomIndex] ?? string.Empty;
    }

    private void RequestPhaseTwoSensorCalibration()
    {
        playerController?.ResetPhaseTwoSensorState();
        arduinoTest?.BeginPhaseTwoCalibration();
    }

    private void ResolvePhaseTwoShuttlePoints(Vector3 centerPoint)
    {
        float sideDistance = Mathf.Max(Mathf.Max(0.45f, phaseTwoRoundSideOffset), phaseTwoSideOffsetAmount);
        float forwardBias = Mathf.Max(0.2f, phaseTwoResolveWalkFrontDistance * 0.22f);

        if (playerStats == null)
        {
            phaseTwoResolveCenterPoint = centerPoint;
            phaseTwoResolveLeftPoint = centerPoint + (Vector3.left * sideDistance) + (Vector3.forward * forwardBias);
            phaseTwoResolveRightPoint = centerPoint + (Vector3.right * sideDistance) + (Vector3.forward * forwardBias);
            return;
        }

        Transform playerTransform = playerStats.transform;
        Vector3 forward = playerTransform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
            forward = Vector3.forward;

        forward.Normalize();
        Vector3 right = new Vector3(forward.z, 0f, -forward.x).normalized;

        phaseTwoResolveCenterPoint = centerPoint;
        phaseTwoResolveLeftPoint = centerPoint - right * sideDistance + forward * forwardBias;
        phaseTwoResolveRightPoint = centerPoint + right * sideDistance + forward * forwardBias;
        phaseTwoResolveLeftPoint.y = centerPoint.y;
        phaseTwoResolveRightPoint.y = centerPoint.y;
    }

    private void PreparePhaseTwoMovementPattern()
    {
        phaseTwoApproachSideSign = Random.value < 0.5f ? -1 : 1;
        phaseTwoUseSideCut = Random.value < Mathf.Clamp01(phaseTwoSideCutChance);
        phaseTwoUsePressureApproach = Random.value < Mathf.Clamp01(pressureApproachChance);
    }

    private float GetPhaseTwoActiveSideOffset()
    {
        return Mathf.Max(Mathf.Abs(phaseTwoBullSideOffset), phaseTwoSideOffsetAmount);
    }

    private void UpdatePhaseTwoRoundApproach()
    {
        if (bullAI == null || playerStats == null)
            return;

        Vector3 current = bullAI.transform.position;
        Vector3 target = GetPhaseTwoRoundApproachTarget(current);
        target.y = current.y;

        float approachSpeed = phaseTwoUsePressureApproach
            ? Mathf.Max(0.35f, pressureApproachSpeed)
            : Mathf.Max(0.35f, phaseTwoShuttleRunSpeed * 0.45f);
        Vector3 nextPosition = Vector3.MoveTowards(current, target, approachSpeed * Time.unscaledDeltaTime);
        Vector3 moveDirection = target - current;
        moveDirection.y = 0f;
        Quaternion nextRotation = moveDirection.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(moveDirection.normalized, Vector3.up)
            : bullAI.transform.rotation;

        bullAI.SetPhaseTwoPose(nextPosition, nextRotation);
    }

    private Vector3 GetPhaseTwoRoundApproachTarget(Vector3 currentBullPosition)
    {
        if (playerStats == null)
            return currentBullPosition;

        Vector3 toBull = currentBullPosition - playerStats.transform.position;
        toBull.y = 0f;
        if (toBull.sqrMagnitude <= 0.0001f)
            toBull = Vector3.forward;
        toBull.Normalize();

        Vector3 right = new Vector3(toBull.z, 0f, -toBull.x).normalized;
        float minimumDistance = GetPhaseTwoMinimumPlayerDistance();
        float frontDistance = phaseTwoUsePressureApproach
            ? Mathf.Max(minimumDistance + 0.2f, phaseTwoBullFrontDistance * 0.68f)
            : Mathf.Max(minimumDistance + 0.35f, phaseTwoBullFrontDistance * 0.45f);
        float sideOffset = phaseTwoUseSideCut ? phaseTwoApproachSideSign * GetPhaseTwoActiveSideOffset() : 0f;
        if (!phaseTwoUseSideCut && phaseTwoUsePressureApproach)
            sideOffset = phaseTwoApproachSideSign * Mathf.Max(0.35f, GetPhaseTwoActiveSideOffset() * 0.45f);
        Vector3 target = playerStats.transform.position - (toBull * frontDistance);
        target += right * sideOffset;
        return ClampPhaseTwoTargetToPlayerView(target, frontDistance);
    }

    private Vector3 GetPhaseTwoMissApproachTarget(Vector3 currentBullPosition, Vector3 fallbackDirection)
    {
        if (playerStats == null)
            return currentBullPosition;

        Vector3 direction = fallbackDirection;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = playerStats.transform.position - currentBullPosition;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.forward;
        direction.Normalize();

        Vector3 right = new Vector3(direction.z, 0f, -direction.x).normalized;
        Vector3 target = playerStats.transform.position - direction * Mathf.Max(GetPhaseTwoMinimumPlayerDistance(), 1.45f);
        if (phaseTwoUseSideCut)
            target += right * (phaseTwoApproachSideSign * GetPhaseTwoActiveSideOffset());
        target.y = currentBullPosition.y;
        return ClampPhaseTwoTargetToPlayerView(target, GetPhaseTwoMinimumPlayerDistance());
    }
    private void OnDestroy()
    {
        StopEndingVideoPlayback();
        UnsubscribeTutorialSignals();
        phaseTwoPresentation?.ExitPhaseTwo();
        Time.timeScale = 1f;
        ApplySkyboxForPhase(GamePhase.PhaseOne);
    }

    private void HandleTutorialCapaTimingResult(string result)
    {
        tutorialCapaQteResolved = true;
        bullAI?.ResolveExternalChargeTimingResult(result);
    }

    private void HandleTutorialBanderillasPerformed()
    {
        if (currentPhase != GamePhase.PhaseZeroTutorial || tutorialState != TutorialState.Attack)
            return;

        tutorialAttackPerformed = true;
        tutorialAttackResolveAt = Time.time + tutorialAttackResolveDelay;
    }

    private void HandleTutorialBanderillasHit(float damage)
    {
        if (currentPhase != GamePhase.PhaseZeroTutorial || tutorialState != TutorialState.Attack)
            return;

        if (damage <= 0f)
            return;

        tutorialAttackCleanHitRegistered = true;
        tutorialAttackResolveAt = -1f;
    }

    private void RefreshTutorialSubscriptions()
    {
        if (subscribedTutorialPlayerStats != playerStats)
        {
            if (subscribedTutorialPlayerStats != null)
                subscribedTutorialPlayerStats.OnBanderillasPerformed -= HandleTutorialBanderillasPerformed;

            subscribedTutorialPlayerStats = playerStats;
            if (subscribedTutorialPlayerStats != null)
                subscribedTutorialPlayerStats.OnBanderillasPerformed += HandleTutorialBanderillasPerformed;
        }

        if (subscribedTutorialBullAI != bullAI)
        {
            if (subscribedTutorialBullAI != null)
                subscribedTutorialBullAI.OnBanderillasHit -= HandleTutorialBanderillasHit;

            subscribedTutorialBullAI = bullAI;
            if (subscribedTutorialBullAI != null)
                subscribedTutorialBullAI.OnBanderillasHit += HandleTutorialBanderillasHit;
        }
    }

    private void UnsubscribeTutorialSignals()
    {
        if (subscribedTutorialPlayerStats != null)
            subscribedTutorialPlayerStats.OnBanderillasPerformed -= HandleTutorialBanderillasPerformed;

        if (subscribedTutorialBullAI != null)
            subscribedTutorialBullAI.OnBanderillasHit -= HandleTutorialBanderillasHit;

        subscribedTutorialPlayerStats = null;
        subscribedTutorialBullAI = null;
    }
}









