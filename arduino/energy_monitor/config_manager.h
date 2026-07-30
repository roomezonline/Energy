#ifndef CONFIG_MANAGER_H
#define CONFIG_MANAGER_H

#include <Arduino.h>
#include <Preferences.h>
#include "global.h"

class ConfigManager {
public:
    void begin() {
        _prefs.begin("energyCfg", false);

        _localMode = _prefs.getUChar("localMode", 0) ? true : false;
        _phaseCount = _prefs.getUChar("phaseCount", 3);
        _publishIntervalMs = _prefs.getInt("publishMs", PUBLISH_INTERVAL_MS);
        _overVoltageThreshold = _prefs.getFloat("ovThresh", 253.0);
        _underVoltageThreshold = _prefs.getFloat("uvThresh", 207.0);
        _overCurrentThreshold = _prefs.getFloat("ocThresh", 20.0);
        _phaseImbalanceThreshold = _prefs.getFloat("piThresh", 15.0);
        _lowPFThreshold = _prefs.getFloat("lpThresh", 0.80);
        _freqMinThreshold = _prefs.getFloat("fMinThresh", 49.5);
        _freqMaxThreshold = _prefs.getFloat("fMaxThresh", 50.5);
        _highPowerThreshold = _prefs.getFloat("hpThresh", 5000.0);
        _temperatureThreshold = _prefs.getFloat("tempThresh", 40.0);

        LOGF("[CONFIG] loaded from NVS (localMode=%d, publishMs=%d)\n",
            _localMode, _publishIntervalMs);
    }

    void parseAndApply(const String& json) {
        if (_localMode) {
            LOGD("[CONFIG] Local mode ON — ignoring server config");
            return;
        }

        bool found;
        int iv;
        float fv;

        iv = _extractInt(json, "\"publishIntervalMs\":", found);
        if (found && iv > 0 && iv != _publishIntervalMs) { _publishIntervalMs = iv; _configDirty = true; }

        fv = _extractFloat(json, "\"overVoltage\":", found);
        if (found && fv != _overVoltageThreshold) { _overVoltageThreshold = fv; _configDirty = true; }

        fv = _extractFloat(json, "\"underVoltage\":", found);
        if (found && fv != _underVoltageThreshold) { _underVoltageThreshold = fv; _configDirty = true; }

        fv = _extractFloat(json, "\"overCurrent\":", found);
        if (found && fv != _overCurrentThreshold) { _overCurrentThreshold = fv; _configDirty = true; }

        fv = _extractFloat(json, "\"phaseImbalance\":", found);
        if (found && fv != _phaseImbalanceThreshold) { _phaseImbalanceThreshold = fv; _configDirty = true; }

        fv = _extractFloat(json, "\"lowPF\":", found);
        if (found && fv != _lowPFThreshold) { _lowPFThreshold = fv; _configDirty = true; }

        fv = _extractFloat(json, "\"freqMin\":", found);
        if (found && fv != _freqMinThreshold) { _freqMinThreshold = fv; _configDirty = true; }

        fv = _extractFloat(json, "\"freqMax\":", found);
        if (found && fv != _freqMaxThreshold) { _freqMaxThreshold = fv; _configDirty = true; }

        fv = _extractFloat(json, "\"highPower\":", found);
        if (found && fv != _highPowerThreshold) { _highPowerThreshold = fv; _configDirty = true; }

        fv = _extractFloat(json, "\"temperatureThreshold\":", found);
        if (found && fv != _temperatureThreshold) { _temperatureThreshold = fv; _configDirty = true; }

        iv = _extractInt(json, "\"phaseCount\":", found);
        if (found && iv >= 1 && iv <= 3 && iv != _phaseCount) { _phaseCount = iv; _configDirty = true; }

        // Extract Persian date/time from server response
        bool ptFound;
        String ptStr = _extractStr(json, "\"persianTime\":\"", ptFound);
        if (ptFound && ptStr.length() > 0) {
            g_persianTime = ptStr;
            LOGF("[CONFIG] Persian time extracted: '%s'\n", ptStr.c_str());
        } else {
            LOGF("[CONFIG] Persian time NOT found (found=%d, len=%d)\n", ptFound, ptStr.length());
        }

        // Extract server time from response and sync DS3231 RTC
        bool stFound;
        String stStr = _extractStr(json, "\"serverTime\":\"", stFound);
        if (stFound && stStr.length() >= 19) {
            int y, M, d, h, m, s;
            if (sscanf(stStr.c_str(), "%d-%d-%dT%d:%d:%d", &y, &M, &d, &h, &m, &s) == 6) {
                rtcManager.syncFromServer(y, M, d, h, m, s);
                LOGF("[CONFIG] Server time synced: %s → %04d-%02d-%02dT%02d:%02d:%02d\n",
                    stStr.c_str(), y, M, d, h, m, s);
            } else {
                LOGF("[CONFIG] sscanf FAILED for serverTime: '%s' (len=%d)\n", stStr.c_str(), stStr.length());
            }
        } else {
            LOGF("[CONFIG] serverTime NOT extracted (found=%d, len=%d)\n", stFound, stStr.length());
        }

        if (_publishIntervalMs < 1000) { _publishIntervalMs = PUBLISH_INTERVAL_MS; _configDirty = true; }
        if (_configDirty) {
            _saveToNvs();
            _configDirty = false;
            LOGD("[CONFIG] Updated from server and saved to NVS");
        }
    }

    bool applyConfigJson(const String& json) {
        bool found;
        int iv;

        iv = _extractInt(json, "\"localMode\":", found);
        if (found && (bool)iv != _localMode) { _localMode = (bool)iv; _configDirty = true; }

        iv = _extractInt(json, "\"publishIntervalMs\":", found);
        if (found && iv > 0 && iv != _publishIntervalMs) { _publishIntervalMs = iv; _configDirty = true; }

        iv = _extractInt(json, "\"phaseCount\":", found);
        if (found && iv >= 1 && iv <= 3 && iv != _phaseCount) { _phaseCount = iv; _configDirty = true; }

        float fv;
        fv = _extractFloat(json, "\"overVoltage\":", found);
        if (found && fv != _overVoltageThreshold) { _overVoltageThreshold = fv; _configDirty = true; }

        fv = _extractFloat(json, "\"underVoltage\":", found);
        if (found && fv != _underVoltageThreshold) { _underVoltageThreshold = fv; _configDirty = true; }

        fv = _extractFloat(json, "\"overCurrent\":", found);
        if (found && fv != _overCurrentThreshold) { _overCurrentThreshold = fv; _configDirty = true; }

        fv = _extractFloat(json, "\"phaseImbalance\":", found);
        if (found && fv != _phaseImbalanceThreshold) { _phaseImbalanceThreshold = fv; _configDirty = true; }

        fv = _extractFloat(json, "\"lowPF\":", found);
        if (found && fv != _lowPFThreshold) { _lowPFThreshold = fv; _configDirty = true; }

        fv = _extractFloat(json, "\"freqMin\":", found);
        if (found && fv != _freqMinThreshold) { _freqMinThreshold = fv; _configDirty = true; }

        fv = _extractFloat(json, "\"freqMax\":", found);
        if (found && fv != _freqMaxThreshold) { _freqMaxThreshold = fv; _configDirty = true; }

        fv = _extractFloat(json, "\"highPower\":", found);
        if (found && fv != _highPowerThreshold) { _highPowerThreshold = fv; _configDirty = true; }

        fv = _extractFloat(json, "\"temperatureThreshold\":", found);
        if (found && fv != _temperatureThreshold) { _temperatureThreshold = fv; _configDirty = true; }

        // Calibration — handle sensor type change first
        {
            bool stFound;
            int stVal = _extractInt(json, "\"sensorType\":", stFound);
            if (stFound && stVal != (int)g_sensorType) {
                // Save current calibration to the old type before switching
                _saveCalForType(g_sensorType);
                g_sensorType = (SensorType)stVal;
                // Load the new type's stored calibration
                _loadCalForType(g_sensorType);
                _configDirty = true;
            }
        }

        // Calibration values (applied to the now-active type)
        float cfv;
        cfv = _extractFloat(json, "\"calCurrent\":", found); if (found && cfv != g_cal.current) { g_cal.current = cfv; _calDirty = true; }
        cfv = _extractFloat(json, "\"calPower\":", found);   if (found && cfv != g_cal.power) { g_cal.power = cfv; _calDirty = true; }
        cfv = _extractFloat(json, "\"calPf\":", found);      if (found && cfv != g_cal.pf) { g_cal.pf = cfv; _calDirty = true; }
        cfv = _extractFloat(json, "\"calEnergy\":", found);  if (found && cfv != g_cal.energy) { g_cal.energy = cfv; _calDirty = true; }
        cfv = _extractFloat(json, "\"calOffset\":", found);  if (found && cfv != g_cal.offset) { g_cal.offset = cfv; _calDirty = true; }
        // calEnabled toggle (also sent alone by saveCalOnly, or bundled in saveCalCfg)
        {
            bool ceFound;
            int ceVal = _extractInt(json, "\"calEnabled\":", ceFound);
            if (ceFound && (ceVal != 0) != g_calEnabled) { g_calEnabled = (ceVal != 0); _calDirty = true; }
        }

        if (_publishIntervalMs < 1000) { _publishIntervalMs = PUBLISH_INTERVAL_MS; _configDirty = true; }
        if (_configDirty) {
            _saveToNvs();
            _configDirty = false;
        }
        if (_calDirty) {
            saveCalibration();
            _calDirty = false;
        }
        LOGF("[CONFIG] Applied from web UI (localMode=%d)\n", _localMode);
        return true;
    }

    String toJson() const {
        String j;
        j.reserve(300);
        j += "{";
        j += "\"localMode\":" + String(_localMode ? "true" : "false");
        j += ",\"publishIntervalMs\":" + String(_publishIntervalMs);
        j += ",\"phaseCount\":" + String(_phaseCount);
        j += ",\"overVoltage\":" + String(_overVoltageThreshold, 1);
        j += ",\"underVoltage\":" + String(_underVoltageThreshold, 1);
        j += ",\"overCurrent\":" + String(_overCurrentThreshold, 1);
        j += ",\"phaseImbalance\":" + String(_phaseImbalanceThreshold, 1);
        j += ",\"lowPF\":" + String(_lowPFThreshold, 2);
        j += ",\"freqMin\":" + String(_freqMinThreshold, 1);
        j += ",\"freqMax\":" + String(_freqMaxThreshold, 1);
        j += ",\"highPower\":" + String(_highPowerThreshold, 1);
        j += ",\"temperatureThreshold\":" + String(_temperatureThreshold, 1);
        j += ",\"source\":\"" + String(_localMode ? "local" : "server") + "\"";
        j += ",\"serverReachable\":" + String(_serverReachable ? "true" : "false");
        j += ",\"sensorType\":" + String((int)g_sensorType);
        j += ",\"calEnabled\":" + String(g_calEnabled ? "true" : "false");
        j += ",\"cal\":{";
        j += "\"current\":" + String(g_cal.current, 4);
        j += ",\"power\":" + String(g_cal.power, 4);
        j += ",\"pf\":" + String(g_cal.pf, 4);
        j += ",\"energy\":" + String(g_cal.energy, 4);
        j += ",\"offset\":" + String(g_cal.offset, 4);
        j += "}";
        j += "}";
        return j;
    }

    int getPhaseCount() const { return _phaseCount; }
    int getPublishIntervalMs() const { return _publishIntervalMs; }
    float getOverVoltageThreshold() const { return _overVoltageThreshold; }
    float getUnderVoltageThreshold() const { return _underVoltageThreshold; }
    float getOverCurrentThreshold() const { return _overCurrentThreshold; }
    float getPhaseImbalanceThreshold() const { return _phaseImbalanceThreshold; }
    float getLowPFThreshold() const { return _lowPFThreshold; }
    float getFreqMinThreshold() const { return _freqMinThreshold; }
    float getFreqMaxThreshold() const { return _freqMaxThreshold; }
    float getHighPowerThreshold() const { return _highPowerThreshold; }
    float getTemperatureThreshold() const { return _temperatureThreshold; }
    bool getLocalMode() const { return _localMode; }
    void setServerReachable(bool r) { _serverReachable = r; }
    bool isServerReachable() const { return _serverReachable; }

    void printConfig(Stream& s) const {
        s.printf("[CONFIG] localMode=%d source=%s publishMs=%d\n",
            _localMode, _localMode ? "local" : "server", _publishIntervalMs);
        s.printf("  overV=%.1f underV=%.1f overI=%.1f imbalance=%.1f\n",
            _overVoltageThreshold, _underVoltageThreshold, _overCurrentThreshold, _phaseImbalanceThreshold);
        s.printf("  lowPF=%.2f fMin=%.1f fMax=%.1f highP=%.1f\n",
            _lowPFThreshold, _freqMinThreshold, _freqMaxThreshold, _highPowerThreshold);
    }

    void loadCalibration() {
        // Always load from NVS (regardless of local mode)
        g_sensorType = (SensorType)_prefs.getUChar("sensorType", SENSOR_CLAMP);

        // Load clamp calibration
        float clampCur = _prefs.getFloat("cal_cur", 2.05f);
        float clampPwr = _prefs.getFloat("cal_pwr", 2.10f);
        float clampPf  = _prefs.getFloat("cal_pf",  1.02f);
        float clampEnr = _prefs.getFloat("cal_enr", 1.08f);
        float clampOff = _prefs.getFloat("cal_off", 0.0f);

        // Load ring calibration
        float ringCur = _prefs.getFloat("ring_cal_cur", 1.0f);
        float ringPwr = _prefs.getFloat("ring_cal_pwr", 1.0f);
        float ringPf  = _prefs.getFloat("ring_cal_pf",  1.0f);
        float ringEnr = _prefs.getFloat("ring_cal_enr", 1.0f);
        float ringOff = _prefs.getFloat("ring_cal_off", 0.0f);

        // Set active calibration based on sensor type
        if (g_sensorType == SENSOR_RING) {
            g_cal.current = ringCur; g_cal.power = ringPwr; g_cal.pf = ringPf;
            g_cal.energy = ringEnr; g_cal.offset = ringOff;
        } else {
            g_cal.current = clampCur; g_cal.power = clampPwr; g_cal.pf = clampPf;
            g_cal.energy = clampEnr; g_cal.offset = clampOff;
        }

        g_calEnabled  = _prefs.getUChar("calEnabled", 1);  // default: ON
        LOGF("[CONFIG] Loaded calibration (sensor=%d): cur=%.2f pwr=%.2f pf=%.2f enr=%.2f off=%.3f enabled=%d\n",
             g_sensorType, g_cal.current, g_cal.power, g_cal.pf, g_cal.energy, g_cal.offset, g_calEnabled);
    }

    void saveCalibration() {
        // Save current g_cal to the active type's NVS keys
        _saveCalForType(g_sensorType);
        _prefs.putUChar("sensorType", (uint8_t)g_sensorType);
        _prefs.putUChar("calEnabled", g_calEnabled ? 1 : 0);
        LOGD("[CONFIG] Calibration saved to NVS");
    }

private:
    bool _localMode = false;
    int _phaseCount = 3;
    bool _serverReachable = false;
    bool _configDirty = false;
    bool _calDirty = false;
    int _publishIntervalMs = PUBLISH_INTERVAL_MS;
    float _overVoltageThreshold = 253.0;
    float _underVoltageThreshold = 207.0;
    float _overCurrentThreshold = 20.0;
    float _phaseImbalanceThreshold = 15.0;
    float _lowPFThreshold = 0.80;
    float _freqMinThreshold = 49.5;
    float _freqMaxThreshold = 50.5;
    float _highPowerThreshold = 5000.0;
    float _temperatureThreshold = 40.0;

    Preferences _prefs;

    void _saveToNvs() {
        _prefs.putUChar("localMode", (uint8_t)_localMode);
        _prefs.putUChar("phaseCount", (uint8_t)_phaseCount);
        _prefs.putInt("publishMs", _publishIntervalMs);
        _prefs.putFloat("ovThresh", _overVoltageThreshold);
        _prefs.putFloat("uvThresh", _underVoltageThreshold);
        _prefs.putFloat("ocThresh", _overCurrentThreshold);
        _prefs.putFloat("piThresh", _phaseImbalanceThreshold);
        _prefs.putFloat("lpThresh", _lowPFThreshold);
        _prefs.putFloat("fMinThresh", _freqMinThreshold);
        _prefs.putFloat("fMaxThresh", _freqMaxThreshold);
        _prefs.putFloat("hpThresh", _highPowerThreshold);
        _prefs.putFloat("tempThresh", _temperatureThreshold);
    }

    void _saveCalForType(SensorType t) {
        if (t == SENSOR_RING) {
            _prefs.putFloat("ring_cal_cur", g_cal.current);
            _prefs.putFloat("ring_cal_pwr", g_cal.power);
            _prefs.putFloat("ring_cal_pf",  g_cal.pf);
            _prefs.putFloat("ring_cal_enr", g_cal.energy);
            _prefs.putFloat("ring_cal_off", g_cal.offset);
        } else {
            _prefs.putFloat("cal_cur", g_cal.current);
            _prefs.putFloat("cal_pwr", g_cal.power);
            _prefs.putFloat("cal_pf",  g_cal.pf);
            _prefs.putFloat("cal_enr", g_cal.energy);
            _prefs.putFloat("cal_off", g_cal.offset);
        }
    }

    void _loadCalForType(SensorType t) {
        if (t == SENSOR_RING) {
            g_cal.current = _prefs.getFloat("ring_cal_cur", 1.0f);
            g_cal.power   = _prefs.getFloat("ring_cal_pwr", 1.0f);
            g_cal.pf      = _prefs.getFloat("ring_cal_pf",  1.0f);
            g_cal.energy  = _prefs.getFloat("ring_cal_enr", 1.0f);
            g_cal.offset  = _prefs.getFloat("ring_cal_off", 0.0f);
        } else {
            g_cal.current = _prefs.getFloat("cal_cur", 2.05f);
            g_cal.power   = _prefs.getFloat("cal_pwr", 2.10f);
            g_cal.pf      = _prefs.getFloat("cal_pf",  1.02f);
            g_cal.energy  = _prefs.getFloat("cal_enr", 1.08f);
            g_cal.offset  = _prefs.getFloat("cal_off", 0.0f);
        }
    }

    String _extractCalBlock(const String& json) {
        int pos = json.indexOf("\"cal\":{");
        if (pos < 0) return "";
        int start = pos + 6;  // position of opening {
        int depth = 0;
        for (int i = start; i < (int)json.length(); i++) {
            if (json[i] == '{') depth++;
            else if (json[i] == '}') {
                depth--;
                if (depth == 0) return json.substring(start, i + 1);
            }
        }
        return "";
    }

    int _extractInt(const String& json, const String& key, bool& found) {
        int pos = json.indexOf(key);
        if (pos < 0) { found = false; return 0; }
        found = true;
        pos += key.length();
        while (pos < (int)json.length() && json[pos] == ' ') pos++;
        if (pos + 3 <= (int)json.length() && json.substring(pos, pos + 4) == "true") return 1;
        if (pos + 4 <= (int)json.length() && json.substring(pos, pos + 5) == "false") return 0;
        String val;
        while (pos < (int)json.length() && json[pos] >= '0' && json[pos] <= '9')
            val += json[pos++];
        return val.length() > 0 ? val.toInt() : 0;
    }

    String _extractStr(const String& json, const String& key, bool& found) {
        int pos = json.indexOf(key);
        if (pos < 0) { found = false; return ""; }
        found = true;
        pos += key.length();
        while (pos < (int)json.length() && json[pos] == ' ') pos++;
        if (pos >= (int)json.length()) return "";
        if (json[pos] == '"') {
            pos++;
            String val;
            val.reserve(32);
            while (pos < (int)json.length() && json[pos] != '"') {
                if (json[pos] == '\\' && pos + 1 < (int)json.length()) pos++;
                val += json[pos++];
            }
            return val;
        }
        String val;
        val.reserve(16);
        while (pos < (int)json.length() && json[pos] != ',' && json[pos] != '}') {
            val += json[pos++];
        }
        if (val.length() > 0 && val.charAt(val.length() - 1) == '"') {
            val.remove(val.length() - 1);
        }
        return val;
    }

    float _extractFloat(const String& json, const String& key, bool& found) {
        int pos = json.indexOf(key);
        if (pos < 0) { found = false; return 0.0; }
        found = true;
        pos += key.length();
        while (pos < (int)json.length() && json[pos] == ' ') pos++;
        String val;
        bool dotFound = false;
        while (pos < (int)json.length()) {
            char c = json[pos];
            if ((c >= '0' && c <= '9') || (c == '.' && !dotFound)) {
                val += c;
                if (c == '.') dotFound = true;
                pos++;
            } else break;
        }
        return val.length() > 0 ? val.toFloat() : 0.0;
    }
};

#endif
