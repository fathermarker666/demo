using System;
using System.Collections.Generic;
using UnityEngine;
using InfimaGames.LowPolyShooterPack;

[DisallowMultipleComponent]
public class BullfightHandAnimatorController : MonoBehaviour
{
    public enum HandPropVisibility
    {
        None,
        Cloth,
        Sword
    }

    private enum HandState
    {
        Idle,
        Walk,
        HoldCloth,
        UseCloth,
        Dash,
        Throw,
        HoldSword,
        UseSword,
        Hurt,
        Death
    }

    private sealed class StateBinding
    {
        public StateBinding(string layerName, string stateName, string clipName)
        {
            LayerName = layerName;
            StateName = stateName;
            ClipName = clipName;
        }

        public string LayerName { get; }
        public string StateName { get; }
        public string ClipName { get; }
        public int LayerIndex { get; set; } = -1;
        public float ClipLength { get; set; } = 0.2f;
    }

    [Header("References")]
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private BullfightPlayerController playerController;
    [SerializeField] private BullfightGameFlow gameFlow;
    [SerializeField] private Animator handAnimator;
    [SerializeField] private RuntimeAnimatorController handController;

    [Header("Detection")]
    [SerializeField] private string handAnimatorNameHint = "SK_FP_CH_Default_Root";
    [SerializeField] private string handControllerNameHint = "AC_Phase1Idle_Edit";
    [SerializeField] private string externalHandRigNameHint = "SK_FP_CH_Default_Cubic";
    [SerializeField] private float walkThreshold = 0.05f;

    [Header("Rig Prop Detection")]
    [SerializeField] private string adoptedRigClothPropName = "Cape";
    [SerializeField] private string adoptedRigSwordPropName = "Rapier_lowpoly";

    [Header("Adopted Rig Offsets")]
    [SerializeField] private Vector3 adoptedRigLocalPositionOffset = new Vector3(-1.54f, -1.5f, 0.38f);
    [SerializeField] private Vector3 adoptedRigLocalEulerOffset = new Vector3(0f, 90f, -90f);

    private Character shooterCharacter;
    private HandState? currentState;
    private HandState? transientState;
    private float transientStateRemaining = -1f;
    private bool bindingsCached;
    private bool subscribed;
    private readonly List<Animator> disabledConflictingAnimators = new List<Animator>();
    private readonly List<SkinnedMeshRenderer> disabledConflictingRenderers = new List<SkinnedMeshRenderer>();
    private Animator conflictReferenceAnimator;
    private BullfightProjectileThrower projectileThrower;
    private Transform adoptedRigRoot;
    private Vector3 adoptedRigBaseLocalPosition;
    private Quaternion adoptedRigBaseLocalRotation = Quaternion.identity;
    private Vector3 adoptedRigBaseLocalScale = Vector3.one;
    private bool adoptedRigBasePoseCached;
    private Transform adoptedRigClothProp;
    private Transform adoptedRigSwordProp;
    private bool throwSpawnTriggered;
    private Transform presentationRoot;
    private bool rigSetupDirty = true;

    private readonly Dictionary<HandState, StateBinding> bindings = new Dictionary<HandState, StateBinding>
    {
        { HandState.Idle, new StateBinding("IDEL", "Phase1_Idle_Clean", "Phase1_Idle") },
        { HandState.Walk, new StateBinding("WAIK", "Phase1_walk_Clean 1", "Phase1_walk") },
        // The authored cloth clips are named opposite to their in-game meaning:
        // "usecloth" is the held pose, and "holdcloth" is the one-shot use action.
        { HandState.HoldCloth, new StateBinding("usecloth", "Phase1__usecloth", "Phase1__usecloth") },
        { HandState.UseCloth, new StateBinding("holdcloth", "Phase1__holdcloth", "Phase1__holdcloth") },
        { HandState.Dash, new StateBinding("DASH", "Phase1__dash", "Phase1__dash") },
        { HandState.Throw, new StateBinding("THROW", "Phase1__throw 1", "Phase1__throw 1") },
        { HandState.HoldSword, new StateBinding("HOLDSOURE", "Phase1__holdsource", "Phase1__holdsource") },
        { HandState.UseSword, new StateBinding("USESOURE", "Phase1__usesoure", "Phase1__usesoure") },
        { HandState.Hurt, new StateBinding("BE HURT", "Phase1_be hurt", "Phase1_be hurt") },
        { HandState.Death, new StateBinding("DEATH", "Phase1__death", "Phase1__death") }
    };

    private void Awake()
    {
        ResolveReferencesIfNeeded();
        EnsureRigSetupIfNeeded(true);
        CacheBindings();
        ForceRefreshState();
    }

    private void OnEnable()
    {
        ResolveReferencesIfNeeded();
        EnsureRigSetupIfNeeded(true);
        CacheBindings();
        Subscribe();
        ForceRefreshState();
    }

    private void Start()
    {
        ResolveReferencesIfNeeded();
        EnsureRigSetupIfNeeded(true);
        CacheBindings();
        ForceRefreshState();
    }

    private void Update()
    {
        if (HasMissingCoreReferences())
            ResolveReferencesIfNeeded();

        if (NeedsRigSetup())
            EnsureRigSetupIfNeeded();

        if (NeedsBindingCache())
            CacheBindings();

        if (!subscribed && playerStats != null)
            Subscribe();

        RefreshAnimatorPlaybackTiming();

        if (handAnimator == null || !bindingsCached)
            return;

        if (playerController != null && playerController.IsPhaseTwoStabPressedThisFrame())
            PlayTransient(HandState.UseSword);

        if (transientState.HasValue)
        {
            transientStateRemaining -= GetAnimatorPlaybackDeltaTime();
            if (transientStateRemaining <= 0f)
            {
                transientState = null;
                transientStateRemaining = -1f;
            }
        }

        SwitchState(ResolveDesiredState(), restart: false);
        UpdateThrowSpawn();
    }

    private void LateUpdate()
    {
        // Re-apply adopted rig alignment after Mecanim updates so root-position curves
        // or other late animation writes do not cancel the manual offsets.
        if (adoptedRigRoot == null)
            return;

        ApplyRigOffsets(adoptedRigRoot);
        UpdateAdoptedRigPropVisibility(adoptedRigRoot);
    }

    private void OnDisable()
    {
        RestoreConflictingAnimators();
        Unsubscribe();
    }

    private void ResolveReferencesIfNeeded()
    {
        if (playerStats == null)
            playerStats = BullfightSceneCache.GetLocalOrScene<PlayerStats>(this);

        if (playerController == null)
            playerController = BullfightSceneCache.GetLocalOrScene<BullfightPlayerController>(this);

        if (gameFlow == null)
            gameFlow = BullfightSceneCache.FindObject<BullfightGameFlow>();

        if (shooterCharacter == null)
            shooterCharacter = BullfightSceneCache.GetLocalOrScene<Character>(this);

        if (projectileThrower == null)
            projectileThrower = BullfightSceneCache.GetLocalOrScene<BullfightProjectileThrower>(this);
    }

    private bool HasMissingCoreReferences()
    {
        return playerStats == null ||
               playerController == null ||
               gameFlow == null ||
               shooterCharacter == null ||
               projectileThrower == null;
    }

    private bool NeedsRigSetup()
    {
        return rigSetupDirty ||
               presentationRoot == null ||
               handAnimator == null ||
               (adoptedRigRoot == null && !adoptedRigBasePoseCached);
    }

    private bool NeedsBindingCache()
    {
        return !bindingsCached && handAnimator != null;
    }

    private void EnsureRigSetupIfNeeded(bool force = false)
    {
        if (adoptedRigRoot == null)
            adoptedRigBasePoseCached = false;

        bool needsSetup = force ||
                          rigSetupDirty ||
                          presentationRoot == null ||
                          handAnimator == null ||
                          (adoptedRigRoot == null && !adoptedRigBasePoseCached);

        if (!needsSetup)
            return;

        presentationRoot = transform.Find(handAnimatorNameHint);
        AdoptSceneHandRig(presentationRoot);

        Animator preferredHandAnimator = FindPreferredHandAnimator();
        bool handAnimatorChanged = preferredHandAnimator != null && preferredHandAnimator != handAnimator;
        if (handAnimatorChanged)
        {
            handAnimator = preferredHandAnimator;
            bindingsCached = false;
            rigSetupDirty = true;
        }

        if (handController == null && handAnimator != null && handAnimator.runtimeAnimatorController != null)
        {
            string controllerName = handAnimator.runtimeAnimatorController.name;
            if (controllerName.Contains(handControllerNameHint, StringComparison.OrdinalIgnoreCase))
                handController = handAnimator.runtimeAnimatorController;
        }

        bool controllerChanged = false;
        if (handAnimator != null && handController != null && handAnimator.runtimeAnimatorController != handController)
        {
            handAnimator.runtimeAnimatorController = handController;
            controllerChanged = true;
            bindingsCached = false;
            rigSetupDirty = true;
        }

        if (handAnimator != null)
        {
            handAnimator.enabled = true;
            handAnimator.applyRootMotion = false;
            handAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            ApplyAnimatorPlaybackTiming(handAnimator);

            if (presentationRoot != null && handAnimator.transform.parent == presentationRoot)
                SetLayerRecursively(handAnimator.transform, presentationRoot.gameObject.layer);

            if (handAnimatorChanged || controllerChanged)
            {
                handAnimator.Rebind();
                handAnimator.Update(0f);
                currentState = null;
            }
        }

        DisableConflictingAnimators();
        rigSetupDirty = false;
    }

    private void CacheBindings()
    {
        if (bindingsCached)
            return;

        if (handAnimator == null)
            return;

        RuntimeAnimatorController controller = handAnimator.runtimeAnimatorController;
        if (controller == null)
            return;

        foreach (KeyValuePair<HandState, StateBinding> pair in bindings)
        {
            StateBinding binding = pair.Value;
            binding.LayerIndex = handAnimator.GetLayerIndex(binding.LayerName);
            binding.ClipLength = ResolveClipLength(controller, binding.ClipName, binding.ClipLength);
        }

        bindingsCached = true;
    }

    private void Subscribe()
    {
        if (subscribed || playerStats == null)
            return;

        playerStats.OnCapaPerformed += HandleCapaPerformed;
        playerStats.OnDashPerformed += HandleDashPerformed;
        playerStats.OnBanderillasPerformed += HandleBanderillasPerformed;
        playerStats.OnDamaged += HandleDamaged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || playerStats == null)
            return;

        playerStats.OnCapaPerformed -= HandleCapaPerformed;
        playerStats.OnDashPerformed -= HandleDashPerformed;
        playerStats.OnBanderillasPerformed -= HandleBanderillasPerformed;
        playerStats.OnDamaged -= HandleDamaged;
        subscribed = false;
    }

    private void HandleCapaPerformed()
    {
        PlayTransient(HandState.UseCloth);
    }

    private void HandleBanderillasPerformed()
    {
        PlayTransient(HandState.Throw);
    }

    private void HandleDashPerformed()
    {
        PlayTransient(HandState.Dash);
    }

    private void HandleDamaged(float _)
    {
        if (playerStats != null && playerStats.IsDead)
        {
            ForceDeathAnimation();
            return;
        }

        PlayTransient(HandState.Hurt);
    }

    public void ForceDeathAnimation()
    {
        if (handAnimator == null || !bindingsCached)
            return;

        transientState = null;
        transientStateRemaining = -1f;
        SwitchState(HandState.Death, restart: true);
    }

    public void PlayPhaseTwoStabAnimation()
    {
        if (handAnimator == null || !bindingsCached)
        {
            ResolveReferencesIfNeeded();
            EnsureRigSetupIfNeeded(true);
            CacheBindings();
        }

        if (handAnimator == null || !bindingsCached)
            return;

        PlayTransient(HandState.UseSword);
    }

    private void PlayTransient(HandState state)
    {
        if (!bindings.TryGetValue(state, out StateBinding binding))
            return;

        transientState = state;
        transientStateRemaining = Mathf.Max(0.05f, binding.ClipLength);
        if (state == HandState.Throw)
            throwSpawnTriggered = false;
        SwitchState(state, restart: true);
    }

    private void RefreshAnimatorPlaybackTiming()
    {
        ApplyAnimatorPlaybackTiming(handAnimator);

        if (adoptedRigRoot == null)
            return;

        Animator adoptedAnimator = adoptedRigRoot.GetComponent<Animator>();
        if (adoptedAnimator != handAnimator)
            ApplyAnimatorPlaybackTiming(adoptedAnimator);
    }

    private void ApplyAnimatorPlaybackTiming(Animator animator)
    {
        if (animator == null)
            return;

        animator.speed = GetAnimatorPlaybackSpeed();
        animator.updateMode = AnimatorUpdateMode.Normal;
    }

    private float GetAnimatorPlaybackSpeed()
    {
        if (!ShouldCompensatePhaseTwoHandAnimation())
            return 1f;

        float timeScale = Time.timeScale;
        if (timeScale <= 0.0001f)
            return 1f;

        return 1f / timeScale;
    }

    private float GetAnimatorPlaybackDeltaTime()
    {
        if (BullfightPauseSettingsUI.Instance != null && BullfightPauseSettingsUI.Instance.IsPauseMenuOpen)
            return 0f;

        return ShouldCompensatePhaseTwoHandAnimation()
            ? Time.unscaledDeltaTime
            : Time.deltaTime;
    }

    private bool ShouldCompensatePhaseTwoHandAnimation()
    {
        return gameFlow != null &&
               gameFlow.currentPhase == BullfightGameFlow.GamePhase.PhaseTwo &&
               (BullfightPauseSettingsUI.Instance == null || !BullfightPauseSettingsUI.Instance.IsPauseMenuOpen);
    }

    private HandState ResolveDesiredState()
    {
        if (playerStats != null && playerStats.IsDead)
            return HandState.Death;

        if (transientState.HasValue)
            return transientState.Value;

        if (gameFlow != null && gameFlow.currentPhase == BullfightGameFlow.GamePhase.PhaseTwo)
            return HandState.HoldSword;

        if (playerStats != null && playerStats.isHoldingCloth)
            return HandState.HoldCloth;

        return IsWalking() ? HandState.Walk : HandState.Idle;
    }

    private bool IsWalking()
    {
        if (playerStats != null && (playerStats.isHoldingCloth || playerStats.isStunned || playerStats.IsDead))
            return false;

        if (gameFlow != null && gameFlow.currentPhase == BullfightGameFlow.GamePhase.PhaseTwo)
            return false;

        if (shooterCharacter != null)
            return shooterCharacter.GetInputMovement().sqrMagnitude > walkThreshold * walkThreshold;

        Vector2 fallbackInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        return fallbackInput.sqrMagnitude > walkThreshold * walkThreshold;
    }

    private void ForceRefreshState()
    {
        if (handAnimator == null || !bindingsCached)
            return;

        HandState desiredState = ResolveDesiredState();
        SwitchState(desiredState, restart: true);
    }

    private void SwitchState(HandState state, bool restart)
    {
        if (handAnimator == null || !bindingsCached)
            return;

        if (!bindings.TryGetValue(state, out StateBinding binding))
            return;

        if (binding.LayerIndex < 0)
            return;

        foreach (KeyValuePair<HandState, StateBinding> pair in bindings)
        {
            if (pair.Value.LayerIndex >= 0)
                handAnimator.SetLayerWeight(pair.Value.LayerIndex, pair.Key == state ? 1f : 0f);
        }

        if (restart || !currentState.HasValue || currentState.Value != state)
            handAnimator.Play(binding.StateName, binding.LayerIndex, 0f);

        if (state != HandState.Throw)
            throwSpawnTriggered = false;

        currentState = state;
    }

    private void UpdateThrowSpawn()
    {
        if (throwSpawnTriggered || projectileThrower == null || currentState != HandState.Throw)
            return;

        if (!bindings.TryGetValue(HandState.Throw, out StateBinding binding) || binding.LayerIndex < 0)
            return;

        AnimatorStateInfo stateInfo = handAnimator.GetCurrentAnimatorStateInfo(binding.LayerIndex);
        if (stateInfo.normalizedTime < projectileThrower.ThrowSpawnNormalizedTime)
            return;

        projectileThrower.NotifyThrowAnimationReachedFrame();
        throwSpawnTriggered = true;
    }

    private void DisableConflictingAnimators()
    {
        if (handAnimator == conflictReferenceAnimator)
            return;

        RestoreConflictingAnimators();
        if (handAnimator == null)
            return;

        conflictReferenceAnimator = handAnimator;

        foreach (Animator candidate in GetComponentsInChildren<Animator>(true))
        {
            if (candidate == null || candidate == handAnimator)
                continue;

            bool sharesAvatar = candidate.avatar != null && handAnimator.avatar != null && candidate.avatar == handAnimator.avatar;
            if (!sharesAvatar)
                continue;

            candidate.enabled = false;
            disabledConflictingAnimators.Add(candidate);

            foreach (SkinnedMeshRenderer renderer in candidate.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer == null || !renderer.enabled || disabledConflictingRenderers.Contains(renderer))
                    continue;

                Animator ownerAnimator = renderer.GetComponentInParent<Animator>();
                if (ownerAnimator != candidate)
                    continue;

                renderer.enabled = false;
                disabledConflictingRenderers.Add(renderer);
            }
        }
    }

    private void RestoreConflictingAnimators()
    {
        for (int i = 0; i < disabledConflictingAnimators.Count; i++)
        {
            Animator candidate = disabledConflictingAnimators[i];
            if (candidate != null)
                candidate.enabled = true;
        }

        disabledConflictingAnimators.Clear();

        for (int i = 0; i < disabledConflictingRenderers.Count; i++)
        {
            SkinnedMeshRenderer renderer = disabledConflictingRenderers[i];
            if (renderer != null)
                renderer.enabled = true;
        }

        disabledConflictingRenderers.Clear();
        conflictReferenceAnimator = null;
    }

    private void AdoptSceneHandRig(Transform presentationRoot)
    {
        if (presentationRoot == null)
            return;

        Animator externalHandAnimator = ResolveAdoptedHandAnimator(presentationRoot);
        if (externalHandAnimator == null)
            return;

        Transform externalRig = externalHandAnimator.transform;
        if (!externalRig.gameObject.activeSelf)
            externalRig.gameObject.SetActive(true);

        bool parentChanged = externalRig.parent != presentationRoot;
        if (parentChanged)
            externalRig.SetParent(presentationRoot, true);

        if (adoptedRigRoot != externalRig)
        {
            adoptedRigRoot = externalRig;
            adoptedRigBasePoseCached = false;
            adoptedRigClothProp = null;
            adoptedRigSwordProp = null;
        }

        if (!adoptedRigBasePoseCached || parentChanged)
        {
            Transform internalAnchor = FindAnchorTransform(presentationRoot, externalRig);
            Transform externalAnchor = FindAnchorTransform(externalRig, null);

            AlignRigToAnchor(externalRig, externalAnchor, internalAnchor);
            CacheAdoptedRigBasePose(externalRig);
        }

        ApplyRigOffsets(externalRig);
        SetLayerRecursively(externalRig, presentationRoot.gameObject.layer);
        EnsureRigVisible(externalRig);
        UpdateAdoptedRigPropVisibility(externalRig);

        externalHandAnimator.enabled = true;
        externalHandAnimator.applyRootMotion = false;
        externalHandAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        ApplyAnimatorPlaybackTiming(externalHandAnimator);
    }

    private Animator ResolveAdoptedHandAnimator(Transform presentationRoot)
    {
        if (handAnimator != null &&
            handAnimator.gameObject.name.Contains(externalHandRigNameHint, StringComparison.OrdinalIgnoreCase))
        {
            return handAnimator;
        }

        foreach (Animator candidate in GetComponentsInChildren<Animator>(true))
        {
            if (candidate == null)
                continue;

            if (!candidate.gameObject.name.Contains(externalHandRigNameHint, StringComparison.OrdinalIgnoreCase))
                continue;

            return candidate;
        }

        return FindExternalSceneHandAnimator();
    }

    private Animator FindPreferredHandAnimator()
    {
        Animator bestAnimator = null;
        int bestScore = 0;

        foreach (Animator candidate in GetComponentsInChildren<Animator>(true))
        {
            int score = ScoreHandAnimatorCandidate(candidate);
            if (score <= bestScore)
                continue;

            bestScore = score;
            bestAnimator = candidate;
        }

        return bestAnimator;
    }

    private Animator FindExternalSceneHandAnimator()
    {
        Animator bestAnimator = null;
        int bestScore = 0;

        var sceneAnimators = BullfightSceneCache.GetSceneObjects<Animator>();
        for (int i = 0; i < sceneAnimators.Count; i++)
        {
            Animator candidate = sceneAnimators[i];
            if (candidate == null || candidate.transform.IsChildOf(transform))
                continue;

            if (!candidate.gameObject.name.Contains(externalHandRigNameHint, StringComparison.OrdinalIgnoreCase))
                continue;

            int score = ScoreHandAnimatorCandidate(candidate);
            if (score <= bestScore)
                continue;

            bestScore = score;
            bestAnimator = candidate;
        }

        return bestAnimator;
    }

    private int ScoreHandAnimatorCandidate(Animator candidate)
    {
        if (candidate == null)
            return 0;

        int score = 0;
        bool matchesHint = false;

        string controllerName = candidate.runtimeAnimatorController != null ? candidate.runtimeAnimatorController.name : string.Empty;
        string objectName = candidate.gameObject.name;

        if (controllerName.Contains(handControllerNameHint, StringComparison.OrdinalIgnoreCase))
        {
            score += 100;
            matchesHint = true;
        }

        if (objectName.Contains(handAnimatorNameHint, StringComparison.OrdinalIgnoreCase))
        {
            score += 60;
            matchesHint = true;
        }

        if (objectName.Contains(externalHandRigNameHint, StringComparison.OrdinalIgnoreCase))
        {
            score += 200;
            matchesHint = true;
        }

        if (!matchesHint)
            return 0;

        if (candidate.transform.IsChildOf(transform))
            score += 10;

        return score;
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null)
            return;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = layer;
    }

    private static void EnsureRigVisible(Transform root)
    {
        if (root == null)
            return;

        foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer == null)
                continue;

            if (!renderer.gameObject.activeSelf)
                renderer.gameObject.SetActive(true);

            renderer.enabled = true;
            renderer.updateWhenOffscreen = true;
        }
    }

    private static Transform FindAnchorTransform(Transform root, Transform ignoredSubtreeRoot)
    {
        if (root == null)
            return null;

        Transform socketAnchor = FindDescendantByName(root, "SOCKET_Camera", ignoredSubtreeRoot);
        if (socketAnchor != null)
            return socketAnchor;

        Transform headAnchor = FindDescendantByName(root, "head", ignoredSubtreeRoot);
        if (headAnchor != null)
            return headAnchor;

        return FindDescendantByName(root, "spine_03", ignoredSubtreeRoot);
    }

    private static Transform FindDescendantByName(Transform root, string targetName, Transform ignoredSubtreeRoot)
    {
        if (root == null)
            return null;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (ignoredSubtreeRoot != null && child.IsChildOf(ignoredSubtreeRoot))
                continue;

            if (string.Equals(child.name, targetName, StringComparison.OrdinalIgnoreCase))
                return child;
        }

        return null;
    }

    private static void AlignRigToAnchor(Transform rigRoot, Transform sourceAnchor, Transform targetAnchor)
    {
        if (rigRoot == null)
            return;

        if (sourceAnchor == null || targetAnchor == null)
        {
            rigRoot.localPosition = Vector3.zero;
            rigRoot.localRotation = Quaternion.identity;
            rigRoot.localScale = Vector3.one;
            return;
        }

        Quaternion rotationDelta = targetAnchor.rotation * Quaternion.Inverse(sourceAnchor.rotation);
        rigRoot.rotation = rotationDelta * rigRoot.rotation;
        rigRoot.position += targetAnchor.position - sourceAnchor.position;
    }

    private void ApplyRigOffsets(Transform rigRoot)
    {
        if (rigRoot == null)
            return;

        if (!adoptedRigBasePoseCached || adoptedRigRoot != rigRoot)
            CacheAdoptedRigBasePose(rigRoot);

        rigRoot.localPosition = adoptedRigBaseLocalPosition + adoptedRigLocalPositionOffset;
        rigRoot.localScale = adoptedRigBaseLocalScale;

        Transform parent = rigRoot.parent;
        Quaternion baseWorldRotation = parent != null
            ? parent.rotation * adoptedRigBaseLocalRotation
            : adoptedRigBaseLocalRotation;

        Vector3 pitchAxis = parent != null ? parent.right : Vector3.right;
        Vector3 yawAxis = parent != null ? parent.up : Vector3.up;
        Vector3 rollAxis = parent != null ? parent.forward : Vector3.forward;

        Quaternion worldOffsetRotation =
            Quaternion.AngleAxis(adoptedRigLocalEulerOffset.y, yawAxis) *
            Quaternion.AngleAxis(adoptedRigLocalEulerOffset.x, pitchAxis) *
            Quaternion.AngleAxis(adoptedRigLocalEulerOffset.z, rollAxis);

        rigRoot.rotation = worldOffsetRotation * baseWorldRotation;
    }

    private void CacheAdoptedRigBasePose(Transform rigRoot)
    {
        if (rigRoot == null)
            return;

        adoptedRigRoot = rigRoot;
        adoptedRigBaseLocalPosition = rigRoot.localPosition;
        adoptedRigBaseLocalRotation = rigRoot.localRotation;
        adoptedRigBaseLocalScale = rigRoot.localScale;
        adoptedRigBasePoseCached = true;
    }

    private void UpdateAdoptedRigPropVisibility(Transform rigRoot)
    {
        if (rigRoot == null)
            return;

        CacheAdoptedRigPropReferences(rigRoot);

        HandPropVisibility visibility = GetCurrentPropVisibility();
        SetPropActive(adoptedRigClothProp, visibility == HandPropVisibility.Cloth);
        SetPropActive(adoptedRigSwordProp, visibility == HandPropVisibility.Sword);
    }

    private void CacheAdoptedRigPropReferences(Transform rigRoot)
    {
        if (rigRoot == null)
            return;

        if (adoptedRigClothProp == null)
            adoptedRigClothProp = FindDescendantByName(rigRoot, adoptedRigClothPropName, null);

        if (adoptedRigSwordProp == null)
            adoptedRigSwordProp = FindDescendantByName(rigRoot, adoptedRigSwordPropName, null);
    }

    private static void SetPropActive(Transform propRoot, bool visible)
    {
        if (propRoot == null)
            return;

        if (propRoot.gameObject.activeSelf != visible)
            propRoot.gameObject.SetActive(visible);
    }

    private static float ResolveClipLength(RuntimeAnimatorController controller, string clipName, float fallbackLength)
    {
        if (controller == null)
            return fallbackLength;

        foreach (AnimationClip clip in controller.animationClips)
        {
            if (clip != null && clip.name == clipName)
                return clip.length;
        }

        return fallbackLength;
    }

    public string GetDebugSummary()
    {
        if (handAnimator == null)
            return "Hand Anim: None";

        string controllerName = handAnimator.runtimeAnimatorController != null
            ? handAnimator.runtimeAnimatorController.name
            : "None";

        string desiredState = ResolveDesiredState().ToString();
        string cachedState = currentState.HasValue ? currentState.Value.ToString() : "None";
        string activeLayer = "None";
        string activeState = "None";
        float normalizedTime = 0f;

        foreach (KeyValuePair<HandState, StateBinding> pair in bindings)
        {
            StateBinding binding = pair.Value;
            if (binding.LayerIndex < 0 || handAnimator.GetLayerWeight(binding.LayerIndex) <= 0.001f)
                continue;

            AnimatorStateInfo stateInfo = handAnimator.GetCurrentAnimatorStateInfo(binding.LayerIndex);
            activeLayer = binding.LayerName;
            activeState = stateInfo.IsName(binding.StateName) ? binding.StateName : stateInfo.shortNameHash.ToString();
            normalizedTime = stateInfo.normalizedTime;
            break;
        }

        return
            $"Hand Anim Obj: {handAnimator.gameObject.name}\n" +
            $"Hand Ctrl: {controllerName}\n" +
            $"Desired/Cached: {desiredState} / {cachedState}\n" +
            $"Layer/State: {activeLayer} / {activeState}\n" +
            $"NormTime: {normalizedTime:F2}\n" +
            $"Anim Enabled: {handAnimator.enabled}  Speed: {handAnimator.speed:F2}";
    }

    public HandPropVisibility GetCurrentPropVisibility()
    {
        HandState state = currentState ?? ResolveDesiredState();
        return state switch
        {
            HandState.HoldCloth => HandPropVisibility.Cloth,
            HandState.UseCloth => HandPropVisibility.Cloth,
            HandState.HoldSword => HandPropVisibility.Sword,
            HandState.UseSword => HandPropVisibility.Sword,
            _ => HandPropVisibility.None
        };
    }

    public bool ShouldShowClothProp() => GetCurrentPropVisibility() == HandPropVisibility.Cloth;

    public bool ShouldShowSwordProp() => GetCurrentPropVisibility() == HandPropVisibility.Sword;
}
