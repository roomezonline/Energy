

#include "global.h"
#include <Preferences.h>
#include "wifi_config_manager.h"
#include "http_client.h"
#include "phase_combiner.h"
#include "config_manager.h"
#include "alarm_manager.h"
#include "outage_buffer.h"
#include "energy_tracker.h"
#include "ota_updater.h"
#include "temp_sensor.h"
#include "oled_display.h"
#include "esp_timer.h"

WiFiConfigManager wifiManager;
HttpClient        httpClient;
PhaseCombiner     combiner;
ConfigManager     configManager;
AlarmManager      alarmManager;
OutageBuffer      outageBuffer;
EnergyTracker     energyTracker;
OTAUpdater        otaUpdater;
TempSensor        tempSensor;
OledDisplay       oledDisplay;
LogManager        logManager;
RtcManager        rtcManager;
EnergyData        _liveData;
unsigned long     lastPublish = 0;
unsigned long     lastRead = 0;
unsigned long     lastDisplay = 0;
bool              _wifiWasConnected = false;
bool              _lastOverheat = false;
bool              _lastHttpOk = false;
unsigned long     _blueBlinkUntil = 0;


String          g_persianTime      = "";
bool            g_debugDisabled    = false;
CalibrationFactors g_cal;            // single set for all 3 phases
bool              g_calEnabled = true;  // default: ON
SensorType        g_sensorType = SENSOR_CLAMP;

// ============================================================
//  Device ID — NVS save/load
// ============================================================
void saveDeviceIdToNvs(const String& id) {
    Preferences prefs;
    prefs.begin("energyCfg", false);
    prefs.putString("deviceId", id);
    prefs.end();
    LOGF("[MAIN] Device ID saved to NVS: %s\n", id.c_str());
}

String loadDeviceIdFromNvs() {
    Preferences prefs;
    prefs.begin("energyCfg", true);
    String id = prefs.getString("deviceId", "");
    prefs.end();
    LOGF("[MAIN] Device ID loaded from NVS: '%s'\n", id.c_str());
    return id;
}


static unsigned long _rgbBootUntil = 0;
static unsigned long _rgbLastToggle = 0;
static int _rgbCycleStep = 0;
static bool _rgbOn = false;

static void _rgbSet(int r, int g, int b) {
    digitalWrite(RGB_R, r ? HIGH : LOW);
    digitalWrite(RGB_G, g ? HIGH : LOW);
    digitalWrite(RGB_B, b ? HIGH : LOW);
}

static void _rgbBoot() {
    _rgbSet(1, 1, 1);
    _rgbBootUntil = millis() + 2000;
}

static void _rgbLoop() {
    unsigned long now = millis();

    // Boot phase (2s white)
    if (_rgbBootUntil > 0) {
        if (now < _rgbBootUntil) { _rgbSet(1, 1, 1); return; }
        _rgbBootUntil = 0;
    }

    
    if (otaUpdater.isUpdateInProgress()) {
        if (now - _rgbLastToggle > 400) {
            _rgbLastToggle = now;
            _rgbCycleStep = (_rgbCycleStep + 1) % 3;
        }
        _rgbSet(_rgbCycleStep == 0 ? 1 : 0, _rgbCycleStep == 1 ? 1 : 0, _rgbCycleStep == 2 ? 1 : 0);
        return;
    }

   
    if (alarmManager.hasDisconnectAlarms() || alarmManager.hasCriticalAlarms()) {
        if (now - _rgbLastToggle > 300) { _rgbLastToggle = now; _rgbOn = !_rgbOn; }
        _rgbSet(_rgbOn ? 1 : 0, 0, 0);
        return;
    }

    
    if (alarmManager.hasWarningAlarms()) {
        if (now - _rgbLastToggle > 1000) { _rgbLastToggle = now; _rgbOn = !_rgbOn; }
        _rgbSet(_rgbOn ? 1 : 0, _rgbOn ? 1 : 0, 0);
        return;
    }

  
    if (_blueBlinkUntil > 0) {
        if (now < _blueBlinkUntil) { _rgbSet(0, 0, 1); return; }
        _blueBlinkUntil = 0;
    }

   
    if (!energyTracker.hasValidTime()) {
        if (now - _rgbLastToggle > 2000) { _rgbLastToggle = now; _rgbOn = !_rgbOn; }
        _rgbSet(_rgbOn ? 1 : 0, _rgbOn ? 1 : 0, _rgbOn ? 1 : 0);
        return;
    }

 
    if (!_lastHttpOk) {
        _rgbSet(1, 0, 0);
        return;
    }

   
    if (WiFi.status() == WL_DISCONNECTED) {
        if (now - _rgbLastToggle > 500) { _rgbLastToggle = now; _rgbOn = !_rgbOn; }
        _rgbSet(0, 0, _rgbOn ? 1 : 0);
        return;
    }

   
    if (!wifiManager.isConnected()) {
        if (now - _rgbLastToggle > 1000) { _rgbLastToggle = now; _rgbOn = !_rgbOn; }
        _rgbSet(_rgbOn ? 1 : 0, _rgbOn ? 1 : 0, 0);
        return;
    }

   
    _rgbSet(0, 1, 0);
}

void setup() {
    Serial.begin(115200);
    Serial.println("\n========================================");
    Serial.println("  Energy Monitor - ESP32 Client");
    Serial.println("========================================");

    WiFi.setSleep(false);
    WiFi.setAutoReconnect(true);

    pinMode(ALARM_LED, OUTPUT);
    digitalWrite(ALARM_LED, LOW);
    pinMode(RGB_R, OUTPUT);
    pinMode(RGB_G, OUTPUT);
    pinMode(RGB_B, OUTPUT);
    pinMode(DISPLAY_WAKE_PIN, INPUT_PULLDOWN);

    configManager.begin();
    configManager.loadCalibration();

    if (g_saveDeviceId) {
        saveDeviceIdToNvs(DEVICE_ID_DEFAULT);
        g_saveDeviceId = false;
    }
    g_deviceId = loadDeviceIdFromNvs();
    if (g_deviceId.length() == 0) {
        Serial.println("[MAIN] FATAL: No device ID in NVS and g_saveDeviceId is false!");
        Serial.println("[MAIN] Set g_saveDeviceId = true and flash with a valid DEVICE_ID_DEFAULT");
        while (1) { digitalWrite(RGB_R, HIGH); delay(500); digitalWrite(RGB_R, LOW); delay(500); }
    }

    alarmManager.begin();
    outageBuffer.begin();
    energyTracker.begin();
    if (outageBuffer.hasPending()) {
        Serial.println("[MAIN] Clearing legacy outage buffer");
        outageBuffer.clear();
    }
    tempSensor.begin();
    rtcManager.begin();
    configTime(GMT_OFFSET_SEC, DAYLIGHT_OFFSET_SEC, "");
    otaUpdater.begin();
    oledDisplay.begin();
    otaUpdater.setDisplay(&oledDisplay);

    // Boot sequence: web address → device ID → WiFi search
    String webHost = API_BASE_URL;
    webHost.replace("https://", "");
    webHost.replace("http://", "");
    oledDisplay.showBootWeb(webHost);
    delay(2500);
    oledDisplay.showBootDeviceId(g_deviceId);
    delay(2500);

    combiner.setDeviceId(g_deviceId);

    httpClient.setLoopCallback([] { wifiManager.loop(); });

    
    wifiManager.begin();           // AP+STA + start trying saved networks
    wifiManager.setLiveDataRef(&_liveData);

    oledDisplay.setWifiStatus("Searching...", 0);
    LOGF("[MAIN] Scanning saved networks...\n");

    unsigned long wifiStart = millis();
    bool wifiConnected = false;
    int lastState = -1;
    String lastConnectingSsid = "";

    while (millis() - wifiStart < 35000) {
        wifiManager.loop();

        if (wifiManager.isConnected()) {
            wifiConnected = true;
            break;
        }

        int state = wifiManager.getStaState();
        String ssid = wifiManager.getConnectingSsid();

        if (state != lastState) {
            if (state == 1 && ssid.length() > 0) {
                lastConnectingSsid = ssid;
                oledDisplay.setWifiStatus(ssid, 1);
                LOGF("[MAIN] Trying %s...\n", ssid.c_str());
            } else if (lastState == 1 && (state == 0 || state == 3)) {
                oledDisplay.setWifiStatus(lastConnectingSsid, 3);
                LOGF("[MAIN] Failed: %s\n", lastConnectingSsid.c_str());
            } else if (state == 0 || state == 3) {
                oledDisplay.setWifiStatus("Searching...", 0);
            }
            lastState = state;
        }

        delay(50);
    }

   
    if (wifiConnected) {
        LOGF("[MAIN] Connected to %s (%s)\n", WiFi.SSID().c_str(), WiFi.localIP().toString().c_str());
        _wifiWasConnected = true;
        oledDisplay.setWifiStatus(WiFi.localIP().toString(), 2);
        delay(2000);

        // Start server for live data API
        wifiManager.startCaptivePortal();

        LOGF("[MAIN] === OTA Check ===\n");
        // Show the update-check splash before the (blocking) OTA check
        oledDisplay.showBootUpdateCheck();
        delay(1500);
        if (otaUpdater.checkForUpdates()) {
            LOGF("[MAIN] Update available — starting OTA\n");
            otaUpdater.performUpdate();   // displays "Update available" + versions + progress
        } else {
            LOGF("[MAIN] No update — showing current version\n");
            oledDisplay.showBootUpToDate(CURRENT_VERSION);
            delay(4000);
        }

        if (rtcManager.needsSync()) {
            LOGF("[MAIN] === RTC NTP Fallback ===\n");
            rtcManager.tryNtpSync();
        }
        if (rtcManager.needsSync()) {
            LOGF("[MAIN] NTP failed — will sync from first server publish\n");
        } else {
            LOGF("[MAIN] RTC synced\n");
        }

        oledDisplay.setNormalMode();
    } else {
        LOGF("[MAIN] No saved network could be connected\n");
        LOGF("[MAIN] Starting captive portal...\n");
        wifiManager.startCaptivePortal();
        oledDisplay.setWifiStatus("", 4);  // "No connection — AP ready on 192.168.4.1"
        delay(3000);

        oledDisplay.setNormalMode();
    }

    LOGF("[MAIN] API target: %s%s\n", API_BASE_URL.c_str(), API_ENDPOINT.c_str());
    LOGF("[MAIN] ========================================\n");
    LOGF("[MAIN] Remapping Serial for PZEM3...\n");
    Serial.flush();
    _rgbBoot();
    delay(50);
    combiner.beginPhaseC();
    g_debugDisabled = true;
}

void loop() {
    _rgbLoop();
    wifiManager.loop();
    otaUpdater.loop();

    unsigned long now = millis();

    // OLED refresh at fast cadence (slides + standby eyes animation)
    if (now - lastDisplay >= DISPLAY_REFRESH_MS) {
        lastDisplay = now;
        oledDisplay.loop(_liveData, _lastHttpOk);
    }

    // Wake display from standby when pin 25 goes HIGH
    if (digitalRead(DISPLAY_WAKE_PIN) == HIGH) {
        oledDisplay.wake();
    }

    if (now - lastRead >= 1000) {
        lastRead = now;

        // Read temperature every 5 seconds (before readOne to avoid cache overwrite)
        if (now % 5000 < 1000) {
            _liveData.temperature = tempSensor.read();
        }
        // readOne() returns _cache which resets temperature to 0 — save and restore
        float savedTemp = _liveData.temperature;
        _liveData = combiner.readOne();
        _liveData.temperature = savedTemp;

     
        float thresh = configManager.getTemperatureThreshold();
        if (savedTemp > thresh) {
            if (!_lastOverheat) { _lastOverheat = true; digitalWrite(ALARM_LED, HIGH); }
        } else if (savedTemp < thresh - 2.0f) {
            if (_lastOverheat) { _lastOverheat = false; digitalWrite(ALARM_LED, LOW); }
        }

        alarmManager.checkAlarms(_liveData, configManager);

        energyTracker.update(0, _liveData.phaseA.energy);
        energyTracker.update(1, _liveData.phaseB.energy);
        energyTracker.update(2, _liveData.phaseC.energy);
    }

    
    if (!wifiManager.isConnected()) {
        _lastHttpOk = false;
        return;
    }

    _wifiWasConnected = true;
    unsigned long interval = configManager.getPublishIntervalMs();
    unsigned long retryInterval = min((unsigned long)3000, interval); // fast retry on failure
    if (now - lastPublish >= (_lastHttpOk ? interval : retryInterval)) {
        lastPublish = now;

        wifiManager.loop();

        // Copy computed deltas into live data for transmission
        _liveData.phaseA.delta = energyTracker.delta(0);
        _liveData.phaseB.delta = energyTracker.delta(1);
        _liveData.phaseC.delta = energyTracker.delta(2);

        String json = _liveData.toJson();

        String responseBody;
        bool ok = httpClient.postData(g_deviceId, json, responseBody);
        wifiManager.loop();

        _lastHttpOk = ok;
        _blueBlinkUntil = millis() + 200;
        configManager.setServerReachable(ok);

        if (ok) {
            energyTracker.onSuccess();
            if (responseBody.length() > 0)
                configManager.parseAndApply(responseBody);
        }
    }
}
