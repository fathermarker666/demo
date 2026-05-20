#include <WiFi.h>
#include <esp_now.h>
#include <math.h>
#include <string.h>

/*
  Receiver / Unity bridge ESP32
  - Receives SensorPacket over ESP-NOW
  - Prints Unity-compatible serial lines at 115200 baud
  - Forwards CAL from USB serial back to the handheld controller
*/

namespace Config {
constexpr uint32_t kSerialBaud = 115200;
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

uint8_t gHandheldMac[6] = {};
bool gHasHandheldMac = false;
bool gLastThrustLatched = false;
bool gAwaitingCalibrationReady = false;
char gSerialCommandBuffer[8];
uint8_t gSerialCommandLength = 0;

void processIncomingPacket(const uint8_t* mac, const uint8_t* incomingData, int len);

bool ensurePeerRegistered(const uint8_t* mac) {
  if (mac == nullptr) {
    return false;
  }

  if (esp_now_is_peer_exist(mac)) {
    return true;
  }

  esp_now_peer_info_t peerInfo{};
  memcpy(peerInfo.peer_addr, mac, 6);
  peerInfo.channel = 0;
  peerInfo.encrypt = false;
  return esp_now_add_peer(&peerInfo) == ESP_OK;
}

void rememberHandheldMac(const uint8_t* mac) {
  if (mac == nullptr) {
    return;
  }

  if (gHasHandheldMac && memcmp(gHandheldMac, mac, sizeof(gHandheldMac)) == 0) {
    return;
  }

  memcpy(gHandheldMac, mac, sizeof(gHandheldMac));
  gHasHandheldMac = true;
  ensurePeerRegistered(gHandheldMac);
}

void printReceiverMac() {
  Serial.print("RX_MAC:");
  Serial.println(WiFi.macAddress());
}

void emitForce(float force) {
  Serial.print("FORCE:");
  Serial.println(force, 1);
}

void emitDistance(float distanceCm) {
  Serial.print("DIST:");
  Serial.println(distanceCm, 1);
}

void emitReady() {
  Serial.println("READY");
}

void emitThrustDetected() {
  Serial.println("THRUST_DETECTED");
}

void processSensorPacket(const uint8_t* mac, const SensorPacket& packet) {
  rememberHandheldMac(mac);

  const bool distanceValid = (packet.flags & PacketFlag::kDistanceValid) != 0;
  const bool thrustLatched = (packet.flags & PacketFlag::kThrustLatched) != 0;
  const bool readyPulse = (packet.flags & PacketFlag::kReadyPulse) != 0;
  const bool hasValidForce = isfinite(packet.force) && packet.force >= 0.0f;

  if (distanceValid) {
    emitDistance(packet.distanceCm);
  }

  const bool inferReadyFromForce = gAwaitingCalibrationReady && hasValidForce;
  if (readyPulse || inferReadyFromForce) {
    emitReady();
    gAwaitingCalibrationReady = false;
    gLastThrustLatched = false;
  }

  if (!hasValidForce || gAwaitingCalibrationReady) {
    if (!hasValidForce) {
      gLastThrustLatched = false;
    }
    return;
  }

  emitForce(packet.force);

  if (thrustLatched && !gLastThrustLatched) {
    emitThrustDetected();
  }

  gLastThrustLatched = thrustLatched;
}

void processIncomingPacket(const uint8_t* mac, const uint8_t* incomingData, int len) {
  if (incomingData == nullptr || len != static_cast<int>(sizeof(SensorPacket))) {
    return;
  }

  SensorPacket packet{};
  memcpy(&packet, incomingData, sizeof(packet));
  if (packet.packetType != PacketType::kSensor) {
    return;
  }

  processSensorPacket(mac, packet);
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

void sendCalibrationCommand() {
  if (!gHasHandheldMac) {
    return;
  }

  if (!ensurePeerRegistered(gHandheldMac)) {
    return;
  }

  CommandPacket packet{};
  packet.packetType = PacketType::kCommand;
  packet.command = CommandId::kCalibrate;

  const esp_err_t result = esp_now_send(
      gHandheldMac, reinterpret_cast<const uint8_t*>(&packet), sizeof(packet));
  if (result == ESP_OK) {
    gAwaitingCalibrationReady = true;
    gLastThrustLatched = false;
  }
}

void dispatchSerialCommandToken() {
  if (gSerialCommandLength == 3 &&
      gSerialCommandBuffer[0] == 'C' &&
      gSerialCommandBuffer[1] == 'A' &&
      gSerialCommandBuffer[2] == 'L') {
    sendCalibrationCommand();
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

bool initEspNow() {
  WiFi.mode(WIFI_STA);
  delay(100);

  if (esp_now_init() != ESP_OK) {
    Serial.println("ESPNOW_INIT_FAIL");
    return false;
  }

  esp_now_register_recv_cb(onEspNowDataRecv);
  return true;
}

void setup() {
  Serial.begin(Config::kSerialBaud);
  WiFi.mode(WIFI_STA);
  delay(100);
  printReceiverMac();

  if (!initEspNow()) {
    return;
  }
}

void loop() {
  handleSerialInput();
}
