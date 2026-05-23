#include <Wire.h>
#include <WiFi.h>
#include <esp_now.h>
#include <math.h>
#include <string.h>

/*
  Handheld ESP32 sender
  - MPU6050 on SDA=21, SCL=22
  - Ultrasonic TRIG=16, ECHO=18
  - Dual analog sticks on ADC1 pins
  - Buttons: A/B/X/Y/LB/RB/LT/RT
  - Sends controller packets over ESP-NOW every 20 ms
  - Sends sensor packets over ESP-NOW every 20 ms
  - Accepts CAL from USB serial and from the paired receiver ESP32

  IMPORTANT:
  Flash the receiver first, read the receiver STA MAC from Serial Monitor,
  then replace Config::kReceiverMac with that MAC and reflash this board.
*/

namespace Config {
constexpr uint8_t kSensorAddress = 0x68;
constexpr uint8_t kSdaPin = 21;
constexpr uint8_t kSclPin = 22;
constexpr uint32_t kSerialBaud = 115200;
constexpr uint32_t kI2cClock = 400000;
constexpr uint32_t kSensorSampleIntervalMs = 20;
constexpr uint32_t kControllerSampleIntervalMs = 20;
constexpr uint32_t kCalibrationDurationMs = 350;
constexpr float kAccelScaleLsbPerG = 16384.0f;
constexpr float kMinimumNoiseBandG = 0.015f;
constexpr float kForceScale = 40.0f;
constexpr float kForceCap = 50.0f;
constexpr float kThrustThreshold = 35.0f;
constexpr float kReleaseThreshold = 18.0f;
constexpr uint8_t kUltrasonicTrigPin = 16;
constexpr uint8_t kUltrasonicEchoPin = 18;
constexpr uint32_t kUltrasonicPulseTimeoutUs = 5000;
constexpr float kUltrasonicMaxDistanceCm = 80.0f;

constexpr uint8_t kLeftStickXPin = 32;
constexpr uint8_t kLeftStickYPin = 33;
constexpr uint8_t kRightStickXPin = 34;
constexpr uint8_t kRightStickYPin = 35;
constexpr uint16_t kAnalogMax = 4095;
constexpr uint16_t kAnalogMidpoint = kAnalogMax / 2;
constexpr uint8_t kStickCalibrationSamples = 40;
constexpr uint8_t kStickCalibrationDelayMs = 5;
constexpr float kStickDeadzone = 0.12f;
constexpr bool kInvertLeftStickX = false;
constexpr bool kInvertLeftStickY = true;
constexpr bool kInvertRightStickX = false;
constexpr bool kInvertRightStickY = true;

constexpr uint8_t kButtonAPin = 4;
constexpr uint8_t kButtonBPin = 13;
constexpr uint8_t kButtonXPin = 14;
constexpr uint8_t kButtonYPin = 19;
constexpr uint8_t kButtonLbPin = 23;
constexpr uint8_t kButtonRbPin = 25;
constexpr uint8_t kButtonLtPin = 26;
constexpr uint8_t kButtonRtPin = 27;

constexpr uint8_t kReceiverMac[6] = {
    0x30, 0x76, 0xF5, 0xF8, 0xFF, 0xF0};
}

namespace PacketType {
constexpr uint8_t kSensor = 1;
constexpr uint8_t kCommand = 2;
constexpr uint8_t kController = 3;
}

namespace CommandId {
constexpr uint8_t kCalibrate = 1;
}

namespace PacketFlag {
constexpr uint8_t kThrustLatched = 1 << 0;
constexpr uint8_t kDistanceValid = 1 << 1;
constexpr uint8_t kReadyPulse = 1 << 2;
}

namespace ControllerButtonBit {
constexpr uint16_t kA = 1 << 0;
constexpr uint16_t kB = 1 << 1;
constexpr uint16_t kX = 1 << 2;
constexpr uint16_t kY = 1 << 3;
constexpr uint16_t kLb = 1 << 4;
constexpr uint16_t kRb = 1 << 5;
constexpr uint16_t kLt = 1 << 6;
constexpr uint16_t kRt = 1 << 7;
}

namespace MpuReg {
constexpr uint8_t kWhoAmI = 0x75;
constexpr uint8_t kPwrMgmt1 = 0x6B;
constexpr uint8_t kPwrMgmt2 = 0x6C;
constexpr uint8_t kConfig = 0x1A;
constexpr uint8_t kSampleRateDiv = 0x19;
constexpr uint8_t kAccelConfig = 0x1C;
constexpr uint8_t kAccelConfig2 = 0x1D;
constexpr uint8_t kAccelXoutH = 0x3B;
}

struct __attribute__((packed)) SensorPacket {
  uint8_t packetType;
  float force;
  float distanceCm;
  uint8_t flags;
};

struct __attribute__((packed)) CommandPacket {
  uint8_t packetType;
  uint8_t command;
};

struct __attribute__((packed)) ControllerPacket {
  uint8_t packetType;
  int16_t leftX;
  int16_t leftY;
  int16_t rightX;
  int16_t rightY;
  uint16_t buttons;
};

static_assert(sizeof(SensorPacket) == 10, "Unexpected SensorPacket size");
static_assert(sizeof(CommandPacket) == 2, "Unexpected CommandPacket size");
static_assert(sizeof(ControllerPacket) == 11, "Unexpected ControllerPacket size");

struct CalibrationStats {
  float sumY = 0.0f;
  float minY = 0.0f;
  float maxY = 0.0f;
  uint32_t sampleCount = 0;
};

struct ButtonBinding {
  uint8_t pin;
  uint16_t mask;
};

const ButtonBinding kButtonBindings[] = {
    {Config::kButtonAPin, ControllerButtonBit::kA},
    {Config::kButtonBPin, ControllerButtonBit::kB},
    {Config::kButtonXPin, ControllerButtonBit::kX},
    {Config::kButtonYPin, ControllerButtonBit::kY},
    {Config::kButtonLbPin, ControllerButtonBit::kLb},
    {Config::kButtonRbPin, ControllerButtonBit::kRb},
    {Config::kButtonLtPin, ControllerButtonBit::kLt},
    {Config::kButtonRtPin, ControllerButtonBit::kRt},
};

const uint8_t kStickPins[] = {
    Config::kLeftStickXPin,
    Config::kLeftStickYPin,
    Config::kRightStickXPin,
    Config::kRightStickYPin,
};

const bool kStickInvert[] = {
    Config::kInvertLeftStickX,
    Config::kInvertLeftStickY,
    Config::kInvertRightStickX,
    Config::kInvertRightStickY,
};

enum class RunMode {
  Idle,
  Calibrating,
  Monitoring
};

RunMode gMode = RunMode::Idle;
CalibrationStats gCalibration;
unsigned long gLastSensorSampleMs = 0;
unsigned long gLastControllerSampleMs = 0;
unsigned long gCalibrationStartMs = 0;
float gAccelYOffset = 0.0f;
float gNoiseBandY = Config::kMinimumNoiseBandG;
float gLastForce = 0.0f;
bool gSensorReady = false;
bool gThrustLatched = false;
bool gPendingReadyPulse = false;
int gStickCenterRaw[4] = {
    Config::kAnalogMidpoint,
    Config::kAnalogMidpoint,
    Config::kAnalogMidpoint,
    Config::kAnalogMidpoint,
};
char gSerialCommandBuffer[8];
uint8_t gSerialCommandLength = 0;

void handleCalibrationCommand();
void processIncomingPacket(const uint8_t* mac, const uint8_t* incomingData, int len);

bool writeRegister(uint8_t reg, uint8_t value) {
  Wire.beginTransmission(Config::kSensorAddress);
  Wire.write(reg);
  Wire.write(value);
  return Wire.endTransmission() == 0;
}

bool readRegisters(uint8_t startReg, uint8_t* buffer, size_t length) {
  Wire.beginTransmission(Config::kSensorAddress);
  Wire.write(startReg);
  if (Wire.endTransmission(false) != 0) {
    return false;
  }

  const size_t received = Wire.requestFrom(
      static_cast<int>(Config::kSensorAddress), static_cast<int>(length), static_cast<int>(true));
  if (received != length) {
    return false;
  }

  for (size_t i = 0; i < length; ++i) {
    buffer[i] = Wire.read();
  }

  return true;
}

bool readAccelY(float& accelYG) {
  uint8_t buffer[6];
  if (!readRegisters(MpuReg::kAccelXoutH, buffer, sizeof(buffer))) {
    return false;
  }

  const int16_t rawY = static_cast<int16_t>((buffer[2] << 8) | buffer[3]);
  accelYG = static_cast<float>(rawY) / Config::kAccelScaleLsbPerG;
  return true;
}

bool initSensor() {
  uint8_t whoAmI = 0;
  if (!readRegisters(MpuReg::kWhoAmI, &whoAmI, 1)) {
    return false;
  }

  if (!writeRegister(MpuReg::kPwrMgmt1, 0x01)) {
    return false;
  }
  if (!writeRegister(MpuReg::kPwrMgmt2, 0x00)) {
    return false;
  }
  if (!writeRegister(MpuReg::kConfig, 0x03)) {
    return false;
  }
  if (!writeRegister(MpuReg::kSampleRateDiv, 0x09)) {
    return false;
  }
  if (!writeRegister(MpuReg::kAccelConfig, 0x00)) {
    return false;
  }
  if (!writeRegister(MpuReg::kAccelConfig2, 0x03)) {
    return false;
  }

  return whoAmI != 0x00 && whoAmI != 0xFF;
}

bool readUltrasonicDistanceCm(float& distanceCm) {
  digitalWrite(Config::kUltrasonicTrigPin, LOW);
  delayMicroseconds(2);
  digitalWrite(Config::kUltrasonicTrigPin, HIGH);
  delayMicroseconds(10);
  digitalWrite(Config::kUltrasonicTrigPin, LOW);

  const unsigned long pulseDuration =
      pulseIn(Config::kUltrasonicEchoPin, HIGH, Config::kUltrasonicPulseTimeoutUs);
  if (pulseDuration == 0) {
    return false;
  }

  distanceCm = static_cast<float>(pulseDuration) / 58.0f;
  return isfinite(distanceCm) && distanceCm > 0.0f &&
         distanceCm <= Config::kUltrasonicMaxDistanceCm;
}

void initControllerInputs() {
  analogReadResolution(12);

  for (size_t i = 0; i < sizeof(kStickPins) / sizeof(kStickPins[0]); ++i) {
    analogSetPinAttenuation(kStickPins[i], ADC_11db);
  }

  for (size_t i = 0; i < sizeof(kButtonBindings) / sizeof(kButtonBindings[0]); ++i) {
    pinMode(kButtonBindings[i].pin, INPUT_PULLUP);
  }
}

void calibrateStickCenters() {
  long sums[4] = {0, 0, 0, 0};

  delay(20);
  for (uint8_t sample = 0; sample < Config::kStickCalibrationSamples; ++sample) {
    for (size_t axisIndex = 0; axisIndex < sizeof(kStickPins) / sizeof(kStickPins[0]); ++axisIndex) {
      sums[axisIndex] += analogRead(kStickPins[axisIndex]);
    }
    delay(Config::kStickCalibrationDelayMs);
  }

  for (size_t axisIndex = 0; axisIndex < sizeof(kStickPins) / sizeof(kStickPins[0]); ++axisIndex) {
    gStickCenterRaw[axisIndex] = static_cast<int>(
        sums[axisIndex] / (Config::kStickCalibrationSamples > 0 ? Config::kStickCalibrationSamples : 1));
  }
}

float applyStickDeadzone(float normalizedValue) {
  float magnitude = fabsf(normalizedValue);
  if (magnitude <= Config::kStickDeadzone) {
    return 0.0f;
  }

  const float scaledMagnitude =
      (magnitude - Config::kStickDeadzone) / (1.0f - Config::kStickDeadzone);
  const float signedValue = normalizedValue < 0.0f ? -scaledMagnitude : scaledMagnitude;
  return constrain(signedValue, -1.0f, 1.0f);
}

float normalizeStickAxis(int rawValue, int centerValue, bool invert) {
  const int clampedCenter = constrain(centerValue, 1, Config::kAnalogMax - 1);
  const float delta = static_cast<float>(rawValue - clampedCenter);
  const float range = delta >= 0.0f
      ? static_cast<float>(Config::kAnalogMax - clampedCenter)
      : static_cast<float>(clampedCenter);

  float normalized = range > 1.0f ? delta / range : 0.0f;
  normalized = constrain(normalized, -1.0f, 1.0f);
  normalized = applyStickDeadzone(normalized);

  return invert ? -normalized : normalized;
}

int16_t encodeStickAxis(float normalizedValue) {
  const float clampedValue = constrain(normalizedValue, -1.0f, 1.0f);
  return static_cast<int16_t>(lrintf(clampedValue * 1000.0f));
}

uint16_t readControllerButtonsMask() {
  uint16_t buttonsMask = 0;

  for (size_t i = 0; i < sizeof(kButtonBindings) / sizeof(kButtonBindings[0]); ++i) {
    if (digitalRead(kButtonBindings[i].pin) == LOW) {
      buttonsMask |= kButtonBindings[i].mask;
    }
  }

  return buttonsMask;
}

void resetCalibrationStats() {
  gCalibration = CalibrationStats{};
}

void startCalibration() {
  resetCalibrationStats();
  gCalibrationStartMs = millis();
  gMode = RunMode::Calibrating;
  gThrustLatched = false;
  gLastForce = 0.0f;
  gPendingReadyPulse = false;
}

void finishCalibration() {
  if (gCalibration.sampleCount == 0) {
    gMode = RunMode::Idle;
    return;
  }

  gAccelYOffset = gCalibration.sumY / static_cast<float>(gCalibration.sampleCount);
  const float positiveNoise = gCalibration.maxY - gAccelYOffset;
  const float negativeNoise = gAccelYOffset - gCalibration.minY;
  gNoiseBandY = max(max(positiveNoise, negativeNoise), Config::kMinimumNoiseBandG);
  gMode = RunMode::Monitoring;
  gThrustLatched = false;
  gLastForce = 0.0f;
  gPendingReadyPulse = true;
}

float computeForce(float accelYG) {
  const float delta = fabsf(accelYG - gAccelYOffset);
  const float adjustedDelta = max(0.0f, delta - gNoiseBandY);
  const float scaledForce = adjustedDelta * Config::kForceScale;
  return min(Config::kForceCap, scaledForce);
}

void updateThrustLatch(float force) {
  if (!gThrustLatched && force >= Config::kThrustThreshold) {
    gThrustLatched = true;
  } else if (gThrustLatched && force <= Config::kReleaseThreshold) {
    gThrustLatched = false;
  }
}

void logSendFailure(esp_err_t result) {
  static unsigned long lastLogAt = 0;
  const unsigned long now = millis();
  if (now - lastLogAt < 1000) {
    return;
  }

  lastLogAt = now;
  Serial.print("ESPNOW_SEND_FAIL:");
  Serial.println(static_cast<int>(result));
}

void sendSensorPacket(float forceValue, bool forceValid, float distanceCm, bool distanceValid,
                      bool readyPulse) {
  SensorPacket packet{};
  packet.packetType = PacketType::kSensor;
  packet.force = forceValid ? forceValue : -1.0f;
  packet.distanceCm = distanceValid ? distanceCm : 0.0f;
  packet.flags = 0;

  if (gThrustLatched) {
    packet.flags |= PacketFlag::kThrustLatched;
  }
  if (distanceValid) {
    packet.flags |= PacketFlag::kDistanceValid;
  }
  if (readyPulse) {
    packet.flags |= PacketFlag::kReadyPulse;
  }

  const esp_err_t result = esp_now_send(
      Config::kReceiverMac, reinterpret_cast<const uint8_t*>(&packet), sizeof(packet));
  if (result != ESP_OK) {
    logSendFailure(result);
    return;
  }

  if (readyPulse) {
    gPendingReadyPulse = false;
  }
}

void sendControllerPacket() {
  ControllerPacket packet{};
  packet.packetType = PacketType::kController;
  packet.leftX = encodeStickAxis(
      normalizeStickAxis(analogRead(Config::kLeftStickXPin), gStickCenterRaw[0], kStickInvert[0]));
  packet.leftY = encodeStickAxis(
      normalizeStickAxis(analogRead(Config::kLeftStickYPin), gStickCenterRaw[1], kStickInvert[1]));
  packet.rightX = encodeStickAxis(
      normalizeStickAxis(analogRead(Config::kRightStickXPin), gStickCenterRaw[2], kStickInvert[2]));
  packet.rightY = encodeStickAxis(
      normalizeStickAxis(analogRead(Config::kRightStickYPin), gStickCenterRaw[3], kStickInvert[3]));
  packet.buttons = readControllerButtonsMask();

  const esp_err_t result = esp_now_send(
      Config::kReceiverMac, reinterpret_cast<const uint8_t*>(&packet), sizeof(packet));
  if (result != ESP_OK) {
    logSendFailure(result);
  }
}

void updateControllerTask() {
  const unsigned long now = millis();
  if (now - gLastControllerSampleMs < Config::kControllerSampleIntervalMs) {
    return;
  }

  gLastControllerSampleMs = now;
  sendControllerPacket();
}

void updateSensorTask() {
  const unsigned long now = millis();
  if (now - gLastSensorSampleMs < Config::kSensorSampleIntervalMs) {
    return;
  }
  gLastSensorSampleMs = now;

  const bool readyPulseForThisPacket = gPendingReadyPulse;

  float distanceCm = 0.0f;
  const bool distanceValid = readUltrasonicDistanceCm(distanceCm);

  float packetForce = -1.0f;
  bool forceValid = false;

  float accelYG = 0.0f;
  const bool accelValid = gSensorReady && readAccelY(accelYG);

  if (gMode == RunMode::Calibrating) {
    if (accelValid) {
      if (gCalibration.sampleCount == 0) {
        gCalibration.minY = accelYG;
        gCalibration.maxY = accelYG;
      } else {
        gCalibration.minY = min(gCalibration.minY, accelYG);
        gCalibration.maxY = max(gCalibration.maxY, accelYG);
      }

      gCalibration.sumY += accelYG;
      ++gCalibration.sampleCount;

      if (now - gCalibrationStartMs >= Config::kCalibrationDurationMs) {
        finishCalibration();
      }
    }

    sendSensorPacket(packetForce, forceValid, distanceCm, distanceValid, readyPulseForThisPacket);
    return;
  }

  if (gMode == RunMode::Monitoring && accelValid) {
    packetForce = computeForce(accelYG);
    gLastForce = packetForce;
    forceValid = true;
    updateThrustLatch(packetForce);
  } else {
    gThrustLatched = false;
  }

  sendSensorPacket(packetForce, forceValid, distanceCm, distanceValid, readyPulseForThisPacket);
}

void dispatchSerialCommandToken() {
  if (gSerialCommandLength == 3 &&
      gSerialCommandBuffer[0] == 'C' &&
      gSerialCommandBuffer[1] == 'A' &&
      gSerialCommandBuffer[2] == 'L') {
    handleCalibrationCommand();
  }

  gSerialCommandLength = 0;
}

void handleSerialInput() {
  while (Serial.available() > 0) {
    char ch = static_cast<char>(Serial.read());
    if (ch == '\r' || ch == '\n' || ch == ' ' || ch == '\t') {
      if (gSerialCommandLength > 0) {
        dispatchSerialCommandToken();
      }
      continue;
    }

    if (ch >= 'a' && ch <= 'z') {
      ch = ch - 'a' + 'A';
    }

    if (gSerialCommandLength < sizeof(gSerialCommandBuffer)) {
      gSerialCommandBuffer[gSerialCommandLength++] = ch;
    } else {
      gSerialCommandLength = 0;
    }
  }
}

void handleCalibrationCommand() {
  if (!gSensorReady) {
    return;
  }

  startCalibration();
}

void processIncomingPacket(const uint8_t* /*mac*/, const uint8_t* incomingData, int len) {
  if (incomingData == nullptr || len != static_cast<int>(sizeof(CommandPacket))) {
    return;
  }

  CommandPacket packet{};
  memcpy(&packet, incomingData, sizeof(packet));
  if (packet.packetType != PacketType::kCommand) {
    return;
  }

  if (packet.command == CommandId::kCalibrate) {
    handleCalibrationCommand();
  }
}

#if defined(ESP_ARDUINO_VERSION_MAJOR) && ESP_ARDUINO_VERSION_MAJOR >= 3
void onEspNowDataRecv(const esp_now_recv_info_t* recvInfo, const uint8_t* incomingData, int len) {
  const uint8_t* mac = recvInfo != nullptr ? recvInfo->src_addr : nullptr;
  processIncomingPacket(mac, incomingData, len);
}
#else
void onEspNowDataRecv(const uint8_t* mac, const uint8_t* incomingData, int len) {
  processIncomingPacket(mac, incomingData, len);
}
#endif

bool initEspNow() {
  WiFi.mode(WIFI_STA);
  delay(100);

  if (esp_now_init() != ESP_OK) {
    Serial.println("ESPNOW_INIT_FAIL");
    return false;
  }

  esp_now_peer_info_t peerInfo{};
  memcpy(peerInfo.peer_addr, Config::kReceiverMac, sizeof(Config::kReceiverMac));
  peerInfo.channel = 0;
  peerInfo.encrypt = false;

  if (esp_now_add_peer(&peerInfo) != ESP_OK) {
    Serial.println("ESPNOW_PEER_FAIL");
    return false;
  }

  esp_now_register_recv_cb(onEspNowDataRecv);
  return true;
}

void setup() {
  Serial.begin(Config::kSerialBaud);
  Wire.begin(Config::kSdaPin, Config::kSclPin);
  Wire.setClock(Config::kI2cClock);

  pinMode(Config::kUltrasonicTrigPin, OUTPUT);
  pinMode(Config::kUltrasonicEchoPin, INPUT);
  digitalWrite(Config::kUltrasonicTrigPin, LOW);

  initControllerInputs();
  calibrateStickCenters();

  if (!initEspNow()) {
    return;
  }

  gSensorReady = initSensor();
  gLastSensorSampleMs = millis();
  gLastControllerSampleMs = millis();

  if (!gSensorReady) {
    Serial.println("SENSOR_INIT_FAIL");
    return;
  }

  startCalibration();
}

void loop() {
  handleSerialInput();
  updateControllerTask();
  updateSensorTask();
}
