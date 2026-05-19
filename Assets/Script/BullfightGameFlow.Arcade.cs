using UnityEngine;

public partial class BullfightGameFlow
{
    private BullfightArcadeScoring arcadeScoring;
    private bool arcadeModeEnabled;
    private bool arcadePhaseClearSequenceActive;
    private float arcadePhaseClearSequenceTimer;
    private bool arcadePhaseClearScoreCommitted;
    private ArcadePhaseSummary activeArcadePhaseSummary;
    private ArcadeRunSummary pendingArcadeRunSummary;
    private bool arcadeFinalResultSequenceActive;
    private bool arcadeOverlayGameplayFrozen;

    public BullfightArcadeScoring ArcadeScoring => arcadeScoring;
    public bool IsArcadeModeEnabled => arcadeModeEnabled;
    public bool IsArcadePhaseClearSequenceActive => arcadePhaseClearSequenceActive;
    public bool HasActiveArcadeOverlay => arcadePhaseClearSequenceActive || arcadeFinalResultSequenceActive;

    private void ConfigureArcadeRun(bool enabled)
    {
        arcadeModeEnabled = enabled;
        EnsureArcadeScoring();
        arcadeScoring.ConfigureRun(enabled);
        EnsureArcadeBindings();
    }

    private void ResetArcadeRuntimeState()
    {
        BullfightHudController hudController = BullfightHudController.Instance;
        if (hudController != null)
            hudController.ArcadeFinalResultClosed -= HandleArcadeFinalResultClosed;

        SetArcadeOverlayGameplayFrozen(false);
        arcadeModeEnabled = false;
        arcadePhaseClearSequenceActive = false;
        arcadePhaseClearSequenceTimer = 0f;
        arcadePhaseClearScoreCommitted = false;
        activeArcadePhaseSummary = null;
        pendingArcadeRunSummary = null;
        arcadeFinalResultSequenceActive = false;

        if (arcadeScoring != null)
            arcadeScoring.ConfigureRun(false);
    }

    private void EnsureArcadeScoring()
    {
        if (arcadeScoring == null)
            arcadeScoring = new BullfightArcadeScoring(this);
    }

    private void EnsureArcadeBindings()
    {
        if (arcadeScoring == null)
            return;

        arcadeScoring.Bind(playerStats, bullAI, bullStats);
    }

    private bool UpdateArcadeSequences()
    {
        EnsureArcadeBindings();
        arcadeScoring?.Tick(Time.unscaledDeltaTime);

        if (arcadeFinalResultSequenceActive)
        {
            UpdateArcadeFinalResultSequence();
            return true;
        }

        if (!arcadePhaseClearSequenceActive)
            return false;

        UpdateArcadePhaseClearSequence();
        return true;
    }

    private bool TryBeginArcadePhaseOneClearSequence()
    {
        return TryBeginArcadePhaseOneSummarySequence(ArcadePhaseEndReason.Clear);
    }

    private bool TryBeginArcadePhaseOneTimeUpSequence()
    {
        return TryBeginArcadePhaseOneSummarySequence(ArcadePhaseEndReason.TimeUp);
    }

    private bool TryBeginArcadePhaseOneSummarySequence(ArcadePhaseEndReason endReason)
    {
        if (!arcadeModeEnabled || arcadeScoring == null || arcadePhaseClearSequenceActive)
            return false;

        activeArcadePhaseSummary = arcadeScoring.BuildPhaseOneSummary(endReason);
        if (activeArcadePhaseSummary == null)
            return false;

        arcadePhaseClearSequenceActive = true;
        arcadePhaseClearSequenceTimer = 0f;
        arcadePhaseClearScoreCommitted = false;
        isEnteringPhaseTwo = false;
        SetArcadeOverlayGameplayFrozen(true);

        if (timingRing != null)
        {
            timingRing.HideImmediate();
            timingRing.ResetTimingWindow();
        }

        BullfightHudController.Instance?.ShowArcadePhaseClearPanel(activeArcadePhaseSummary);
        return true;
    }

    private void UpdateArcadePhaseClearSequence()
    {
        if (activeArcadePhaseSummary == null)
        {
            arcadePhaseClearSequenceActive = false;
            return;
        }

        arcadePhaseClearSequenceTimer += Time.unscaledDeltaTime;

        if (!arcadePhaseClearScoreCommitted &&
            arcadePhaseClearSequenceTimer >= activeArcadePhaseSummary.ScoreInjectDelay)
        {
            arcadeScoring?.CommitPendingPhaseOneSummary();
            arcadePhaseClearScoreCommitted = true;
        }

        if (arcadePhaseClearSequenceTimer < activeArcadePhaseSummary.DisplayDuration)
            return;

        arcadeScoring?.ConsumePendingPhaseOneSummary();
        arcadePhaseClearSequenceActive = false;
        arcadePhaseClearSequenceTimer = 0f;
        arcadePhaseClearScoreCommitted = false;
        activeArcadePhaseSummary = null;
        SetArcadeOverlayGameplayFrozen(false);
        isEnteringPhaseTwo = true;
    }

    private void NotifyArcadePhaseTwoEntered()
    {
        if (!arcadeModeEnabled)
            return;

        arcadeScoring?.QueueComboCarryNotice();
    }

    private void MarkArcadeRunDebugModified()
    {
        if (!arcadeModeEnabled)
            return;

        arcadeScoring?.MarkRunUnranked("Debug");
    }

    private void ShowArcadeResultsOrReturnToMenu()
    {
        if (!arcadeModeEnabled || arcadeScoring == null)
        {
            ReturnToStartMenuAfterEnding();
            return;
        }

        ArcadeRunSummary summary = arcadeScoring.BuildRunSummary(currentEnding);
        if (summary == null)
        {
            ReturnToStartMenuAfterEnding();
            return;
        }

        ResetSceneForMainMenu();
        SetArcadeOverlayGameplayFrozen(true);
        pendingArcadeRunSummary = summary;
        arcadeFinalResultSequenceActive = true;

        BullfightHudController hudController = BullfightHudController.Instance;
        if (hudController == null)
        {
            arcadeFinalResultSequenceActive = false;
            pendingArcadeRunSummary = null;
            SetArcadeOverlayGameplayFrozen(false);
            ReturnToStartMenuAfterEnding();
            return;
        }

        hudController.ArcadeFinalResultClosed -= HandleArcadeFinalResultClosed;
        hudController.ArcadeFinalResultClosed += HandleArcadeFinalResultClosed;
        hudController.ShowArcadeFinalResultPanel(summary);
    }

    private void UpdateArcadeFinalResultSequence()
    {
        BullfightHudController hudController = BullfightHudController.Instance;
        if (hudController == null)
        {
            arcadeFinalResultSequenceActive = false;
            pendingArcadeRunSummary = null;
            SetArcadeOverlayGameplayFrozen(false);
            ReturnToStartMenuAfterEnding();
            return;
        }

        if (pendingArcadeRunSummary != null && !hudController.IsArcadeFinalResultPanelActive)
            hudController.ShowArcadeFinalResultPanel(pendingArcadeRunSummary);
    }

    private void HandleArcadeFinalResultClosed()
    {
        BullfightHudController hudController = BullfightHudController.Instance;
        if (hudController != null)
            hudController.ArcadeFinalResultClosed -= HandleArcadeFinalResultClosed;

        if (!arcadeFinalResultSequenceActive)
            return;

        arcadeFinalResultSequenceActive = false;
        pendingArcadeRunSummary = null;
        SetArcadeOverlayGameplayFrozen(false);
        ReturnToStartMenuAfterEnding();
    }

    private void SetArcadeOverlayGameplayFrozen(bool frozen)
    {
        if (arcadeOverlayGameplayFrozen == frozen)
            return;

        arcadeOverlayGameplayFrozen = frozen;
        ResolveReferencesIfNeeded();

        if (frozen)
        {
            playerController?.ClearInputBuffers();
            playerController?.ForceStopMovement();
            if (playerController != null)
                playerController.enabled = false;

            if (playerStats != null)
            {
                playerStats.SetHoldingCloth(false);
                playerStats.SetShooterGameplayEnabled(false);
                playerStats.SetMainMenuFrozen(true);
            }

            if (bullAI != null)
            {
                bullAI.SetTutorialControl(false);
                bullAI.enabled = false;
            }

            return;
        }

        if (playerStats != null)
        {
            playerStats.SetMainMenuFrozen(false);
            playerStats.SetShooterGameplayEnabled(currentPhase == GamePhase.PhaseOne && currentEnding == EndingType.None);
        }

        if (playerController != null)
            playerController.enabled = true;

        if (bullAI != null)
            bullAI.enabled = true;
    }
}
