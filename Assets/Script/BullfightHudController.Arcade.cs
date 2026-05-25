using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public partial class BullfightHudController
{
    private const int MaxArcadeEventPopupQueue = 8;

    private enum ArcadeComboDisplayMode
    {
        Hidden,
        Normal,
        Break,
        Carry
    }

    public static BullfightHudController Instance { get; private set; }

    private RectTransform arcadeScoreRoot;
    private Image arcadeScorePanel;
    private Text arcadeHighScoreLabel;
    private Text arcadeHighScoreValue;
    private Text arcadeNewHighScoreBadge;
    private Text arcadeScoreLabel;
    private Text arcadeScoreValue;
    private Image arcadeEligibilityPill;
    private Text arcadeEligibilityText;

    private RectTransform arcadePhaseTimerRoot;
    private Image arcadePhaseTimerPanel;
    private Text arcadePhaseTimerText;

    private RectTransform arcadeComboRoot;
    private Image arcadeComboPanel;
    private Text arcadeComboHeader;
    private Text arcadeComboValue;
    private Text arcadeComboStreak;

    private RectTransform arcadeJudgeScoreRoot;
    private Text arcadeJudgeScoreText;

    private RectTransform arcadeEventPopupRoot;
    private Text arcadeEventPopupText;

    private RectTransform arcadeToastRoot;
    private Image arcadeToastPanel;
    private Text arcadeToastText;

    private Image arcadeModalBackdrop;
    private RectTransform arcadePhaseClearRoot;
    private Image arcadePhaseClearPanel;
    private Text arcadePhaseClearTitle;
    private readonly Text[] arcadePhaseClearLines = new Text[6];

    private RectTransform arcadeFinalResultRoot;
    private Image arcadeFinalResultPanel;
    private Text arcadeFinalResultTitle;
    private Text arcadeFinalResultBanner;
    private Text arcadeFinalResultScoreValue;
    private readonly Text[] arcadeFinalResultRows = new Text[6];
    private Text arcadeFinalResultHint;

    private readonly Queue<ArcadeScoreEvent> arcadeEventPopupQueue = new Queue<ArcadeScoreEvent>();
    private ArcadeScoreEvent activeJudgeScoreEvent;
    private float activeJudgeScoreTimer;
    private ArcadeScoreEvent activeEventPopup;
    private float activeEventPopupTimer;
    private float arcadeToastTimer;
    private ArcadeComboDisplayMode arcadeComboMode = ArcadeComboDisplayMode.Hidden;
    private float arcadeComboTransientTimer;
    private string arcadeComboTransientValue = string.Empty;
    private ArcadePhaseSummary activeArcadePhaseSummaryHud;
    private float activeArcadePhaseSummaryTimer;
    private ArcadeRunSummary activeArcadeRunSummaryHud;
    private float activeArcadeRunSummaryTimer;
    private bool arcadeFinalResultDismissed;

    private int arcadeDisplayedScore;
    private int arcadeScoreAnimationFrom;
    private int arcadeScoreAnimationTo;
    private float arcadeScoreAnimationTimer;
    private float arcadeScoreAnimationDuration;
    private bool arcadeScoreInitialized;
    private float arcadeScoreFlashTimer;
    private Color arcadeScoreFlashColor = Color.white;
    private float arcadeNewHighScoreTimer;
    private BullTimingRing cachedArcadeTimingRing;

    public event Action ArcadeFinalResultClosed;

    private void ResetArcadeHudRuntime()
    {
        arcadeScoreRoot = null;
        arcadeScorePanel = null;
        arcadeHighScoreLabel = null;
        arcadeHighScoreValue = null;
        arcadeNewHighScoreBadge = null;
        arcadeScoreLabel = null;
        arcadeScoreValue = null;
        arcadeEligibilityPill = null;
        arcadeEligibilityText = null;
        arcadePhaseTimerRoot = null;
        arcadePhaseTimerPanel = null;
        arcadePhaseTimerText = null;
        arcadeComboRoot = null;
        arcadeComboPanel = null;
        arcadeComboHeader = null;
        arcadeComboValue = null;
        arcadeComboStreak = null;
        arcadeJudgeScoreRoot = null;
        arcadeJudgeScoreText = null;
        arcadeEventPopupRoot = null;
        arcadeEventPopupText = null;
        arcadeToastRoot = null;
        arcadeToastPanel = null;
        arcadeToastText = null;
        arcadeModalBackdrop = null;
        arcadePhaseClearRoot = null;
        arcadePhaseClearPanel = null;
        arcadePhaseClearTitle = null;
        for (int index = 0; index < arcadePhaseClearLines.Length; index++)
            arcadePhaseClearLines[index] = null;
        arcadeFinalResultRoot = null;
        arcadeFinalResultPanel = null;
        arcadeFinalResultTitle = null;
        arcadeFinalResultBanner = null;
        arcadeFinalResultScoreValue = null;
        for (int index = 0; index < arcadeFinalResultRows.Length; index++)
            arcadeFinalResultRows[index] = null;
        arcadeFinalResultHint = null;
        arcadeEventPopupQueue.Clear();
        activeJudgeScoreEvent = null;
        activeJudgeScoreTimer = 0f;
        activeEventPopup = null;
        activeEventPopupTimer = 0f;
        arcadeToastTimer = 0f;
        arcadeComboMode = ArcadeComboDisplayMode.Hidden;
        arcadeComboTransientTimer = 0f;
        arcadeComboTransientValue = string.Empty;
        activeArcadePhaseSummaryHud = null;
        activeArcadePhaseSummaryTimer = 0f;
        activeArcadeRunSummaryHud = null;
        activeArcadeRunSummaryTimer = 0f;
        arcadeFinalResultDismissed = false;
        arcadeDisplayedScore = 0;
        arcadeScoreAnimationFrom = 0;
        arcadeScoreAnimationTo = 0;
        arcadeScoreAnimationTimer = 0f;
        arcadeScoreAnimationDuration = 0f;
        arcadeScoreInitialized = false;
        arcadeScoreFlashTimer = 0f;
        arcadeNewHighScoreTimer = 0f;
        cachedArcadeTimingRing = null;
    }

    private void EnsureArcadeUi()
    {
        if (hudCanvasRect == null)
            return;

        EnsureArcadeScoreRoot();
        EnsureArcadePhaseTimer();
        EnsureArcadeComboRoot();
        EnsureArcadeJudgePopup();
        EnsureArcadeEventPopup();
        EnsureArcadeToast();
        EnsureArcadeModalPanels();
    }

    public void ShowArcadePhaseClearPanel(ArcadePhaseSummary summary)
    {
        if (summary == null)
            return;

        EnsureArcadeUi();
        activeArcadePhaseSummaryHud = summary;
        activeArcadePhaseSummaryTimer = 0f;
    }

    public void ShowArcadeFinalResultPanel(ArcadeRunSummary summary)
    {
        if (summary == null)
            return;

        EnsureArcadeUi();
        activeArcadeRunSummaryHud = summary;
        activeArcadeRunSummaryTimer = 0f;
        arcadeFinalResultDismissed = false;
        arcadeEventPopupQueue.Clear();
        activeEventPopup = null;
        activeJudgeScoreEvent = null;
    }

    public bool IsArcadeFinalResultPanelActive => activeArcadeRunSummaryHud != null;

    public bool ConsumeArcadeFinalResultDismissed()
    {
        if (!arcadeFinalResultDismissed)
            return false;

        arcadeFinalResultDismissed = false;
        return true;
    }

    private void UpdateArcadeHud()
    {
        if (hudCanvasRect == null)
            return;

        EnsureArcadeUi();
        DrainArcadeScoreEvents();
        UpdateArcadeScoreTicker();
        UpdateArcadeScoreRoot();
        UpdateArcadePhaseTimer();
        UpdateArcadeComboRoot();
        UpdateArcadeJudgePopupState();
        UpdateArcadeEventPopupState();
        UpdateArcadeToastState();
        UpdateArcadePhaseClearPanelState();
        UpdateArcadeFinalResultPanelState();
        UpdateArcadeModalBackdropState();
    }

    private void DrainArcadeScoreEvents()
    {
        BullfightArcadeScoring arcadeScoring = gameFlow != null ? gameFlow.ArcadeScoring : null;
        if (arcadeScoring == null)
            return;

        while (arcadeScoring.TryDequeueUiEvent(out ArcadeScoreEvent scoreEvent))
            HandleArcadeScoreEvent(scoreEvent);
    }

    private void HandleArcadeScoreEvent(ArcadeScoreEvent scoreEvent)
    {
        if (scoreEvent == null)
            return;

        if (scoreEvent.SourceAnchor == ArcadeScoreSourceAnchor.Toast || !string.IsNullOrEmpty(scoreEvent.ToastText))
        {
            arcadeToastTimer = 1.4f;
            if (arcadeToastText != null)
            {
                arcadeToastText.text = string.IsNullOrEmpty(scoreEvent.ToastText) ? "本局不計入排行榜" : scoreEvent.ToastText;
                ApplyLocalizedUiFont(arcadeToastText, arcadeToastText.text, 24, wrap: true, VerticalWrapMode.Truncate, minBestFitSize: 18);
            }
            return;
        }

        if (scoreEvent.EventId == "COMBO_CARRY")
        {
            arcadeComboMode = ArcadeComboDisplayMode.Carry;
            arcadeComboTransientTimer = 0.8f;
            arcadeComboTransientValue = $"x{GetComboMultiplierForDisplay(scoreEvent.ComboAfter):0.00}";
            return;
        }

        if (scoreEvent.BreaksCombo && scoreEvent.ComboBefore > 1)
        {
            arcadeComboMode = ArcadeComboDisplayMode.Break;
            arcadeComboTransientTimer = 0.75f;
            arcadeComboTransientValue = $"x{GetComboMultiplierForDisplay(scoreEvent.ComboBefore):0.00} LOST";
        }

        if (scoreEvent.TriggeredHighScoreFlash)
            arcadeNewHighScoreTimer = 0.9f;

        if (!scoreEvent.SuppressPopup && scoreEvent.SourceAnchor == ArcadeScoreSourceAnchor.Judge)
        {
            activeJudgeScoreEvent = scoreEvent;
            activeJudgeScoreTimer = 0f;
        }
        else if (!scoreEvent.SuppressPopup && scoreEvent.SourceAnchor == ArcadeScoreSourceAnchor.Event)
        {
            EnqueueArcadeEventPopup(scoreEvent);
        }

        if (scoreEvent.AwardedPoints > 0)
        {
            arcadeScoreFlashColor = scoreEvent.JudgeResult switch
            {
                "Perfect!" => new Color(0.78f, 0.95f, 0.62f, 1f),
                "Good" => new Color(1f, 0.78f, 0.42f, 1f),
                _ => new Color(1f, 0.93f, 0.66f, 1f)
            };
            arcadeScoreFlashTimer = 0.24f;
        }
        else
        {
            arcadeScoreFlashColor = new Color(0.96f, 0.34f, 0.24f, 1f);
            arcadeScoreFlashTimer = 0.24f;
        }
    }

    private void EnqueueArcadeEventPopup(ArcadeScoreEvent scoreEvent)
    {
        if (scoreEvent == null)
            return;

        if (arcadeEventPopupQueue.Count >= MaxArcadeEventPopupQueue)
        {
            if (scoreEvent.AwardedPoints <= 50)
                return;

            arcadeEventPopupQueue.Dequeue();
        }

        arcadeEventPopupQueue.Enqueue(scoreEvent);
    }

    private void UpdateArcadeScoreTicker()
    {
        ArcadeScoreState arcadeState = gameFlow != null && gameFlow.ArcadeScoring != null
            ? gameFlow.ArcadeScoring.State
            : null;
        if (arcadeState == null)
            return;

        if (!arcadeScoreInitialized)
        {
            arcadeDisplayedScore = arcadeState.CurrentScore;
            arcadeScoreAnimationFrom = arcadeState.CurrentScore;
            arcadeScoreAnimationTo = arcadeState.CurrentScore;
            arcadeScoreInitialized = true;
        }

        if (arcadeScoreAnimationTo != arcadeState.CurrentScore)
        {
            arcadeScoreAnimationFrom = arcadeDisplayedScore;
            arcadeScoreAnimationTo = arcadeState.CurrentScore;
            arcadeScoreAnimationTimer = 0f;
            arcadeScoreAnimationDuration = GetScoreTickerDuration(Mathf.Abs(arcadeScoreAnimationTo - arcadeScoreAnimationFrom));
        }

        if (arcadeScoreAnimationDuration <= 0.0001f)
        {
            arcadeDisplayedScore = arcadeScoreAnimationTo;
            return;
        }

        if (arcadeDisplayedScore == arcadeScoreAnimationTo)
            return;

        arcadeScoreAnimationTimer += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(arcadeScoreAnimationTimer / arcadeScoreAnimationDuration);
        float easedProgress = 1f - Mathf.Pow(1f - progress, 3f);
        arcadeDisplayedScore = Mathf.RoundToInt(Mathf.Lerp(arcadeScoreAnimationFrom, arcadeScoreAnimationTo, easedProgress));
        if (progress >= 1f)
            arcadeDisplayedScore = arcadeScoreAnimationTo;
    }

    private void UpdateArcadeScoreRoot()
    {
        ArcadeScoreState arcadeState = gameFlow != null && gameFlow.ArcadeScoring != null
            ? gameFlow.ArcadeScoring.State
            : null;

        bool shouldShow = arcadeState != null &&
                          gameFlow != null &&
                          gameFlow.IsArcadeModeEnabled &&
                          gameFlow.currentPhase != BullfightGameFlow.GamePhase.Ending &&
                          !gameFlow.IsTutorialActive &&
                          activeArcadeRunSummaryHud == null;

        if (arcadeScoreRoot != null)
            arcadeScoreRoot.gameObject.SetActive(shouldShow);

        if (arcadeNewHighScoreBadge != null)
            arcadeNewHighScoreBadge.gameObject.SetActive(shouldShow && arcadeNewHighScoreTimer > 0f);

        if (arcadeHighScoreValue == null || arcadeScoreValue == null || arcadeEligibilityText == null || arcadeEligibilityPill == null || arcadeState == null)
            return;

        if (arcadeNewHighScoreTimer > 0f)
            arcadeNewHighScoreTimer = Mathf.Max(0f, arcadeNewHighScoreTimer - Time.unscaledDeltaTime);

        if (arcadeScoreFlashTimer > 0f)
            arcadeScoreFlashTimer = Mathf.Max(0f, arcadeScoreFlashTimer - Time.unscaledDeltaTime);

        arcadeHighScoreValue.text = FormatArcadeScore(arcadeState.HighScore);
        arcadeScoreValue.text = FormatArcadeScore(arcadeDisplayedScore);
        float flashBlend = arcadeScoreFlashTimer <= 0f ? 0f : Mathf.Clamp01(arcadeScoreFlashTimer / 0.24f);
        arcadeScoreValue.color = Color.Lerp(bullBossTitleColor, arcadeScoreFlashColor, flashBlend);

        if (!arcadeState.IsLeaderboardEligible)
        {
            arcadeEligibilityText.text = "UNRANKED";
            arcadeEligibilityPill.color = new Color(0.46f, 0.11f, 0.11f, 0.94f);
        }
        else if (arcadeState.IsLeaderboardAtRisk)
        {
            arcadeEligibilityText.text = "RANKING AT RISK";
            arcadeEligibilityPill.color = new Color(0.72f, 0.45f, 0.08f, 0.94f);
        }
        else
        {
            arcadeEligibilityText.text = "RANKING VALID";
            arcadeEligibilityPill.color = new Color(0.41f, 0.31f, 0.08f, 0.94f);
        }
    }

    private void UpdateArcadePhaseTimer()
    {
        ArcadeScoreState arcadeState = gameFlow != null && gameFlow.ArcadeScoring != null
            ? gameFlow.ArcadeScoring.State
            : null;

        bool shouldShow = arcadePhaseTimerRoot != null &&
                          arcadeState != null &&
                          gameFlow != null &&
                          gameFlow.IsArcadeModeEnabled &&
                          gameFlow.currentPhase == BullfightGameFlow.GamePhase.PhaseOne &&
                          !gameFlow.IsTutorialActive &&
                          activeArcadePhaseSummaryHud == null &&
                          activeArcadeRunSummaryHud == null;

        if (arcadePhaseTimerRoot != null)
            arcadePhaseTimerRoot.gameObject.SetActive(shouldShow);

        if (!shouldShow || arcadePhaseTimerText == null || arcadePhaseTimerPanel == null || arcadeState == null)
            return;

        float secondsRemaining = Mathf.Max(0f, arcadeState.PhaseOneTimeRemaining);
        int totalSeconds = Mathf.CeilToInt(secondsRemaining);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        arcadePhaseTimerText.text = $"{minutes:00}:{seconds:00}";

        if (secondsRemaining <= 10f)
        {
            float pulse = 1f + (Mathf.Sin(Time.unscaledTime * 10f) * 0.06f);
            arcadePhaseTimerRoot.localScale = new Vector3(pulse, pulse, 1f);
            arcadePhaseTimerText.color = new Color(1f, 0.78f, 0.76f, 1f);
            arcadePhaseTimerPanel.color = new Color(0.54f, 0.09f, 0.09f, 0.94f);
        }
        else if (secondsRemaining <= 30f)
        {
            arcadePhaseTimerRoot.localScale = Vector3.one;
            arcadePhaseTimerText.color = new Color(1f, 0.93f, 0.72f, 1f);
            arcadePhaseTimerPanel.color = new Color(0.58f, 0.29f, 0.08f, 0.92f);
        }
        else
        {
            arcadePhaseTimerRoot.localScale = Vector3.one;
            arcadePhaseTimerText.color = bullBossTitleColor;
            arcadePhaseTimerPanel.color = new Color(0.21f, 0.08f, 0.08f, 0.88f);
        }
    }

    private void UpdateArcadeComboRoot()
    {
        ArcadeScoreState arcadeState = gameFlow != null && gameFlow.ArcadeScoring != null
            ? gameFlow.ArcadeScoring.State
            : null;

        bool gameplayVisible = arcadeState != null &&
                               gameFlow != null &&
                               gameFlow.IsArcadeModeEnabled &&
                               gameFlow.currentPhase != BullfightGameFlow.GamePhase.Ending &&
                               !gameFlow.IsTutorialActive &&
                               activeArcadePhaseSummaryHud == null &&
                               activeArcadeRunSummaryHud == null;

        if (arcadeComboTransientTimer > 0f)
            arcadeComboTransientTimer = Mathf.Max(0f, arcadeComboTransientTimer - Time.unscaledDeltaTime);

        if (arcadeComboRoot == null || arcadeComboHeader == null || arcadeComboValue == null || arcadeComboStreak == null)
            return;

        if (arcadeComboMode == ArcadeComboDisplayMode.Carry && arcadeComboTransientTimer > 0f)
        {
            arcadeComboRoot.gameObject.SetActive(true);
            arcadeComboHeader.gameObject.SetActive(true);
            arcadeComboHeader.text = "CARRY OVER";
            arcadeComboValue.text = arcadeComboTransientValue;
            arcadeComboValue.color = bullBossGold;
            arcadeComboStreak.text = "COMBO CONTINUES";
            arcadeComboStreak.color = bullBossTitleColor;
            return;
        }

        if (arcadeComboMode == ArcadeComboDisplayMode.Break && arcadeComboTransientTimer > 0f)
        {
            arcadeComboRoot.gameObject.SetActive(true);
            arcadeComboHeader.gameObject.SetActive(true);
            arcadeComboHeader.text = "COMBO BREAK";
            arcadeComboValue.text = arcadeComboTransientValue;
            arcadeComboValue.color = new Color(0.74f, 0.74f, 0.74f, 1f);
            arcadeComboStreak.text = string.Empty;
            return;
        }

        if (arcadeComboTransientTimer <= 0f)
            arcadeComboMode = ArcadeComboDisplayMode.Hidden;

        if (!gameplayVisible || arcadeState == null || arcadeState.ComboCount <= 0)
        {
            arcadeComboRoot.gameObject.SetActive(false);
            return;
        }

        arcadeComboMode = ArcadeComboDisplayMode.Normal;
        arcadeComboRoot.gameObject.SetActive(true);
        arcadeComboHeader.gameObject.SetActive(false);
        arcadeComboHeader.text = string.Empty;
        arcadeComboValue.text = $"COMBO {arcadeState.ComboCount}";
        arcadeComboValue.color = bullBossTitleColor;
        arcadeComboStreak.text = $"x{arcadeState.ComboMultiplier:0.00}";
        arcadeComboStreak.color = bullBossGold;
    }

    private void UpdateArcadeJudgePopupState()
    {
        if (arcadeJudgeScoreRoot == null || arcadeJudgeScoreText == null)
            return;

        BullTimingRing timingRing = GetCachedArcadeTimingRing();
        Vector2 anchorPosition = timingRing != null
            ? timingRing.FeedbackAnchoredPosition + new Vector2(0f, -58f)
            : new Vector2(0f, 42f);
        arcadeJudgeScoreRoot.anchoredPosition = anchorPosition;

        if (activeJudgeScoreEvent == null)
        {
            arcadeJudgeScoreRoot.gameObject.SetActive(false);
            return;
        }

        activeJudgeScoreTimer += Time.unscaledDeltaTime;
        bool expanded = activeJudgeScoreTimer >= 0.12f &&
                        activeJudgeScoreEvent.UsesCombo &&
                        activeJudgeScoreEvent.ComboMultiplierApplied > 1.001f &&
                        activeJudgeScoreEvent.IsPositive;

        string baseText = activeJudgeScoreEvent.IsPositive
            ? $"+{activeJudgeScoreEvent.BasePoints}"
            : $"-{activeJudgeScoreEvent.AwardedPoints}";
        string expandedText = activeJudgeScoreEvent.IsPositive
            ? $"+{activeJudgeScoreEvent.AwardedPoints}  x{activeJudgeScoreEvent.ComboMultiplierApplied:0.0}"
            : baseText;

        arcadeJudgeScoreRoot.gameObject.SetActive(true);
        arcadeJudgeScoreText.text = expanded ? expandedText : baseText;
        arcadeJudgeScoreText.color = activeJudgeScoreEvent.IsPositive
            ? activeJudgeScoreEvent.JudgeResult switch
            {
                "Perfect!" => new Color(0.76f, 0.95f, 0.62f, 1f),
                "Good" => new Color(1f, 0.78f, 0.42f, 1f),
                _ => bullBossTitleColor
            }
            : new Color(0.96f, 0.34f, 0.24f, 1f);

        float life = 1.05f;
        float fadeStart = 0.76f;
        Color color = arcadeJudgeScoreText.color;
        if (activeJudgeScoreTimer > fadeStart)
        {
            float alpha = 1f - Mathf.Clamp01((activeJudgeScoreTimer - fadeStart) / (life - fadeStart));
            color.a = alpha;
        }

        arcadeJudgeScoreText.color = color;
        arcadeJudgeScoreRoot.localScale = Vector3.one * (expanded ? 1.08f : 1f);

        if (activeJudgeScoreTimer >= life)
        {
            activeJudgeScoreEvent = null;
            activeJudgeScoreTimer = 0f;
            arcadeJudgeScoreRoot.gameObject.SetActive(false);
        }
    }

    private void UpdateArcadeEventPopupState()
    {
        if (arcadeEventPopupRoot == null || arcadeEventPopupText == null)
            return;

        if (activeEventPopup == null && arcadeEventPopupQueue.Count > 0)
        {
            activeEventPopup = arcadeEventPopupQueue.Dequeue();
            activeEventPopupTimer = 0f;
        }

        if (activeEventPopup == null)
        {
            arcadeEventPopupRoot.gameObject.SetActive(false);
            return;
        }

        activeEventPopupTimer += Time.unscaledDeltaTime;
        arcadeEventPopupRoot.gameObject.SetActive(true);
        arcadeEventPopupText.text = $"{activeEventPopup.Label}  {(activeEventPopup.IsPositive ? "+" : "-")}{activeEventPopup.AwardedPoints}";
        arcadeEventPopupText.color = activeEventPopup.IsPositive
            ? new Color(1f, 0.93f, 0.66f, 1f)
            : new Color(0.96f, 0.34f, 0.24f, 1f);

        float fadeStart = 0.62f;
        float life = 0.96f;
        Color color = arcadeEventPopupText.color;
        if (activeEventPopupTimer > fadeStart)
        {
            float alpha = 1f - Mathf.Clamp01((activeEventPopupTimer - fadeStart) / (life - fadeStart));
            color.a = alpha;
        }

        arcadeEventPopupText.color = color;
        arcadeEventPopupRoot.anchoredPosition = new Vector2(0f, -96f + Mathf.Lerp(0f, 18f, Mathf.Clamp01(activeEventPopupTimer / life)));

        if (activeEventPopupTimer >= life)
        {
            activeEventPopup = null;
            activeEventPopupTimer = 0f;
            arcadeEventPopupRoot.gameObject.SetActive(false);
        }
    }

    private void UpdateArcadeToastState()
    {
        if (arcadeToastRoot == null || arcadeToastText == null || arcadeToastPanel == null)
            return;

        if (arcadeToastTimer <= 0f)
        {
            arcadeToastRoot.gameObject.SetActive(false);
            return;
        }

        arcadeToastTimer = Mathf.Max(0f, arcadeToastTimer - Time.unscaledDeltaTime);
        arcadeToastRoot.gameObject.SetActive(true);
        float shake = Mathf.Sin((1.4f - arcadeToastTimer) * 28f) * Mathf.Clamp01(arcadeToastTimer / 1.4f) * 12f;
        arcadeToastRoot.anchoredPosition = new Vector2(shake, -96f);

        float fadeStart = 1.0f;
        float alpha = arcadeToastTimer > fadeStart ? 1f : Mathf.Clamp01(arcadeToastTimer / fadeStart);
        Color panelColor = arcadeToastPanel.color;
        panelColor.a = 0.92f * alpha;
        arcadeToastPanel.color = panelColor;
        Color textColor = arcadeToastText.color;
        textColor.a = alpha;
        arcadeToastText.color = textColor;
    }

    private void UpdateArcadePhaseClearPanelState()
    {
        if (arcadePhaseClearRoot == null || arcadePhaseClearTitle == null || arcadePhaseClearPanel == null)
            return;

        if (activeArcadePhaseSummaryHud == null)
        {
            arcadePhaseClearRoot.gameObject.SetActive(false);
            return;
        }

        activeArcadePhaseSummaryTimer += Time.unscaledDeltaTime;
        arcadePhaseClearRoot.gameObject.SetActive(true);
        arcadePhaseClearTitle.text = activeArcadePhaseSummaryHud.Title;

        for (int index = 0; index < arcadePhaseClearLines.Length; index++)
        {
            Text line = arcadePhaseClearLines[index];
            if (line == null)
                continue;

            bool shouldShowLine = index < activeArcadePhaseSummaryHud.Items.Count &&
                                  activeArcadePhaseSummaryTimer >= index * 0.25f;
            line.gameObject.SetActive(shouldShowLine);
            if (!shouldShowLine)
                continue;

            ArcadePhaseSummaryItem item = activeArcadePhaseSummaryHud.Items[index];
            if (item.IsCarryOnly)
                line.text = string.IsNullOrEmpty(item.ValueText) ? item.Label : $"{item.Label}   {item.ValueText}";
            else
                line.text = $"{item.Label}   +{item.Points}";
            line.color = item.IsCarryOnly ? bullBossTitleColor : bullBossGold;
        }

        if (activeArcadePhaseSummaryTimer < activeArcadePhaseSummaryHud.DisplayDuration)
            return;

        activeArcadePhaseSummaryHud = null;
        activeArcadePhaseSummaryTimer = 0f;
        arcadePhaseClearRoot.gameObject.SetActive(false);
    }

    private void UpdateArcadeFinalResultPanelState()
    {
        if (arcadeFinalResultRoot == null || arcadeFinalResultPanel == null)
            return;

        if (activeArcadeRunSummaryHud == null)
        {
            arcadeFinalResultRoot.gameObject.SetActive(false);
            return;
        }

        activeArcadeRunSummaryTimer += Time.unscaledDeltaTime;
        arcadeFinalResultRoot.gameObject.SetActive(true);

        bool isNumberOneCelebration = activeArcadeRunSummaryHud.IsNewRecord && activeArcadeRunSummaryHud.IsRanked;
        Color panelColor = isNumberOneCelebration
            ? new Color(0.80f, 0.62f, 0.10f, 0.985f)
            : new Color(0.15f, 0.04f, 0.04f, 0.97f);
        Color textColor = isNumberOneCelebration ? Color.white : bullBossTitleColor;
        Color scoreColor = isNumberOneCelebration ? Color.white : bullBossGold;

        arcadeFinalResultPanel.color = panelColor;

        if (arcadeFinalResultTitle != null)
        {
            arcadeFinalResultTitle.text = activeArcadeRunSummaryHud.EndingTitle;
            arcadeFinalResultTitle.color = textColor;
        }

        if (arcadeFinalResultBanner != null)
        {
            arcadeFinalResultBanner.gameObject.SetActive(activeArcadeRunSummaryHud.IsNewRecord);
            arcadeFinalResultBanner.text = isNumberOneCelebration ? "YOU ARE NO.1!" : "NEW RECORD";
            arcadeFinalResultBanner.color = isNumberOneCelebration ? Color.white : bullBossGold;
            arcadeFinalResultBanner.fontSize = isNumberOneCelebration ? 42 : 34;
        }

        if (arcadeFinalResultScoreValue != null)
        {
            arcadeFinalResultScoreValue.text = FormatArcadeScore(activeArcadeRunSummaryHud.FinalScore);
            arcadeFinalResultScoreValue.color = scoreColor;
            arcadeFinalResultScoreValue.fontSize = 228;
        }

        SetFinalResultRow(0, $"HIGH SCORE      {FormatArcadeScore(activeArcadeRunSummaryHud.NewHighScore)}");
        SetFinalResultRow(1, $"MAX COMBO       x{activeArcadeRunSummaryHud.MaxComboMultiplier:0.00} / {activeArcadeRunSummaryHud.MaxComboCount}");
        SetFinalResultRow(2, $"PHASE 1         {FormatArcadeSignedScore(activeArcadeRunSummaryHud.Phase1Subtotal)}");
        SetFinalResultRow(3, $"PHASE 2         {FormatArcadeSignedScore(activeArcadeRunSummaryHud.Phase2Subtotal)}");
        SetFinalResultRow(4, $"CLEAR BONUS     {FormatArcadeSignedScore(activeArcadeRunSummaryHud.ClearBonusSubtotal)}");
        SetFinalResultRow(5, $"RANKING         {activeArcadeRunSummaryHud.RankingStatusText}");

        for (int index = 0; index < arcadeFinalResultRows.Length; index++)
        {
            if (arcadeFinalResultRows[index] == null)
                continue;

            arcadeFinalResultRows[index].color = textColor;
        }

        if (arcadeFinalResultHint != null)
        {
            bool canDismiss = activeArcadeRunSummaryTimer >= 2.5f;
            arcadeFinalResultHint.gameObject.SetActive(true);
            arcadeFinalResultHint.color = textColor;
            arcadeFinalResultHint.text = canDismiss ? "A \u7e7c\u7e8c / 30\u79d2\u5f8c\u81ea\u52d5\u95dc\u9589" : "\u7d50\u7b97\u4e2d...";
            ApplyLocalizedUiFont(arcadeFinalResultHint, arcadeFinalResultHint.text, 37, wrap: true, VerticalWrapMode.Truncate, minBestFitSize: 18);
        }

        bool dismissRequested = activeArcadeRunSummaryTimer >= 2.5f && WasArcadeConfirmRequestedThisFrame();
        bool autoDismiss = activeArcadeRunSummaryTimer >= 30f;
        if (!dismissRequested && !autoDismiss)
            return;

        activeArcadeRunSummaryHud = null;
        activeArcadeRunSummaryTimer = 0f;
        arcadeFinalResultDismissed = true;
        arcadeFinalResultRoot.gameObject.SetActive(false);
        ArcadeFinalResultClosed?.Invoke();
    }

    private void UpdateArcadeModalBackdropState()
    {
        if (arcadeModalBackdrop == null)
            return;

        bool shouldShow = activeArcadePhaseSummaryHud != null || activeArcadeRunSummaryHud != null;
        arcadeModalBackdrop.gameObject.SetActive(shouldShow);
    }

    private void EnsureArcadeScoreRoot()
    {
        if (arcadeScoreRoot != null)
            return;

        arcadeScoreRoot = GetOrCreateUiRect(hudCanvasRect, "ArcadeScoreRoot");
        arcadeScoreRoot.anchorMin = new Vector2(1f, 1f);
        arcadeScoreRoot.anchorMax = new Vector2(1f, 1f);
        arcadeScoreRoot.pivot = new Vector2(1f, 1f);
        arcadeScoreRoot.anchoredPosition = new Vector2(-40f, -78f);
        arcadeScoreRoot.sizeDelta = new Vector2(430f, 214f);

        arcadeScorePanel = GetOrCreateUiImage(arcadeScoreRoot, "Background");
        StretchToFillParent(arcadeScorePanel.rectTransform);
        arcadeScorePanel.color = new Color(0.12f, 0.04f, 0.04f, 0.86f);

        arcadeHighScoreLabel = GetOrCreateUiText(arcadeScoreRoot, "HighScoreLabel");
        ConfigureArcadeText(arcadeHighScoreLabel, new Vector2(22f, -18f), new Vector2(180f, 24f), TextAnchor.MiddleLeft, 19, bullBossGold, FontStyle.Bold, "HIGH SCORE");

        arcadeHighScoreValue = GetOrCreateUiText(arcadeScoreRoot, "HighScoreValue");
        ConfigureArcadeText(arcadeHighScoreValue, new Vector2(22f, -42f), new Vector2(280f, 34f), TextAnchor.MiddleLeft, 24, bullBossTitleColor, FontStyle.Bold, FormatArcadeScore(0));

        arcadeNewHighScoreBadge = GetOrCreateUiText(arcadeScoreRoot, "NewBadge");
        ConfigureArcadeText(arcadeNewHighScoreBadge, new Vector2(123.9f, -4.4f), new Vector2(224.3f, 93.1f), TextAnchor.MiddleCenter, 45, new Color(1f, 0.95f, 0.4f, 1f), FontStyle.Bold, "NEW");

        arcadeScoreLabel = GetOrCreateUiText(arcadeScoreRoot, "ScoreLabel");
        ConfigureArcadeText(arcadeScoreLabel, new Vector2(22f, -84f), new Vector2(180f, 24f), TextAnchor.MiddleLeft, 23, bullBossGold, FontStyle.Bold, "SCORE");

        arcadeScoreValue = GetOrCreateUiText(arcadeScoreRoot, "ScoreValue");
        ConfigureArcadeText(arcadeScoreValue, new Vector2(20f, -108f), new Vector2(372f, 60f), TextAnchor.MiddleLeft, 42, bullBossTitleColor, FontStyle.Bold, FormatArcadeScore(0));

        arcadeEligibilityPill = GetOrCreateUiImage(arcadeScoreRoot, "EligibilityPill");
        RectTransform pillRect = arcadeEligibilityPill.rectTransform;
        pillRect.anchorMin = new Vector2(0f, 0f);
        pillRect.anchorMax = new Vector2(1f, 0f);
        pillRect.pivot = new Vector2(0.5f, 0f);
        pillRect.anchoredPosition = new Vector2(0f, 14f);
        pillRect.sizeDelta = new Vector2(-36f, 32f);
        arcadeEligibilityPill.color = new Color(0.41f, 0.31f, 0.08f, 0.94f);

        arcadeEligibilityText = GetOrCreateUiText(arcadeEligibilityPill.rectTransform, "EligibilityText");
        ConfigureArcadeText(arcadeEligibilityText, new Vector2(0f, -4f), new Vector2(320f, 24f), TextAnchor.MiddleCenter, 16, bullBossTitleColor, FontStyle.Bold, "RANKING VALID");
    }

    private void EnsureArcadePhaseTimer()
    {
        if (arcadePhaseTimerRoot != null)
            return;

        arcadePhaseTimerRoot = GetOrCreateUiRect(hudCanvasRect, "ArcadePhaseTimerRoot");
        arcadePhaseTimerRoot.anchorMin = new Vector2(0.5f, 1f);
        arcadePhaseTimerRoot.anchorMax = new Vector2(0.5f, 1f);
        arcadePhaseTimerRoot.pivot = new Vector2(0.5f, 1f);
        arcadePhaseTimerRoot.anchoredPosition = new Vector2(0f, -110f);
        arcadePhaseTimerRoot.sizeDelta = new Vector2(214f, 54f);

        arcadePhaseTimerPanel = GetOrCreateUiImage(arcadePhaseTimerRoot, "Background");
        StretchToFillParent(arcadePhaseTimerPanel.rectTransform);
        arcadePhaseTimerPanel.color = new Color(0.21f, 0.08f, 0.08f, 0.88f);

        arcadePhaseTimerText = GetOrCreateUiText(arcadePhaseTimerRoot, "TimerText");
        ConfigureArcadeText(arcadePhaseTimerText, new Vector2(0f, -4f), new Vector2(200f, 40f), TextAnchor.MiddleCenter, 28, bullBossTitleColor, FontStyle.Bold, "02:00");
        arcadePhaseTimerRoot.gameObject.SetActive(false);
    }

    private void EnsureArcadeComboRoot()
    {
        if (arcadeComboRoot != null)
            return;

        arcadeComboRoot = GetOrCreateUiRect(hudCanvasRect, "ArcadeComboRoot");
        arcadeComboRoot.anchorMin = new Vector2(1f, 0.5f);
        arcadeComboRoot.anchorMax = new Vector2(1f, 0.5f);
        arcadeComboRoot.pivot = new Vector2(1f, 0.5f);
        arcadeComboRoot.anchoredPosition = new Vector2(-46f, 72f);
        arcadeComboRoot.sizeDelta = new Vector2(360f, 150f);

        arcadeComboPanel = GetOrCreateUiImage(arcadeComboRoot, "Background");
        StretchToFillParent(arcadeComboPanel.rectTransform);
        arcadeComboPanel.color = new Color(0.14f, 0.05f, 0.05f, 0.82f);

        arcadeComboHeader = GetOrCreateUiText(arcadeComboRoot, "Header");
        ConfigureArcadeText(arcadeComboHeader, new Vector2(0f, -18f), new Vector2(280f, 28f), TextAnchor.MiddleCenter, 22, bullBossGold, FontStyle.Bold, "COMBO");

        arcadeComboValue = GetOrCreateUiText(arcadeComboRoot, "Value");
        ConfigureArcadeText(arcadeComboValue, new Vector2(0f, -33.5f), new Vector2(330f, 58f), TextAnchor.MiddleCenter, 54, bullBossTitleColor, FontStyle.Bold, "COMBO 1");

        arcadeComboStreak = GetOrCreateUiText(arcadeComboRoot, "Streak");
        ConfigureArcadeText(arcadeComboStreak, new Vector2(0f, -106.7f), new Vector2(300f, 28f), TextAnchor.MiddleCenter, 28, bullBossGold, FontStyle.Bold, "x1.00");

        arcadeComboRoot.gameObject.SetActive(false);
    }

    private void EnsureArcadeJudgePopup()
    {
        if (arcadeJudgeScoreRoot != null)
            return;

        arcadeJudgeScoreRoot = GetOrCreateUiRect(hudCanvasRect, "ArcadeJudgePopup");
        arcadeJudgeScoreRoot.anchorMin = new Vector2(0.5f, 0.5f);
        arcadeJudgeScoreRoot.anchorMax = new Vector2(0.5f, 0.5f);
        arcadeJudgeScoreRoot.pivot = new Vector2(0.5f, 0.5f);
        arcadeJudgeScoreRoot.anchoredPosition = new Vector2(0f, 42f);
        arcadeJudgeScoreRoot.sizeDelta = new Vector2(520f, 46f);

        arcadeJudgeScoreText = GetOrCreateUiText(arcadeJudgeScoreRoot, "JudgeScoreText");
        ConfigureArcadeText(arcadeJudgeScoreText, Vector2.zero, new Vector2(520f, 46f), TextAnchor.MiddleCenter, 28, bullBossTitleColor, FontStyle.Bold, string.Empty);
        arcadeJudgeScoreRoot.gameObject.SetActive(false);
    }

    private void EnsureArcadeEventPopup()
    {
        if (arcadeEventPopupRoot != null)
            return;

        arcadeEventPopupRoot = GetOrCreateUiRect(hudCanvasRect, "ArcadeEventPopup");
        arcadeEventPopupRoot.anchorMin = new Vector2(0.5f, 0.5f);
        arcadeEventPopupRoot.anchorMax = new Vector2(0.5f, 0.5f);
        arcadeEventPopupRoot.pivot = new Vector2(0.5f, 0.5f);
        arcadeEventPopupRoot.anchoredPosition = new Vector2(0f, -96f);
        arcadeEventPopupRoot.sizeDelta = new Vector2(860f, 42f);

        arcadeEventPopupText = GetOrCreateUiText(arcadeEventPopupRoot, "EventPopupText");
        ConfigureArcadeText(arcadeEventPopupText, Vector2.zero, new Vector2(860f, 42f), TextAnchor.MiddleCenter, 24, bullBossTitleColor, FontStyle.Bold, string.Empty);
        arcadeEventPopupRoot.gameObject.SetActive(false);
    }

    private void EnsureArcadeToast()
    {
        if (arcadeToastRoot != null)
            return;

        arcadeToastRoot = GetOrCreateUiRect(hudCanvasRect, "ArcadeToastRoot");
        arcadeToastRoot.anchorMin = new Vector2(0.5f, 1f);
        arcadeToastRoot.anchorMax = new Vector2(0.5f, 1f);
        arcadeToastRoot.pivot = new Vector2(0.5f, 1f);
        arcadeToastRoot.anchoredPosition = new Vector2(0f, -96f);
        arcadeToastRoot.sizeDelta = new Vector2(520f, 58f);

        arcadeToastPanel = GetOrCreateUiImage(arcadeToastRoot, "ToastBackground");
        StretchToFillParent(arcadeToastPanel.rectTransform);
        arcadeToastPanel.color = new Color(0.44f, 0.08f, 0.08f, 0.92f);

        arcadeToastText = GetOrCreateUiText(arcadeToastRoot, "ToastText");
        ConfigureArcadeText(arcadeToastText, Vector2.zero, new Vector2(480f, 40f), TextAnchor.MiddleCenter, 24, new Color(1f, 0.92f, 0.88f, 1f), FontStyle.Bold, "本局不計入排行榜");
        arcadeToastRoot.gameObject.SetActive(false);
    }

    private void EnsureArcadeModalPanels()
    {
        if (arcadeModalBackdrop == null)
        {
            arcadeModalBackdrop = GetOrCreateUiImage(hudCanvasRect, "ArcadeModalBackdrop");
            StretchToFullScreen(arcadeModalBackdrop.rectTransform);
            arcadeModalBackdrop.color = new Color(0.02f, 0.01f, 0.01f, 0.72f);
            arcadeModalBackdrop.raycastTarget = false;
            arcadeModalBackdrop.gameObject.SetActive(false);
        }

        if (arcadePhaseClearRoot == null)
        {
            arcadePhaseClearRoot = GetOrCreateUiRect(hudCanvasRect, "ArcadePhaseClearPanel");
            arcadePhaseClearRoot.anchorMin = new Vector2(0.5f, 0.5f);
            arcadePhaseClearRoot.anchorMax = new Vector2(0.5f, 0.5f);
            arcadePhaseClearRoot.pivot = new Vector2(0.5f, 0.5f);
            arcadePhaseClearRoot.anchoredPosition = Vector2.zero;
            arcadePhaseClearRoot.sizeDelta = new Vector2(760f, 420f);

            arcadePhaseClearPanel = GetOrCreateUiImage(arcadePhaseClearRoot, "Background");
            StretchToFillParent(arcadePhaseClearPanel.rectTransform);
            arcadePhaseClearPanel.color = new Color(0.15f, 0.04f, 0.04f, 0.94f);

            arcadePhaseClearTitle = GetOrCreateUiText(arcadePhaseClearRoot, "Title");
            ConfigureArcadeText(arcadePhaseClearTitle, new Vector2(0f, -48f), new Vector2(560f, 42f), TextAnchor.MiddleCenter, 34, bullBossTitleColor, FontStyle.Bold, "PHASE 1 CLEAR");

            for (int index = 0; index < arcadePhaseClearLines.Length; index++)
            {
                arcadePhaseClearLines[index] = GetOrCreateUiText(arcadePhaseClearRoot, $"Line{index + 1}");
                ConfigureArcadeText(
                    arcadePhaseClearLines[index],
                    new Vector2(0f, -132f - (index * 40f)),
                    new Vector2(620f, 32f),
                    TextAnchor.MiddleCenter,
                    24,
                    bullBossTitleColor,
                    FontStyle.Bold,
                    string.Empty);
                arcadePhaseClearLines[index].gameObject.SetActive(false);
            }

            arcadePhaseClearRoot.gameObject.SetActive(false);
        }

        if (arcadeFinalResultRoot == null)
        {
            arcadeFinalResultRoot = GetOrCreateUiRect(hudCanvasRect, "ArcadeFinalResultPanel");
            StretchToFullScreen(arcadeFinalResultRoot);
            arcadeFinalResultRoot.offsetMin = new Vector2(40f, 28f);
            arcadeFinalResultRoot.offsetMax = new Vector2(-40f, -28f);

            arcadeFinalResultPanel = GetOrCreateUiImage(arcadeFinalResultRoot, "Background");
            StretchToFillParent(arcadeFinalResultPanel.rectTransform);
            arcadeFinalResultPanel.color = new Color(0.15f, 0.04f, 0.04f, 0.97f);

            arcadeFinalResultTitle = GetOrCreateUiText(arcadeFinalResultRoot, "Title");
            ConfigureArcadeText(arcadeFinalResultTitle, new Vector2(0f, -52f), new Vector2(1031.27f, 171.7f), TextAnchor.MiddleCenter, 95, bullBossTitleColor, FontStyle.Bold, "ARCADE RESULT");

            arcadeFinalResultBanner = GetOrCreateUiText(arcadeFinalResultRoot, "Banner");
            ConfigureArcadeText(arcadeFinalResultBanner, new Vector2(0f, -118f), new Vector2(620f, 42f), TextAnchor.MiddleCenter, 34, new Color(1f, 0.95f, 0.4f, 1f), FontStyle.Bold, "NEW RECORD");

            arcadeFinalResultScoreValue = GetOrCreateUiText(arcadeFinalResultRoot, "FinalScore");
            ConfigureArcadeText(arcadeFinalResultScoreValue, new Vector2(0f, -224f), new Vector2(1436.1f, 239.38f), TextAnchor.MiddleCenter, 228, bullBossGold, FontStyle.Bold, FormatArcadeScore(0));
            Outline finalScoreOutline = arcadeFinalResultScoreValue.GetComponent<Outline>();
            if (finalScoreOutline == null)
                finalScoreOutline = arcadeFinalResultScoreValue.gameObject.AddComponent<Outline>();
            finalScoreOutline.effectColor = new Color(0.16f, 0.05f, 0.02f, 0.92f);
            finalScoreOutline.effectDistance = new Vector2(4f, -4f);

            for (int index = 0; index < arcadeFinalResultRows.Length; index++)
            {
                arcadeFinalResultRows[index] = GetOrCreateUiText(arcadeFinalResultRoot, $"Row{index + 1}");
                ConfigureArcadeText(
                    arcadeFinalResultRows[index],
                    new Vector2(194f, -502f - (index * 52f)),
                    new Vector2(920f, 40f),
                    TextAnchor.MiddleCenter,
                    38,
                    bullBossTitleColor,
                    FontStyle.Bold,
                    string.Empty);
                arcadeFinalResultRows[index].alignment = TextAnchor.MiddleLeft;
            }

            arcadeFinalResultHint = GetOrCreateUiText(arcadeFinalResultRoot, "Hint");
            ConfigureArcadeText(arcadeFinalResultHint, new Vector2(0f, -912f), new Vector2(720f, 32f), TextAnchor.MiddleCenter, 37, bullBossTitleColor, FontStyle.Italic, "\u7d50\u7b97\u4e2d...");
            arcadeFinalResultRoot.gameObject.SetActive(false);
        }

        arcadeModalBackdrop.transform.SetAsLastSibling();
        arcadePhaseClearRoot.SetAsLastSibling();
        arcadeFinalResultRoot.SetAsLastSibling();
    }

    private void SetFinalResultRow(int index, string value)
    {
        if (index < 0 || index >= arcadeFinalResultRows.Length || arcadeFinalResultRows[index] == null)
            return;

        arcadeFinalResultRows[index].text = value;
    }

    private BullTimingRing GetCachedArcadeTimingRing()
    {
        if (cachedArcadeTimingRing == null)
            cachedArcadeTimingRing = BullfightSceneCache.FindObject<BullTimingRing>();

        return cachedArcadeTimingRing;
    }

    private static void ConfigureArcadeText(
        Text text,
        Vector2 anchoredPosition,
        Vector2 size,
        TextAnchor alignment,
        int fontSize,
        Color color,
        FontStyle style,
        string value)
    {
        if (text == null)
            return;

        Vector2 anchor;
        Vector2 pivot;
        switch (alignment)
        {
            case TextAnchor.UpperCenter:
            case TextAnchor.MiddleCenter:
            case TextAnchor.LowerCenter:
                anchor = new Vector2(0.5f, 1f);
                pivot = new Vector2(0.5f, 1f);
                break;
            case TextAnchor.UpperRight:
            case TextAnchor.MiddleRight:
            case TextAnchor.LowerRight:
                anchor = new Vector2(1f, 1f);
                pivot = new Vector2(1f, 1f);
                break;
            default:
                anchor = new Vector2(0f, 1f);
                pivot = new Vector2(0f, 1f);
                break;
        }

        RectTransform rect = text.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        text.alignment = alignment;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.text = value;
        text.raycastTarget = false;
        ApplyLocalizedUiFont(text, value, fontSize, wrap: true, VerticalWrapMode.Truncate, minBestFitSize: Mathf.Max(12, fontSize - 6));
    }

    private static void StretchToFillParent(RectTransform rect)
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

    private static float GetScoreTickerDuration(int delta)
    {
        if (delta < 1000)
            return 0.18f;
        if (delta < 3000)
            return 0.28f;
        return 0.40f;
    }

    private static string FormatArcadeScore(int score)
    {
        return Mathf.Max(0, score).ToString("D8");
    }

    private static string FormatArcadeSignedScore(int score)
    {
        string sign = score < 0 ? "-" : "+";
        return $"{sign}{Mathf.Abs(score):D8}";
    }

    private static float GetComboMultiplierForDisplay(int comboCount)
    {
        if (comboCount <= 1)
            return 1f;
        if (comboCount == 2)
            return 1.15f;
        if (comboCount == 3)
            return 1.30f;
        if (comboCount == 4)
            return 1.50f;
        if (comboCount == 5)
            return 1.70f;
        return 2f;
    }

    private static bool WasArcadeConfirmRequestedThisFrame()
    {
        bool keyboardRequested = Keyboard.current != null &&
                                 (Keyboard.current.enterKey.wasPressedThisFrame ||
                                  Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
                                  Keyboard.current.spaceKey.wasPressedThisFrame);

        bool gamepadRequested = Gamepad.current != null &&
                                (Gamepad.current.startButton.wasPressedThisFrame ||
                                 Gamepad.current.buttonSouth.wasPressedThisFrame);

        bool mouseRequested = Mouse.current != null &&
                              Mouse.current.leftButton.wasPressedThisFrame;

        return keyboardRequested || gamepadRequested || mouseRequested;
    }
}
