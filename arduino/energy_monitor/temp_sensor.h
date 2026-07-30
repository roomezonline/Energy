#ifndef TEMP_SENSOR_H
#define TEMP_SENSOR_H

#include <Arduino.h>
#include "rtc_manager.h"

#define TEMP_SAMPLES    3
#define TEMP_MIN       -40
#define TEMP_MAX        85

class TempSensor {
public:
    void begin() {
        if (rtcManager.isOk()) {
            float t = rtcManager.getTemperature();
            if (t > TEMP_MIN && t < TEMP_MAX) _lastValid = t;
        }
    }

    float read() {
        if (!rtcManager.isOk()) return NAN;
        float sum = 0;
        int valid = 0;
        for (int i = 0; i < TEMP_SAMPLES; i++) {
            float t = rtcManager.getTemperature();
            if (t > TEMP_MIN && t < TEMP_MAX) {
                sum += t;
                valid++;
            }
            delay(5);
        }
        float avg = (valid > 0) ? (sum / valid) : _lastValid;
        if (avg > TEMP_MIN && avg < TEMP_MAX) _lastValid = avg;
        return _lastValid;
    }

private:
    float _lastValid = NAN;
};

#endif
