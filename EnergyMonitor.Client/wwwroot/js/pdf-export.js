window.pdfExport = {
    downloadInvoice: async function (elementId, filename) {
        try {
            await this._printAsPdf(elementId);
            return true;
        } catch (e) {
            console.error('PDF export failed:', e);
            return false;
        }
    },

    _printAsPdf: function (elementId) {
        var content = document.getElementById(elementId);
        if (!content) { console.error('Element not found:', elementId); return; }

        var html = '<!DOCTYPE html><html dir="rtl" lang="fa"><head><meta charset="UTF-8">';
        html += '<style>';
        html += 'body{font-family:Vazirmatn,system-ui,sans-serif;padding:12px 16px;line-height:1.7;color:#1e293b;margin:0;background:#fff}';

        // Header / Top
        html += '.inv-top{display:flex;align-items:center;justify-content:space-between;margin-bottom:14px;flex-wrap:wrap;gap:8px}';
        html += '.inv-logo{display:flex;align-items:center;gap:8px}';
        html += '.inv-logo-icon{width:40px;height:40px;border-radius:50%;background:linear-gradient(135deg,#2563eb,#8b5cf6);display:flex;align-items:center;justify-content:center;font-size:20px;color:#fff}';
        html += '.inv-logo-title{font-size:16px;font-weight:800;color:#2563eb}';
        html += '.inv-logo-sub{font-size:10px;color:#94a3b8}';
        html += '.inv-title{font-size:18px;font-weight:800;color:#1e293b}';
        html += '.inv-badge{font-size:10px;font-weight:700;background:#fef3c7;color:#d97706;padding:3px 12px;border-radius:20px}';

        // Info cards
        html += '.inv-info{display:flex;flex-wrap:wrap;gap:8px;margin:14px 0 8px}';
        html += '.inv-info-card{flex:1;min-width:140px;display:flex;align-items:center;gap:8px;padding:8px 12px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px}';
        html += '.inv-info-card-sm{flex:0 1 auto;min-width:90px}';
        html += '.inv-info-icon{font-size:18px;width:32px;height:32px;border-radius:8px;background:linear-gradient(135deg,#2563eb,#8b5cf6);display:flex;align-items:center;justify-content:center;color:#fff}';
        html += '.inv-info-lbl{font-size:10px;color:#64748b;font-weight:600}';
        html += '.inv-info-val{font-size:13px;color:#1e293b;font-weight:700}';

        // Sections
        html += '.inv-divider{border-top:1px solid #e2e8f0;margin:12px 0}';
        html += '.inv-section{margin-bottom:12px}';
        html += '.inv-section-title{font-size:14px;font-weight:700;color:#1e293b;margin-bottom:8px;padding-right:10px;border-right:3px solid #2563eb}';

        // Tables
        html += '.inv-table{width:100%;border-collapse:collapse;font-size:12px}';
        html += '.inv-table th{background:linear-gradient(135deg,#2563eb,#3b82f6);color:#fff;font-weight:700;padding:7px 8px;text-align:center;border:1px solid #1d4ed8;font-size:11px}';
        html += '.inv-table td{padding:6px 8px;text-align:center;border:1px solid #e2e8f0;color:#334155}';
        html += '.inv-table tbody tr:nth-child(even){background:#f8fafc}';
        html += '.inv-table .inv-total{font-weight:700;color:#2563eb}';
        html += '.inv-table .inv-grand{font-weight:800;color:#2563eb;font-size:13px}';
        html += '.inv-table tfoot tr{background:#eef2ff;font-weight:700}';
        html += '.inv-table tfoot td{border:1px solid #c7d2fe;color:#1e3a8a}';
        html += '.inv-highlight,.inv-table .inv-highlight td{background:#fffbeb!important;font-weight:700;color:#d97706}';
        html += '.inv-penalty,.inv-table .inv-penalty td{background:#fef2f2!important;color:#dc2626;font-weight:600}';
        html += '.inv-discount,.inv-table .inv-discount td{background:#f0fdf4!important;color:#059669;font-weight:600}';
        html += '.inv-table-xs td{padding:3px 5px;font-size:10px}';
        html += '.inv-ridx{width:20px;min-width:20px}';
        html += '.page-break-before{page-break-before:always}';

        // Summary
        html += '.inv-summary{margin:6px 0;padding:12px 16px;background:linear-gradient(135deg,#eff6ff,#dbeafe);border:1px solid #bfdbfe;border-radius:8px}';
        html += '.inv-summary-row{display:flex;justify-content:space-between;align-items:center;padding:5px 0;font-size:13px;color:#374151}';
        html += '.inv-summary-grand{font-size:20px;font-weight:800;color:#2563eb;padding-top:8px;border-top:2px dashed #93c5fd;margin-top:6px}';
        html += '.inv-summary-grand small{font-size:13px;font-weight:600}';

        // Consumer info
        html += '.inv-consumer-info{padding:8px 12px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;font-size:11px}';
        html += '.inv-consumer-row{display:flex;align-items:center;gap:4px;color:#475569;flex-wrap:wrap}';
        html += '.inv-cval{font-weight:700;color:#1e293b}';

        // PF
        html += '.inv-pf-row{display:flex;flex-wrap:wrap;gap:10px;padding:8px 12px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;font-size:12px}';
        html += '.inv-pf-item{display:flex;align-items:center;gap:6px;color:#475569}';
        html += '.inv-pf-dot{width:8px;height:8px;border-radius:50%;display:inline-block}';
        html += '.inv-pf-val{font-weight:700;color:#1e293b}';
        html += '.inv-pf-avg{padding-right:10px;border-right:1px solid #e2e8f0}';

        // Footer
        html += '.inv-footer{margin-top:14px;padding-top:10px;border-top:1px solid #e2e8f0;display:flex;justify-content:space-between;align-items:center;flex-wrap:wrap;gap:8px}';
        html += '.inv-footer-row{font-size:10px;color:#94a3b8;display:flex;gap:12px}';
        html += '.inv-footer-barcode{display:flex;align-items:center;gap:6px}';
        html += '.inv-qr{width:32px;height:32px;border:1px solid #d1d5db;border-radius:4px;display:flex;align-items:center;justify-content:center;font-size:16px;color:#94a3b8;background:#f8fafc}';

        // Print
        html += '@media print{body{padding:3mm 5mm;font-size:9pt}.inv-table{font-size:9pt}.inv-section-title{font-size:11pt}.inv-title{font-size:14pt}}';
        html += '@page{margin:5mm}';
        html += '</style></head><body>';
        html += content.innerHTML;
        html += '</body></html>';

        var win = window.open('', '_blank');
        win.document.write(html);
        win.document.close();
        win.focus();
        setTimeout(function () { win.print(); }, 500);
    }
};
