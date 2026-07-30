#ifndef RTC_MANAGER_H
#define RTC_MANAGER_H

#include <Arduino.h>
#include <Wire.h>
#include <RTClib.h>
#include <time.h>

#define RTC_SDA 18
#define RTC_SCL 5
#define IRAN_OFFSET 12600  // +3:30 in seconds
#define NTP_TIMEOUT_MS 15000
#define NTP_POLL_MS 200

class RtcManager {
public:
    RtcManager() {}

    bool begin() {
        Wire1.begin(RTC_SDA, RTC_SCL);
        if (!_rtc.begin(&Wire1)) {
            _ok = false;
            Serial.println("[RTC] DS3231 not found on Wire1 (SDA=18, SCL=5)");
            return false;
        }
        _ok = true;
        Serial.println("[RTC] DS3231 initialized");

        // Check oscillator stop flag (OSF) — battery backup failure
        // Validate time: year must be within reasonable range
        DateTime dt = _rtc.now();
        int y = dt.year();
        if (_rtc.lostPower()) {
            Serial.println("[RTC] WARNING: Oscillator stopped — battery backup failed!");
            _needsSync = true;
        } else if (y < 2024 || y > 2035) {
            Serial.printf("[RTC] WARNING: Invalid year %d — RTC lost time!\n", y);
            _needsSync = true;
        } else {
            _needsSync = false;
            Serial.printf("[RTC] Current time: %04d-%02d-%02d %02d:%02d:%02d\n",
                y, dt.month(), dt.day(), dt.hour(), dt.minute(), dt.second());
        }

        if (_needsSync) {
            Serial.println("[RTC] Time needs sync from server — LED will blink white");
        }
        return true;
    }

    bool needsSync() const { return _needsSync; }

    void syncFromServer(int y, int M, int d, int h, int m, int s) {
        if (!_ok) return;
        _rtc.adjust(DateTime(y, M, d, h, m, s));
        _needsSync = false;
        Serial.printf("[RTC] Synced from server: %04d-%02d-%02dT%02d:%02d:%02d\n",
            y, M, d, h, m, s);
    }

    void syncFromEpoch(time_t epoch) {
        if (!_ok || epoch < 100000) return;
        _rtc.adjust(DateTime(epoch));
        _needsSync = false;
        Serial.printf("[RTC] Synced from NTP, epoch=%lu\n", (unsigned long)epoch);
    }

    // Fallback: sync RTC from SNTP when server is unreachable
    // Blocks up to NTP_TIMEOUT_MS (15s). Call only when WiFi is connected.
    bool tryNtpSync() {
        if (!_ok || !_needsSync) return false;

        Serial.println("[RTC] NTP fallback: configuring SNTP...");
        configTime(0, 0, "pool.ntp.org", "time.google.com", "time.windows.com");

        unsigned long start = millis();
        while (millis() - start < NTP_TIMEOUT_MS) {
            time_t now;
            time(&now);
            if (now >= 1700000000) {  // after 2023-10-01
                syncFromEpoch(now);
                // Re-set Iran timezone for localtime() used by energy_tracker
                configTime(IRAN_OFFSET, 0, "pool.ntp.org", "time.google.com", "time.windows.com");
                return true;
            }
            delay(NTP_POLL_MS);
        }

        Serial.println("[RTC] NTP fallback: timeout, no response");
        return false;
    }

    time_t getEpoch() {
        if (!_ok || _needsSync) return 0;
        return _rtc.now().unixtime();
    }

    // UTC timestamp (for JSON payload to server)
    String getTimestamp() {
        if (!_ok || _needsSync) return "1970-01-01T00:00:00Z";
        DateTime dt = _rtc.now();
        char buf[24];
        snprintf(buf, sizeof(buf), "%04d-%02d-%02dT%02d:%02d:%02dZ",
            dt.year(), dt.month(), dt.day(),
            dt.hour(), dt.minute(), dt.second());
        return String(buf);
    }

    // Iran local time (UTC + 3:30)
    time_t getLocalEpoch() {
        time_t e = getEpoch();
        return (e > 100000) ? (e + IRAN_OFFSET) : 0;
    }

    // Persian date + Iran time for display
    String getLocalDisplayString() {
        if (!_ok) return "---";
        time_t localEpoch = getLocalEpoch();
        if (localEpoch < 100000) return "No time";
        DateTime dt((uint32_t)localEpoch);
        int gy = dt.year(), gm = dt.month(), gd = dt.day();
        int gh = dt.hour(), gmi = dt.minute();
        JalaliDate j = _gregToJalali(gy, gm, gd);
        char buf[24];
        snprintf(buf, sizeof(buf), "%04d/%02d/%02d %02d:%02d",
            j.year, j.month, j.day, gh, gmi);
        return String(buf);
    }

    String getLocalDateString() {
        if (!_ok) return "---";
        time_t localEpoch = getLocalEpoch();
        if (localEpoch < 100000) return "No time";
        DateTime dt((uint32_t)localEpoch);
        JalaliDate j = _gregToJalali(dt.year(), dt.month(), dt.day());
        char buf[14];
        snprintf(buf, sizeof(buf), "%04d/%02d/%02d", j.year, j.month, j.day);
        return String(buf);
    }

    String getLocalTimeString() {
        if (!_ok) return "--:--";
        time_t localEpoch = getLocalEpoch();
        if (localEpoch < 100000) return "--:--";
        DateTime dt((uint32_t)localEpoch);
        char buf[8];
        snprintf(buf, sizeof(buf), "%02d:%02d", dt.hour(), dt.minute());
        return String(buf);
    }

    float getTemperature() {
        if (!_ok) return 0;
        return _rtc.getTemperature();
    }

    bool isOk() const { return _ok; }

private:
    struct JalaliDate { int year, month, day; };

    static JalaliDate _gregToJalali(int gy, int gm, int gd) {
        // --- g2d: Gregorian → Julian Day number (jalaali-js algorithm) ---
        auto _d = [](long a, long b) -> long { return a / b; };
        auto _m = [](long a, long b) -> long { long r = a % b; return r < 0 ? r + b : r; };
        long jdn = _d((gy + _d(gm - 8, 6) + 100100L) * 1461L, 4)
            + _d(153L * _m(gm + 9, 12) + 2, 5)
            + gd - 34840408L;
        jdn = jdn - _d(_d(gy + 100100L + _d(gm - 8, 6), 100) * 3, 4) + 752;

        // --- Jalaali year estimate ---
        int jy = gy - 621;

        // --- jalCal: leap & March day (Farvardin 1) ---
        static const int breaks[] = {-61, 9, 38, 199, 426, 686, 756, 818, 1111,
                                      1181, 1210, 1635, 2060, 2097, 2192, 2262,
                                      2324, 2394, 2456, 3178};
        int leapJ = -14;
        int jp = breaks[0], jump = 0, n = 0;
        for (int i = 1; i < 20; i++) {
            int jm2 = breaks[i];
            jump = jm2 - jp;
            if (jy < jm2) break;
            leapJ = leapJ + _d(jump, 33) * 8 + _d(_m(jump, 33), 4);
            jp = jm2;
        }
        n = jy - jp;
        leapJ = leapJ + _d(n, 33) * 8 + _d(_m(n, 33) + 3, 4);
        if (_m(jump, 33) == 4 && jump - n == 4) leapJ += 1;
        int leapG = _d(gy, 4) - _d((_d(gy, 100) + 1) * 3, 4) - 150;
        int march = 20 + leapJ - leapG;

        // --- leap status of jy (0 = leap) ---
        int nn = n;
        if (jump - n < 6) nn = n - jump + _d(jump + 4, 33) * 33;
        int leap = _m(_m(nn + 1, 33) - 1, 4);
        if (leap == -1) leap = 4;

        // --- g2d of Nowruz (Farvardin 1) ---
        long jdn1f = _d((gy + _d(3 - 8, 6) + 100100L) * 1461L, 4)
            + _d(153L * _m(3 + 9, 12) + 2, 5)
            + march - 34840408L;
        jdn1f = jdn1f - _d(_d(gy + 100100L + _d(3 - 8, 6), 100) * 3, 4) + 752;

        // --- d2j: days since Nowruz → Jalaali ---
        long k = jdn - jdn1f;
        if (k >= 0) {
            if (k <= 185)
                return {jy, 1 + _d(k, 31), _m(k, 31) + 1};
            k -= 186;
        } else {
            jy -= 1;
            k += 179;
            if (leap == 1) k += 1;
        }
        return {jy, 7 + _d(k, 30), _m(k, 30) + 1};
    }

    RTC_DS3231 _rtc;
    bool _ok = false;
    bool _needsSync = true;  // assume unsynced until proven otherwise
};

extern RtcManager rtcManager;

#endif
