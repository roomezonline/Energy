#ifndef GLOBAL_H
#define GLOBAL_H

#include <Arduino.h>
#include "log_manager.h"
#include "rtc_manager.h"


#define CURRENT_VERSION "4.0"

// ============================================================
//  Pins
// ============================================================
#define PZEM1_RX    23   // Phase A 
#define PZEM1_TX    22   // Phase A 
#define PZEM2_RX    16   // Phase B 
#define PZEM2_TX    17   // Phase B 
#define PZEM3_RX    26   // Phase C 
#define PZEM3_TX    27   // Phase C 
#define ALARM_LED    4   // Temperature alert 
// RGB LED pins 
#define RGB_R       12   //  Red   
#define RGB_G       14   //  Green 
#define RGB_B       13   //  Blue  
#define DS18B20_PIN 32   //  DS18B20 temperature sensor
#define DISPLAY_WAKE_PIN   25   //standby

const String  DEVICE_ID_DEFAULT  = "AYSAD-003";
bool          g_saveDeviceId     = false;  // true → write DEVICE_ID_DEFAULT 

// const String  API_BASE_URL       = "http://192.168.1.125:5204";
const String  API_BASE_URL       = "https://energy.aysad.ir";

const String  API_ENDPOINT       = "/api/ingestion/publish";

const String  SITE_URL           = "errorservice.ir";


#define GMT_OFFSET_SEC    12600    
#define DAYLIGHT_OFFSET_SEC 0


const unsigned long PUBLISH_INTERVAL_MS = 15000;
const unsigned long WINDOW_MS          = 15000;   


const String  AP_SSID            = "EnergyCal-Setup";
const String  AP_PASSWORD        = "2537025370";  

// ============================================================
//  Temperature source
// ============================================================
// true  → read temperature from external DS18B20 (DS18B20_PIN)
// false → read temperature from the ESP32 internal temperature sensor
bool g_useExternalTemp = true;

// The ESP32 internal sensor measures CPU die temp, NOT ambient.
// It is inaccurate (±10 °C or more) and varies per chip — correct
// it with a fixed offset: actual = sensor reading + this offset.
// Example: sensor reads 45 °C when the room is 26 °C → offset = -19.
// (No effect when g_useExternalTemp = true)
#define ESP_INTERNAL_TEMP_OFFSET -19.0f

// ============================================================
//  Display / standby
// ============================================================
#define DISPLAY_REFRESH_MS 100  // OLED refresh cadence in main loop (ms)
#define STANDBY_TIMEOUT_MS 180000UL  // 3 min of slides → standby

// true  → standby mode is enabled (eyes after timeout, wake on pin 25)
// false → standby disabled (display always on)
bool g_standbyEnabled = true;


const int     MAX_SAVED_NETWORKS = 3;
const int     MAX_FAILS          = 3;    
const int     MAX_ALARMS         = 20;   
const int     MAX_HTTP_RESPONSE  = 2048;  
const float   PZEM_ENERGY_MAX    = 4294967.0f;  
const float   MAX_ENERGY_DELTA   = 0.5f;        




const char* const OTA_UPDATE_SERVER        = "https://energy.aysad.ir";
const char* const OTA_UPDATE_URL           = "/firmware/energy_monitor/";
const char* const OTA_VERSION_URL          = "/firmware/energy_monitor/version.txt";
const char* const OTA_FIRMWARE_FILENAME    = "energy_monitor.ino.bin";
const unsigned long OTA_CHECK_INTERVAL_MS  = 86400000UL;  // 24 hours


extern String          g_persianTime;         


inline bool isTimeSynced() {
    return rtcManager.isOk();
}

inline time_t getCurrentTime() {
    return rtcManager.getEpoch();
}

inline String getTimestamp() {
    return rtcManager.getTimestamp();
}


extern bool g_debugDisabled;

#define LOGD(msg)       do { logManager.add(msg); if (!g_debugDisabled) { Serial.println(msg); } } while(0)
#define LOGD_T(msg)     do { if (!g_debugDisabled) { Serial.print(msg); } } while(0)
#define LOGF(fmt, ...)  do { char _lb[LOG_LINE_MAX]; snprintf(_lb, sizeof(_lb), fmt, ##__VA_ARGS__); logManager.add(_lb); if (!g_debugDisabled) { Serial.print(_lb); } } while(0)


enum SensorType : uint8_t {
    SENSOR_CLAMP = 0,
    SENSOR_RING = 1
};


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


String g_deviceId;  

void saveDeviceIdToNvs(const String& id);
String loadDeviceIdFromNvs();

#endif


