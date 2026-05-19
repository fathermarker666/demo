using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using UnityEngine.InputSystem;
using InfimaGames.LowPolyShooterPack;
using System.Reflection;

public class PlayerStats : MonoBehaviour
{
    public event Action OnCapaPerformed;
    public event Action OnDashPerformed;
    public event Action OnEvadePerformed;
    public event Action OnBanderillasPerformed;
    public event Action<float> OnDamaged;
    public event Action<bool> OnHoldingClothChanged;
    public event Action<bool> OnStunStateChanged;
    public event Action<bool> OnBullChargeLockStateChanged;
    public event Action<bool> OnPerfectDodgeBuffStateChanged;

    [Header("Health")]
    public float maxHealth = 500f;
    public float currentHealth;
    public Slider healthBar;
    public Slider staminaBar;

    [Header("Stamina")]
    public float maxStamina = 100f;
    public float currentStamina;
    public float holdClothRecoveryPerSecond = 15f;
    public float capaCost = 15f;
    [FormerlySerializedAs("evadeCost")] public float dashCost = 25f;
    public float banderillasCost = 60f;
    public float stunDuration = 2f;

    [Header("Dash")]
    public float dashDistance = 2.25f;
    public float dashDuration = 0.18f;
    [SerializeField] private Animator dashAnimator;
    [SerializeField] private string dashAnimationStateName = "Dash";

    [Header("Hit Reaction")]
    public float knockbackDistance = 0.1f;
    public float knockbackDuration = 0.08f;

    [Header("Perfect Reward")]
    public float perfectDodgeDuration = 2f;
    public float perfectDodgeSpeedMultiplier = 1.25f;
    public float perfectDodgeStaminaRestore = 40f;

    [Header("Look")]
    [SerializeField] private float lookSensitivityMultiplier = 1.45f;
    [SerializeField] private float holdingClothLockYawSpeed = 7.5f;
    [SerializeField] private Vector3 gameplayViewEuler = new Vector3(-10f, 0f, 0f);

    [Header("Debug")]
    public bool isStunned;
    public bool isActing;
    public bool isTaunting;
    public bool isHoldingCloth;

    [Header("Input")]
    [SerializeField] private InputActionAsset bullfightActionsAsset;

    private Rigidbody rigidBody;
    private float stunTimer;
    private float invulnerabilityTimer;
    private float dashTimer;
    private float knockbackTimer;
    private float perfectDodgeBuffTimer;
    private Vector3 dashVelocity;
    private Vector3 knockbackVelocity;
    private Transform firstPersonRoot;
    private Transform firstPersonCamera;
    private Camera gameplayCamera;
    private Character shooterCharacter;
    private BullfightPlayerController playerController;
    private BullfightStunVfx stunVfx;
    private BullfightHandAnimatorController handAnimatorController;
    private BullAI bullAI;
    private BullfightGameFlow gameFlow;
    private CameraLook cameraLook;
    private Movement movementComponent;
    private bool shooterGameplayEnabled = true;
    private bool deathPresentationApplied;
    private bool cachedGameplayNearClip;
    private Quaternion cachedCameraLocalRotation = Quaternion.identity;
    private Quaternion deathPresentationStartRotation = Quaternion.identity;
    private Quaternion deathPresentationTargetRotation = Quaternion.identity;
    private float cachedGameplayNearClipPlane;
    private bool perfectDodgeBuffActive;
    private float deathPresentationTimer;
    private float deathPresentationTurnDelay;
    private bool clothCameraLockActive;
    private float rumbleTimer;
    private float rumbleLowMotor;
    private float rumbleHighMotor;
    private bool hasCachedLookSensitivity;
    private bool lookSensitivityApplied;
    private Vector2 cachedLookSensitivity = Vector2.one;
    private FieldInfo cameraLookSensitivityField;
    private FieldInfo cameraLookRotationCameraField;
    private FieldInfo cameraLookRotationCharacterField;
    private bool mainMenuFrozen;
    private bool hasMainMenuFrozenPose;
    private Vector3 mainMenuFrozenPosition;
    private Quaternion mainMenuFrozenRotation = Quaternion.identity;
    private Quaternion mainMenuFrozenCameraRotation = Quaternion.identity;
    private bool bullChargeLocked;
    public event Action OnDeath;

    [Header("Death Presentation")]
    [SerializeField] private Vector3 deathCameraEuler = new Vector3(-48f, 12f, 4f);
    [SerializeField] private float deathCameraTurnDuration = 1.15f;
    [SerializeField] private float phaseTwoDeathCameraTurnDelay = 0.28f;
    [SerializeField] private float gameplayNearClipPlane = 0.1f;

    [Header("Gamepad Rumble")]
    [SerializeField] private float dashRumbleLow = 0.12f;
    [SerializeField] private float dashRumbleHigh = 0.28f;
    [SerializeField] private float dashRumbleDuration = 0.08f;
    [SerializeField] private float damageRumbleLow = 0.28f;
    [SerializeField] private float damageRumbleHigh = 0.7f;
    [SerializeField] private float damageRumbleDuration = 0.2f;
    [SerializeField] private float perfectRumbleLow = 0.16f;
    [SerializeField] private float perfectRumbleHigh = 0.48f;
    [SerializeField] private float perfectRumbleDuration = 0.12f;

    public bool IsDead => currentHealth <= 0f;
    public bool IsDashing => dashTimer > 0f;
    public bool IsInvulnerable => invulnerabilityTimer > 0f;
    public bool IsPerfectDodgeBuffActive => perfectDodgeBuffActive;
    public bool IsBullChargeLocked => bullChargeLocked;
    public float LastDashTime { get; private set; } = -999f;
    public float HealthNormalized => maxHealth <= 0f ? 0f : currentHealth / maxHealth;
    public float StaminaNormalized => maxStamina <= 0f ? 0f : currentStamina / maxStamina;
    public float evadeCost => dashCost;

    private bool deathEventRaised = false;

    private void Awake()
    {
        currentHealth = maxHealth;
        currentStamina = maxStamina;
        isStunned = false;
        isActing = false;
        isTaunting = false;
        isHoldingCloth = false;
        rigidBody = GetComponent<Rigidbody>();
        _ = GetComponent<BullfightPlayerController>() ?? gameObject.AddComponent<BullfightPlayerController>();
        _ = GetComponent<BullfightCapeBinder>() ?? gameObject.AddComponent<BullfightCapeBinder>();
        _ = GetComponent<BullfightSpawnManager>() ?? gameObject.AddComponent<BullfightSpawnManager>();
        _ = GetComponent<BullfightHandAnimatorController>() ?? gameObject.AddComponent<BullfightHandAnimatorController>();
        _ = GetComponent<BullfightProjectileThrower>() ?? gameObject.AddComponent<BullfightProjectileThrower>();
        _ = GetComponent<BullfightStaminaPreviewUI>() ?? gameObject.AddComponent<BullfightStaminaPreviewUI>();
        _ = GetComponent<BullfightStunVfx>() ?? gameObject.AddComponent<BullfightStunVfx>();
        //_ = GetComponent<BullfightAudioController>() ?? gameObject.AddComponent<BullfightAudioController>();
        _ = GetComponent<BullfightPerfectDodgeVfx>() ?? gameObject.AddComponent<BullfightPerfectDodgeVfx>();
        stunVfx = GetComponent<BullfightStunVfx>();
        handAnimatorController = GetComponent<BullfightHandAnimatorController>();
        shooterCharacter = GetComponent<Character>();
        movementComponent = GetComponent<Movement>();
        playerController = GetComponent<BullfightPlayerController>();
        playerController?.ConfigureInputActions(bullfightActionsAsset);
        bullAI = FindObjectOfType<BullAI>(true);
        gameFlow = BullfightSceneCache.FindObject<BullfightGameFlow>();
        lookSensitivityMultiplier = Mathf.Max(1.45f, lookSensitivityMultiplier);
        CachePresentationReferences();
        StabilizeFirstPersonPresentation();
        UpdateHealthBar();
    }

    private void Start()
    {
        // Ensure gameplay state starts from full resources.
        currentHealth = maxHealth;
        currentStamina = maxStamina;

        UpdateUI();

    }

    private void Update()
    {
        if (mainMenuFrozen)
        {
            EnforceMainMenuFrozenPose();
            UpdateGamepadRumble();
            return;
        }

        UpdateStun();
        UpdateInvulnerability();
        UpdateDash();
        UpdateKnockback();
        UpdatePerfectDodgeBuff();
        UpdateStamina();
        UpdateHoldingClothCameraLock();
        EnforceHorizontalOnlyView();
        UpdateDeathPresentation();
        UpdateGamepadRumble();
        StabilizeFirstPersonPresentation();
        UpdateUI();
    }

    private void LateUpdate()
    {
        if (mainMenuFrozen)
            EnforceMainMenuFrozenPose();
    }

    private bool IsGameplayControlLocked()
    {
        return isStunned || bullChargeLocked;
    }

    private void RestoreGameplayControlsIfAvailable()
    {
        if (mainMenuFrozen)
            return;

        SetShooterControlEnabled(shooterGameplayEnabled && !IsGameplayControlLocked());
        if (!IsGameplayControlLocked() && !isHoldingCloth)
            EnsureGameplayLookUnlocked();
    }

    public bool CanAct()
    {
        return !IsDead && !IsGameplayControlLocked() && !isActing;
    }

    public bool TrySpendStamina(float amount)
    {
        if (currentStamina < amount)
            return false;

        currentStamina -= amount;
        if (currentStamina <= 0f)
            TriggerStun();

        return true;
    }

    public bool HasEnoughStamina(float amount)
    {
        return currentStamina >= Mathf.Max(0f, amount);
    }

    public float GetProjectedStamina(float spendAmount)
    {
        return Mathf.Clamp(currentStamina - Mathf.Max(0f, spendAmount), 0f, maxStamina);
    }

    public float GetProjectedStaminaNormalized(float spendAmount)
    {
        return maxStamina <= 0f ? 0f : GetProjectedStamina(spendAmount) / maxStamina;
    }

    public bool TryStartBanderillas()
    {
        if (!CanAct())
            return false;

        if (!TrySpendStamina(banderillasCost))
            return false;

        isActing = true;
        Invoke(nameof(EndActionLock), 0.6f);
        OnBanderillasPerformed?.Invoke();
        return true;
    }

    public bool TryDoCapa()
    {
        if (!CanAct() || !isHoldingCloth)
            return false;

        if (!TrySpendStamina(capaCost))
            return false;

        isActing = true;
        Invoke(nameof(EndActionLock), 0.35f);
        OnCapaPerformed?.Invoke();
        return true;
    }

    public bool TryDash()
    {
        if (!CanAct())
            return false;

        if (!TrySpendStamina(dashCost))
            return false;

        isActing = true;
        dashVelocity = ResolveDashDirection() * (dashDistance / Mathf.Max(0.01f, dashDuration));
        dashTimer = dashDuration;
        LastDashTime = Time.time;
        if (dashAnimator != null && dashAnimator.runtimeAnimatorController != null && !string.IsNullOrWhiteSpace(dashAnimationStateName))
            dashAnimator.Play(dashAnimationStateName, 0, 0f);
        OnDashPerformed?.Invoke();
        OnEvadePerformed?.Invoke();
        PlayGamepadRumble(dashRumbleLow, dashRumbleHigh, dashRumbleDuration);
        return true;
    }

    public bool TryEvade() => TryDash();

    public void SetBullChargeLock(bool active)
    {
        bool nextValue = active && !IsDead;
        if (bullChargeLocked == nextValue)
            return;

        SetBullChargeLockStateInternal(nextValue);
        if (bullChargeLocked)
        {
            isActing = false;
            dashTimer = 0f;
            dashVelocity = Vector3.zero;
            SetHoldingCloth(false);
            StopMovementImmediate();
            SetShooterControlEnabled(false);
            return;
        }

        RestoreGameplayControlsIfAvailable();
    }

    public void SetHoldingCloth(bool value)
    {
        bool nextValue = !IsDead && !IsGameplayControlLocked() && value;
        if (isHoldingCloth == nextValue)
            return;

        isHoldingCloth = nextValue;
        // Keep controller active during hold cloth, otherwise look input can get stuck
        // when rapidly toggling hold/release.
        SetShooterControlEnabled(shooterGameplayEnabled && !IsGameplayControlLocked());

        if (!isHoldingCloth)
            EnsureGameplayLookUnlocked();

        OnHoldingClothChanged?.Invoke(isHoldingCloth);
    }

    public void ForceStun()
    {
        TriggerStun();
    }

    public void GrantInvulnerability(float duration)
    {
        invulnerabilityTimer = Mathf.Max(invulnerabilityTimer, duration);
    }

    public void TakeDamage(float amount)
    {
        amount = Mathf.Max(0f, amount);
        if (amount <= 0f)
            return;

        if (IsDead)
            return;

        if (IsInvulnerable)
            return;

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        stunVfx?.TriggerDamageFlash();
        OnDamaged?.Invoke(amount);
        PlayGamepadRumble(damageRumbleLow, damageRumbleHigh, damageRumbleDuration);

        if (IsDead)
        {
            if (!deathEventRaised)
            {
                deathEventRaised = true;
                OnDeath?.Invoke();
            }

            ApplyDeathPresentation();
        }
    }

    public void TakeBullImpact(float amount, Vector3 impactSource)
    {
        float previousHealth = currentHealth;
        TakeDamage(amount);
        if (currentHealth >= previousHealth) return;
        ApplyKnockback(impactSource, knockbackDistance, knockbackDuration);
        stunVfx?.TriggerBullImpactBurst();
        ForceStun();
    }

    public void ForceEndingDeathPresentation()
    {
        ForceDeathStateInternal(logDebug: false);
    }

    public void ForceDeathForDebug()
    {
        ForceDeathStateInternal(logDebug: true);
    }

    public void RefillStaminaForDebug()
    {
        if (IsDead)
            return;

        currentStamina = Mathf.Clamp(maxStamina, 0f, maxStamina);
        UpdateUI();
        Debug.Log($"Debug shortcut: player stamina restored to {currentStamina}.");
    }

    public void AddStamina(float amount)
    {
        currentStamina = Mathf.Clamp(currentStamina + amount, 0f, maxStamina);
    }

    public void ResetCombatState(bool refillHealth = true, bool refillStamina = true)
    {
        CancelInvoke(nameof(EndActionLock));

        bool wasStunned = isStunned;

        invulnerabilityTimer = 0f;
        dashTimer = 0f;
        knockbackTimer = 0f;
        stunTimer = 0f;
        perfectDodgeBuffTimer = 0f;
        dashVelocity = Vector3.zero;
        knockbackVelocity = Vector3.zero;
        LastDashTime = -999f;
        isActing = false;
        isTaunting = false;
        isStunned = false;
        SetBullChargeLockStateInternal(false);

        if (refillHealth)
            currentHealth = maxHealth;

        if (refillStamina)
            currentStamina = maxStamina;

        deathEventRaised = false;

        SetHoldingCloth(false);
        ApplyPerfectDodgeBuffState(false);
        StopMovementImmediate();

        if (rigidBody != null)
        {
            rigidBody.velocity = Vector3.zero;
            rigidBody.angularVelocity = Vector3.zero;
        }

        CachePresentationReferences();
        if (cameraLook != null)
        {
            SyncCameraLookStateFromTransforms();
            cameraLook.enabled = true;
        }

        if (firstPersonCamera != null)
            firstPersonCamera.localRotation = GetGameplayCameraLocalRotation();

        deathPresentationApplied = false;
        deathPresentationTimer = 0f;
        clothCameraLockActive = false;
        SetShooterControlEnabled(shooterGameplayEnabled && !IsGameplayControlLocked());

        if (wasStunned)
            OnStunStateChanged?.Invoke(false);

        UpdateUI();
    }

    public void SetShooterGameplayEnabled(bool enabledState)
    {
        shooterGameplayEnabled = enabledState;
        SetShooterControlEnabled(enabledState && !IsGameplayControlLocked());
        if (enabledState && !IsGameplayControlLocked() && !isHoldingCloth)
            EnsureGameplayLookUnlocked();
    }

    public void SetMainMenuFrozen(bool frozen)
    {
        if (frozen)
        {
            shooterGameplayEnabled = false;
            StopMovementImmediate();
            SetShooterControlEnabled(false);
            ApplyPerfectDodgeBuffState(false);
            CacheMainMenuFrozenPose();
            mainMenuFrozen = true;
            return;
        }

        mainMenuFrozen = false;
        hasMainMenuFrozenPose = false;
        shooterGameplayEnabled = true;
        SetShooterControlEnabled(!IsGameplayControlLocked());
        ApplyPerfectDodgeBuffState(perfectDodgeBuffTimer > 0f);

        if (!IsGameplayControlLocked() && !isHoldingCloth)
            EnsureGameplayLookUnlocked();
    }

    private void CacheMainMenuFrozenPose()
    {
        CachePresentationReferences();

        mainMenuFrozenPosition = transform.position;
        mainMenuFrozenRotation = transform.rotation;
        mainMenuFrozenCameraRotation = firstPersonCamera != null ? firstPersonCamera.localRotation : Quaternion.identity;
        hasMainMenuFrozenPose = true;
    }

    private void EnforceMainMenuFrozenPose()
    {
        if (!hasMainMenuFrozenPose)
            CacheMainMenuFrozenPose();

        if (rigidBody != null)
        {
            rigidBody.velocity = Vector3.zero;
            rigidBody.angularVelocity = Vector3.zero;
        }

        transform.SetPositionAndRotation(mainMenuFrozenPosition, mainMenuFrozenRotation);

        if (firstPersonCamera != null)
            firstPersonCamera.localRotation = mainMenuFrozenCameraRotation;

        if (cameraLookRotationCameraField != null && cameraLook != null)
            cameraLookRotationCameraField.SetValue(cameraLook, mainMenuFrozenCameraRotation);

        if (cameraLookRotationCharacterField != null && cameraLook != null)
            cameraLookRotationCharacterField.SetValue(cameraLook, mainMenuFrozenRotation);
    }

    public void RewardPerfectDodge()
    {
        AddStamina(perfectDodgeStaminaRestore);
        perfectDodgeBuffTimer = Mathf.Max(perfectDodgeBuffTimer, perfectDodgeDuration);
        ApplyPerfectDodgeBuffState(perfectDodgeBuffTimer > 0f);
        PlayGamepadRumble(perfectRumbleLow, perfectRumbleHigh, perfectRumbleDuration);
    }

    public void PlayGamepadRumble(float lowMotor, float highMotor, float duration)
    {
        Gamepad gamepad = Gamepad.current;
        if (gamepad == null)
            return;

        rumbleLowMotor = Mathf.Max(rumbleLowMotor, Mathf.Clamp01(lowMotor));
        rumbleHighMotor = Mathf.Max(rumbleHighMotor, Mathf.Clamp01(highMotor));
        rumbleTimer = Mathf.Max(rumbleTimer, Mathf.Max(0.01f, duration));
        gamepad.SetMotorSpeeds(rumbleLowMotor, rumbleHighMotor);
    }

    private void UpdateStun()
    {
        if (!isStunned)
            return;

        StopMovementImmediate();
        stunTimer -= Time.deltaTime;
        if (stunTimer <= 0f)
        {
            isStunned = false;
            SetShooterControlEnabled(shooterGameplayEnabled && !bullChargeLocked);
            if (!bullChargeLocked)
                EnsureGameplayLookUnlocked();
            OnStunStateChanged?.Invoke(false);
        }
    }

    private void UpdateDash()
    {
        if (dashTimer <= 0f)
            return;

        Vector3 delta = dashVelocity * Time.deltaTime;
        if (rigidBody != null && !rigidBody.isKinematic)
            rigidBody.MovePosition(rigidBody.position + delta);
        else
            transform.position += delta;

        dashTimer -= Time.deltaTime;
        if (dashTimer > 0f)
            return;

        dashTimer = 0f;
        dashVelocity = Vector3.zero;
        isActing = false;
    }

    private void UpdateStamina()
    {
        if (IsDead)
            return;

        if (isHoldingCloth && !isActing)
            AddStamina(holdClothRecoveryPerSecond * Time.deltaTime);
    }

    private void UpdatePerfectDodgeBuff()
    {
        if (perfectDodgeBuffTimer <= 0f)
        {
            if (perfectDodgeBuffActive)
                ApplyPerfectDodgeBuffState(false);
            return;
        }

        perfectDodgeBuffTimer = Mathf.Max(0f, perfectDodgeBuffTimer - Time.deltaTime);
        ApplyPerfectDodgeBuffState(perfectDodgeBuffTimer > 0f);
    }

    private void UpdateInvulnerability()
    {
        if (invulnerabilityTimer <= 0f)
            return;

        invulnerabilityTimer -= Time.deltaTime;
    }

    private void UpdateKnockback()
    {
        if (knockbackTimer <= 0f)
            return;

        Vector3 delta = knockbackVelocity * Time.deltaTime;
        if (rigidBody != null && !rigidBody.isKinematic)
            rigidBody.MovePosition(rigidBody.position + delta);
        else
            transform.position += delta;

        knockbackTimer -= Time.deltaTime;
    }

    private void ApplyKnockback(Vector3 sourcePosition, float distance, float duration)
    {
        Vector3 direction = transform.position - sourcePosition;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = transform.forward.sqrMagnitude > 0.001f ? transform.forward : Vector3.back;

        direction.Normalize();
        knockbackVelocity = direction * (distance / Mathf.Max(0.01f, duration));
        knockbackTimer = duration;
    }

    private void TriggerStun()
    {
        currentStamina = 0f;
        isStunned = true;
        isActing = false;
        dashTimer = 0f;
        dashVelocity = Vector3.zero;
        SetHoldingCloth(false);
        stunTimer = stunDuration;
        StopMovementImmediate();
        SetShooterControlEnabled(false);
        OnStunStateChanged?.Invoke(true);
    }

    private void ApplyPerfectDodgeBuffState(bool active)
    {
        if (movementComponent == null)
            movementComponent = GetComponent<Movement>();

        if (movementComponent != null)
            movementComponent.SetExternalSpeedMultiplier(active ? perfectDodgeSpeedMultiplier : 1f);

        if (perfectDodgeBuffActive == active)
            return;

        perfectDodgeBuffActive = active;
        OnPerfectDodgeBuffStateChanged?.Invoke(active);
    }

    private void EndActionLock()
    {
        isActing = false;
    }
    private void UpdateUI()
    {
        if (healthBar != null)
            healthBar.value = HealthNormalized;

        if (staminaBar != null)
            staminaBar.value = StaminaNormalized;
    }

    private void UpdateHealthBar()
    {
        if (healthBar != null)
            healthBar.value = HealthNormalized;
    }

    private void ForceDeathStateInternal(bool logDebug)
    {
        bool wasDead = IsDead;
        bool wasStunned = isStunned;

        invulnerabilityTimer = 0f;
        currentHealth = 0f;
        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
        isActing = false;
        isStunned = false;
        SetBullChargeLockStateInternal(false);
        SetHoldingCloth(false);
        StopMovementImmediate();
        SetShooterControlEnabled(false);

        if (!wasDead)
            stunVfx?.TriggerDamageFlash();

        if (wasStunned)
            OnStunStateChanged?.Invoke(false);

        OnDamaged?.Invoke(maxHealth);
        ApplyDeathPresentation();
        UpdateUI();

        if (logDebug)
            Debug.Log("Debug shortcut: player health forced to 0.");
    }

    private void SetBullChargeLockStateInternal(bool nextValue)
    {
        if (bullChargeLocked == nextValue)
            return;

        bullChargeLocked = nextValue;
        OnBullChargeLockStateChanged?.Invoke(nextValue);
    }

    private void CachePresentationReferences()
    {
        if (firstPersonRoot == null)
            firstPersonRoot = transform.Find("SK_FP_CH_Default_Root");

        if (firstPersonCamera == null && firstPersonRoot != null)
        {
            Transform socketCamera = firstPersonRoot.Find("Armature/root/pelvis/spine_01/spine_02/spine_03/neck_01/head/SOCKET_Camera/Camera");
            if (socketCamera != null)
            {
                firstPersonCamera = socketCamera;
                cachedCameraLocalRotation = GetGameplayCameraLocalRotation();
            }
        }

        if (gameplayCamera == null)
            gameplayCamera = shooterCharacter != null ? shooterCharacter.GetCameraWorld() : null;

        if (gameplayCamera == null && firstPersonCamera != null)
            gameplayCamera = firstPersonCamera.GetComponent<Camera>();

        if (cameraLook == null && firstPersonRoot != null)
            cameraLook = firstPersonRoot.GetComponent<CameraLook>();

        if (cameraLook != null)
        {
            CacheCameraLookReflection();
            ApplyLookSensitivityIfNeeded();
        }
    }

    private void StabilizeFirstPersonPresentation()
    {
        if (firstPersonRoot != null)
        {
            firstPersonRoot.localPosition = new Vector3(0f, 1.8f, 0f);
            firstPersonRoot.localRotation = Quaternion.identity;
        }

        if (gameplayCamera != null)
        {
            if (!cachedGameplayNearClip)
            {
                cachedGameplayNearClipPlane = gameplayCamera.nearClipPlane;
                cachedGameplayNearClip = true;
            }

            gameplayCamera.nearClipPlane = Mathf.Max(gameplayCamera.nearClipPlane, gameplayNearClipPlane);
        }

        if (IsDead)
            ApplyDeathPresentation();
    }

    private void ApplyDeathPresentation()
    {
        if (deathPresentationApplied)
            return;

        CachePresentationReferences();
        if (gameFlow == null)
            gameFlow = BullfightSceneCache.FindObject<BullfightGameFlow>();

        handAnimatorController ??= GetComponent<BullfightHandAnimatorController>();
        handAnimatorController?.ForceDeathAnimation();

        if (cameraLook != null)
            cameraLook.enabled = false;

        if (firstPersonCamera != null)
            deathPresentationStartRotation = firstPersonCamera.localRotation;

        bool phaseTwoDeath = gameFlow != null && gameFlow.currentPhase == BullfightGameFlow.GamePhase.PhaseTwo;
        deathPresentationTargetRotation = cachedCameraLocalRotation * Quaternion.Euler(deathCameraEuler);
        deathPresentationTurnDelay = phaseTwoDeath ? Mathf.Max(0f, phaseTwoDeathCameraTurnDelay) : 0f;
        deathPresentationTimer = 0f;
        deathPresentationApplied = true;
    }

    private void SetShooterControlEnabled(bool enabledState)
    {
        if (shooterCharacter != null)
            shooterCharacter.enabled = enabledState;
    }

    private void StopMovementImmediate()
    {
        if (playerController == null)
            playerController = GetComponent<BullfightPlayerController>();

        playerController?.ForceStopMovement();

        if (rigidBody != null)
        {
            Vector3 velocity = rigidBody.velocity;
            velocity.x = 0f;
            velocity.z = 0f;
            rigidBody.velocity = velocity;
        }
    }

    public bool TryGetFirstPersonCameraPoint(out Vector3 cameraPoint)
    {
        CachePresentationReferences();

        Transform reference = gameplayCamera != null ? gameplayCamera.transform : firstPersonCamera;
        if (reference == null)
        {
            cameraPoint = Vector3.zero;
            return false;
        }

        cameraPoint = reference.position;
        return true;
    }

    private Vector3 ResolveDashDirection()
    {
        Vector2 movementInput = shooterCharacter != null
            ? shooterCharacter.GetInputMovement()
            : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        Transform reference = gameplayCamera != null ? gameplayCamera.transform : transform;
        Vector3 forward = reference.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
            forward = transform.forward;
        forward.Normalize();

        Vector3 right = reference.right;
        right.y = 0f;
        if (right.sqrMagnitude <= 0.0001f)
            right = transform.right;
        right.Normalize();

        Vector3 dashDirection = (forward * movementInput.y) + (right * movementInput.x);
        dashDirection.y = 0f;
        if (dashDirection.sqrMagnitude > 0.0001f)
            return dashDirection.normalized;

        if (bullAI == null)
            bullAI = FindObjectOfType<BullAI>(true);

        if (bullAI != null)
        {
            Vector3 awayFromBull = transform.position - bullAI.transform.position;
            awayFromBull.y = 0f;
            if (awayFromBull.sqrMagnitude > 0.0001f)
                return awayFromBull.normalized;
        }

        return forward.sqrMagnitude > 0.0001f ? forward : Vector3.forward;
    }

    private void UpdateHoldingClothCameraLock()
    {
        CachePresentationReferences();

        bool shouldLock = !IsDead && !IsGameplayControlLocked() && isHoldingCloth;
        if (!shouldLock)
        {
            if (clothCameraLockActive)
                ReleaseHoldingClothCameraLock();
            return;
        }

        if (bullAI == null)
            bullAI = FindObjectOfType<BullAI>(true);

        if (bullAI == null)
            return;

        if (cameraLook != null && cameraLook.enabled)
            cameraLook.enabled = false;

        clothCameraLockActive = true;

        Vector3 toBull = bullAI.transform.position - transform.position;
        toBull.y = 0f;
        if (toBull.sqrMagnitude > 0.0001f)
        {
            Quaternion targetYaw = Quaternion.LookRotation(toBull.normalized, Vector3.up);
            Quaternion nextYaw = Quaternion.Slerp(transform.rotation, targetYaw, Time.deltaTime * holdingClothLockYawSpeed);
            if (rigidBody != null && !rigidBody.isKinematic)
                rigidBody.MoveRotation(nextYaw);
            else
                transform.rotation = nextYaw;
        }

        if (firstPersonCamera != null)
            firstPersonCamera.localRotation = GetGameplayCameraLocalRotation();
    }

    private void ReleaseHoldingClothCameraLock()
    {
        clothCameraLockActive = false;

        if (firstPersonCamera != null)
            firstPersonCamera.localRotation = GetGameplayCameraLocalRotation();

        if (cameraLook != null && !IsDead && !IsGameplayControlLocked())
        {
            SyncCameraLookStateFromTransforms();
            cameraLook.enabled = true;
        }
    }

    private void EnforceHorizontalOnlyView()
    {
        if (IsDead)
            return;

        CachePresentationReferences();

        if (firstPersonCamera != null)
            firstPersonCamera.localRotation = GetGameplayCameraLocalRotation();

        if (cameraLookRotationCameraField != null && cameraLook != null)
            cameraLookRotationCameraField.SetValue(cameraLook, GetGameplayCameraLocalRotation());

        if (!isHoldingCloth)
            EnsureGameplayLookUnlocked();
    }

    private void EnsureGameplayLookUnlocked()
    {
        if (cameraLook == null || IsDead || IsGameplayControlLocked() || !shooterGameplayEnabled)
            return;

        if (cameraLook.enabled)
            return;

        clothCameraLockActive = false;
        SyncCameraLookStateFromTransforms();
        cameraLook.enabled = true;
    }

    private void UpdateDeathPresentation()
    {
        if (!deathPresentationApplied || firstPersonCamera == null)
            return;

        deathPresentationTimer += Time.deltaTime;
        float progress = Mathf.Clamp01((deathPresentationTimer - deathPresentationTurnDelay) / Mathf.Max(0.01f, deathCameraTurnDuration));
        firstPersonCamera.localRotation = Quaternion.Slerp(deathPresentationStartRotation, deathPresentationTargetRotation, progress);
    }

    private void UpdateGamepadRumble()
    {
        if (rumbleTimer <= 0f)
            return;

        rumbleTimer = Mathf.Max(0f, rumbleTimer - Time.unscaledDeltaTime);
        if (rumbleTimer > 0f)
            return;

        rumbleLowMotor = 0f;
        rumbleHighMotor = 0f;
        Gamepad.current?.ResetHaptics();
    }

    private void CacheCameraLookReflection()
    {
        if (cameraLookSensitivityField == null)
            cameraLookSensitivityField = typeof(CameraLook).GetField("sensitivity", BindingFlags.Instance | BindingFlags.NonPublic);

        if (cameraLookRotationCameraField == null)
            cameraLookRotationCameraField = typeof(CameraLook).GetField("rotationCamera", BindingFlags.Instance | BindingFlags.NonPublic);

        if (cameraLookRotationCharacterField == null)
            cameraLookRotationCharacterField = typeof(CameraLook).GetField("rotationCharacter", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    private void ApplyLookSensitivityIfNeeded()
    {
        if (cameraLook == null || cameraLookSensitivityField == null)
            return;

        if (!hasCachedLookSensitivity)
        {
            cachedLookSensitivity = (Vector2)cameraLookSensitivityField.GetValue(cameraLook);
            hasCachedLookSensitivity = true;
        }

        cameraLookSensitivityField.SetValue(cameraLook, cachedLookSensitivity * Mathf.Max(0.05f, lookSensitivityMultiplier));

        cameraLookSensitivityField.SetValue(cameraLook, cachedLookSensitivity * Mathf.Max(0.05f, lookSensitivityMultiplier));
        lookSensitivityApplied = true;
    }

    private void SyncCameraLookStateFromTransforms()
    {
        CacheCameraLookReflection();
        if (cameraLook == null)
            return;

        if (cameraLookRotationCameraField != null && firstPersonCamera != null)
            cameraLookRotationCameraField.SetValue(cameraLook, GetGameplayCameraLocalRotation());

        if (cameraLookRotationCharacterField != null)
            cameraLookRotationCharacterField.SetValue(cameraLook, transform.localRotation);
    }

    private Quaternion GetGameplayCameraLocalRotation()
    {
        return Quaternion.Euler(gameplayViewEuler);
    }

    private void OnDisable()
    {
        Gamepad.current?.ResetHaptics();
    }

    private void OnDestroy()
    {
        Gamepad.current?.ResetHaptics();
    }
}



