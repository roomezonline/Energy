#ifndef OUTAGE_BUFFER_H
#define OUTAGE_BUFFER_H

#include <Preferences.h>
#include "global.h"
#include "energy_data.h"

class OutageBuffer {
public:
    void begin() {
        Preferences prefs;
        prefs.begin("energyOutage", true);
        _hasPending = prefs.getBool("out_pend", false);
        if (_hasPending) {
            _data.totalDeltaA  = prefs.getFloat("out_dA", 0);
            _data.totalDeltaB  = prefs.getFloat("out_dB", 0);
            _data.totalDeltaC  = prefs.getFloat("out_dC", 0);
            _data.maxPeakA     = prefs.getFloat("out_pA", 0);
            _data.maxPeakB     = prefs.getFloat("out_pB", 0);
            _data.maxPeakC     = prefs.getFloat("out_pC", 0);
            _data.maxPowerA    = prefs.getFloat("out_wA", 0);
            _data.maxPowerB    = prefs.getFloat("out_wB", 0);
            _data.maxPowerC    = prefs.getFloat("out_wC", 0);
            _data.startEpoch   = prefs.getUInt("out_start", 0);
            _data.inOutage = true;
        }
        prefs.end();
    }

    void addWindow(EnergyData& live) {
        bool any = false;
        auto add = [&](WindowAgg& w,
            float& total, float& peakA, float& peakW) {
            if (!w.valid) return;
            total += w.deltaEnergy;
            if (w.maxCurrent > peakA) peakA = w.maxCurrent;
            if (w.maxPower > peakW) peakW = w.maxPower;
            any = true;
        };
        add(live.winA, _data.totalDeltaA, _data.maxPeakA, _data.maxPowerA);
        add(live.winB, _data.totalDeltaB, _data.maxPeakB, _data.maxPowerB);
        add(live.winC, _data.totalDeltaC, _data.maxPeakC, _data.maxPowerC);
        if (!any) return;

        if (!_data.inOutage) {
            _data.inOutage = true;
            _data.startEpoch = (uint32_t)getCurrentTime();
            _writeNvs();
        }
    }

    void save() {
        if (_data.inOutage && (millis() - _lastNvsSave > 300000))
            _writeNvs();
    }

    bool hasPending() const { return _hasPending || _data.inOutage; }

    void populate(OutagePayload& out) {
        out.hasData = true;
        out.outageStartEpoch = _data.startEpoch;
        out.totalDeltaA = _data.totalDeltaA;
        out.totalDeltaB = _data.totalDeltaB;
        out.totalDeltaC = _data.totalDeltaC;
        out.maxPeakA = _data.maxPeakA;
        out.maxPeakB = _data.maxPeakB;
        out.maxPeakC = _data.maxPeakC;
        out.maxPowerA = _data.maxPowerA;
        out.maxPowerB = _data.maxPowerB;
        out.maxPowerC = _data.maxPowerC;
    }

    void clear() {
        _data = _OutageData();
        _hasPending = false;
        Preferences prefs;
        prefs.begin("energyOutage", false);
        prefs.putBool("out_pend", false);
        prefs.end();
    }

    String getStatusJson() const {
        String j = "{";
        bool pend = _hasPending || _data.inOutage;
        j += "\"pending\":" + String(pend ? "true" : "false");
        if (pend) {
            j += ",\"epoch\":" + String(_data.startEpoch);
            j += ",\"dA\":" + String(_data.totalDeltaA, 4);
            j += ",\"dB\":" + String(_data.totalDeltaB, 4);
            j += ",\"dC\":" + String(_data.totalDeltaC, 4);
            j += ",\"tot\":" + String(
                _data.totalDeltaA + _data.totalDeltaB + _data.totalDeltaC, 4);
            j += ",\"pA\":" + String(_data.maxPeakA, 3);
            j += ",\"pB\":" + String(_data.maxPeakB, 3);
            j += ",\"pC\":" + String(_data.maxPeakC, 3);
            j += ",\"wA\":" + String(_data.maxPowerA, 1);
            j += ",\"wB\":" + String(_data.maxPowerB, 1);
            j += ",\"wC\":" + String(_data.maxPowerC, 1);
        }
        j += "}";
        return j;
    }

private:
    struct _OutageData {
        float totalDeltaA = 0, totalDeltaB = 0, totalDeltaC = 0;
        float maxPeakA = 0, maxPeakB = 0, maxPeakC = 0;
        float maxPowerA = 0, maxPowerB = 0, maxPowerC = 0;
        uint32_t startEpoch = 0;
        bool inOutage = false;
    };

    _OutageData _data;
    bool _hasPending = false;
    unsigned long _lastNvsSave = 0;

    void _writeNvs() {
        Preferences prefs;
        prefs.begin("energyOutage", false);
        prefs.putBool("out_pend", true);
        prefs.putFloat("out_dA", _data.totalDeltaA);
        prefs.putFloat("out_dB", _data.totalDeltaB);
        prefs.putFloat("out_dC", _data.totalDeltaC);
        prefs.putFloat("out_pA", _data.maxPeakA);
        prefs.putFloat("out_pB", _data.maxPeakB);
        prefs.putFloat("out_pC", _data.maxPeakC);
        prefs.putFloat("out_wA", _data.maxPowerA);
        prefs.putFloat("out_wB", _data.maxPowerB);
        prefs.putFloat("out_wC", _data.maxPowerC);
        prefs.putUInt("out_start", _data.startEpoch);
        prefs.end();
        _lastNvsSave = millis();
        _hasPending = true;
    }
};

#endif
