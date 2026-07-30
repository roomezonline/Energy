#ifndef GLOBAL_H
#define GLOBAL_H

#include <Arduino.h>
#include "log_manager.h"
#include "rtc_manager.h"

// ============================================================
//  Pins
// ============================================================
#define PZEM1_RX    23   // Phase A — Serial1 RX (PZEM TX)
#define PZEM1_TX    22   // Phase A — Serial1 TX (PZEM RX)
#define PZEM2_RX    16   // Phase B — Serial2 RX
#define PZEM2_TX    17   // Phase B — Serial2 TX
#define PZEM3_RX    26   // Phase C — UART0 RX (GPIO26)
#define PZEM3_TX    27   // Phase C — UART0 TX (GPIO27)
#define ALARM_LED    4   // Temperature alert output (HIGH when temp exceeds threshold)
// RGB LED pins (identified by boot blink test)
#define RGB_R       12   // Physical Red   on GPIO12 (swapped with B)
#define RGB_G       14   // Physical Green on GPIO14
#define RGB_B       13   // Physical Blue  on GPIO13 (swapped with R)

// ============================================================
//  Hardware UART assignments
// ============================================================
//  Phase A: Serial1  (pins 23/22  — PZEM004T library via SoftwareSerial constructor)
//  Phase B: Serial2  (pins 16/17  — PZEM004T library via SoftwareSerial constructor)
//  Phase C: Serial   (pins 26/27  — direct Modbus on UART0, not PZEM library)
//  NOTE: Serial is remapped to GPIO26/27 at 9600 baud after beginPhaseC().
//  After that point ALL Serial.printf()/print/println calls send garbage to PZEM3.
//  Use LOGD(...) / LOGF(fmt,...) macros instead (no-op after beginPhaseC).

// ============================================================
//  Server
// ============================================================
// const String  API_BASE_URL       = "http://192.168.1.125:5204";
const String  API_BASE_URL       = "https://energy.aysad.ir";

const String  API_ENDPOINT       = "/api/ingestion/publish";
const String  DEVICE_ID_DEFAULT  = "AYSAD-002";
bool          g_saveDeviceId     = false;  // true → write DEVICE_ID_DEFAULT to NVS at boot
const String  SITE_URL           = "errorservice.ir";

// ============================================================
//  Time zone (for localtime() in period rollover)
// ============================================================
#define GMT_OFFSET_SEC    12600    // Iran UTC+3:30 = 3*3600 + 30*60
#define DAYLIGHT_OFFSET_SEC 0

// ============================================================
//  Timing
// ============================================================
const unsigned long PUBLISH_INTERVAL_MS = 15000;
const unsigned long WINDOW_MS          = 15000;   // window duration for min/max/avg/deltaEnergy aggregation

// ============================================================
//  WiFi AP
// ============================================================
const String  AP_SSID            = "EnergyCal-Setup";
const String  AP_PASSWORD        = "2537025370";  // min 8 chars for WPA2

// ============================================================
//  Limits & thresholds
// ============================================================
const int     MAX_SAVED_NETWORKS = 3;
const int     MAX_FAILS          = 3;     // consecutive failed reads before phase marked disconnected
const int     MAX_ALARMS         = 20;    // circular buffer size in NVS
const int     MAX_HTTP_RESPONSE  = 2048;  // HTTP response buffer size
const float   PZEM_ENERGY_MAX    = 4294967.0f;  // 32-bit register max (kWh)
const float   MAX_ENERGY_DELTA   = 0.5f;        // max kWh increase per second (1800 kW load)

// ============================================================
//  Firmware / OTA
// ============================================================
#define CURRENT_VERSION "2.0"

const char* const OTA_UPDATE_SERVER        = "https://energy.aysad.ir";
const char* const OTA_UPDATE_URL           = "/firmware/energy_monitor/";
const char* const OTA_VERSION_URL          = "/firmware/energy_monitor/version.txt";
const char* const OTA_FIRMWARE_FILENAME    = "energy_monitor.ino.bin";
const unsigned long OTA_CHECK_INTERVAL_MS  = 86400000UL;  // 24 hours

// ============================================================
//  Persian time display (set by config_manager from HTTP response)
// ============================================================
extern String          g_persianTime;         // Persian date/time from server "1405/04/23 15:30:00"

// ============================================================
//  Time utility — all time queries go through the DS3231 RTC
//  The RTC is ONLY synced from the server (on each successful publish)
// ============================================================
inline bool isTimeSynced() {
    return rtcManager.isOk();
}

inline time_t getCurrentTime() {
    return rtcManager.getEpoch();
}

inline String getTimestamp() {
    return rtcManager.getTimestamp();
}

// ============================================================
//  Debug output (disabled after beginPhaseC to avoid PZEM3 noise)
// ============================================================
extern bool g_debugDisabled;

#define LOGD(msg)       do { logManager.add(msg); if (!g_debugDisabled) { Serial.println(msg); } } while(0)
#define LOGD_T(msg)     do { if (!g_debugDisabled) { Serial.print(msg); } } while(0)
#define LOGF(fmt, ...)  do { char _lb[LOG_LINE_MAX]; snprintf(_lb, sizeof(_lb), fmt, ##__VA_ARGS__); logManager.add(_lb); if (!g_debugDisabled) { Serial.print(_lb); } } while(0)

// ============================================================
//  Sensor type
// ============================================================
enum SensorType : uint8_t {
    SENSOR_CLAMP = 0,
    SENSOR_RING = 1
};

// ============================================================
//  Calibration factors (shared for all 3 phases)
//  Stored in NVS under "energyCfg" namespace
//  Default: enabled (g_calEnabled = true)
// ============================================================
struct CalibrationFactors {
    float current;
    float power;
    float pf;
    float energy;
    float offset;
    CalibrationFactors() : current(2.05f), power(2.10f), pf(1.02f), energy(1.08f), offset(0.0f) {}
};

extern CalibrationFactors g_cal;
extern bool g_calEnabled;
extern SensorType g_sensorType;

// ============================================================
//  Device ID — stored in NVS, controlled by g_saveDeviceId
//  g_saveDeviceId = true  → write DEVICE_ID_DEFAULT to NVS at boot
//  g_saveDeviceId = false → read existing device ID from NVS
// ============================================================
String g_deviceId;  // runtime device ID (loaded from NVS or default)

void saveDeviceIdToNvs(const String& id);
String loadDeviceIdFromNvs();

#endif


