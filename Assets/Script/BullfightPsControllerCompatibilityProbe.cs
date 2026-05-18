using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using InputAccelerometer = UnityEngine.InputSystem.Accelerometer;
using InputAttitudeSensor = UnityEngine.InputSystem.AttitudeSensor;
using InputGyroscope = UnityEngine.InputSystem.Gyroscope;
using InputLinearAccelerationSensor = UnityEngine.InputSystem.LinearAccelerationSensor;

public enum ProbeSourceMode
{
    ArduinoOnly,
    ObserveBoth,
    PsPrototypeOnly
}

[DisallowMultipleComponent]
public class BullfightPsControllerCompatibilityProbe : MonoBehaviour
{
    private const bool ProbeRuntimeEnabled = false;

    [Header("References")]
    [SerializeField] private BullfightPlayerController playerController;
    [SerializeField] private BullfightGameFlow gameFlow;
    [SerializeField] private ArduinoTest arduinoTest;

    [Header("Mode")]
    [SerializeField] private ProbeSourceMode sourceMode = ProbeSourceMode.ArduinoOnly;
    [SerializeField] private bool showOverlay = true;
    [SerializeField] private bool useRuntimeCanvasOverlay = true;

    [Header("Stillness Thresholds")]
    [SerializeField] private float stillnessEnterDuration = 0.18f;
    [SerializeField] private float stillnessExitGrace = 0.08f;
    [SerializeField] private float gyroStillThreshold = 0.35f;
    [SerializeField] private float accelStillThreshold = 0.18f;

    [Header("Gesture Thresholds")]
    [SerializeField] private float swingImpulseThreshold = 0.9f;
    [SerializeField] private float swingGyroThreshold = 1.4f;
    [SerializeField] private float stabImpulseThreshold = 1.25f;
    [SerializeField] private float gestureCooldown = 0.35f;
    [SerializeField] private float phaseTwoForceScale = 50f;

    [Header("Diagnostics")]
    [SerializeField] private float deviceRefreshInterval = 1f;

    private const float OverlayWidth = 520f;
    private const float OverlayPadding = 14f;
    private const float MotionUpdateEpsilon = 0.001f;
    private const float AttitudeUpdateDegrees = 0.1f;
    private const float NeutralNoiseRiskWindow = 1.5f;

    private readonly List<string> riskMessages = new List<string>(6);

    private Gamepad activeGamepad;
    private Joystick activeJoystick;
    private InputGyroscope activeGyroscope;
    private InputAccelerometer activeAccelerometer;
    private InputAttitudeSensor activeAttitudeSensor;
    private InputLinearAccelerationSensor activeLinearAccelerationSensor;

    private GUIStyle panelStyle;
    private GUIStyle labelStyle;
    private GUIStyle titleStyle;
    private Rect overlayRect;
    private Canvas runtimeOverlayCanvas;
    private RectTransform runtimeOverlayRoot;
    private Text runtimeOverlayText;
    private Text runtimeOverlayTitleText;
    private Text runtimeToastText;

    private string deviceInventorySummary = string.Empty;
    private string controllerSummary = "No gamepad detected.";
    private string compatibilityVerdict = "Controller Not Suitable";
    private string rumbleStatus = "Not tested";

    private Vector3 baselineAcceleration;
    private Vector3 rawGyro;
    private Vector3 rawAcceleration;
    private Vector3 rawLinearAcceleration;
    private Vector3 accelerationDelta;
    private Vector3 filteredAngularVelocity;
    private Vector3 filteredLinearImpulse;
    private Quaternion rawAttitude = Quaternion.identity;

    private float stillnessScore;
    private float stableStillnessTimer;
    private float unstableStillnessTimer;
    private float baselineOffsetMagnitude;
    private float mappedPhaseTwoForce;
    private float calibrationSignal;
    private float primaryForceSignalMagnitude;
    private float swingPeak;
    private float stabPeak;
    private float lastSwingTime = -999f;
    private float lastStabTime = -999f;
    private float nextDeviceRefreshAt = -1f;
    private float rumbleStopAt = -1f;
    private float neutralNoiseTimer;
    private float lastMotionUpdateAt = -999f;
    private float nextMotionConsoleLogAt = -1f;
    private float toastHideAt = -999f;

    private bool hasObservedMotionSignal;
    private bool hasMotionOwnershipRisk;
    private bool motionNoiseRiskLatched;
    private bool prototypeHoldActive;
    private bool prototypeCalibrationHeld;
    private bool prototypeCalibrationReady;
    private bool managedArduinoDisable;
    private bool syncedForceScale;
    private bool isPlayStationController;
    private bool isDualShockLayout;
    private bool isDualSenseLayout;
    private bool looksLikeGenericOrXInput;
    private bool hasRumbleCommandSupport;

    private ProbeSourceMode appliedSourceMode = (ProbeSourceMode)(-1);
    private string lastLoggedVerdict = string.Empty;

    public ProbeSourceMode SourceMode => sourceMode;

    private bool HasMotionSensorDevice =>
        activeGyroscope != null ||
        activeAccelerometer != null ||
        activeLinearAccelerationSensor != null ||
        activeAttitudeSensor != null;

    private bool HasReadableMotionStream =>
        activeGyroscope != null ||
        activeAccelerometer != null ||
        activeLinearAccelerationSensor != null;

    private bool HasValidController => activeGamepad != null && isPlayStationController && !looksLikeGenericOrXInput;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CleanupDormantProbeArtifacts()
    {
        DestroyOrphanedOverlayCanvas();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapProbeAfterSceneLoad()
    {
        if (!ProbeRuntimeEnabled)
            return;

        if (FindObjectOfType<BullfightPsControllerCompatibilityProbe>() != null)
            return;

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
            return;

        GameObject host = GameObject.Find("SerialManager");
        if (host == null)
            return;

        host.AddComponent<BullfightPsControllerCompatibilityProbe>();
        Debug.Log("[PS Probe] Auto-attached probe to SerialManager after scene load.");
    }

    private void Reset()
    {
        arduinoTest = GetComponent<ArduinoTest>();
        ResolveReferences(force: true);
    }

    private void Awake()
    {
        if (!ProbeRuntimeEnabled)
        {
            DeactivateRuntimeArtifacts();
            enabled = false;
            return;
        }

        overlayRect = new Rect(16f, 16f, OverlayWidth, 0f);
        ResolveReferences(force: true);
        SyncForceScaleWithController();
        EnsureRuntimeOverlay();
        SetOverlayVisibility(showOverlay);
    }

    private void OnEnable()
    {
        if (!ProbeRuntimeEnabled)
        {
            DeactivateRuntimeArtifacts();
            enabled = false;
            return;
        }

        InputSystem.onDeviceChange += HandleDeviceChange;
        ResolveReferences(force: true);
        RefreshDevices(logInventory: true);
        CaptureBaseline(logToConsole: true);
        ApplySourceMode(force: true);
        LogCapabilityReport("startup");
        ShowToast("PS probe live. F10 overlay, F11 source mode.");
    }

    private void OnDisable()
    {
        InputSystem.onDeviceChange -= HandleDeviceChange;
        StopRumble();
        RestoreArduinoIfManaged();
        ClearPrototypeInjectionState();
    }

    private void Update()
    {
        if (!ProbeRuntimeEnabled)
        {
            DeactivateRuntimeArtifacts();
            enabled = false;
            return;
        }

        ResolveReferences(force: false);
        SyncForceScaleWithController();
        EnsureRuntimeOverlay();

        if (Time.unscaledTime >= nextDeviceRefreshAt)
            RefreshDevices(logInventory: false);

        HandleHotkeys();
        UpdateRumbleLifetime();
        UpdateMotionDiagnostics();
        ApplySourceMode(force: false);

        if (sourceMode == ProbeSourceMode.PsPrototypeOnly)
            UpdatePrototypeInjection();

        EvaluateCompatibilityVerdict();
        UpdateRiskMessages();
        UpdateRuntimeOverlay();
        LogVerdictIfChanged();
    }

    private void OnGUI()
    {
        if (!ProbeRuntimeEnabled)
            return;

        if (runtimeOverlayText != null)
            return;

        if (!showOverlay)
            return;

        EnsureGuiStyles();

        string overlayText = BuildOverlayText();
        float textHeight = labelStyle.CalcHeight(new GUIContent(overlayText), OverlayWidth - (OverlayPadding * 2f));
        overlayRect.height = Mathf.Max(360f, textHeight + 70f);

        GUI.Box(overlayRect, GUIContent.none, panelStyle);

        Rect titleRect = new Rect(overlayRect.x + OverlayPadding, overlayRect.y + OverlayPadding, overlayRect.width - (OverlayPadding * 2f), 24f);
        GUI.Label(titleRect, "PS Controller Compatibility Probe", titleStyle);

        Rect bodyRect = new Rect(
            overlayRect.x + OverlayPadding,
            overlayRect.y + 42f,
            overlayRect.width - (OverlayPadding * 2f),
            overlayRect.height - 54f);

        GUI.Label(bodyRect, overlayText, labelStyle);
    }

    private void DeactivateRuntimeArtifacts()
    {
        if (runtimeOverlayCanvas != null)
        {
            Destroy(runtimeOverlayCanvas.gameObject);
            runtimeOverlayCanvas = null;
        }

        runtimeOverlayRoot = null;
        runtimeOverlayText = null;
        runtimeOverlayTitleText = null;
        runtimeToastText = null;

        DestroyOrphanedOverlayCanvas();
    }

    private static void DestroyOrphanedOverlayCanvas()
    {
        GameObject orphanedCanvas = GameObject.Find("PSProbeOverlayCanvas");
        if (orphanedCanvas != null)
            Destroy(orphanedCanvas);
    }

    private void HandleDeviceChange(InputDevice device, InputDeviceChange change)
    {
        switch (change)
        {
            case InputDeviceChange.Added:
            case InputDeviceChange.Removed:
            case InputDeviceChange.Reconnected:
            case InputDeviceChange.Disconnected:
            case InputDeviceChange.Enabled:
            case InputDeviceChange.Disabled:
            case InputDeviceChange.ConfigurationChanged:
                RefreshDevices(logInventory: true);
                if (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected)
                    ClearPrototypeInjectionState();
                LogCapabilityReport($"device {change}");
                ShowToast($"Device {change}: {device.displayName}");
                break;
        }
    }

    private void ResolveReferences(bool force)
    {
        if (force || playerController == null)
            playerController = FindObjectOfType<BullfightPlayerController>();

        if (force || gameFlow == null)
            gameFlow = FindObjectOfType<BullfightGameFlow>();

        if (force || arduinoTest == null)
            arduinoTest = GetComponent<ArduinoTest>();
    }

    private void SyncForceScaleWithController()
    {
        if (syncedForceScale || playerController == null)
            return;

        phaseTwoForceScale = Mathf.Max(1f, playerController.PhaseTwoForceCap);
        syncedForceScale = true;
    }

    private void RefreshDevices(bool logInventory)
    {
        activeGamepad = Gamepad.current;
        activeJoystick = Joystick.current;
        activeGyroscope = InputGyroscope.current;
        activeAccelerometer = InputAccelerometer.current;
        activeAttitudeSensor = InputAttitudeSensor.current;
        activeLinearAccelerationSensor = InputLinearAccelerationSensor.current;
        nextDeviceRefreshAt = Time.unscaledTime + Mathf.Max(0.25f, deviceRefreshInterval);

        EnableDeviceIfPresent(activeGyroscope);
        EnableDeviceIfPresent(activeAccelerometer);
        EnableDeviceIfPresent(activeAttitudeSensor);
        EnableDeviceIfPresent(activeLinearAccelerationSensor);

        EvaluateControllerIdentity();
        hasMotionOwnershipRisk = EvaluateMotionOwnershipRisk();

        if (logInventory)
        {
            deviceInventorySummary = BuildDeviceInventorySummary();
            Debug.Log($"[PS Probe] InputSystem device scan\n{deviceInventorySummary}");
        }
    }

    private void EnableDeviceIfPresent(InputDevice device)
    {
        if (device == null || device.enabled)
            return;

        InputSystem.EnableDevice(device);
    }

    private void EvaluateControllerIdentity()
    {
        isPlayStationController = false;
        isDualShockLayout = false;
        isDualSenseLayout = false;
        looksLikeGenericOrXInput = false;
        hasRumbleCommandSupport = false;

        if (activeGamepad == null)
        {
            controllerSummary = activeJoystick != null
                ? $"No Gamepad.current. Joystick.current = {DescribeDevice(activeJoystick)}"
                : "No gamepad detected.";
            return;
        }

        InputDeviceDescription description = activeGamepad.description;
        string fullIdentity = BuildIdentityBlob(activeGamepad);
        string lowerIdentity = fullIdentity.ToLowerInvariant();

        isDualShockLayout = activeGamepad is DualShockGamepad ||
                            InputSystem.IsFirstLayoutBasedOnSecond(activeGamepad.layout, "DualShockGamepad");
        isDualSenseLayout = activeGamepad is DualSenseGamepadHID ||
                            activeGamepad.GetType().Name.Contains("DualSense") ||
                            activeGamepad.layout.Contains("DualSense");
        isPlayStationController = isDualShockLayout ||
                                  isDualSenseLayout ||
                                  lowerIdentity.Contains("sony") ||
                                  lowerIdentity.Contains("playstation") ||
                                  lowerIdentity.Contains("dualshock") ||
                                  lowerIdentity.Contains("dualsense") ||
                                  lowerIdentity.Contains("wireless controller");

        looksLikeGenericOrXInput = lowerIdentity.Contains("xinput") ||
                                   lowerIdentity.Contains("xbox") ||
                                   (!isPlayStationController && activeGamepad.layout == "Gamepad");

        hasRumbleCommandSupport = activeGamepad is DualShockGamepad || !looksLikeGenericOrXInput;

        controllerSummary =
            $"displayName={activeGamepad.displayName}, product={description.product}, manufacturer={description.manufacturer}, layout={activeGamepad.layout}, type={activeGamepad.GetType().Name}";
    }

    private bool EvaluateMotionOwnershipRisk()
    {
        if (!HasMotionSensorDevice || activeGamepad == null)
            return false;

        bool gyroMatches = SensorLooksPlayStationDerived(activeGyroscope);
        bool accelMatches = SensorLooksPlayStationDerived(activeAccelerometer);
        bool linearMatches = SensorLooksPlayStationDerived(activeLinearAccelerationSensor);
        bool attitudeMatches = SensorLooksPlayStationDerived(activeAttitudeSensor);

        return !(gyroMatches || accelMatches || linearMatches || attitudeMatches);
    }

    private bool SensorLooksPlayStationDerived(InputDevice sensor)
    {
        if (sensor == null)
            return false;

        string identity = BuildIdentityBlob(sensor).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(identity))
            return false;

        return identity.Contains("sony") ||
               identity.Contains("playstation") ||
               identity.Contains("dualshock") ||
               identity.Contains("dualsense") ||
               identity.Contains("wireless controller");
    }

    private string BuildDeviceInventorySummary()
    {
        StringBuilder builder = new StringBuilder(512);

        foreach (InputDevice device in InputSystem.devices)
            builder.AppendLine($"- id={device.deviceId}, enabled={device.enabled}, {DescribeDevice(device)}");

        builder.AppendLine($"Gamepad.current: {(activeGamepad != null ? DescribeDevice(activeGamepad) : "null")}");
        builder.AppendLine($"Joystick.current: {(activeJoystick != null ? DescribeDevice(activeJoystick) : "null")}");
        builder.AppendLine($"Gyroscope.current: {(activeGyroscope != null ? DescribeDevice(activeGyroscope) : "null")}");
        builder.AppendLine($"Accelerometer.current: {(activeAccelerometer != null ? DescribeDevice(activeAccelerometer) : "null")}");
        builder.AppendLine($"AttitudeSensor.current: {(activeAttitudeSensor != null ? DescribeDevice(activeAttitudeSensor) : "null")}");
        builder.AppendLine($"LinearAccelerationSensor.current: {(activeLinearAccelerationSensor != null ? DescribeDevice(activeLinearAccelerationSensor) : "null")}");

        return builder.ToString().TrimEnd();
    }

    private string DescribeDevice(InputDevice device)
    {
        if (device == null)
            return "null";

        InputDeviceDescription description = device.description;
        return $"name={device.displayName}, layout={device.layout}, type={device.GetType().Name}, product={description.product}, manufacturer={description.manufacturer}, interface={description.interfaceName}";
    }

    private string BuildIdentityBlob(InputDevice device)
    {
        if (device == null)
            return string.Empty;

        InputDeviceDescription description = device.description;
        return $"{device.displayName} {device.layout} {device.GetType().Name} {description.product} {description.manufacturer} {description.interfaceName}";
    }

    private void HandleHotkeys()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.f8Key.wasPressedThisFrame)
            RunRumbleTest();

        if (keyboard.f9Key.wasPressedThisFrame)
            CaptureBaseline(logToConsole: true);

        if (keyboard.f10Key.wasPressedThisFrame)
            SetOverlayVisibility(!showOverlay);

        if (keyboard.f11Key.wasPressedThisFrame)
            CycleSourceMode();
    }

    private void RunRumbleTest()
    {
        if (activeGamepad == null)
        {
            rumbleStatus = "Failed: no Gamepad.current.";
            Debug.LogWarning("[PS Probe] Rumble test skipped: no Gamepad.current.");
            ShowToast(rumbleStatus);
            return;
        }

        if (!hasRumbleCommandSupport)
        {
            rumbleStatus = "Failed: layout/OS rumble support not trusted for this device.";
            Debug.LogWarning($"[PS Probe] Rumble test blocked: layout/OS support is not trusted for {DescribeDevice(activeGamepad)}");
            ShowToast(rumbleStatus);
            return;
        }

        try
        {
            activeGamepad.SetMotorSpeeds(0.35f, 0.8f);
            rumbleStopAt = Time.unscaledTime + 0.25f;
            rumbleStatus = "Command sent. Physical feedback still depends on the current Windows/Bluetooth driver path.";
            Debug.Log($"[PS Probe] Rumble command sent to {DescribeDevice(activeGamepad)}");
            ShowToast("Rumble command sent.");
        }
        catch (System.Exception exception)
        {
            rumbleStatus = $"Failed: {exception.GetType().Name}";
            Debug.LogWarning($"[PS Probe] Rumble test failed for {DescribeDevice(activeGamepad)}. Reason: {exception.Message}");
            ShowToast(rumbleStatus);
        }
    }

    private void UpdateRumbleLifetime()
    {
        if (rumbleStopAt < 0f || Time.unscaledTime < rumbleStopAt)
            return;

        StopRumble();
    }

    private void StopRumble()
    {
        if (activeGamepad != null)
        {
            try
            {
                activeGamepad.SetMotorSpeeds(0f, 0f);
                activeGamepad.PauseHaptics();
            }
            catch
            {
            }
        }

        rumbleStopAt = -1f;
    }

    private void CaptureBaseline(bool logToConsole)
    {
        baselineAcceleration = activeAccelerometer != null ? activeAccelerometer.acceleration.ReadValue() : Vector3.zero;
        filteredAngularVelocity = Vector3.zero;
        filteredLinearImpulse = Vector3.zero;
        stableStillnessTimer = 0f;
        unstableStillnessTimer = 0f;
        stillnessScore = 0f;
        neutralNoiseTimer = 0f;
        motionNoiseRiskLatched = false;

        if (logToConsole)
            Debug.Log($"[PS Probe] Motion baseline captured. Accelerometer baseline={baselineAcceleration}");

        ShowToast("Motion baseline reset.");
    }

    private void UpdateMotionDiagnostics()
    {
        if (!HasMotionSensorDevice)
        {
            rawGyro = Vector3.zero;
            rawAcceleration = Vector3.zero;
            rawLinearAcceleration = Vector3.zero;
            accelerationDelta = Vector3.zero;
            filteredAngularVelocity = Vector3.zero;
            filteredLinearImpulse = Vector3.zero;
            baselineOffsetMagnitude = 0f;
            calibrationSignal = 0f;
            mappedPhaseTwoForce = 0f;
            primaryForceSignalMagnitude = 0f;
            stillnessScore = Mathf.MoveTowards(stillnessScore, 0f, Time.unscaledDeltaTime * 4f);
            stableStillnessTimer = 0f;
            unstableStillnessTimer = 0f;
            return;
        }

        float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);

        rawGyro = activeGyroscope != null ? activeGyroscope.angularVelocity.ReadValue() : Vector3.zero;
        rawAcceleration = activeAccelerometer != null ? activeAccelerometer.acceleration.ReadValue() : Vector3.zero;
        rawLinearAcceleration = activeLinearAccelerationSensor != null ? activeLinearAccelerationSensor.acceleration.ReadValue() : Vector3.zero;
        rawAttitude = activeAttitudeSensor != null ? activeAttitudeSensor.attitude.ReadValue() : Quaternion.identity;

        accelerationDelta = activeLinearAccelerationSensor != null
            ? rawLinearAcceleration
            : (activeAccelerometer != null ? rawAcceleration - baselineAcceleration : Vector3.zero);

        filteredAngularVelocity = Vector3.Lerp(filteredAngularVelocity, rawGyro, 1f - Mathf.Exp(-10f * dt));
        filteredLinearImpulse = Vector3.Lerp(filteredLinearImpulse, accelerationDelta, 1f - Mathf.Exp(-14f * dt));

        float rawGyroMagnitude = filteredAngularVelocity.magnitude;
        float rawImpulseMagnitude = filteredLinearImpulse.magnitude;
        bool hasAccelerationSignal = activeLinearAccelerationSensor != null || activeAccelerometer != null;
        baselineOffsetMagnitude = accelerationDelta.magnitude;
        primaryForceSignalMagnitude = hasAccelerationSignal ? rawImpulseMagnitude : rawGyroMagnitude;
        calibrationSignal = hasAccelerationSignal ? baselineOffsetMagnitude : rawGyroMagnitude;

        swingPeak = Mathf.Max(rawGyroMagnitude, rawImpulseMagnitude, swingPeak * 0.92f);
        stabPeak = Mathf.Max(primaryForceSignalMagnitude, stabPeak * 0.92f);

        bool motionUpdated =
            rawGyroMagnitude > MotionUpdateEpsilon ||
            rawImpulseMagnitude > MotionUpdateEpsilon ||
            Quaternion.Angle(rawAttitude, Quaternion.identity) > AttitudeUpdateDegrees;

        if (motionUpdated)
        {
            lastMotionUpdateAt = Time.unscaledTime;
            hasObservedMotionSignal = true;
            MaybeLogMotionActivity(rawGyroMagnitude, rawImpulseMagnitude);
        }

        bool gyroStable = activeGyroscope == null || rawGyroMagnitude <= gyroStillThreshold;
        bool accelStable = !hasAccelerationSignal || baselineOffsetMagnitude <= accelStillThreshold;
        bool isStable = gyroStable && accelStable;

        if (isStable)
        {
            stableStillnessTimer += dt;
            unstableStillnessTimer = 0f;
        }
        else
        {
            unstableStillnessTimer += dt;
            stableStillnessTimer = 0f;
        }

        float targetStillness = isStable ? 1f : 0f;
        float riseRate = stillnessEnterDuration > 0.0001f ? dt / stillnessEnterDuration : 1f;
        float fallRate = stillnessExitGrace > 0.0001f ? dt / stillnessExitGrace : 1f;
        stillnessScore = Mathf.MoveTowards(stillnessScore, targetStillness, isStable ? riseRate : fallRate);

        mappedPhaseTwoForce = MapImpulseToPhaseTwoForce(primaryForceSignalMagnitude);

        if (sourceMode == ProbeSourceMode.PsPrototypeOnly && IsGamepadNeutral())
        {
            if (!isStable)
            {
                neutralNoiseTimer += dt;
                if (neutralNoiseTimer >= NeutralNoiseRiskWindow)
                    motionNoiseRiskLatched = true;
            }
            else
            {
                neutralNoiseTimer = 0f;
            }
        }
        else
        {
            neutralNoiseTimer = 0f;
        }
    }

    private void MaybeLogMotionActivity(float gyroMagnitude, float impulseMagnitude)
    {
        if (!HasReadableMotionStream)
            return;

        float now = Time.unscaledTime;
        if (now < nextMotionConsoleLogAt)
            return;

        bool meaningfulMotion = gyroMagnitude >= 0.2f ||
                                impulseMagnitude >= 0.08f ||
                                baselineOffsetMagnitude >= 0.08f;
        if (!meaningfulMotion)
            return;

        nextMotionConsoleLogAt = now + 0.75f;
        Debug.Log(
            $"[PS Probe] Motion activity detected. gyro={gyroMagnitude:F3}, impulse={impulseMagnitude:F3}, baselineOffset={baselineOffsetMagnitude:F3}, linear={FormatVector3(rawLinearAcceleration)}, accel={FormatVector3(rawAcceleration)}"
        );
    }

    private float MapImpulseToPhaseTwoForce(float impulseMagnitude)
    {
        if (playerController == null)
            return Mathf.Max(0f, impulseMagnitude * phaseTwoForceScale);

        float maxForce = Mathf.Clamp(phaseTwoForceScale, 1f, playerController.PhaseTwoForceCap);
        return Mathf.Clamp01(impulseMagnitude / Mathf.Max(0.01f, stabImpulseThreshold)) * maxForce;
    }

    private void UpdatePrototypeInjection()
    {
        if (playerController == null || gameFlow == null || !HasValidController || !HasReadableMotionStream)
        {
            ClearPrototypeInjectionState();
            return;
        }

        bool inPhaseTwo = gameFlow.currentPhase == BullfightGameFlow.GamePhase.PhaseTwo;

        if (inPhaseTwo)
            UpdatePhaseTwoPrototype();
        else
            UpdatePhaseOnePrototype();
    }

    private void UpdatePhaseOnePrototype()
    {
        if (prototypeCalibrationHeld || prototypeCalibrationReady || mappedPhaseTwoForce > 0f)
        {
            prototypeCalibrationHeld = false;
            prototypeCalibrationReady = false;
            playerController.SetPhaseTwoCalibrationSensorHeld(false);
            playerController.SetPhaseTwoSensorReading(0f, 0f);
            playerController.SetPhaseTwoSensorCalibrationReady(false);
        }

        bool shouldHold = stableStillnessTimer >= stillnessEnterDuration;

        if (!prototypeHoldActive && shouldHold)
        {
            prototypeHoldActive = true;
            playerController.SetUltrasonicHoldActive(true);
        }
        else if (prototypeHoldActive && unstableStillnessTimer > stillnessExitGrace)
        {
            prototypeHoldActive = false;
            playerController.SetUltrasonicHoldActive(false);
        }

        if (!prototypeHoldActive)
            return;

        float now = Time.unscaledTime;
        bool swingTriggered = filteredAngularVelocity.magnitude >= swingGyroThreshold ||
                              filteredLinearImpulse.magnitude >= swingImpulseThreshold;

        if (!swingTriggered || now - lastSwingTime < gestureCooldown)
            return;

        lastSwingTime = now;
        playerController.TriggerSensorSwing();
    }

    private void UpdatePhaseTwoPrototype()
    {
        if (prototypeHoldActive)
        {
            prototypeHoldActive = false;
            playerController.SetUltrasonicHoldActive(false);
        }

        bool shouldHoldCalibration = stableStillnessTimer >= stillnessEnterDuration;
        if (prototypeCalibrationHeld != shouldHoldCalibration)
        {
            prototypeCalibrationHeld = shouldHoldCalibration;
            playerController.SetPhaseTwoCalibrationSensorHeld(shouldHoldCalibration);
        }

        bool shouldReady = shouldHoldCalibration &&
                           (activeLinearAccelerationSensor != null || activeAccelerometer != null
                               ? baselineOffsetMagnitude <= accelStillThreshold
                               : filteredAngularVelocity.magnitude <= gyroStillThreshold);
        if (prototypeCalibrationReady != shouldReady)
        {
            prototypeCalibrationReady = shouldReady;
            playerController.SetPhaseTwoSensorCalibrationReady(shouldReady);
        }

        playerController.SetPhaseTwoSensorReading(calibrationSignal, mappedPhaseTwoForce);

        float now = Time.unscaledTime;
        bool stabTriggered = prototypeCalibrationReady &&
                             (primaryForceSignalMagnitude >= stabImpulseThreshold ||
                              mappedPhaseTwoForce >= playerController.PhaseTwoStabThreshold);

        if (!stabTriggered || now - lastStabTime < gestureCooldown)
            return;

        lastStabTime = now;
        playerController.TriggerPhaseTwoStab();
    }

    private void ClearPrototypeInjectionState()
    {
        if (playerController != null)
        {
            playerController.SetUltrasonicHoldActive(false);
            playerController.SetPhaseTwoCalibrationSensorHeld(false);
            playerController.SetPhaseTwoSensorReading(0f, 0f);
            playerController.SetPhaseTwoSensorCalibrationReady(false);
        }

        prototypeHoldActive = false;
        prototypeCalibrationHeld = false;
        prototypeCalibrationReady = false;
        mappedPhaseTwoForce = 0f;
        calibrationSignal = 0f;
    }

    private void CycleSourceMode()
    {
        sourceMode = sourceMode switch
        {
            ProbeSourceMode.ArduinoOnly => ProbeSourceMode.ObserveBoth,
            ProbeSourceMode.ObserveBoth => ProbeSourceMode.PsPrototypeOnly,
            _ => ProbeSourceMode.ArduinoOnly
        };

        ApplySourceMode(force: true);
    }

    private void ApplySourceMode(bool force)
    {
        if (!force && appliedSourceMode == sourceMode)
            return;

        bool enteringPrototype = sourceMode == ProbeSourceMode.PsPrototypeOnly;

        if (enteringPrototype)
        {
            if (arduinoTest != null && arduinoTest.enabled)
            {
                arduinoTest.enabled = false;
                managedArduinoDisable = true;
            }
        }
        else
        {
            RestoreArduinoIfManaged();
            ClearPrototypeInjectionState();
        }

        appliedSourceMode = sourceMode;
        LogCapabilityReport($"mode -> {sourceMode}");
        ShowToast($"Source mode: {sourceMode}");
    }

    private void RestoreArduinoIfManaged()
    {
        if (!managedArduinoDisable)
            return;

        if (arduinoTest != null && !arduinoTest.enabled)
            arduinoTest.enabled = true;

        managedArduinoDisable = false;
    }

    private void EvaluateCompatibilityVerdict()
    {
        if (!HasValidController)
        {
            compatibilityVerdict = "Controller Not Suitable";
            return;
        }

        if (!HasReadableMotionStream)
        {
            compatibilityVerdict = "Gameplay OK / Motion Fail";
            return;
        }

        if (!hasObservedMotionSignal && Time.unscaledTime - lastMotionUpdateAt > 1f)
        {
            compatibilityVerdict = "Gameplay OK / Motion Unknown";
            return;
        }

        compatibilityVerdict = "Full Input OK";
    }

    private void UpdateRiskMessages()
    {
        riskMessages.Clear();

        if (looksLikeGenericOrXInput)
            riskMessages.Add("PlayStation controller detected as generic/XInput-like layout");

        if (!HasReadableMotionStream)
            riskMessages.Add("No Unity-readable gyro/accel sensor stream on this Windows session");

        if (HasReadableMotionStream && hasMotionOwnershipRisk)
            riskMessages.Add("Sensor stream exists but cannot be proven to belong only to this controller");

        if (motionNoiseRiskLatched)
            riskMessages.Add("Motion stream is too noisy for stable hold calibration");

        if (HasReadableMotionStream && sourceMode == ProbeSourceMode.PsPrototypeOnly)
            riskMessages.Add("Motion can trigger phase 2 but is not directional enough for cloth biomechanics");

        if (HasValidController)
            riskMessages.Add("One-piece controller cannot validate dual-corner cloth behavior");
    }

    private void LogVerdictIfChanged()
    {
        string signature = $"{compatibilityVerdict}|{sourceMode}|{string.Join(" | ", riskMessages)}";
        if (signature == lastLoggedVerdict)
            return;

        lastLoggedVerdict = signature;
        LogCapabilityReport("verdict update");
    }

    private void LogCapabilityReport(string reason)
    {
        EvaluateCompatibilityVerdict();
        UpdateRiskMessages();

        StringBuilder builder = new StringBuilder(512);
        builder.AppendLine($"[PS Probe] Capability report ({reason})");
        builder.AppendLine($"Source mode: {sourceMode}");
        builder.AppendLine($"Verdict: {compatibilityVerdict}");
        builder.AppendLine($"Controller: {controllerSummary}");
        builder.AppendLine($"Motion: sensorsPresent={HasMotionSensorDevice}, readable={HasReadableMotionStream}, observedSignal={hasObservedMotionSignal}, lastUpdateAge={(Time.unscaledTime - lastMotionUpdateAt):F2}s");
        builder.AppendLine($"Rumble: {rumbleStatus}");

        if (riskMessages.Count > 0)
        {
            builder.AppendLine("Risks:");
            for (int i = 0; i < riskMessages.Count; i++)
                builder.AppendLine($"- {riskMessages[i]}");
        }

        Debug.Log(builder.ToString().TrimEnd());
        Debug.Log(BuildCompactSummary(reason));
    }

    private string BuildCompactSummary(string reason)
    {
        string controllerName = activeGamepad != null ? activeGamepad.displayName : "none";
        return $"[PS Probe] Summary ({reason}) verdict={compatibilityVerdict}, controller={controllerName}, motionReadable={HasReadableMotionStream}, motionObserved={hasObservedMotionSignal}, rumbleTrusted={hasRumbleCommandSupport}, source={sourceMode}";
    }

    private string BuildOverlayText()
    {
        StringBuilder builder = new StringBuilder(1024);

        builder.AppendLine($"Verdict: {compatibilityVerdict}");
        builder.AppendLine($"Source: {sourceMode}  |  Arduino: {GetArduinoModeLabel()}");
        builder.AppendLine($"Controller: {controllerSummary}");
        builder.AppendLine($"Hotkeys: F8 rumble, F9 baseline, F10 overlay, F11 source");
        builder.AppendLine();

        if (activeGamepad != null)
        {
            builder.AppendLine(
                $"Sticks L={FormatVector2(activeGamepad.leftStick.ReadValue())}  R={FormatVector2(activeGamepad.rightStick.ReadValue())}");
            builder.AppendLine(
                $"Triggers L2={activeGamepad.leftTrigger.ReadValue():F2}  R2={activeGamepad.rightTrigger.ReadValue():F2}  L1={FormatBool(activeGamepad.leftShoulder.isPressed)}  R1={FormatBool(activeGamepad.rightShoulder.isPressed)}");
            builder.AppendLine(
                $"Buttons Square/X={FormatBool(activeGamepad.buttonWest.isPressed)}  Triangle/Y={FormatBool(activeGamepad.buttonNorth.isPressed)}  Circle/B={FormatBool(activeGamepad.buttonEast.isPressed)}  Cross/A={FormatBool(activeGamepad.buttonSouth.isPressed)}");
            builder.AppendLine(
                $"Start={FormatBool(activeGamepad.startButton.isPressed)}  Select={FormatBool(activeGamepad.selectButton.isPressed)}  L3={FormatBool(activeGamepad.leftStickButton.isPressed)}  R3={FormatBool(activeGamepad.rightStickButton.isPressed)}");
        }
        else
        {
            builder.AppendLine("Gamepad: not detected.");
        }

        builder.AppendLine();
        builder.AppendLine(
            $"Motion sensors: gyro={GetSensorStatus(activeGyroscope)}  accel={GetSensorStatus(activeAccelerometer)}  linear={GetSensorStatus(activeLinearAccelerationSensor)}  attitude={GetSensorStatus(activeAttitudeSensor)}");
        builder.AppendLine($"Gyro: {FormatVector3(rawGyro)}  |mag|={filteredAngularVelocity.magnitude:F3}");
        builder.AppendLine($"Accel: {FormatVector3(rawAcceleration)}  baseline={FormatVector3(baselineAcceleration)}");
        builder.AppendLine($"Linear/Delta: {FormatVector3(accelerationDelta)}  |mag|={baselineOffsetMagnitude:F3}");
        builder.AppendLine($"Attitude Euler: {FormatVector3(rawAttitude.eulerAngles)}");
        builder.AppendLine($"Stillness={stillnessScore:F2}  stableFor={stableStillnessTimer:F2}s  unstableFor={unstableStillnessTimer:F2}s");
        builder.AppendLine($"SwingPeak={swingPeak:F3}  StabPeak={stabPeak:F3}  MotionAge={(Time.unscaledTime - lastMotionUpdateAt):F2}s");
        builder.AppendLine();

        builder.AppendLine(
            $"Phase1 Hold={FormatBool(prototypeHoldActive)}  swingCooldown={Mathf.Max(0f, gestureCooldown - (Time.unscaledTime - lastSwingTime)):F2}s");
        builder.AppendLine(
            $"Phase2 Held={FormatBool(prototypeCalibrationHeld)}  Ready={FormatBool(prototypeCalibrationReady)}  CalibOffset={calibrationSignal:F3}  Force={mappedPhaseTwoForce:F2}");
        builder.AppendLine($"Rumble: {rumbleStatus}");

        if (riskMessages.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Risks:");
            for (int i = 0; i < riskMessages.Count; i++)
                builder.AppendLine($"- {riskMessages[i]}");
        }

        return builder.ToString().TrimEnd();
    }

    private string GetArduinoModeLabel()
    {
        if (arduinoTest == null)
            return "No ArduinoTest component";

        if (managedArduinoDisable)
            return "Disabled by PsPrototypeOnly";

        return arduinoTest.enabled
            ? $"Enabled ({arduinoTest.ConnectionState})"
            : "Disabled";
    }

    private string GetSensorStatus(InputDevice sensor)
    {
        if (sensor == null)
            return "none";

        bool updatedRecently = Time.unscaledTime - lastMotionUpdateAt <= 1f;
        return $"{(sensor.enabled ? "on" : "off")}/{(updatedRecently ? "updated" : "idle")}";
    }

    private bool IsGamepadNeutral()
    {
        if (activeGamepad == null)
            return true;

        return activeGamepad.leftStick.ReadValue().sqrMagnitude < 0.0225f &&
               activeGamepad.rightStick.ReadValue().sqrMagnitude < 0.0225f &&
               activeGamepad.leftTrigger.ReadValue() < 0.15f &&
               activeGamepad.rightTrigger.ReadValue() < 0.15f &&
               !activeGamepad.leftShoulder.isPressed &&
               !activeGamepad.rightShoulder.isPressed &&
               !activeGamepad.buttonWest.isPressed &&
               !activeGamepad.buttonNorth.isPressed &&
               !activeGamepad.buttonEast.isPressed &&
               !activeGamepad.buttonSouth.isPressed;
    }

    private void EnsureGuiStyles()
    {
        if (panelStyle != null)
            return;

        panelStyle = new GUIStyle(GUI.skin.box);
        panelStyle.alignment = TextAnchor.UpperLeft;
        panelStyle.padding = new RectOffset(10, 10, 10, 10);
        panelStyle.normal.background = Texture2D.whiteTexture;
        panelStyle.normal.textColor = Color.white;

        labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.wordWrap = true;
        labelStyle.fontSize = 13;
        labelStyle.richText = false;
        labelStyle.normal.textColor = new Color(0.92f, 0.95f, 1f);

        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 15;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = new Color(1f, 0.96f, 0.82f);

        Texture2D background = new Texture2D(1, 1);
        background.SetPixel(0, 0, new Color(0.05f, 0.08f, 0.12f, 0.84f));
        background.Apply();
        panelStyle.normal.background = background;
    }

    private void EnsureRuntimeOverlay()
    {
        if (!useRuntimeCanvasOverlay || runtimeOverlayCanvas != null)
            return;

        GameObject canvasObject = new GameObject("PSProbeOverlayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.hideFlags = HideFlags.DontSave;

        runtimeOverlayCanvas = canvasObject.GetComponent<Canvas>();
        runtimeOverlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        runtimeOverlayCanvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        runtimeOverlayRoot = CreateUiRect("Panel", canvasObject.transform);
        runtimeOverlayRoot.anchorMin = new Vector2(0f, 1f);
        runtimeOverlayRoot.anchorMax = new Vector2(0f, 1f);
        runtimeOverlayRoot.pivot = new Vector2(0f, 1f);
        runtimeOverlayRoot.anchoredPosition = new Vector2(16f, -16f);
        runtimeOverlayRoot.sizeDelta = new Vector2(560f, 720f);

        Image background = runtimeOverlayRoot.gameObject.AddComponent<Image>();
        background.color = new Color(0.05f, 0.08f, 0.12f, 0.9f);

        runtimeOverlayTitleText = CreateText("Title", runtimeOverlayRoot, 15, FontStyle.Bold, new Color(1f, 0.96f, 0.82f));
        RectTransform titleRect = runtimeOverlayTitleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.offsetMin = new Vector2(14f, -34f);
        titleRect.offsetMax = new Vector2(-14f, -10f);
        runtimeOverlayTitleText.text = "PS Controller Compatibility Probe";

        runtimeOverlayText = CreateText("Body", runtimeOverlayRoot, 14, FontStyle.Normal, new Color(0.92f, 0.95f, 1f));
        RectTransform bodyRect = runtimeOverlayText.rectTransform;
        bodyRect.anchorMin = new Vector2(0f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.offsetMin = new Vector2(14f, 14f);
        bodyRect.offsetMax = new Vector2(-14f, -42f);
        runtimeOverlayText.alignment = TextAnchor.UpperLeft;
        runtimeOverlayText.horizontalOverflow = HorizontalWrapMode.Wrap;
        runtimeOverlayText.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform toastRoot = CreateUiRect("Toast", canvasObject.transform);
        toastRoot.anchorMin = new Vector2(0.5f, 1f);
        toastRoot.anchorMax = new Vector2(0.5f, 1f);
        toastRoot.pivot = new Vector2(0.5f, 1f);
        toastRoot.anchoredPosition = new Vector2(0f, -16f);
        toastRoot.sizeDelta = new Vector2(860f, 44f);

        Image toastBackground = toastRoot.gameObject.AddComponent<Image>();
        toastBackground.color = new Color(0.14f, 0.2f, 0.28f, 0.92f);

        runtimeToastText = CreateText("ToastText", toastRoot, 16, FontStyle.Bold, Color.white);
        RectTransform toastTextRect = runtimeToastText.rectTransform;
        toastTextRect.anchorMin = Vector2.zero;
        toastTextRect.anchorMax = Vector2.one;
        toastTextRect.offsetMin = new Vector2(10f, 4f);
        toastTextRect.offsetMax = new Vector2(-10f, -4f);
        runtimeToastText.alignment = TextAnchor.MiddleCenter;
        runtimeToastText.text = string.Empty;
        toastRoot.gameObject.SetActive(false);
    }

    private void UpdateRuntimeOverlay()
    {
        if (runtimeOverlayText == null)
            return;

        runtimeOverlayText.text = BuildOverlayText();
        if (runtimeOverlayRoot != null)
            runtimeOverlayRoot.gameObject.SetActive(showOverlay);

        if (runtimeToastText == null)
            return;

        bool toastActive = Time.unscaledTime <= toastHideAt && !string.IsNullOrWhiteSpace(runtimeToastText.text);
        if (runtimeToastText.transform.parent != null)
            runtimeToastText.transform.parent.gameObject.SetActive(toastActive);
    }

    private void SetOverlayVisibility(bool visible)
    {
        showOverlay = visible;

        if (runtimeOverlayRoot != null)
            runtimeOverlayRoot.gameObject.SetActive(visible);
    }

    private void ShowToast(string message, float duration = 2.25f)
    {
        if (runtimeToastText == null)
            return;

        runtimeToastText.text = message;
        toastHideAt = Time.unscaledTime + Mathf.Max(0.5f, duration);
        if (runtimeToastText.transform.parent != null)
            runtimeToastText.transform.parent.gameObject.SetActive(true);
    }

    private RectTransform CreateUiRect(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject.GetComponent<RectTransform>();
    }

    private Text CreateText(string name, Transform parent, int fontSize, FontStyle fontStyle, Color color)
    {
        RectTransform rect = CreateUiRect(name, parent);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = LoadBuiltinFont();
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static Font LoadBuiltinFont()
    {
        try
        {
            Font legacyRuntime = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (legacyRuntime != null)
                return legacyRuntime;
        }
        catch
        {
        }

        try
        {
            Font osFont = Font.CreateDynamicFontFromOSFont(
                new[]
                {
                    "Microsoft JhengHei UI",
                    "Microsoft JhengHei",
                    "Segoe UI",
                    "Arial"
                },
                16
            );
            if (osFont != null)
                return osFont;
        }
        catch
        {
        }

        return null;
    }

    private string FormatBool(bool value) => value ? "Y" : "N";

    private string FormatVector2(Vector2 value) => $"({value.x:F2}, {value.y:F2})";

    private string FormatVector3(Vector3 value) => $"({value.x:F2}, {value.y:F2}, {value.z:F2})";
}
