#ifndef TEMP_SENSOR_H
#define TEMP_SENSOR_H

#include <Arduino.h>
#include <OneWire.h>
#include <DallasTemperature.h>
#include "global.h"

#define TEMP_SAMPLES        3     // internal/RTC samples for averaging
#define TEMP_MIN           -40
#define TEMP_MAX            85

// External DS18B20 settings (mirrors the standalone temp project)
#define DS18B20_SAMPLES     3     // number of DS18B20 samples for averaging
#define DS18B20_RES_BITS    10    // resolution (10 = 0.25°C, ~187ms per conversion)
#define DELAY_BETWEEN_SAMPLES 10  // delay between samples (ms)
#define MAX_ATTEMPTS        20    // max attempts to gather valid readings

class TempSensor {
public:
    TempSensor() : _ds(&_oneWire) {}

    void begin() {
        if (!g_useExternalTemp) {
            // ESP32 internal temperature sensor (CPU die temperature)
            LOGF("[TEMP] Source: ESP32 internal temperature sensor\n");
            return;
        }

        // External DS18B20 on the free pin
        pinMode(DS18B20_PIN, INPUT_PULLUP);
        _ds.begin();
        _sensorFound = _ds.getDeviceCount() > 0;
        if (_sensorFound) {
            _ds.setResolution(DS18B20_RES_BITS);
            LOGF("[TEMP] Source: external DS18B20 on GPIO %d (%d device(s))\n",
                DS18B20_PIN, _ds.getDeviceCount());
        } else {
            LOGF("[TEMP] WARNING: DS18B20 not found on GPIO %d — temp will read 0\n", DS18B20_PIN);
        }
    }

    float read() {
        if (g_useExternalTemp) return _readExternal();
        return _readInternal();
    }

private:
    OneWire _oneWire;
    DallasTemperature _ds;
    bool _sensorFound = false;
    float _lastValid = NAN;

    float _readInternal() {
        // ESP32 internal die temperature sensor — always available
        float sum = 0;
        int valid = 0;
        for (int i = 0; i < TEMP_SAMPLES; i++) {
            float t = temperatureRead() + ESP_INTERNAL_TEMP_OFFSET;
            if (t > TEMP_MIN && t < TEMP_MAX) {
                sum += t;
                valid++;
            }
            delay(5);
        }
        float avg = (valid > 0) ? (sum / valid) : _lastValid;
        if (avg > TEMP_MIN && avg < TEMP_MAX) _lastValid = avg;
        if (isnan(_lastValid)) return 0;
        return _lastValid;
    }

    float _readExternal() {
        if (!_sensorFound) return 0;

        float sum = 0;
        int valid = 0;
        int attempts = 0;

        while (valid < DS18B20_SAMPLES && attempts < MAX_ATTEMPTS) {
            _ds.requestTemperatures();
            float t = _ds.getTempCByIndex(0);

            if (t > TEMP_MIN && t < TEMP_MAX) {
                sum += t;
                valid++;
                if (valid == DS18B20_SAMPLES) break;
            }

            attempts++;
            delay(DELAY_BETWEEN_SAMPLES);
        }

        if (valid == DS18B20_SAMPLES) {
            float avg = sum / DS18B20_SAMPLES;
            if (avg > TEMP_MIN && avg < TEMP_MAX) _lastValid = avg;
            return _lastValid;
        }

        // Not enough valid readings — fall back to last valid value
        if (isnan(_lastValid)) return 0;
        return _lastValid;
    }
};

#endif
