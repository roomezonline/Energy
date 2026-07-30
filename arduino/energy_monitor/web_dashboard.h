#ifndef WEB_DASHBOARD_H
#define WEB_DASHBOARD_H

#include <Arduino.h>
#include "energy_data.h"
#include "rtc_manager.h"

class WebDashboard {
public:
    // === CSS ===
    static String buildStyles() {
        String c;
        c += ".tab-bar{display:flex;gap:0;margin-bottom:16px;background:#e8e8ed;border-radius:10px;padding:3px}";
        c += ".tab{flex:1;padding:10px;text-align:center;border:none;border-radius:8px;font-size:13px;font-weight:600;cursor:pointer;background:transparent;color:#8e8e93;transition:all .25s}";
        c += ".tab.act{background:#fff;color:#007aff;box-shadow:0 1px 4px rgba(0,0,0,.08)}";
        c += ".pan{display:none}.pan.act{display:block}";

        // Monitoring section
        c += ".mon-hdr{display:flex;justify-content:space-between;align-items:center;margin-bottom:16px}";
        c += ".mon-hdr h2{font-size:18px;font-weight:700;color:#1c1c1e;margin:0}";
        c += ".mon-hdr .dev{font-size:13px;color:#8e8e93}";
        c += ".mon-grid{display:grid;grid-template-columns:1fr 1fr 1fr;gap:8px}";

        c += ".mcard{border-radius:14px;padding:10px;background:linear-gradient(160deg,#fff,#fafafa);border:1px solid #e8e8ed;box-shadow:0 2px 8px rgba(0,0,0,.04)}";
        c += ".mcard-a{border-top:3px solid #ff4d6d}";
        c += ".mcard-b{border-top:3px solid #f9c74f}";
        c += ".mcard-c{border-top:3px solid #43b581}";
        c += ".mcard-hd{display:flex;align-items:center;gap:6px;margin-bottom:10px}";
        c += ".mcard-hd .dot{width:10px;height:10px;border-radius:50%;display:inline-block}";
        c += ".mcard-hd .dot-a{background:#ff4d6d;box-shadow:0 0 8px rgba(255,77,109,.4)}";
        c += ".mcard-hd .dot-b{background:#f9c74f;box-shadow:0 0 8px rgba(249,199,79,.4)}";
        c += ".mcard-hd .dot-c{background:#43b581;box-shadow:0 0 8px rgba(67,181,129,.4)}";
        c += ".mcard-hd .lbl{font-size:14px;font-weight:700;color:#1c1c1e}";
        c += ".mcard-hd .badge{font-size:10px;padding:2px 8px;border-radius:10px;font-weight:600}";
        c += ".badge-a{background:#ff4d6d15;color:#ff4d6d}";
        c += ".badge-b{background:#f9c74f15;color:#d4a017}";
        c += ".badge-c{background:#43b58115;color:#43b581}";

        c += ".mstat{display:flex;justify-content:space-between;padding:5px 0;font-size:12px;gap:8px}";
        c += ".mstat+.mstat{border-top:1px solid #f0f0f5}";
        c += ".mstat-l{color:#8e8e93;white-space:nowrap}";
        c += ".mstat-r{font-weight:600;color:#1c1c1e;direction:ltr;white-space:nowrap}";
        c += ".mstat-r.unit-v{color:#ff4d6d}";
        c += ".mstat-r.unit-a{color:#f9c74f}";
        c += ".mstat-r.unit-w{color:#43b581}";
        c += ".mstat-r.unit-pf{color:#7b2cbf}";
        c += ".mstat-r.unit-var{color:#ff6b35}";

        // Total bar
        c += ".total-bar{background:#f8f8fc;border-radius:12px;padding:14px 16px;margin-top:12px;display:flex;justify-content:space-between;align-items:center;border:1px solid #e8e8ed}";
        c += ".total-bar .l{font-size:12px;color:#8e8e93}";
        c += ".total-bar .r{font-size:18px;font-weight:700;color:#1c1c1e}";
        c += ".total-bar .r span{font-size:12px;font-weight:400;color:#8e8e93}";

        // Freq bar
        c += ".freq-bar{background:#f8f8fc;border-radius:12px;padding:12px 16px;margin-top:8px;display:flex;justify-content:space-between;align-items:center;border:1px solid #e8e8ed}";
        c += ".freq-bar .l{font-size:12px;color:#8e8e93}";
        c += ".freq-bar .r{font-size:15px;font-weight:600;color:#1c1c1e;direction:ltr}";
c += ".temp-bar{background:#e8f5e9;border-radius:14px;padding:14px 18px;margin:12px 0 16px;display:flex;justify-content:space-between;align-items:center;border:2px solid #c8e6c9;transition:all .3s}";
c += ".temp-bar span{font-size:13px;font-weight:600;color:#2e7d32;transition:color .3s}";
c += ".temp-bar b{font-size:16px;font-weight:700;color:#1b5e20;direction:ltr;transition:color .3s}";
c += ".temp-bar.alert{background:#ffebee;border-color:#ef5350}";
c += ".temp-bar.alert span{color:#c62828}";
c += ".temp-bar.alert b{color:#b71c1c}";

        // Outage bar
        c += ".outage-bar{background:#1a3a3a;border-radius:12px;padding:12px 16px;margin-top:8px;border:1px solid #2ec4b660}";
        c += ".outage-bar>div:first-child{font-size:12px;font-weight:700;color:#2ec4b6;margin-bottom:6px}";
        c += ".outage-body{display:flex;flex-direction:column;gap:4px}";
        c += ".outage-row{display:flex;justify-content:space-between;align-items:center;font-size:12px;color:#bbb}";
        c += ".outage-row b{color:#fff;direction:ltr}";

        // Status bar
        c += ".mon-status{background:#34c759;border-radius:10px;padding:8px 14px;margin-bottom:14px;display:flex;align-items:center;gap:8px;font-size:12px;color:#fff;font-weight:600}";
        c += ".mon-status.off{background:#ff3b30}";
        c += ".mon-status.warn{background:#ff9500}";
        c += ".mon-status .blink{width:6px;height:6px;border-radius:50%;background:#fff;animation:blink 1.2s ease-in-out infinite}";
        c += "@keyframes blink{0%,100%{opacity:1}50%{opacity:.3}}";

        c += ".last-up{font-size:11px;color:#c7c7cc;text-align:center;margin-top:12px}";

        // Energy row
        c += ".mcard .enrg{display:flex;justify-content:space-between;padding:4px 0 0;margin-top:4px;border-top:1px dashed #e8e8ed;font-size:11px;color:#8e8e93;gap:4px;white-space:nowrap}";
        c += ".mcard .enrg b{color:#1c1c1e;direction:ltr}";
        c += ".mcard .enrg .ed{color:#34c759;font-weight:600}";
        c += ".mcard .enrg .em{color:#ff9500;font-weight:600}";
        c += ".mcard .enrg .ey{color:#ff3b30;font-weight:600}";
        c += ".mcard .peak{display:flex;justify-content:space-between;padding:3px 0;font-size:11px;color:#8e8e93;gap:4px}";
        c += ".mcard .peak b{color:#1c1c1e;direction:ltr;font-weight:500}";
        c += ".mcard .peak .ed{color:#5856d6;font-weight:600}";
        c += ".mcard .peak .em{color:#5856d6;font-weight:600}";
        c += ".mcard.cut{border-color:#ff3b30;opacity:.6}";
        c += ".mcard-hd .cut-badge{background:#ff3b30;color:#fff;font-size:10px;padding:2px 8px;border-radius:10px;font-weight:600}";

        // Reset button
        c += ".reset-btn{display:block;width:100%;padding:12px;margin-top:16px;border:2px solid #ff3b30;border-radius:12px;background:#fff;color:#ff3b30;font-size:14px;font-weight:600;cursor:pointer;transition:all .2s}";
        c += ".reset-btn:hover{background:#ff3b30;color:#fff}";
        c += ".reset-btn:active{transform:scale(.98)}";

        // Reset modal
        c += ".ovr{display:none;position:fixed;inset:0;background:rgba(0,0,0,.5);z-index:200;justify-content:center;align-items:center}";
        c += ".ovr.s{display:flex}";
        c += ".sht{background:#fff;border-radius:20px;padding:24px;width:90%;max-width:360px;animation:up .3s ease}";
        c += ".sht .gr{width:32px;height:4px;border-radius:2px;background:#e0e0e0;margin:0 auto 16px}";
        c += ".btn-r{background:#ff3b30;color:#fff;flex:1;padding:14px;border:none;border-radius:12px;font-size:15px;font-weight:600;cursor:pointer}";
        c += ".btn-r:active{opacity:.7}";
        c += ".btn-s{background:#f2f2f7;color:#3a3a3c;flex:1;padding:14px;border:none;border-radius:12px;font-size:15px;font-weight:600;cursor:pointer}";

        // Settings section
        c += ".cfg-hdr{display:flex;justify-content:space-between;align-items:center;margin-bottom:16px}";
        c += ".cfg-hdr h2{font-size:18px;font-weight:700;color:#1c1c1e;margin:0}";
        c += ".cfg-source{font-size:11px;font-weight:600;padding:4px 10px;border-radius:20px}";
        c += ".cfg-src-server{background:#e3f2fd;color:#1565c0}";
        c += ".cfg-src-local{background:#fff3e0;color:#e65100}";
        c += ".cfg-status{background:#f8f8fc;border-radius:10px;padding:10px 14px;margin-bottom:16px;display:flex;align-items:center;gap:10px;border:1px solid #e8e8ed;font-size:13px;font-weight:600;color:#555}";

        c += ".tgl-rw{display:flex;justify-content:space-between;align-items:center;padding:12px 14px;background:#f8f8fc;border-radius:10px;margin-bottom:16px;border:1px solid #e8e8ed}";
        c += ".tgl-lbl{font-size:14px;font-weight:600;color:#1c1c1e}";
        c += ".tgl-dsc{font-size:11px;color:#8e8e93;margin-top:2px}";
        c += ".tgl{position:relative;width:48px;height:28px;flex-shrink:0}";
        c += ".tgl input{opacity:0;width:0;height:0}";
        c += ".tgl .sl{position:absolute;cursor:pointer;inset:0;background:#c7c7cc;border-radius:14px;transition:.3s}";
        c += ".tgl .sl::before{content:'';position:absolute;width:22px;height:22px;left:3px;bottom:3px;background:#fff;border-radius:50%;transition:.3s;box-shadow:0 1px 3px rgba(0,0,0,.15)}";
        c += ".tgl input:checked+.sl{background:#007aff}";
        c += ".tgl input:checked+.sl::before{transform:translateX(20px)}";

        c += ".cfg-grp{background:#fff;border-radius:12px;padding:14px 16px;margin-bottom:12px;border:1px solid #e8e8ed}";
        c += ".cfg-grp-hd{font-size:12px;font-weight:600;color:#8e8e93;margin-bottom:12px;letter-spacing:.3px}";
        c += ".cfg-fld{display:flex;justify-content:space-between;align-items:center;padding:6px 0}";
        c += ".cfg-fld+.cfg-fld{border-top:1px solid #f2f2f7}";
        c += ".cfg-fld label{font-size:13px;color:#555;flex:1}";
        c += ".cfg-fld .val{font-size:14px;font-weight:600;color:#1c1c1e;direction:ltr;min-width:60px;text-align:right}";
        c += ".cfg-fld input{width:80px;padding:6px 8px;border:1px solid #ddd;border-radius:8px;font-size:13px;font-weight:600;text-align:center;direction:ltr;outline:none;background:#fafafa;color:#1c1c1e}";
        c += ".cfg-fld input:focus{border-color:#007aff;background:#fff}";
        c += ".cfg-fld input:disabled{background:#f2f2f7;color:#aaa;border-color:#e8e8ed}";
        c += ".cfg-fld select{width:96px;padding:6px 8px;border:1px solid #ddd;border-radius:8px;font-size:13px;font-weight:600;text-align:center;direction:ltr;outline:none;background:#fafafa;color:#1c1c1e;cursor:pointer}";
        c += ".cfg-fld select:focus{border-color:#007aff;background:#fff}";
        c += ".cfg-fld .unit{font-size:11px;color:#8e8e93;min-width:20px;text-align:left}";

        c += ".cfg-save{width:100%;padding:12px;border:none;border-radius:10px;background:#007aff;color:#fff;font-size:14px;font-weight:700;cursor:pointer;margin-top:8px;display:none}";
        c += ".cfg-save.show{display:block}";
        c += ".cfg-save:active{opacity:.7}";
        c += ".cfg-save:disabled{background:#c7c7cc;cursor:default}";
        c += ".cfg-save-always{width:100%;padding:12px;border:none;border-radius:10px;background:#34c759;color:#fff;font-size:14px;font-weight:700;cursor:pointer;margin-top:8px;display:block}";
        c += ".cfg-save-always:active{opacity:.7}";
        c += ".cfg-save-always:disabled{background:#c7c7cc;cursor:default}";

        c += ".cfg-toast{padding:8px 12px;border-radius:8px;margin-top:10px;text-align:center;font-size:12px;font-weight:600;display:none}";
        c += ".cfg-toast.show{display:block}";
        c += ".cfg-toast.ok{background:#e8f5e9;color:#2e7d32}";
        c += ".cfg-toast.er{background:#ffebee;color:#c62828}";

        // Alarm section
        c += ".alarm-section{background:#f8f8fc;border-radius:12px;margin-top:12px;border:1px solid #e8e8ed;overflow:hidden}";
        c += ".alarm-hdr{display:flex;justify-content:space-between;align-items:center;padding:10px 14px;cursor:pointer;user-select:none}";
        c += ".alarm-hdr .l{display:flex;align-items:center;gap:8px}";
        c += ".alarm-hdr .l span{font-size:12px;font-weight:600;color:#1c1c1e}";
        c += ".alarm-hdr .badge{font-size:10px;padding:2px 8px;border-radius:10px;background:#ff3b30;color:#fff;font-weight:600;min-width:18px;text-align:center}";
        c += ".alarm-hdr .badge.zero{background:#c7c7cc}";
        c += ".alarm-hdr .arr{font-size:10px;color:#8e8e93;transition:transform .25s}";
        c += ".alarm-hdr .arr.open{transform:rotate(180deg)}";
        c += ".alarm-body{padding:0 14px 10px;display:none;max-height:240px;overflow-y:auto}";
        c += ".alarm-body.open{display:block}";
        c += ".alarm-item{display:flex;align-items:flex-start;gap:8px;padding:8px 0;border-bottom:1px solid #f0f0f5}";
        c += ".alarm-item:last-child{border-bottom:none}";
        c += ".alarm-item .dot{width:8px;height:8px;border-radius:50%;margin-top:4px;flex-shrink:0}";
        c += ".alarm-item .dot.critical{background:#ff3b30;box-shadow:0 0 6px rgba(255,59,48,.4)}";
        c += ".alarm-item .dot.warning{background:#ff9500;box-shadow:0 0 6px rgba(255,149,0,.4)}";
        c += ".alarm-item .dot.resolved{background:#34c759}";
        c += ".alarm-item .dot.info{background:#5ac8fa;box-shadow:0 0 6px rgba(90,200,250,.4)}";
        c += ".alarm-item .content{flex:1;min-width:0}";
        c += ".alarm-item .title{font-size:12px;font-weight:600;color:#1c1c1e}";
        c += ".alarm-item .msg{font-size:11px;color:#8e8e93;margin-top:2px;word-break:break-word}";
        c += ".alarm-item .ts{font-size:10px;color:#c7c7cc;margin-top:2px}";
        c += ".alarm-item .tag{font-size:9px;padding:1px 6px;border-radius:6px;font-weight:600;margin-left:6px;flex-shrink:0}";
        c += ".alarm-item .tag.critical-tag{background:#ff3b3015;color:#ff3b30}";
        c += ".alarm-item .tag.resolved-tag{background:#34c75915;color:#34c759}";
        c += ".alarm-item .tag.info-tag{background:#5ac8fa15;color:#5ac8fa}";
        c += ".alarm-empty{padding:16px;text-align:center;font-size:12px;color:#c7c7cc}";
        c += ".alarm-clear{width:100%;padding:8px;border:none;border-radius:8px;background:#ff3b30;color:#fff;font-size:12px;font-weight:600;cursor:pointer;margin-top:6px}";
        c += ".alarm-clear:active{opacity:.7}";

        // Desktop: wider cards, larger fonts, more padding
        c += "@media(min-width:768px){";
        c += ".mon-grid{gap:16px;grid-template-columns:1fr 1fr 1fr}";
        c += ".mcard{padding:20px}";
        c += ".mcard-hd{gap:10px;margin-bottom:14px}";
        c += ".mcard-hd .lbl{font-size:16px}";
        c += ".mstat{font-size:14px;padding:8px 0}";
        c += ".mstat-l{font-size:13px}";
        c += ".mcard .enrg{font-size:13px}";
        c += ".mcard .peak{font-size:12px}";
        c += ".total-bar{padding:16px 24px}";
        c += ".total-bar .r{font-size:24px}";
        c += ".total-bar .l{font-size:13px}";
        c += ".freq-bar{padding:14px 24px}";
        c += ".alarm-section{margin-top:16px}";
        c += "}";

        return c;
    }

    // === HTML ===
    static String buildHtml() {
        String h;
        h += "\n<div id=monPanel>";
        h += "\n<div class=mon-hdr><h2>⚡ Live Monitor</h2><div class=dev id=devID></div></div>";
        h += "\n<div class=mon-status id=monStatus><span class=blink></span> <span id=monStatusText>Waiting for data...</span> <span id=timeValidBadge style=display:none></span></div>";
        h += "\n<div class=temp-bar><span>🌡 Ambient Temp</span> <b id=ambTemp>--</b></div>";

        h += "\n<div class=mon-grid>";
        h += "\n  <div class='mcard mcard-a' id=cardA>";
        h += "\n    <div class=mcard-hd><span class='dot dot-a'></span><span class=lbl>Phase A</span></div>";
        h += "\n    <div class=mstat><span class=mstat-l>Voltage</span><span class='mstat-r unit-v' id=vA>---</span></div>";
        h += "\n    <div class=mstat><span class=mstat-l>Current</span><span class='mstat-r unit-a' id=cA>---</span></div>";
        h += "\n    <div class=mstat><span class=mstat-l>Power</span><span class='mstat-r unit-w' id=pA>---</span></div>";
        h += "\n    <div class=mstat><span class=mstat-l>Reactive</span><span class='mstat-r unit-var' id=qA>---</span></div>";
        h += "\n    <div class=mstat><span class=mstat-l>PF</span><span class='mstat-r unit-pf' id=pfA>---</span></div>";
        h += "\n    <div class=enrg><span class=ed>Today</span> <b id=edA>---</b></div>";
        h += "\n    <div class=enrg><span class=em>Month</span> <b id=emA>---</b></div>";
        h += "\n    <div class=enrg><span class=ey>Year</span> <b id=eyA>---</b></div>";
        h += "\n    <div class=peak><span class=ed>Peak Amp</span> <b id=pkA>---</b></div>";
        h += "\n    <div class=peak><span class=em>Peak PW</span> <b id=pwA>---</b></div>";
        h += "\n  </div>";
        h += "\n";
        h += "\n  <div class='mcard mcard-b' id=cardB>";
        h += "\n    <div class=mcard-hd><span class='dot dot-b'></span><span class=lbl>Phase B</span></div>";
        h += "\n    <div class=mstat><span class=mstat-l>Voltage</span><span class='mstat-r unit-v' id=vB>---</span></div>";
        h += "\n    <div class=mstat><span class=mstat-l>Current</span><span class='mstat-r unit-a' id=cB>---</span></div>";
        h += "\n    <div class=mstat><span class=mstat-l>Power</span><span class='mstat-r unit-w' id=pB>---</span></div>";
        h += "\n    <div class=mstat><span class=mstat-l>Reactive</span><span class='mstat-r unit-var' id=qB>---</span></div>";
        h += "\n    <div class=mstat><span class=mstat-l>PF</span><span class='mstat-r unit-pf' id=pfB>---</span></div>";
        h += "\n    <div class=enrg><span class=ed>Today</span> <b id=edB>---</b></div>";
        h += "\n    <div class=enrg><span class=em>Month</span> <b id=emB>---</b></div>";
        h += "\n    <div class=enrg><span class=ey>Year</span> <b id=eyB>---</b></div>";
        h += "\n    <div class=peak><span class=ed>Peak Amp</span> <b id=pkB>---</b></div>";
        h += "\n    <div class=peak><span class=em>Peak PW</span> <b id=pwB>---</b></div>";
        h += "\n  </div>";
        h += "\n";
        h += "\n  <div class='mcard mcard-c' id=cardC>";
        h += "\n    <div class=mcard-hd><span class='dot dot-c'></span><span class=lbl>Phase C</span></div>";
        h += "\n    <div class=mstat><span class=mstat-l>Voltage</span><span class='mstat-r unit-v' id=vC>---</span></div>";
        h += "\n    <div class=mstat><span class=mstat-l>Current</span><span class='mstat-r unit-a' id=cC>---</span></div>";
        h += "\n    <div class=mstat><span class=mstat-l>Power</span><span class='mstat-r unit-w' id=pC>---</span></div>";
        h += "\n    <div class=mstat><span class=mstat-l>Reactive</span><span class='mstat-r unit-var' id=qC>---</span></div>";
        h += "\n    <div class=mstat><span class=mstat-l>PF</span><span class='mstat-r unit-pf' id=pfC>---</span></div>";
        h += "\n    <div class=enrg><span class=ed>Today</span> <b id=edC>---</b></div>";
        h += "\n    <div class=enrg><span class=em>Month</span> <b id=emC>---</b></div>";
        h += "\n    <div class=enrg><span class=ey>Year</span> <b id=eyC>---</b></div>";
        h += "\n    <div class=peak><span class=ed>Peak Amp</span> <b id=pkC>---</b></div>";
        h += "\n    <div class=peak><span class=em>Peak PW</span> <b id=pwC>---</b></div>";
        h += "\n  </div>";
        h += "\n</div>";

        h += "\n<div class=total-bar><div class=l>Total Power</div><div class=r id=totP>---</div></div>";
        h += "\n<div class=freq-bar><div class=l>Frequency</div><div class=r id=monFreq>---</div></div>";

        // Outage info bar
        h += "\n<div class=outage-bar id=outageBar style='display:none'>";
        h += "\n<div><span>⚡ Outage Buffer</span></div>";
        h += "\n<div class=outage-body>";
        h += "\n<div class=outage-row><span>Total Energy</span><b id=outageTot>0</b></div>";
        h += "\n<div class=outage-row><span>Started</span><span id=outageStart>---</span></div>";
        h += "\n<div class=outage-row id=outagePeaks><span>Peak Current</span><span id=outagePeak>A:0 B:0 C:0</span></div>";
        h += "\n</div>";
        h += "\n</div>";

        // Alarm section (collapsible)
        h += "\n<div class=alarm-section id=alarmSec>";
        h += "\n<div class=alarm-hdr onclick=toggleAlarms()>";
        h += "\n<div class=l><span>🔔 Alarms</span><span class='badge zero' id=alarmBadge>0</span></div>";
        h += "\n<span class=arr id=alarmArrow>&#x25BC;</span>";
        h += "\n</div>";
        h += "\n<div class=alarm-body id=alarmBody>";
        h += "\n<div class=alarm-empty id=alarmEmpty>No alarms</div>";
        h += "\n<div id=alarmList></div>";
        h += "\n<button class=alarm-clear id=alarmClearBtn onclick=clearAlarms() style='display:none'>✓ Clear All Alarms</button>";
        h += "\n</div>";
        h += "\n</div>";
        h += "\n<div class=last-up id=monLast>Waiting...</div>";
        h += "\n</div>";
        return h;
    }

    // === JSON serializer ===
    static String dataToJson(const EnergyData& d, bool serverReachable = false, const String& alarmsJson = "", int phaseCount = 3) {
        String j = "{";
        j += "\n\"t\":\"" + d.timestamp + "\"";
        String escape = d.deviceId;
        escape.replace("\"", "\\\"");
        j += "\n,\"d\":\"" + escape + "\"";
        j += "\n,\"f\":" + String(d.frequency, 2);
        j += "\n,\"tmp\":" + String(d.temperature, 1);
        j += "\n,\"pc\":" + String(phaseCount);

        auto addPhase = [&](const String& n, const PhaseData& p) {
            float s = p.voltage * p.current;
            float reactive = sqrt(max(0.0f, s * s - p.power * p.power));
            j += "\n,\"" + n + "\":[";
            j += String(p.voltage, 2) + ",";
            j += String(p.current, 2) + ",";
            j += String(p.power, 2) + ",";
            j += String(p.pf, 2) + ",";
            j += String(p.energy, 2) + ",";
            j += String(p.connected ? 1 : 0) + ",";
            j += String(reactive, 2);
            j += "\n]";
        };
        addPhase("a", d.phaseA);
        addPhase("b", d.phaseB);
        addPhase("c", d.phaseC);

        j += "\n,\"ptp\":" + String(d.phaseA.power + d.phaseB.power + d.phaseC.power, 2);
        j += "\n,\"sr\":" + String(serverReachable ? "true" : "false");
        // Persian time: server time preferred, RTC fallback
        String pt = g_persianTime;
        if (pt.length() == 0) {
            pt = rtcManager.getLocalDisplayString();
        }
        if (pt.length() > 0) {
            String escPt = pt;
            escPt.replace("\"", "\\\"");
            j += "\n,\"pt\":\"" + escPt + "\"";
        }
        if (alarmsJson.length() > 0) {
            j += "\n,\"al\":" + alarmsJson;
        }
        j += "\n}";
        return j;
    }

    // === JS ===
    static String buildJs() {
        String j;
        j += "\n\nvar _monInt=null;var _monTimer=null;var _alarmOpen=false;var _persianTime='';var _lastToggle=0;";

        // Jalali (Shamsi) date conversion
        j += "\n\nfunction g2j(gy,gm,gd){var g_d_m=[0,31,59,90,120,151,181,212,243,273,304,334];var jy=(gy<=1600)?0:979;gy-=(gy<=1600)?621:1600;var gy2=(gm>2)?(gy+1):gy;var days=(365*gy)+Math.floor((gy2+3)/4)-Math.floor((gy2+99)/100)+Math.floor((gy2+399)/400)-80+gd+g_d_m[gm-1];jy+=33*Math.floor(days/12053);days%=12053;jy+=4*Math.floor(days/1461);days%=1461;jy+=Math.floor((days-1)/365);if(days>365)days=(days-1)%365;var jm=(days<186)?1+Math.floor(days/31):7+Math.floor((days-186)/30);var jd=1+((days<186)?(days%31):((days-186)%30));return[jy,jm,jd]}";

        j += "\n\nfunction toJalali(ts){";
        j += "\n\nif(!ts)return'';";
        j += "\n\nif(ts.indexOf('boot')===0){var bp=ts.indexOf(' ');return bp>0?ts.substring(bp+1).replace('Z',''):ts}";
        j += "\n\ntry{var d=new Date(ts);if(isNaN(d.getTime()))return ts;";
        j += "\n\nvar j=g2j(d.getFullYear(),d.getMonth()+1,d.getDate());";
        j += "\n\nvar sh=['0','1','2','3','4','5','6','7','8','9'];";
        j += "\n\nfunction num2farsi(n){var s=String(n);var r='';for(var i=0;i<s.length;i++){r+=sh[parseInt(s[i])]}return r}";
        j += "\n\nvar h=String(d.getHours()).padStart(2,'0');var m=String(d.getMinutes()).padStart(2,'0');";
        j += "\n\nreturn num2farsi(j[0])+'/'+num2farsi(j[1])+'/'+num2farsi(j[2])+' '+num2farsi(h)+':'+num2farsi(m)}";
        j += "\n\ncatch(e){return ts}";
        j += "\n\n}";

        // Reset modal functions
        j += "\n\nfunction showResetModal(){$i('resetModal').className='ovr s'}";
        j += "\n\nfunction hideResetModal(){$i('resetModal').className='ovr'}";
        j += "\n\nfunction doReset(){";
        j += "\n\nvar btn=$i('resetConfirmBtn');btn.disabled=true;btn.textContent='Resetting...';";
        j += "\n\nvar x=new XMLHttpRequest();";
        j += "\n\nx.open('POST','/api/reset');";
        j += "\n\nx.onload=function(){";
        j += "\n\nif(x.status==200){";
        j += "\n\nhideResetModal();";
        j += "\n\nloadEnergy();loadMon();";
        j += "\n\n}else{";
        j += "\n\nbtn.disabled=false;btn.textContent='Reset Everything';";
        j += "\n\n}};x.send()}";

        j += "\n\nfunction startMon(){if(_monInt)return;loadMon();loadEnergy();_monInt=setInterval(loadMon,3000);setInterval(loadEnergy,5000)}";
        j += "\n\nfunction stopMon(){if(_monInt){clearInterval(_monInt);_monInt=null}if(_monTimer){clearTimeout(_monTimer);_monTimer=null}}";

        j += "\n\nfunction loadEnergy(){";
        j += "\n\nvar x=new XMLHttpRequest();";
        j += "\n\nx.open('GET','/api/energy');";
        j += "\n\nx.onload=function(){if(x.status!=200)return;";
        j += "\n\ntry{var d=JSON.parse(x.responseText)}catch(e){return}";
        j += "\n\nif(!d)return;";
        j += "\n\n['a','b','c'].forEach(function(p){";
        j += "\n\nif(!d[p])return;";
        j += "\n\n$i('ed'+p.toUpperCase()).textContent=smartE(d[p].d);";
        j += "\n\n$i('em'+p.toUpperCase()).textContent=smartE(d[p].m);";
        j += "\n\n$i('ey'+p.toUpperCase()).textContent=smartE(d[p].y);";
        j += "\n\n$i('pk'+p.toUpperCase()).textContent=smartA(d[p].pkA);";
        j += "\n\n$i('pw'+p.toUpperCase()).textContent=smartW(d[p].pkW);";
        j += "\n\n})};x.send()}";

        j += "\n\nfunction toggleAlarms(){";
        j += "\n\n_alarmOpen=!_alarmOpen;";
        j += "\n\n$i('alarmBody').className='alarm-body'+(_alarmOpen?' open':'');";
        j += "\n\n$i('alarmArrow').className='arr'+( _alarmOpen?' open':'');";
        j += "\n\n}";

        j += "\n\nfunction renderAlarms(al){";
        j += "\n\nvar list=$i('alarmList');var empty=$i('alarmEmpty');var badge=$i('alarmBadge');var clearBtn=$i('alarmClearBtn');";
        j += "\n\nif(!al||!al.length){list.innerHTML='';empty.style.display='block';badge.textContent='0';badge.className='badge zero';clearBtn.style.display='none';return}";
        j += "\n\nempty.style.display='none';";
        j += "\n\nvar active=0;var html='';";
        j += "\n\nfor(var i=0;i<al.length;i++){";
        j += "\n\nvar a=al[i];var r=a.r===true;";
        j += "\n\nif(!r)active++;";
        j += "\n\nvar dotClass=r?'dot resolved':(a.s=='Critical'?'dot critical':(a.s=='Info'?'dot info':'dot warning'));";
        j += "\n\nvar tag=r?'<span class=\"tag resolved-tag\">Resolved</span>':(a.s=='Critical'?'<span class=\"tag critical-tag\">'+a.s+'</span>':(a.s=='Info'?'<span class=\"tag info-tag\">Info</span>':'<span class=\"tag critical-tag\" style=background:#ff950015;color:#ff9500>'+a.s+'</span>'));";
        j += "\n\nvar tsTxt=toJalali(a.ts);if(r&&a.ra)tsTxt+=' → '+toJalali(a.ra);";
        j += "\n\nhtml+='<div class=alarm-item><span class=\"dot '+dotClass+'\"></span><div class=content><div class=title>'+esc(a.t)+' '+(a.p?'['+a.p+']':'')+'</div>'+(a.m?'<div class=msg>'+esc(a.m)+'</div>':'')+'<div class=ts>'+tsTxt+'</div></div>'+tag+'</div>';";
        j += "\n\n}";
        j += "\n\nlist.innerHTML=html;";
        j += "\n\nbadge.textContent=active;";
        j += "\n\nbadge.className='badge'+(active===0?' zero':'');";
        j += "\n\nclearBtn.style.display=active>0?'block':'none'";
        j += "\n\n}";

        j += "\n\nfunction clearAlarms(){";
        j += "\n\nvar x=new XMLHttpRequest();";
        j += "\n\nx.open('POST','/api/alarms/clear');";
        j += "\n\nx.onload=function(){";
        j += "\n\nif(x.status==200){";
        j += "\n\nrenderAlarms([]);";
        j += "\n\n$i('alarmClearBtn').style.display='none';";
        j += "\n\n}";
        j += "\n\n};x.send()";
        j += "\n\n}";

        j += "\n\nfunction smartW(w){if(w>=1000000)return (w/1000000).toFixed(2)+' MW';if(w>=1000)return (w/1000).toFixed(1)+' kW';return w.toFixed(0)+' W'}";
        j += "\n\nfunction smartA(a){if(a>=1000)return (a/1000).toFixed(2)+' kA';return a.toFixed(2)+' A'}";
        j += "\n\nfunction smartE(e){if(e>=1000)return (e/1000).toFixed(3)+' MWh';if(e>=1)return e.toFixed(3)+' kWh';return (e*1000).toFixed(1)+' Wh'}";
        j += "\n\nfunction smartVAr(v){if(v>=1000000)return (v/1000000).toFixed(2)+' MVAr';if(v>=1000)return (v/1000).toFixed(1)+' kVAr';return v.toFixed(0)+' VAr'}";

        j += "\n\nfunction loadMon(){";
        j += "\n\nvar x=new XMLHttpRequest();";
        j += "\n\nx.open('GET','/api/live');";
        j += "\n\nx.onload=function(){try{";
        j += "\n\nvar s=$i('monStatus');var st=$i('monStatusText');";
        j += "\n\nif(x.status!=200){";
        j += "\n\ns.className='mon-status warn';st.textContent='⚠ Server busy';";
        j += "\n\nreturn}";
        j += "\n\ntry{var d=JSON.parse(x.responseText)}catch(e){";
        j += "\n\ns.className='mon-status warn';st.textContent='⚠ Parse error';return}";

        // Check if we have phase data (any phase with voltage or connected)
        j += "\n\nvar hasData = d && d.a && (d.a[0]>0 || d.a[5]===1 || d.b[0]>0 || d.b[5]===1 || d.c[0]>0 || d.c[5]===1);";
        j += "\n\nif(!hasData){";
        j += "\n\ns.className='mon-status off';st.textContent='No data yet';return";
        j += "\n\n}";

        // Update each phase independently
        j += "\n\nvar phs=['a','b','c'];var cols=['A','B','C'];var names=['A','B','C'];";
        j += "\n\nfor(var i=0;i<3;i++){var p=phs[i];var c=cols[i];var n=names[i];";
        j += "\n\nif(!d[p]||d[p].length<6)continue;";
        j += "\n\nvar connected=d[p][5]===undefined||d[p][5]==1;";
        j += "\n\nvar card=document.querySelector('.mcard-'+p);";
        j += "\n\nif(!connected){";
        j += "\n\ncard.className=(card.className||'mcard mcard-'+p).replace(/ cut/g,'')+' cut';";
        j += "\n\n$i('v'+c).textContent='---';$i('c'+c).textContent='---';";
        j += "\n\n$i('p'+c).textContent='---';$i('q'+c).textContent='---';$i('pf'+c).textContent='---';$i('ed'+c).textContent='---';$i('em'+c).textContent='---';$i('ey'+c).textContent='---';";
        j += "\n\nvar hd=card.querySelector('.mcard-hd');";
        j += "\n\nhd.innerHTML='<span class=\"dot dot-'+p+'\"></span><span class=lbl style=color:#ff3b30>disconnect</span><span class=cut-badge>⛔</span>';";
        j += "\n\n}else{";
        j += "\n\ncard.className=((card.className||'mcard mcard-'+p).replace(/ cut/g,''));";
        j += "\n\n$i('v'+c).textContent=d[p][0].toFixed(0)+' V';";
        j += "\n\n$i('c'+c).textContent=smartA(d[p][1]);";
        j += "\n\n$i('p'+c).textContent=smartW(d[p][2]);";
        j += "\n\n$i('q'+c).textContent=smartVAr(d[p][6]);";
        j += "\n\n$i('pf'+c).textContent=d[p][3].toFixed(2);";
        j += "\n\nvar hd2=card.querySelector('.mcard-hd');";
        j += "\n\nhd2.innerHTML='<span class=\"dot dot-'+p+'\"></span><span class=lbl>'+n+'</span>';";
        j += "\n\n}";
        j += "\n\n}";

        // Show/hide phase cards based on phaseCount
        j += "\n\nvar pc=d.pc||3;";
        j += "\n\nvar cards=['cardA','cardB','cardC'];";
        j += "\n\nfor(var ci=0;ci<3;ci++){";
        j += "\n\nvar el=$i(cards[ci]);if(el)el.style.display=ci<pc?'':'none'}";

        j += "\n\n$i('totP').innerHTML=smartW(d.ptp);";
        j += "\n\n$i('monFreq').textContent=d.f.toFixed(2)+' Hz';";
        j += "\n\nvar ambEl=$i('ambTemp');var tbEl=ambEl&&ambEl.parentElement;";
        j += "\n\nif(typeof d.tmp!=='undefined'&&d.tmp){";
        j += "\n\nambEl.textContent=d.tmp.toFixed(1)+' °C';";
        j += "\n\nif(tbEl&&typeof d.tt!=='undefined')tbEl.className='temp-bar'+(d.tmp>d.tt?' alert':'')";
        j += "\n\n}else{";
        j += "\n\nambEl.textContent='--';if(tbEl)tbEl.className='temp-bar'";
        j += "\n\n}";

        j += "\n\nif(d.pt){";
        j += "\n\n$i('monLast').textContent=d.pt;";
        j += "\n\n_persianTime=d.pt";
        j += "\n\n}else{";
        j += "\n\nvar now=new Date();";
        j += "\n\nvar h=String(now.getHours()).padStart(2,'0');";
        j += "\n\nvar m=String(now.getMinutes()).padStart(2,'0');";
        j += "\n\nvar s2=String(now.getSeconds()).padStart(2,'0');";
        j += "\n\nvar jd=g2j(now.getFullYear(),now.getMonth()+1,now.getDate());";
        j += "\n\nvar farsiNum=function(n){var sh=['0','1','2','3','4','5','6','7','8','9'];return String(n).split('').map(function(c){return sh[parseInt(c)]||c}).join('')};";
        j += "\n\n$i('monLast').textContent=farsiNum(jd[0])+'/'+farsiNum(jd[1])+'/'+farsiNum(jd[2])+' '+farsiNum(h)+':'+farsiNum(m)+':'+farsiNum(s2)}";

        j += "\n\nif(d.d)$i('devID').textContent=d.d;";

        // Time validity
        j += "\n\nvar tv=$i('timeValidBadge');";
        j += "\n\nif(d.tv===false){tv.style.display='';tv.textContent='⚠ Time not synced';tv.style.color='#ff9500'}";
        j += "\n\nelse{tv.style.display='none'}";

        // Server reachability status
        j += "\n\nif(d.sr){";
        j += "\n\ns.className='mon-status';st.textContent='✅ Server Online';";
        j += "\n\n}else{";
        j += "\n\ns.className='mon-status off';st.textContent='❌ CONNECTION ERROR — local data only';";
        j += "\n\n}";

        // Update alarms
        j += "\n\nif(d.al){renderAlarms(d.al)}";

        // Fetch outage status
        j += "\n\nfetchOutage()";

        j += "\n\n}catch(e){console.error('Mon x.onload err',e,this)}};";
        j += "\n\nx.onerror=function(){";
        j += "\n\nvar s=$i('monStatus');var st=$i('monStatusText');";
        j += "\n\ns.className='mon-status off';";
        j += "\n\nst.textContent='❌ CONNECTION ERROR';";
        j += "\n\n};x.send()}";

        // Fetch outage status
        j += "\n\nfunction fetchOutage(){";
        j += "\n\nvar x=new XMLHttpRequest();";
        j += "\n\nx.open('GET','/api/outage');";
        j += "\n\nx.onload=function(){";
        j += "\n\ntry{var o=JSON.parse(x.responseText)}catch(e){return};";
        j += "\n\nvar bar=$i('outageBar');";
        j += "\n\nif(!o.pending){bar.style.display='none';return};";
        j += "\n\nbar.style.display='';";
        j += "\n\n$i('outageTot').textContent=smartE(o.tot);";
        j += "\n\n$i('outageStart').textContent=o.epoch?toJalali(new Date(o.epoch*1000).toISOString()):'---';";
        j += "\n\n$i('outagePeak').textContent='A:'+smartA(o.pA)+' B:'+smartA(o.pB)+' C:'+smartA(o.pC);";
        j += "\n\n};x.send()";
        j += "\n\n}";

        return j;
    }

    // === Settings HTML ===
    static String buildSettingsHtml() {
        String h;
        h += "\n<div id=cfgPanel>";

        h += "\n<div class=cfg-hdr><h2>⚙ Settings</h2><span class='cfg-source cfg-src-server' id=cfgSrc>Server</span></div>";
        h += "\n<div class=cfg-status id=cfgStatus>✓ Config from server</div>";

        // Local mode toggle
        h += "\n<div class=tgl-rw>";
        h += "\n<div><div class=tgl-lbl>Local Mode</div><div class=tgl-dsc>Manual settings on device</div></div>";
        h += "\n<label class=tgl><input type=checkbox id=localModeChk onchange=onLocalModeToggle()><span class=sl></span></label>";
        h += "\n</div>";

        // Publish interval
        h += "\n<div class=cfg-grp>";
        h += "\n<div class=cfg-grp-hd>🔌 Connection</div>";
        h += "\n<div class=cfg-fld><label>Publish interval (ms)</label><input type=number id=cfgPublishMs class=cfgIn min=1000 max=60000 step=1000 disabled><span class=unit>ms</span></div>";
        h += "\n<div class=cfg-fld><label>Phase count</label><select id=cfgPhaseCount class=cfgIn disabled><option value=1>1-Phase</option><option value=3 selected>3-Phase</option></select><span class=unit></span></div>";
        h += "\n</div>";

        // Voltage
        h += "\n<div class=cfg-grp>";
        h += "\n<div class=cfg-grp-hd>⚡ Voltage</div>";
        h += "\n<div class=cfg-fld><label>Max voltage</label><input type=number id=cfgOverV class=cfgIn min=220 max=300 step=0.5 disabled><span class=unit>V</span></div>";
        h += "\n<div class=cfg-fld><label>Min voltage</label><input type=number id=cfgUnderV class=cfgIn min=180 max=230 step=0.5 disabled><span class=unit>V</span></div>";
        h += "\n<div class=cfg-fld><label>Phase imbalance</label><input type=number id=cfgImbalance class=cfgIn min=1 max=50 step=1 disabled><span class=unit>V</span></div>";
        h += "\n</div>";

        // Current & Power
        h += "\n<div class=cfg-grp>";
        h += "\n<div class=cfg-grp-hd>💡 Current & Power</div>";
        h += "\n<div class=cfg-fld><label>Max current</label><input type=number id=cfgOverI class=cfgIn min=5 max=100 step=0.5 disabled><span class=unit>A</span></div>";
        h += "\n<div class=cfg-fld><label>Max power per phase</label><input type=number id=cfgHighP class=cfgIn min=1000 max=50000 step=100 disabled><span class=unit>W</span></div>";
        h += "\n</div>";

        // Temperature
        h += "\n<div class=cfg-grp>";
        h += "\n<div class=cfg-grp-hd>🌡️ Temperature</div>";
        h += "\n<div class=cfg-fld><label>Overheat threshold</label><input type=number id=cfgTempTh class=cfgIn min=10 max=100 step=0.5 disabled><span class=unit>°C</span></div>";
        h += "\n</div>";

        // PF & Freq
        h += "\n<div class=cfg-grp>";
        h += "\n<div class=cfg-grp-hd>📊 PF & Frequency</div>";
        h += "\n<div class=cfg-fld><label>Min PF</label><input type=number id=cfgLowPF class=cfgIn min=0.5 max=0.99 step=0.01 disabled><span class=unit></span></div>";
        h += "\n<div class=cfg-fld><label>Min frequency</label><input type=number id=cfgFMin class=cfgIn min=47 max=50 step=0.1 disabled><span class=unit>Hz</span></div>";
        h += "\n<div class=cfg-fld><label>Max frequency</label><input type=number id=cfgFMax class=cfgIn min=50 max=53 step=0.1 disabled><span class=unit>Hz</span></div>";
        h += "\n</div>";

        // Calibration (single set for all phases)
        h += "\n<div class=cfg-grp>";
        h += "\n<div class=cfg-grp-hd>🔧 Calibration</div>";
        h += "\n<div class=cfg-fld><label>نوع سنسور</label><select id=sensorType class=cfgIn onchange=onSensorTypeChange()><option value=0>کلمپی (Clamp)</option><option value=1>حلقه‌ای (Ring)</option></select></div>";
        h += "\n<div class=cfg-fld><label>Enable</label><div class=tgl><input type=checkbox id=calEnabled checked onchange=onCalToggle()><span class=sl></span></div></div>";
        h += "\n<div class=cfg-fld><label>Current</label><input type=number id=calCur min=0.1 max=10 step=0.01><span class=unit></span></div>";
        h += "\n<div class=cfg-fld><label>Power</label><input type=number id=calPwr min=0.1 max=10 step=0.01><span class=unit></span></div>";
        h += "\n<div class=cfg-fld><label>PF</label><input type=number id=calPf min=0.1 max=5 step=0.01><span class=unit></span></div>";
        h += "\n<div class=cfg-fld><label>Energy</label><input type=number id=calEnr min=0.1 max=10 step=0.01><span class=unit></span></div>";
        h += "\n<div class=cfg-fld><label>Offset</label><input type=number id=calOff min=-1 max=1 step=0.01><span class=unit>A</span></div>";
        h += "\n<button class=cfg-save-always id=calSave onclick=saveCalCfg()>💾 Save Calibration</button>";
        h += "\n<div class=cfg-toast id=calToast></div>";
        h += "\n</div>";

        // Save button (thresholds only)
        h += "\n<button class=cfg-save id=cfgSave onclick=saveLocalCfg()>💾 Save Local Settings</button>";
        h += "\n<div class=cfg-toast id=cfgToast></div>";
        h += "\n</div>";

        // Firmware Update
        h += "\n<div class=cfg-grp>";
        h += "\n<div class=cfg-grp-hd>📲 Firmware</div>";
        h += "\n<div class=cfg-fld><label>Current version</label><span id=otaVersion style='font-weight:600'>---</span></div>";
        h += "\n<div id=otaStatus class=cfg-status style='margin-bottom:8px'></div>";
        h += "\n<button class=cfg-save-always id=otaCheckBtn onclick=triggerOtaCheck()>⟳ Check for Updates</button>";
        h += "\n<div class=cfg-toast id=otaToast></div>";
        h += "\n</div>";

        // Reset All
        h += "\n<button class=reset-btn onclick=showResetModal()>&#x1f504; Reset All Data</button>";
        h += "\n<div class=ovr id=resetModal>";
        h += "\n  <div class=sht>";
        h += "\n    <div class=gr></div>";
        h += "\n    <h3 style=text-align:center>Reset All Data</h3>";
        h += "\n    <p style=text-align:center;color:#8e8e93;font-size:13px;margin:12px 0>This will reset:<br>- Energy counters (Day/Month/Year)<br>- All alarms<br>- Outage data<br><br><b style=color:#ff3b30>This cannot be undone!</b></p>";
        h += "\n    <div class=btns>";
        h += "\n      <button class=btn btn-s onclick=hideResetModal()>Cancel</button>";
        h += "\n      <button class=btn btn-r id=resetConfirmBtn onclick=doReset()>Reset Everything</button>";
        h += "\n    </div>";
        h += "\n  </div>";
        h += "\n</div>";

        return h;
    }

    // === Settings JS ===
    static String buildSettingsJs() {
        String j;
        j += "\n\nvar _cfgInt=null;";
        j += "\n\nfunction startCfg(){loadCfg()}";
        j += "\n\nfunction stopCfg(){}";

        j += "\n\nfunction loadCfg(){";
        j += "\n\nvar x=new XMLHttpRequest();";
        j += "\n\nx.open('GET','/api/config');";
        j += "\n\nx.onload=function(){if(x.status!=200)return;";
        j += "\n\ntry{var d=JSON.parse(x.responseText)}catch(e){return}";
        j += "\n\nupdateCfgUI(d)};x.send()}";

        j += "\n\nfunction updateCfgUI(d){";
        j += "\n\nvar lm=d.localMode;var src=d.source||(lm?'local':'server');";
        j += "\n\nvar sr=d.serverReachable;";

        // Source badge
        j += "\n\nvar s=$i('cfgSrc');";
        j += "\n\nif(src=='local'){s.textContent='Local';s.className='cfg-source cfg-src-local'}";
        j += "\n\nelse{s.textContent='Server';s.className='cfg-source cfg-src-server'}";

        // Server connection status indicator
        j += "\n\nvar st=$i('cfgStatus');";
        j += "\n\nif(lm){";
        j += "\n\nst.innerHTML='🟠 Local mode — independent from server';";
        j += "\n\n}else if(sr){";
        j += "\n\nst.innerHTML='🟢 Server online — config from server';";
        j += "\n\n}else{";
        j += "\n\nst.innerHTML='🔴 Connection error — showing local data';";
        j += "\n\n}";

        // Toggle (skip if recently toggled by user)
        j += "\n\nif(Date.now()-_lastToggle>2000)$i('localModeChk').checked=lm;";

        // Fields
        j += "\n\nvar fields=[";
        j += "\n\n['cfgPublishMs',d.publishIntervalMs],";
        j += "\n\n['cfgPhaseCount',d.phaseCount],";
        j += "\n\n['cfgOverV',d.overVoltage],['cfgUnderV',d.underVoltage],['cfgImbalance',d.phaseImbalance],";
        j += "\n\n['cfgOverI',d.overCurrent],['cfgHighP',d.highPower],";
        j += "\n\n['cfgTempTh',d.temperatureThreshold],";
        j += "\n\n['cfgLowPF',d.lowPF],['cfgFMin',d.freqMin],['cfgFMax',d.freqMax]";
        j += "\n\n];";

        // Calibration fields (single set for all phases)
        j += "\n\nvar cal=d.cal;if(cal){";
        j += "\n\n$i('calCur').value=cal.current;";
        j += "\n\n$i('calPwr').value=cal.power;";
        j += "\n\n$i('calPf').value=cal.pf;";
        j += "\n\n$i('calEnr').value=cal.energy;";
        j += "\n\n$i('calOff').value=cal.offset;";
        j += "\n\n}";
        j += "\n\nif(d.sensorType!==undefined)$i('sensorType').value=d.sensorType;";
        j += "\n\nif(d.calEnabled!==undefined && Date.now()-_lastToggle>2000)$i('calEnabled').checked=d.calEnabled;";

        j += "\n\nfor(var i=0;i<fields.length;i++){";
        j += "\n\nvar el=$i(fields[i][0]);";
        j += "\n\nif(el){el.value=fields[i][1];el.disabled=!lm}";
        j += "\n\n}";

        // Save button
        j += "\n\n$i('cfgSave').className='cfg-save'+(lm?' show':'')";
        j += "\n\n}";

        // Local mode toggle handler
        j += "\n\nfunction onLocalModeToggle(){";
        j += "\n\n_lastToggle=Date.now();";
        j += "\n\nvar lm=$i('localModeChk').checked;";
        j += "\n\nvar inputs=document.querySelectorAll('.cfgIn');";
        j += "\n\nfor(var i=0;i<inputs.length;i++)inputs[i].disabled=!lm;";
        j += "\n\n$i('cfgSave').className='cfg-save'+(lm?' show':'');";
        j += "\n\nsaveLocalModeOnly(lm)";
        j += "\n\n}";

        // Save only localMode toggle state without other fields
        j += "\n\nfunction saveLocalModeOnly(val){";
        j += "\n\nvar x=new XMLHttpRequest();";
        j += "\n\nx.open('POST','/api/config');";
        j += "\n\nx.setRequestHeader('Content-Type','application/json');";
        j += "\n\nx.onload=function(){";
        j += "\n\nif(x.status==200){";
        j += "\n\nvar s=$i('cfgSrc');var st=$i('cfgStatus');";
        j += "\n\nif(val){s.textContent='Local';s.className='cfg-source cfg-src-local';st.innerHTML='🟠 Local mode - independent from server'}";
        j += "\n\nelse{s.textContent='Server';s.className='cfg-source cfg-src-server';st.innerHTML='🟡 Server mode'}";
        j += "\n\n}};";
        j += "\n\nx.send('{\"localMode\":'+val+'}')";
        j += "\n\n}";

        // Calibration toggle handler
        j += "\n\nfunction onCalToggle(){";
        j += "\n\n_lastToggle=Date.now();";
        j += "\n\nvar en=$i('calEnabled').checked;";
        j += "\n\nsaveCalOnly(en)";
        j += "\n\n}";

        // Sensor type change handler — set defaults for the selected type
        j += "\n\nfunction onSensorTypeChange(){";
        j += "\n\nvar t=parseInt($i('sensorType').value);";
        j += "\n\nif(t==1){";
        j += "\n\n$i('calCur').value=1.0;$i('calPwr').value=1.0;";
        j += "\n\n$i('calPf').value=1.0;$i('calEnr').value=1.0;";
        j += "\n\n$i('calOff').value=0;";
        j += "\n\n}else{";
        j += "\n\n$i('calCur').value=2.05;$i('calPwr').value=2.10;";
        j += "\n\n$i('calPf').value=1.02;$i('calEnr').value=1.08;";
        j += "\n\n$i('calOff').value=0;";
        j += "\n\n}";
        j += "\n\n}";

        // Save only calEnabled state
        j += "\n\nfunction saveCalOnly(val){";
        j += "\n\nvar x=new XMLHttpRequest();";
        j += "\n\nx.open('POST','/api/config');";
        j += "\n\nx.setRequestHeader('Content-Type','application/json');";
        j += "\n\nx.onload=function(){if(x.status==200){";
        j += "\n\n$i('cfgToast').textContent=val?'✓ Calibration ON':'✓ Calibration OFF';";
        j += "\n\n$i('cfgToast').className='cfg-toast show ok';";
        j += "\n\nsetTimeout(function(){$i('cfgToast').className='cfg-toast'},2000)";
        j += "\n\n}};";
        j += "\n\nx.onerror=function(){";
        j += "\n\n$i('cfgToast').textContent='✗ Failed to toggle calibration';$i('cfgToast').className='cfg-toast show er';";
        j += "\n\nsetTimeout(function(){$i('cfgToast').className='cfg-toast'},3000)";
        j += "\n\n};";
        j += "\n\nx.send('{\"calEnabled\":'+val+'}')";
        j += "\n\n}";

        // Save calibration values (flat fields — always works regardless of local mode)
        j += "\n\nfunction saveCalCfg(){";
        j += "\n\nvar d={};";
        j += "\n\nd.sensorType=parseInt($i('sensorType').value)||0;";
        j += "\n\nd.calCurrent=parseFloat($i('calCur').value)||2.05;";
        j += "\n\nd.calPower=parseFloat($i('calPwr').value)||2.10;";
        j += "\n\nd.calPf=parseFloat($i('calPf').value)||1.02;";
        j += "\n\nd.calEnergy=parseFloat($i('calEnr').value)||1.08;";
        j += "\n\nd.calOffset=parseFloat($i('calOff').value)||0;";
        j += "\n\nd.calEnabled=$i('calEnabled').checked;";
        j += "\n\nvar btn=$i('calSave');btn.disabled=true;btn.textContent='Saving...';";
        j += "\n\nvar x=new XMLHttpRequest();";
        j += "\n\nx.open('POST','/api/config');";
        j += "\n\nx.setRequestHeader('Content-Type','application/json');";
        j += "\n\nx.onload=function(){";
        j += "\n\nvar t=$i('calToast');";
        j += "\n\nif(x.status==200){";
        j += "\n\nt.textContent='✓ Calibration saved';t.className='cfg-toast show ok';";
        j += "\n\n}else{t.textContent='✗ Calibration save failed — HTTP '+x.status;t.className='cfg-toast show er';}";
        j += "\n\nbtn.disabled=false;btn.textContent='💾 Save Calibration';";
        j += "\n\nsetTimeout(function(){$i('calToast').className='cfg-toast'},3000)";
        j += "\n\n};";
        j += "\n\nx.onerror=function(){";
        j += "\n\n$i('calToast').textContent='✗ Network error';$i('calToast').className='cfg-toast show er';";
        j += "\n\nbtn.disabled=false;btn.textContent='💾 Save Calibration'";
        j += "\n\n};";
        j += "\n\nx.send(JSON.stringify(d))}";

        // Save threshold settings (always includes localMode state)
        j += "\n\nfunction saveLocalCfg(){";
        j += "\n\nvar d={};";
        j += "\n\nd.localMode=$i('localModeChk').checked;";
        j += "\n\nd.publishIntervalMs=parseInt($i('cfgPublishMs').value)||15000;";
        j += "\n\nd.phaseCount=parseInt($i('cfgPhaseCount').value)||3;";
        j += "\n\nd.overVoltage=parseFloat($i('cfgOverV').value)||253;";
        j += "\n\nd.underVoltage=parseFloat($i('cfgUnderV').value)||207;";
        j += "\n\nd.phaseImbalance=parseFloat($i('cfgImbalance').value)||15;";
        j += "\n\nd.overCurrent=parseFloat($i('cfgOverI').value)||20;";
        j += "\n\nd.highPower=parseFloat($i('cfgHighP').value)||5000;";
        j += "\n\nd.temperatureThreshold=parseFloat($i('cfgTempTh').value)||40;";
        j += "\n\nd.lowPF=parseFloat($i('cfgLowPF').value)||0.8;";
        j += "\n\nd.freqMin=parseFloat($i('cfgFMin').value)||49.5;";
        j += "\n\nd.freqMax=parseFloat($i('cfgFMax').value)||50.5;";

        j += "\n\nvar btn=$i('cfgSave');btn.disabled=true;btn.textContent='Saving...';";

        j += "\n\nvar x=new XMLHttpRequest();";
        j += "\n\nx.open('POST','/api/config');";
        j += "\n\nx.setRequestHeader('Content-Type','application/json');";
        j += "\n\nx.onload=function(){";
        j += "\n\nvar t=$i('cfgToast');";
        j += "\n\nif(x.status==200){";
        j += "\n\nt.textContent='✓ Thresholds saved'+(d.localMode?' (Local Mode)':' (Server Mode)');t.className='cfg-toast show ok';";
        j += "\n\n}else{";
        j += "\n\nt.textContent='✗ Save failed — HTTP '+x.status;t.className='cfg-toast show er';";
        j += "\n\n}";
        j += "\n\nbtn.disabled=false;btn.textContent='💾 Save Local Settings';";
        j += "\n\nsetTimeout(function(){$i('cfgToast').className='cfg-toast'},3000)";
        j += "\n\n};";
        j += "\n\nx.onerror=function(){";
        j += "\n\n$i('cfgToast').textContent='✗ Network error — ESP unreachable';$i('cfgToast').className='cfg-toast show er';";
        j += "\n\nbtn.disabled=false;btn.textContent='💾 Save Local Settings'";
        j += "\n\n};";
        j += "\n\nx.send(JSON.stringify(d))}";

        // OTA check function
        j += "\n\nfunction triggerOtaCheck(){";
        j += "\n\nvar btn=$i('otaCheckBtn');var ts=$i('otaToast');var st=$i('otaStatus');";
        j += "\n\nbtn.disabled=true;btn.innerHTML='⟳ Checking...';ts.className='cfg-toast';st.innerHTML='';";
        j += "\n\nvar x=new XMLHttpRequest();";
        j += "\n\nx.open('GET','/api/ota-check');";
        j += "\n\nx.onload=function(){";
        j += "\n\nif(x.status==200){";
        j += "\n\ntry{var d=JSON.parse(x.responseText)}catch(e){return}";
        j += "\n\nif(d.ok){st.innerHTML='🟡 Check initiated...';setTimeout(otaPollStatus,2000)}";
        j += "\n\nelse{st.innerHTML='🔴 '+d.msg;btn.disabled=false;btn.innerHTML='⟳ Check for Updates'}";
        j += "\n\n}else{btn.disabled=false;btn.innerHTML='⟳ Check for Updates'}";
        j += "\n\n};";
        j += "\n\nx.onerror=function(){";
        j += "\n\nts.textContent='✗ Network error';ts.className='cfg-toast show er';";
        j += "\n\nbtn.disabled=false;btn.innerHTML='⟳ Check for Updates'";
        j += "\n\n};x.send()}";

        // OTA status polling
        j += "\n\nfunction otaPollStatus(){";
        j += "\n\nvar x=new XMLHttpRequest();";
        j += "\n\nx.open('GET','/api/ota-status');";
        j += "\n\nx.onload=function(){";
        j += "\n\nif(x.status!=200)return;";
        j += "\n\ntry{var d=JSON.parse(x.responseText)}catch(e){return}";
        j += "\n\nvar st=$i('otaStatus');var btn=$i('otaCheckBtn');var vs=$i('otaVersion');";
        j += "\n\nif(vs)vs.textContent=d.version;";
        j += "\n\nif(d.status=='updating'){";
        j += "\n\nst.innerHTML='🟡 Updating to '+d.version+'...';";
        j += "\n\nsetTimeout(otaPollStatus,3000)";
        j += "\n\n}else if(d.status=='idle'){";
        j += "\n\nst.innerHTML='🟢 Up to date ('+d.version+')';";
        j += "\n\nbtn.disabled=false;btn.innerHTML='⟳ Check for Updates'";
        j += "\n\n}};x.send()}";

        // Initial OTA version load
        j += "\n\nsetTimeout(function(){";
        j += "\n\nvar x=new XMLHttpRequest();";
        j += "\n\nx.open('GET','/api/ota-status');";
        j += "\n\nx.onload=function(){if(x.status==200){try{var d=JSON.parse(x.responseText);";
        j += "\n\nvar vs=$i('otaVersion');if(vs)vs.textContent=d.version;";
        j += "\n\n}catch(e){}}}";
        j += "\n\nx.send()";
        j += "\n\n},1000)";

        return j;
    }
    // === Log Panel ===
    static String buildLogHtml() {
        String h;
        h += "\n<div id=logPanel>";
        h += "\n<div class=cfg-hdr><h2>System Logs</h2><button class=log-clear onclick=clearLogs()>&#x1F5D1; Clear</button></div>";
        h += "\n<div class=log-container id=logContainer>";
        h += "\n<div class=log-empty id=logEmpty>No logs yet</div>";
        h += "\n<div id=logList></div>";
        h += "\n</div>";
        h += "\n<div class=log-auto><label class=tgl-rw style='margin:0;padding:8px 0'><span class=tgl-lbl>Auto-refresh</span><label class=tgl><input type=checkbox id=logAutoChk checked onchange=toggleLogAuto()><span class=sl></span></label></label></div>";
        h += "\n</div>";
        return h;
    }

    static String buildLogJs() {
        String j;
        j += "\n\nvar _logInt=null;";
        j += "\n\nfunction startLog(){loadLogs();if(_logInt)return;_logInt=setInterval(loadLogs,2000)}";
        j += "\n\nfunction stopLog(){if(_logInt){clearInterval(_logInt);_logInt=null}}";
        j += "\n\nfunction toggleLogAuto(){var en=$i('logAutoChk').checked;if(en){startLog()}else{stopLog()}}";
        j += "\n\nfunction loadLogs(){";
        j += "\n\nvar x=new XMLHttpRequest();";
        j += "\n\nx.open('GET','/api/logs');";
        j += "\n\nx.onload=function(){";
        j += "\n\nif(x.status!=200){$i('logList').innerHTML='<div class=log-error>Failed to load logs (HTTP '+x.status+')</div>';return}";
        j += "\n\ntry{var logs=JSON.parse(x.responseText)}catch(e){return}";
        j += "\n\nrenderLogs(logs)";
        j += "\n\n};";
        j += "\n\nx.onerror=function(){";
        j += "\n\n$i('logList').innerHTML='<div class=log-error>Network error</div>'";
        j += "\n\n};x.send()}";

        j += "\n\nfunction renderLogs(logs){";
        j += "\n\nvar list=$i('logList');var empty=$i('logEmpty');";
        j += "\n\nif(!logs||!logs.length){list.innerHTML='';empty.style.display='block';return}";
        j += "\n\nempty.style.display='none';";
        j += "\n\nvar html='';";
        j += "\n\nvar baseTime=logs[0]?logs[0].t:0;";
        j += "\n\nvar start=Math.max(0,logs.length-6);";
        j += "\n\nfor(var i=logs.length-1;i>=start;i--){";
        j += "\n\nvar e=logs[i];";
        j += "\n\nvar diff=e.t-baseTime;";
        j += "\n\nvar ts='+'+Math.floor(diff/1000)+'.'+(diff%1000);";
        j += "\n\nif(ts==='+0.0')ts='now';";
        j += "\n\nvar msg=esc(e.m||'');";
        j += "\n\nvar cls='log-entry';";
        j += "\n\nif(msg.indexOf('ERROR')>=0||msg.indexOf('FAILED')>=0||msg.indexOf('fail')>=0)cls+=' log-err';";
        j += "\n\nelse if(msg.indexOf('SUCCESS')>=0||msg.indexOf('ok')>=0)cls+=' log-ok';";
        j += "\n\nelse if(msg.indexOf('WARN')>=0)cls+=' log-warn';";
        j += "\n\nhtml+='<div class=\"'+cls+'\"><span class=log-ts>'+ts+'s</span><span class=log-msg>'+msg+'</span></div>'";
        j += "\n\n}";
        j += "\n\nlist.innerHTML=html;";
        j += "\n\nlist.scrollTop=list.scrollHeight";
        j += "\n\n}";

        j += "\n\nfunction clearLogs(){";
        j += "\n\nvar x=new XMLHttpRequest();";
        j += "\n\nx.open('POST','/api/logs');";
        j += "\n\nx.onload=function(){if(x.status==200){$i('logList').innerHTML='';$i('logEmpty').style.display='block'}};";
        j += "\n\nx.send()}";
        return j;
    }


};

#endif

