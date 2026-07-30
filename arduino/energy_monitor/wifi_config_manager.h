#ifndef WIFI_CONFIG_MANAGER_H
#define WIFI_CONFIG_MANAGER_H

#include <Arduino.h>
#include <WiFi.h>
#include <DNSServer.h>
#include <Preferences.h>
#include <vector>
#include "global.h"
#include "energy_data.h"
#include "config_manager.h"
#include "alarm_manager.h"
#include "outage_buffer.h"
#include "energy_tracker.h"
#include "ota_updater.h"
#include "web_dashboard.h"
#include "phase_combiner.h"

extern ConfigManager configManager;
extern AlarmManager  alarmManager;
extern OutageBuffer  outageBuffer;
extern EnergyTracker energyTracker;
extern OTAUpdater    otaUpdater;
extern PhaseCombiner combiner;

// ============================================================
struct StoredNetwork {
    String ssid;
    String password;
    bool lastConnected = false;
};

// ============================================================
class WiFiConfigManager {
public:
    WiFiConfigManager() {}

    bool begin() {
        LOGD("\n--- WiFiConfigManager ---");

        _instance = this;
        WiFi.onEvent(_onWiFiEvent);

        // Disable internal auto-reconnect so WE control when to retry
        WiFi.setAutoReconnect(false);

        _loadNetworks();

        // Always run AP + STA together
        WiFi.setSleep(false);
        WiFi.setTxPower(WIFI_POWER_19_5dBm);
        WiFi.mode(WIFI_AP_STA);
        WiFi.softAPConfig(IPAddress(192, 168, 4, 1), IPAddress(192, 168, 4, 1), IPAddress(255, 255, 255, 0));
        WiFi.softAP(AP_SSID.c_str(), AP_PASSWORD.length() > 0 ? AP_PASSWORD.c_str() : nullptr, 6);
        delay(100);
        _portalActive = false;
        _server.begin();

        LOGF("AP '%s' active on 192.168.4.1\n", AP_SSID.c_str());

        // Try saved networks (async, non-blocking)
        if (!_networks.empty()) {
            _tryStoredNetworks();
        } else {
            LOGD("No saved networks. AP ready for configuration.");
        }
        return WiFi.status() == WL_CONNECTED;
    }

    void loop() {
        _pollConnect();
        _handleClient(); // always serve HTTP (live data API)
        if (_portalActive) {
            _dns.processNextRequest();
        }
    }

    bool isConnected() {
        // We trust the event-driven state machine over WiFi.status() polling
        return _staState == STA_CONNECTED;
    }
    String getIP() const { return _localIP; }
    bool isPortalActive() const { return _portalActive; }
    void setLiveDataRef(EnergyData* ref) { _liveDataRef = ref; }
    void startCaptivePortal() {
        if (_portalActive) return;
        _dns.start(53, "*", IPAddress(192, 168, 4, 1));
        _portalActive = true;
        LOGF("Captive portal started on 192.168.4.1\n");
    }
    int getStaState() const { return _staState; }
    String getConnectingSsid() const { return _connectingSsid; }

    void autoReconnect() {
        if (_networks.empty()) return;
        if (_staState == STA_CONNECTED || _staState == STA_CONNECTING) return;

        // On disconnect event, try all networks async
        _tryStoredNetworks();
    }

private:
    enum WiFiState { STA_IDLE, STA_CONNECTING, STA_CONNECTED, STA_DISCONNECTED };

    std::vector<StoredNetwork> _networks;
    bool _connected = false;
    bool _portalActive = false;
    String _localIP;

    WiFiState _staState = STA_IDLE;
    String _connectingSsid;
    unsigned long _connectStart = 0;
    unsigned long _connectTimeout = 0;
    unsigned long _lastFailTime = 0;
    int _connectRetryCount = 0;
    size_t _nextNetworkIndex = 0;

    EnergyData* _liveDataRef = nullptr;

    DNSServer _dns;
    WiFiServer _server{80};
    String _currentRequest;
    String _requestPath;
    String _requestMethod;
    String _postBody;
    uint8_t _lastDisconnectReason = 0;  // stores WiFi.reasonCode() on failure

    // Singleton pointer for static event handler
    static WiFiConfigManager* _instance;
    static void _onWiFiEvent(WiFiEvent_t event, WiFiEventInfo_t info) {
        if (!_instance) return;
        switch (event) {
            case ARDUINO_EVENT_WIFI_STA_GOT_IP:
                _instance->_handleGotIP();
                break;
            case ARDUINO_EVENT_WIFI_STA_DISCONNECTED:
                _instance->_handleDisconnect(info.wifi_sta_disconnected.reason);
                break;
            default:
                break;
        }
    }

    void _handleGotIP() {
        LOGF("[WIFI] Got IP: %s\n", WiFi.localIP().toString().c_str());
        _staState = STA_CONNECTED;
        _connected = true;
        _localIP = WiFi.localIP().toString();
        _markLastConnected(WiFi.SSID());
        LOGF("[WIFI] Connected to %s (%s)\n", WiFi.SSID().c_str(), _localIP.c_str());
    }

    void _handleDisconnect(uint8_t reason) {
        _lastDisconnectReason = reason;
        _lastFailTime = millis();
        _connected = false;

        if (_staState == STA_CONNECTED) {
            LOGF("[WIFI] Connection lost (reason %d)\n", reason);
        } else if (_staState == STA_CONNECTING) {
            LOGF("[WIFI] Connection to %s failed (reason %d)\n", _connectingSsid.c_str(), reason);
            _connectRetryCount++;
        }
        _staState = STA_DISCONNECTED;
    }

    // ================ Async Connect ================

    // Begin async connection — register event handler, call WiFi.begin(), return immediately.
    // Success/failure delivered via event handlers. Call _pollConnect() periodically.
    bool _beginConnectAsync(const String& ssid, const String& password, unsigned long timeoutMs) {
        if (_staState == STA_CONNECTING) {
            LOGF("[WIFI] Already connecting to %s, ignoring %s\n", _connectingSsid.c_str(), ssid.c_str());
            return false;
        }

        LOGF("[WIFI] Connecting to [%s] ... ", ssid.c_str());

        // Ensure clean STA state
        WiFi.disconnect(false, false);
        delay(10);
        WiFi.config(INADDR_NONE, INADDR_NONE, INADDR_NONE);  // reset to DHCP

        _staState = STA_CONNECTING;
        _connectingSsid = ssid;
        _connectStart = millis();
        _connectTimeout = timeoutMs;
        _lastDisconnectReason = 0;

        WiFi.begin(ssid.c_str(), password.c_str());
        return true;
    }

    // Must be called from loop() — handles timeout + auto-retry
    void _pollConnect() {
        if (_staState == STA_CONNECTING) {
            if (_connectTimeout > 0 && millis() - _connectStart >= _connectTimeout) {
                LOGF("[WIFI] Connection to %s timed out (%lums)\n", _connectingSsid.c_str(), _connectTimeout);
                WiFi.disconnect(false, false);
                _lastDisconnectReason = 255;
                _lastFailTime = millis();
                _connectRetryCount++;
                _staState = STA_DISCONNECTED;
            }
            return;
        }

        // Auto-retry: if disconnected for >3s, try next saved network
        if (_staState == STA_DISCONNECTED && !_networks.empty()) {
            if (millis() - _lastFailTime > 3000) {
                _nextNetworkIndex = _connectRetryCount % _networks.size();
                StoredNetwork& net = _networks[_nextNetworkIndex];
                LOGF("[WIFI] Auto-retry #%d: trying %s\n", _connectRetryCount, net.ssid.c_str());
                _beginConnectAsync(net.ssid, net.password, 12000);
            }
        }
    }

    void _restartSta() {
        WiFi.disconnect(false, true);
        delay(10);
        WiFi.mode(WIFI_AP_STA);
        delay(10);
        _staState = STA_IDLE;
    }

    // Try stored networks one by one (async) — returns true if at least one attempt started
    bool _tryStoredNetworks() {
        for (size_t i = 0; i < _networks.size(); i++) {
            LOGF("Trying %s ... ", _networks[i].ssid.c_str());
            if (_beginConnectAsync(_networks[i].ssid, _networks[i].password, 8000)) {
                return true;  // first async attempt started
            }
        }
        return false;
    }

    void _loadNetworks() {
        _networks.clear();
        Preferences prefs;
        prefs.begin("wificfg", true);

        uint8_t count = prefs.getUChar("count", 0);
        if (count > MAX_SAVED_NETWORKS) count = MAX_SAVED_NETWORKS;

        for (uint8_t i = 0; i < count; i++) {
            String s = prefs.getString(("s" + String(i) + "_s").c_str(), "");
            String p = prefs.getString(("s" + String(i) + "_p").c_str(), "");
            bool last = prefs.getBool(("s" + String(i) + "_l").c_str(), false);
            if (s.length() > 0) {
                StoredNetwork n;
                n.ssid = s; n.password = p; n.lastConnected = last;
                _networks.push_back(n);
            }
        }
        prefs.end();

        // Sort: lastConnected first, then by order
        if (_networks.size() > 1) {
            for (size_t i = 0; i < _networks.size(); i++)
                if (_networks[i].lastConnected && i > 0) {
                    StoredNetwork n = _networks[i];
                    _networks.erase(_networks.begin() + i);
                    _networks.insert(_networks.begin(), n);
                    break;
                }
        }

        LOGF("Loaded %d network(s)\n", _networks.size());
        for (size_t i = 0; i < _networks.size(); i++)
            LOGF("  [%d] %s (last=%d)\n", i, _networks[i].ssid.c_str(), _networks[i].lastConnected);
    }

    void _saveNetwork(const String& ssid, const String& password) {
        for (int i = (int)_networks.size() - 1; i >= 0; i--)
            if (_networks[i].ssid == ssid) _networks.erase(_networks.begin() + i);

        StoredNetwork n;
        n.ssid = ssid;
        n.password = password;
        n.lastConnected = false;
        _networks.insert(_networks.begin(), n);
        while (_networks.size() > MAX_SAVED_NETWORKS) _networks.pop_back();

        Preferences prefs;
        prefs.begin("wificfg", false);
        prefs.putUChar("count", (uint8_t)_networks.size());
        for (size_t i = 0; i < _networks.size(); i++) {
            prefs.putString(("s" + String(i) + "_s").c_str(), _networks[i].ssid);
            prefs.putString(("s" + String(i) + "_p").c_str(), _networks[i].password);
            prefs.putBool(("s" + String(i) + "_l").c_str(), _networks[i].lastConnected);
        }
        prefs.end();
        LOGF("Saved '%s' as priority 0\n", ssid.c_str());
    }

    void _deleteNetwork(const String& ssid) {
        for (size_t i = 0; i < _networks.size(); i++)
            if (_networks[i].ssid == ssid) {
                _networks.erase(_networks.begin() + i);
                break;
            }
        Preferences prefs;
        prefs.begin("wificfg", false);
        prefs.putUChar("count", (uint8_t)_networks.size());
        for (size_t i = 0; i < _networks.size(); i++) {
            prefs.putString(("s" + String(i) + "_s").c_str(), _networks[i].ssid);
            prefs.putString(("s" + String(i) + "_p").c_str(), _networks[i].password);
            prefs.putBool(("s" + String(i) + "_l").c_str(), _networks[i].lastConnected);
        }
        prefs.end();
        LOGF("Forgot network '%s'\n", ssid.c_str());
    }

    // ================ Connection ================

    void _markLastConnected(const String& ssid) {
        for (size_t i = 0; i < _networks.size(); i++) {
            _networks[i].lastConnected = (_networks[i].ssid == ssid);
        }
        Preferences prefs;
        prefs.begin("wificfg", false);
        for (size_t i = 0; i < _networks.size(); i++) {
            prefs.putBool(("s" + String(i) + "_l").c_str(), _networks[i].lastConnected);
        }
        prefs.end();
    }

    // ================ Captive Portal ================

    void _startCaptivePortal() {
        WiFi.setSleep(false);
        WiFi.setTxPower(WIFI_POWER_19_5dBm);
        WiFi.mode(WIFI_AP_STA);
        WiFi.softAPConfig(IPAddress(192, 168, 4, 1), IPAddress(192, 168, 4, 1), IPAddress(255, 255, 255, 0));
        WiFi.softAP(AP_SSID.c_str(), AP_PASSWORD.length() > 0 ? AP_PASSWORD.c_str() : nullptr, 6);
        delay(100);
        _portalActive = true;

        _dns.start(53, "*", IPAddress(192, 168, 4, 1));
        _server.begin();

        LOGF("Captive portal started. Connect to AP '%s'\n", AP_SSID.c_str());
        LOGF("AP IP: 192.168.4.1\n");
    }

    void _stopCaptivePortal() {
        _dns.stop();
        _server.stop();
        WiFi.softAPdisconnect(true);
        _portalActive = false;
    }

    // ================ HTTP ================

    void _handleClient() {
        WiFiClient client = _server.available();
        if (!client) return;

        String req;
        _currentRequest = "";
        _requestPath = "/";
        _postBody = "";
        unsigned long timeout = millis() + 2000;

        while (client.connected() && millis() < timeout) {
            if (client.available()) {
                char c = (char)client.read();
                req += c;
                if (req.endsWith("\r\n\r\n")) {
                    timeout = millis() + 500;
                    break;
                }
            }
        }

        int sp1 = req.indexOf(' ');
        if (sp1 < 0) { client.stop(); return; }
        int sp2 = req.indexOf(' ', sp1 + 1);
        _requestMethod = req.substring(0, sp1);
        if (sp2 > sp1) _requestPath = req.substring(sp1 + 1, sp2);

        if (_requestMethod == "POST") {
            String lowerReq = req;
            lowerReq.toLowerCase();
            int cli = lowerReq.indexOf("content-length: ");
            if (cli >= 0) {
                int cle = req.indexOf("\r\n", cli);
                int blen = req.substring(cli + 16, cle).toInt();
                if (blen > 0 && blen < 1024) {
                    int bstart = req.indexOf("\r\n\r\n") + 4;
                    if (bstart + blen <= (int)req.length()) {
                        _postBody = req.substring(bstart, bstart + blen);
                    } else {
                        while ((int)req.length() < bstart + blen && millis() < timeout + 1000) {
                            if (client.available()) req += (char)client.read();
                        }
                        if (bstart + blen <= (int)req.length())
                            _postBody = req.substring(bstart, bstart + blen);
                    }
                }
            }
        }

        if (_requestPath == "/api/connect")  _handleConnect();
        else if (_requestPath == "/api/forget")  _handleForget();
        else if (_requestPath == "/api/info")   _handleInfo();
        else if (_requestPath == "/api/live")   _handleLive();
        else if (_requestPath == "/api/config") _handleConfig();
        else if (_requestPath == "/api/alarms") _handleAlarms();
        else if (_requestPath == "/api/alarms/clear") _handleAlarmsClear();
        else if (_requestPath == "/api/outage")     _handleOutageStatus();
        else if (_requestPath == "/api/energy")    _handleEnergy();

        else if (_requestPath == "/api/reset")     _handleReset();
        else if (_requestPath == "/api/ota-check") _handleOtaCheck();
        else if (_requestPath == "/api/ota-status") _handleOtaStatus();
        else if (_requestPath == "/api/logs")      _handleLogs();
        else if (_requestPath == "/api/scan")      _handleWifiScan();
        else if (_requestPath == "/style.css")  _handleCss();
        else if (_requestPath == "/favicon.ico") { _currentRequest = "HTTP/1.1 204 No Content\r\nConnection: close\r\n\r\n"; }
        else                                    _handleRoot();

        client.print(_currentRequest);
        client.flush();
        delay(10);
        client.stop();
    }

    void _handleLogs() {
        if (_requestMethod == "POST") {
            logManager.clear();
            String json = "{\"ok\":true}";
            _currentRequest = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " +
                              String(json.length()) + "\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n" + json;
        } else {
            String json = logManager.getJson();
            _currentRequest = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " +
                              String(json.length()) + "\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n" + json;
        }
    }

    void _handleInfo() {
        String apIP = WiFi.softAPIP().toString();
        String staIP = _connected ? WiFi.localIP().toString() : "---";
        String staSSID = _connected ? WiFi.SSID() : (_connectingSsid.length() ? _connectingSsid : "---");
        String staStatus = _connected ? "connected" : (_staState == STA_CONNECTING ? "connecting" : "disconnected");
        String json = "{\"ap\":\"" + AP_SSID + "\",\"apIP\":\"" + apIP + "\""
                      ",\"sta\":\"" + staSSID + "\",\"ip\":\"" + staIP + "\""
                      ",\"staStatus\":\"" + staStatus + "\""
                       ",\"reason\":" + String(_lastDisconnectReason) + 
                       ",\"mqtt\":false,\"saved\":[";
        for (size_t i = 0; i < _networks.size(); i++) {
            if (i > 0) json += ",";
            json += "{\"ssid\":\"" + _escapeJson(_networks[i].ssid) + "\""
                    ",\"last\":" + String(_connected && _networks[i].ssid == WiFi.SSID() ? "true" : "false") + "}";
        }
        json += "]}";
        _currentRequest = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " +
                          String(json.length()) + "\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n" + json;
    }

    void _handleLive() {
        String json;
        if (_liveDataRef) {
            String alarmsJson = alarmManager.getRecentJson();
            json = WebDashboard::dataToJson(*_liveDataRef, configManager.isServerReachable(), alarmsJson, configManager.getPhaseCount());
            // Append time-valid and temp threshold before closing brace
            String tv = energyTracker.hasValidTime() ? "true" : "false";
            int pos = json.lastIndexOf('}');
            if (pos > 0) json = json.substring(0, pos) + ",\"tv\":" + tv + ",\"tt\":" + String(configManager.getTemperatureThreshold(), 1) + "}";
        } else {
            json = "{\"t\":\"\",\"d\":\"\",\"f\":0,\"a\":[0,0,0,0,0,0],\"b\":[0,0,0,0,0,0],\"c\":[0,0,0,0,0,0],\"pt\":0,\"sr\":false,\"tv\":false}";
        }
        _currentRequest = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " +
                          String(json.length()) + "\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n" + json;
    }

    void _handleConfig() {
        if (_requestMethod == "POST") {
            // Save config from web UI
            configManager.applyConfigJson(_postBody);
            String resp = configManager.toJson();
            _currentRequest = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " +
                              String(resp.length()) + "\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n" + resp;
        } else {
            // GET — return current config
            String json = configManager.toJson();
            _currentRequest = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " +
                              String(json.length()) + "\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n" + json;
        }
    }

    void _handleAlarms() {
        String json = alarmManager.getRecentJson();
        _currentRequest = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " +
                          String(json.length()) + "\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n" + json;
    }

    void _handleAlarmsClear() {
        alarmManager.clearAll();
        String json = "{\"ok\":true}";
        _currentRequest = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " +
                          String(json.length()) + "\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n" + json;
    }

    void _handleOutageStatus() {
        String json = outageBuffer.getStatusJson();
        _currentRequest = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " +
                          String(json.length()) + "\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n" + json;
    }

    void _handleEnergy() {
        String json = energyTracker.toJson();
        _currentRequest = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " +
                          String(json.length()) + "\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n" + json;
    }

    void _handleReset() {
        combiner.resetEnergy();
        energyTracker.reset();
        alarmManager.clearAll();
        outageBuffer.clear();
        String json = "{\"ok\":true,\"msg\":\"All data reset\"}";
        _currentRequest = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " +
                          String(json.length()) + "\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n" + json;
    }

    void _handleRoot() {
        String html = _buildHtmlPage();
        _currentRequest = "HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: " +
                          String(html.length()) + "\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n" + html;
    }

    void _handleWifiScan() {
        // ESP32 single radio: scan FAILS if STA is in "connecting" state (ESP_ERR_WIFI_STATE).
        // Even if our _staState says DISCONNECTED, internal ESP-IDF may be auto-retrying.
        // Fix: always disconnect STA before scan, then resume after.
        bool wasConnected = (_staState == STA_CONNECTED);
        bool wasConnecting = (_staState == STA_CONNECTING);

        WiFi.disconnect(false);
        _staState = STA_DISCONNECTED;
        delay(100);

        int n = WiFi.scanNetworks(true);
        if (n == WIFI_SCAN_FAILED) {
            WiFi.disconnect(false);
            delay(200);
            n = WiFi.scanNetworks(true);
        }

        if (n == WIFI_SCAN_RUNNING) {
            unsigned long start = millis();
            while (WiFi.scanComplete() < 0 && millis() - start < 5000) {
                _handleClient();
                delay(10);
            }
            n = WiFi.scanComplete();
        }

        String json = "[";
        if (n > 0) {
            for (int i = 0; i < n; i++) {
                if (i > 0) json += ",";
                String s = WiFi.SSID(i);
                s.replace("\\", "\\\\"); s.replace("\"", "\\\"");
                json += "{\"s\":\"" + s + "\",\"r\":" + String(WiFi.RSSI(i));
                json += ",\"e\":" + String(WiFi.encryptionType(i)) + "}";
            }
        }
        json += "]";
        WiFi.scanDelete();

        // Resume STA connection if we interrupted it
        if (wasConnecting || wasConnected) {
            _lastFailTime = millis() - 3000; // triggers immediate retry in _pollConnect
        }

        _currentRequest = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " +
                          String(json.length()) + "\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n" + json;
    }

    void _handleCss() {
        String c;
        c += "*{margin:0;padding:0;box-sizing:border-box}";
        c += "body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background:#f2f2f7;color:#1c1c1e;min-height:100vh}";
        c += ".cnt{max-width:480px;margin:0 auto;padding:12px}";
        c += "@media(min-width:768px){.cnt{max-width:1200px}.mon-grid{grid-template-columns:repeat(3,1fr)!important}}";

        c += ".top{background:linear-gradient(135deg,#007aff,#5856d6);margin:-12px -12px 12px;padding:28px 12px 20px;border-radius:0 0 24px 24px;text-align:center;color:#fff}";
        c += ".top h1{font-size:22px;font-weight:700;color:#fff;padding:0}";
        c += ".top h1 span{display:block;font-size:13px;font-weight:400;color:rgba(255,255,255,.7);margin-top:4px}";
        c += ".top .ico{width:48px;height:48px;border-radius:14px;background:rgba(255,255,255,.2);display:inline-flex;align-items:center;justify-content:center;margin-bottom:10px;font-size:22px;backdrop-filter:blur(4px)}";
        c += ".top h1{font-size:16px;font-weight:700;word-break:keep-all;white-space:normal;line-height:1.4}";

        c += ".crd{background:#fff;border-radius:14px;margin-bottom:12px;padding:16px;box-shadow:0 1px 3px rgba(0,0,0,.08)}";
        c += ".crd-hd{font-size:12px;font-weight:600;color:#8e8e93;margin-bottom:12px;letter-spacing:.3px;padding-left:10px;border-left:3px solid #007aff}";

        c += ".rw{display:flex;justify-content:space-between;align-items:center;padding:9px 0}";
        c += ".rw+.rw{border-top:1px solid #f2f2f7}";
        c += ".rw-l{font-size:14px;color:#8e8e93}";
        c += ".rw-r{font-size:14px;font-weight:500;color:#1c1c1e;display:flex;align-items:center;gap:6px}";

        c += ".tag{font-size:11px;font-weight:600;padding:2px 10px;border-radius:20px}";
        c += ".tag-gr{background:#e8f5e9;color:#2e7d32}";
        c += ".tag-rd{background:#ffebee;color:#c62828}";
        c += ".tag-gy{background:#f2f2f7;color:#8e8e93}";
        c += ".tag-bl{background:#e3f2fd;color:#1565c0}";

        c += ".lst{list-style:none}";
        c += ".btn-p{background:#007aff;color:#fff;border:none;padding:10px 22px;border-radius:10px;font-size:14px;font-weight:600;cursor:pointer}";
        c += ".btn-p:hover{background:#0062cc}";
        c += ".btn-p:disabled{background:#a0c4ff;cursor:default}";
        c += ".btn-s{border:1px solid #e0e0e0;background:#fff;color:#1c1c1e;padding:10px 22px;border-radius:10px;font-size:14px;font-weight:500;cursor:pointer}";
        c += ".fld{margin-bottom:12px}";
        c += ".fld label{display:block;font-size:12px;color:#8e8e93;margin-bottom:4px;font-weight:500}";
        c += ".fld input{width:100%;padding:10px 12px;border:1px solid #e0e0e0;border-radius:10px;font-size:14px;outline:none;transition:border-color .2s}";
        c += ".fld input:focus{border-color:#007aff}";
        c += ".fld-rw{display:flex;gap:8px}";
        c += ".fld-rw .fld{flex:1}";
        c += ".btns{display:flex;gap:8px;margin-top:12px}";
        c += ".btns .btn-p{flex:1}";
        c += ".toast{padding:8px 12px;border-radius:8px;font-size:12px;margin-top:8px;text-align:center}";
        c += ".toast.ok{background:#e8f5e9;color:#2e7d32}";
        c += ".toast.er{background:#ffebee;color:#c62828}";
        c += ".toast.wa{background:#fff8e1;color:#f57f17}";
        c += ".fw-li{display:flex;align-items:center;padding:12px 0;gap:10px}";
        c += ".fw-li+.fw-li{border-top:1px solid #f2f2f7}";
        c += ".fw-li .fw-nm{flex:1;font-size:14px;font-weight:500}";
        c += ".fw-li .fw-bd{font-size:11px;padding:2px 8px;border-radius:4px;background:#e8f5e9;color:#2e7d32;font-weight:600}";
c += ".fw-li .fw-dl{font-size:12px;color:#ff3b30;background:none;border:1px solid #ffcdd2;cursor:pointer;padding:4px 10px;border-radius:6px;font-weight:500}";
c += ".fw-li .fw-dl:hover{background:#ffebee;border-color:#ff3b30}";
        c += ".fw-empty{padding:20px;text-align:center;color:#8e8e93;font-size:13px}";
        c += ".net-item{padding:10px;margin:4px 0;border:1px solid #e0e0e0;border-radius:10px;cursor:pointer;background:#fff;transition:.15s}";
        c += ".net-item:hover{border-color:#007aff;background:#f0f7ff}";

        // Log panel styles
        c += ".log-container{background:#0d1117;border-radius:10px;padding:8px;max-height:400px;overflow-y:auto;font-family:&apos;SF Mono&apos;,&apos;Cascadia Code&apos;,&apos;Courier New&apos;,monospace;font-size:11px;line-height:1.5;direction:ltr;text-align:left}";
        c += ".log-entry{display:flex;gap:8px;padding:2px 4px;border-bottom:1px solid #1e1e2e;word-break:break-all}";
        c += ".log-entry:last-child{border-bottom:none}";
        c += ".log-ts{color:#6e7681;white-space:nowrap;flex-shrink:0;min-width:60px}";
        c += ".log-msg{color:#c9d1d9;flex:1}";
        c += ".log-err .log-msg{color:#f85149}";
        c += ".log-ok .log-msg{color:#3fb950}";
        c += ".log-warn .log-msg{color:#d29922}";
        c += ".log-empty{text-align:center;padding:24px 16px;color:#6e7681;font-size:13px}";
        c += ".log-error{text-align:center;padding:16px;color:#f85149;font-size:12px}";
        c += ".log-clear{background:transparent;border:1px solid #e0e0e0;border-radius:8px;padding:6px 14px;font-size:12px;font-weight:600;cursor:pointer;color:#555;transition:all .2s}";
        c += ".log-clear:hover{background:#ffebee;border-color:#ff3b30;color:#ff3b30}";
        c += ".log-auto{display:flex;justify-content:flex-end;margin-top:8px;gap:8px;align-items:center}";

        c += ".sp{text-align:center;padding:40px 0;color:#8e8e93}";
        c += ".sp::after{content:'';display:inline-block;width:22px;height:22px;border:3px solid #e0e0e0;border-top-color:#007aff;border-radius:50%;animation:sp .7s linear infinite}";
        c += "@keyframes sp{to{transform:rotate(360deg)}}";

        c += WebDashboard::buildStyles();
        _currentRequest = "HTTP/1.1 200 OK\r\nContent-Type: text/css; charset=utf-8\r\nContent-Length: " +
                          String(c.length()) + "\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n" + c;
    }

    void _handleOtaCheck() {
        if (otaUpdater.isUpdateInProgress()) {
            String json = "{\"ok\":false,\"msg\":\"Update already in progress\"}";
            _currentRequest = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " +
                              String(json.length()) + "\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n" + json;
            return;
        }
        otaUpdater.triggerCheck();
        String json = "{\"ok\":true,\"msg\":\"Check triggered\"}";
        _currentRequest = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " +
                          String(json.length()) + "\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n" + json;
    }

    void _handleOtaStatus() {
        String json;
        if (otaUpdater.isUpdateInProgress()) {
            json = "{\"status\":\"updating\",\"version\":\"" + otaUpdater.getNewVersion() + "\"}";
        } else {
            json = "{\"status\":\"idle\",\"version\":\"" CURRENT_VERSION "\"}";
        }
        _currentRequest = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " +
                          String(json.length()) + "\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n" + json;
    }

    void _handleForget() {
        String ssid;
        int s = _postBody.indexOf("\"ssid\"");
        if (s >= 0) {
            int colon = _postBody.indexOf(':', s + 6);
            int start = _postBody.indexOf('"', colon + 1);
            int end = _postBody.indexOf('"', start + 1);
            if (start >= 0 && end > start) ssid = _postBody.substring(start + 1, end);
        }
        if (ssid.length() > 0) {
            _deleteNetwork(ssid);
        }
        String resp = "{\"ok\":true}";
        _currentRequest = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " +
                          String(resp.length()) + "\r\nConnection: close\r\n\r\n" + resp;
    }

    void _handleConnect() {
        String ssid, password;
        int s = _postBody.indexOf("\"ssid\"");
        if (s >= 0) {
            int colon = _postBody.indexOf(':', s + 6);
            int start = _postBody.indexOf('"', colon + 1);
            int end = _postBody.indexOf('"', start + 1);
            if (start >= 0 && end > start) ssid = _postBody.substring(start + 1, end);
        }
        int p = _postBody.indexOf("\"password\"");
        if (p >= 0) {
            int colon = _postBody.indexOf(':', p + 9);
            int start = _postBody.indexOf('"', colon + 1);
            int end = _postBody.indexOf('"', start + 1);
            if (start >= 0 && end > start) password = _postBody.substring(start + 1, end);
        }

        if (ssid.length() == 0) {
            LOGF("POST /api/connect — body=[%s]\n", _postBody.c_str());
            _currentRequest = "HTTP/1.1 400 Bad Request\r\nContent-Type: application/json\r\nContent-Length: 22\r\nConnection: close\r\n\r\n{\"ok\":false,\"msg\":\"SSID required\"}";
            return;
        }

        LOGF("POST /api/connect — ssid=[%s]\n", ssid.c_str());
        _saveNetwork(ssid, password);
        _connectRetryCount = 0;
        _nextNetworkIndex = 0;

        if (_beginConnectAsync(ssid, password, 10000)) {
            String resp = "{\"ok\":true,\"msg\":\"Connecting...\"}";
            _currentRequest = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " + String(resp.length()) + "\r\nConnection: close\r\n\r\n" + resp;
        } else {
            String resp = "{\"ok\":false,\"msg\":\"Already connecting to " + _connectingSsid + "\"}";
            _currentRequest = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " + String(resp.length()) + "\r\nConnection: close\r\n\r\n" + resp;
        }
    }

    String _escapeJson(const String& s) {
        String out;
        for (size_t i = 0; i < s.length(); i++) {
            char c = s[i];
            if (c == '"' || c == '\\') out += '\\';
            out += c;
        }
        return out;
    }

    String _buildHtmlPage() {
        String h;
        h.reserve(20000);
        h += "<!DOCTYPE html><html><head>";
        h += "<meta charset=utf-8>";
        h += "<meta name=viewport content='width=device-width,initial-scale=1,maximum-scale=1,user-scalable=no'>";
        h += "<title>Energy Monitoring System</title>";
        h += "<link rel=stylesheet href=/style.css>";
        h += "</head><body><div class=cnt>";

        h += "<div class=top><div class=ico><svg width=24 height=24 viewBox='0 0 24 24' fill=none stroke=white stroke-width=2 stroke-linecap=round stroke-linejoin=round><circle cx='12' cy='12' r='10'/><polyline points='12 6 12 12 16 14'/></svg></div><h1>Energy Monitoring System</h1><div id=liveClock style='font-size:14px;font-weight:600;margin-top:6px;opacity:.9'>--:--:--</div></div>";

        // Tabs
        h += "<div class=tab-bar><button class='tab act' id=t0 onclick=swTab(0)>📡 WiFi</button><button class=tab id=t1 onclick=swTab(1)>⚡ Monitor</button><button class=tab id=t2 onclick=swTab(2)>⚙ Settings</button><button class=tab id=t3 onclick=swTab(3)>&#x1F4C4; Log</button></div>";

        // === Panel 0: WiFi Setup ===
        h += "<div class='pan act' id=pan0>";

        // Card: Device Status
        h += "<div class=crd>";
        h += "<div class=crd-hd>Device Status</div>";
        h += "<div class=rw><div class=rw-l>AP Network</div><div class=rw-r id=apN></div></div>";
        h += "<div class=rw><div class=rw-l>AP IP</div><div class=rw-r>192.168.4.1</div></div>";
        h += "<div class=rw><div class=rw-l>Status</div><div class=rw-r id=staS></div></div>";
        h += "<div class=rw><div class=rw-l>Connected To</div><div class=rw-r id=staN>---</div></div>";
        h += "<div class=rw><div class=rw-l>STA IP</div><div class=rw-r id=staI>---</div></div>";
        h += "</div>";

        // Card: Add Network
        h += "<div class=crd>";
        h += "<div class=crd-hd>Add WiFi Network</div>";
        h += "<div style='margin-bottom:10px'>";
        h += "<button class='btn btn-p' onclick='scanWiFi()' id='scanBtn' type=button>📶 Scan Networks</button>";
        h += "<div id=scanResult style='margin-top:8px'></div>";
        h += "<div id=scanList style='margin-top:4px'></div>";
        h += "</div>";
        h += "<form onsubmit='return doSave(event)'>";
        h += "<div class=fld-rw>";
        h += "<div class=fld><label for=ssidIn>Network Name (SSID)</label><input type=text id=ssidIn placeholder='Enter SSID' autocomplete=off maxlength=32></div>";
        h += "<div class=fld><label for=pwIn>Password</label><input type=password id=pwIn placeholder='Password' autocomplete=off maxlength=64></div>";
        h += "</div>";
        h += "<div class=btns><button type=submit class='btn btn-p' id=btnSave>Save & Connect</button></div>";
        h += "<div class=toast id=toast></div>";
        h += "</form>";
        h += "</div>";

        // Card: Saved Networks
        h += "<div class=crd>";
        h += "<div class=crd-hd>Saved Networks (<span id=savedCount>0</span>/3)</div>";
        h += "<div id=savedList><div class=fw-empty>No saved networks</div></div>";
        h += "</div>";

        h += "</div>"; // end pan0

        // === Panel 1: Live Monitor ===
        h += "<div class='pan' id=pan1>";
        h += WebDashboard::buildHtml();
        h += "</div>";

        // === Panel 2: Settings ===
        h += "<div class='pan' id=pan2>";
        h += WebDashboard::buildSettingsHtml();
        h += "</div>";

        // === Panel 3: Log ===
        h += "<div class='pan' id=pan3>";
        h += WebDashboard::buildLogHtml();
        h += "</div>";

        h += "<script>";

        // ---- helpers ----
        h += "function $i(i){return document.getElementById(i)}";
        h += "function esc(s){return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;')}";

        // ---- load info + saved networks ----
        h += "function loadI(){";
        h += "var x=new XMLHttpRequest();";
        h += "x.open('GET','/api/info');";
        h += "x.onload=function(){";
        h += "if(x.status!=200)return;";
        h += "try{var d=JSON.parse(x.responseText)}catch(e){return}";
        h += "$i('apN').textContent=d.ap||'---';";
        h += "var s=$i('staS');";
        h += "if(d.staStatus=='connected'){";
        h += "s.innerHTML='<span class=tag tag-gr>Connected</span>';";
        h += "$i('staN').textContent=d.sta;$i('staI').textContent=d.ip;";
        h += "}else{";
        h += "s.innerHTML='<span class=tag tag-rd>Disconnected</span>';";
        h += "$i('staN').textContent='---';$i('staI').textContent='---';";
        h += "}";
        h += "if(d.saved){";
        h += "$i('savedCount').textContent=d.saved.length;";
        h += "var L=$i('savedList');L.innerHTML='';";
        h += "if(!d.saved.length){L.innerHTML='<div class=fw-empty>No saved networks</div>'}";
        h += "for(var i=0;i<d.saved.length;i++){";
        h += "var n=d.saved[i];";
        h += "var li=document.createElement('div');li.className='fw-li';";
        h += "li.innerHTML=";
        h += "'<span class=fw-nm>'+esc(n.ssid)+'</span>'";
        h += "+(n.last?'<span class=fw-bd>Connected</span>':'')";
        h += "+'<button class=fw-dl onclick=doForget(\"'+esc(n.ssid)+'\")>Delete</button>';";
        h += "L.appendChild(li)}";
        h += "}};x.send()}";

        // ---- select scanned network ----
        h += "function selectNet(s){$i('ssidIn').value=s;$i('pwIn').focus()}";

        // ---- scan WiFi ----
        h += "function scanWiFi(){";
        h += "var b=$i('scanBtn'),r=$i('scanResult'),l=$i('scanList');";
        h += "b.disabled=true;r.innerHTML='Scanning...';l.innerHTML='';";
        h += "var x=new XMLHttpRequest();";
        h += "x.open('GET','/api/scan');";
        h += "x.onload=function(){b.disabled=false;";
        h += "if(x.status!=200){r.innerHTML='<span class=toast er>Scan failed</span>';return}";
        h += "try{var nets=JSON.parse(x.responseText)}catch(e){r.innerHTML='<span class=toast er>Parse error</span>';return}";
        h += "if(!nets.length){r.innerHTML='<span class=toast wa>No networks found</span>';return}";
        h += "r.innerHTML='<span class=toast ok>Found '+nets.length+' network(s)</span>';";
        h += "var html='';";
        h += "for(var i=0;i<nets.length;i++){";
        h += "var n=nets[i],rssi=n.r;";
        h += "var pc=Math.min(100,Math.max(0,2*(rssi+100)));";
        h += "var bars= pc<25?1: pc<50?2: pc<75?3:4;";
        h += "var co= pc<25?'#ef4444': pc<50?'#f59e0b': pc<75?'#22c55e':'#16a34a';";
        h += "html+='<div class=net-item onclick=\\'selectNet(\\\"'+esc(n.s)+'\\\")\\'>';";
        h += "html+='<div style=display:flex;justify-content:space-between;align-items:center>';";
        h += "html+='<span>'+esc(n.s)+'</span>';";
        h += "html+='<div style=display:flex;align-items:center;gap:6px>';";
        h += "html+='<div style=display:flex;align-items:flex-end;height:18px;gap:2px>';";
        h += "for(var j=0;j<4;j++){";
        h += "var bh=(j+1)*4;";
        h += "html+='<div style=width:5px;height:'+bh+'px;border-radius:1px;background:'+(j<bars?co:'#e5e7eb')+'></div>'";
        h += "}";
        h += "html+='</div>';";
        h += "html+='<span style=font-size:13px;font-weight:600;color:'+co+'>'+pc+'%</span>';";
        h += "html+='</div></div></div>'";
        h += "}";
        h += "l.innerHTML=html";
        h += "};";
        h += "x.onerror=function(){b.disabled=false;r.innerHTML='<span class=toast er>Request failed</span>'};";
        h += "x.send()}";

        // ---- save & connect ----
        h += "function doSave(e){";
        h += "e.preventDefault();";
        h += "var ss=$i('ssidIn').value.trim();";
        h += "var pw=$i('pwIn').value;";
        h += "var t=$i('toast');var b=$i('btnSave');";
        h += "if(!ss){t.textContent='SSID is required';t.className='toast er';return}";
        h += "t.textContent='Connecting...';t.className='toast wa';b.disabled=true;";
        h += "var x=new XMLHttpRequest();";
        h += "x.open('POST','/api/connect');";
        h += "x.setRequestHeader('Content-Type','application/json');";
        h += "x.onload=function(){";
        h += "try{var d=JSON.parse(x.responseText)}catch(e){t.textContent='Error parsing response';t.className='toast er';b.disabled=false;return}";
        h += "if(d.ok&&d.ip){t.textContent='Connected! IP: '+d.ip;t.className='toast ok';$i('ssidIn').value='';$i('pwIn').value=''}else{t.textContent=d.msg||'Saved';t.className='toast ok'}";
        h += "loadI();";
        h += "b.disabled=false;";
        h += "};x.onerror=function(){t.textContent='Network error';t.className='toast er';b.disabled=false};";
        h += "x.send(JSON.stringify({ssid:ss,password:pw}));return false}";

        // ---- forget ----
        h += "function doForget(ss){";
        h += "if(!confirm('Remove \"'+ss+'\" from saved networks?'))return;";
        h += "var x=new XMLHttpRequest();";
        h += "x.open('POST','/api/forget');";
        h += "x.setRequestHeader('Content-Type','application/json');";
        h += "x.onload=function(){loadI()};";
        h += "x.onerror=function(){};";
        h += "x.send(JSON.stringify({ssid:ss}));return false}";

        // ---- tab switcher (3 tabs) ----
        h += "var _tabLabels=['WiFi Setup','Live Monitor','Settings','System Log'];";
        h += "function swTab(i){";
        h += "stopMon();stopCfg();stopLog();";
        h += "for(var j=0;j<4;j++){";
        h += "document.getElementById('pan'+j).className='pan'+(j==i?' act':'');";
        h += "document.getElementById('t'+j).className='tab'+(j==i?' act':'');";
        h += "}";
        h += "if(i==1)startMon();else if(i==2)startCfg();else if(i==3)startLog();";
        h += "}";

        h += "\n// === Monitor JS ===\n";
        h += WebDashboard::buildJs();
        h += "\n// === Settings JS ===\n";
        h += WebDashboard::buildSettingsJs();
        h += "\n// === Log JS ===\n";
        h += WebDashboard::buildLogJs();

        // ---- live clock ----
        // ONLY server time. No browser fallback. If no server → warning.
        h += "var _ptDate='',_ptH=0,_ptM=0,_ptS=0,_ptTick=0,_ptParsed='',_ptGot=false;";
        h += "var _sh=['0','1','2','3','4','5','6','7','8','9'];";
        h += "var _fn=function(v){return String(v).split('').map(function(c){return _sh[parseInt(c)]||c}).join('')};";
        h += "function updateClock(){";
        h += "if(_persianTime&&_persianTime!==_ptParsed){";
        h += "_ptParsed=_persianTime;_ptGot=true;";
        h += "var t=_persianTime.split(' ');";
        h += "if(t.length==2){_ptDate=t[0];var p=t[1].split(':');if(p.length==3){_ptH=parseInt(p[0]);_ptM=parseInt(p[1]);_ptS=parseInt(p[2]);_ptTick=0}}";
        h += "_persianTime=''}";
        h += "if(_ptGot){";
        h += "var h=_ptH,m=_ptM,s=_ptS+_ptTick;";
        h += "if(s>=60){s-=60;m++}if(m>=60){m-=60;h++}if(h>=24)h=0;";
        h += "_ptTick++;";
        h += "$i('liveClock').textContent=_ptDate+' '+_fn(h)+':'+_fn(m)+':'+_fn(s);";
        h += "}else{";
        h += "$i('liveClock').innerHTML='<span style=color:#ff9500>&#9888; Connect to server for time</span>';}";
        h += "}";
        h += "updateClock();setInterval(updateClock,1000);loadI();setInterval(loadI,5000)";

        h += "</script></div></body></html>";
        return h;
    }
};

WiFiConfigManager* WiFiConfigManager::_instance = nullptr;

#endif





