using EnergyMonitor.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EnergyMonitor.Services;

public interface IPdfReportService
{
    Task<byte[]> GenerateBillingReportAsync(BillingCalculationRequest request, CancellationToken ct = default);
}

public class PdfReportService : IPdfReportService
{
    private const string F = "Vazirmatn";
    private readonly IBillingService _billing;

    public PdfReportService(IBillingService billing) => _billing = billing;

    // Force RTL direction using Unicode control characters
    private static string R(string text) => "\u202B" + text + "\u202C";

    private static string D(decimal value, string fmt = "N0") =>
        value.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture);

    public async Task<byte[]> GenerateBillingReportAsync(BillingCalculationRequest request, CancellationToken ct = default)
    {
        var r = await _billing.CalculateAsync(request, ct);

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(26);
                page.DefaultTextStyle(x => x.FontFamily(F).FontSize(9));

                page.Header().Column(h =>
                {
                    h.Item().Background("#1e293b").Padding(14).Row(row =>
                    {
                        row.ConstantItem(140).AlignRight().Column(c =>
                        {
                            c.Item().Text(R("انرژی‌کال")).Bold().FontSize(20).FontColor("#fff").AlignRight();
                            c.Item().PaddingTop(2).Text(R("سامانه پایش هوشمند انرژی")).FontSize(8).FontColor("#94a3b8").AlignRight();
                        });
                        row.RelativeItem().AlignCenter().Column(c =>
                        {
                            c.Item().Text(R("صورتحساب مصرف انرژی")).Bold().FontSize(20).FontColor("#fff").AlignCenter();
                            c.Item().PaddingTop(2).Text(R(r.FromDate + "  تا  " + r.ToDate)).FontSize(9).FontColor("#94a3b8").AlignCenter();
                            c.Item().PaddingTop(1).Text(R(r.Days + " روز  |  " + r.Months + " ماه")).FontSize(8).FontColor("#cbd5e1").AlignCenter();
                        });
                        row.ConstantItem(140).AlignLeft().Column(c =>
                        {
                            c.Item().AlignLeft().Background("#f59e0b").PaddingHorizontal(14).PaddingVertical(5).Text(R(r.TariffName)).FontSize(10).FontColor("#fff").Bold().AlignCenter();
                            if (!string.IsNullOrEmpty(r.ConsumerTypeName))
                                c.Item().PaddingTop(4).AlignLeft().Text(R(r.ConsumerTypeName)).FontSize(9).FontColor("#cbd5e1").AlignLeft();
                        });
                    });
                    h.Item().Background("#f59e0b").Height(3);
                });

                page.Content().Column(content =>
                {
                    content.Spacing(10);

                    // Info cards
                    content.Item().Row(row =>
                    {
                        row.Spacing(6);
                        Card(row, R("مشترک"), R(r.CenterName), 180);
                        Card(row, R("دوره صورتحساب"), R(r.FromDate + "  تا  " + r.ToDate), 190);
                        Card(row, R("تعداد روز"), R(r.Days + " روز"), 110);
                    });

                    // Consumer info
                    if (!string.IsNullOrEmpty(r.ConsumerTypeName) || r.EcaCoefficient.HasValue)
                    {
                        content.Item().Background("#f8fafc").Border(1).BorderColor("#e2e8f0").Padding(8).AlignRight().Text(t =>
                        {
                            t.Span(R("نوع مصرف‌کننده: ")).FontColor("#64748b");
                            t.Span(R(r.ConsumerTypeName ?? "—")).Bold().FontColor("#1e293b");
                            if (r.Year.HasValue)
                                t.Span(R("  |  سال: " + r.Year.Value)).FontColor("#64748b");
                            if (r.EcaCoefficient.HasValue)
                                t.Span(R("  |  ECA: " + r.EcaCoefficient.Value.ToString("F4"))).Bold().FontColor("#7c3aed");
                        });
                    }

                    // Effective rates
                    if (r.EffectiveOffPeakRate.HasValue)
                    {
                        content.Item().Background("#f0f9ff").Border(1).BorderColor("#bae6fd").Padding(8).AlignRight().Text(t =>
                        {
                            t.Span(R("نرخ‌های مؤثر: ")).FontColor("#64748b");
                            t.Span(R("کم‌باری " + D(r.EffectiveOffPeakRate.Value, "N0"))).Bold().FontColor("#0284c7");
                            t.Span(R("  |  میان‌باری " + D(r.EffectiveMidPeakRate.Value, "N0"))).Bold().FontColor("#0284c7");
                            t.Span(R("  |  اوج‌باری " + D(r.EffectivePeakRate.Value, "N0"))).Bold().FontColor("#0284c7");
                            t.Span(R("  ریال/kWh")).FontColor("#64748b");
                        });
                    }

                    // ═══ Energy Consumption ═══
                    content.Item().Column(sec =>
                    {
                        SectionTitle(sec, R("مصرف انرژی"));
                        sec.Item().Table(tbl =>
                        {
                            tbl.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(75);
                                c.RelativeColumn();
                                c.RelativeColumn();
                                c.RelativeColumn();
                                c.ConstantColumn(80);
                            });
                            tbl.Header(h =>
                            {
                                h.Cell().Background("#1e293b").PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text(R("جمع")).Bold().FontSize(8).FontColor("#fff");
                                h.Cell().Background("#1e293b").PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text(R("اوج‌باری")).Bold().FontSize(8).FontColor("#fff");
                                h.Cell().Background("#1e293b").PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text(R("میان‌باری")).Bold().FontSize(8).FontColor("#fff");
                                h.Cell().Background("#1e293b").PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text(R("کم‌باری")).Bold().FontSize(8).FontColor("#fff");
                                h.Cell().Background("#1e293b").PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text(R("فاز")).Bold().FontSize(8).FontColor("#fff");
                            });
                            PhaseRow(tbl, "A", "#f97316", r.PhaseA);
                            PhaseRow(tbl, "B", "#3b82f6", r.PhaseB);
                            PhaseRow(tbl, "C", "#ef4444", r.PhaseC);
                            EnergyTotal(tbl, r.TotalKWh, r.OffPeakKWh, r.MidPeakKWh, r.PeakKWh);
                        });
                    });

                    // ═══ Cost Breakdown ═══
                    content.Item().Column(sec =>
                    {
                        SectionTitle(sec, R("هزینه مصرف"));
                        sec.Item().Table(tbl =>
                        {
                            tbl.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(75);
                                c.ConstantColumn(90);
                                c.RelativeColumn();
                                c.ConstantColumn(30);
                            });
                            tbl.Header(h =>
                            {
                                h.Cell().Background("#1e293b").PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text(R("مبلغ (ریال)")).Bold().FontSize(8).FontColor("#fff");
                                h.Cell().Background("#1e293b").PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text(R("نرخ")).Bold().FontSize(8).FontColor("#fff");
                                h.Cell().Background("#1e293b").PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text(R("شرح")).Bold().FontSize(8).FontColor("#fff");
                                h.Cell().Background("#1e293b").PaddingVertical(5).PaddingHorizontal(4).AlignCenter().Text("#").Bold().FontSize(8).FontColor("#fff");
                            });

                            int idx = 1;

                            if (r.PhaseACost > 0 || r.PhaseBCost > 0 || r.PhaseCCost > 0)
                            {
                                Cst(tbl, r.PhaseACost, R("kWh " + D(r.PhaseA.Total, "N3")), R("هزینه فاز A"), idx++, "#f0f9ff", "#0369a1");
                                Cst(tbl, r.PhaseBCost, R("kWh " + D(r.PhaseB.Total, "N3")), R("هزینه فاز B"), idx++, "#f0f9ff", "#0369a1");
                                Cst(tbl, r.PhaseCCost, R("kWh " + D(r.PhaseC.Total, "N3")), R("هزینه فاز C"), idx++, "#f0f9ff", "#0369a1");
                            }

                            if (r.HasTieredRates)
                            {
                                if (r.Tier1KWh > 0) Cst(tbl, r.Tier1Cost, R(D(r.Tier1Rate, "N0") + " ریال/kWh"), R("پله اول (" + D(r.Tier1KWh, "N3") + " kWh)"), idx++);
                                if (r.Tier2KWh > 0) Cst(tbl, r.Tier2Cost, R(D(r.Tier2Rate, "N0") + " ریال/kWh"), R("پله دوم (" + D(r.Tier2KWh, "N3") + " kWh)"), idx++);
                                if (r.Tier3KWh > 0) Cst(tbl, r.Tier3Cost, R(D(r.Tier3Rate, "N0") + " ریال/kWh"), R("پله سوم (" + D(r.Tier3KWh, "N3") + " kWh)"), idx++);
                            }
                            else
                            {
                                if (r.OffPeakKWh > 0) Cst(tbl, r.OffPeakCost, R(D(r.OffPeakRate, "N0") + " ریال/kWh"), R("کم‌باری (" + D(r.OffPeakKWh, "N3") + " kWh)"), idx++);
                                if (r.MidPeakKWh > 0) Cst(tbl, r.MidPeakCost, R(D(r.MidPeakRate, "N0") + " ریال/kWh"), R("میان‌باری (" + D(r.MidPeakKWh, "N3") + " kWh)"), idx++);
                                if (r.PeakKWh > 0) Cst(tbl, r.PeakCost, R(D(r.PeakRate, "N0") + " ریال/kWh"), R("اوج‌باری (" + D(r.PeakKWh, "N3") + " kWh)"), idx++);
                            }

                            // Energy subtotal
                            TblCell(tbl, "#fffbeb", R(D(r.EnergyCost) + " ریال"), true, "#d97706");
                            TblCell(tbl, "#fffbeb", "", false, "#d97706");
                            TblCell(tbl, "#fffbeb", R("جمع هزینه انرژی"), true, "#d97706");
                            TblCell(tbl, "#fffbeb", "", false, "#d97706", true);

                            if (r.MonthlyFixedFeeTotal > 0)
                                Cst(tbl, r.MonthlyFixedFeeTotal, R(r.Months + " ماه"), R("آبونمان"), idx++);
                            if (r.DemandCost > 0)
                                Cst(tbl, r.DemandCost, R(D(r.MaxDemandKW, "N2") + " kW"), R("هزینه دیماند"), idx++);
                            if (r.PeakPenalty > 0)
                                Cst(tbl, r.PeakPenalty, "", R("جریمه اوج مصرف"), idx++, "#fef2f2", "#dc2626");
                            if (r.OffPeakDiscount > 0)
                                Cst(tbl, r.OffPeakDiscount, "", R("تخفیف کم‌باری"), idx++, "#f0fdf4", "#16a34a");
                            if (r.Article16Cost > 0)
                                Cst(tbl, r.Article16Cost, "", R("قانون ماده ۱۶"), idx++, "#fefce8", "#a16207");
                            if (r.TollAmount > 0)
                                Cst(tbl, r.TollAmount, "", R("عوارض"), idx++);
                            if (r.ReactivePenalty > 0)
                                Cst(tbl, r.ReactivePenalty, R(D(r.ReactivePenaltyMultiplier, "F1") + " برابر"), R("جریمه راکتیو (PF " + D(r.AveragePf, "F3") + ")"), idx++, "#fef2f2", "#dc2626");
                            if (r.ReactiveBonus > 0)
                                Cst(tbl, r.ReactiveBonus, "", R("پاداش راکتیو"), idx++, "#f0fdf4", "#16a34a");
                        });
                    });

                    // ═══ Summary ═══
                    content.Item().Background("#f0fdf4").Border(1.5f).BorderColor("#86efac").Padding(14).Column(sum =>
                    {
                        sum.Spacing(6);
                        sum.Item().AlignRight().Text(R("جمع کل قبل از مالیات: " + D(r.SubTotal) + " ریال")).FontSize(10).FontColor("#374151");
                        sum.Item().AlignRight().Text(R("مالیات بر ارزش افزوده (" + r.TaxPercent + "%): " + D(r.TaxAmount) + " ریال")).FontSize(10).FontColor("#374151");
                        sum.Item().PaddingTop(6).BorderTop(2).BorderColor("#16a34a").AlignRight().Text(t =>
                        {
                            t.Span(R("قابل پرداخت: " + D(r.GrandTotal) + " ریال")).Bold().FontSize(16).FontColor("#16a34a");
                        });
                    });

                    // ═══ Daily Details ═══
                    if (r.PeriodDetails.Count > 0)
                    {
                        content.Item().Column(sec =>
                        {
                            SectionTitle(sec, R("جزئیات روزانه مصرف"));

                            sec.Item().Table(tbl =>
                            {
                                tbl.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn();
                                    c.RelativeColumn();
                                    c.RelativeColumn();
                                    c.RelativeColumn();
                                    c.RelativeColumn();
                                });
                                tbl.Header(h =>
                                {
                                    h.Cell().Background("#f1f5f9").PaddingVertical(4).PaddingHorizontal(3).AlignRight().Text(R("جمع")).Bold().FontSize(7).FontColor("#475569");
                                    h.Cell().Background("#f1f5f9").PaddingVertical(4).PaddingHorizontal(3).AlignRight().Text(R("اوج‌باری")).Bold().FontSize(7).FontColor("#475569");
                                    h.Cell().Background("#f1f5f9").PaddingVertical(4).PaddingHorizontal(3).AlignRight().Text(R("میان‌باری")).Bold().FontSize(7).FontColor("#475569");
                                    h.Cell().Background("#f1f5f9").PaddingVertical(4).PaddingHorizontal(3).AlignRight().Text(R("کم‌باری")).Bold().FontSize(7).FontColor("#475569");
                                    h.Cell().Background("#f1f5f9").PaddingVertical(4).PaddingHorizontal(3).AlignRight().Text(R("تاریخ")).Bold().FontSize(7).FontColor("#475569");
                                });

                                bool alt = false;
                                foreach (var day in r.PeriodDetails)
                                {
                                    var bg = alt ? "#f8fafc" : "#fff";
                                    alt = !alt;
                                    DayCell(tbl, bg, R(D(day.TotalKWh, "N3") + " kWh"), true);
                                    DayCell(tbl, bg, D(day.PeakKWh, "N3"));
                                    DayCell(tbl, bg, D(day.MidPeakKWh, "N3"));
                                    DayCell(tbl, bg, D(day.OffPeakKWh, "N3"));
                                    DayCell(tbl, bg, R(day.PersianDate));
                                }
                            });
                        });
                    }
                });

                page.Footer().Column(ft =>
                {
                    ft.Item().PaddingTop(6).BorderTop(1).BorderColor("#e2e8f0").Row(fr =>
                    {
                        fr.RelativeItem().AlignRight().Text(R("صادر شده توسط سامانه انرژی‌کال")).FontSize(8).FontColor("#94a3b8");
                        fr.RelativeItem().AlignLeft().Text(DateTime.Now.ToString("yyyy/MM/dd HH:mm")).FontSize(8).FontColor("#94a3b8");
                    });
                    ft.Item().PaddingTop(2).Background("#1e293b").Padding(6).Row(fr =>
                    {
                        fr.RelativeItem().AlignRight().Text(R("EnergyCal — سامانه پایش هوشمند مصرف انرژی")).FontSize(7).FontColor("#94a3b8");
                        fr.RelativeItem().AlignLeft().Text(R(r.CenterName)).FontSize(7).FontColor("#64748b");
                    });
                });
            });
        }).GeneratePdf();
    }

    private static void Card(RowDescriptor row, string label, string value, int w)
    {
        row.ConstantItem(w).Background("#fff").Border(1).BorderColor("#e2e8f0").Padding(10).Column(c =>
        {
            c.Item().AlignRight().Text(label).FontSize(8).FontColor("#64748b");
            c.Item().PaddingTop(3).AlignRight().Text(value).Bold().FontSize(11).FontColor("#1e293b");
        });
    }

    private static void SectionTitle(ColumnDescriptor col, string title)
    {
        col.Item().Row(rt =>
        {
            rt.ConstantItem(4).Height(18).Background("#2563eb").AlignMiddle();
            rt.AutoItem().PaddingRight(8).AlignRight().Text(title).Bold().FontSize(12).FontColor("#1e293b");
        });
    }

    private static void PhaseRow(TableDescriptor tbl, string label, string color, PhasePeriodKWh p)
    {
        var total = p.OffPeak + p.MidPeak + p.Peak;

        TblCell(tbl, "#fff", R("kWh " + D(total, "N3")), true, color);
        TblCell(tbl, "#fff", D(p.Peak, "N3"), false);
        TblCell(tbl, "#fff", D(p.MidPeak, "N3"), false);
        TblCell(tbl, "#fff", D(p.OffPeak, "N3"), false);

        // فاز (leftmost column)
        tbl.Cell().Border(0.5f).BorderColor("#e2e8f0").PaddingVertical(5).PaddingHorizontal(4).AlignRight().Row(rc =>
        {
            rc.ConstantItem(8).Background(color).Width(8).Height(8).AlignMiddle();
            rc.RelativeItem().PaddingRight(6).AlignRight().Text(R("فاز " + label)).FontSize(9);
        });
    }

    private static void EnergyTotal(TableDescriptor tbl, decimal total, decimal off, decimal mid, decimal peak)
    {
        var bg = "#eef2ff"; var bc = "#c7d2fe"; var clr = "#2563eb";
        TblCell(tbl, bg, R("kWh " + D(total, "N3")), true, clr);
        TblCell(tbl, bg, D(peak, "N3"), true, clr);
        TblCell(tbl, bg, D(mid, "N3"), true, clr);
        TblCell(tbl, bg, D(off, "N3"), true, clr);
        tbl.Cell().Background(bg).Border(0.5f).BorderColor(bc).PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text(R("جمع کل")).Bold().FontSize(9).FontColor(clr);
    }

    private static void Cst(TableDescriptor tbl, decimal amount, string rate, string desc, int idx, string bg = "#fff", string clr = "#1e293b")
    {
        TblCell(tbl, bg, R(D(amount) + " ریال"), true, clr);
        TblCell(tbl, bg, rate, false, clr);
        TblCell(tbl, bg, desc, false, clr);
        TblCell(tbl, bg, idx.ToString(), false, clr, true);
    }

    private static void TblCell(TableDescriptor tbl, string bg, string text, bool bold, string clr = "#1e293b", bool center = false)
    {
        var c = tbl.Cell().Background(bg).Border(0.5f).BorderColor("#e2e8f0").PaddingVertical(5).PaddingHorizontal(4);
        if (center) c.AlignCenter(); else c.AlignRight();
        if (bold) c.Text(text).Bold().FontSize(9).FontColor(clr);
        else c.Text(text).FontSize(9).FontColor(clr);
    }

    private static void DayCell(TableDescriptor tbl, string bg, string text, bool bold = false)
    {
        var c = tbl.Cell().Background(bg).Border(0.5f).BorderColor("#e2e8f0").PaddingVertical(3).PaddingHorizontal(4).AlignRight();
        if (bold) c.Text(text).Bold().FontSize(7).FontColor("#1e293b");
        else c.Text(text).FontSize(7).FontColor("#1e293b");
    }
}
