#ifndef PHASE_COMBINER_H
#define PHASE_COMBINER_H

#include <Arduino.h>
#include "global.h"
#include "energy_data.h"
#include "phase_a.h"
#include "phase_b.h"
#include "phase_c.h"

class PhaseCombiner {
public:
    PhaseCombiner() { _resetWindow(); }

    void setDeviceId(const String& id) {
        _deviceId = id;
    }

    EnergyData readOne() {
        int idx = _nextPhase;
        switch (idx) {
            case 0:
                _cache.phaseA = _phaseA.read();
                _cache.frequency = _phaseA.readFrequency();
                break;
            case 1:
                _cache.phaseB = _phaseB.read();
                break;
            case 2:
                _cache.phaseC = _phaseC.read();
                break;
        }
        _nextPhase = (idx + 1) % 3;

        _checkWindow();
        _updateWindow(idx);
        _finalizeIfExpired();

        _cache.deviceId = _deviceId;
        _cache.timestamp = getTimestamp();
        return _cache;
    }

    const EnergyData& getCache() const { return _cache; }

    PhaseA& getPhaseA() { return _phaseA; }
    PhaseC& getPhaseC() { return _phaseC; }
    void beginPhaseC() { _phaseC.begin(); }

    void resetEnergy() {
        _phaseA.resetEnergy();
        _phaseB.resetEnergy();
        _phaseC.resetEnergy();
    }

private:
    struct _WindowData {
        float energyStart = 0;
        float lastEnergy = 0;
        float minV = 1e9, maxV = -1e9;
        float minA = 1e9, maxA = -1e9;
        float minW = 1e9, maxW = -1e9;
        float sumV = 0, sumA = 0, sumW = 0;
        int count = 0;
    };

    String _deviceId;
    PhaseA _phaseA;
    PhaseB _phaseB;
    PhaseC _phaseC;
    EnergyData _cache;
    int _nextPhase = 0;

    _WindowData _w[3];
    unsigned long _windowStart = 0;
    bool _windowActive = false;

    PhaseData& _phaseRef(int idx) {
        return idx == 0 ? _cache.phaseA : idx == 1 ? _cache.phaseB : _cache.phaseC;
    }

    WindowAgg& _winRef(int idx) {
        return idx == 0 ? _cache.winA : idx == 1 ? _cache.winB : _cache.winC;
    }

    void _resetWindow() {
        _windowActive = false;
        _windowStart = 0;
        for (int i = 0; i < 3; i++) _w[i] = _WindowData();
    }

    void _checkWindow() {
        if (!_windowActive) {
            _windowActive = true;
            _windowStart = millis();
            for (int i = 0; i < 3; i++) {
                _w[i] = _WindowData();
                _winRef(i).valid = false;
            }
        }
    }

    void _updateWindow(int idx) {
        auto& w = _w[idx];
        auto& pd = _phaseRef(idx);
        if (w.count == 0) w.energyStart = pd.energy;
        w.lastEnergy = pd.energy;
        w.minV = min(w.minV, pd.voltage);
        w.maxV = max(w.maxV, pd.voltage);
        w.minA = min(w.minA, pd.current);
        w.maxA = max(w.maxA, pd.current);
        w.minW = min(w.minW, pd.power);
        w.maxW = max(w.maxW, pd.power);
        w.sumV += pd.voltage;
        w.sumA += pd.current;
        w.sumW += pd.power;
        w.count++;
    }

    void _finalizeIfExpired() {
        if (!_windowActive) return;
        if (millis() - _windowStart < WINDOW_MS) return;

        for (int i = 0; i < 3; i++) {
            auto& w = _w[i];
            auto& wr = _winRef(i);
            if (w.count >= 2) {
                wr.minVoltage = w.minV;
                wr.maxVoltage = w.maxV;
                wr.minCurrent = w.minA;
                wr.maxCurrent = w.maxA;
                wr.minPower = w.minW;
                wr.maxPower = w.maxW;
                wr.avgVoltage = w.sumV / w.count;
                wr.avgCurrent = w.sumA / w.count;
                wr.avgPower = w.sumW / w.count;
                // 15-second window: energy can only increase slightly
                // Negative delta = PZEM noise/failure, not wrap
                float delta = w.lastEnergy - w.energyStart;
                // Max plausible delta for this window: MAX_ENERGY_DELTA × WINDOW_MS / 1000 × phases
                float maxWindowDelta = MAX_ENERGY_DELTA * (WINDOW_MS / 1000.0f);
                if (isnan(delta) || delta < 0 || delta > maxWindowDelta) delta = 0;
                wr.deltaEnergy = delta;
                wr.valid = true;
            } else {
                wr.valid = false;
            }
        }

        _resetWindow();
    }
};

#endif
