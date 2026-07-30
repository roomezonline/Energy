#ifndef PHASE_C_H
#define PHASE_C_H

#include <Arduino.h>
#include "global.h"
#include "energy_data.h"

// Direct Modbus reader for PZEM3 using UART0 (Serial) — reliable hardware UART
// NOTE: Serial is remapped to GPIO26/27 here. Debug output is lost after begin().
class PhaseC {
public:
    PhaseC() : _initialized(false) {}

    void begin() {
        Serial.begin(9600, SERIAL_8N1, PZEM3_RX, PZEM3_TX);
        _initialized = true;
        delay(100);
    }

    PhaseData read() {
        if (!_initialized) return _last;
        PhaseData d = _last;

        // If disconnected, skip blocking Modbus reads — retry every 30s
        if (_failCount >= MAX_FAILS && (int32_t)(millis() - _nextRetry) < 0) {
            d.connected = false;
            _last = d;
            return d;
        }

        bool ok = _readRegs();

        if (ok) {
            d.voltage = _values.voltage;
            if (g_calEnabled) {
                d.current = max(0.0f, _values.current * g_cal.current + g_cal.offset);
                d.power   = max(0.0f, _values.power   * g_cal.power);
                d.energy  = max(0.0f, _values.energy  * g_cal.energy);
                float apparent = d.voltage * d.current;
                d.pf = apparent > 0 ? constrain(d.power / apparent, 0.0f, 1.0f) : 0.0f;
            }
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

    PhaseData getLast() const { return _last; }
    bool isConnected() const { return _failCount < MAX_FAILS; }

    void resetEnergy() {
        uint8_t cmd[6];
        cmd[0] = 0xF8;
        cmd[1] = 0x42;
        cmd[2] = 0x00;
        cmd[3] = 0x00;
        uint16_t crc = _crc16(cmd, 4);
        cmd[4] = crc & 0xFF;
        cmd[5] = (crc >> 8) & 0xFF;
        Serial.write(cmd, 6);
        Serial.flush();
    }

private:
    bool _initialized;
    PhaseData _last;
    int _failCount = 0;
    unsigned long _nextRetry = 0;

    struct {
        float voltage, current, power, energy, pf;
    } _values;

    uint16_t _crc16(const uint8_t* data, uint16_t len) {
        uint16_t crc = 0xFFFF;
        for (uint16_t i = 0; i < len; i++) {
            crc ^= data[i];
            for (int j = 0; j < 8; j++) {
                if (crc & 0x0001) { crc >>= 1; crc ^= 0xA001; }
                else { crc >>= 1; }
            }
        }
        return crc;
    }

    bool _readRegs() {
        while (Serial.available() > 0) Serial.read();

        uint8_t cmd[8];
        cmd[0] = 0xF8;
        cmd[1] = 0x04;
        cmd[2] = 0x00;
        cmd[3] = 0x00;
        cmd[4] = 0x00;
        cmd[5] = 0x0A;

        uint16_t crc = _crc16(cmd, 6);
        cmd[6] = crc & 0xFF;
        cmd[7] = (crc >> 8) & 0xFF;

        Serial.write(cmd, 8);
        Serial.flush();

        uint8_t buf[32];
        unsigned long start = millis();
        int idx = 0;
        while (millis() - start < 200 && idx < 25) {
            if (Serial.available()) {
                buf[idx++] = Serial.read();
            }
            yield();
            delay(1);
        }

        if (idx < 25) return false;

        uint16_t respCrc = buf[idx - 2] | ((uint16_t)buf[idx - 1] << 8);
        if (_crc16(buf, idx - 2) != respCrc) return false;

        // PZEM stores 32-bit values as: high_reg << 16 | low_reg (little-endian register ordering)
        // low_reg  = buf[5] << 8 | buf[6]   (register 0x0001 — first in response)
        // high_reg = buf[7] << 8 | buf[8]   (register 0x0002 — second in response)
        // Combined: buf[7] << 24 | buf[8] << 16 | buf[5] << 8 | buf[6]
        float v = ((uint32_t)buf[3] << 8 | buf[4]) / 10.0f;
        if (v < 1 || v > 300) return false;
        _values.voltage = v;

        float c = ((uint32_t)buf[7] << 24 | (uint32_t)buf[8] << 16 |
                   (uint32_t)buf[5] << 8 | buf[6]) / 1000.0f;
        if (c < 0 || c >= 100) return false;
        _values.current = c;

        float p = ((uint32_t)buf[11] << 24 | (uint32_t)buf[12] << 16 |
                   (uint32_t)buf[9] << 8 | buf[10]) / 10.0f;
        if (p < 0 || p >= 30000) return false;
        _values.power = p;

        float e = ((uint32_t)buf[15] << 24 | (uint32_t)buf[16] << 16 |
                   (uint32_t)buf[13] << 8 | buf[14]) / 1000.0f;
        if (e < 0 || e >= PZEM_ENERGY_MAX) return false;
        _values.energy = e;

        float pf = ((uint32_t)buf[19] << 8 | buf[20]) / 100.0f;
        if (pf < 0 || pf > 1) return false;
        _values.pf = pf;

        return true;
    }
};

#endif
