#include <Wire.h>
#include <WiFi.h>
#include <esp_now.h>
#include <math.h>
#include <string.h>

/*
  Handheld ESP32 sender
  - MPU6050 on SDA=21, SCL=22
  - Ultrasonic TRIG=5, ECHO=18
  - Sends SensorPacket over ESP-NOW every 50 ms
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
constexpr uint32_t kSampleIntervalMs = 50;
constexpr uint32_t kCalibrationDurationMs = 350;
constexpr float kAccelScaleLsbPerG = 16384.0f;
constexpr float kMinimumNoiseBandG = 0.015f;
constexpr float kForceScale = 40.0f;
constexpr float kForceCap = 50.0f;
constexpr float kThrustThreshold = 35.0f;
constexpr float kReleaseThreshold = 18.0f;
constexpr uint8_t kUltrasonicTrigPin = 5;
constexpr uint8_t kUltrasonicEchoPin = 18;
constexpr uint32_t kUltrasonicPulseTimeoutUs = 5000;
constexpr float kUltrasonicMaxDistanceCm = 80.0f;
constexpr uint8_t kReceiverMac[6] = {
    0x30, 0x76, 0xF5, 0xF8, 0xFF, 0xF0};
}

namespace PacketType {
constexpr uint8_t kSensor = 1;
constexpr uint8_t kCommand = 2;
}

namespace CommandId {
constexpr uint8_t kCalibrate = 1;
}

namespace PacketFlag {
constexpr uint8_t kThrustLatched = 1 << 0;
constexpr uint8_t kDistanceValid = 1 << 1;
constexpr uint8_t kReadyPulse = 1 << 2;
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

static_assert(sizeof(SensorPacket) == 10, "Unexpected SensorPacket size");
static_assert(sizeof(CommandPacket) == 2, "Unexpected CommandPacket size");

struct CalibrationStats {
  float sumY = 0.0f;
  float minY = 0.0f;
  float maxY = 0.0f;
  uint32_t sampleCount = 0;
};

enum class RunMode {
  Idle,
  Calibrating,
  Monitoring
};

RunMode gMode = RunMode::Idle;
CalibrationStats gCalibration;
unsigned long gLastSampleMs = 0;
unsigned long gCalibrationStartMs = 0;
float gAccelYOffset = 0.0f;
float gNoiseBandY = Config::kMinimumNoiseBandG;
float gLastForce = 0.0f;
bool gSensorReady = false;
bool gThrustLatched = false;
bool gPendingReadyPulse = false;
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

void updateSensorTask() {
  const unsigned long now = millis();
  if (now - gLastSampleMs < Config::kSampleIntervalMs) {
    return;
  }
  gLastSampleMs = now;

  const bool readyPulseForThisPacket = gPendingReadyPulse;

  float distanceCm = 0.0f;
  const bool distanceValid = readUltrasonicDistanceCm(distanceCm);

  float packetForce = -1.0f;
  bool forceValid = false;

  float accelYG = 0.0f;
  const bool accelValid = readAccelY(accelYG);

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

  if (!initEspNow()) {
    return;
  }

  gSensorReady = initSensor();
  gLastSampleMs = millis();

  if (!gSensorReady) {
    Serial.println("SENSOR_INIT_FAIL");
    return;
  }

  startCalibration();
}

void loop() {
  handleSerialInput();

  if (!gSensorReady) {
    return;
  }

  updateSensorTask();
}
