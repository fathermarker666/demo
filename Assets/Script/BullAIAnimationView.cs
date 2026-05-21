using System;
using UnityEngine;

public sealed class BullAIAnimationView
{
    private static readonly string[] RoamIdleClips =
    {
        "Arm_Bull|Idle_1",
        "Arm_Bull|Idle_2",
        "Arm_Bull|Idle_4",
        "Arm_Bull|Idle_5",
        "Arm_Bull|Idle_6",
        "Arm_Bull|Eat_loop",
        "Arm_Bull|Drink_loop"
    };

    private static readonly string[] RoamMoveClips =
    {
        "Arm_Bull|Walk_F_IP",
        "Arm_Bull|Trot_F_IP"
    };

    private static readonly string[] EngageMoveClips =
    {
        "Arm_Bull|Trot_F_IP",
        "Arm_Bull|Run_F_IP",
        "Arm_Bull|Attack_Run_IP"
    };

    private static readonly string[] FatigueClips =
    {
        "Arm_Bull|Idle_3",
        "Arm_Bull|Idle_5",
        "Arm_Bull|Idle_6"
    };

    private readonly Animator animator;
    private string currentAnimationState;
    private string roamingIdleClip = RoamIdleClips[0];
    private string roamingMoveClip = RoamMoveClips[0];
    private string engagingMoveClip = EngageMoveClips[0];
    private string fatigueClip = FatigueClips[0];
    private float variationTimer;
    private BullAI.BullState lastVariationState;
    private bool lastMovingState;

    public BullAIAnimationView(Animator animator)
    {
        this.animator = animator;
    }

    public void Reset()
    {
        currentAnimationState = null;
        variationTimer = 0f;
        lastVariationState = default;
        lastMovingState = false;
        roamingIdleClip = RoamIdleClips[0];
        roamingMoveClip = RoamMoveClips[0];
        engagingMoveClip = EngageMoveClips[0];
        fatigueClip = FatigueClips[0];
    }

    public void Play(string clipName, float crossFadeDuration, bool forceRestart = false)
    {
        if (string.IsNullOrWhiteSpace(clipName))
            return;

        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        if (!forceRestart && currentAnimationState == clipName)
            return;

        animator.CrossFade(clipName, crossFadeDuration, 0, 0f);
        currentAnimationState = clipName;
    }

    public void PlayState(BullAI bullAI, bool isMovingHorizontally)
    {
        BullAI.BullState state = bullAI != null ? bullAI.currentState : BullAI.BullState.Idle;
        RefreshVariations(state, isMovingHorizontally);

        string nextState = state switch
        {
            BullAI.BullState.Idle => roamingIdleClip,
            BullAI.BullState.Roaming => isMovingHorizontally ? roamingMoveClip : roamingIdleClip,
            BullAI.BullState.Engaging => isMovingHorizontally ? engagingMoveClip : "Arm_Bull|Idle_3",
            BullAI.BullState.Telegraphing => "Arm_Bull|Attack_F_IP",
            BullAI.BullState.Charging => "Arm_Bull|Run_F_IP",
            BullAI.BullState.Impact => "Arm_Bull|Hit_Front",
            BullAI.BullState.Hurt => "Arm_Bull|Hit_Middle",
            BullAI.BullState.PerfectReaction => "Arm_Bull|JumpStart_up",
            BullAI.BullState.CloseRangeImpactReaction => "Arm_Bull|JumpStart_up",
            BullAI.BullState.Fatigued => fatigueClip,
            BullAI.BullState.CirclingReset => "Arm_Bull|Trot_F_IP",
            BullAI.BullState.Dead => "Arm_Bull|Death_L",
            _ => "Arm_Bull|Idle_1"
        };

        Play(nextState, 0.12f);
    }

    private void RefreshVariations(BullAI.BullState state, bool isMovingHorizontally)
    {
        bool changedState = state != lastVariationState || isMovingHorizontally != lastMovingState;
        variationTimer -= Time.deltaTime;
        if (!changedState && variationTimer > 0f)
            return;

        switch (state)
        {
            case BullAI.BullState.Idle:
            case BullAI.BullState.Roaming:
                roamingIdleClip = Pick(RoamIdleClips, roamingIdleClip);
                roamingMoveClip = Pick(RoamMoveClips, roamingMoveClip);
                variationTimer = UnityEngine.Random.Range(1.4f, 3.2f);
                break;
            case BullAI.BullState.Engaging:
                engagingMoveClip = Pick(EngageMoveClips, engagingMoveClip);
                variationTimer = UnityEngine.Random.Range(0.9f, 1.8f);
                break;
            case BullAI.BullState.Fatigued:
                fatigueClip = Pick(FatigueClips, fatigueClip);
                variationTimer = UnityEngine.Random.Range(1.2f, 2.6f);
                break;
            default:
                variationTimer = 0.35f;
                break;
        }

        lastVariationState = state;
        lastMovingState = isMovingHorizontally;
    }

    private static string Pick(string[] options, string current)
    {
        if (options == null || options.Length == 0)
            return current;

        if (options.Length == 1)
            return options[0];

        string next = current;
        for (int i = 0; i < 4 && next == current; i++)
            next = options[UnityEngine.Random.Range(0, options.Length)];

        return next;
    }
}
