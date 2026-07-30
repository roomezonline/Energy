#ifndef ALARM_MANAGER_H
#define ALARM_MANAGER_H

#include <Arduino.h>
#include <Preferences.h>
#include "global.h"
#include "energy_data.h"
#include "config_manager.h"

class AlarmManager {
public:
    void begin() {
        _prefs.begin("energyAlarm", false);
        _alarmCount = _prefs.getUChar("count", 0);
        _writeIdx = _prefs.getUChar("idx", 0);
        _loadCache();
        for (int i = 0; i < MAX_ALARMS; i++) {
            if (_slotValid[i] && !_slotResolved[i] &&
                strcmp(_titleCache[i], "Phase Disconnected") == 0) {
                const char* ph = _phaseCache[i];
                if (strcmp(ph, "A") == 0) _disconnectAlarmActive[0] = true;
                else if (strcmp(ph, "B") == 0) _disconnectAlarmActive[1] = true;
                else if (strcmp(ph, "C") == 0) _disconnectAlarmActive[2] = true;
            }
        }
        _syncDisconnectFlag();
    }

    void checkAlarms(const EnergyData& data, ConfigManager& config) {
        // Wait for all phases to be read at least once after boot (30s timeout)
        if (millis() < _bootWaitMs) return;

        int phaseCount = config.getPhaseCount();

        // Resolve stale disconnect alarms for disabled phases (e.g. phaseCount changed)
        for (int i = phaseCount; i < 3; i++) {
            static const char* phaseNames[3] = {"A", "B", "C"};
            if (_disconnectAlarmActive[i]) {
                _resolveIfExists("Phase Disconnected", phaseNames[i]);
                _disconnectAlarmActive[i] = false;
                _prevConnected[i] = true;
                _syncDisconnectFlag();
            }
        }

        const PhaseData* phases[3] = {&data.phaseA, &data.phaseB, &data.phaseC};
        static const char* phaseNames[3] = {"A", "B", "C"};

        float connectedVoltages[3];
        int connectedCount = 0;

        for (int i = 0; i < phaseCount; i++) {
            const auto& p = *phases[i];
            const char* ph = phaseNames[i];

            if (p.connected) {
                if (!_seenConnected[i]) {
                    _seenConnected[i] = true;
                    _prevConnected[i] = true;
                } else if (!_prevConnected[i]) {
                    _resolveIfExists("Phase Disconnected", ph);
                    _disconnectAlarmActive[i] = false;
                    _syncDisconnectFlag();
                    _addIfNew("Phase Reconnected",
                        String("Phase ") + ph + " reconnected", "Info", ph);
                    _resolveIfExists("Phase Reconnected", ph);
                }
            } else {
                if (_seenConnected[i] && _prevConnected[i]) {
                    _addIfNew("Phase Disconnected",
                        String("Phase ") + ph + " is disconnected", "Critical", ph);
                    _disconnectAlarmActive[i] = true;
                    _syncDisconnectFlag();
                } else if (!_seenConnected[i] && millis() >= 10000) {
                    _addIfNew("Phase Disconnected",
                        String("Phase ") + ph + " is disconnected", "Critical", ph);
                    _disconnectAlarmActive[i] = true;
                    _syncDisconnectFlag();
                    _seenConnected[i] = true;
                }
            }
            _prevConnected[i] = p.connected;

            if (p.connected && p.voltage > config.getOverVoltageThreshold()) {
                _addIfNew("Over Voltage",
                    String("Phase ") + ph + ": " + String(p.voltage, 1) + "V > " + String(config.getOverVoltageThreshold(), 1) + "V",
                    "Critical", ph);
            } else {
                _resolveIfExists("Over Voltage", ph);
            }

            if (p.connected && p.voltage > 0 && p.voltage < config.getUnderVoltageThreshold()) {
                _addIfNew("Under Voltage",
                    String("Phase ") + ph + ": " + String(p.voltage, 1) + "V < " + String(config.getUnderVoltageThreshold(), 1) + "V",
                    "Critical", ph);
            } else {
                _resolveIfExists("Under Voltage", ph);
            }

            if (p.connected && p.current > config.getOverCurrentThreshold()) {
                _addIfNew("Over Current",
                    String("Phase ") + ph + ": " + String(p.current, 2) + "A > " + String(config.getOverCurrentThreshold(), 1) + "A",
                    "Warning", ph);
            } else {
                _resolveIfExists("Over Current", ph);
            }

            if (p.connected && p.power > config.getHighPowerThreshold()) {
                _addIfNew("High Power",
                    String("Phase ") + ph + ": " + String(p.power, 0) + "W > " + String(config.getHighPowerThreshold(), 0) + "W",
                    "Warning", ph);
            } else {
                _resolveIfExists("High Power", ph);
            }

            if (p.connected && p.pf > 0 && p.pf < config.getLowPFThreshold()) {
                _addIfNew("Low PF",
                    String("Phase ") + ph + ": PF=" + String(p.pf, 2) + " < " + String(config.getLowPFThreshold(), 2),
                    "Warning", ph);
            } else {
                _resolveIfExists("Low PF", ph);
            }

            if (p.connected) {
                connectedVoltages[connectedCount++] = p.voltage;
            }
        }

        if (connectedCount >= 2) {
            float vMin = connectedVoltages[0], vMax = connectedVoltages[0];
            for (int i = 1; i < connectedCount; i++) {
                if (connectedVoltages[i] < vMin) vMin = connectedVoltages[i];
                if (connectedVoltages[i] > vMax) vMax = connectedVoltages[i];
            }
            float imb = vMax - vMin;
            if (imb > config.getPhaseImbalanceThreshold()) {
                _addIfNew("Phase Imbalance",
                    String("Imbalance: ") + String(imb, 1) + "V > " + String(config.getPhaseImbalanceThreshold(), 1) + "V",
                    "Warning", "All");
            } else {
                _resolveIfExists("Phase Imbalance", "All");
            }
        } else if (connectedCount < 2) {
            _resolveIfExists("Phase Imbalance", "All");
        }

        if (data.frequency > 0) {
            bool freqLow = data.frequency < config.getFreqMinThreshold();
            bool freqHigh = data.frequency > config.getFreqMaxThreshold();
            if (freqLow || freqHigh) {
                _addIfNew("Invalid Frequency",
                    String("Freq: ") + String(data.frequency, 2) + "Hz (range: " + String(config.getFreqMinThreshold(), 1) + "-" + String(config.getFreqMaxThreshold(), 1) + "Hz)",
                    "Critical", "");
            } else {
                _resolveIfExists("Invalid Frequency", "");
            }
        }
    }

    String getRecentJson(int maxCount = 5) {
        String json;
        json.reserve(maxCount * 120);
        json += "[";
        int count = 0;
        int idx = _writeIdx;
        for (int i = 0; i < _alarmCount && count < maxCount; i++) {
            idx = (idx - 1 + MAX_ALARMS) % MAX_ALARMS;
            String val = _readRaw(idx);
            if (val.length() > 0) {
                if (count > 0) json += ",";
                json += val;
                count++;
            }
        }
        json += "]";
        return json;
    }

    int getActiveCount() {
        int active = 0;
        for (int i = 0; i < MAX_ALARMS; i++) {
            if (_slotValid[i] && !_slotResolved[i]) active++;
        }
        return active;
    }

    bool hasAlarms() const { return _alarmCount > 0; }

    bool hasDisconnectAlarms() const {
        return _hasDisconnectAlarm;
    }
    int getTotalCount() const { return _alarmCount; }

    void clearAll() {
        _prefs.putUChar("count", 0);
        _prefs.putUChar("idx", 0);
        for (int i = 0; i < MAX_ALARMS; i++) {
            String key = "alarm" + String(i);
            _prefs.remove(key.c_str());
            _slotValid[i] = false;
            _slotResolved[i] = false;
        }
        _alarmCount = 0;
        _writeIdx = 0;
        for (int i = 0; i < 3; i++) {
            _disconnectAlarmActive[i] = false;
            _prevConnected[i] = false;
            _seenConnected[i] = false;
        }
        _syncDisconnectFlag();
        LOGD("[ALARM] All alarms cleared");
    }

    bool hasCriticalAlarms() const {
        for (int i = 0; i < MAX_ALARMS; i++) {
            if (_slotValid[i] && !_slotResolved[i] &&
                strcmp(_severityCache[i], "Critical") == 0) return true;
        }
        return false;
    }

    bool hasWarningAlarms() const {
        for (int i = 0; i < MAX_ALARMS; i++) {
            if (_slotValid[i] && !_slotResolved[i] &&
                strcmp(_severityCache[i], "Warning") == 0) return true;
        }
        return false;
    }

    bool hasAnyAlarm() const {
        for (int i = 0; i < MAX_ALARMS; i++) {
            if (_slotValid[i] && !_slotResolved[i]) return true;
        }
        return false;
    }

private:
    Preferences _prefs;
    uint8_t _alarmCount = 0;
    uint8_t _writeIdx = 0;
    bool _slotValid[MAX_ALARMS] = {};
    bool _slotResolved[MAX_ALARMS] = {};
    char _titleCache[MAX_ALARMS][32] = {};
    char _phaseCache[MAX_ALARMS][8] = {};
    char _severityCache[MAX_ALARMS][16] = {};
    bool _prevConnected[3] = {false, false, false};
    volatile bool _disconnectAlarmActive[3] = {false, false, false};
    volatile bool _hasDisconnectAlarm = false;
    void _syncDisconnectFlag() { _hasDisconnectAlarm = _disconnectAlarmActive[0] || _disconnectAlarmActive[1] || _disconnectAlarmActive[2]; }
    bool _seenConnected[3] = {false, false, false};
    unsigned long _bootWaitMs = 30000;

    void _loadCache() {
        for (int i = 0; i < MAX_ALARMS; i++) {
            _slotValid[i] = false;
            _slotResolved[i] = false;
            _titleCache[i][0] = '\0';
            _phaseCache[i][0] = '\0';
            _severityCache[i][0] = '\0';
            String raw = _readRaw(i);
            if (raw.length() == 0) continue;
            _slotValid[i] = true;
            _slotResolved[i] = _isResolved(raw);
            // Extract and cache title/phase/severity from NVS (done once at boot / after changes)
            String t = _extractStr(raw, "\"t\":\"");
            String p = _extractStr(raw, "\"p\":\"");
            String s = _extractStr(raw, "\"s\":\"");
            strlcpy(_titleCache[i], t.c_str(), sizeof(_titleCache[i]));
            strlcpy(_phaseCache[i], p.c_str(), sizeof(_phaseCache[i]));
            strlcpy(_severityCache[i], s.c_str(), sizeof(_severityCache[i]));
        }
    }

    void _updateSlotCache(int idx, const String& json) {
        _slotValid[idx] = true;
        _slotResolved[idx] = false;
        String t = _extractStr(json, "\"t\":\"");
        String p = _extractStr(json, "\"p\":\"");
        String s = _extractStr(json, "\"s\":\"");
        strlcpy(_titleCache[idx], t.c_str(), sizeof(_titleCache[idx]));
        strlcpy(_phaseCache[idx], p.c_str(), sizeof(_phaseCache[idx]));
        strlcpy(_severityCache[idx], s.c_str(), sizeof(_severityCache[idx]));
    }

    void _markSlotResolved(int idx) {
        _slotResolved[idx] = true;
    }

    bool _isResolved(const String& val) {
        int pos = val.indexOf("\"r\":");
        if (pos < 0) return false;
        pos += 4;
        while (pos < (int)val.length() && val[pos] == ' ') pos++;
        return val.startsWith("true", pos);
    }

    String _readRaw(int idx) {
        String key = "alarm" + String(idx);
        return _prefs.getString(key.c_str(), "");
    }

    // Pure RAM lookup — NO NVS reads
    int _findUnresolved(const String& title, const String& phase) {
        for (int i = 0; i < MAX_ALARMS; i++) {
            if (!_slotValid[i] || _slotResolved[i]) continue;
            if (strcmp(_titleCache[i], title.c_str()) == 0 &&
                strcmp(_phaseCache[i], phase.c_str()) == 0) {
                return i;
            }
        }
        return -1;
    }

    // Pure RAM lookup — NO NVS reads
    int _findResolved(const String& title, const String& phase) {
        for (int i = 0; i < MAX_ALARMS; i++) {
            if (!_slotValid[i] || !_slotResolved[i]) continue;
            if (strcmp(_titleCache[i], title.c_str()) == 0 &&
                strcmp(_phaseCache[i], phase.c_str()) == 0) {
                return i;
            }
        }
        return -1;
    }

    void _resolveIfExists(const String& title, const String& phase) {
        // Fast path: no alarms at all → nothing to resolve
        if (_alarmCount == 0) return;

        int found = _findUnresolved(title, phase);
        if (found < 0) return;
        String key = "alarm" + String(found);
        String raw = _prefs.getString(key.c_str(), "");
        if (raw.length() == 0) return;
        raw.replace("\"r\":false", "\"r\":true");
        String newTs = getTimestamp();
        int tsPos = raw.indexOf("\"ra\":\"");
        if (tsPos >= 0) {
            tsPos += 5;
            int tsEnd = raw.indexOf("\"", tsPos + 1);
            if (tsEnd > tsPos) {
                String before = raw.substring(0, tsPos + 1);
                String after = raw.substring(tsEnd);
                raw = before + newTs + after;
            }
        }
        _prefs.putString(key.c_str(), raw);
        _markSlotResolved(found);
        LOGF("[ALARM] Resolved: %s [%s] at %s\n", title.c_str(), phase.c_str(), newTs.c_str());
    }

    String _buildAlarmJson(const String& title, const String& message,
                           const String& severity, const String& phase) const {
        String ts = getTimestamp();
        String j = "{";
        j += "\"t\":\"" + _escape(title) + "\"";
        j += ",\"m\":\"" + _escape(message) + "\"";
        j += ",\"s\":\"" + severity + "\"";
        j += ",\"p\":\"" + phase + "\"";
        j += ",\"ts\":\"" + ts + "\"";
        j += ",\"ra\":\"\"";
        j += ",\"r\":false";
        j += "}";
        return j;
    }

    void _addIfNew(const String& title, const String& message,
                   const String& severity, const String& phase) {
        // RAM lookup — no NVS reads
        if (_findUnresolved(title, phase) >= 0) return;

        // Reuse a resolved slot (same title+phase)
        // RAM lookup — no NVS reads
        int reuseIdx = _findResolved(title, phase);
        if (reuseIdx >= 0) {
            String json = _buildAlarmJson(title, message, severity, phase);
            String key = "alarm" + String(reuseIdx);
            _prefs.putString(key.c_str(), json);
            _updateSlotCache(reuseIdx, json);
            LOGF("[ALARM] %s [%s] %s (reused)\n", severity.c_str(), phase.c_str(), title.c_str());
            return;
        }

        // Circular buffer — NVS write only (no reads)
        String json = _buildAlarmJson(title, message, severity, phase);
        String key = "alarm" + String(_writeIdx);
        _prefs.putString(key.c_str(), json);
        _updateSlotCache(_writeIdx, json);
        _writeIdx = (_writeIdx + 1) % MAX_ALARMS;
        if (_alarmCount < MAX_ALARMS) _alarmCount++;
        _prefs.putUChar("count", _alarmCount);
        _prefs.putUChar("idx", _writeIdx);
        LOGF("[ALARM] %s [%s] %s\n", severity.c_str(), phase.c_str(), title.c_str());
    }

    String _escape(const String& s) const {
        String out;
        out.reserve(s.length());
        for (size_t i = 0; i < s.length(); i++) {
            char c = s[i];
            if (c == '"' || c == '\\') out += '\\';
            out += c;
        }
        return out;
    }

    String _extractStr(const String& json, const String& key) const {
        int pos = json.indexOf(key);
        if (pos < 0) return "";
        pos += key.length();
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
        return val;
    }
};

#endif
