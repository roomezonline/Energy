#ifndef PHASE_B_H
#define PHASE_B_H

#include <PZEM004Tv30.h>
#include "global.h"
#include "energy_data.h"

class PhaseB {
public:
    PhaseB() : _pzem(Serial2, PZEM2_RX, PZEM2_TX) {}

    PhaseData read() {
        PhaseData d = _last;

        // If disconnected, skip blocking PZEM reads — retry every 30s
        if (_failCount >= MAX_FAILS && (int32_t)(millis() - _nextRetry) < 0) {
            d.connected = false;
            d.voltage = 0; d.current = 0; d.power = 0; d.pf = 0;
            d.energy = _last.energy;
            _last = d;
            return d;
        }

        bool gotAny = false;

        float v = _pzem.voltage();
        if (!isnan(v) && v > 1 && v < 300) { d.voltage = v; gotAny = true; }

        float c = _pzem.current();
        if (!isnan(c) && c >= 0 && c < 100) { d.current = c; }

        float p = _pzem.power();
        if (!isnan(p) && p >= 0 && p < 30000) { d.power = p; }

        float e = _pzem.energy();
        if (!isnan(e) && e >= 0 && e < PZEM_ENERGY_MAX) { d.energy = e; }

        float pf = _pzem.pf();
        if (!isnan(pf) && pf >= 0 && pf <= 1) { d.pf = pf; }

        if (g_calEnabled) {
            d.current = max(0.0f, d.current * g_cal.current + g_cal.offset);
            d.power   = max(0.0f, d.power   * g_cal.power);
            d.energy  = max(0.0f, d.energy  * g_cal.energy);
            float apparent = d.voltage * d.current;
            d.pf = apparent > 0 ? constrain(d.power / apparent, 0.0f, 1.0f) : 0.0f;
        }

        if (gotAny) {
            _failCount = 0;
            d.connected = true;
            _nextRetry = 0;
        } else {
            _failCount++;
            d.connected = _failCount < MAX_FAILS;
            if (!d.connected) {
                d.voltage = 0; d.current = 0; d.power = 0; d.pf = 0;
                d.energy = _last.energy;
                _nextRetry = millis() + 30000;
            }
        }

        _last = d;
        return d;
    }

    void resetEnergy() { _pzem.resetEnergy(); }

    PhaseData getLast() const { return _last; }
    bool isConnected() const { return _failCount < MAX_FAILS; }

private:
    PZEM004Tv30 _pzem;
    PhaseData _last;
    int _failCount = 0;
    unsigned long _nextRetry = 0;
};

#endif
