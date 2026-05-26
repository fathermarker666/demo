using System;
using System.Collections;
using System.Globalization;
using System.IO.Ports;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[DefaultExecutionOrder(-1000)]
public class ArduinoTest : MonoBehaviour
{
    public const string VirtualGamepadUsage = "ESP32VirtualGamepad";

    public enum SensorConnectionState
    {
        Disconnected,
        PortOpenNoSignal,
        Active
    }

    [SerializeField] string portName = "COM6";
    [SerializeField] int baudRate = 115200;
    [SerializeField] float initialOpenDelay = 0.5f;
    [SerializeField] float reopenDelay = 1f;
    [SerializeField] float maxForceValue = 50f;
    [SerializeField] string calibrationCommand = "CAL";
    [SerializeField] float sensorSignalTimeout = 1.2f;
    [SerializeField] float phaseOneHoldDistanceThresholdCm = 30f;
    [SerializeField] float phaseOneHoldSignalTimeout = 0.5f;
    [SerializeField] string virtualGamepadName = "ESP32 Virtual Gamepad";

    SerialPort sp;
    Coroutine reopenCoroutine;
    bool isQuitting;
    BullfightPlayerController playerController;
    float lastSensorMessageAt = -999f;
    float lastParsedForceAt = -999f;
    float lastUltrasonicDistanceCm = -1f;
    float lastUltrasonicMessageAt = -999f;
    bool isUltrasonicHoldingCloth;
    string pendingSerialData = string.Empty;
    string lastSensorMessage = string.Empty;
    string lastOpenError = string.Empty;
    float nextPlayerResolveAt = -1f;
    string lastOpenFailureWarningMessage = string.Empty;
    bool phaseTwoCalibrationRequested;
    bool phaseTwoCalibrationReadyReceived;
    bool phaseTwoFirstForceReceived;
    bool phaseTwoCalibrationBridgeForwarded;
    bool phaseTwoCalibrationBridgeFailed;
    string lastPhaseTwoDiagnosticMessage = string.Empty;
    SensorConnectionState connectionState = SensorConnectionState.Disconnected;
    Gamepad virtualGamepad;

    const uint ButtonMaskSouth = 1u << 0;
    const uint ButtonMaskEast = 1u << 1;
    const uint ButtonMaskWest = 1u << 2;
    const uint ButtonMaskNorth = 1u << 3;
    const uint ButtonMaskLeftShoulder = 1u << 4;
    const uint ButtonMaskRightShoulder = 1u << 5;
    const uint ButtonMaskLeftTrigger = 1u << 6;
    const uint ButtonMaskRightTrigger = 1u << 7;

    public event Action<SensorConnectionState> OnConnectionStateChanged;

    public bool IsPortOpen => sp != null && sp.IsOpen;
    public bool IsSensorConnected => connectionState == SensorConnectionState.Active;
    public bool HasRecentForcePacket => Time.unscaledTime - lastParsedForceAt <= Mathf.Max(0.2f, sensorSignalTimeout);
    public string LastSensorMessage => lastSensorMessage;
    public float LastUltrasonicDistanceCm => lastUltrasonicDistanceCm;
    public bool IsUltrasonicHoldingCloth => isUltrasonicHoldingCloth;
    public SensorConnectionState ConnectionState => connectionState;
    public bool IsPhaseTwoCalibrationSessionActive => phaseTwoCalibrationRequested;
    public bool HasReceivedPhaseTwoCalibrationReady => phaseTwoCalibrationReadyReceived;
    public bool IsAwaitingPhaseTwoCalibrationReady => phaseTwoCalibrationRequested && !phaseTwoCalibrationReadyReceived;
    public bool HasReceivedPhaseTwoCalibrationForce => phaseTwoFirstForceReceived;
    public bool HasForwardedPhaseTwoCalibrationCommand => phaseTwoCalibrationBridgeForwarded;
    public bool HasFailedPhaseTwoCalibrationCommandForward => phaseTwoCalibrationBridgeFailed;
    public bool IsAwaitingPhaseTwoCalibrationBridge =>
        phaseTwoCalibrationRequested && !phaseTwoCalibrationBridgeForwarded && !phaseTwoCalibrationBridgeFailed;
    public string CurrentConnectionStatus
    {
        get
        {
            if (connectionState == SensorConnectionState.Active)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(lastOpenError))
                return $"[未連接到感測器] {portName} 開啟失敗";

            if (connectionState == SensorConnectionState.PortOpenNoSignal)
                return $"[未連接到感測器] {portName} 已開啟，等待資料中";

            return $"[未連接到感測器] 請確認 {portName}";
        }
    }

    void Start()
    {
        EnsureVirtualGamepad();
        reopenCoroutine = StartCoroutine(OpenPortAfterDelay(initialOpenDelay));
    }

    IEnumerator OpenPortAfterDelay(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        reopenCoroutine = null;
        OpenPort();
    }

    void OpenPort()
    {
        ClosePort();
        EnsurePhaseOneHoldThresholdDefault();
        lastSensorMessageAt = -999f;
        lastParsedForceAt = -999f;
        lastUltrasonicDistanceCm = -1f;
        lastUltrasonicMessageAt = -999f;
        pendingSerialData = string.Empty;
        lastSensorMessage = string.Empty;
        ResolvePlayerControllerIfNeeded(force: true);
        playerController?.SetPhaseTwoSensorCalibrationReady(false);

        sp = new SerialPort(portName, baudRate)
        {
            ReadTimeout = 100,
            NewLine = "\n"
        };

        try
        {
            sp.Open();
            lastOpenError = string.Empty;
            lastOpenFailureWarningMessage = string.Empty;
            SetConnectionState(SensorConnectionState.PortOpenNoSignal);
            Debug.Log($"Serial port {portName} opened.");
        }
        catch (Exception e)
        {
            lastOpenError = e.Message;
            LogOpenFailureWarning("Open failed: " + e.Message);
            ClosePort();

            if (!isQuitting && isActiveAndEnabled && reopenCoroutine == null)
                reopenCoroutine = StartCoroutine(OpenPortAfterDelay(reopenDelay));
        }
    }

    void Update()
    {
        ResolvePlayerControllerIfNeeded();

        if (sp == null || !sp.IsOpen)
        {
            SetConnectionState(SensorConnectionState.Disconnected);
            UpdateUltrasonicHoldTimeout();
            return;
        }

        try
        {
            const int maxLinesPerFrame = 24;
            if (sp.BytesToRead > 0)
                pendingSerialData += sp.ReadExisting();

            int linesReadThisFrame = 0;
            while (linesReadThisFrame < maxLinesPerFrame)
            {
                int newlineIndex = pendingSerialData.IndexOf('\n');
                if (newlineIndex < 0)
                    break;

                string data = pendingSerialData.Substring(0, newlineIndex).Trim();
                pendingSerialData = pendingSerialData.Substring(newlineIndex + 1);
                if (!string.IsNullOrEmpty(data))
                    HandleSensorMessage(data);

                linesReadThisFrame++;

                if (sp != null && sp.IsOpen && sp.BytesToRead > 0)
                    pendingSerialData += sp.ReadExisting();
            }
        }
        catch (TimeoutException)
        {
        }
        catch (Exception e)
        {
            Debug.LogWarning("Serial connection lost, retrying: " + e.Message);
            ClosePort();

            if (!isQuitting && isActiveAndEnabled && reopenCoroutine == null)
                reopenCoroutine = StartCoroutine(OpenPortAfterDelay(reopenDelay));
        }

        if (Time.unscaledTime - lastSensorMessageAt > Mathf.Max(0.2f, sensorSignalTimeout))
            SetConnectionState(SensorConnectionState.PortOpenNoSignal);

        UpdateUltrasonicHoldTimeout();
    }

    void OnDisable()
    {
        playerController?.SetPhaseTwoCalibrationSensorHeld(false);
        playerController?.ResetSensorDrivenInputs();
        ResetVirtualGamepadState();
        ClosePort();
    }

    void OnDestroy()
    {
        RemoveVirtualGamepad();
        ClosePort();
    }

    void OnApplicationQuit()
    {
        isQuitting = true;
        playerController?.SetPhaseTwoCalibrationSensorHeld(false);
        playerController?.ResetSensorDrivenInputs();
        RemoveVirtualGamepad();
        ClosePort();
    }

    public void BeginPhaseTwoCalibration()
    {
        ResolvePlayerControllerIfNeeded(force: true);

        if (sp == null || !sp.IsOpen)
            OpenPort();

        playerController?.SetPhaseTwoSensorCalibrationReady(false);
        phaseTwoCalibrationRequested = true;
        phaseTwoCalibrationReadyReceived = false;
        phaseTwoFirstForceReceived = false;
        phaseTwoCalibrationBridgeForwarded = false;
        phaseTwoCalibrationBridgeFailed = false;
        lastPhaseTwoDiagnosticMessage = string.Empty;

        bool commandSent = TryWriteLine(calibrationCommand);
        LogPhaseTwoCalibrationDiagnostic(commandSent
            ? "[ESP32] Phase Two calibration: CAL sent."
            : "[ESP32] Phase Two calibration: CAL could not be sent because the serial bridge is unavailable.");
    }

    void HandleSensorMessage(string data)
    {
        string message = data.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(message))
            return;

        lastSensorMessageAt = Time.unscaledTime;
        lastSensorMessage = data.Trim();
        SetConnectionState(SensorConnectionState.Active);

        if (TryHandleControllerMessage(data))
            return;

        if (TryHandleUltrasonicDistanceMessage(data))
            return;

        if (TryHandlePhaseTwoBridgeDiagnosticMessage(data))
            return;

        if (TryHandleForceMessage(data))
            return;

        if (message.Contains("BULL_START"))
        {
            Debug.Log("Bingo! Received bullfight start command.");
            return;
        }

        if (message.Contains("READY"))
        {
            playerController?.SetPhaseTwoSensorCalibrationReady(true);
            if (phaseTwoCalibrationRequested && !phaseTwoCalibrationReadyReceived)
            {
                phaseTwoCalibrationReadyReceived = true;
                LogPhaseTwoCalibrationDiagnostic("[ESP32] Phase Two calibration: READY received.");
            }
            return;
        }

        if (message.Contains("THRUST") || message.Contains("STAB"))
        {
            playerController?.TriggerPhaseTwoStab();
            return;
        }

        switch (message)
        {
            case "SWING":
            case "CAPA":
            case "PHASE1_SWING":
                playerController?.TriggerSensorSwing();
                break;

            case "PHASE2_CALIBRATION_START":
            case "PHASE2_CALIBRATE_START":
            case "CALIBRATION_START":
            case "CALIBRATE_ON":
                playerController?.SetPhaseTwoCalibrationSensorHeld(true);
                break;

            case "PHASE2_CALIBRATION_STOP":
            case "PHASE2_CALIBRATE_STOP":
            case "CALIBRATION_STOP":
            case "CALIBRATE_OFF":
                playerController?.SetPhaseTwoCalibrationSensorHeld(false);
                break;

            case "CAL_RESET":
            case "PHASE2_CAL_RESET":
            case "CALIBRATION_RESET":
                playerController?.SetPhaseTwoSensorCalibrationReady(false);
                break;
        }
    }

    bool TryHandlePhaseTwoBridgeDiagnosticMessage(string rawData)
    {
        string data = rawData.Trim();
        if (string.IsNullOrEmpty(data))
            return false;

        if (data.StartsWith("PHASE2_FIRST_FORCE_BRIDGED", StringComparison.OrdinalIgnoreCase))
        {
            phaseTwoCalibrationBridgeForwarded = true;
            phaseTwoCalibrationBridgeFailed = false;

            string[] parts = data.Split(new[] { ':' }, 2);
            if (parts.Length == 2 &&
                float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float bridgedForce))
            {
                LogPhaseTwoCalibrationDiagnostic($"[ESP32] Phase Two bridge: first FORCE bridged ({bridgedForce:F1}).");
            }
            else
            {
                LogPhaseTwoCalibrationDiagnostic("[ESP32] Phase Two bridge: first FORCE bridged.");
            }

            return true;
        }

        switch (data.ToUpperInvariant())
        {
            case "PHASE2_CAL_SENT":
                phaseTwoCalibrationBridgeForwarded = true;
                phaseTwoCalibrationBridgeFailed = false;
                LogPhaseTwoCalibrationDiagnostic("[ESP32] Phase Two bridge: CAL forwarded by receiver.");
                return true;

            case "PHASE2_CAL_NO_PEER":
                phaseTwoCalibrationBridgeForwarded = false;
                phaseTwoCalibrationBridgeFailed = true;
                LogPhaseTwoCalibrationDiagnostic("[ESP32] Phase Two bridge: receiver has no handheld peer.");
                return true;

            case "PHASE2_CAL_SEND_FAIL":
                phaseTwoCalibrationBridgeForwarded = false;
                phaseTwoCalibrationBridgeFailed = true;
                LogPhaseTwoCalibrationDiagnostic("[ESP32] Phase Two bridge: receiver failed to forward CAL.");
                return true;

            case "PHASE2_READY_BRIDGED":
                phaseTwoCalibrationBridgeForwarded = true;
                phaseTwoCalibrationBridgeFailed = false;
                LogPhaseTwoCalibrationDiagnostic("[ESP32] Phase Two bridge: READY bridged.");
                return true;
        }

        return false;
    }

    bool TryHandleControllerMessage(string rawData)
    {
        string data = rawData.Trim();
        if (string.IsNullOrEmpty(data))
            return false;

        const string prefix = "CTRL:";
        if (!data.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        string[] parts = data.Substring(prefix.Length).Split(',');
        if (parts.Length != 5)
            return true;

        if (!int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int leftX) ||
            !int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int leftY) ||
            !int.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rightX) ||
            !int.TryParse(parts[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rightY) ||
            !uint.TryParse(parts[4].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out uint buttonsMask))
            return true;

        EnsureVirtualGamepad();
        if (virtualGamepad == null)
            return true;

        virtualGamepad.MakeCurrent();
        GamepadState state = default;
        state.leftStick = new Vector2(NormalizeControllerAxis(leftX), NormalizeControllerAxis(leftY));
        state.rightStick = new Vector2(NormalizeControllerAxis(rightX), NormalizeControllerAxis(rightY));
        state.leftTrigger = (buttonsMask & ButtonMaskLeftTrigger) != 0 ? 1f : 0f;
        state.rightTrigger = (buttonsMask & ButtonMaskRightTrigger) != 0 ? 1f : 0f;
        state = state.WithButton(GamepadButton.South, (buttonsMask & ButtonMaskSouth) != 0);
        state = state.WithButton(GamepadButton.East, (buttonsMask & ButtonMaskEast) != 0);
        state = state.WithButton(GamepadButton.West, (buttonsMask & ButtonMaskWest) != 0);
        state = state.WithButton(GamepadButton.North, (buttonsMask & ButtonMaskNorth) != 0);
        state = state.WithButton(GamepadButton.LeftShoulder, (buttonsMask & ButtonMaskLeftShoulder) != 0);
        state = state.WithButton(GamepadButton.RightShoulder, (buttonsMask & ButtonMaskRightShoulder) != 0);

        InputState.Change(virtualGamepad, state);
        return true;
    }

    bool TryHandleUltrasonicDistanceMessage(string rawData)
    {
        if (playerController == null)
            return false;

        string data = rawData.Trim();
        if (string.IsNullOrEmpty(data))
            return false;

        const string prefix = "DIST:";
        if (!data.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        string valueText = data.Substring(prefix.Length).Trim();
        if (!float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out float distanceCm))
            return false;

        lastUltrasonicDistanceCm = distanceCm;
        lastUltrasonicMessageAt = Time.unscaledTime;

        bool shouldHoldCloth = distanceCm > phaseOneHoldDistanceThresholdCm;
        if (shouldHoldCloth == isUltrasonicHoldingCloth)
            return true;

        isUltrasonicHoldingCloth = shouldHoldCloth;
        playerController.SetUltrasonicHoldActive(shouldHoldCloth);
        return true;
    }

    bool TryHandleForceMessage(string rawData)
    {
        if (playerController == null)
            return false;

        string data = rawData.Trim();
        if (string.IsNullOrEmpty(data))
            return false;

        if (TryHandleCsvSensorMessage(data))
            return true;

        string[] separators = { ":", "=", "," };
        for (int i = 0; i < separators.Length; i++)
        {
            string separator = separators[i];
            int splitIndex = data.IndexOf(separator, StringComparison.Ordinal);
            if (splitIndex < 0)
                continue;

            string label = data.Substring(0, splitIndex).Trim();
            string valueText = data.Substring(splitIndex + separator.Length).Trim();
            if (!label.Equals("FORCE", StringComparison.OrdinalIgnoreCase) &&
                !label.Equals("POWER", StringComparison.OrdinalIgnoreCase) &&
                !label.Equals("THRUST", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedForce))
                return false;

            lastParsedForceAt = Time.unscaledTime;
            playerController.SetPhaseTwoSensorCalibrationReady(true);
            playerController.SetPhaseTwoSensorReading(parsedForce, Mathf.Clamp(Mathf.Abs(parsedForce), 0f, Mathf.Max(1f, maxForceValue)));
            RegisterPhaseTwoForceDiagnostic(parsedForce);
            return true;
        }

        if (!float.TryParse(data, NumberStyles.Float, CultureInfo.InvariantCulture, out float rawForce))
            return false;

        lastParsedForceAt = Time.unscaledTime;
        playerController.SetPhaseTwoSensorCalibrationReady(true);
        playerController.SetPhaseTwoSensorReading(rawForce, Mathf.Clamp(Mathf.Abs(rawForce), 0f, Mathf.Max(1f, maxForceValue)));
        RegisterPhaseTwoForceDiagnostic(rawForce);
        return true;
    }

    bool TryHandleCsvSensorMessage(string data)
    {
        string[] parts = data.Split(',');
        if (parts.Length < 2)
            return false;

        if (!float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float signedSignal))
            return false;

        float displayedForce = Mathf.Abs(signedSignal);
        if (parts.Length >= 3 &&
            float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float reportedCap) &&
            reportedCap > 0.01f)
        {
            displayedForce = Mathf.Clamp(displayedForce, 0f, reportedCap);
        }

        lastParsedForceAt = Time.unscaledTime;
        playerController.SetPhaseTwoSensorCalibrationReady(true);
        playerController.SetPhaseTwoSensorReading(signedSignal, Mathf.Clamp(displayedForce, 0f, Mathf.Max(1f, maxForceValue)));
        RegisterPhaseTwoForceDiagnostic(signedSignal);
        return true;
    }

    bool TryWriteLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || sp == null || !sp.IsOpen)
            return false;

        try
        {
            sp.WriteLine(line);
            return true;
        }
        catch (Exception e)
        {
            lastOpenError = e.Message;
            Debug.LogWarning("Serial write failed: " + e.Message);
            return false;
        }
    }

    void UpdateUltrasonicHoldTimeout()
    {
        if (!isUltrasonicHoldingCloth)
            return;

        if (Time.unscaledTime - lastUltrasonicMessageAt <= Mathf.Max(0.1f, phaseOneHoldSignalTimeout))
            return;

        ClearUltrasonicHoldState();
    }

    void EnsurePhaseOneHoldThresholdDefault()
    {
        if (phaseOneHoldDistanceThresholdCm <= 0f)
            phaseOneHoldDistanceThresholdCm = 30f;
    }

    float NormalizeControllerAxis(int value)
    {
        return Mathf.Clamp(value / 1000f, -1f, 1f);
    }

    void ClearUltrasonicHoldState(bool resetRestAnchor = true)
    {
        isUltrasonicHoldingCloth = false;
        playerController?.SetUltrasonicHoldActive(false);
    }

    void ResolvePlayerControllerIfNeeded(bool force = false)
    {
        if (playerController != null)
            return;

        if (!force && Time.unscaledTime < nextPlayerResolveAt)
            return;

        nextPlayerResolveAt = Time.unscaledTime + 0.5f;
        playerController = FindObjectOfType<BullfightPlayerController>(true);
    }

    void LogOpenFailureWarning(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (message == lastOpenFailureWarningMessage)
            return;

        lastOpenFailureWarningMessage = message;
        Debug.LogWarning(message);
    }

    void LogPhaseTwoCalibrationDiagnostic(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || message == lastPhaseTwoDiagnosticMessage)
            return;

        lastPhaseTwoDiagnosticMessage = message;
        Debug.Log(message);
    }

    void RegisterPhaseTwoForceDiagnostic(float forceValue)
    {
        if (!phaseTwoCalibrationRequested || phaseTwoFirstForceReceived)
            return;

        phaseTwoFirstForceReceived = true;
        if (!phaseTwoCalibrationReadyReceived)
            phaseTwoCalibrationReadyReceived = true;

        LogPhaseTwoCalibrationDiagnostic($"[ESP32] Phase Two calibration: first FORCE received ({forceValue:F1}).");
    }

    void SetConnectionState(SensorConnectionState newState)
    {
        if (connectionState == newState)
            return;

        SensorConnectionState previousState = connectionState;
        connectionState = newState;

        if (previousState == SensorConnectionState.Active && newState != SensorConnectionState.Active)
            ClearSensorDrivenInputState();

        OnConnectionStateChanged?.Invoke(connectionState);
    }

    void ClearSensorDrivenInputState()
    {
        if (isUltrasonicHoldingCloth)
            ClearUltrasonicHoldState();

        ResetVirtualGamepadState();
        playerController?.SetPhaseTwoCalibrationSensorHeld(false);
        playerController?.ResetSensorDrivenInputs(clearUltrasonicHold: false);
    }

    void EnsureVirtualGamepad()
    {
        if (virtualGamepad != null)
            return;

        virtualGamepad = InputSystem.AddDevice<Gamepad>(virtualGamepadName);
        InputSystem.SetDeviceUsage(virtualGamepad, VirtualGamepadUsage);
        virtualGamepad.MakeCurrent();
        ResetVirtualGamepadState();
    }

    void ResetVirtualGamepadState()
    {
        if (virtualGamepad == null)
            return;

        GamepadState clearedState = default;
        InputState.Change(virtualGamepad, clearedState);
    }

    void RemoveVirtualGamepad()
    {
        if (virtualGamepad == null)
            return;

        InputSystem.RemoveDevice(virtualGamepad);
        virtualGamepad = null;
    }

    void ClosePort()
    {
        if (reopenCoroutine != null)
        {
            StopCoroutine(reopenCoroutine);
            reopenCoroutine = null;
        }

        if (sp == null)
        {
            SetConnectionState(SensorConnectionState.Disconnected);
            return;
        }

        try
        {
            if (sp.IsOpen)
                sp.Close();
        }
        catch (Exception e)
        {
            Debug.LogWarning("Close failed: " + e.Message);
        }
        finally
        {
            sp.Dispose();
            sp = null;
            SetConnectionState(SensorConnectionState.Disconnected);
            lastSensorMessageAt = -999f;
            lastParsedForceAt = -999f;
            pendingSerialData = string.Empty;
            lastSensorMessage = string.Empty;
            lastUltrasonicDistanceCm = -1f;
            lastUltrasonicMessageAt = -999f;
            ResetVirtualGamepadState();
        }
    }
}
