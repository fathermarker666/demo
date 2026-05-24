using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using InfimaGames.LowPolyShooterPack;

public class BullfightPlayerController : MonoBehaviour
{
    [Header("References")]
    public PlayerStats playerStats;
    public Transform bullTarget;

    [Header("Input")]
    public KeyCode holdClothKey = KeyCode.C;
    public KeyCode capaKey = KeyCode.Space;
    public KeyCode attackKey = KeyCode.F;
    public KeyCode evadeKey = KeyCode.LeftControl;
    public KeyCode phaseTwoCalibrationKey = KeyCode.G;
    public KeyCode phaseTwoStabKey = KeyCode.E;

    [Header("Action Assets")]
    [SerializeField] private InputActionAsset bullfightActionsAsset;

    [Header("Buffer")]
    public float capaBufferDuration = 0.35f;
    public float attackBufferDuration = 0.15f;
    public float phaseTwoStabBufferDuration = 0.2f;
    [SerializeField] private float holdTriggerThreshold = 0.8f;

    [Header("Phase Two Sensor")]
    [SerializeField] private float phaseTwoForceCap = 50f;
    [SerializeField] private float phaseTwoStabThreshold = 35f;
    [SerializeField] private float phaseTwoStabReleaseThreshold = 20f;
    [SerializeField] private float phaseTwoWeakStabThreshold = 25f;
    [SerializeField] private float phaseTwoCalibrationStableThreshold = 2f;
    [SerializeField] private float phaseTwoSensorTimeout = 1.2f;

    private float capaBufferedUntil = -1f;
    private float attackBufferedUntil = -1f;
    private float phaseTwoStabBufferedUntil = -1f;
    private float phaseTwoWeakStabBufferedUntil = -1f;
    private float sensorPhaseTwoForce;
    private float sensorPhaseTwoCalibrationForce;
    private float sensorPhaseTwoPeakForce;
    private float sensorPhaseTwoForceUpdatedAt = -999f;
    private bool dashSuppressed;
    private Character shooterCharacter;
    private FieldInfo axisMovementField;
    private FieldInfo holdingButtonRunField;
    private BullfightGameFlow gameFlow;
    private ArduinoTest arduinoTest;
    private InputActionAsset runtimeBullfightActions;
    private InputAction holdAction;
    private InputAction swingAction;
    private InputAction attackAction;
    private InputAction dashAction;
    private InputAction phaseTwoCalibrationAction;
    private InputAction phaseTwoStabAction;
    private int sensorSwingFrame = -1;
    private int sensorPhaseTwoStabFrame = -1;
    private int sensorPhaseTwoWeakStabFrame = -1;
    private bool sensorPhaseTwoCalibrationHeld;
    private bool sensorPhaseTwoCalibrationReady;
    private bool sensorPhaseTwoAttemptActive;
    private bool sensorPhaseTwoStabLatched;
    
    private bool ultrasonicHoldActive;
    private float nextReferenceResolveAt = -1f;

    private void Awake()
    {
        ResolveReferencesIfNeeded(true);
        RefreshBullfightActions();
    }

    private void OnEnable()
    {
        RefreshBullfightActions();
    }

    private void OnDisable()
    {
        runtimeBullfightActions?.Disable();
    }

    private void OnDestroy()
    {
        if (runtimeBullfightActions != null)
            Destroy(runtimeBullfightActions);
    }

    private void Update()
    {
        if (HasMissingReferences())
            ResolveReferencesIfNeeded();

        if (IsPhaseTwoInputMode())
        {
            playerStats?.SetHoldingCloth(false);
            ForceStopMovement();
            UpdatePhaseTwoBufferedInputs();
            return;
        }

        UpdateHoldingCloth();
        if (playerStats != null && (playerStats.isStunned || playerStats.IsBullChargeLocked))
        {
            ForceStopMovement();
            return;
        }
        FreezeMovementWhileHoldingCloth();
        UpdateBufferedInputs();
        HandleEvade();
    }

    public bool ConsumeCapaPressed()
    {
        if (playerStats == null || !playerStats.isHoldingCloth)
        {
            capaBufferedUntil = -1f;
            return false;
        }

        if (Time.time > capaBufferedUntil)
            return false;

        capaBufferedUntil = -1f;
        return true;
    }

    public bool ConsumeAttackPressed()
    {
        if (playerStats != null && playerStats.isHoldingCloth)
        {
            attackBufferedUntil = -1f;
            return false;
        }

        if (Time.time > attackBufferedUntil)
            return false;

        attackBufferedUntil = -1f;
        return true;
    }

    public bool IsAttackPressedThisFrame()
    {
        return WasAttackPressedThisFrame();
    }

    public Transform GetBullTarget()
    {
        if (bullTarget == null)
            ResolveReferencesIfNeeded();

        return bullTarget != null ? bullTarget : transform;
    }

    public bool IsPhaseTwoCalibrationHeld()
    {
        return IsPhaseTwoInputMode() && IsPhaseTwoCalibrationPressed();
    }

    public float PhaseTwoForceCap => Mathf.Max(1f, phaseTwoForceCap);

    public float PhaseTwoCalibrationStableThreshold => Mathf.Max(0f, phaseTwoCalibrationStableThreshold);

    public float PhaseTwoStabThreshold => Mathf.Clamp(phaseTwoStabThreshold, 0f, PhaseTwoForceCap);

    public float PhaseTwoWeakStabThreshold => Mathf.Clamp(Mathf.Max(25f, phaseTwoWeakStabThreshold), 0f, PhaseTwoStabThreshold);

    public bool IsPhaseTwoSensorCalibrationReady() => sensorPhaseTwoCalibrationReady;

    public bool HasRecentPhaseTwoSensorReading()
    {
        return Time.unscaledTime - sensorPhaseTwoForceUpdatedAt <= Mathf.Max(0.05f, phaseTwoSensorTimeout);
    }

    public float GetPhaseTwoSensorForce()
    {
        return HasRecentPhaseTwoSensorReading() ? sensorPhaseTwoForce : 0f;
    }

    public float GetPhaseTwoSensorCalibrationForce()
    {
        return HasRecentPhaseTwoSensorReading() ? sensorPhaseTwoCalibrationForce : 0f;
    }

    public float GetPhaseTwoSensorForceNormalized()
    {
        return Mathf.Clamp01(GetPhaseTwoSensorForce() / PhaseTwoForceCap);
    }

    public bool IsPhaseTwoSensorStable()
    {
        return HasRecentPhaseTwoSensorReading() && Mathf.Abs(GetPhaseTwoSensorCalibrationForce()) <= PhaseTwoCalibrationStableThreshold;
    }

    public bool ConsumePhaseTwoStabPressed()
    {
        if (!IsPhaseTwoInputMode())
        {
            phaseTwoStabBufferedUntil = -1f;
            return false;
        }

        if (Time.time > phaseTwoStabBufferedUntil)
            return false;

        phaseTwoStabBufferedUntil = -1f;
        return true;
    }

    public bool IsPhaseTwoStabPressedThisFrame()
    {
        return IsPhaseTwoInputMode() && WasPhaseTwoStabPressedThisFrame();
    }

    public bool ConsumePhaseTwoWeakStabPressed()
    {
        if (!IsPhaseTwoInputMode())
        {
            phaseTwoWeakStabBufferedUntil = -1f;
            return false;
        }

        if (Time.time > phaseTwoWeakStabBufferedUntil)
            return false;

        phaseTwoWeakStabBufferedUntil = -1f;
        return true;
    }

    public void ClearInputBuffers()
    {
        capaBufferedUntil = -1f;
        attackBufferedUntil = -1f;
        phaseTwoStabBufferedUntil = -1f;
        phaseTwoWeakStabBufferedUntil = -1f;
        sensorSwingFrame = -1;
        sensorPhaseTwoStabFrame = -1;
        sensorPhaseTwoWeakStabFrame = -1;
    }

    public void ResetSensorDrivenInputs(bool clearUltrasonicHold = true)
    {
        sensorSwingFrame = -1;
        sensorPhaseTwoStabFrame = -1;
        sensorPhaseTwoWeakStabFrame = -1;
        sensorPhaseTwoCalibrationHeld = false;
        sensorPhaseTwoCalibrationReady = false;
        sensorPhaseTwoAttemptActive = false;
        sensorPhaseTwoStabLatched = false;
        sensorPhaseTwoForce = 0f;
        sensorPhaseTwoCalibrationForce = 0f;
        sensorPhaseTwoPeakForce = 0f;
        sensorPhaseTwoForceUpdatedAt = -999f;
        phaseTwoStabBufferedUntil = -1f;
        phaseTwoWeakStabBufferedUntil = -1f;

        if (clearUltrasonicHold)
            ultrasonicHoldActive = false;
    }

    public void ConfigureInputActions(InputActionAsset asset)
    {
        bullfightActionsAsset = asset;
        RefreshBullfightActions();
    }

    public void SetDashSuppressed(bool suppressed)
    {
        dashSuppressed = suppressed;
    }

    public bool IsDashSuppressed() => dashSuppressed;

    public void TriggerSensorSwing()
    {
        sensorSwingFrame = Time.frameCount;
    }

    

    public void SetPhaseTwoCalibrationSensorHeld(bool held)
    {
        sensorPhaseTwoCalibrationHeld = held;
    }

    public void SetUltrasonicHoldActive(bool active)
    {
        ultrasonicHoldActive = active;
    }

    public void SetPhaseTwoSensorCalibrationReady(bool ready)
    {
        sensorPhaseTwoCalibrationReady = ready;
        if (!ready)
        {
            sensorPhaseTwoForce = 0f;
            sensorPhaseTwoCalibrationForce = 0f;
            sensorPhaseTwoPeakForce = 0f;
            sensorPhaseTwoForceUpdatedAt = -999f;
            sensorPhaseTwoAttemptActive = false;
            sensorPhaseTwoStabLatched = false;
            sensorPhaseTwoStabFrame = -1;
            sensorPhaseTwoWeakStabFrame = -1;
        }
    }

    public void ResetPhaseTwoSensorState()
    {
        ResetSensorDrivenInputs(clearUltrasonicHold: false);
    }

    public void SetPhaseTwoSensorReading(float calibrationSignal, float force)
    {
        bool hadRecentReading = HasRecentPhaseTwoSensorReading();
        sensorPhaseTwoForce = Mathf.Clamp(force, 0f, PhaseTwoForceCap);
        float clampedCalibrationSignal = Mathf.Clamp(calibrationSignal, -PhaseTwoForceCap, PhaseTwoForceCap);
        sensorPhaseTwoCalibrationForce = hadRecentReading
            ? Mathf.Lerp(sensorPhaseTwoCalibrationForce, clampedCalibrationSignal, 0.35f)
            : clampedCalibrationSignal;
        sensorPhaseTwoForceUpdatedAt = Time.unscaledTime;

        float releaseThreshold = Mathf.Clamp(phaseTwoStabReleaseThreshold, 0f, PhaseTwoStabThreshold);
        float weakThreshold = Mathf.Clamp(PhaseTwoWeakStabThreshold, Mathf.Max(0.5f, PhaseTwoCalibrationStableThreshold + 0.5f), Mathf.Max(0.5f, PhaseTwoStabThreshold - 0.5f));

        if (!IsPhaseTwoInputMode() || !sensorPhaseTwoCalibrationReady)
            return;

        if (sensorPhaseTwoForce >= weakThreshold)
        {
            sensorPhaseTwoAttemptActive = true;
            sensorPhaseTwoPeakForce = Mathf.Max(sensorPhaseTwoPeakForce, sensorPhaseTwoForce);
        }

        if (!sensorPhaseTwoStabLatched && sensorPhaseTwoPeakForce >= PhaseTwoStabThreshold)
        {
            sensorPhaseTwoStabLatched = true;
            sensorPhaseTwoStabFrame = Time.frameCount;
        }

        if (sensorPhaseTwoForce > releaseThreshold)
            return;

        if (sensorPhaseTwoAttemptActive && !sensorPhaseTwoStabLatched && sensorPhaseTwoPeakForce >= weakThreshold)
            sensorPhaseTwoWeakStabFrame = Time.frameCount;

        sensorPhaseTwoAttemptActive = false;
        sensorPhaseTwoPeakForce = 0f;
        sensorPhaseTwoStabLatched = false;
    }

    public void SetPhaseTwoSensorForce(float force)
    {
        SetPhaseTwoSensorReading(force, Mathf.Abs(force));
    }

    public void TriggerPhaseTwoStab()
    {
        sensorPhaseTwoStabFrame = Time.frameCount;
    }

    public void ForceStopMovement()
    {
        if (HasMissingReferences())
            ResolveReferencesIfNeeded();

        if (shooterCharacter == null)
            return;

        axisMovementField?.SetValue(shooterCharacter, Vector2.zero);
        holdingButtonRunField?.SetValue(shooterCharacter, false);
    }

    public float GetMovementInputMagnitude()
    {
        if (shooterCharacter == null)
            ResolveReferencesIfNeeded();

        if (shooterCharacter != null)
            return shooterCharacter.GetInputMovement().magnitude;

        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).magnitude;
    }

    public float GetLookInputMagnitude()
    {
        if (shooterCharacter == null)
            ResolveReferencesIfNeeded();

        if (shooterCharacter != null)
            return shooterCharacter.GetInputLook().magnitude;

        return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")).magnitude;
    }

    public bool WasTutorialAdvancePressedThisFrame()
    {
        return WasAttackPressedThisFrame() ||
               (Keyboard.current != null &&
                (Keyboard.current.enterKey.wasPressedThisFrame ||
                 Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
                 Keyboard.current.spaceKey.wasPressedThisFrame)) ||
               (Gamepad.current != null &&
                (Gamepad.current.startButton.wasPressedThisFrame ||
                 Gamepad.current.buttonSouth.wasPressedThisFrame));
    }

    public bool IsSensorInputActive() =>
        arduinoTest != null && arduinoTest.ConnectionState == ArduinoTest.SensorConnectionState.Active;

    public string GetMoveDisplayLabel() => "\u5de6\u8611\u83c7\u982d";

    public string GetLookDisplayLabel() => "\u53f3\u8611\u83c7\u982d";

    public string GetHoldDisplayLabel()
    {
        string keyboardLabel = GetReadableKeyboardBindingLabel(holdAction, holdClothKey);
        return IsSensorInputActive()
            ? "\u96d9\u624b\u5f80\u524d\u4f38"
            : keyboardLabel;
    }

    public string GetSwingDisplayLabel()
    {
        string keyboardLabel = GetReadableKeyboardBindingLabel(swingAction, capaKey);
        return IsSensorInputActive()
            ? $"\u96d9\u624b\u5f80\u524d\u4f38\u5f8c\u6309\u4e0b {GetReadableGamepadBindingLabel(swingAction, "X")}"
            : keyboardLabel;
    }

    public string GetDashDisplayLabel() => GetReadableGamepadBindingLabel(dashAction, "Y");

    public string GetAttackDisplayLabel() => GetReadableGamepadBindingLabel(attackAction, "B");

    public string GetPhaseTwoCalibrationDisplayLabel()
    {
        string keyboardLabel = GetReadableKeyboardBindingLabel(phaseTwoCalibrationAction, phaseTwoCalibrationKey);
        return HasRecentPhaseTwoSensorReading()
            ? "\u4fdd\u6301\u528d\u4e0d\u52d5"
            : keyboardLabel;
    }

    public string GetPhaseTwoStabDisplayLabel()
    {
        string keyboardLabel = GetReadableKeyboardBindingLabel(phaseTwoStabAction, phaseTwoStabKey);
        return HasRecentPhaseTwoSensorReading()
            ? "\u63ee\u528d\u8d85\u904e\u529b\u9053\u9580\u6abb"
            : keyboardLabel;
    }

    private void UpdateHoldingCloth()
    {
        if (playerStats == null)
            return;

        playerStats.SetHoldingCloth(IsHoldPressed());
    }

    private void UpdateBufferedInputs()
    {
        if (WasSwingPressedThisFrame() && playerStats != null && playerStats.isHoldingCloth)
            capaBufferedUntil = Time.time + capaBufferDuration;

        if (WasAttackPressedThisFrame() && (playerStats == null || !playerStats.isHoldingCloth))
            attackBufferedUntil = Time.time + attackBufferDuration;
    }

    private void HandleEvade()
    {
        if (playerStats == null)
            return;

        if (dashSuppressed)
            return;

        if (WasDashPressedThisFrame())
            playerStats.TryEvade();
    }

    private void UpdatePhaseTwoBufferedInputs()
    {
        if (WasPhaseTwoStabPressedThisFrame())
            phaseTwoStabBufferedUntil = Time.time + phaseTwoStabBufferDuration;

        if (WasPhaseTwoWeakStabPressedThisFrame())
            phaseTwoWeakStabBufferedUntil = Time.time + phaseTwoStabBufferDuration;
    }

    private bool IsHoldPressed()
    {
        return AreHoldTriggersPressed() || IsActionPressed(holdAction) || Input.GetKey(holdClothKey) || ultrasonicHoldActive;
    }

    private bool WasSwingPressedThisFrame()
    {
        return WasSensorTriggeredThisFrame(sensorSwingFrame) || WasActionPressedThisFrame(swingAction) || Input.GetKeyDown(capaKey);
    }

    private bool WasAttackPressedThisFrame()
    {
        return WasActionPressedThisFrame(attackAction) || Input.GetKeyDown(attackKey);
    }

    private bool WasDashPressedThisFrame()
    {
        return WasActionPressedThisFrame(dashAction) || Input.GetKeyDown(evadeKey);
    }

    private bool IsPhaseTwoCalibrationPressed()
    {
        return sensorPhaseTwoCalibrationHeld || IsActionPressed(phaseTwoCalibrationAction) || Input.GetKey(phaseTwoCalibrationKey);
    }

    private bool WasPhaseTwoStabPressedThisFrame()
    {
        return WasSensorTriggeredThisFrame(sensorPhaseTwoStabFrame) || WasActionPressedThisFrame(phaseTwoStabAction) || Input.GetKeyDown(phaseTwoStabKey);
    }

    private bool WasPhaseTwoWeakStabPressedThisFrame()
    {
        return WasSensorTriggeredThisFrame(sensorPhaseTwoWeakStabFrame);
    }

    private bool WasSensorTriggeredThisFrame(int triggeredFrame)
    {
        return triggeredFrame == Time.frameCount;
    }

    private bool IsActionPressed(InputAction action)
    {
        return action != null && action.IsPressed();
    }

    private bool WasActionPressedThisFrame(InputAction action)
    {
        return action != null && action.WasPressedThisFrame();
    }

    private bool HasMissingReferences()
    {
        return playerStats == null || shooterCharacter == null || axisMovementField == null || holdingButtonRunField == null || gameFlow == null || arduinoTest == null;
    }

    private void ResolveReferencesIfNeeded(bool force = false)
    {
        if (!force && Time.unscaledTime < nextReferenceResolveAt)
            return;

        nextReferenceResolveAt = Time.unscaledTime + 0.5f;

        if (playerStats == null)
            playerStats = BullfightSceneCache.GetLocalOrScene<PlayerStats>(this);

        if (bullTarget == null)
            bullTarget = transform;

        if (shooterCharacter == null)
            shooterCharacter = GetComponent<Character>() ?? BullfightSceneCache.FindObject<Character>();

        if (axisMovementField == null)
            axisMovementField = typeof(Character).GetField("axisMovement", BindingFlags.Instance | BindingFlags.NonPublic);

        if (holdingButtonRunField == null)
            holdingButtonRunField = typeof(Character).GetField("holdingButtonRun", BindingFlags.Instance | BindingFlags.NonPublic);

        if (gameFlow == null)
            gameFlow = BullfightSceneCache.FindObject<BullfightGameFlow>();

        if (arduinoTest == null)
            arduinoTest = BullfightSceneCache.FindObject<ArduinoTest>();
    }

    private void RefreshBullfightActions()
    {
        if (!isActiveAndEnabled)
            return;

        if (bullfightActionsAsset == null)
            return;

        if (runtimeBullfightActions != null)
        {
            runtimeBullfightActions.Disable();
            Destroy(runtimeBullfightActions);
        }

        runtimeBullfightActions = Instantiate(bullfightActionsAsset);
        runtimeBullfightActions.Enable();

        holdAction = runtimeBullfightActions.FindAction("player/hold");
        swingAction = runtimeBullfightActions.FindAction("player/swing");
        attackAction = runtimeBullfightActions.FindAction("player/attack");
        dashAction = runtimeBullfightActions.FindAction("player/dash");
        phaseTwoCalibrationAction = runtimeBullfightActions.FindAction("player/phaseTwoCalibration");
        phaseTwoStabAction = runtimeBullfightActions.FindAction("player/phaseTwoStab");
    }

    private void FreezeMovementWhileHoldingCloth()
    {
        if (playerStats == null || !playerStats.isHoldingCloth)
            return;

        if (shooterCharacter == null || axisMovementField == null || holdingButtonRunField == null)
            ResolveReferencesIfNeeded();

        if (shooterCharacter == null)
            return;

        axisMovementField?.SetValue(shooterCharacter, Vector2.zero);
        holdingButtonRunField?.SetValue(shooterCharacter, false);
    }

    private bool IsPhaseTwoInputMode()
    {
        return gameFlow != null && gameFlow.currentPhase == BullfightGameFlow.GamePhase.PhaseTwo;
    }

    private bool AreHoldTriggersPressed()
    {
        Gamepad gamepad = Gamepad.current;
        if (gamepad == null)
            return false;

        return gamepad.leftTrigger.ReadValue() >= holdTriggerThreshold &&
               gamepad.rightTrigger.ReadValue() >= holdTriggerThreshold;
    }

    private static string GetReadableGamepadBindingLabel(InputAction action, string fallback)
    {
        if (action == null)
            return fallback;

        for (int index = 0; index < action.bindings.Count; index++)
        {
            InputBinding binding = action.bindings[index];
            if (binding.isComposite || binding.isPartOfComposite || string.IsNullOrWhiteSpace(binding.path))
                continue;

            if (!binding.path.Contains("<Gamepad>"))
                continue;

            if (binding.path.Contains("leftShoulder"))
                return "LB";
            if (binding.path.Contains("rightShoulder"))
                return "RB";
            if (binding.path.Contains("buttonWest"))
                return "X";
            if (binding.path.Contains("buttonEast"))
                return "B";
            if (binding.path.Contains("buttonNorth"))
                return "Y";
            if (binding.path.Contains("buttonSouth"))
                return "A";
            if (binding.path.Contains("leftStickPress"))
                return "L3";
            if (binding.path.Contains("rightStickPress"))
                return "R3";
        }

        return fallback;
    }

    private static string GetReadableKeyboardBindingLabel(InputAction action, KeyCode fallbackKey)
    {
        if (action != null)
        {
            for (int index = 0; index < action.bindings.Count; index++)
            {
                InputBinding binding = action.bindings[index];
                if (binding.isComposite || binding.isPartOfComposite || string.IsNullOrWhiteSpace(binding.path))
                    continue;

                if (!binding.path.Contains("<Keyboard>"))
                    continue;

                return GetReadableKeyboardPath(binding.path);
            }
        }

        return GetReadableKeyCodeLabel(fallbackKey);
    }

    private static string GetReadableKeyboardPath(string bindingPath)
    {
        if (string.IsNullOrWhiteSpace(bindingPath))
            return string.Empty;

        int slashIndex = bindingPath.LastIndexOf('/');
        string keyName = slashIndex >= 0 ? bindingPath.Substring(slashIndex + 1) : bindingPath;
        return keyName switch
        {
            "space" => "Space",
            "ctrl" => "LeftCtrl",
            "leftCtrl" => "LeftCtrl",
            "rightCtrl" => "RightCtrl",
            "leftShift" => "LeftShift",
            "rightShift" => "RightShift",
            "enter" => "Enter",
            "numpadEnter" => "NumpadEnter",
            "escape" => "Esc",
            _ when keyName.Length == 1 => keyName.ToUpperInvariant(),
            _ => keyName
        };
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
}






