#ifndef ENERGY_TRACKER_H
#define ENERGY_TRACKER_H

#include <Arduino.h>
#include <Preferences.h>
#include <math.h>
#include "global.h"

class EnergyTracker {
public:
    void begin() {
        Preferences p;
        p.begin("energy", true);
        for (int i = 0; i < 3; i++) {
            _cur[i]  = p.getDouble(("c" + String(i)).c_str(), 0);
            _snt[i]  = p.getDouble(("s" + String(i)).c_str(), 0);
            _init[i] = p.getBool(("f" + String(i)).c_str(), false);
            _d0[i]   = p.getDouble(("d0" + String(i)).c_str(), 0);
            _m0[i]   = p.getDouble(("m0" + String(i)).c_str(), 0);
            _y0[i]   = p.getDouble(("y0" + String(i)).c_str(), 0);
            _pkA[i]  = p.getFloat(("pA" + String(i)).c_str(), 0);
            _pkW[i]  = p.getFloat(("pW" + String(i)).c_str(), 0);
        }
        _lastDay    = p.getInt("lD", 0);
        _lastMonth  = p.getInt("lM", 0);
        _lastYear   = p.getInt("lY", 0);
        _nvsWearCnt = p.getULong("wear", 0);
        p.end();
        for (int i = 0; i < 3; i++) _firstUpdate[i] = true;
        LOGF("[TRACKER] loaded from NVS (wear count: %lu)\n", _nvsWearCnt);
        _checkNvsWear();
    }

    // returns true if value was accepted
    bool update(int phase, double val) {
        if (phase < 0 || phase > 2) return false;

        if (isnan(val) || isinf(val) || val < 0 || val >= PZEM_ENERGY_MAX)
            return false;

        if (!_init[phase]) {
            _cur[phase] = val;
            _snt[phase] = val;
            _init[phase] = true;
            _firstUpdate[phase] = false;
            _dirtyCur[phase] = _dirtySnt[phase] = _dirtyInit[phase] = true;
            _rolloverCheck();
            _save();
            return true;
        }

        double adj = val;
        if (val < _cur[phase] - 0.001) {
            double drop = _cur[phase] - val;
            if (drop > PZEM_ENERGY_MAX * 0.5)
                adj = val + PZEM_ENERGY_MAX;
            else
                return false;
        }

        // first update after boot: accept any valid value (catch up after power loss)
        if (_firstUpdate[phase]) {
            _firstUpdate[phase] = false;
            _cur[phase] = adj;
            _dirtyCur[phase] = true;
            _rolloverCheck();
            _save();
            return true;
        }

        double d = adj - _cur[phase];
        if (d > MAX_ENERGY_DELTA) {
            LOGF("[TRACKER] Phase %c: rejected delta=%.3f\n", 'A' + phase, d);
            return false;
        }

        _cur[phase] = adj;
        _dirtyCur[phase] = true;
        _rolloverCheck();
        if (millis() - _lastSave > 30000) _save();
        return true;
    }

    double delta(int phase) const {
        if (phase < 0 || phase > 2 || !_init[phase]) return 0;
        double d = _cur[phase] - _snt[phase];
        return (d < 0.001) ? 0 : d;
    }

    void onSuccess() {
        for (int i = 0; i < 3; i++) {
            if (_init[i]) {
                _snt[i] = _cur[i];
                _dirtySnt[i] = true;
            }
        }
        _save();
    }

    void updatePeaks(int phase, float current, float power) {
        if (phase < 0 || phase > 2) return;
        bool chg = false;
        if (current > _pkA[phase]) { _pkA[phase] = current; chg = true; _dirtyPkA[phase] = true; }
        if (power   > _pkW[phase]) { _pkW[phase] = power;   chg = true; _dirtyPkW[phase] = true; }
        if (chg && millis() - _lastSave > 30000) _save();
    }

    float peakCurrent(int phase) const {
        return (phase >= 0 && phase < 3) ? _pkA[phase] : 0;
    }

    float peakPower(int phase) const {
        return (phase >= 0 && phase < 3) ? _pkW[phase] : 0;
    }

    void reset() {
        for (int i = 0; i < 3; i++) {
            _cur[i] = 0; _snt[i] = 0; _init[i] = false;
            _d0[i] = 0; _m0[i] = 0; _y0[i] = 0;
            _pkA[i] = 0; _pkW[i] = 0;
            _dirtyCur[i] = _dirtySnt[i] = _dirtyInit[i] = true;
            _dirtyD0[i] = _dirtyM0[i] = _dirtyY0[i] = true;
            _dirtyPkA[i] = _dirtyPkW[i] = true;
        }
        _lastDay = 0; _lastMonth = 0; _lastYear = 0;
        _dirtyDay = _dirtyMonth = _dirtyYear = true;
        _save();
        LOGD("[TRACKER] reset");
    }

    // full JSON for captive-portal dashboard
    String toJson() {
        String j = "{";
        for (int i = 0; i < 3; i++) {
            if (i > 0) j += ",";
            j += "\"" + String(char('a' + i)) + "\":{";
            j += "\"c\":"  + String(_cur[i], 3);
            j += ",\"d\":"  + String(_period(_cur[i], _d0[i]), 3);
            j += ",\"m\":"  + String(_period(_cur[i], _m0[i]), 3);
            j += ",\"y\":"  + String(_period(_cur[i], _y0[i]), 3);
            j += ",\"pkA\":" + String(_pkA[i], 2);
            j += ",\"pkW\":" + String(_pkW[i], 0);
            j += "}";
        }
        j += "}";
        return j;
    }

    bool hasValidTime() const { return getCurrentTime() >= 100000; }

private:
    double _cur[3]  = {0, 0, 0};
    double _snt[3]  = {0, 0, 0};
    bool   _init[3] = {false, false, false};
    bool   _firstUpdate[3] = {true, true, true};
    double _d0[3]   = {0, 0, 0};
    double _m0[3]   = {0, 0, 0};
    double _y0[3]   = {0, 0, 0};
    float  _pkA[3]  = {0, 0, 0};
    float  _pkW[3]  = {0, 0, 0};
    int    _lastDay   = 0;
    int    _lastMonth = 0;
    int    _lastYear  = 0;
    unsigned long _lastSave = 0;

    // Per-key dirty flags (NVS wear reduction)
    bool _dirtyCur[3]   = {false, false, false};
    bool _dirtySnt[3]   = {false, false, false};
    bool _dirtyInit[3]  = {false, false, false};
    bool _dirtyD0[3]    = {false, false, false};
    bool _dirtyM0[3]    = {false, false, false};
    bool _dirtyY0[3]    = {false, false, false};
    bool _dirtyPkA[3]   = {false, false, false};
    bool _dirtyPkW[3]   = {false, false, false};
    bool _dirtyDay      = false;
    bool _dirtyMonth    = false;
    bool _dirtyYear     = false;
    unsigned long _nvsWearCnt = 0;

    static double _period(double cur, double start) {
        double v = cur - start;
        return (v < 0) ? 0 : v;
    }

    void _rolloverCheck() {
        time_t now = getCurrentTime();
        if (now < 100000) return;
        struct tm* t = localtime(&now);
        if (!t) return;
        int d = t->tm_mday;
        int m = t->tm_mon + 1;
        int y = t->tm_year + 1900;

        bool changed = false;

        if (d != _lastDay) {
            for (int i = 0; i < 3; i++) {
                _d0[i] = _cur[i];
                _pkA[i] = 0;
                _pkW[i] = 0;
                _dirtyD0[i] = _dirtyPkA[i] = _dirtyPkW[i] = true;
            }
            _lastDay = d;
            _dirtyDay = true;
            changed = true;
        }

        if (m != _lastMonth) {
            for (int i = 0; i < 3; i++) {
                _m0[i] = _cur[i];
                _dirtyM0[i] = true;
            }
            _lastMonth = m;
            _dirtyMonth = true;
            changed = true;
        }

        if (y != _lastYear) {
            for (int i = 0; i < 3; i++) {
                _y0[i] = _cur[i];
                _dirtyY0[i] = true;
            }
            _lastYear = y;
            _dirtyYear = true;
            changed = true;
        }

        if (changed) _save();
    }

    void _save() {
        Preferences p;
        p.begin("energy", false);
        bool wrote = false;
        for (int i = 0; i < 3; i++) {
            if (_dirtyCur[i])  { p.putDouble(("c" + String(i)).c_str(),  _cur[i]);  _dirtyCur[i] = false; wrote = true; }
            if (_dirtySnt[i])  { p.putDouble(("s" + String(i)).c_str(),  _snt[i]);  _dirtySnt[i] = false; wrote = true; }
            if (_dirtyInit[i]) { p.putBool(("f" + String(i)).c_str(),   _init[i]); _dirtyInit[i] = false; wrote = true; }
            if (_dirtyD0[i])   { p.putDouble(("d0" + String(i)).c_str(), _d0[i]);   _dirtyD0[i] = false; wrote = true; }
            if (_dirtyM0[i])   { p.putDouble(("m0" + String(i)).c_str(), _m0[i]);   _dirtyM0[i] = false; wrote = true; }
            if (_dirtyY0[i])   { p.putDouble(("y0" + String(i)).c_str(), _y0[i]);   _dirtyY0[i] = false; wrote = true; }
            if (_dirtyPkA[i])  { p.putFloat(("pA" + String(i)).c_str(),  _pkA[i]);  _dirtyPkA[i] = false; wrote = true; }
            if (_dirtyPkW[i])  { p.putFloat(("pW" + String(i)).c_str(),  _pkW[i]);  _dirtyPkW[i] = false; wrote = true; }
        }
        if (_dirtyDay)   { p.putInt("lD", _lastDay);   _dirtyDay = false; wrote = true; }
        if (_dirtyMonth) { p.putInt("lM", _lastMonth); _dirtyMonth = false; wrote = true; }
        if (_dirtyYear)  { p.putInt("lY", _lastYear);  _dirtyYear = false; wrote = true; }
        if (wrote) {
            _nvsWearCnt++;
            p.putULong("wear", _nvsWearCnt);
            _checkNvsWear();
        }
        p.end();
        _lastSave = millis();
    }

    void _checkNvsWear() {
        // Warn at wear milestones: 1K, 5K, 10K, 50K, 100K
        if (_nvsWearCnt == 1000 || _nvsWearCnt == 5000 || _nvsWearCnt == 10000 ||
            _nvsWearCnt == 50000 || _nvsWearCnt == 100000 ||
            (_nvsWearCnt > 0 && _nvsWearCnt % 50000 == 0)) {
            LOGF("[TRACKER] *** NVS wear: %lu writes ***\n", _nvsWearCnt);
        }
    }
};

#endif
