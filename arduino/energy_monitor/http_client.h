#ifndef HTTP_CLIENT_H
#define HTTP_CLIENT_H

#include <Arduino.h>
#include <WiFi.h>
#include <WiFiClientSecure.h>
#include "global.h"

class HttpClient {
public:
    using LoopCallback = void(*)();

    HttpClient() : _loopCb(nullptr) {}

    void setLoopCallback(LoopCallback cb) { _loopCb = cb; }

    // POST data to API — returns true/false + response body for config parsing
    bool postData(const String& deviceId, const String& jsonPayload, String& responseBody) {
        responseBody = "";
        // Parse URL into host + port + path
        String host, path;
        uint16_t port;
        if (!_parseUrl(API_BASE_URL, host, port, path)) {
            LOGD("[HTTP] ERROR: Invalid API_BASE_URL: " + API_BASE_URL);
            return false;
        }
        path += API_ENDPOINT;
        path += "?deviceId=";
        path += deviceId;

        String fullUrl = API_BASE_URL + API_ENDPOINT + "?deviceId=" + deviceId;

        LOGD("");
        LOGD("========================================");
        LOGD("[HTTP] Sending energy data...");
        LOGD("[HTTP] URL: " + fullUrl);
        LOGD("[HTTP] Host: " + host + ":" + String(port));
        LOGD("[HTTP] Path: " + path);
        LOGD("[HTTP] Device: " + deviceId);
        LOGD("[HTTP] Payload: " + jsonPayload);
        LOGD("----------------------------------------");

        bool isSecure = API_BASE_URL.startsWith("https://");
        WiFiClient *clientPtr = nullptr;
        WiFiClient plainClient;
        WiFiClientSecure secureClient;
        if (isSecure) {
            secureClient.setInsecure();
            secureClient.setTimeout(1000);
            clientPtr = &secureClient;
        } else {
            plainClient.setTimeout(1000);
            clientPtr = &plainClient;
        }
        WiFiClient &client = *clientPtr;

        if (!client.connect(host.c_str(), port, 1500)) {
            LOGD("[HTTP] ERROR: Connection FAILED!");
            LOGD("========================================");
            return false;
        }
        yield();

        // Build HTTP POST request
        String request;
        request += "POST " + path + " HTTP/1.1\r\n";
        request += "Host: " + host + ":" + String(port) + "\r\n";
        request += "Content-Type: application/json\r\n";
        request += "Content-Length: " + String(jsonPayload.length()) + "\r\n";
        request += "Connection: close\r\n";
        request += "\r\n";
        request += jsonPayload;

        // Send
        size_t sent = client.print(request);
        LOGF("[HTTP] Sent %d bytes\n", sent);

        // Read response
        String raw;
        raw.reserve(MAX_HTTP_RESPONSE);
        unsigned long timeout = millis() + 5000;
        while (client.connected() && millis() < timeout) {
            if (_loopCb) _loopCb();
            while (client.available()) {
                if (raw.length() >= MAX_HTTP_RESPONSE) break;
                char c = client.read();
                raw += c;
            }
            delay(1);
        }
        // Drain any remaining data (client may have disconnected but data is buffered)
        unsigned long drainStart = millis();
        while (client.available() && millis() - drainStart < 200) {
            if (raw.length() >= MAX_HTTP_RESPONSE) break;
            char c = client.read();
            raw += c;
        }
        client.stop();

        // Extract status code
        int statusCode = 0;
        if (raw.length() >= 12) {
            String statusStr = raw.substring(9, 12);
            statusCode = statusStr.toInt();
        }

        LOGD("----------------------------------------");
        LOGD("[HTTP] Response received:");
        if (raw.length() > 0) {
            int eol = raw.indexOf('\r');
            if (eol > 0) {
                LOGD("[HTTP] " + raw.substring(0, eol));
            }
            int bodyStart = raw.indexOf("\r\n\r\n");
            if (bodyStart > 0) {
                responseBody = raw.substring(bodyStart + 4);
                responseBody.trim();
                if (responseBody.length() > 0) {
                    LOGD("[HTTP] Body: " + responseBody);
                }
            }
        } else {
            LOGD("[HTTP] (empty response)");
        }

        bool ok = (statusCode >= 200 && statusCode < 300);
        if (ok) {
            LOGF("[HTTP] SUCCESS (HTTP %d)\n", statusCode);
        } else {
            LOGF("[HTTP] FAILED (HTTP %d)\n", statusCode);
        }
        LOGD("========================================");
        return ok;
    }

private:
    LoopCallback _loopCb;

    // Parse URL into host + port + path
    // eg: "http://192.168.1.100:5103" -> host="192.168.1.100", port=5103, path=""
    // eg: "https://example.com/api" -> host="example.com", port=443, path="/api"
    bool _parseUrl(const String& url, String& host, uint16_t& port, String& path) {
        String remaining = url;

        // Remove protocol
        bool isSecure = false;
        if (remaining.startsWith("https://")) {
            isSecure = true;
            remaining = remaining.substring(8);
        } else if (remaining.startsWith("http://")) {
            remaining = remaining.substring(7);
        } else {
            return false; // Invalid protocol
        }

        // Split host from path
        int slashIdx = remaining.indexOf('/');
        String hostPart;
        if (slashIdx >= 0) {
            hostPart = remaining.substring(0, slashIdx);
            path = remaining.substring(slashIdx);
        } else {
            hostPart = remaining;
            path = "";
        }

        // Split port from host
        int colonIdx = hostPart.indexOf(':');
        if (colonIdx >= 0) {
            host = hostPart.substring(0, colonIdx);
            port = (uint16_t)hostPart.substring(colonIdx + 1).toInt();
        } else {
            host = hostPart;
            port = isSecure ? 443 : 80;
        }

        return host.length() > 0;
    }
};

#endif
