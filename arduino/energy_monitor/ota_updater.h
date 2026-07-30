#ifndef OTA_UPDATER_H
#define OTA_UPDATER_H

#include <Arduino.h>
#include <WiFi.h>
#include <HTTPClient.h>
#include <HTTPUpdate.h>
#include <WiFiClientSecure.h>
#include "global.h"
#include "oled_display.h"

class OTAUpdater {
public:
    void begin() {
        _lastCheck = 0;
        LOGD("[OTA] Firmware version: " CURRENT_VERSION);
    }

    void setDisplay(OledDisplay* disp) { _display = disp; }

    void loop() {
        if (_updateInProgress) return;
        unsigned long now = millis();
        if (now - _lastCheck < OTA_CHECK_INTERVAL_MS) return;
        _lastCheck = now;
        checkAndUpdate();
    }

    bool checkForUpdates() {
        if (WiFi.status() != WL_CONNECTED) {
            LOGD("[OTA] No WiFi — skipping update check");
            return false;
        }

        HTTPClient http;
        String versionUrl = String(OTA_UPDATE_SERVER) + String(OTA_VERSION_URL);
        http.begin(versionUrl);
        http.setTimeout(5000);
        int httpCode = http.GET();

        if (httpCode == HTTP_CODE_OK) {
            String newVersion = http.getString();
            newVersion.trim();
            http.end();

            LOGF("[OTA] Current: %s  Server: %s\n", CURRENT_VERSION, newVersion.c_str());

            if (newVersion != CURRENT_VERSION && newVersion.length() > 0) {
                LOGD("[OTA] New firmware available!");
                _newVersion = newVersion;
                return true;
            }
            LOGD("[OTA] Firmware is up to date");
            return false;
        }
        LOGF("[OTA] Version check failed (HTTP %d)\n", httpCode);
        http.end();
        return false;
    }

    void performUpdate() {
        if (WiFi.status() != WL_CONNECTED) {
            LOGD("[OTA] No WiFi — cannot update");
            return;
        }

        _updateInProgress = true;
        LOGD("[OTA] Starting firmware download...");

        String firmwareUrl = String(OTA_UPDATE_SERVER) + String(OTA_UPDATE_URL) + OTA_FIRMWARE_FILENAME;

        if (_display) _display->showOtaStart(_newVersion);

        WiFiClientSecure client;
        client.setInsecure();

        httpUpdate.onStart([this]() {
            LOGD("[OTA] Update started");
        });

        httpUpdate.onEnd([this]() {
            LOGD("[OTA] Update finished");
            if (_display) _display->showOtaDone();
            delay(3000);
        });

        httpUpdate.onProgress([this](int cur, int total) {
            unsigned long now = millis();
            if (total > 0) {
                int pct = (cur * 100) / total;
                if (_display && (now - _lastProgressDisp > 100 || pct >= 100)) {
                    _lastProgressDisp = now;
                    String st = "Downloading...";
                    _display->showOtaProgress(pct, st);
                }
            }
        });

        httpUpdate.onError([this](int err) {
            String msg = "Error " + String(err);
            LOGF("[OTA] Error [%d]: %s\n", err, httpUpdate.getLastErrorString().c_str());
            if (_display) _display->showOtaError(httpUpdate.getLastErrorString());
        });

        LOGF("[OTA] Downloading: %s\n", firmwareUrl.c_str());
        t_httpUpdate_return ret = httpUpdate.update(client, firmwareUrl);

        switch (ret) {
            case HTTP_UPDATE_FAILED:
                LOGF("[OTA] Update failed (%d): %s\n",
                     httpUpdate.getLastError(),
                     httpUpdate.getLastErrorString().c_str());
                _updateInProgress = false;
                delay(5000);
                break;

            case HTTP_UPDATE_NO_UPDATES:
                LOGD("[OTA] No update available");
                _updateInProgress = false;
                break;

            case HTTP_UPDATE_OK:
                LOGD("[OTA] Update OK — rebooting...");
                break;
        }
    }

    void checkAndUpdate() {
        if (checkForUpdates()) {
            LOGD("[OTA] New version found, downloading...");
            performUpdate();
        }
    }

    bool isUpdateInProgress() const { return _updateInProgress; }
    String getNewVersion() const { return _newVersion; }

    void triggerCheck() {
        _lastCheck = 0;
    }

    void forceCheckNow() {
        checkAndUpdate();
    }

private:
    unsigned long _lastCheck = 0;
    unsigned long _lastProgressDisp = 0;
    bool _updateInProgress = false;
    String _newVersion;
    OledDisplay* _display = nullptr;
};

#endif
