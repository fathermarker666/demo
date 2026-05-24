using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BullfightAudioController : MonoBehaviour
{
    [System.Serializable]

    private class AudioCue
    {
        public AudioClip clip;
        [Range(0f, 1.5f)] public float volume = 1f;
        [Range(0.5f, 1.5f)] public float pitch = 1f;
        [Range(0f, 1f)] public float spatialBlend = 0f;
    }

    private sealed class ManagedSfxSource
    {
        public AudioSource Source;
        public float BaseVolume;
    }

    private enum PlaceholderCue
    {
        ClothRaise, ClothLower, Capa, Evade, Banderillas,
        SwordStab,
        PlayerHit, PlayerStun, PlayerRecover, BullTelegraph,
        BullCharge, BullFatigue, BullHurt, BullDeath,
        TimingPerfect, TimingGood, TimingMiss,
        HeartbeatLoop, CrowdCheer, CrowdDisappointment,
        PlayerBreathLoop
    }

    private enum BullStateAudioMode
    {
        None,
        Telegraph,
        Charge,
        Fatigue
    }

    private delegate float ProceduralSample(int sampleIndex, float time, float progress);

    [Header("Bull Ambient SFX")]
    [SerializeField] private AudioCue bullIdleCueA;
    [SerializeField] private AudioCue bullIdleCueB;
    [SerializeField] private Vector2 idleIntervalRange = new Vector2(3f, 7f);
    [SerializeField] private float proximityMaxDistance = 20f;
    [SerializeField] private float proximityMinDistance = 2f;
    [SerializeField] private float proximityVolumeMultiplier = 1.5f;

    [Header("Background Music")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioClip[] phaseBGMClips;
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.5f;

    [Header("General")]
    [SerializeField] private bool useProceduralFallback = true;
    [SerializeField, Range(0f, 1f)] private float masterVolume = 0.9f;
    [SerializeField] private Transform playerAnchor;
    [SerializeField] private Transform bullAnchor;

    [Header("Player SFX")]
    [SerializeField] private AudioCue clothRaiseCue = new AudioCue { volume = 0.35f, pitch = 1.08f, spatialBlend = 0.05f };
    [SerializeField] private AudioCue clothLowerCue = new AudioCue { volume = 0.3f, pitch = 0.96f, spatialBlend = 0.05f };
    [SerializeField] private AudioCue capaCue = new AudioCue { volume = 0.6f, pitch = 1.04f, spatialBlend = 0.1f };
    [SerializeField] private AudioCue evadeCue = new AudioCue { volume = 0.55f, pitch = 1.08f, spatialBlend = 0.1f };
    [SerializeField] private AudioCue banderillasCue = new AudioCue { volume = 0.7f, pitch = 0.98f, spatialBlend = 0.2f };
    [SerializeField] private AudioCue swordStabCue = new AudioCue { volume = 0.78f, pitch = 1.03f, spatialBlend = 0.18f };
    [SerializeField] private AudioCue playerHitCue = new AudioCue { volume = 0.78f, pitch = 0.94f, spatialBlend = 0.15f };
    [SerializeField] private AudioCue playerStunCue = new AudioCue { volume = 0.55f, pitch = 0.9f, spatialBlend = 0f };
    [SerializeField] private AudioCue playerRecoverCue = new AudioCue { volume = 0.45f, pitch = 1.05f, spatialBlend = 0f };
    [SerializeField] private AudioCue playerDeathCue;

    [Header("Bull SFX")]
    [SerializeField] private AudioCue bullTelegraphCue = new AudioCue { volume = 0.8f, pitch = 0.96f, spatialBlend = 1f };
    [SerializeField] private AudioCue bullChargeCue = new AudioCue { volume = 0.9f, pitch = 1f, spatialBlend = 1f };
    [SerializeField] private AudioCue bullFatigueCue = new AudioCue { volume = 0.75f, pitch = 0.92f, spatialBlend = 1f };
    [SerializeField] private AudioCue bullHurtCue = new AudioCue { volume = 0.85f, pitch = 0.95f, spatialBlend = 1f };
    [SerializeField] private AudioCue bullDeathCue = new AudioCue { volume = 1f, pitch = 0.9f, spatialBlend = 1f };

    [Header("QTE SFX")]
    [SerializeField] private AudioCue timingPerfectCue = new AudioCue { volume = 0.45f, pitch = 1.05f, spatialBlend = 0f };
    [SerializeField] private AudioCue timingGoodCue = new AudioCue { volume = 0.42f, pitch = 1f, spatialBlend = 0f };
    [SerializeField] private AudioCue timingMissCue = new AudioCue { volume = 0.5f, pitch = 0.95f, spatialBlend = 0f };

    [Header("Feedback SFX")]
    [SerializeField] private AudioCue heartbeatLoopCue = new AudioCue { volume = 0.22f, pitch = 1f, spatialBlend = 0f };
    [SerializeField] private AudioCue playerBreathLoopCue = new AudioCue { volume = 0.24f, pitch = 1f, spatialBlend = 0f };
    [SerializeField] private AudioCue crowdCheerCue = new AudioCue { volume = 0.4f, pitch = 1f, spatialBlend = 1f };
    [SerializeField] private AudioCue crowdDisappointmentCue = new AudioCue { volume = 0.38f, pitch = 0.96f, spatialBlend = 1f };
    [SerializeField] private float crowdCheerCooldown = 0.45f;

    private const string BgmVolumePrefKey = "Bullfight.Audio.BgmVolume";
    private const string SfxVolumePrefKey = "Bullfight.Audio.SfxVolume";

    private readonly Dictionary<PlaceholderCue, AudioClip> placeholderClips = new Dictionary<PlaceholderCue, AudioClip>();
    private readonly List<ManagedSfxSource> activeSfxSources = new List<ManagedSfxSource>();

    private PlayerStats playerStats;
    private BullStats bullStats;
    private BullAI bullAI;
    private BullTimingRing timingRing;
    private BullfightGameFlow gameFlow;
    private BullfightSpawnManager spawnManager;
    private ManualStartMenuController manualStartMenu;
    private BullfightStartMenu startMenu;

    private PlayerStats subscribedPlayerStats;
    private BullStats subscribedBullStats;
    private BullTimingRing subscribedTimingRing;
    private BullAI trackedBullAI;
    private BullfightArcadeScoring trackedArcadeScoring;
    private AudioSource bullStateSource;
    private AudioSource bullIdleSource;
    private AudioSource heartbeatLoopSource;
    private AudioSource playerBreathLoopSource;
    private BullStateAudioMode activeBullStateAudioMode;
    private float idleTimer = 0f;
    private bool sfxSuppressed;
    private bool temporaryGameplaySfxBlocked;
    private float lastCrowdReactionAt = -999f;

    private void Awake()
    {
        ResolveReferencesIfNeeded();
        EnsureRuntimeSources();
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
        }

        LoadSavedVolumes();
        ApplyBgmVolume();
    }

    private void OnEnable() { ResolveReferencesIfNeeded(); RefreshBindings(); }
    private void Start()
    {
        ResolveReferencesIfNeeded();
        EnsureRuntimeSources();
        RefreshBindings();
    }

    private void Update()
    {
        if (HasMissingReferences()) ResolveReferencesIfNeeded();
        EnsureRuntimeSources();
        if (playerAnchor == null && playerStats != null) playerAnchor = playerStats.transform;
        if (bullAnchor == null && bullAI != null) bullAnchor = bullAI.transform;
        RefreshBindings();
        RefreshArcadeBindings();
        CleanupManagedSfxSources();
        ApplyBgmVolume();
        UpdateRuntimeSourceAnchors();
        SyncTemporaryGameplayBlockState();
        SyncBullStateAudio();
        SyncHeartbeatLoop();
        SyncPlayerBreathLoop();
        HandleBullIdleAmbient();
    }

    private void OnDisable()
    {
        UnbindAll();
        UnbindArcadeScoring();
        StopGameplaySfx();
    }

    public float BgmVolume => bgmVolume;
    public float SfxVolume => masterVolume;

    public void SetBgmVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        ApplyBgmVolume();
        PlayerPrefs.SetFloat(BgmVolumePrefKey, bgmVolume);
        PlayerPrefs.Save();
    }

    public void SetSfxVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        RefreshManagedSfxVolumes();
        PlayerPrefs.SetFloat(SfxVolumePrefKey, masterVolume);
        PlayerPrefs.Save();
    }

    // --- BGM Logic ---
    public void PlayPhaseBGM(int phaseIndex)
    {
        sfxSuppressed = false;
        Debug.Log("[Audio] PlayPhaseBGM called: " + phaseIndex);

        if (phaseBGMClips == null || phaseIndex < 0 || phaseIndex >= phaseBGMClips.Length)
        {
            Debug.LogWarning("[Audio] phaseBGMClips invalid");
            return;
        }

        if (bgmSource == null)
        {
            Debug.LogWarning("[Audio] bgmSource is null");
            return;
        }

        AudioClip nextClip = phaseBGMClips[phaseIndex];

        if (nextClip == null)
        {
            Debug.LogWarning("[Audio] nextClip is null");
            return;
        }

        StopAllCoroutines();

        bgmSource.Stop();
        bgmSource.clip = nextClip;
        bgmSource.volume = bgmVolume;
        bgmSource.loop = true;
        bgmSource.spatialBlend = 0f;
        bgmSource.Play();

        Debug.Log("[Audio] force play clip = " + nextClip.name + " / isPlaying = " + bgmSource.isPlaying);
    }
    private void LoadSavedVolumes()
    {
        bgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumePrefKey, bgmVolume));
        masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumePrefKey, masterVolume));
    }

    private void ApplyBgmVolume()
    {
        if (bgmSource != null)
            bgmSource.volume = bgmVolume;
    }

    private void RefreshManagedSfxVolumes()
    {
        CleanupManagedSfxSources();
        for (int i = 0; i < activeSfxSources.Count; i++)
        {
            ManagedSfxSource entry = activeSfxSources[i];
            if (entry?.Source == null)
                continue;

            entry.Source.volume = Mathf.Max(0f, entry.BaseVolume) * masterVolume;
        }
    }

    private void CleanupManagedSfxSources()
    {
        for (int i = activeSfxSources.Count - 1; i >= 0; i--)
        {
            ManagedSfxSource entry = activeSfxSources[i];
            if (entry == null || entry.Source == null)
                activeSfxSources.RemoveAt(i);
        }
    }

    public void StopBGM()
    {
        if (bgmSource == null) return;

        bgmSource.Stop();
        bgmSource.clip = null;
    }

    public void StopAllAudio()
    {
        sfxSuppressed = true;
        StopAllCoroutines();
        StopBGM();
        idleTimer = 0f;
        StopGameplaySfx();
    }

    public void PlayPhaseTwoStabCue()
    {
        if (HasMissingReferences())
            ResolveReferencesIfNeeded();

        if (playerAnchor == null && playerStats != null)
            playerAnchor = playerStats.transform;

        PlayCue(swordStabCue, PlaceholderCue.SwordStab, playerAnchor);
    }

    private IEnumerator FadeSwitchBGM(AudioClip nextClip)
    {
        Debug.Log("[Audio] FadeSwitchBGM start: " + (nextClip != null ? nextClip.name : "NULL"));
        float duration = 1.0f;
        float timer = 0;
        if (bgmSource.isPlaying)
        {
            while (timer < duration)
            {
                timer += Time.deltaTime;
                bgmSource.volume = Mathf.Lerp(bgmVolume, 0, timer / duration);
                yield return null;
            }
        }
        bgmSource.clip = nextClip;
        bgmSource.Play();
        timer = 0;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(0, bgmVolume, timer / duration);
            yield return null;
        }
    }

    // --- Original Logic Refined ---
    private bool HasMissingReferences() => playerStats == null || bullAI == null || bullStats == null || timingRing == null || gameFlow == null;

    private void ResolveReferencesIfNeeded()
    {
        if (playerStats == null) playerStats = BullfightSceneCache.GetLocalOrScene<PlayerStats>(this);
        if (playerAnchor == null) playerAnchor = playerStats != null ? playerStats.transform : transform;
        if (spawnManager == null && playerStats != null) spawnManager = playerStats.GetComponent<BullfightSpawnManager>();
        if (bullAI == null) bullAI = BullfightSceneCache.FindObject<BullAI>();
        if (bullStats == null) bullStats = bullAI != null ? bullAI.GetComponent<BullStats>() : BullfightSceneCache.FindObject<BullStats>();
        if (bullAnchor == null && bullAI != null) bullAnchor = bullAI.transform;
        if (timingRing == null) timingRing = BullfightSceneCache.FindObject<BullTimingRing>();
        if (gameFlow == null) gameFlow = BullfightSceneCache.FindObject<BullfightGameFlow>();
        if (spawnManager == null) spawnManager = BullfightSceneCache.FindObject<BullfightSpawnManager>();
        if (manualStartMenu == null) manualStartMenu = FindObjectOfType<ManualStartMenuController>(true);
        if (startMenu == null) startMenu = FindObjectOfType<BullfightStartMenu>(true);
    }

    private void EnsureRuntimeSources()
    {
        bullStateSource ??= GetOrCreateRuntimeSource("BullStateSource", true);
        bullIdleSource ??= GetOrCreateRuntimeSource("BullIdleSource", false);
        heartbeatLoopSource ??= GetOrCreateRuntimeSource("HeartbeatLoopSource", true);
        playerBreathLoopSource ??= GetOrCreateRuntimeSource("PlayerBreathLoopSource", true);
    }

    private AudioSource GetOrCreateRuntimeSource(string objectName, bool loop)
    {
        Transform existing = transform.Find(objectName);
        AudioSource source = existing != null ? existing.GetComponent<AudioSource>() : null;
        if (source == null)
        {
            GameObject runtimeObject = existing != null ? existing.gameObject : new GameObject(objectName);
            runtimeObject.transform.SetParent(transform, false);
            source = runtimeObject.GetComponent<AudioSource>();
            if (source == null)
                source = runtimeObject.AddComponent<AudioSource>();
        }

        source.playOnAwake = false;
        source.loop = loop;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1f;
        source.maxDistance = 18f;
        source.dopplerLevel = 0f;
        return source;
    }

    private void RefreshBindings()
    {
        if (subscribedPlayerStats != playerStats)
        {
            if (subscribedPlayerStats != null) UnbindPlayer(subscribedPlayerStats);
            subscribedPlayerStats = playerStats;
            if (subscribedPlayerStats != null) BindPlayer(subscribedPlayerStats);
        }
        if (subscribedBullStats != bullStats)
        {
            if (subscribedBullStats != null) UnbindBull(subscribedBullStats);
            subscribedBullStats = bullStats;
            if (subscribedBullStats != null) BindBull(subscribedBullStats);
        }
        if (subscribedTimingRing != timingRing)
        {
            if (subscribedTimingRing != null) UnbindTiming(subscribedTimingRing);
            subscribedTimingRing = timingRing;
            if (subscribedTimingRing != null) BindTiming(subscribedTimingRing);
        }
        if (trackedBullAI != bullAI)
        {
            trackedBullAI = bullAI;
            activeBullStateAudioMode = BullStateAudioMode.None;
            if (trackedBullAI != null) bullAnchor = trackedBullAI.transform;
        }
    }

    private void RefreshArcadeBindings()
    {
        BullfightArcadeScoring nextArcadeScoring = gameFlow != null ? gameFlow.ArcadeScoring : null;
        if (trackedArcadeScoring == nextArcadeScoring)
            return;

        UnbindArcadeScoring();
        trackedArcadeScoring = nextArcadeScoring;
        if (trackedArcadeScoring != null)
            trackedArcadeScoring.ScoreEventQueued += HandleArcadeScoreEventQueued;
    }

    private void BindPlayer(PlayerStats stats)
    {
        stats.OnHoldingClothChanged += HandleHoldingClothChanged;
        stats.OnCapaPerformed += HandleCapaPerformed;
        stats.OnEvadePerformed += HandleEvadePerformed;
        stats.OnBanderillasPerformed += HandleBanderillasPerformed;
        stats.OnDamaged += HandlePlayerDamaged;
        stats.OnStunStateChanged += HandleStunStateChanged;
        stats.OnDeath += HandlePlayerDeath;
    }

    private void UnbindPlayer(PlayerStats stats)
    {
        stats.OnHoldingClothChanged -= HandleHoldingClothChanged;
        stats.OnCapaPerformed -= HandleCapaPerformed;
        stats.OnEvadePerformed -= HandleEvadePerformed;
        stats.OnBanderillasPerformed -= HandleBanderillasPerformed;
        stats.OnDamaged -= HandlePlayerDamaged;
        stats.OnStunStateChanged -= HandleStunStateChanged;
        stats.OnDeath -= HandlePlayerDeath;
    }

    private void BindBull(BullStats stats) { stats.OnDamaged += HandleBullDamaged; stats.OnDefeated += HandleBullDefeated; }
    private void UnbindBull(BullStats stats) { stats.OnDamaged -= HandleBullDamaged; stats.OnDefeated -= HandleBullDefeated; }
    private void BindTiming(BullTimingRing ring) { ring.OnTimingResolved += HandleTimingResolved; }
    private void UnbindTiming(BullTimingRing ring) { ring.OnTimingResolved -= HandleTimingResolved; }

    private void UnbindAll()
    {
        if (subscribedPlayerStats != null) UnbindPlayer(subscribedPlayerStats);
        if (subscribedBullStats != null) UnbindBull(subscribedBullStats);
        if (subscribedTimingRing != null) UnbindTiming(subscribedTimingRing);
        subscribedPlayerStats = null; subscribedBullStats = null; subscribedTimingRing = null;
    }

    private void UnbindArcadeScoring()
    {
        if (trackedArcadeScoring != null)
            trackedArcadeScoring.ScoreEventQueued -= HandleArcadeScoreEventQueued;

        trackedArcadeScoring = null;
    }

    private void UpdateRuntimeSourceAnchors()
    {
        if (bullStateSource != null && bullAnchor != null)
            bullStateSource.transform.position = bullAnchor.position;

        if (bullIdleSource != null && bullAnchor != null)
            bullIdleSource.transform.position = bullAnchor.position;

        if (heartbeatLoopSource != null && playerAnchor != null)
            heartbeatLoopSource.transform.position = playerAnchor.position;

        if (playerBreathLoopSource != null && playerAnchor != null)
            playerBreathLoopSource.transform.position = playerAnchor.position;
    }

    private void SyncTemporaryGameplayBlockState()
    {
        bool nextBlocked = IsFrontendVisible() || (BullfightPauseSettingsUI.Instance != null && BullfightPauseSettingsUI.Instance.IsPauseMenuOpen);
        if (temporaryGameplaySfxBlocked == nextBlocked)
            return;

        temporaryGameplaySfxBlocked = nextBlocked;
        if (temporaryGameplaySfxBlocked)
            StopGameplaySfx();
        else
            activeBullStateAudioMode = BullStateAudioMode.None;
    }

    private bool IsFrontendVisible()
    {
        if (manualStartMenu == null || manualStartMenu.gameObject == null)
            manualStartMenu = FindObjectOfType<ManualStartMenuController>(true);

        if (manualStartMenu != null && manualStartMenu.IsFrontendVisible)
            return true;

        if (startMenu == null || startMenu.gameObject == null)
            startMenu = FindObjectOfType<BullfightStartMenu>(true);

        return startMenu != null && startMenu.IsMenuVisible;
    }

    private bool IsGameplaySfxBlocked()
    {
        return sfxSuppressed || temporaryGameplaySfxBlocked || masterVolume <= 0f;
    }

    private void SyncBullStateAudio()
    {
        if (bullStateSource == null)
            return;

        if (IsGameplaySfxBlocked() || trackedBullAI == null)
        {
            StopRuntimeSource(bullStateSource);
            activeBullStateAudioMode = BullStateAudioMode.None;
            return;
        }

        BullStateAudioMode desiredMode = trackedBullAI.currentState switch
        {
            BullAI.BullState.Telegraphing => BullStateAudioMode.Telegraph,
            BullAI.BullState.Charging => BullStateAudioMode.Charge,
            BullAI.BullState.Fatigued => BullStateAudioMode.Fatigue,
            _ => BullStateAudioMode.None
        };

        if (desiredMode == BullStateAudioMode.None)
        {
            StopRuntimeSource(bullStateSource);
            activeBullStateAudioMode = BullStateAudioMode.None;
            return;
        }

        if (desiredMode == activeBullStateAudioMode)
        {
            if (desiredMode == BullStateAudioMode.Charge && !bullStateSource.isPlaying)
                PlayRuntimeCue(bullStateSource, bullChargeCue, PlaceholderCue.BullCharge, bullAnchor, true);

            return;
        }

        activeBullStateAudioMode = desiredMode;
        switch (desiredMode)
        {
            case BullStateAudioMode.Telegraph:
                PlayRuntimeCue(bullStateSource, bullTelegraphCue, PlaceholderCue.BullTelegraph, bullAnchor, false);
                break;
            case BullStateAudioMode.Charge:
                PlayRuntimeCue(bullStateSource, bullChargeCue, PlaceholderCue.BullCharge, bullAnchor, true);
                break;
            case BullStateAudioMode.Fatigue:
                PlayRuntimeCue(bullStateSource, bullFatigueCue, PlaceholderCue.BullFatigue, bullAnchor, false);
                break;
        }
    }

    private void SyncHeartbeatLoop()
    {
        if (heartbeatLoopSource == null)
            return;

        bool shouldPlayHeartbeat =
            !IsGameplaySfxBlocked() &&
            playerStats != null &&
            !playerStats.IsDead &&
            playerStats.isHoldingCloth &&
            gameFlow != null &&
            gameFlow.currentPhase == BullfightGameFlow.GamePhase.PhaseOne;

        if (!shouldPlayHeartbeat)
        {
            StopRuntimeSource(heartbeatLoopSource);
            return;
        }

        PlayRuntimeCue(heartbeatLoopSource, heartbeatLoopCue, PlaceholderCue.HeartbeatLoop, playerAnchor, true);
    }

    private void SyncPlayerBreathLoop()
    {
        if (playerBreathLoopSource == null)
            return;

        bool shouldPlayBreath =
            !IsGameplaySfxBlocked() &&
            playerStats != null &&
            !playerStats.IsDead &&
            gameFlow != null &&
            gameFlow.currentPhase == BullfightGameFlow.GamePhase.PhaseTwo &&
            gameFlow.CurrentPhaseTwoState == BullfightGameFlow.PhaseTwoState.Standoff;

        if (!shouldPlayBreath)
        {
            StopRuntimeSource(playerBreathLoopSource);
            return;
        }

        PlayRuntimeCue(playerBreathLoopSource, playerBreathLoopCue, PlaceholderCue.PlayerBreathLoop, playerAnchor, true);
    }

    private void HandleArcadeScoreEventQueued(ArcadeScoreEvent scoreEvent)
    {
        if (!ShouldPlayCrowdCheer(scoreEvent))
            return;

        PlayCrowdCheer();
    }

    private bool ShouldPlayCrowdCheer(ArcadeScoreEvent scoreEvent)
    {
        if (scoreEvent == null || !scoreEvent.IsPositive || scoreEvent.AwardedPoints <= 0)
            return false;

        return scoreEvent.EventId switch
        {
            "BANDERILLAS_HIT" => true,
            "BANDERILLAS_KILL_BONUS" => true,
            "PHASE_TWO_ROUND_WIN" => true,
            "PHASE_TWO_PERFECT_BONUS" => true,
            _ => false
        };
    }

    private void PlayCrowdCheer()
    {
        if (Time.unscaledTime - lastCrowdReactionAt < crowdCheerCooldown)
            return;

        lastCrowdReactionAt = Time.unscaledTime;
        PlayCrowdReactionCue(crowdCheerCue, PlaceholderCue.CrowdCheer);
    }

    private void PlayCrowdDisappointment()
    {
        if (Time.unscaledTime - lastCrowdReactionAt < crowdCheerCooldown)
            return;

        lastCrowdReactionAt = Time.unscaledTime;
        PlayCrowdReactionCue(crowdDisappointmentCue, PlaceholderCue.CrowdDisappointment);
    }

    private void HandleHoldingClothChanged(bool isHolding) => PlayCue(isHolding ? clothRaiseCue : clothLowerCue, isHolding ? PlaceholderCue.ClothRaise : PlaceholderCue.ClothLower, playerAnchor);
    private void HandleCapaPerformed() => PlayCue(capaCue, PlaceholderCue.Capa, playerAnchor);
    private void HandleEvadePerformed() => PlayCue(evadeCue, PlaceholderCue.Evade, playerAnchor);
    private void HandleBanderillasPerformed() => PlayCue(banderillasCue, PlaceholderCue.Banderillas, playerAnchor);
    private void HandlePlayerDamaged(float _) => PlayCue(playerHitCue, PlaceholderCue.PlayerHit, playerAnchor);
    private void HandlePlayerDeath()
    {
        PlayCue(playerDeathCue, PlaceholderCue.PlayerHit, playerAnchor);
    }
    private void HandleStunStateChanged(bool isStunned) => PlayCue(isStunned ? playerStunCue : playerRecoverCue, isStunned ? PlaceholderCue.PlayerStun : PlaceholderCue.PlayerRecover, playerAnchor);
    private void HandleBullDamaged(float _) => PlayCue(bullHurtCue, PlaceholderCue.BullHurt, bullAnchor);
    private void HandleBullDefeated() => PlayCue(bullDeathCue, PlaceholderCue.BullDeath, bullAnchor);

    private void HandleTimingResolved(string result)
    {

        switch (result)
        {
            case "Perfect!": PlayCue(timingPerfectCue, PlaceholderCue.TimingPerfect, null); break;
            case "Good": PlayCue(timingGoodCue, PlaceholderCue.TimingGood, null); break;
            default:
                PlayCue(timingMissCue, PlaceholderCue.TimingMiss, null);
                if (ShouldPlayCrowdDisappointmentForTimingResult())
                    PlayCrowdDisappointment();
                break;
        }
    }

    private void HandleBullIdleAmbient()
    {
        if (bullIdleSource == null || IsGameplaySfxBlocked())
        {
            StopRuntimeSource(bullIdleSource);
            return;
        }

        if (trackedBullAI == null)
        {
            StopRuntimeSource(bullIdleSource);
            return;
        }

        if (trackedBullAI.currentState == BullAI.BullState.Idle ||
            trackedBullAI.currentState == BullAI.BullState.Roaming)
        {
            idleTimer -= Time.deltaTime;

            if (idleTimer <= 0f)
            {
                AudioCue chosen = Random.value > 0.5f ? bullIdleCueA : bullIdleCueB;
                PlayRuntimeCueWithProximity(bullIdleSource, chosen, PlaceholderCue.BullFatigue, bullAnchor, false);

                idleTimer = Random.Range(idleIntervalRange.x, idleIntervalRange.y);
            }
        }
        else
        {
            idleTimer = 0f;
            StopRuntimeSource(bullIdleSource);
        }
    }

    private void PlayCueWithProximity(AudioCue configuredCue, PlaceholderCue fallbackCue, Transform anchor)
    {
        if (configuredCue == null) return;

        float distance = 0f;

        if (playerAnchor != null && anchor != null)
            distance = Vector3.Distance(playerAnchor.position, anchor.position);

        float t = Mathf.InverseLerp(proximityMaxDistance, proximityMinDistance, distance);
        float volumeBoost = Mathf.Lerp(0.6f, proximityVolumeMultiplier, t);

        AudioCue boostedCue = new AudioCue
        {
            clip = configuredCue.clip,
            volume = configuredCue.volume * volumeBoost,
            pitch = Mathf.Lerp(configuredCue.pitch, configuredCue.pitch * 0.9f, t), // �V��V�C�I
            spatialBlend = configuredCue.spatialBlend
        };

        PlayCue(boostedCue, fallbackCue, anchor);
    }

    private void PlayRuntimeCueWithProximity(AudioSource source, AudioCue configuredCue, PlaceholderCue fallbackCue, Transform anchor, bool loop)
    {
        if (configuredCue == null)
            return;

        float distance = 0f;
        if (playerAnchor != null && anchor != null)
            distance = Vector3.Distance(playerAnchor.position, anchor.position);

        float t = Mathf.InverseLerp(proximityMaxDistance, proximityMinDistance, distance);
        float volumeBoost = Mathf.Lerp(0.6f, proximityVolumeMultiplier, t);

        AudioCue boostedCue = new AudioCue
        {
            clip = configuredCue.clip,
            volume = configuredCue.volume * volumeBoost,
            pitch = Mathf.Lerp(configuredCue.pitch, configuredCue.pitch * 0.9f, t),
            spatialBlend = configuredCue.spatialBlend
        };

        PlayRuntimeCue(source, boostedCue, fallbackCue, anchor, loop);
    }

    private void PlayRuntimeCue(AudioSource source, AudioCue configuredCue, PlaceholderCue fallbackCue, Transform anchor, bool loop)
    {
        if (source == null || configuredCue == null || IsGameplaySfxBlocked())
            return;

        AudioClip clip = configuredCue.clip != null ? configuredCue.clip : GetPlaceholderClip(fallbackCue);
        if (clip == null)
            return;

        if (anchor != null)
            source.transform.position = anchor.position;

        float resolvedVolume = Mathf.Max(0f, configuredCue.volume) * masterVolume;
        float resolvedPitch = Mathf.Approximately(configuredCue.pitch, 0f) ? 1f : configuredCue.pitch;
        float resolvedSpatialBlend = Mathf.Clamp01(configuredCue.spatialBlend);
        if (source.isPlaying && source.clip == clip && source.loop == loop)
        {
            source.volume = resolvedVolume;
            source.pitch = resolvedPitch;
            source.spatialBlend = resolvedSpatialBlend;
            return;
        }

        source.Stop();
        source.clip = clip;
        source.loop = loop;
        source.volume = resolvedVolume;
        source.pitch = resolvedPitch;
        source.spatialBlend = resolvedSpatialBlend;
        source.Play();
    }

    private void StopRuntimeSource(AudioSource source)
    {
        if (source == null)
            return;

        source.Stop();
        source.clip = null;
    }

    private void StopGameplaySfx()
    {
        CleanupManagedSfxSources();
        for (int i = activeSfxSources.Count - 1; i >= 0; i--)
        {
            ManagedSfxSource entry = activeSfxSources[i];
            if (entry?.Source == null)
                continue;

            entry.Source.Stop();
            if (entry.Source.gameObject != null)
                Destroy(entry.Source.gameObject);
        }

        activeSfxSources.Clear();
        StopRuntimeSource(bullStateSource);
        StopRuntimeSource(bullIdleSource);
        StopRuntimeSource(heartbeatLoopSource);
        StopRuntimeSource(playerBreathLoopSource);
        activeBullStateAudioMode = BullStateAudioMode.None;
    }

    private bool ShouldPlayCrowdDisappointmentForTimingResult()
    {
        if (gameFlow == null)
            return false;

        if (gameFlow.currentPhase == BullfightGameFlow.GamePhase.PhaseOne)
            return true;

        return gameFlow.currentPhase == BullfightGameFlow.GamePhase.PhaseTwo &&
               gameFlow.CurrentPhaseTwoState == BullfightGameFlow.PhaseTwoState.RoundWindow;
    }

    private void PlayCrowdReactionCue(AudioCue configuredCue, PlaceholderCue fallbackCue)
    {
        if (configuredCue == null)
            return;

        if (TryGetCrowdReactionPosition(out Vector3 crowdPosition))
        {
            PlayCueAtPosition(configuredCue, fallbackCue, crowdPosition);
            return;
        }

        PlayCue(configuredCue, fallbackCue, null);
    }

    private bool TryGetCrowdReactionPosition(out Vector3 position)
    {
        position = transform.position;
        if (spawnManager == null)
            return false;

        Vector3 center = spawnManager.ArenaCenter;
        Vector3 sourceBasis = playerAnchor != null
            ? playerAnchor.position
            : bullAnchor != null ? bullAnchor.position : center + Vector3.forward;

        Vector3 horizontalDirection = sourceBasis - center;
        horizontalDirection.y = 0f;
        if (horizontalDirection.sqrMagnitude <= 0.0001f)
        {
            horizontalDirection = playerAnchor != null ? playerAnchor.forward : transform.forward;
            horizontalDirection.y = 0f;
        }

        if (horizontalDirection.sqrMagnitude <= 0.0001f)
            return false;

        float reactionRadius = Mathf.Max(1f, spawnManager.ArenaRadius + 0.2f);
        position = center + horizontalDirection.normalized * reactionRadius;
        position.y = Mathf.Max(center.y + 1.35f, sourceBasis.y + 0.6f);
        return true;
    }

    private void PlayCue(AudioCue configuredCue, PlaceholderCue fallbackCue, Transform anchor)
    {
        if (IsGameplaySfxBlocked() || configuredCue == null) return;
        AudioClip clip = configuredCue.clip != null ? configuredCue.clip : GetPlaceholderClip(fallbackCue);
        if (clip == null) return;
        Transform parent = anchor != null ? anchor : transform;
        GameObject audioObject = new GameObject("BullfightSfx_" + fallbackCue);
        audioObject.transform.SetParent(parent, false);
        audioObject.transform.localPosition = Vector3.zero;
        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.clip = clip;
        ManagedSfxSource managedSource = new ManagedSfxSource
        {
            Source = source,
            BaseVolume = Mathf.Max(0f, configuredCue.volume)
        };
        activeSfxSources.Add(managedSource);
        source.volume = managedSource.BaseVolume * masterVolume;
        source.pitch = Mathf.Approximately(configuredCue.pitch, 0f) ? 1f : configuredCue.pitch;
        source.spatialBlend = Mathf.Clamp01(configuredCue.spatialBlend);
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1f;
        source.maxDistance = 18f;
        source.dopplerLevel = 0f;
        source.Play();
        Destroy(audioObject, (clip.length / Mathf.Max(0.01f, Mathf.Abs(source.pitch))) + 0.1f);
    }

    private void PlayCueAtPosition(AudioCue configuredCue, PlaceholderCue fallbackCue, Vector3 worldPosition)
    {
        if (IsGameplaySfxBlocked() || configuredCue == null)
            return;

        AudioClip clip = configuredCue.clip != null ? configuredCue.clip : GetPlaceholderClip(fallbackCue);
        if (clip == null)
            return;

        GameObject audioObject = new GameObject("BullfightSfx_" + fallbackCue);
        audioObject.transform.SetParent(transform, false);
        audioObject.transform.position = worldPosition;
        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.clip = clip;
        ManagedSfxSource managedSource = new ManagedSfxSource
        {
            Source = source,
            BaseVolume = Mathf.Max(0f, configuredCue.volume)
        };
        activeSfxSources.Add(managedSource);
        source.volume = managedSource.BaseVolume * masterVolume;
        source.pitch = Mathf.Approximately(configuredCue.pitch, 0f) ? 1f : configuredCue.pitch;
        source.spatialBlend = Mathf.Clamp01(configuredCue.spatialBlend);
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1f;
        source.maxDistance = 24f;
        source.dopplerLevel = 0f;
        source.Play();
        Destroy(audioObject, (clip.length / Mathf.Max(0.01f, Mathf.Abs(source.pitch))) + 0.1f);
    }

    private AudioClip GetPlaceholderClip(PlaceholderCue cue)
    {
        if (!useProceduralFallback) return null;
        if (placeholderClips.TryGetValue(cue, out AudioClip clip)) return clip;
        clip = CreatePlaceholderClip(cue);
        if (clip != null) placeholderClips[cue] = clip;
        return clip;
    }

    private static AudioClip CreatePlaceholderClip(PlaceholderCue cue)
    {
        switch (cue)
        {
            case PlaceholderCue.ClothRaise: return CreateClip("ClothRaise", 0.16f, (i, t, p) => (Sine(t, Mathf.Lerp(420f, 700f, p)) * 0.1f + Noise(i * 5) * 0.009f) * Envelope(p, 0.18f, 0.35f));
            case PlaceholderCue.ClothLower: return CreateClip("ClothLower", 0.16f, (i, t, p) => (Sine(t, Mathf.Lerp(650f, 360f, p)) * 0.08f + Noise(i * 7) * 0.009f) * Envelope(p, 0.15f, 0.45f));
            case PlaceholderCue.Capa: return CreateClip("Capa", 0.22f, (i, t, p) => (Noise(i * 11) * 0.08f + Sine(t, Mathf.Lerp(520f, 240f, p)) * 0.05f) * Envelope(p, 0.08f, 0.22f));
            case PlaceholderCue.Evade: return CreateClip("Evade", 0.14f, (i, t, p) => (Sine(t, Mathf.Lerp(260f, 980f, p)) * 0.12f + Noise(i * 13) * 0.02f) * Envelope(p, 0.04f, 0.25f));
            case PlaceholderCue.Banderillas: return CreateClip("Banderillas", 0.18f, (i, t, p) => (Square(t, Mathf.Lerp(170f, 130f, p)) * 0.08f + Noise(i * 17) * 0.06f) * Envelope(p, 0.02f, 0.14f));
            case PlaceholderCue.SwordStab: return CreateClip("SwordStab", 0.16f, (i, t, p) => (Noise(i * 53) * 0.05f + Square(t, Mathf.Lerp(620f, 340f, p)) * 0.05f + Sine(t, Mathf.Lerp(980f, 540f, p)) * 0.04f) * Envelope(p, 0.01f, 0.18f));
            case PlaceholderCue.PlayerHit: return CreateClip("PlayerHit", 0.22f, (i, t, p) => (Sine(t, Mathf.Lerp(180f, 85f, p)) * 0.12f + Noise(i * 19) * 0.07f) * Envelope(p, 0.01f, 0.4f));
            case PlaceholderCue.PlayerStun: return CreateClip("PlayerStun", 0.45f, (i, t, p) => (Sine(t, Mathf.Lerp(150f, 210f, 0.5f + 0.5f * Mathf.Sin(p * Mathf.PI * 6f))) * 0.08f + Noise(i * 23) * 0.03f) * Envelope(p, 0.05f, 0.25f));
            case PlaceholderCue.PlayerRecover: return CreateClip("PlayerRecover", 0.2f, (i, t, p) => (Sine(t, Mathf.Lerp(240f, 420f, p)) * 0.08f + Sine(t, Mathf.Lerp(360f, 560f, p)) * 0.04f) * Envelope(p, 0.05f, 0.3f));
            case PlaceholderCue.BullTelegraph: return CreateClip("BullTelegraph", 0.38f, (i, t, p) => (Sine(t, Mathf.Lerp(95f, 72f, p)) * 0.14f + Noise(i * 29) * 0.04f) * Envelope(p, 0.06f, 0.2f));
            case PlaceholderCue.BullCharge: return CreateClip("BullCharge", 0.34f, (i, t, p) => (Sine(t, Mathf.Lerp(80f, 58f, p)) * 0.14f + Noise(i * 31) * 0.05f) * Envelope(p, 0.03f, 0.15f) * (0.6f + Mathf.Clamp01(Mathf.Sin(p * Mathf.PI * 4f)) * 0.4f));
            case PlaceholderCue.BullFatigue: return CreateClip("BullFatigue", 0.48f, (i, t, p) => (Noise(i * 37) * 0.08f + Sine(t, Mathf.Lerp(120f, 80f, p)) * 0.05f) * Envelope(p, 0.04f, 0.25f));
            case PlaceholderCue.BullHurt: return CreateClip("BullHurt", 0.28f, (i, t, p) => (Sine(t, Mathf.Lerp(160f, 110f, p)) * 0.12f + Noise(i * 41) * 0.05f) * Envelope(p, 0.02f, 0.18f));
            case PlaceholderCue.BullDeath: return CreateClip("BullDeath", 0.82f, (i, t, p) => (Sine(t, Mathf.Lerp(150f, 48f, p)) * 0.12f + Sine(t, Mathf.Lerp(80f, 35f, p)) * 0.08f + Noise(i * 43) * 0.03f) * Envelope(p, 0.02f, 0.2f));
            case PlaceholderCue.TimingPerfect: return CreateClip("TimingPerfect", 0.18f, (i, t, p) => (Sine(t, 880f) * 0.08f + Sine(t, 1320f) * 0.04f) * Envelope(p, 0.02f, 0.35f));
            case PlaceholderCue.TimingGood: return CreateClip("TimingGood", 0.15f, (i, t, p) => Sine(t, Mathf.Lerp(620f, 780f, p)) * 0.08f * Envelope(p, 0.02f, 0.3f));
            case PlaceholderCue.TimingMiss: return CreateClip("TimingMiss", 0.18f, (i, t, p) => (Square(t, Mathf.Lerp(220f, 150f, p)) * 0.06f + Noise(i * 47) * 0.03f) * Envelope(p, 0.01f, 0.28f));
            case PlaceholderCue.HeartbeatLoop: return CreateClip("HeartbeatLoop", 0.9f, (i, t, p) => (HeartbeatPulse(p, 0.14f, 0.04f, 0.17f) + HeartbeatPulse(p, 0.42f, 0.04f, 0.13f)) * (Sine(t, 72f) * 0.7f + Sine(t, 108f) * 0.3f));
            case PlaceholderCue.CrowdCheer: return CreateClip("CrowdCheer", 0.65f, (i, t, p) => (Noise(i * 59) * 0.05f + Sine(t, Mathf.Lerp(260f, 420f, p)) * 0.03f + Sine(t, Mathf.Lerp(430f, 620f, p)) * 0.02f) * Envelope(p, 0.04f, 0.24f));
            case PlaceholderCue.CrowdDisappointment: return CreateClip("CrowdDisappointment", 0.72f, (i, t, p) => (Noise(i * 61) * 0.04f + Sine(t, Mathf.Lerp(180f, 120f, p)) * 0.03f + Sine(t, Mathf.Lerp(240f, 165f, p)) * 0.02f) * Envelope(p, 0.05f, 0.3f));
            case PlaceholderCue.PlayerBreathLoop: return CreateClip("PlayerBreathLoop", 1.15f, (i, t, p) => (Sine(t, Mathf.Lerp(112f, 88f, p)) * 0.03f + Sine(t, Mathf.Lerp(164f, 136f, p)) * 0.015f + Noise(i * 67) * 0.004f) * (0.35f + Mathf.Clamp01(Mathf.Sin(p * Mathf.PI)) * 0.65f));
            default: return null;
        }
    }

    private static AudioClip CreateClip(string name, float duration, ProceduralSample sampler)
    {
        const int sampleRate = 44100;
        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(duration * sampleRate));
        float[] data = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++) data[i] = Mathf.Clamp(sampler(i, i / (float)sampleRate, sampleCount == 1 ? 0f : i / (float)(sampleCount - 1)), -1f, 1f);
        AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
        clip.SetData(data, 0); return clip;
    }

    private static float Envelope(float p, float a, float r) => (a <= 0f ? 1f : Mathf.Clamp01(p / a)) * (r <= 0f ? 1f : Mathf.Clamp01((1f - p) / r));
    private static float HeartbeatPulse(float progress, float center, float width, float amplitude)
    {
        float delta = Mathf.Abs(progress - center);
        if (delta >= width)
            return 0f;

        float normalized = 1f - (delta / width);
        return amplitude * normalized * normalized;
    }
    private static float Sine(float t, float f) => Mathf.Sin(t * f * Mathf.PI * 2f);
    private static float Square(float t, float f) => Mathf.Sign(Sine(t, f));
    private static float Noise(int s) { int v = (s << 13) ^ s; return 1f - ((v * (v * v * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824f; }
}
