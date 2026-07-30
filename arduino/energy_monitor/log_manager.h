#ifndef LOG_MANAGER_H
#define LOG_MANAGER_H

#include <Arduino.h>

#define LOG_BUFFER_SIZE 60
#define LOG_LINE_MAX 180

class LogManager {
public:
    LogManager() : _head(0), _count(0) {}

    void add(const char* msg) {
        size_t len = strlen(msg);
        if (len >= LOG_LINE_MAX) len = LOG_LINE_MAX - 1;
        memcpy(_lines[_head], msg, len);
        _lines[_head][len] = '\0';
        _millis[_head] = millis();
        _head = (_head + 1) % LOG_BUFFER_SIZE;
        if (_count < LOG_BUFFER_SIZE) _count++;
    }

    void add(const String& msg) {
        add(msg.c_str());
    }

    String getJson() {
        String json = "[";
        int start = (_count < LOG_BUFFER_SIZE) ? 0 : _head;
        int total = (_count < LOG_BUFFER_SIZE) ? _count : LOG_BUFFER_SIZE;
        for (int i = 0; i < total; i++) {
            int idx = (start + i) % LOG_BUFFER_SIZE;
            if (i > 0) json += ",";
            json += "{\"t\":";
            json += String(_millis[idx]);
            json += ",\"m\":\"";
            String escaped = _lines[idx];
            escaped.replace("\\", "\\\\");
            escaped.replace("\"", "\\\"");
            escaped.replace("\n", "\\n");
            escaped.replace("\r", "\\r");
            escaped.replace("\t", "\\t");
            json += escaped;
            json += "\"}";
        }
        json += "]";
        return json;
    }

    void clear() {
        _head = 0;
        _count = 0;
    }

private:
    char _lines[LOG_BUFFER_SIZE][LOG_LINE_MAX];
    unsigned long _millis[LOG_BUFFER_SIZE];
    int _head;
    int _count;
};

extern LogManager logManager;

#endif