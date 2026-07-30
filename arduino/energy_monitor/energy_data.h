#ifndef ENERGY_DATA_H
#define ENERGY_DATA_H

#include <Arduino.h>

struct PhaseData {
    float voltage = 0;
    float current = 0;
    float power = 0;
    float pf = 0;
    float energy = 0;
    float delta = 0;
    bool connected = false;
};

struct WindowAgg {
    float minVoltage = 0, maxVoltage = 0;
    float minCurrent = 0, maxCurrent = 0;
    float minPower = 0, maxPower = 0;
    float avgVoltage = 0, avgCurrent = 0, avgPower = 0;
    float deltaEnergy = 0;
    bool valid = false;
};

struct OutagePayload {
    uint32_t outageStartEpoch = 0;
    float totalDeltaA = 0, totalDeltaB = 0, totalDeltaC = 0;
    float maxPeakA = 0, maxPeakB = 0, maxPeakC = 0;
    float maxPowerA = 0, maxPowerB = 0, maxPowerC = 0;
    bool hasData = false;
};

struct EnergyData {
    String deviceId;
    String timestamp;
    PhaseData phaseA;
    PhaseData phaseB;
    PhaseData phaseC;
    float frequency = 0;

    float temperature = 0;

    WindowAgg winA, winB, winC;
    OutagePayload outage;

    String toJson() const {
        String j;
        j.reserve(512);
        j = "{";
        j += "\"deviceId\":\"";
        j += deviceId;
        j += "\"";
        j += ",\"timestamp\":\"";
        j += timestamp;
        j += "\"";
        j += ",\"frequency\":";
        j += String(frequency, 2);
        j += ",\"temp\":";
        j += isnan(temperature) ? String("-127") : String(temperature, 1);

        auto addPhase = [&](const String& name, const PhaseData& p) {
            j += ",\"";
            j += name;
            j += "\":{";
            j += "\"voltage\":";
            j += String(p.voltage, 2);
            j += ",\"current\":";
            j += String(p.current, 2);
            j += ",\"power\":";
            j += String(p.power, 2);
            j += ",\"pf\":";
            j += String(p.pf, 2);
            j += ",\"energy\":";
            j += String(p.energy, 3);
            j += ",\"delta\":";
            j += String(p.delta, 4);
            j += ",\"connected\":";
            j += String(p.connected ? "true" : "false");
            j += "}";
        };

        auto addWin = [&](const String& name, const WindowAgg& w) {
            if (!w.valid) return;
            j += ",\"";
            j += name;
            j += "Win\":{";
            j += "\"mnV\":";
            j += String(w.minVoltage, 1);
            j += ",\"mxV\":";
            j += String(w.maxVoltage, 1);
            j += ",\"mnA\":";
            j += String(w.minCurrent, 3);
            j += ",\"mxA\":";
            j += String(w.maxCurrent, 3);
            j += ",\"mnW\":";
            j += String(w.minPower, 1);
            j += ",\"mxW\":";
            j += String(w.maxPower, 1);
            j += ",\"aV\":";
            j += String(w.avgVoltage, 1);
            j += ",\"aA\":";
            j += String(w.avgCurrent, 3);
            j += ",\"aW\":";
            j += String(w.avgPower, 1);
            j += ",\"dE\":";
            j += String(w.deltaEnergy, 3);
            j += "}";
        };

        addPhase("phaseA", phaseA);
        addPhase("phaseB", phaseB);
        addPhase("phaseC", phaseC);

        addWin("phaseA", winA);
        addWin("phaseB", winB);
        addWin("phaseC", winC);

        if (outage.hasData) {
            j += ",\"outage\":{";
            j += "\"sd\":";
            j += String(outage.outageStartEpoch);
            j += ",\"dA\":";
            j += String(outage.totalDeltaA, 4);
            j += ",\"maxA\":";
            j += String(outage.maxPeakA, 3);
            j += ",\"mwA\":";
            j += String(outage.maxPowerA, 1);
            j += ",\"dB\":";
            j += String(outage.totalDeltaB, 4);
            j += ",\"maxB\":";
            j += String(outage.maxPeakB, 3);
            j += ",\"mwB\":";
            j += String(outage.maxPowerB, 1);
            j += ",\"dC\":";
            j += String(outage.totalDeltaC, 4);
            j += ",\"maxC\":";
            j += String(outage.maxPeakC, 3);
            j += ",\"mwC\":";
            j += String(outage.maxPowerC, 1);
            j += "}";
        }

        j += "}";
        return j;
    }
};

#endif
