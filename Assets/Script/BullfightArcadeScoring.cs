using System;
using System.Collections.Generic;
using UnityEngine;

public enum ArcadeScoreSourceAnchor
{
    None,
    Judge,
    Event,
    Toast
}

public enum ArcadePhaseEndReason
{
    Clear,
    TimeUp
}

public sealed class ArcadeScoreState
{
    public int CurrentScore;
    public int HighScore;
    public int PreviousHighScore;
    public int ComboCount;
    public float ComboMultiplier = 1f;
    public int MaxComboCount;
    public float MaxComboMultiplier = 1f;
    public bool IsLeaderboardEligible = true;
    public bool IsLeaderboardAtRisk;
    public string LeaderboardBlockReason = string.Empty;
    public float PhaseOneTimeRemaining;
}

public sealed class ArcadeScoreEvent
{
    public string EventId = string.Empty;
    public string Label = string.Empty;
    public int BasePoints;
    public int AwardedPoints;
    public bool IsPositive;
    public bool UsesCombo;
    public int ComboBefore;
    public int ComboAfter;
    public bool BreaksCombo;
    public string JudgeResult = string.Empty;
    public ArcadeScoreSourceAnchor SourceAnchor = ArcadeScoreSourceAnchor.None;
    public float ComboMultiplierApplied = 1f;
    public bool TriggeredHighScoreFlash;
    public string ToastText = string.Empty;
    public bool SuppressPopup;
}

public sealed class ArcadePhaseSummaryItem
{
    public string Label = string.Empty;
    public int Points;
    public bool IsCarryOnly;
    public string ValueText = string.Empty;
}

public sealed class ArcadePhaseSummary
{
    public string PhaseId = string.Empty;
    public string Title = string.Empty;
    public readonly List<ArcadePhaseSummaryItem> Items = new List<ArcadePhaseSummaryItem>();
    public int Subtotal;
    public float CarryComboMultiplier;
    public int CarryComboCount;
    public float ScoreInjectDelay;
    public float DisplayDuration;
    public ArcadePhaseEndReason PhaseEndReason;
}

public sealed class ArcadeRunSummary
{
    public int FinalScore;
    public int PreviousHighScore;
    public int NewHighScore;
    public bool IsNewRecord;
    public bool IsRanked;
    public BullfightGameFlow.EndingType EndingType;
    public int Phase1Subtotal;
    public int Phase2Subtotal;
    public int ClearBonusSubtotal;
    public int MaxComboCount;
    public float MaxComboMultiplier;
    public string RankingStatusText = string.Empty;
    public string EndingTitle = string.Empty;
}

public sealed class BullfightArcadeScoring
{
    private const string HighScorePrefKey = "BullfightArcadeHighScore";
    private const int PhaseOnePerfectCapaPoints = 900;
    private const int PhaseOneGoodCapaPoints = 600;
    private const int PhaseOneMissPoints = -500;
    private const int PhaseOneDashPoints = 50;
    private const int PhaseOneBanderillasHitPoints = 700;
    private const int PhaseOneBanderillasKillBonus = 1200;
    private const int PhaseOneBanderillasMissPoints = -300;
    private const int PhaseOnePlayerHitPoints = -900;
    private const int PhaseTwoPerfectEstocadaPoints = 1800;
    private const int PhaseTwoGoodEstocadaPoints = 1300;
    private const int PhaseTwoRoundWinBonus = 700;
    private const int PhaseTwoPerfectRoundBonus = 300;
    private const int PhaseTwoRoundLossPoints = -1400;
    private const int PhaseOneClearBonus = 2000;
    private const int PhaseOneNoDamageBonus = 1500;
    private const int PhaseTwoClearBonus = 3500;
    private const int PhaseTwoPerfectRunBonus = 2500;
    private const float PhaseOneDurationSeconds = 120f;
    private const float BanderillasMissTimeout = 1f;
    private const int PendingUiEventLimit = 64;

    private readonly Queue<ArcadeScoreEvent> pendingUiEvents = new Queue<ArcadeScoreEvent>();
    private readonly ArcadeScoreState state = new ArcadeScoreState();
    private readonly BullfightGameFlow gameFlow;

    private PlayerStats trackedPlayerStats;
    private BullAI trackedBullAI;
    private BullStats trackedBullStats;
    private ArcadePhaseSummary pendingPhaseOneSummary;
    private bool phaseOneSummaryCommitted;
    private bool phaseOneTimedOut;
    private int phaseOneSubtotal;
    private int phaseTwoSubtotal;
    private int clearBonusSubtotal;
    private int pendingBanderillasAttempts;
    private float banderillasMissResolveAt = -1f;
    private bool phaseOneNoDamage = true;
    private bool phaseTwoHadLoss;
    private bool bullKillBonusAwarded;
    private bool runActive;
    private bool arcadeEnabled;

    public BullfightArcadeScoring(BullfightGameFlow owner)
    {
        gameFlow = owner;
        ResetState(false);
    }

    public ArcadeScoreState State => state;
    public bool IsArcadeEnabled => arcadeEnabled;
    public bool IsRunActive => runActive;
    public bool HasPendingPhaseOneSummary => pendingPhaseOneSummary != null && !phaseOneSummaryCommitted;
    public bool IsPhaseOneTimeExpired => phaseOneTimedOut;

    public void ConfigureRun(bool enabled)
    {
        ResetState(enabled);
    }

    public void Bind(PlayerStats playerStats, BullAI bullAI, BullStats bullStats)
    {
        if (trackedPlayerStats != playerStats)
        {
            if (trackedPlayerStats != null)
            {
                trackedPlayerStats.OnBanderillasPerformed -= HandleBanderillasPerformed;
                trackedPlayerStats.OnDamaged -= HandlePlayerDamaged;
                trackedPlayerStats.OnDashPerformed -= HandleDashPerformed;
            }

            trackedPlayerStats = playerStats;
            if (trackedPlayerStats != null)
            {
                trackedPlayerStats.OnBanderillasPerformed += HandleBanderillasPerformed;
                trackedPlayerStats.OnDamaged += HandlePlayerDamaged;
                trackedPlayerStats.OnDashPerformed += HandleDashPerformed;
            }
        }

        if (trackedBullAI != bullAI)
        {
            if (trackedBullAI != null)
            {
                trackedBullAI.OnBanderillasHit -= HandleBanderillasHit;
                trackedBullAI.OnChargeTimingResolved -= HandleChargeTimingResolved;
            }

            trackedBullAI = bullAI;
            if (trackedBullAI != null)
            {
                trackedBullAI.OnBanderillasHit += HandleBanderillasHit;
                trackedBullAI.OnChargeTimingResolved += HandleChargeTimingResolved;
            }
        }

        if (trackedBullStats != bullStats)
        {
            if (trackedBullStats != null)
                trackedBullStats.OnDefeated -= HandleBullDefeated;

            trackedBullStats = bullStats;
            if (trackedBullStats != null)
                trackedBullStats.OnDefeated += HandleBullDefeated;
        }
    }

    public void Dispose()
    {
        Bind(null, null, null);
    }

    public void Tick(float unscaledDeltaTime)
    {
        if (!runActive || !arcadeEnabled)
            return;

        if (gameFlow != null)
        {
            bool atRisk = state.IsLeaderboardEligible &&
                          gameFlow.currentPhase == BullfightGameFlow.GamePhase.PhaseTwo &&
                          gameFlow.CurrentPhaseTwoState == BullfightGameFlow.PhaseTwoState.Standoff &&
                          gameFlow.PhaseTwoMercyTimeRemaining > 0f &&
                          gameFlow.PhaseTwoMercyTimeRemaining <= 5f;
            state.IsLeaderboardAtRisk = atRisk;
        }

        if (ShouldTrackPhaseOneCombat() && pendingPhaseOneSummary == null && !phaseOneTimedOut)
        {
            state.PhaseOneTimeRemaining = Mathf.Max(0f, state.PhaseOneTimeRemaining - Mathf.Max(0f, unscaledDeltaTime));
            if (state.PhaseOneTimeRemaining <= 0f)
                phaseOneTimedOut = true;
        }

        if (pendingBanderillasAttempts > 0 &&
            banderillasMissResolveAt > 0f &&
            Time.unscaledTime >= banderillasMissResolveAt &&
            ShouldTrackPhaseOneCombat())
        {
            while (pendingBanderillasAttempts > 0)
            {
                pendingBanderillasAttempts--;
                AddScoredEvent(
                    "BANDERILLAS_MISS",
                    "BANDERILLAS MISS",
                    PhaseOneBanderillasMissPoints,
                    false,
                    true,
                    string.Empty,
                    ArcadeScoreSourceAnchor.Event);
            }

            banderillasMissResolveAt = -1f;
        }
    }

    public bool TryDequeueUiEvent(out ArcadeScoreEvent scoreEvent)
    {
        if (pendingUiEvents.Count > 0)
        {
            scoreEvent = pendingUiEvents.Dequeue();
            return true;
        }

        scoreEvent = null;
        return false;
    }

    public ArcadePhaseSummary BuildPhaseOneSummary(ArcadePhaseEndReason endReason)
    {
        if (!runActive || !arcadeEnabled)
            return null;

        if (pendingPhaseOneSummary != null)
            return pendingPhaseOneSummary;

        pendingBanderillasAttempts = 0;
        banderillasMissResolveAt = -1f;
        phaseOneTimedOut = endReason == ArcadePhaseEndReason.TimeUp;

        pendingPhaseOneSummary = new ArcadePhaseSummary
        {
            PhaseId = endReason == ArcadePhaseEndReason.Clear ? "PHASE_ONE_CLEAR" : "PHASE_ONE_TIME_UP",
            Title = endReason == ArcadePhaseEndReason.Clear ? "PHASE 1 CLEAR" : "PHASE 1 TIME UP",
            CarryComboMultiplier = state.ComboMultiplier,
            CarryComboCount = state.ComboCount,
            PhaseEndReason = endReason
        };

        if (endReason == ArcadePhaseEndReason.Clear)
        {
            pendingPhaseOneSummary.Items.Add(new ArcadePhaseSummaryItem
            {
                Label = "PHASE CLEAR",
                Points = PhaseOneClearBonus
            });
            pendingPhaseOneSummary.Subtotal += PhaseOneClearBonus;

            if (phaseOneNoDamage)
            {
                pendingPhaseOneSummary.Items.Add(new ArcadePhaseSummaryItem
                {
                    Label = "NO DAMAGE",
                    Points = PhaseOneNoDamageBonus
                });
                pendingPhaseOneSummary.Subtotal += PhaseOneNoDamageBonus;
            }
        }
        else
        {
            pendingPhaseOneSummary.Items.Add(new ArcadePhaseSummaryItem
            {
                Label = "TIME LIMIT REACHED",
                IsCarryOnly = true
            });
        }

        if (state.ComboCount > 0)
        {
            pendingPhaseOneSummary.Items.Add(new ArcadePhaseSummaryItem
            {
                Label = "COMBO CARRIED",
                IsCarryOnly = true,
                ValueText = $"x{state.ComboMultiplier:0.00}"
            });
        }

        pendingPhaseOneSummary.ScoreInjectDelay = Mathf.Max(0.55f, pendingPhaseOneSummary.Items.Count * 0.25f);
        pendingPhaseOneSummary.DisplayDuration = Mathf.Max(2.2f, pendingPhaseOneSummary.Items.Count * 0.25f + 0.8f);
        return pendingPhaseOneSummary;
    }

    public void CommitPendingPhaseOneSummary()
    {
        if (pendingPhaseOneSummary == null || phaseOneSummaryCommitted || !runActive || !arcadeEnabled)
            return;

        ApplyFlatScoreToCurrentPhase(pendingPhaseOneSummary.Subtotal, isClearBonus: true);
        phaseOneSummaryCommitted = true;
    }

    public void ConsumePendingPhaseOneSummary()
    {
        pendingPhaseOneSummary = null;
        phaseOneSummaryCommitted = false;
        phaseOneTimedOut = false;
    }

    public void QueueComboCarryNotice()
    {
        if (!runActive || !arcadeEnabled || state.ComboCount <= 0)
            return;

        EnqueueUiEvent(new ArcadeScoreEvent
        {
            EventId = "COMBO_CARRY",
            Label = "CARRY OVER",
            ComboAfter = state.ComboCount,
            ComboMultiplierApplied = state.ComboMultiplier,
            SourceAnchor = ArcadeScoreSourceAnchor.Event
        });
    }

    public void RegisterPhaseTwoStabResult(string result)
    {
        if (!ShouldTrackPhaseTwoCombat())
            return;

        bool isPerfect = string.Equals(result, "Perfect!", StringComparison.Ordinal);
        bool isGood = string.Equals(result, "Good", StringComparison.Ordinal);
        if (isPerfect || isGood)
        {
            int basePoints = isPerfect ? PhaseTwoPerfectEstocadaPoints : PhaseTwoGoodEstocadaPoints;
            AddScoredEvent(
                isPerfect ? "PHASE_TWO_PERFECT" : "PHASE_TWO_GOOD",
                isPerfect ? "PERFECT ESTOCADA" : "GOOD ESTOCADA",
                basePoints,
                true,
                false,
                result,
                ArcadeScoreSourceAnchor.Judge);

            int roundBonus = PhaseTwoRoundWinBonus + (isPerfect ? PhaseTwoPerfectRoundBonus : 0);
            AddScoredEvent(
                isPerfect ? "PHASE_TWO_PERFECT_BONUS" : "PHASE_TWO_ROUND_WIN",
                isPerfect ? "ROUND WIN + PERFECT BONUS" : "ROUND WIN",
                roundBonus,
                false,
                false,
                string.Empty,
                ArcadeScoreSourceAnchor.Event);
            return;
        }

        phaseTwoHadLoss = true;
        AddScoredEvent(
            "PHASE_TWO_MISS",
            "ROUND LOSS",
            PhaseTwoRoundLossPoints,
            false,
            true,
            "Miss",
            ArcadeScoreSourceAnchor.Judge);
    }

    public void MarkRunUnranked(string reason)
    {
        if (!runActive || !arcadeEnabled || !state.IsLeaderboardEligible)
            return;

        state.IsLeaderboardEligible = false;
        state.IsLeaderboardAtRisk = false;
        state.LeaderboardBlockReason = reason ?? string.Empty;
        state.HighScore = state.PreviousHighScore;

        EnqueueUiEvent(new ArcadeScoreEvent
        {
            EventId = "UNRANKED_TOAST",
            ToastText = "本局不計入排行榜",
            SourceAnchor = ArcadeScoreSourceAnchor.Toast
        });
    }

    public ArcadeRunSummary BuildRunSummary(BullfightGameFlow.EndingType endingType)
    {
        if (!arcadeEnabled)
            return null;

        if (endingType == BullfightGameFlow.EndingType.Mercy)
            MarkRunUnranked("Mercy");

        if (endingType == BullfightGameFlow.EndingType.Glory)
        {
            ApplyFlatScoreToCurrentPhase(PhaseTwoClearBonus, true);
            if (!phaseTwoHadLoss)
                ApplyFlatScoreToCurrentPhase(PhaseTwoPerfectRunBonus, true);
        }

        int previousHighScore = state.PreviousHighScore;
        int newHighScore = previousHighScore;
        bool isNewRecord = false;
        if (state.IsLeaderboardEligible && state.CurrentScore > previousHighScore)
        {
            newHighScore = state.CurrentScore;
            isNewRecord = true;
            PlayerPrefs.SetInt(HighScorePrefKey, newHighScore);
            PlayerPrefs.Save();
        }

        state.HighScore = state.IsLeaderboardEligible ? Mathf.Max(previousHighScore, state.CurrentScore) : previousHighScore;
        runActive = false;

        return new ArcadeRunSummary
        {
            FinalScore = state.CurrentScore,
            PreviousHighScore = previousHighScore,
            NewHighScore = newHighScore,
            IsNewRecord = isNewRecord,
            IsRanked = state.IsLeaderboardEligible,
            EndingType = endingType,
            Phase1Subtotal = phaseOneSubtotal,
            Phase2Subtotal = phaseTwoSubtotal,
            ClearBonusSubtotal = clearBonusSubtotal,
            MaxComboCount = state.MaxComboCount,
            MaxComboMultiplier = state.MaxComboMultiplier,
            RankingStatusText = state.IsLeaderboardEligible ? "RANKING VALID" : "UNRANKED RUN",
            EndingTitle = GetEndingTitle(endingType)
        };
    }

    private void ResetState(bool enabled)
    {
        arcadeEnabled = enabled;
        runActive = enabled;
        state.CurrentScore = 0;
        state.PreviousHighScore = PlayerPrefs.GetInt(HighScorePrefKey, 0);
        state.HighScore = state.PreviousHighScore;
        state.ComboCount = 0;
        state.ComboMultiplier = 1f;
        state.MaxComboCount = 0;
        state.MaxComboMultiplier = 1f;
        state.IsLeaderboardEligible = enabled;
        state.IsLeaderboardAtRisk = false;
        state.LeaderboardBlockReason = string.Empty;
        state.PhaseOneTimeRemaining = PhaseOneDurationSeconds;
        phaseOneSubtotal = 0;
        phaseTwoSubtotal = 0;
        clearBonusSubtotal = 0;
        pendingUiEvents.Clear();
        pendingPhaseOneSummary = null;
        phaseOneSummaryCommitted = false;
        phaseOneTimedOut = false;
        phaseOneNoDamage = true;
        phaseTwoHadLoss = false;
        bullKillBonusAwarded = false;
        pendingBanderillasAttempts = 0;
        banderillasMissResolveAt = -1f;
    }

    private void HandleChargeTimingResolved(BullChargeTimingResolution resolution)
    {
        if (!ShouldTrackPhaseOneCombat() || resolution == null)
            return;

        if (string.Equals(resolution.Result, "Perfect!", StringComparison.Ordinal))
        {
            AddScoredEvent("PHASE_ONE_PERFECT", "PERFECT CAPA", PhaseOnePerfectCapaPoints, true, false, resolution.Result, ArcadeScoreSourceAnchor.Judge);
            return;
        }

        if (string.Equals(resolution.Result, "Good", StringComparison.Ordinal))
        {
            AddScoredEvent("PHASE_ONE_GOOD", "GOOD CAPA", PhaseOneGoodCapaPoints, true, false, resolution.Result, ArcadeScoreSourceAnchor.Judge);
            return;
        }

        if (resolution.PlayerDashedDuringCharge && !resolution.PlayerTookDamage)
            return;

        AddScoredEvent("PHASE_ONE_MISS", "CAPA MISS", PhaseOneMissPoints, false, true, "Miss", ArcadeScoreSourceAnchor.Judge);
    }

    private void HandleDashPerformed()
    {
        if (!ShouldTrackPhaseOneCombat())
            return;

        AddScoredEvent(
            "PHASE_ONE_DASH",
            "DASH",
            PhaseOneDashPoints,
            false,
            false,
            string.Empty,
            ArcadeScoreSourceAnchor.None,
            suppressPopup: true);
    }

    private void HandleBanderillasPerformed()
    {
        if (!ShouldTrackPhaseOneCombat())
            return;

        pendingBanderillasAttempts++;
        banderillasMissResolveAt = Time.unscaledTime + BanderillasMissTimeout;
    }

    private void HandleBanderillasHit(float damage)
    {
        if (!ShouldTrackPhaseOneCombat())
            return;

        if (pendingBanderillasAttempts > 0)
            pendingBanderillasAttempts--;

        if (pendingBanderillasAttempts <= 0)
        {
            pendingBanderillasAttempts = 0;
            banderillasMissResolveAt = -1f;
        }

        AddScoredEvent(
            "BANDERILLAS_HIT",
            "BANDERILLAS HIT",
            PhaseOneBanderillasHitPoints,
            true,
            false,
            string.Empty,
            ArcadeScoreSourceAnchor.Event);
    }

    private void HandleBullDefeated()
    {
        if (!ShouldTrackPhaseOneCombat() || bullKillBonusAwarded)
            return;

        bullKillBonusAwarded = true;
        AddScoredEvent(
            "BANDERILLAS_KILL_BONUS",
            "KILL BONUS",
            PhaseOneBanderillasKillBonus,
            false,
            false,
            string.Empty,
            ArcadeScoreSourceAnchor.Event);
    }

    private void HandlePlayerDamaged(float damage)
    {
        if (!ShouldTrackPhaseOneCombat() || damage <= 0f)
            return;

        phaseOneNoDamage = false;
        AddScoredEvent(
            "PLAYER_HIT",
            "PLAYER HIT",
            PhaseOnePlayerHitPoints,
            false,
            true,
            string.Empty,
            ArcadeScoreSourceAnchor.Event);
    }

    private bool ShouldTrackPhaseOneCombat()
    {
        return runActive &&
               arcadeEnabled &&
               gameFlow != null &&
               gameFlow.currentPhase == BullfightGameFlow.GamePhase.PhaseOne &&
               !gameFlow.IsTutorialActive &&
               !gameFlow.IsArcadePhaseClearSequenceActive;
    }

    private bool ShouldTrackPhaseTwoCombat()
    {
        return runActive &&
               arcadeEnabled &&
               gameFlow != null &&
               gameFlow.currentPhase == BullfightGameFlow.GamePhase.PhaseTwo &&
               gameFlow.CurrentPhaseTwoState == BullfightGameFlow.PhaseTwoState.RoundWindow;
    }

    private void AddScoredEvent(
        string eventId,
        string label,
        int basePoints,
        bool usesCombo,
        bool breaksCombo,
        string judgeResult,
        ArcadeScoreSourceAnchor sourceAnchor,
        bool suppressPopup = false)
    {
        int comboBefore = state.ComboCount;
        float multiplier = 1f;
        int comboAfter = comboBefore;
        int awardedPoints = basePoints;

        if (usesCombo && basePoints > 0)
        {
            comboAfter = comboBefore + 1;
            multiplier = GetComboMultiplier(comboAfter);
            awardedPoints = Mathf.RoundToInt(basePoints * multiplier);
            state.ComboCount = comboAfter;
            state.ComboMultiplier = multiplier;
            state.MaxComboCount = Mathf.Max(state.MaxComboCount, comboAfter);
            state.MaxComboMultiplier = Mathf.Max(state.MaxComboMultiplier, multiplier);
        }
        else if (breaksCombo)
        {
            comboAfter = 0;
            state.ComboCount = 0;
            state.ComboMultiplier = 1f;
        }

        ApplyDeltaToCurrentPhase(awardedPoints);
        int previousHighScore = state.HighScore;
        state.CurrentScore = Mathf.Max(0, state.CurrentScore + awardedPoints);
        bool highScoreFlash = false;
        if (state.IsLeaderboardEligible && state.CurrentScore > state.HighScore)
        {
            state.HighScore = state.CurrentScore;
            highScoreFlash = state.HighScore > previousHighScore;
        }
        else if (!state.IsLeaderboardEligible)
        {
            state.HighScore = state.PreviousHighScore;
        }

        EnqueueUiEvent(new ArcadeScoreEvent
        {
            EventId = eventId,
            Label = label,
            BasePoints = Mathf.Abs(basePoints),
            AwardedPoints = Mathf.Abs(awardedPoints),
            IsPositive = awardedPoints >= 0,
            UsesCombo = usesCombo,
            ComboBefore = comboBefore,
            ComboAfter = comboAfter,
            BreaksCombo = breaksCombo && comboBefore > 0,
            JudgeResult = judgeResult,
            SourceAnchor = suppressPopup ? ArcadeScoreSourceAnchor.None : sourceAnchor,
            ComboMultiplierApplied = multiplier,
            TriggeredHighScoreFlash = highScoreFlash,
            SuppressPopup = suppressPopup
        });
    }

    private void EnqueueUiEvent(ArcadeScoreEvent scoreEvent)
    {
        if (scoreEvent == null)
            return;

        while (pendingUiEvents.Count >= PendingUiEventLimit)
            pendingUiEvents.Dequeue();

        pendingUiEvents.Enqueue(scoreEvent);
    }

    private void ApplyFlatScoreToCurrentPhase(int points, bool isClearBonus)
    {
        if (points == 0)
            return;

        if (points > 0)
            ApplyDeltaToCurrentPhase(points, isClearBonus);

        state.CurrentScore = Mathf.Max(0, state.CurrentScore + points);
        if (state.IsLeaderboardEligible && state.CurrentScore > state.HighScore)
            state.HighScore = state.CurrentScore;
    }

    private void ApplyDeltaToCurrentPhase(int delta, bool isClearBonus = false)
    {
        if (isClearBonus)
        {
            clearBonusSubtotal += delta;
            return;
        }

        if (gameFlow != null && gameFlow.currentPhase == BullfightGameFlow.GamePhase.PhaseTwo)
            phaseTwoSubtotal += delta;
        else
            phaseOneSubtotal += delta;
    }

    private static float GetComboMultiplier(int comboCount)
    {
        if (comboCount <= 1)
            return 1f;

        return comboCount switch
        {
            2 => 1.15f,
            3 => 1.30f,
            4 => 1.50f,
            5 => 1.70f,
            _ => 2f
        };
    }

    private static string GetEndingTitle(BullfightGameFlow.EndingType endingType)
    {
        return endingType switch
        {
            BullfightGameFlow.EndingType.Glory => "GLORY ENDING",
            BullfightGameFlow.EndingType.Tragedy => "TRAGEDY ENDING",
            BullfightGameFlow.EndingType.Mercy => "MERCY ENDING",
            _ => "ARCADE RESULT"
        };
    }
}
