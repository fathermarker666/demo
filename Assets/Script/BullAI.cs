using System;
using UnityEngine;

public class BullAI : MonoBehaviour
{
    public event Action<float> OnBanderillasHit;

    private const float ChargePreImpactHoldBuffer = 0.1f;
    private const float MissCommitDistance = 0.9f;
    private const float ChargeContactForwardEpsilon = 0.05f;
    private const float ChargeHitboxContactPadding = 0.01f;
    private const string AnimationAttackForward = "Arm_Bull|Attack_F";
    private const string AnimationAttackForwardInPlace = "Arm_Bull|Attack_F_IP";
    private const string AnimationDeathLeft = "Arm_Bull|Death_L";
    private const string AnimationPerfectReactionStart = "Arm_Bull|JumpStart_up";
    private const string AnimationPerfectReactionAir = "Arm_Bull|JumpAir_up";
    private const float PerfectReactionStartFrames = 16f;
    private const float PerfectReactionAirFrames = 30f;
    private const float PerfectReactionTotalFrames = PerfectReactionStartFrames + PerfectReactionAirFrames;
    private const string AnimationHitFront = "Arm_Bull|Hit_Front";
    private const string AnimationHitMiddle = "Arm_Bull|Hit_Middle";
    private const string AnimationIdleCalm = "Arm_Bull|Idle_1";
    private const string AnimationIdleThreat = "Arm_Bull|Idle_3";
    private const string AnimationRunForwardInPlace = "Arm_Bull|Run_F_IP";
    private const string AnimationTrotForwardInPlace = "Arm_Bull|Trot_F_IP";
    private const string AnimationTurnLeftInPlace = "Arm_Bull|Turn180_L_IP";

    public enum BullState { Idle, Roaming, Engaging, Telegraphing, Charging, Impact, Hurt, PerfectReaction, Fatigued, CirclingReset, Dead }

    [Header("State")] public BullState currentState = BullState.Idle;
    [Header("References")] public Transform player; public PlayerStats playerStats; public BullfightPlayerController playerController; public BullStats bullStats; public BullTimingRing timingScript; public BullfightGameFlow gameFlow; public BullfightSpawnManager spawnManager;
    [Header("Trigger")] public float detectRange = 18f; public float dangerTriggerDistance = 3.6f; public float tauntTriggerDistance = 9f; public float telegraphRange = 3f; public float telegraphMissDistance = 1.1f; public float safeReengageDistance = 3.6f; public float banderillasRange = 4.5f;
    [Header("Visibility")] public float telegraphVisibleDot = 0.18f;
    [Header("Movement")] public float roamingSpeed = 1.21f; public float engageApproachSpeed = 0.6f; public float telegraphApproachSpeed = 0.75f; public float chargeSpeed = 1.85f; public float turnSpeed = 6f; public float roamingRadius = 4.2f; public Vector2 roamingPauseRange = new Vector2(0.45f, 1.15f); public float postChargeCircleRadius = 3.3f; public float postChargeCircleDuration = 3f; public float arenaBoundaryPadding = 0.45f;
    [Header("Grounding")] public float groundProbeHeight = 12f; public float groundSnapSpeed = 20f; public float visualGroundOffset = 0f;
    [Header("Combat")] public float collisionDamage = 50f; public float banderillasDamage = 25f; public float hitDistance = 0.75f; public float hitCooldown = 1f; public float minimumChargeTravelDistance = 1.2f; public float minimumChargeHitDelay = 0.65f; public float engageDelay = 0.9f; public float successfulDodgeInvulnerability = 1.2f; public float attackRecoveryDuration = 5.2f; public Vector2 fatigueDurationRange = new Vector2(5f, 5.8f); public float maxChargeDistance = 3.8f; public float telegraphDuration = 1.8f; public float hurtFlinchDuration = 0.45f; public float chargeImpactStopBuffer = 0.4f; public float phaseOnePerfectReactionDuration = 1f;
    [Header("Charge QTE")] public float qteRevealRemainingDistance = 0.9f; public float qteRevealRemainingTime = 0.5f; public float closeRangePenaltyStart = 1.3f; public float closeRangePenaltyEnd = 0.8f; public float telegraphLanePadding = 0.18f; public float cameraClearanceBuffer = 0.16f; public float impactDuration = 0.12f; public float laneCheckHeight = 2f; public float telegraphYOffset = 0.04f; public Color telegraphColor = new Color(1f, 0.14f, 0.14f, 0.9f); public Color telegraphFillColor = new Color(1f, 0.14f, 0.14f, 0.38f);
    [Header("Auto Attack")] public bool enableAutoAttack = true; public float autoAttackInterval = 20f; public float autoAttackTelegraphRange = 3f;
    [Header("Debug")] public bool useDebugTuning = false; public bool logStateTransitions = false;

    private float stateTimer, lastHitTime = -999f, engageTimer, attackRecoveryTimer, chargeStartedAt = -999f, telegraphStartDistance, telegraphTimer, chargeDuration, circlingAngle, circlingDirection = 1f, currentHorizontalMotion, autoAttackTimer, chargeQteTimer, chargeQteDuration;
    private float plannedChargeDistance, chargeLaneHalfWidth, displayedTelegraphTravelDistance;
    private int perfectReactionPhase;
    private bool timingActive, canDamagePlayerThisCharge, pendingCircleReset, hasRoamTarget, hasQueuedMovePosition, hasQueuedMoveRotation, autoAttackPending, autoAttackCommitted, isPlayerInsideChargeLane, dashedThisCharge, impactCirclesAfterCharge, chargeHasEligibleTarget, chargeQteResolved, chargeResultAllowsDamage, externalChargeTimingControl, phaseTwoChargeMotionActive, chargeMissPlayerLockActive;
    private bool tutorialControlActive, tutorialChargeDamageEnabled, tutorialChargeSequenceActive, tutorialChargeSequenceComplete, tutorialChargeHitPlayer, tutorialChargeUsesTiming;
    private string tutorialChargeResult = string.Empty;
    private Animator animator; private BullAIAnimationView animationView; private BullAIChargeTelegraphView chargeTelegraphView; private Vector3 chargeStartPosition, chargeDirection = Vector3.forward, roamCenter, roamTarget, circlingCenter, queuedMovePosition; private Quaternion queuedMoveRotation;
    private Rigidbody bullRigidbody; private Collider bullCollider, playerCollider; private BullChargeHitbox chargeHitbox;
    private readonly Collider[] chargeLaneOverlapResults = new Collider[8];
    private readonly Collider[] chargeHitboxOverlapBuffer = new Collider[8];
    private readonly RaycastHit[] chargeHitboxSweepBuffer = new RaycastHit[8];
    private BullfightGameFlow linkedGameFlow;
    private float locomotionBlendTimer = 0f;
    private const float LocomotionHoldTime = 0.12f;
    private const float AnimationMotionThreshold = 0.05f;

    public bool CanDamagePlayerThisCharge => canDamagePlayerThisCharge;
    public bool CanReceiveChargeTimingInput => currentState == BullState.Charging && timingActive;
    public float CurrentDistanceToPlayer => HorizontalDistanceToPlayer();
    public bool IsTutorialChargeSequenceComplete => tutorialChargeSequenceComplete;
    public bool DidTutorialChargeHitPlayer => tutorialChargeHitPlayer;
    public string TutorialChargeResult => tutorialChargeResult;

    public void StartPhaseTwoChargeAtPlayer(float desiredDuration = -1f)
    {
        if (player == null)
            ResolveReferencesIfNeeded();

        currentState = BullState.Charging;
        ResetChargeQteState();
        canDamagePlayerThisCharge = false;
        chargeResultAllowsDamage = false;
        chargeStartPosition = GetCurrentBullPosition();
        chargeStartedAt = Time.time;

        Vector3 targetPoint = player != null ? player.position : transform.position + transform.forward;
        if (playerStats != null && playerStats.TryGetFirstPersonCameraPoint(out Vector3 cameraPoint))
            targetPoint = cameraPoint;

        chargeDirection = targetPoint - GetCurrentBullPosition();
        chargeDirection.y = 0f;

        if (chargeDirection.sqrMagnitude <= 0.0001f)
            chargeDirection = transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Vector3.forward;

        chargeDirection.Normalize();

        Quaternion chargeRotation = Quaternion.LookRotation(chargeDirection, Vector3.up);
        if (bullRigidbody != null)
        {
            bullRigidbody.rotation = chargeRotation;
            bullRigidbody.angularVelocity = Vector3.zero;
        }

        transform.rotation = chargeRotation;
        queuedMoveRotation = chargeRotation;
        hasQueuedMoveRotation = true;

        float resolvedDuration = desiredDuration > 0f ? Mathf.Max(0.2f, desiredDuration) : -1f;
        plannedChargeDistance = resolvedDuration > 0f
            ? Mathf.Clamp(GetChargeSpeed() * resolvedDuration, minimumChargeTravelDistance, maxChargeDistance)
            : GetPlannedChargeDistance(HorizontalDistanceToPlayer());
        chargeDuration = resolvedDuration > 0f ? resolvedDuration : GetChargeTimingDuration(plannedChargeDistance);
        stateTimer = chargeDuration;
        displayedTelegraphTravelDistance = plannedChargeDistance;
        chargeLaneHalfWidth = GetChargeLaneHalfWidth();
        dashedThisCharge = false;
        phaseTwoChargeMotionActive = true;

        HideChargeTelegraph();
    }

    private void Awake()
    {
        if (useDebugTuning)
            ApplySlowDebugTuning();
        bullRigidbody = GetComponent<Rigidbody>() ?? gameObject.AddComponent<Rigidbody>();
        bullCollider = GetComponent<Collider>();
        if (bullCollider == null)
        {
            BoxCollider body = gameObject.AddComponent<BoxCollider>();
            body.center = new Vector3(0f, 0.75f, 0.2f);
            body.size = new Vector3(0.8f, 1.5f, 2.6f);
            bullCollider = body;
        }
        animator = GetComponent<Animator>();
        animationView = new BullAIAnimationView(animator);
        chargeTelegraphView = new BullAIChargeTelegraphView(transform);
        bullStats = GetComponent<BullStats>() ?? gameObject.AddComponent<BullStats>();
        ConfigureRigidbody();
        EnsureChargeHitbox();
        ResolveReferencesIfNeeded();
        roamCenter = GetCurrentBullPosition();
        currentState = BullState.Idle;
        engageTimer = engageDelay;
        ScheduleNextAutoAttack();
        SnapToGround(true);
        KeepInsideArena();
        CommitQueuedPoseImmediate();
    }

    private void Start()
    {
        ResolveReferencesIfNeeded();
        if (player == null || playerStats == null || playerController == null || timingScript == null)
            Debug.LogWarning("BullAI is missing references. Check Player, PlayerStats, BullfightPlayerController, and BullTimingRing bindings.");
    }

    private void Update()
    {
        if (HasMissingReferences())
            ResolveReferencesIfNeeded();

        if (player == null || playerStats == null || bullStats == null) return;
        if (gameFlow != null && gameFlow.currentPhase == BullfightGameFlow.GamePhase.PhaseZeroTutorial)
        {
            UpdateTutorialBehavior();
            KeepInsideArena();
            SnapToGround(false);
            UpdateAnimation();
            return;
        }

        if (gameFlow != null && gameFlow.currentPhase == BullfightGameFlow.GamePhase.PhaseTwo)
        {
            KeepInsideArena();
            if (phaseTwoChargeMotionActive && currentState == BullState.Charging)
            {
                UpdatePhaseTwoChargeMotion();
                SnapToGround(false);
                UpdateAnimation();
            }
            else
            {
                SnapToGround(false);
            }
            return;
        }

        if (bullStats.currentHealth <= 0f && currentState != BullState.Dead) EnterDeathState();
        if (currentState == BullState.Dead) { UpdateAnimation(); return; }
        KeepInsideArena();
        if (playerStats.isHoldingCloth) bullStats.AddTauntRage(Time.deltaTime);
        if (engageTimer > 0f)
        {
            engageTimer -= Time.deltaTime;
            if (currentState == BullState.Idle) currentState = BullState.Roaming;
            UpdateAnimation();
            return;
        }
        if (gameFlow != null && gameFlow.currentPhase != BullfightGameFlow.GamePhase.PhaseOne)
        {
            currentState = BullState.Fatigued;
            ResetChargeQteState();
            HideChargeTelegraph();
            if (timingScript != null)
                timingScript.HideImmediate();
            UpdateAnimation();
            return;
        }
        float distance = HorizontalDistanceToPlayer();
        attackRecoveryTimer = Mathf.Max(0f, attackRecoveryTimer - Time.deltaTime);
        UpdateAutoAttackTimer();
        HandlePlayerAttack(distance);
        UpdateState(distance);
        KeepInsideArena();
        SnapToGround(false);
        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        UpdateChargeLaneOccupancy();
        MaintainBullPlayerBlocking();
        if (bullRigidbody == null) return;
        Vector3 currentPosition = bullRigidbody.position;
        Vector3 targetPosition = hasQueuedMovePosition ? queuedMovePosition : currentPosition;
        currentHorizontalMotion = HorizontalDistance(currentPosition, targetPosition) / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        if (hasQueuedMoveRotation) bullRigidbody.MoveRotation(queuedMoveRotation);
        if (hasQueuedMovePosition) bullRigidbody.MovePosition(queuedMovePosition);
        if (!bullRigidbody.isKinematic)
        {
            Vector3 velocity = bullRigidbody.velocity;
            velocity.x = 0f;
            velocity.z = 0f;
            bullRigidbody.velocity = velocity;
        }
        hasQueuedMovePosition = false;
        hasQueuedMoveRotation = false;
    }

    private void ApplySlowDebugTuning()
    {
        dangerTriggerDistance = 1f; tauntTriggerDistance = 9f; telegraphRange = 1.35f; autoAttackInterval = 20f; autoAttackTelegraphRange = 3.6f; telegraphMissDistance = 0.9f; safeReengageDistance = 3.6f; engageApproachSpeed = 0.46f; telegraphApproachSpeed = 0.58f; chargeSpeed = 2.6f; postChargeCircleRadius = 3.3f; postChargeCircleDuration = 3f; hitDistance = 0.75f; minimumChargeTravelDistance = 1f; attackRecoveryDuration = 5.2f; fatigueDurationRange = new Vector2(5f, 5.8f); maxChargeDistance = 3.8f; chargeImpactStopBuffer = 0.4f; cameraClearanceBuffer = 0.16f;
    }

    private void UpdateState(float distance)
    {
        switch (currentState)
        {
            case BullState.Idle: currentState = BullState.Roaming; break;
            case BullState.Roaming: UpdateRoaming(distance); break;
            case BullState.Engaging: UpdateEngaging(distance); break;
            case BullState.Telegraphing: UpdateTelegraph(); break;
            case BullState.Charging: UpdateCharge(); break;
            case BullState.Impact: UpdateImpact(); break;
            case BullState.Hurt: UpdateHurt(distance); break;
            case BullState.PerfectReaction: UpdatePerfectReaction(); break;
            case BullState.Fatigued: UpdateFatigue(); break;
            case BullState.CirclingReset: UpdateCirclingReset(distance); break;
        }
    }

    private void UpdateRoaming(float distance)
    {
        if (attackRecoveryTimer <= 0f && HasAutoAttackRequest())
        {
            if (distance <= GetAutoAttackTelegraphRange()) BeginTelegraph(distance); else BeginEngaging();
            return;
        }
        if (attackRecoveryTimer <= 0f && ShouldTriggerAttack(distance))
        {
            if (CanBeginTelegraph(distance)) BeginTelegraph(distance); else BeginEngaging();
            return;
        }
        if (stateTimer > 0f) { stateTimer -= Time.deltaTime; return; }
        if (!hasRoamTarget || HorizontalDistance(GetCurrentBullPosition(), roamTarget) <= 0.35f)
        {
            roamTarget = PickRoamTarget();
            hasRoamTarget = true;
            stateTimer = UnityEngine.Random.Range(roamingPauseRange.x, roamingPauseRange.y);
            return;
        }
        FaceTowards(roamTarget);
        MoveHorizontallyTowards(roamTarget, roamingSpeed);
    }

    private void BeginEngaging() { BeginAutoAttackApproach(); currentState = BullState.Engaging; ResetChargeQteState(); hasRoamTarget = false; stateTimer = 0f; HideChargeTelegraph(); }

    private void UpdateEngaging(float distance)
    {
        if (player == null) { currentState = BullState.Roaming; return; }
        bool forceAutoAttack = HasActiveAutoAttack();
        FacePlayer();
        // TODO(hand-off): Auto-attack telegraph timing/range still needs retuning.
        // Current symptom: if telegraph starts too early, QTE colors/results desync from player expectation.
        if (forceAutoAttack || playerStats.isHoldingCloth) MoveHorizontallyTowards(player.position, GetEngageApproachSpeed());
        if (!forceAutoAttack && !playerStats.isHoldingCloth && distance > detectRange * 1.15f) { currentState = BullState.Roaming; return; }
        if ((forceAutoAttack && distance <= GetAutoAttackTelegraphRange()) || CanBeginTelegraph(distance)) BeginTelegraph(distance);
    }

    private void BeginTelegraph(float distance)
    {
        ConsumeAutoAttack();
        currentState = BullState.Telegraphing;
        ResetChargeQteState();
        canDamagePlayerThisCharge = false;
        telegraphStartDistance = Mathf.Max(distance, telegraphRange);
        telegraphTimer = telegraphDuration;
        dashedThisCharge = false;
        plannedChargeDistance = GetPlannedChargeDistance(distance);
        displayedTelegraphTravelDistance = GetTelegraphDisplayTravelDistance();
        chargeLaneHalfWidth = GetChargeLaneHalfWidth();
        UpdateChargeTelegraphPreview();
        if (timingScript != null)
        {
            timingScript.HideImmediate();
            timingScript.ResetTimingWindow();
        }
        if (logStateTransitions)
            Debug.Log($"Bull -> Telegraphing. Distance: {distance:F2}");
    }

    private void UpdateTelegraph()
    {
        telegraphTimer = Mathf.Max(0f, telegraphTimer - Time.deltaTime);
        FacePlayer();
        plannedChargeDistance = GetPlannedChargeDistance(HorizontalDistanceToPlayer());
        displayedTelegraphTravelDistance = GetTelegraphDisplayTravelDistance();
        chargeLaneHalfWidth = GetChargeLaneHalfWidth();
        UpdateChargeTelegraphPreview();

        if (telegraphTimer <= 0f)
        {
            bool tutorialDashCharge = tutorialControlActive && tutorialChargeSequenceActive && !tutorialChargeUsesTiming;
            StartCharge(tutorialDashCharge, "TelegraphComplete");
        }
    }

    private void UpdateCharge()
    {
        MoveCharge(GetChargeSpeed());
        if (!dashedThisCharge && playerStats != null && playerStats.LastDashTime >= chargeStartedAt)
            dashedThisCharge = true;

        bool usesExternallyDrivenTutorialTiming = tutorialControlActive && tutorialChargeSequenceActive && tutorialChargeUsesTiming;
        if (!usesExternallyDrivenTutorialTiming &&
            !chargeHasEligibleTarget &&
            !chargeResultAllowsDamage &&
            IsPlayerInsideActiveChargeLane())
            BeginChargeTiming();

        if (timingActive)
        {
            UpdateChargeTiming();
            TryApplyChargePreImpactHold();
        }

        Vector3 currentChargePosition = GetCurrentBullPosition();
        float chargeTravelDistance = HorizontalDistance(chargeStartPosition, currentChargePosition);

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f || chargeTravelDistance >= plannedChargeDistance)
        {
            if (TryResolveTerminalChargeDamage())
                return;

            EnterFatigue(true);
        }
    }

    private void UpdateImpact()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer > 0f)
            return;

        EnterFatigue(impactCirclesAfterCharge);
    }

    private void UpdateFatigue()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer > 0f) return;
        if (tutorialControlActive && tutorialChargeSequenceActive)
        {
            tutorialChargeSequenceActive = false;
            tutorialChargeSequenceComplete = true;
            pendingCircleReset = false;
            currentState = BullState.Idle;
            return;
        }

        if (pendingCircleReset) BeginCirclingReset(); else currentState = BullState.Roaming;
    }

    private void UpdateHurt(float distance)
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer > 0f) return;
        if (attackRecoveryTimer > 0f) { currentState = BullState.Roaming; return; }
        if (HasAutoAttackRequest()) { if (distance <= GetAutoAttackTelegraphRange()) BeginTelegraph(distance); else BeginEngaging(); }
        else if (ShouldTriggerAttack(distance)) BeginEngaging();
        else currentState = BullState.Roaming;
    }

    private void UpdatePerfectReaction()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer > 0f)
            return;

        if (perfectReactionPhase < 1)
        {
            perfectReactionPhase++;
            PlayPerfectReactionPhase();
            return;
        }

        EnterFatigue(false);
    }

    private void BeginCirclingReset()
    {
        pendingCircleReset = false; currentState = BullState.CirclingReset; stateTimer = postChargeCircleDuration; circlingDirection = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        Vector3 currentPosition = GetCurrentBullPosition();
        circlingCenter = player != null ? player.position : currentPosition;
        circlingCenter.y = currentPosition.y;
        Vector3 offset = currentPosition - circlingCenter; offset.y = 0f;
        if (offset.sqrMagnitude <= 0.001f) offset = transform.right.sqrMagnitude > 0.001f ? transform.right : Vector3.right;
        circlingAngle = Mathf.Atan2(offset.z, offset.x);
        ResetChargeQteState(); hasRoamTarget = false; HideChargeTelegraph();
    }

    private void UpdateCirclingReset(float distance)
    {
        if (player != null) { circlingCenter = player.position; circlingCenter.y = GetCurrentBullPosition().y; }
        float angularSpeed = Mathf.PI / Mathf.Max(0.1f, postChargeCircleDuration);
        circlingAngle += circlingDirection * angularSpeed * Time.deltaTime;
        Vector3 desired = circlingCenter + new Vector3(Mathf.Cos(circlingAngle), 0f, Mathf.Sin(circlingAngle)) * postChargeCircleRadius;
        desired = ClampWithinArena(desired, arenaBoundaryPadding + 0.15f);
        FaceTowards(desired);
        MoveHorizontallyTowards(desired, roamingSpeed * 1.4f);
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            currentState = BullState.Roaming;
            attackRecoveryTimer = Mathf.Max(attackRecoveryTimer, distance >= safeReengageDistance ? 0.6f : 0.15f);
            hasRoamTarget = false;
        }
    }

    private bool ShouldTriggerAttack(float distance) => distance <= detectRange && ((playerStats.isHoldingCloth && distance <= tauntTriggerDistance) || distance <= dangerTriggerDistance);
    private bool HasAutoAttackRequest() => enableAutoAttack && autoAttackPending;
    private bool HasActiveAutoAttack() => enableAutoAttack && (autoAttackPending || autoAttackCommitted);
    private float GetAutoAttackTelegraphRange()
    {
        float configuredRange = Mathf.Clamp(Mathf.Max(telegraphRange, autoAttackTelegraphRange), telegraphRange, detectRange);
        float qteAlignedRange = Mathf.Clamp(GetQteAlignedAutoAttackTelegraphRange(), telegraphRange, detectRange);
        return Mathf.Min(configuredRange, qteAlignedRange);
    }
    private bool CanBeginTelegraph(float distance) => ShouldTriggerAttack(distance) && IsVisibleInPlayerView() && (playerStats.isHoldingCloth ? distance <= tauntTriggerDistance : distance <= telegraphRange);
    private float GetEngageApproachSpeed() => engageApproachSpeed * bullStats.GetApproachSpeedMultiplier() * (1f + (1f - bullStats.HealthNormalized) * 0.22f);
    private float GetTelegraphApproachSpeed() => telegraphApproachSpeed * bullStats.GetApproachSpeedMultiplier() * (1f + (1f - bullStats.HealthNormalized) * 0.28f);
    private float GetChargeSpeed() => chargeSpeed * bullStats.GetChargeSpeedMultiplier() * (1f + (1f - bullStats.HealthNormalized) * 0.2f);
    private float GetChargeTimingDuration(float travelDistance)
    {
        float baseDuration = 1.55f * bullStats.GetChargeDelayMultiplier();
        float travelDuration = travelDistance / Mathf.Max(0.01f, GetChargeSpeed());
        return Mathf.Max(baseDuration, travelDuration + 0.35f);
    }
    private float GetPlannedChargeDistance(float distanceToPlayer) => Mathf.Clamp(distanceToPlayer, minimumChargeTravelDistance, maxChargeDistance);

    // Keep auto-attack telegraphs close enough that the charge QTE color ramp still matches the bull's actual reach.
    private float GetQteAlignedAutoAttackTelegraphRange()
    {
        float legacyRevealDistance = timingScript != null ? timingScript.LegacyChargeRevealDistance : 2.2f;
        return GetChargeHitboxReach() + legacyRevealDistance + 0.25f;
    }

    private float GetChargeHitboxReach()
    {
        BoxCollider hitboxCollider = chargeHitbox != null ? chargeHitbox.GetComponent<BoxCollider>() : null;
        if (hitboxCollider != null)
            return Mathf.Max(hitDistance, hitboxCollider.center.z + (hitboxCollider.size.z * 0.5f));

        if (bullCollider is BoxCollider bodyBox && bodyBox.transform == transform)
            return Mathf.Max(hitDistance, bodyBox.center.z + bodyBox.size.z + (hitDistance * 0.6f));

        return Mathf.Max(hitDistance, 1.8f);
    }

    private float GetChargeLaneHalfWidth()
    {
        BoxCollider hitboxCollider = chargeHitbox != null ? chargeHitbox.GetComponent<BoxCollider>() : null;
        if (hitboxCollider != null)
            return Mathf.Max(0.45f, hitboxCollider.size.x * 0.5f) + telegraphLanePadding;

        return GetBullBodyHalfWidth() + telegraphLanePadding;
    }

    private float GetTelegraphDisplayTravelDistance()
    {
        float telegraphProgress = telegraphDuration <= 0.01f ? 1f : 1f - Mathf.Clamp01(telegraphTimer / telegraphDuration);
        float startTravel = Mathf.Clamp(Mathf.Min(0.55f, plannedChargeDistance), 0.25f, plannedChargeDistance);
        return Mathf.Lerp(startTravel, plannedChargeDistance, telegraphProgress);
    }

    private float GetChargeThreatDistance(float travelDistance)
    {
        return Mathf.Max(0.5f, travelDistance + hitDistance + telegraphLanePadding);
    }

    private float GetCurrentTelegraphLength()
    {
        return GetChargeThreatDistance(displayedTelegraphTravelDistance);
    }

    private Vector3 GetChargeLaneStart(Vector3 laneDirection)
    {
        return GetCurrentBullPosition() + (laneDirection * GetBullBodyFrontFaceDistance());
    }

    private void UpdateChargeTelegraphPreview()
    {
        if (currentState != BullState.Telegraphing)
        {
            HideChargeTelegraph();
            return;
        }

        EnsureChargeTelegraph();
        if (chargeTelegraphView == null)
            return;

        Vector3 forward = GetHorizontalDirectionToPlayer();
        if (forward.sqrMagnitude <= 0.0001f)
            forward = transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Vector3.forward;

        forward.y = 0f;
        forward.Normalize();
        Vector3 right = new Vector3(forward.z, 0f, -forward.x).normalized;
        Vector3 origin = GetChargeLaneStart(forward) + Vector3.up * telegraphYOffset;
        Vector3 front = origin + (forward * GetCurrentTelegraphLength());
        Vector3 leftOffset = right * chargeLaneHalfWidth;
        Vector3 backLeft = origin - leftOffset;
        Vector3 frontLeft = front - leftOffset;
        Vector3 frontRight = front + leftOffset;
        Vector3 backRight = origin + leftOffset;
        chargeTelegraphView.ShowPreview(backLeft, frontLeft, frontRight, backRight, telegraphColor, telegraphFillColor);
    }

    private void UpdateAutoAttackTimer()
    {
        if (!enableAutoAttack || HasActiveAutoAttack() || player == null) return;
        autoAttackTimer = Mathf.Max(0f, autoAttackTimer - Time.deltaTime);
        if (autoAttackTimer > 0f) return;
        autoAttackPending = true;
    }

    private void BeginAutoAttackApproach()
    {
        if (!autoAttackPending) return;
        autoAttackPending = false;
        autoAttackCommitted = true;
    }

    private void ConsumeAutoAttack()
    {
        bool shouldSchedule = enableAutoAttack && (autoAttackPending || autoAttackCommitted);
        ClearAutoAttackState();
        if (shouldSchedule) ScheduleNextAutoAttack();
    }

    private void ClearAutoAttackState()
    {
        autoAttackPending = false;
        autoAttackCommitted = false;
    }

    private void CancelAutoAttackAndReschedule()
    {
        bool shouldReschedule = enableAutoAttack && (autoAttackPending || autoAttackCommitted || autoAttackTimer <= 0f);
        ClearAutoAttackState();
        if (shouldReschedule) ScheduleNextAutoAttack();
    }

    private void ScheduleNextAutoAttack()
    {
        autoAttackTimer = Mathf.Max(0.1f, autoAttackInterval);
    }

    private void HandleChargeTimingResult(string result)
    {
        if (!timingActive || currentState != BullState.Charging) return;

        timingActive = false;
        chargeQteResolved = true;
        SetDashSuppressed(false);
        if (tutorialControlActive && tutorialChargeSequenceActive)
            tutorialChargeResult = result;

        switch (result)
        {
            case "Perfect!":
            case "Good":
                if (playerStats == null || !playerStats.TryDoCapa())
                {
                    if (tutorialControlActive && tutorialChargeSequenceActive)
                        tutorialChargeResult = "Miss";
                    BeginChargeMissCommit();
                    break;
                }

                canDamagePlayerThisCharge = false;
                chargeResultAllowsDamage = false;
                if (timingScript != null)
                    timingScript.HideRingKeepFeedback();
                playerStats.GrantInvulnerability(successfulDodgeInvulnerability);
                if (result == "Perfect!")
                    playerStats.RewardPerfectDodge();
                if (result == "Perfect!" && gameFlow != null && gameFlow.currentPhase == BullfightGameFlow.GamePhase.PhaseOne)
                    EnterPerfectReaction();
                else
                    EnterFatigue(false);
                break;
            case "Miss":
                if (tutorialControlActive && tutorialChargeSequenceActive)
                    tutorialChargeResult = "Miss";
                BeginChargeMissCommit();
                break;
        }
    }

    private void HandlePlayerAttack(float distance)
    {
        bool attackPressed = playerController != null ? playerController.ConsumeAttackPressed() : Input.GetKeyDown(KeyCode.F);
        if (!attackPressed || distance > banderillasRange) return;
        if (!playerStats.TryStartBanderillas()) return;
        Debug.Log("Banderillas throw queued.");
    }

    public void RegisterBanderillasHit(float damage)
    {
        if (bullStats == null || currentState == BullState.Dead)
            return;

        OnBanderillasHit?.Invoke(damage);
        bullStats.TakeDamage(damage);
        if (bullStats.currentHealth <= 0f)
            EnterDeathState();
        else
            StartHurtFlinch();

        Debug.Log("Banderillas hit the bull.");
    }

    private void StartHurtFlinch()
    {
        CancelAutoAttackAndReschedule();
        currentState = BullState.Hurt; ResetChargeQteState(); pendingCircleReset = false; hasRoamTarget = false; stateTimer = hurtFlinchDuration; attackRecoveryTimer = Mathf.Max(attackRecoveryTimer, 0.45f); HideChargeTelegraph();
        if (timingScript != null) timingScript.HideRingKeepFeedback();
    }

    private void EnterPerfectReaction()
    {
        StopBullMotionImmediate();
        ClearAutoAttackState();
        currentState = BullState.PerfectReaction;
        ResetChargeQteState();
        pendingCircleReset = false;
        hasRoamTarget = false;
        HideChargeTelegraph();
        if (timingScript != null)
            timingScript.HideRingKeepFeedback();

        perfectReactionPhase = 0;
        PlayPerfectReactionPhase();
    }

    private void PlayPerfectReactionPhase()
    {
        float phaseFrames = perfectReactionPhase == 0 ? PerfectReactionStartFrames : PerfectReactionAirFrames;
        string clipName = perfectReactionPhase == 0 ? AnimationPerfectReactionStart : AnimationPerfectReactionAir;
        float totalDuration = Mathf.Max(0.1f, phaseOnePerfectReactionDuration);
        stateTimer = Mathf.Max(0.08f, totalDuration * (phaseFrames / PerfectReactionTotalFrames));
        PlayAnimationClip(clipName, 0.04f, true);
    }

    private void EnterFatigue(bool circleAfterCharge)
    {
        CancelAutoAttackAndReschedule();
        currentState = BullState.Fatigued; ResetChargeQteState(); pendingCircleReset = circleAfterCharge; hasRoamTarget = false; stateTimer = tutorialControlActive && tutorialChargeSequenceActive ? 0.45f : UnityEngine.Random.Range(fatigueDurationRange.x, fatigueDurationRange.y); attackRecoveryTimer = tutorialControlActive && tutorialChargeSequenceActive ? 0.3f : attackRecoveryDuration; HideChargeTelegraph();
        if (timingScript != null) timingScript.HideRingKeepFeedback();
    }

    private void EnterImpact(bool circleAfterCharge)
    {
        currentState = BullState.Impact;
        impactCirclesAfterCharge = circleAfterCharge;
        ResetChargeQteState();
        stateTimer = impactDuration;
        HideChargeTelegraph();
        if (timingScript != null)
            timingScript.HideRingKeepFeedback();
    }

    private void StartCharge(bool damagingCharge, string reason)
    {
        currentState = BullState.Charging;
        chargeDuration = GetChargeTimingDuration(plannedChargeDistance);
        stateTimer = chargeDuration;
        ResetChargeQteState();
        canDamagePlayerThisCharge = damagingCharge;
        chargeResultAllowsDamage = damagingCharge;
        chargeStartPosition = GetCurrentBullPosition();
        chargeStartedAt = Time.time;
        chargeDirection = GetHorizontalDirectionToPlayer();
        if (chargeDirection.sqrMagnitude <= 0.0001f) chargeDirection = transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Vector3.forward;
        chargeDirection.y = 0f;
        chargeDirection.Normalize();
        plannedChargeDistance = GetPlannedChargeDistance(HorizontalDistanceToPlayer());
        displayedTelegraphTravelDistance = plannedChargeDistance;
        chargeLaneHalfWidth = GetChargeLaneHalfWidth();
        isPlayerInsideChargeLane = IsPlayerInsideActiveChargeLane();
        dashedThisCharge = false;
        HideChargeTelegraph();
        if (chargeDirection.sqrMagnitude > 0.0001f) QueueMoveRotation(Quaternion.LookRotation(chargeDirection, Vector3.up));
        if (timingScript != null)
        {
            timingScript.HideImmediate();
            timingScript.ResetTimingWindow();
            if (gameFlow != null &&
                (gameFlow.currentPhase == BullfightGameFlow.GamePhase.PhaseZeroTutorial ||
                 gameFlow.currentPhase == BullfightGameFlow.GamePhase.PhaseOne))
            {
                timingScript.SetTimingWindow(0.5f, 0.7f);
            }
        }

        bool usesExternallyDrivenTutorialTiming = tutorialControlActive && tutorialChargeSequenceActive && tutorialChargeUsesTiming;
        if (usesExternallyDrivenTutorialTiming)
            BeginChargeTiming(false, false, true);
        else if (!damagingCharge && IsPlayerInsideActiveChargeLane())
            BeginChargeTiming();

        if (logStateTransitions)
            Debug.Log($"Bull -> Charging ({reason}). Distance: {HorizontalDistanceToPlayer():F2}");
    }

    private void BeginChargeMissCommit()
    {
        timingActive = false;
        chargeQteResolved = true;
        chargeResultAllowsDamage = true;
        canDamagePlayerThisCharge = true;
        SetDashSuppressed(false);
        if (timingScript != null)
            timingScript.HideRingKeepFeedback();

        float remainingCommitDistance = Mathf.Max(
            Mathf.Max(MissCommitDistance, hitDistance + chargeImpactStopBuffer),
            GetChargeThreatDistance(plannedChargeDistance) * 0.4f);
        float chargeTravelDistance = HorizontalDistance(chargeStartPosition, GetCurrentBullPosition());
        plannedChargeDistance = Mathf.Max(plannedChargeDistance, chargeTravelDistance + remainingCommitDistance);
        stateTimer = Mathf.Max(stateTimer, remainingCommitDistance / Mathf.Max(0.01f, GetChargeSpeed()));

        if (ShouldLockPlayerDuringMissCommit())
            SetChargeMissPlayerLock(true);
    }

    private void ResetChargeQteState()
    {
        SetChargeMissPlayerLock(false);
        timingActive = false;
        canDamagePlayerThisCharge = false;
        chargeHasEligibleTarget = false;
        chargeQteResolved = false;
        chargeResultAllowsDamage = false;
        externalChargeTimingControl = false;
        chargeQteTimer = 0f;
        chargeQteDuration = 0f;
        SetDashSuppressed(false);
    }

    private void SetDashSuppressed(bool suppressed)
    {
        if (playerController != null)
            playerController.SetDashSuppressed(suppressed);
    }

    private float GetTelegraphProgress(float distance)
    {
        float denominator = Mathf.Max(0.01f, telegraphStartDistance - telegraphMissDistance);
        return Mathf.Clamp01((telegraphStartDistance - distance) / denominator);
    }

    private void MoveHorizontallyTowards(Vector3 targetPosition, float speed)
    {
        Vector3 current = GetCurrentBullPosition();
        Vector3 targetFlat = new Vector3(targetPosition.x, current.y, targetPosition.z);
        Vector3 next = Vector3.MoveTowards(current, targetFlat, speed * Time.deltaTime);
        next.y = current.y;
        QueueMovePosition(next);
    }

    private void MoveCharge(float speed)
    {
        MoveCharge(speed, Time.deltaTime);
    }

    private void MoveCharge(float speed, float deltaTime)
    {
        Vector3 current = GetCurrentBullPosition();
        Vector3 next = current + (chargeDirection * speed * Mathf.Max(0f, deltaTime));
        next.y = current.y;
        QueueMovePosition(next);
    }

    private void FacePlayer() { if (player != null) FaceTowards(player.position); }

    private void FaceTowards(Vector3 targetPosition)
    {
        Vector3 current = GetCurrentBullPosition();
        Vector3 direction = new Vector3(targetPosition.x - current.x, 0f, targetPosition.z - current.z);
        if (direction.sqrMagnitude <= 0.0001f) return;
        Quaternion lookRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        QueueMoveRotation(Quaternion.Slerp(GetCurrentBullRotation(), lookRotation, Time.deltaTime * turnSpeed));
    }

    private float HorizontalDistanceToPlayer() => player == null ? 999f : HorizontalDistance(GetCurrentBullPosition(), player.position);
    private Vector3 GetHorizontalDirectionToPlayer() { if (player == null) return transform.forward; Vector3 direction = player.position - GetCurrentBullPosition(); direction.y = 0f; return direction.normalized; }

    private bool IsVisibleInPlayerView()
    {
        if (player == null) return false;
        Transform reference = Camera.main != null ? Camera.main.transform : player;
        Vector3 forward = reference.forward; forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f) forward = player.forward;
        forward.Normalize();
        Vector3 toBull = GetCurrentBullPosition() - reference.position; toBull.y = 0f;
        if (toBull.sqrMagnitude <= 0.0001f) return true;
        toBull.Normalize();
        return Vector3.Dot(forward, toBull) >= telegraphVisibleDot;
    }

    private Vector3 PickRoamTarget()
    {
        roamCenter = GetArenaCenter();
        float radius = Mathf.Min(roamingRadius, Mathf.Max(1f, GetArenaRadius() - 1.2f));
        Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * radius;
        Vector3 candidate = roamCenter + new Vector3(randomCircle.x, 0f, randomCircle.y);
        return ClampWithinArena(new Vector3(candidate.x, GetCurrentBullPosition().y, candidate.z), arenaBoundaryPadding + 0.15f);
    }

    private void KeepInsideArena()
    {
        if (currentState == BullState.Dead) return;
        Vector3 current = GetCurrentBullPosition();
        Vector3 corrected = ClampWithinArena(current, arenaBoundaryPadding);
        if (HorizontalDistance(current, corrected) <= 0.001f) return;
        corrected.y = current.y;
        QueueMovePosition(corrected);
        switch (currentState)
        {
            case BullState.Charging: EnterFatigue(true); break;
            case BullState.Roaming: hasRoamTarget = false; stateTimer = Mathf.Min(stateTimer, 0.15f); break;
            case BullState.Engaging:
            case BullState.Telegraphing:
                ResetChargeQteState();
                CancelAutoAttackAndReschedule();
                if (timingScript != null) timingScript.HideImmediate();
                HideChargeTelegraph();
                currentState = BullState.Roaming;
                attackRecoveryTimer = Mathf.Max(attackRecoveryTimer, 0.75f);
                break;
        }
    }

    private Vector3 ClampWithinArena(Vector3 position, float padding)
    {
        Vector3 center = GetArenaCenter();
        float radius = Mathf.Max(1f, GetArenaRadius() - padding);
        Vector3 offset = new Vector3(position.x - center.x, 0f, position.z - center.z);
        if (offset.sqrMagnitude > radius * radius) offset = offset.normalized * radius;
        return new Vector3(center.x + offset.x, position.y, center.z + offset.z);
    }

    private Vector3 GetArenaCenter() => spawnManager != null ? spawnManager.ArenaCenter : (roamCenter == Vector3.zero ? GetCurrentBullPosition() : roamCenter);
    private float GetArenaRadius() => spawnManager != null ? spawnManager.ArenaRadius : roamingRadius + 3.5f;

    private static float HorizontalDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    private bool HasQueuedHorizontalMovementIntent()
    {
        return hasQueuedMovePosition && HorizontalDistance(GetCurrentBullPosition(), queuedMovePosition) > 0.01f;
    }

    private void UpdateChargeLaneOccupancy()
    {
        if ((currentState != BullState.Telegraphing && currentState != BullState.Charging) || !TryGetPlayerCollider(out Collider activePlayerCollider))
        {
            isPlayerInsideChargeLane = false;
            return;
        }

        Vector3 laneDirection = currentState == BullState.Charging ? chargeDirection : GetHorizontalDirectionToPlayer();
        if (laneDirection.sqrMagnitude <= 0.0001f)
            laneDirection = transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Vector3.forward;

        laneDirection.y = 0f;
        laneDirection.Normalize();
        float laneDistance = currentState == BullState.Charging ? GetChargeThreatDistance(plannedChargeDistance) : GetCurrentTelegraphLength();
        float laneHalfWidth = currentState == BullState.Charging ? chargeLaneHalfWidth : GetChargeLaneHalfWidth();
        Vector3 overlapCenter = GetChargeLaneStart(laneDirection) + (laneDirection * (laneDistance * 0.5f)) + Vector3.up * (laneCheckHeight * 0.5f);
        Vector3 halfExtents = new Vector3(laneHalfWidth, Mathf.Max(0.5f, laneCheckHeight * 0.5f), Mathf.Max(0.5f, laneDistance * 0.5f));
        Quaternion laneRotation = Quaternion.LookRotation(laneDirection, Vector3.up);

        int overlapCount = Physics.OverlapBoxNonAlloc(overlapCenter, halfExtents, chargeLaneOverlapResults, laneRotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        isPlayerInsideChargeLane = false;
        for (int i = 0; i < overlapCount; i++)
        {
            Collider candidate = chargeLaneOverlapResults[i];
            if (candidate == null)
                continue;

            if (!IsPlayerCollider(candidate))
                continue;

            isPlayerInsideChargeLane = true;
            break;
        }

        if (!isPlayerInsideChargeLane)
            isPlayerInsideChargeLane = IsPlayerInsideChargeLaneGeometry(activePlayerCollider, laneDirection, laneDistance, laneHalfWidth);
    }

    private void EnsureChargeTelegraph()
    {
        chargeTelegraphView ??= new BullAIChargeTelegraphView(transform);
    }

    private void HideChargeTelegraph()
    {
        chargeTelegraphView?.Hide();
    }

    private void SnapToGround(bool forceSnap)
    {
        Vector3 currentPosition = GetCurrentBullPosition(), origin = currentPosition + Vector3.up * groundProbeHeight;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, groundProbeHeight * 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return;
        float referenceGroundY = spawnManager != null ? spawnManager.ArenaCenter.y : currentPosition.y;
        float bestHeightDelta = float.MaxValue, closestDistance = float.MaxValue, targetY = currentPosition.y;
        bool foundGround = false;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null) continue;
            Transform hitTransform = hit.collider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform) || hit.normal.y < 0.2f) continue;
            float heightDelta = Mathf.Abs(hit.point.y - referenceGroundY);
            bool isBetterHeight = heightDelta < bestHeightDelta - 0.0001f;
            bool isTieButCloser = Mathf.Abs(heightDelta - bestHeightDelta) <= 0.0001f && hit.distance < closestDistance;
            if (isBetterHeight || isTieButCloser)
            {
                bestHeightDelta = heightDelta;
                closestDistance = hit.distance;
                targetY = hit.point.y + visualGroundOffset;
                foundGround = true;
            }
        }
        if (!foundGround) return;
        Vector3 position = currentPosition;
        position.y = forceSnap ? targetY : Mathf.Lerp(position.y, targetY, Time.deltaTime * groundSnapSpeed);
        QueueMovePosition(position);
    }

    public void PlayPhaseTwoStandoffIdle()
    {
        StopBullMotionImmediate();
        PlayAnimationClip(AnimationIdleThreat, 0.12f);
    }

    public void PlayPhaseTwoStanceConfirm()
    {
        StopBullMotionImmediate();
        PlayAnimationClip(AnimationIdleCalm, 0.1f);
    }

    public void PlayPhaseTwoTelegraph()
    {
        StopBullMotionImmediate();
        PlayAnimationClip(AnimationAttackForwardInPlace, 0.08f);
    }

    public void PlayPhaseTwoAttackFollowThrough()
    {
        StopBullMotionImmediate();
        PlayAnimationClip(AnimationRunForwardInPlace, 0.06f);
    }

    public void PlayPhaseTwoWalkLoop()
    {
        StopBullMotionImmediate();
        PlayAnimationClip(AnimationTrotForwardInPlace, 0.08f);
    }

    public void PlayPhaseTwoHitReaction(bool wasPerfect)
    {
        StopBullMotionImmediate();
        PlayAnimationClip(wasPerfect ? AnimationHitFront : AnimationHitMiddle, 0.06f);
    }

    public void PlayPhaseTwoRoundResetIdle()
    {
        StopBullMotionImmediate();
        PlayAnimationClip(AnimationIdleThreat, 0.1f);
    }

    public void SetPhaseTwoPose(Vector3 position, Quaternion rotation, bool snapToGround = true)
    {
        StopBullMotionImmediate();

        if (bullRigidbody != null)
        {
            bullRigidbody.position = position;
            bullRigidbody.rotation = rotation;
        }

        transform.SetPositionAndRotation(position, rotation);

        if (!snapToGround)
            return;

        QueueMovePosition(position);
        QueueMoveRotation(rotation);
        SnapToGround(true);
        CommitQueuedPoseImmediate();
    }

    public void ForceSnapToGround()
    {
        SnapToGround(true);
        CommitQueuedPoseImmediate();
    }

    public void SetTutorialControl(bool active)
    {
        tutorialControlActive = active;
        tutorialChargeDamageEnabled = false;
        tutorialChargeSequenceActive = false;
        tutorialChargeSequenceComplete = false;
        tutorialChargeHitPlayer = false;
        tutorialChargeUsesTiming = false;
        tutorialChargeResult = string.Empty;

        if (active)
            EnterTutorialIdle();
    }

    public void EnterTutorialIdle()
    {
        StopBullMotionImmediate();
        currentState = BullState.Idle;
        stateTimer = 0f;
        pendingCircleReset = false;
        hasRoamTarget = false;
        tutorialChargeSequenceActive = false;
        tutorialChargeSequenceComplete = false;
        tutorialChargeHitPlayer = false;
        tutorialChargeUsesTiming = false;
        tutorialChargeResult = string.Empty;
        ClearAutoAttackState();
        ResetChargeQteState();
        HideChargeTelegraph();
        if (timingScript != null)
        {
            timingScript.HideImmediate();
            timingScript.ResetTimingWindow();
        }
    }

    public void StartTutorialCharge(bool useTimingWindow, bool allowDamage)
    {
        tutorialControlActive = true;
        tutorialChargeUsesTiming = useTimingWindow;
        tutorialChargeDamageEnabled = allowDamage;
        tutorialChargeSequenceActive = true;
        tutorialChargeSequenceComplete = false;
        tutorialChargeHitPlayer = false;
        tutorialChargeResult = string.Empty;
        pendingCircleReset = false;
        hasRoamTarget = false;
        ClearAutoAttackState();
        ResetChargeQteState();
        BeginTelegraph(Mathf.Max(telegraphRange, HorizontalDistanceToPlayer()));
    }

    private void UpdateTutorialBehavior()
    {
        if (bullStats.currentHealth <= 0f && currentState != BullState.Dead)
            EnterDeathState();

        if (currentState == BullState.Dead)
            return;

        float distance = HorizontalDistanceToPlayer();
        if (tutorialChargeSequenceActive)
        {
            UpdateState(distance);
            return;
        }

        if (gameFlow != null && gameFlow.IsTutorialAttackStepActive)
        {
            FacePlayer();
            HandlePlayerAttack(distance);
        }

        currentState = BullState.Idle;
        HideChargeTelegraph();
    }

    private void UpdateAnimation()
    {
        if (currentState == BullState.PerfectReaction)
            return;

        bool hasMoveIntent =
            currentState == BullState.Roaming ||
            currentState == BullState.Engaging ||
            currentState == BullState.Telegraphing ||
            currentState == BullState.Charging ||
            currentState == BullState.CirclingReset;

        Vector3 currentPosePosition = GetImmediateBullPosition();
        float queuedHorizontalMotion = hasQueuedMovePosition
            ? HorizontalDistance(currentPosePosition, queuedMovePosition) / Mathf.Max(Time.deltaTime, 0.0001f)
            : 0f;
        float resolvedHorizontalMotion = Mathf.Max(currentHorizontalMotion, queuedHorizontalMotion);
        bool isActuallyMoving = resolvedHorizontalMotion > AnimationMotionThreshold;
        bool hasQueuedLocomotion = hasQueuedMovePosition && queuedHorizontalMotion > 0.01f;

        if (isActuallyMoving)
            locomotionBlendTimer = LocomotionHoldTime;
        else
            locomotionBlendTimer = Mathf.Max(0f, locomotionBlendTimer - Time.deltaTime);

        bool isMovingHorizontally = isActuallyMoving || (hasMoveIntent && locomotionBlendTimer > 0f);
        if (!isMovingHorizontally && hasQueuedLocomotion)
            isMovingHorizontally = true;

        animationView ??= new BullAIAnimationView(animator);
        animationView.PlayState(this, isMovingHorizontally);
    }

    private bool HasMissingReferences()
    {
        return player == null ||
               playerStats == null ||
               playerController == null ||
               bullStats == null ||
               timingScript == null ||
               gameFlow == null ||
               spawnManager == null;
    }

    private void ResolveReferencesIfNeeded()
    {
        if (playerController == null)
            playerController = BullfightSceneCache.FindObject<BullfightPlayerController>();

        Transform controllerTarget = playerController != null ? playerController.GetBullTarget() : null;
        if (controllerTarget != null)
        {
            player = controllerTarget;
        }
        else if (player == null)
        {
            player = BullfightSceneCache.FindTransformWithTag("Player");
            if (player == null)
                player = BullfightSceneCache.FindSceneObjectByName<Transform>("P_LPSP_FP_CH");
        }

        if (playerStats == null)
            playerStats = BullfightSceneCache.FindObject<PlayerStats>();

        if (bullStats == null)
            bullStats = GetComponent<BullStats>() ?? BullfightSceneCache.FindObject<BullStats>();

        if (timingScript == null)
            timingScript = BullfightSceneCache.FindObject<BullTimingRing>();

        if (spawnManager == null)
            spawnManager = BullfightSceneCache.FindObject<BullfightSpawnManager>();

        if (gameFlow == null)
            gameFlow = BullfightSceneCache.FindObject<BullfightGameFlow>();

        if (gameFlow == null)
        {
            GameObject gameFlowObject = new("BullfightGameFlow");
            gameFlow = gameFlowObject.AddComponent<BullfightGameFlow>();
        }

        if (gameFlow != linkedGameFlow && gameFlow != null)
        {
            if (gameFlow.playerStats == null)
                gameFlow.playerStats = playerStats;

            if (gameFlow.bullStats == null)
                gameFlow.bullStats = bullStats;

            if (gameFlow.bullAI == null)
                gameFlow.bullAI = this;

            linkedGameFlow = gameFlow;
        }
    }

    public void ResetCombatState()
    {
        currentState = BullState.Roaming; stateTimer = 0f; ResetChargeQteState(); pendingCircleReset = false; engageTimer = engageDelay; lastHitTime = -999f; chargeStartedAt = -999f; chargeStartPosition = GetCurrentBullPosition(); chargeDirection = transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Vector3.forward; animationView?.Reset(); roamCenter = GetArenaCenter(); hasRoamTarget = false; attackRecoveryTimer = 1.25f; plannedChargeDistance = 0f; displayedTelegraphTravelDistance = 0f; chargeLaneHalfWidth = 0f; dashedThisCharge = false; currentHorizontalMotion = 0f; phaseTwoChargeMotionActive = false; locomotionBlendTimer = 0f; ClearAutoAttackState(); ScheduleNextAutoAttack(); HideChargeTelegraph();
        tutorialChargeDamageEnabled = false;
        tutorialChargeSequenceActive = false;
        tutorialChargeSequenceComplete = false;
        tutorialChargeHitPlayer = false;
        tutorialChargeUsesTiming = false;
        tutorialChargeResult = string.Empty;
        if (timingScript != null) timingScript.HideImmediate();
        QueueMovePosition(ClampWithinArena(GetCurrentBullPosition(), arenaBoundaryPadding));
        SnapToGround(true);
        CommitQueuedPoseImmediate();
    }

    private void EnterDeathState()
    {
        currentState = BullState.Dead; ResetChargeQteState(); pendingCircleReset = false; hasRoamTarget = false; stateTimer = 0f; attackRecoveryTimer = 999f; phaseTwoChargeMotionActive = false; ClearAutoAttackState(); HideChargeTelegraph();
        if (timingScript != null) timingScript.HideImmediate();
        if (bullRigidbody != null) bullRigidbody.velocity = Vector3.zero;
    }

    public void ForceDeathState()
    {
        if (!enabled)
            enabled = true;

        if (currentState != BullState.Dead)
            EnterDeathState();

        UpdateAnimation();
    }

    private void StopBullMotionImmediate()
    {
        if (bullRigidbody == null)
            bullRigidbody = GetComponent<Rigidbody>();

        if (bullRigidbody == null)
            return;

        if (!bullRigidbody.isKinematic)
        {
            bullRigidbody.velocity = Vector3.zero;
            bullRigidbody.angularVelocity = Vector3.zero;
        }
    }

    private void PlayAnimationClip(string clipName, float crossFadeDuration, bool forceRestart = false)
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        animationView ??= new BullAIAnimationView(animator);
        animationView.Play(clipName, crossFadeDuration, forceRestart);
    }

    public void TryHitPlayerFromCharge(Collider other)
    {
        if (other == null || !IsPlayerCollider(other)) return;
        _ = TryApplyChargeDamageFromStrictContact();
    }

    private bool TryApplyChargeDamageFromStrictContact()
    {
        if (!CanAttemptChargeDamage() || !TryGetPlayerCollider(out Collider activePlayerCollider))
            return false;
        if (!IsChargeHitboxTouchingPlayer(activePlayerCollider, GetImmediateBullPosition(), GetImmediateBullRotation()))
            return false;

        ApplyChargeDamage();
        return true;
    }

    private bool TryResolveTerminalChargeDamage()
    {
        if (!CanAttemptChargeDamage() || !TryGetPlayerCollider(out Collider activePlayerCollider))
            return false;

        Vector3 startBullPosition = GetImmediateBullPosition();
        Quaternion startBullRotation = GetImmediateBullRotation();
        Vector3 endBullPosition = GetCurrentBullPosition();
        Quaternion endBullRotation = GetCurrentBullRotation();

        if (!DidChargeHitboxReachPlayer(activePlayerCollider, startBullPosition, startBullRotation, endBullPosition, endBullRotation))
            return false;

        ApplyChargeDamage();
        return true;
    }

    private void ApplyChargeDamage(bool forceDamage = false)
    {
        if (!forceDamage && !CanAttemptChargeDamage())
            return;

        lastHitTime = Time.time;
        canDamagePlayerThisCharge = false;
        if (TryGetPlayerCollider(out Collider activePlayerCollider))
            StopBullShortOfPlayer(activePlayerCollider, chargeImpactStopBuffer);

        if (tutorialControlActive && tutorialChargeSequenceActive)
        {
            tutorialChargeHitPlayer = true;
            if (!tutorialChargeDamageEnabled)
            {
                EnterImpact(true);
                return;
            }
        }

        playerStats.TakeBullImpact(collisionDamage, GetCurrentBullPosition());
        EnterImpact(true);
    }

    private void StopBullShortOfPlayer(Collider activePlayerCollider, float extraBuffer)
    {
        if (activePlayerCollider == null || bullCollider == null) return;
        Vector3 stopDirection = chargeDirection;
        stopDirection.y = 0f;
        if (stopDirection.sqrMagnitude <= 0.0001f)
            stopDirection = GetImmediateBullRotation() * Vector3.forward;
        stopDirection.y = 0f;
        if (stopDirection.sqrMagnitude <= 0.0001f) return;
        stopDirection.Normalize();

        Vector3 bullPosition = GetImmediateBullPosition();
        Vector3 playerSurfacePoint = activePlayerCollider.ClosestPoint(bullPosition);
        Vector3 targetBullPosition = playerSurfacePoint - (stopDirection * (GetBullBodyFrontFaceDistance() + extraBuffer));
        targetBullPosition.y = bullPosition.y;

        float forwardAdjustment = Vector3.Dot(targetBullPosition - bullPosition, stopDirection);
        if (forwardAdjustment <= 0f)
        {
            hasQueuedMovePosition = false;
            queuedMovePosition = bullPosition;
            StopBullMotionImmediate();
        }
        else
        {
            QueueMovePosition(targetBullPosition);
            CommitQueuedPoseImmediate();
        }

        ResolvePlayerOverlapFromBull(activePlayerCollider, 0.02f, false);
    }

    private void TryStopBullBeforeImpact()
    {
        if (!TryGetPlayerCollider(out Collider activePlayerCollider))
            return;

        StopBullShortOfPlayer(activePlayerCollider, chargeImpactStopBuffer);
    }

    private void MaintainBullPlayerBlocking()
    {
        if (bullCollider == null)
            return;

        if (!TryGetPlayerCollider(out Collider activePlayerCollider) || activePlayerCollider == null)
            return;

        if (currentState == BullState.Telegraphing)
            return;

        if (IsChargeTimingHoldActive())
        {
            TryApplyChargePreImpactHold(activePlayerCollider);
            return;
        }

        float pushDistance = currentState switch
        {
            BullState.Impact => 0.005f,
            BullState.Charging => 0.02f,
            _ => 0.04f
        };

        ResolvePlayerOverlapFromBull(activePlayerCollider, pushDistance, false);
    }

    private void ResolvePlayerOverlapFromBull(Collider activePlayerCollider, float extraBuffer, bool includeCameraClearance)
    {
        if (activePlayerCollider == null || bullCollider == null)
            return;

        Vector3 bullPosition = GetCurrentBullPosition();
        Quaternion bullRotation = GetCurrentBullRotation();
        Vector3 playerPosition = GetColliderWorldPosition(activePlayerCollider);
        Quaternion playerRotation = GetColliderWorldRotation(activePlayerCollider);

        if (!Physics.ComputePenetration(bullCollider, bullPosition, bullRotation, activePlayerCollider, playerPosition, playerRotation, out Vector3 separationDirection, out float separationDistance))
        {
            if (!includeCameraClearance || playerStats == null || !playerStats.TryGetFirstPersonCameraPoint(out Vector3 cameraPoint))
                return;

            Vector3 closestPoint = bullCollider.ClosestPoint(cameraPoint);
            Vector3 cameraDelta = cameraPoint - closestPoint;
            cameraDelta.y = 0f;
            float requiredClearance = cameraClearanceBuffer + extraBuffer;
            bool cameraInsideBull = bullCollider.bounds.Contains(cameraPoint) || closestPoint == cameraPoint;
            if (!cameraInsideBull && cameraDelta.magnitude >= requiredClearance)
                return;

            Vector3 fallbackDirection = GetPushPlayerAwayDirection(cameraPoint - bullPosition);
            float fallbackDistance = cameraInsideBull
                ? Mathf.Max(requiredClearance, GetPlayerContactRadius(activePlayerCollider) * 0.6f)
                : Mathf.Max(0f, requiredClearance - cameraDelta.magnitude);

            PushPlayerCollider(activePlayerCollider, fallbackDirection * fallbackDistance);
            return;
        }

        Vector3 playerPushDirection = -separationDirection;
        playerPushDirection.y = 0f;
        playerPushDirection = GetPushPlayerAwayDirection(playerPushDirection);

        float pushDistance = separationDistance + extraBuffer;
        if (includeCameraClearance && playerStats != null && playerStats.TryGetFirstPersonCameraPoint(out Vector3 overlapCameraPoint))
        {
            Vector3 closestPoint = bullCollider.ClosestPoint(overlapCameraPoint);
            Vector3 cameraDelta = overlapCameraPoint - closestPoint;
            cameraDelta.y = 0f;
            float requiredClearance = cameraClearanceBuffer + 0.02f;
            bool cameraInsideBull = bullCollider.bounds.Contains(overlapCameraPoint) || closestPoint == overlapCameraPoint;
            if (cameraInsideBull)
                pushDistance += requiredClearance;
            else if (cameraDelta.magnitude < requiredClearance)
                pushDistance += requiredClearance - cameraDelta.magnitude;
        }

        PushPlayerCollider(activePlayerCollider, playerPushDirection * pushDistance);
    }

    private Vector3 GetPushPlayerAwayDirection(Vector3 preferredDirection)
    {
        preferredDirection.y = 0f;
        if (preferredDirection.sqrMagnitude > 0.0001f)
            return preferredDirection.normalized;

        Vector3 fallback = player != null ? player.position - GetCurrentBullPosition() : Vector3.zero;
        fallback.y = 0f;
        if (fallback.sqrMagnitude > 0.0001f)
            return fallback.normalized;

        fallback = -(GetCurrentBullRotation() * Vector3.forward);
        fallback.y = 0f;
        if (fallback.sqrMagnitude > 0.0001f)
            return fallback.normalized;

        return Vector3.back;
    }

    private static void PushPlayerCollider(Collider activePlayerCollider, Vector3 delta)
    {
        if (activePlayerCollider == null || delta.sqrMagnitude <= 0.000001f)
            return;

        Rigidbody attachedBody = activePlayerCollider.attachedRigidbody;
        if (attachedBody != null && !attachedBody.isKinematic)
            attachedBody.MovePosition(attachedBody.position + delta);
        else
            activePlayerCollider.transform.position += delta;
    }

    private bool CanAttemptChargeDamage()
    {
        if (!canDamagePlayerThisCharge || currentState != BullState.Charging || playerStats == null)
            return false;

        return Time.time - lastHitTime >= hitCooldown;
    }

    private void BeginChargeTiming()
    {
        BeginChargeTiming(true, true, false);
    }

    private void BeginChargeTiming(bool showRing, bool armInput, bool externalControl)
    {
        if (currentState != BullState.Charging || timingActive || chargeHasEligibleTarget || chargeResultAllowsDamage)
            return;

        chargeHasEligibleTarget = true;
        chargeQteResolved = false;
        timingActive = true;
        externalChargeTimingControl = externalControl;
        canDamagePlayerThisCharge = false;
        chargeResultAllowsDamage = false;
        chargeQteDuration = externalControl ? 0f : Mathf.Max(0.45f, qteRevealRemainingTime);
        chargeQteTimer = chargeQteDuration;
        SetDashSuppressed(true);

        if (timingScript != null)
        {
            timingScript.HideImmediate();
            timingScript.ResetTimingWindow();
            if (gameFlow != null &&
                (gameFlow.currentPhase == BullfightGameFlow.GamePhase.PhaseZeroTutorial ||
                 gameFlow.currentPhase == BullfightGameFlow.GamePhase.PhaseOne))
            {
                timingScript.SetTimingWindow(0.5f, 0.7f);
            }

            if (!externalControl)
            {
                timingScript.StartTiming(BullTimingRing.TimingMode.Capa, HandleChargeTimingResult, showRing, armInput);
                timingScript.SetTelegraphProgress(0f);
            }
        }
    }

    private void UpdateChargeTiming()
    {
        if (!timingActive)
            return;

        if (externalChargeTimingControl)
            return;

        chargeQteTimer = Mathf.Max(0f, chargeQteTimer - Time.deltaTime);
        float progress = chargeQteDuration <= 0.0001f ? 1f : 1f - (chargeQteTimer / chargeQteDuration);
        if (timingScript != null)
            timingScript.SetTelegraphProgress(progress);

        if (chargeQteTimer > 0f)
            return;

        if (timingScript != null && timingScript.IsActive)
            timingScript.ResolveExternal("Miss");
        else
            HandleChargeTimingResult("Miss");
    }

    private bool IsChargeTimingHoldActive()
    {
        return currentState == BullState.Charging && timingActive && chargeHasEligibleTarget && !chargeQteResolved && !chargeResultAllowsDamage;
    }

    public void ResolveExternalChargeTimingResult(string result)
    {
        if (!timingActive || currentState != BullState.Charging)
            return;

        HandleChargeTimingResult(result);
    }

    private bool TryApplyChargePreImpactHold()
    {
        return TryGetPlayerCollider(out Collider activePlayerCollider) && TryApplyChargePreImpactHold(activePlayerCollider);
    }

    private bool TryApplyChargePreImpactHold(Collider activePlayerCollider)
    {
        if (!IsChargeTimingHoldActive() || activePlayerCollider == null || bullCollider == null)
            return false;

        if (!TryGetChargePreImpactHoldPosition(activePlayerCollider, out Vector3 holdPosition, out Vector3 holdDirection))
            return false;

        Vector3 bullPosition = GetCurrentBullPosition();
        if (!HasReachedHoldLine(bullPosition, holdPosition, holdDirection) && !IsBullOverlappingPlayer(activePlayerCollider))
            return false;

        QueueMovePosition(holdPosition);
        return true;
    }

    private bool TryGetChargePreImpactHoldPosition(Collider activePlayerCollider, out Vector3 holdPosition, out Vector3 holdDirection)
    {
        Vector3 bullPosition = GetCurrentBullPosition();
        holdDirection = chargeDirection;
        if (holdDirection.sqrMagnitude <= 0.0001f)
            holdDirection = GetCurrentBullRotation() * Vector3.forward;
        holdDirection.y = 0f;
        if (holdDirection.sqrMagnitude <= 0.0001f)
        {
            holdPosition = bullPosition;
            return false;
        }

        holdDirection.Normalize();
        Vector3 probeOrigin = bullPosition + (holdDirection * GetBullBodyFrontFaceDistance());
        Vector3 playerSurfacePoint = activePlayerCollider.ClosestPoint(probeOrigin);
        holdPosition = playerSurfacePoint - (holdDirection * (GetBullBodyFrontFaceDistance() + ChargePreImpactHoldBuffer));
        holdPosition.y = bullPosition.y;
        return true;
    }

    private static bool HasReachedHoldLine(Vector3 bullPosition, Vector3 holdPosition, Vector3 holdDirection)
    {
        Vector3 bullFlat = new Vector3(bullPosition.x, 0f, bullPosition.z);
        Vector3 holdFlat = new Vector3(holdPosition.x, 0f, holdPosition.z);
        return Vector3.Dot(bullFlat - holdFlat, holdDirection) >= 0f;
    }

    private bool IsBullOverlappingPlayer(Collider activePlayerCollider)
    {
        if (activePlayerCollider == null || bullCollider == null)
            return false;

        return Physics.ComputePenetration(
            bullCollider,
            GetCurrentBullPosition(),
            GetCurrentBullRotation(),
            activePlayerCollider,
            GetColliderWorldPosition(activePlayerCollider),
            GetColliderWorldRotation(activePlayerCollider),
            out _,
            out _);
    }

    private bool IsPlayerInsideChargeLaneGeometry(Collider activePlayerCollider, Vector3 laneDirection, float laneDistance, float laneHalfWidth)
    {
        if (activePlayerCollider == null)
            return false;

        Vector3 laneStart = GetChargeLaneStart(laneDirection);
        float allowedRadius = laneHalfWidth + GetPlayerContactRadius(activePlayerCollider);

        Vector3 colliderPoint = activePlayerCollider.ClosestPoint(laneStart + (laneDirection * (laneDistance * 0.5f)));
        if (IsPointInsideChargeLane(colliderPoint, laneStart, laneDirection, laneDistance, allowedRadius))
            return true;

        if (player != null && IsPointInsideChargeLane(player.position, laneStart, laneDirection, laneDistance, allowedRadius))
            return true;

        if (playerStats != null && playerStats.TryGetFirstPersonCameraPoint(out Vector3 cameraPoint) &&
            IsPointInsideChargeLane(cameraPoint, laneStart, laneDirection, laneDistance, allowedRadius + 0.12f))
            return true;

        return false;
    }

    private bool IsPlayerInsideActiveChargeLane()
    {
        if (!TryGetPlayerCollider(out Collider activePlayerCollider) || activePlayerCollider == null)
            return false;

        Vector3 laneDirection = chargeDirection;
        if (laneDirection.sqrMagnitude <= 0.0001f)
            laneDirection = GetHorizontalDirectionToPlayer();
        if (laneDirection.sqrMagnitude <= 0.0001f)
            laneDirection = transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Vector3.forward;

        laneDirection.y = 0f;
        laneDirection.Normalize();
        float laneDistance = GetChargeThreatDistance(plannedChargeDistance);
        float laneHalfWidth = chargeLaneHalfWidth > 0.01f ? chargeLaneHalfWidth : GetChargeLaneHalfWidth();
        return IsPlayerInsideChargeLaneGeometry(activePlayerCollider, laneDirection, laneDistance, laneHalfWidth);
    }

    private static bool IsPointInsideChargeLane(Vector3 point, Vector3 laneStart, Vector3 laneDirection, float laneDistance, float allowedRadius)
    {
        Vector3 toPoint = point - laneStart;
        toPoint.y = 0f;
        float alongDistance = Vector3.Dot(toPoint, laneDirection);
        if (alongDistance < -0.15f || alongDistance > laneDistance + 0.15f)
            return false;

        Vector3 lateralOffset = toPoint - (laneDirection * alongDistance);
        return lateralOffset.magnitude <= allowedRadius;
    }

    private bool IsPlayerCollider(Collider other)
    {
        if (playerStats == null) return false;
        if (playerCollider == null) playerCollider = playerStats.GetComponent<Collider>();
        if (other == playerCollider) return true;
        return other.GetComponentInParent<PlayerStats>() == playerStats;
    }

    private bool TryGetPlayerCollider(out Collider activePlayerCollider)
    {
        activePlayerCollider = playerCollider;
        if (activePlayerCollider != null || playerStats == null) return activePlayerCollider != null;
        activePlayerCollider = playerStats.GetComponent<Collider>();
        playerCollider = activePlayerCollider;
        return activePlayerCollider != null;
    }

    private bool IsChargeHitboxTouchingPlayer(Collider activePlayerCollider)
    {
        return IsChargeHitboxTouchingPlayer(activePlayerCollider, GetImmediateBullPosition(), GetImmediateBullRotation());
    }

    private bool IsChargeHitboxTouchingPlayer(Collider activePlayerCollider, Vector3 bullPosition, Quaternion bullRotation)
    {
        if (!TryGetChargeHitboxWorldPose(bullPosition, bullRotation, out BoxCollider hitboxCollider, out Vector3 hitboxWorldPosition, out Quaternion hitboxWorldRotation, out Vector3 worldCenter, out Vector3 halfExtents) ||
            activePlayerCollider == null)
            return false;

        return DoesChargeHitboxOverlapPlayer(activePlayerCollider, hitboxCollider, hitboxWorldPosition, hitboxWorldRotation, worldCenter, halfExtents);
    }

    private bool TryGetChargeHitboxWorldPose(
        Vector3 bullPosition,
        Quaternion bullRotation,
        out BoxCollider hitboxCollider,
        out Vector3 hitboxWorldPosition,
        out Quaternion hitboxWorldRotation,
        out Vector3 worldCenter,
        out Vector3 halfExtents)
    {
        hitboxCollider = chargeHitbox != null ? chargeHitbox.GetComponent<BoxCollider>() : null;
        hitboxWorldPosition = default;
        hitboxWorldRotation = default;
        worldCenter = default;
        halfExtents = default;

        if (hitboxCollider == null || !hitboxCollider.enabled)
            return false;

        Transform hitboxTransform = hitboxCollider.transform;
        Vector3 hitboxScale = hitboxTransform.lossyScale;
        hitboxScale = new Vector3(Mathf.Abs(hitboxScale.x), Mathf.Abs(hitboxScale.y), Mathf.Abs(hitboxScale.z));
        hitboxWorldPosition = bullPosition + (bullRotation * hitboxTransform.localPosition);
        hitboxWorldRotation = bullRotation * hitboxTransform.localRotation;
        worldCenter = hitboxWorldPosition + (hitboxWorldRotation * Vector3.Scale(hitboxCollider.center, hitboxScale));
        halfExtents = Vector3.Scale(hitboxCollider.size * 0.5f, hitboxScale) + Vector3.one * ChargeHitboxContactPadding;
        return true;
    }

    private bool DoesChargeHitboxOverlapPlayer(
        Collider activePlayerCollider,
        BoxCollider hitboxCollider,
        Vector3 hitboxWorldPosition,
        Quaternion hitboxWorldRotation,
        Vector3 worldCenter,
        Vector3 halfExtents)
    {
        int overlapCount = Physics.OverlapBoxNonAlloc(worldCenter, halfExtents, chargeHitboxOverlapBuffer, hitboxWorldRotation, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < overlapCount; i++)
        {
            Collider overlap = chargeHitboxOverlapBuffer[i];
            chargeHitboxOverlapBuffer[i] = null;
            if (overlap == null || overlap == hitboxCollider || overlap.transform.IsChildOf(transform))
                continue;
            if (overlap == activePlayerCollider || IsPlayerCollider(overlap))
                return true;
        }

        return Physics.ComputePenetration(
            hitboxCollider,
            hitboxWorldPosition,
            hitboxWorldRotation,
            activePlayerCollider,
            GetColliderWorldPosition(activePlayerCollider),
            GetColliderWorldRotation(activePlayerCollider),
            out _,
            out _);
    }

    private bool DidChargeHitboxReachPlayer(
        Collider activePlayerCollider,
        Vector3 startBullPosition,
        Quaternion startBullRotation,
        Vector3 endBullPosition,
        Quaternion endBullRotation)
    {
        if (!TryGetChargeHitboxWorldPose(startBullPosition, startBullRotation, out BoxCollider hitboxCollider, out Vector3 startHitboxWorldPosition, out Quaternion startHitboxWorldRotation, out Vector3 startWorldCenter, out Vector3 halfExtents))
            return false;

        if (DoesChargeHitboxOverlapPlayer(activePlayerCollider, hitboxCollider, startHitboxWorldPosition, startHitboxWorldRotation, startWorldCenter, halfExtents))
            return true;

        if (!TryGetChargeHitboxWorldPose(endBullPosition, endBullRotation, out _, out Vector3 endHitboxWorldPosition, out Quaternion endHitboxWorldRotation, out Vector3 endWorldCenter, out Vector3 endHalfExtents))
            return false;

        if (DoesChargeHitboxOverlapPlayer(activePlayerCollider, hitboxCollider, endHitboxWorldPosition, endHitboxWorldRotation, endWorldCenter, endHalfExtents))
            return true;

        Vector3 sweepVector = endWorldCenter - startWorldCenter;
        float sweepDistance = sweepVector.magnitude;
        if (sweepDistance <= 0.0001f)
            return false;

        Vector3 sweepDirection = sweepVector / sweepDistance;
        int hitCount = Physics.BoxCastNonAlloc(startWorldCenter, halfExtents, sweepDirection, chargeHitboxSweepBuffer, startHitboxWorldRotation, sweepDistance, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = chargeHitboxSweepBuffer[i].collider;
            chargeHitboxSweepBuffer[i] = default;
            if (hitCollider == null || hitCollider == hitboxCollider || hitCollider.transform.IsChildOf(transform))
                continue;
            if (hitCollider == activePlayerCollider || IsPlayerCollider(hitCollider))
                return true;
        }

        return false;
    }

    private bool ShouldLockPlayerDuringMissCommit()
    {
        if (playerStats == null || playerStats.IsDead || currentState != BullState.Charging)
            return false;

        return IsPlayerInsideActiveChargeLane();
    }

    private void SetChargeMissPlayerLock(bool active)
    {
        if (chargeMissPlayerLockActive == active)
            return;

        chargeMissPlayerLockActive = active;
        playerStats?.SetBullChargeLock(active);
    }

    // Charge damage is still resolved by the dedicated hit logic, not by the body-blocking collider.
    private bool IsPlayerOverlappingCloseChargeZone(Collider activePlayerCollider)
    {
        if (bullCollider == null) return false;
        Vector3 bullPosition = GetCurrentBullPosition();
        Quaternion bullRotation = GetCurrentBullRotation();
        Vector3 playerPosition = GetColliderWorldPosition(activePlayerCollider);
        Quaternion playerRotation = GetColliderWorldRotation(activePlayerCollider);
        Vector3 playerSamplePoint = activePlayerCollider.ClosestPoint(bullPosition + (chargeDirection * Mathf.Max(0.5f, GetChargeHitboxReach() * 0.7f)));
        Vector3 playerLocalPoint = ToBullLocalPoint(playerSamplePoint, bullPosition, bullRotation);
        float closeContactRadius = GetPlayerContactRadius(activePlayerCollider);
        float halfWidth = GetBullBodyHalfWidth() + closeContactRadius;
        float rearForgiveness = Mathf.Max(0.18f, closeContactRadius * 0.45f);
        float forwardReach = GetBullBodyFrontFaceDistance() + ChargeContactForwardEpsilon;
        bool isInsideCloseZone = Mathf.Abs(playerLocalPoint.x) <= halfWidth && playerLocalPoint.z >= -rearForgiveness && playerLocalPoint.z <= forwardReach;
        if (isInsideCloseZone) return true;
        if (!Physics.ComputePenetration(bullCollider, bullPosition, bullRotation, activePlayerCollider, playerPosition, playerRotation, out _, out _)) return false;
        Vector3 overlapCenterLocal = ToBullLocalPoint(activePlayerCollider.bounds.center, bullPosition, bullRotation);
        return Mathf.Abs(overlapCenterLocal.x) <= halfWidth && overlapCenterLocal.z >= -rearForgiveness;
    }

    private Vector3 ToBullLocalPoint(Vector3 worldPoint, Vector3 bullPosition, Quaternion bullRotation)
    {
        return Quaternion.Inverse(bullRotation) * (worldPoint - bullPosition);
    }

    private float GetBullBodyHalfWidth()
    {
        if (bullCollider is BoxCollider bodyBox && bodyBox.transform == transform)
            return Mathf.Max(0.25f, Mathf.Abs(bodyBox.center.x) + (bodyBox.size.x * 0.5f));
        return 0.45f;
    }

    private float GetBullBodyFrontFaceDistance()
    {
        if (bullCollider is BoxCollider bodyBox && bodyBox.transform == transform)
            return Mathf.Max(0.5f, bodyBox.center.z + (bodyBox.size.z * 0.5f));
        return 1.1f;
    }

    private static float GetPlayerContactRadius(Collider activePlayerCollider)
    {
        if (activePlayerCollider is CapsuleCollider capsule)
            return Mathf.Max(capsule.radius, 0.2f);
        if (activePlayerCollider is CharacterController characterController)
            return Mathf.Max(characterController.radius, 0.2f);
        Bounds bounds = activePlayerCollider.bounds;
        return Mathf.Max(0.2f, Mathf.Min(bounds.extents.x, bounds.extents.z));
    }

    private static Vector3 GetColliderWorldPosition(Collider activePlayerCollider)
    {
        if (activePlayerCollider.attachedRigidbody != null) return activePlayerCollider.attachedRigidbody.position;
        return activePlayerCollider.transform.position;
    }

    private static Quaternion GetColliderWorldRotation(Collider activePlayerCollider)
    {
        if (activePlayerCollider.attachedRigidbody != null) return activePlayerCollider.attachedRigidbody.rotation;
        return activePlayerCollider.transform.rotation;
    }

    private void ConfigureRigidbody()
    {
        if (bullRigidbody == null) return;
        bullRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        bullRigidbody.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    private void EnsureChargeHitbox()
    {
        Transform existing = transform.Find("ChargeHitbox");
        GameObject hitboxObject = existing != null ? existing.gameObject : new GameObject("ChargeHitbox");
        if (hitboxObject.transform.parent != transform) hitboxObject.transform.SetParent(transform, false);
        hitboxObject.layer = gameObject.layer;
        hitboxObject.transform.localPosition = Vector3.zero;
        hitboxObject.transform.localRotation = Quaternion.identity;
        hitboxObject.transform.localScale = Vector3.one;
        BoxCollider hitboxCollider = hitboxObject.GetComponent<BoxCollider>();
        if (hitboxCollider == null)
            hitboxCollider = hitboxObject.AddComponent<BoxCollider>();
        hitboxCollider.isTrigger = true;
        hitboxCollider.enabled = true;
        if (bullCollider is BoxCollider bodyBox && bodyBox.transform == transform)
        {
            hitboxCollider.center = bodyBox.center + new Vector3(0f, 0f, (bodyBox.size.z * 0.5f) + (hitDistance * 0.5f));
            hitboxCollider.size = new Vector3(Mathf.Max(0.45f, bodyBox.size.x * 0.75f), Mathf.Max(0.7f, bodyBox.size.y * 0.75f), Mathf.Max(0.8f, hitDistance * 1.2f));
        }
        else
        {
            hitboxCollider.center = new Vector3(0f, 0.8f, 1.1f);
            hitboxCollider.size = new Vector3(0.75f, 1f, Mathf.Max(0.8f, hitDistance * 1.2f));
        }
        chargeHitbox = hitboxObject.GetComponent<BullChargeHitbox>();
        if (chargeHitbox == null)
            chargeHitbox = hitboxObject.AddComponent<BullChargeHitbox>();
        chargeHitbox.Initialize(this);
    }

    private Vector3 GetCurrentBullPosition() => hasQueuedMovePosition ? queuedMovePosition : GetImmediateBullPosition();
    private Vector3 GetImmediateBullPosition() => bullRigidbody != null ? bullRigidbody.position : transform.position;
    private Quaternion GetImmediateBullRotation() => bullRigidbody != null ? bullRigidbody.rotation : transform.rotation;
    private Quaternion GetCurrentBullRotation() => hasQueuedMoveRotation ? queuedMoveRotation : (bullRigidbody != null ? bullRigidbody.rotation : transform.rotation);
    private void QueueMovePosition(Vector3 nextPosition) { queuedMovePosition = nextPosition; hasQueuedMovePosition = true; }
    private void QueueMoveRotation(Quaternion nextRotation) { queuedMoveRotation = nextRotation; hasQueuedMoveRotation = true; }

    private void UpdatePhaseTwoChargeMotion()
    {
        MoveCharge(GetChargeSpeed(), Time.unscaledDeltaTime);

        if (!dashedThisCharge && playerStats != null && playerStats.LastDashTime >= chargeStartedAt)
            dashedThisCharge = true;

        stateTimer -= Time.unscaledDeltaTime;
        if (stateTimer > 0f && HorizontalDistance(chargeStartPosition, GetCurrentBullPosition()) < plannedChargeDistance)
            return;

        phaseTwoChargeMotionActive = false;
        currentState = BullState.Idle;
        stateTimer = 0f;
        HideChargeTelegraph();
    }

    private void CommitQueuedPoseImmediate()
    {
        if (bullRigidbody == null) return;
        if (hasQueuedMoveRotation) bullRigidbody.rotation = queuedMoveRotation;
        if (hasQueuedMovePosition) bullRigidbody.position = queuedMovePosition;
        hasQueuedMovePosition = false;
        hasQueuedMoveRotation = false;
    }
}

