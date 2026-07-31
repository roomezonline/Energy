#ifndef OLED_DISPLAY_H
#define OLED_DISPLAY_H

#include <Arduino.h>
#include <Wire.h>
#include <Adafruit_SSD1306.h>
#include "energy_data.h"
#include "rtc_manager.h"
#include "global.h"

#define OLED_ADDR      0x3C
#define SCREEN_WIDTH   128
#define SCREEN_HEIGHT  64
#define OLED_SDA       21
#define OLED_SCL       19

// Standby (eyes) settings
#define EYE_FRAME_MS        150        // ms per eye animation frame
#define STANDBY_CONTRAST    50         // soft glow while in standby
#define NORMAL_CONTRAST     127        // default SSD1306 contrast

#define EYE_BLINK_FRAMES 4    // 4 frames = 600ms blink
#define EYE_WINK_FRAMES  5    // 5 frames = 750ms wink (one eye)
#define EYE_LOOK_FRAMES  10   // look left/right, hold, come back
#define EYE_HALF_FRAMES  10   // half-open with gentle flutter
#define EYE_SLEEP_FRAMES 12   // closed (sleeping) hold
#define EYE_IDLE_FRAMES  8    // fully open, idle

class OledDisplay {
public:
    OledDisplay() : _display(SCREEN_WIDTH, SCREEN_HEIGHT, &Wire, -1) {}

    void begin() {
        Wire.begin(OLED_SDA, OLED_SCL);
        _ok = _display.begin(SSD1306_SWITCHCAPVCC, OLED_ADDR);
        if (!_ok) return;
        _display.clearDisplay();
        _display.display();
        _display.setTextColor(SSD1306_WHITE);
        _mode = WIFI_INIT;
        _wifiMsg = "Searching...";
        _wifiStep = 0;
        _wifiTick = 0;
        _wifiFailStart = 0;
        _renderWifiStatus();
    }

    // ---- Boot splash sequence (called from setup) ----
    void showBootWeb(const String& url) {
        _renderBootPage("Web", url);
    }

    void showBootDeviceId(const String& id) {
        _renderBootPage("Device ID", id);
    }

    // Boot splash shown after WiFi connects — before the OTA check
    void showBootUpdateCheck() {
        if (!_ok) return;
        _display.clearDisplay();

        _display.fillRect(0, 0, 128, 16, SSD1306_WHITE);
        _display.setTextColor(SSD1306_BLACK);
        _display.setCursor(6, 4);
        _display.setTextSize(1);
        _display.print("Update Check");

        _display.setTextColor(SSD1306_WHITE);
        _display.drawFastHLine(0, 16, 128, SSD1306_WHITE);

        const char* t = "Checking...";
        int tw = strlen(t) * 6;
        _display.setCursor((128 - tw) / 2, 28);
        _display.setTextSize(1);
        _display.print(t);

        _display.display();
    }

    // Shown for ~4s after the check when no update is available
    void showBootUpToDate(const String& version) {
        if (!_ok) return;
        _display.clearDisplay();

        _display.fillRect(0, 0, 128, 16, SSD1306_WHITE);
        _display.setTextColor(SSD1306_BLACK);
        _display.setCursor(6, 4);
        _display.setTextSize(1);
        _display.print("Update Check");

        _display.setTextColor(SSD1306_WHITE);
        _display.drawFastHLine(0, 16, 128, SSD1306_WHITE);

        const char* t = "Up to date";
        int tw = strlen(t) * 6;
        _display.setCursor((128 - tw) / 2, 22);
        _display.setTextSize(1);
        _display.print(t);

        String ver = "v" + version;
        int vw = ver.length() * 12;
        _display.setCursor((128 - vw) / 2, 36);
        _display.setTextSize(2);
        _display.print(ver);

        _display.display();
    }

    void loop(const EnergyData& data, bool httpOk) {
        if (!_ok) return;

        unsigned long now = millis();

        if (_otaMode) {
            if (_otaDismiss && now - _otaDismiss > 4000) {
                _otaMode = false;
                _otaDismiss = 0;
            }
            return;
        }

        if (_mode == STANDBY) {
            if (now - _eyeLast >= EYE_FRAME_MS) {
                _eyeLast = now;
                _eyeTick();
            }
            return;
        }

        _httpOk = httpOk;

        if (_mode == WIFI_INIT) {
            _loopWifi();
            return;
        }

        if (_mode == NORMAL) {
            if (g_standbyEnabled && now > _standbyDeadline) {
                _enterStandby();
                return;
            }
            _loopSlides(data);
            return;
        }
    }

    void setWifiStatus(const String& msg, int step) {
        if (_mode != WIFI_INIT) return;
        _wifiMsg = msg;
        _wifiStep = step;
        _wifiTick = 0;
        _wifiFailStart = 0;
        _renderWifiStatus();
    }

    void setWifiStatus(const char* msg, int step) {
        setWifiStatus(String(msg), step);
    }

    void setNormalMode() {
        if (_mode == WIFI_INIT) {
            _mode = NORMAL;
            _standbyDeadline = millis() + STANDBY_TIMEOUT_MS;
            for (int i = 0; i < 9; i++) _slideLastShown[i] = 0;
            _slideStart = 0;
        }
    }

    // Wake from standby (called when DISPLAY_WAKE_PIN goes HIGH)
    void wake() {
        if (!_ok || _mode != STANDBY) return;
        _mode = NORMAL;
        _standbyDeadline = millis() + STANDBY_TIMEOUT_MS;
        _slideStart = 0;
        _setContrast(NORMAL_CONTRAST);
        LOGD("[OLED] Woke from standby");
    }

    void showOtaStart(const String& newVersion) {
        if (!_ok) return;
        _otaMode = true;
        _otaDismiss = 0;
        _display.clearDisplay();

        _display.fillRect(0, 0, 128, 16, SSD1306_WHITE);
        _display.setTextColor(SSD1306_BLACK);
        _display.setCursor(6, 4);
        _display.setTextSize(1);
        _display.print("OTA Update");

        _display.setTextColor(SSD1306_WHITE);
        _display.drawFastHLine(0, 16, 128, SSD1306_WHITE);

        const char* t1 = "Update available";
        int t1w = strlen(t1) * 6;
        _display.setCursor((128 - t1w) / 2, 22);
        _display.setTextSize(1);
        _display.print(t1);

        String ver = "v" + String(CURRENT_VERSION) + "  ->  " + newVersion;
        int vw = ver.length() * 6;
        _display.setCursor((128 - vw) / 2, 34);
        _display.print(ver);

        const char* t2 = "Updating...";
        int t2w = strlen(t2) * 6;
        _display.setCursor((128 - t2w) / 2, 50);
        _display.print(t2);

        _display.display();
    }

    void showOtaProgress(int pct, const String& status) {
        if (!_ok) return;
        _display.clearDisplay();

        _display.fillRect(0, 0, 128, 16, SSD1306_WHITE);
        _display.setTextColor(SSD1306_BLACK);
        _display.setCursor(6, 4);
        _display.setTextSize(1);
        _display.print("OTA Update");

        _display.setTextColor(SSD1306_WHITE);
        _display.drawFastHLine(0, 16, 128, SSD1306_WHITE);

        int barX = 10, barY = 22, barW = 108, barH = 14;
        _display.drawRect(barX, barY, barW, barH, SSD1306_WHITE);
        int fillW = (barW - 2) * pct / 100;
        if (fillW > 0)
            _display.fillRect(barX + 1, barY + 1, fillW, barH - 2, SSD1306_WHITE);

        char pctBuf[8];
        snprintf(pctBuf, sizeof(pctBuf), "%d%%", pct);
        int pw = strlen(pctBuf) * 12;
        _display.setCursor((128 - pw) / 2, 40);
        _display.setTextSize(2);
        _display.print(pctBuf);

        _display.setTextSize(1);
        int sw = status.length() * 6;
        _display.setCursor((128 - sw) / 2, 56);
        _display.print(status);

        _display.display();
    }

    void showOtaDone() {
        if (!_ok) return;
        _display.clearDisplay();

        _display.fillRect(0, 0, 128, 16, SSD1306_WHITE);
        _display.setTextColor(SSD1306_BLACK);
        _display.setCursor(6, 4);
        _display.setTextSize(1);
        _display.print("OTA Update");

        _display.setTextColor(SSD1306_WHITE);
        _display.drawFastHLine(0, 16, 128, SSD1306_WHITE);

        _display.fillRect(44, 24, 40, 16, SSD1306_WHITE);

        const char* t1 = "Update Done!";
        int t1w = strlen(t1) * 12;
        _display.setCursor((128 - t1w) / 2, 28);
        _display.setTextSize(2);
        _display.setTextColor(SSD1306_BLACK);
        _display.print(t1);

        _display.setTextColor(SSD1306_WHITE);
        const char* t2 = "Rebooting...";
        int t2w = strlen(t2) * 6;
        _display.setCursor((128 - t2w) / 2, 50);
        _display.setTextSize(1);
        _display.print(t2);

        _display.display();
        _otaDismiss = millis();
    }

    void showOtaError(const String& msg) {
        if (!_ok) return;
        _display.clearDisplay();

        _display.fillRect(0, 0, 128, 16, SSD1306_WHITE);
        _display.setTextColor(SSD1306_BLACK);
        _display.setCursor(6, 4);
        _display.setTextSize(1);
        _display.print("OTA Error");

        _display.setTextColor(SSD1306_WHITE);
        _display.drawFastHLine(0, 16, 128, SSD1306_WHITE);

        const char* t1 = "Update failed!";
        int t1w = strlen(t1) * 6;
        _display.setCursor((128 - t1w) / 2, 24);
        _display.setTextSize(1);
        _display.print(t1);

        const char* t2 = msg.c_str();
        int t2w = strlen(t2) * 6;
        _display.setCursor((128 - t2w) / 2, 38);
        _display.print(t2);

        _display.display();
        _otaDismiss = millis();
    }

private:
    enum DisplayMode { WIFI_INIT, NORMAL, STANDBY };
    enum EyeAction : uint8_t { EYE_IDLE = 0, EYE_BLINK, EYE_WINK_L, EYE_WINK_R, EYE_LOOK, EYE_HALF, EYE_SLEEP };
    enum FaceExpr : uint8_t { EX_HAPPY = 0, EX_NEUTRAL, EX_SURPRISED, EX_SLEEPY, EX_ANGRY };

    Adafruit_SSD1306 _display;
    bool _ok = false;
    bool _httpOk = false;
    DisplayMode _mode = WIFI_INIT;

    String _wifiMsg;
    int _wifiStep = 0;
    unsigned long _wifiTick = 0;
    unsigned long _lastWifiRender = 0;
    unsigned long _wifiFailStart = 0;

    static const int SLIDE_COUNT = 9;
    unsigned long _slideLastShown[SLIDE_COUNT];
    unsigned long _slideStart = 0;
    int _slideIndex = 0;

    static const uint8_t _slidePriority[SLIDE_COUNT];
    static const uint16_t _slideDuration[SLIDE_COUNT];
    static const uint16_t _slideInterval[SLIDE_COUNT];

    bool _otaMode = false;
    unsigned long _otaDismiss = 0;

    // ---- Standby state ----
    unsigned long _standbyDeadline = 0;
    unsigned long _eyeLast = 0;
    uint8_t _eyeAction = EYE_IDLE;
    uint8_t _eyeFrame = 0;
    uint8_t _eyeFrames = 1;
    int8_t _eyeLookDir = 0;   // chosen look direction (EYE_LOOK)
    int8_t _eyeGazeX = 0;
    int8_t _eyeGazeY = 0;
    float _eyeOpenL = 1.0f;   // left eye open level
    float _eyeOpenR = 1.0f;   // right eye open level
    uint8_t _expression = EX_HAPPY;

    void _renderWifiStatus() {
        unsigned long now = millis();
        if (now - _lastWifiRender < 200 && _lastWifiRender > 0) return;
        _lastWifiRender = now;

        _display.clearDisplay();

        _display.fillRect(0, 0, 128, 16, SSD1306_BLACK);
        _display.setCursor(6, 4);
        _display.setTextSize(1);
        _display.print("WiFi Setup");

        _display.drawFastHLine(0, 16, 128, SSD1306_WHITE);

        _wifiTick++;
        const char* spin = "|/-\\";
        char sp = spin[_wifiTick % 4];

        _display.setTextSize(1);

        if (_wifiStep == 0) {
            _display.setCursor(10, 24);
            _display.print("[");
            _display.print(sp);
            _display.print("] Searching...");
            _display.setCursor(10, 40);
            _display.print(_wifiMsg);
        } else if (_wifiStep == 1) {
            _display.setCursor(10, 24);
            _display.print("[");
            _display.print(sp);
            _display.print("] Connecting...");
            String ssid = _wifiMsg;
            if (ssid.length() > 16) ssid = ssid.substring(0, 16) + "..";
            int sw = ssid.length() * 6;
            int sx = (128 - sw) / 2;
            if (sx < 0) sx = 0;
            _display.setCursor(sx, 40);
            _display.print(ssid);
        } else if (_wifiStep == 2) {
            _display.setCursor(10, 24);
            _display.print("[+] Connected!");
            String ip = _wifiMsg;
            int iw = ip.length() * 6;
            int ix = (128 - iw) / 2;
            if (ix < 0) ix = 0;
            _display.setCursor(ix, 40);
            _display.print(ip);
        } else if (_wifiStep == 3) {
            _display.setCursor(10, 24);
            _display.print("[-] Failed!");
            String fssid = _wifiMsg;
            if (fssid.length() > 16) fssid = fssid.substring(0, 16) + "..";
            int fw = fssid.length() * 6;
            int fx = (128 - fw) / 2;
            if (fx < 0) fx = 0;
            _display.setCursor(fx, 40);
            _display.print(fssid);
        } else if (_wifiStep == 4) {
            _display.setCursor(10, 24);
            _display.print("[!] No connection");
            _display.setCursor(10, 42);
            _display.print("AP ready on");
            _display.setCursor(10, 52);
            _display.print("192.168.4.1");
        }

        _display.display();
    }

    void _loopWifi() {
        unsigned long now = millis();

        if (_wifiStep == 2) {
            if (_slideStart == 0) _slideStart = now;
            if (now - _slideStart >= 3000) {
                _mode = NORMAL;
                for (int i = 0; i < SLIDE_COUNT; i++) _slideLastShown[i] = 0;
                _slideStart = 0;
                _standbyDeadline = now + STANDBY_TIMEOUT_MS;
            }
        } else if (_wifiStep == 3) {
            if (_wifiFailStart == 0) _wifiFailStart = now;
            if (now - _wifiFailStart >= 2000) {
                _wifiStep = 0;
                _wifiFailStart = 0;
                _wifiMsg = "Searching...";
                _renderWifiStatus();
            }
        } else if (_wifiStep == 4) {
            if (_slideStart == 0) _slideStart = now;
            if (now - _slideStart >= 4000) {
                _mode = NORMAL;
                for (int i = 0; i < SLIDE_COUNT; i++) _slideLastShown[i] = 0;
                _slideStart = 0;
                _standbyDeadline = now + STANDBY_TIMEOUT_MS;
            }
        } else {
            _renderWifiStatus();
        }
    }

    int _pickNextSlide(const EnergyData& data) {
        unsigned long now = millis();
        int best = -1;
        int bestPrio = 999;

        for (int i = 0; i < SLIDE_COUNT; i++) {
            if (i <= 5) {
                bool phaseConnected = false;
                if (i <= 1) phaseConnected = data.phaseA.connected;
                else if (i <= 3) phaseConnected = data.phaseB.connected;
                else phaseConnected = data.phaseC.connected;
                if (!phaseConnected) continue;
            }

            if (now - _slideLastShown[i] >= _slideInterval[i]) {
                if (_slidePriority[i] < bestPrio) {
                    bestPrio = _slidePriority[i];
                    best = i;
                }
            }
        }

        if (best >= 0) {
            _slideLastShown[best] = now;
            return best;
        }

        unsigned long oldest = now;
        best = 0;
        for (int i = 0; i < SLIDE_COUNT; i++) {
            if (i <= 5) {
                bool phaseConnected = false;
                if (i <= 1) phaseConnected = data.phaseA.connected;
                else if (i <= 3) phaseConnected = data.phaseB.connected;
                else phaseConnected = data.phaseC.connected;
                if (!phaseConnected) continue;
            }
            if (_slideLastShown[i] < oldest) {
                oldest = _slideLastShown[i];
                best = i;
            }
        }
        _slideLastShown[best] = now;
        return best;
    }

    void _loopSlides(const EnergyData& data) {
        unsigned long now = millis();
        unsigned long elapsed = now - _slideStart;
        int dur = _slideDuration[_slideIndex];

        if (elapsed >= (unsigned long)dur) {
            int next = _pickNextSlide(data);
            if (next != _slideIndex || elapsed >= (unsigned long)dur + 500) {
                _slideIndex = next;
                _slideStart = now;
                _render(data, _slideIndex);
            }
        }
    }

    void _render(const EnergyData& data, int idx) {
        const char* label = "";
        float val = 0;
        const char* unit = "";
        int dec = 1;
        bool connected = true;
        bool isTemp = false;
        bool isSpecial = false;

        switch (idx) {
            case 0: label = "L1 Voltage";   val = data.phaseA.voltage; unit = "V"; dec = 1; connected = data.phaseA.connected; break;
            case 1: label = "L1 Current";   val = data.phaseA.current; unit = "A"; dec = 2; connected = data.phaseA.connected; break;
            case 2: label = "L2 Voltage";   val = data.phaseB.voltage; unit = "V"; dec = 1; connected = data.phaseB.connected; break;
            case 3: label = "L2 Current";   val = data.phaseB.current; unit = "A"; dec = 2; connected = data.phaseB.connected; break;
            case 4: label = "L3 Voltage";   val = data.phaseC.voltage; unit = "V"; dec = 1; connected = data.phaseC.connected; break;
            case 5: label = "L3 Current";   val = data.phaseC.current; unit = "A"; dec = 2; connected = data.phaseC.connected; break;
            case 6: label = "Temperature";  val = data.temperature;    unit = "C"; dec = 1; isTemp = true; break;
            case 7: label = "Internet";     isTemp = true; isSpecial = true; break;
            case 8: label = "Date / Time";  isTemp = true; isSpecial = true; break;
        }

        _display.clearDisplay();

        _display.fillRect(0, 0, 128, 16, SSD1306_BLACK);
        _display.setCursor(6, 4);
        _display.setTextSize(1);
        _display.print(label);

        if (!isTemp && !isSpecial) {
            if (connected)
                _display.fillCircle(118, 8, 3, SSD1306_WHITE);
            else
                _display.drawCircle(118, 8, 3, SSD1306_WHITE);
        }

        _display.drawFastHLine(0, 16, 128, SSD1306_WHITE);

        if (idx == 7) {
            _display.setTextSize(3);
            if (_httpOk) {
                _display.fillCircle(118, 8, 3, SSD1306_WHITE);
                const char* txt = "Online";
                int tw = strlen(txt) * 18;
                int tx = (128 - tw) / 2;
                if (tx < 0) tx = 0;
                _display.setCursor(tx, 22);
                _display.print(txt);
            } else {
                _display.drawCircle(118, 8, 3, SSD1306_WHITE);
                const char* txt = "Offline";
                int tw = strlen(txt) * 18;
                int tx = (128 - tw) / 2;
                if (tx < 0) tx = 0;
                _display.setCursor(tx, 22);
                _display.print(txt);
            }
            _display.display();
            return;
        }

        if (idx == 8) {
            String dateStr, timeStr;
            if (g_persianTime.length() > 0) {
                int sp = g_persianTime.indexOf(' ');
                if (sp > 0) {
                    dateStr = g_persianTime.substring(0, sp);
                    timeStr = g_persianTime.substring(sp + 1, sp + 6);
                } else {
                    dateStr = g_persianTime;
                }
            } else {
                dateStr = rtcManager.getLocalDateString();
                timeStr = rtcManager.getLocalTimeString();
            }
            _display.setTextSize(2);
            int dw = dateStr.length() * 12;
            int dx = (128 - dw) / 2;
            if (dx < 0) dx = 0;
            _display.setCursor(dx, 22);
            _display.print(dateStr);
            int tw = timeStr.length() * 12;
            int tx = (128 - tw) / 2;
            if (tx < 0) tx = 0;
            _display.setCursor(tx, 44);
            _display.print(timeStr);
            _display.display();
            return;
        }

        char buf[12];
        if (isTemp || (connected && val >= 0)) {
            dtostrf(val, 1, dec, buf);
        } else {
            strcpy(buf, "disconnect");
        }

        int blen = strlen(buf);
        int bw = blen * 6 * 3;
        int bx = (128 - bw) / 2;

        if (blen <= 5) {
            _display.setCursor(bx < 0 ? 0 : bx, 20);
            _display.setTextSize(3);
            _display.print(buf);
            int uw = strlen(unit) * 6 * 2;
            int ux = (128 - uw) / 2;
            _display.setCursor(ux < 0 ? 0 : ux, 46);
            _display.setTextSize(2);
            _display.print(unit);
        } else {
            int dw = blen * 12;
            int dx = (128 - dw) / 2;
            if (dx < 0) dx = 0;
            _display.setCursor(dx, 26);
            _display.setTextSize(2);
            _display.print(buf);
        }

        _display.display();
    }

    // ============================================================
    //  Boot splash
    // ============================================================
    void _renderBootPage(const String& title, const String& value) {
        if (!_ok) return;
        _display.clearDisplay();

        _display.fillRect(0, 0, 128, 16, SSD1306_WHITE);
        _display.setTextColor(SSD1306_BLACK);
        _display.setCursor(6, 4);
        _display.setTextSize(1);
        _display.print(title);

        _display.setTextColor(SSD1306_WHITE);
        _display.drawFastHLine(0, 16, 128, SSD1306_WHITE);

        _display.setTextSize(2);
        int vw = value.length() * 12;
        if (value.length() > 11) {   // long text → smaller font
            _display.setTextSize(1);
            vw = value.length() * 6;
        }
        int vx = (128 - vw) / 2;
        if (vx < 0) vx = 0;
        _display.setCursor(vx, 28);
        _display.print(value);

        _display.display();
    }

    // ============================================================
    //  Standby — animated face (eyes + eyebrows + mouth)
    // ============================================================
    void _enterStandby() {
        _mode = STANDBY;
        randomSeed(esp_random());
        _eyeBegin();
        _eyeLast = 0;
        _setContrast(STANDBY_CONTRAST);
        _renderFace();
        LOGD("[OLED] Standby mode");
    }

    void _eyeBegin() {
        _eyeAction = EYE_IDLE;
        _eyeFrame = 0;
        _eyeFrames = EYE_IDLE_FRAMES;
        _eyeLookDir = 0;
        _eyeGazeX = 0;
        _eyeGazeY = 0;
        _eyeOpenL = 1.0f;
        _eyeOpenR = 1.0f;
        _expression = EX_HAPPY;
    }

    void _eyeTick() {
        if (++_eyeFrame >= _eyeFrames) {
            _eyeFrame = 0;
            _pickFaceState();
        }
        _applyEyePose();
        _renderFace();
    }

    void _pickFaceState() {
        uint8_t r = (uint8_t)random(100);
        if (r < 15) {
            _eyeAction = EYE_WINK_L; _eyeFrames = EYE_WINK_FRAMES;
            _eyeLookDir = 0; _expression = EX_HAPPY;
        } else if (r < 30) {
            _eyeAction = EYE_WINK_R; _eyeFrames = EYE_WINK_FRAMES;
            _eyeLookDir = 0; _expression = EX_HAPPY;
        } else if (r < 50) {
            _eyeAction = EYE_BLINK; _eyeFrames = EYE_BLINK_FRAMES;
            _eyeLookDir = 0; _expression = EX_HAPPY;
        } else if (r < 62) {
            _eyeAction = EYE_LOOK; _eyeFrames = EYE_LOOK_FRAMES;
            _eyeLookDir = random(2) ? 1 : -1; _expression = EX_NEUTRAL;
        } else if (r < 72) {
            _eyeAction = EYE_IDLE; _eyeFrames = 14;
            _eyeLookDir = 0; _expression = EX_SURPRISED;
        } else if (r < 80) {
            _eyeAction = EYE_IDLE; _eyeFrames = 10;
            _eyeLookDir = 0; _expression = EX_ANGRY;
        } else if (r < 90) {
            _eyeAction = EYE_HALF; _eyeFrames = EYE_HALF_FRAMES;
            _eyeLookDir = 0; _expression = EX_SLEEPY;
        } else {
            _eyeAction = EYE_SLEEP; _eyeFrames = EYE_SLEEP_FRAMES;
            _eyeLookDir = 0; _expression = EX_SLEEPY;
        }
    }

    void _applyEyePose() {
        uint8_t f = _eyeFrame;
        switch (_eyeAction) {
            case EYE_BLINK:
                if (f == 0) { _eyeOpenL = 1.0f; _eyeOpenR = 1.0f; }
                else if (f == 1) { _eyeOpenL = 0.30f; _eyeOpenR = 0.30f; }
                else if (f == 2) { _eyeOpenL = 0.05f; _eyeOpenR = 0.05f; }
                else { _eyeOpenL = 1.0f; _eyeOpenR = 1.0f; }
                _eyeGazeX = 0; _eyeGazeY = 0;
                break;
            case EYE_WINK_L:
                if (f == 0 || f == 4) { _eyeOpenL = 1.0f; }
                else if (f == 1 || f == 3) { _eyeOpenL = 0.15f; }
                else { _eyeOpenL = 0.05f; }
                _eyeOpenR = 1.0f;
                _eyeGazeX = 0; _eyeGazeY = 0;
                break;
            case EYE_WINK_R:
                if (f == 0 || f == 4) { _eyeOpenR = 1.0f; }
                else if (f == 1 || f == 3) { _eyeOpenR = 0.15f; }
                else { _eyeOpenR = 0.05f; }
                _eyeOpenL = 1.0f;
                _eyeGazeX = 0; _eyeGazeY = 0;
                break;
            case EYE_LOOK:
                _eyeOpenL = 1.0f; _eyeOpenR = 1.0f;
                _eyeGazeY = 0;
                _eyeGazeX = (f < 8) ? _eyeLookDir : 0;
                break;
            case EYE_HALF:
                _eyeOpenL = 0.5f + ((f % 2) ? 0.15f : 0.0f);
                _eyeOpenR = _eyeOpenL;
                _eyeGazeX = 0; _eyeGazeY = 0;
                break;
            case EYE_SLEEP:
                _eyeOpenL = 0.05f; _eyeOpenR = 0.05f;
                _eyeGazeX = 0; _eyeGazeY = 0;
                break;
            case EYE_IDLE:
            default:
                _eyeOpenL = 1.0f; _eyeOpenR = 1.0f;
                _eyeGazeX = 0; _eyeGazeY = 0;
                break;
        }
    }

    void _renderFace() {
        _display.clearDisplay();
        _drawEye(38, 32, _eyeOpenL, _eyeGazeX, _eyeGazeY);
        _drawEye(90, 32, _eyeOpenR, _eyeGazeX, _eyeGazeY);
        _drawMouth();
        _display.display();
    }

    void _drawMouth() {
        switch (_expression) {
            case EX_HAPPY:
                _display.drawLine(50, 52, 58, 55, SSD1306_WHITE);
                _display.drawLine(58, 55, 64, 56, SSD1306_WHITE);
                _display.drawLine(64, 56, 70, 55, SSD1306_WHITE);
                _display.drawLine(70, 55, 78, 52, SSD1306_WHITE);
                break;
            case EX_ANGRY:
                // small frown (inverted smile)
                _display.drawLine(54, 56, 60, 53, SSD1306_WHITE);
                _display.drawLine(60, 53, 68, 53, SSD1306_WHITE);
                _display.drawLine(68, 53, 74, 56, SSD1306_WHITE);
                break;
            case EX_SLEEPY:
                _display.drawLine(54, 53, 62, 55, SSD1306_WHITE);
                _display.drawLine(62, 55, 70, 53, SSD1306_WHITE);
                break;
            case EX_NEUTRAL:
            default:
                _display.drawLine(52, 53, 76, 53, SSD1306_WHITE);
                break;
        }
    }

    void _drawEye(int cx, int cy, float open, int gx, int gy) {
        if (open < 0.12f) {
            // Closed — soft sleeping arch
            _display.drawLine(cx - 17, cy, cx - 9, cy - 3, SSD1306_WHITE);
            _display.drawLine(cx - 9, cy - 3, cx + 9, cy - 3, SSD1306_WHITE);
            _display.drawLine(cx + 9, cy - 3, cx + 17, cy, SSD1306_WHITE);
            return;
        }
        int ry = (int)(13.0f * open + 0.5f);
        if (ry < 3) ry = 3;
        _fillEllipse(cx, cy, 18, ry, SSD1306_WHITE);
        int px = cx + gx * 8;
        int py = cy + gy * 4;
        int pr = (open > 0.65f) ? 5 : (open > 0.35f ? 4 : 3);
        _display.fillCircle(px, py, pr, SSD1306_BLACK);
        _display.drawPixel(px - 2, py - 2, SSD1306_WHITE);   // shine
    }

    // Manual ellipse fill (independent of Adafruit_GFX version)
    void _fillEllipse(int cx, int cy, int rx, int ry, uint16_t color) {
        for (int y = -ry; y <= ry; y++) {
            int hw = (int)(rx * sqrt(1.0f - ((float)y * y) / (float)(ry * ry)));
            _display.drawFastHLine(cx - hw, cy + y, hw * 2 + 1, color);
        }
    }

    void _setContrast(uint8_t c) {
        _display.ssd1306_command(SSD1306_SETCONTRAST);
        _display.ssd1306_command(c);
    }
};

const uint8_t OledDisplay::_slidePriority[9] = { 0, 0, 1, 1, 2, 2, 3, 4, 5 };
const uint16_t OledDisplay::_slideDuration[9] = { 3500, 3000, 3500, 3000, 3500, 3000, 3000, 2000, 2000 };
const uint16_t OledDisplay::_slideInterval[9] = { 6000, 6000, 8000, 8000, 10000, 10000, 8000, 14000, 18000 };

#endif
