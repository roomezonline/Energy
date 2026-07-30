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

    private static string P(decimal value, string fmt = "N0") => ToPersian(value.ToString(fmt));
    private static string P(int value) => ToPersian(value.ToString("N0"));

    private static string ToPersian(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (c >= '0' && c <= '9') sb.Append((char)('۰' + (c - '0')));
            else sb.Append(c);
        }
        return sb.ToString();
    }

    public async Task<byte[]> GenerateBillingReportAsync(BillingCalculationRequest request, CancellationToken ct = default)
    {
        var r = await _billing.CalculateAsync(request, ct);

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontFamily(F).FontSize(9).LineHeight(1.3f));
                page.ContentFromRightToLeft();

                // ═══ HEADER ═══
                page.Header().Column(h =>
                {
                    h.Item().Background("#1e293b").Padding(16).Row(row =>
                    {
                        // Right (first item in RTL row): Logo
                        row.AutoItem().AlignRight().Column(c =>
                        {
                            c.Item().Text("انرژی‌کال").Bold().FontSize(22).FontColor("#fff");
                            c.Item().PaddingTop(2).Text("سامانه پایش هوشمند انرژی").FontSize(9).FontColor("#94a3b8");
                        });
                        // Center: Title + date/duration in two columns
                        row.RelativeItem().AlignCenter().Column(c =>
                        {
                            c.Item().Text("صورتحساب مصرف انرژی").Bold().FontSize(18).FontColor("#fff").AlignCenter();
                            c.Item().PaddingTop(6).Row(r2 =>
                            {
                                r2.RelativeItem().AlignCenter().Text($"{r.FromDate}  تا  {r.ToDate}").FontSize(9).FontColor("#94a3b8").AlignCenter();
                                r2.RelativeItem().AlignCenter().Text($"{P(r.Days)} روز  |  {P(r.Months)} ماه").FontSize(9).FontColor("#94a3b8").AlignCenter();
                            });
                        });
                        // Left (last item in RTL row): Consumer type + Tariff chip
                        row.AutoItem().AlignLeft().Column(c =>
                        {
                            if (!string.IsNullOrEmpty(r.ConsumerTypeName))
                                c.Item().PaddingBottom(4).Text(r.ConsumerTypeName).FontSize(9).FontColor("#cbd5e1");
                            c.Item().Background("#f59e0b").PaddingHorizontal(14).PaddingVertical(5).Text(r.TariffName).FontSize(11).FontColor("#fff").Bold().AlignCenter();
                        });
                    });
                    h.Item().Background("#f59e0b").Height(3);
                });

                // ═══ CONTENT ═══
                page.Content().Column(content =>
                {
                    content.Spacing(12);

                    // ─── Info cards ───
                    content.Item().Row(row =>
                    {
                        row.Spacing(8);
                        InfoCard(row, "مشترک", r.CenterName, 170);
                        InfoCard(row, "دوره صورتحساب", $"{r.FromDate}  تا  {r.ToDate}", 200);
                        InfoCard(row, "مدت", $"{P(r.Days)} روز  {P(r.Months)} ماه", 100);
                    });

                    // ─── Consumer info ───
                    if (!string.IsNullOrEmpty(r.ConsumerTypeName) || r.EcaCoefficient.HasValue)
                    {
                        content.Item().Background("#f8fafc").Border(1).BorderColor("#e2e8f0").Padding(10).Row(row =>
                        {
                            row.RelativeItem().AlignRight().Text(t =>
                            {
                                t.Span("نوع مصرف‌کننده: ").FontColor("#64748b");
                                t.Span(r.ConsumerTypeName ?? "—").Bold().FontColor("#1e293b");
                                if (r.Year.HasValue)
                                    t.Span($"  |  سال: {P(r.Year.Value)}").FontColor("#64748b");
                                if (r.EcaCoefficient.HasValue)
                                {
                                    var ecaStr = ToPersian(r.EcaCoefficient.Value.ToString("F4"));
                                    t.Span($"  |  ECA: {ecaStr}").Bold().FontColor("#7c3aed");
                                }
                            });
                        });
                    }

                    // ─── Effective rates ───
                    if (r.EffectiveOffPeakRate.HasValue)
                    {
                        content.Item().Background("#f0f9ff").Border(1).BorderColor("#bae6fd").Padding(10).AlignRight().Text(t =>
                        {
                            t.Span("نرخ‌های مؤثر: ").FontColor("#64748b");
                            t.Span($"کم‌باری {P(r.EffectiveOffPeakRate.Value)}").Bold().FontColor("#0284c7");
                            t.Span($"  |  میان‌باری {P(r.EffectiveMidPeakRate.Value)}").Bold().FontColor("#0284c7");
                            t.Span($"  |  اوج‌باری {P(r.EffectivePeakRate.Value)}").Bold().FontColor("#0284c7");
                            t.Span("  ریال/kWh").FontColor("#64748b");
                        });
                    }

                    // ─── TOU multipliers ───
                    if (r.TouOffPeakMultiplier.HasValue)
                    {
                        content.Item().Background("#fffbeb").Border(1).BorderColor("#fde68a").Padding(8).AlignRight().Text(t =>
                        {
                            t.Span("ضرایب TOU: ").FontColor("#92400e");
                            t.Span($"کم‌باری {P(r.TouOffPeakMultiplier.Value)}").Bold().FontColor("#d97706");
                            t.Span($"  |  میان‌باری {P(r.TouMidPeakMultiplier.Value)}").Bold().FontColor("#d97706");
                            t.Span($"  |  اوج‌باری {P(r.TouPeakMultiplier.Value)}").Bold().FontColor("#d97706");
                        });
                    }

                    // ═══ Energy Consumption ───
                    content.Item().Column(sec =>
                    {
                        SectionTitle(sec, "مصرف انرژی");
                        sec.Item().Table(tbl =>
                        {
                            tbl.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(75);
                                c.RelativeColumn();
                                c.RelativeColumn();
                                c.RelativeColumn();
                                c.ConstantColumn(75);
                            });
                            tbl.Header(h =>
                            {
                                h.Cell().Background("#1e293b").PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text("فاز").Bold().FontSize(8).FontColor("#fff");
                                h.Cell().Background("#1e293b").PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text("کم‌باری").Bold().FontSize(8).FontColor("#fff");
                                h.Cell().Background("#1e293b").PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text("میان‌باری").Bold().FontSize(8).FontColor("#fff");
                                h.Cell().Background("#1e293b").PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text("اوج‌باری").Bold().FontSize(8).FontColor("#fff");
                                h.Cell().Background("#1e293b").PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text("جمع").Bold().FontSize(8).FontColor("#fff");
                            });
                            PhaseRow(tbl, "A", "#f97316", r.PhaseA);
                            PhaseRow(tbl, "B", "#3b82f6", r.PhaseB);
                            PhaseRow(tbl, "C", "#ef4444", r.PhaseC);
                            TotalRow(tbl, r.TotalKWh, r.OffPeakKWh, r.MidPeakKWh, r.PeakKWh);
                        });
                    });

                    // ═══ Cost Breakdown ───
                    content.Item().Column(sec =>
                    {
                        SectionTitle(sec, "هزینه مصرف");
                        sec.Item().Table(tbl =>
                        {
                            tbl.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(75);
                                c.ConstantColumn(100);
                                c.RelativeColumn();
                                c.ConstantColumn(30);
                            });

                            int idx = 1;

                            if (r.PhaseACost > 0 || r.PhaseBCost > 0 || r.PhaseCCost > 0)
                            {
                                CostRow(tbl, r.PhaseACost, $"{P(r.PhaseA.Total, "N3")} kWh", "هزینه فاز A", ref idx, "#f0f9ff", "#0369a1");
                                CostRow(tbl, r.PhaseBCost, $"{P(r.PhaseB.Total, "N3")} kWh", "هزینه فاز B", ref idx, "#f0f9ff", "#0369a1");
                                CostRow(tbl, r.PhaseCCost, $"{P(r.PhaseC.Total, "N3")} kWh", "هزینه فاز C", ref idx, "#f0f9ff", "#0369a1");
                            }

                            if (r.HasTieredRates)
                            {
                                if (r.Tier1KWh > 0) CostRow(tbl, r.Tier1Cost, $"{P(r.Tier1Rate)} ریال/kWh", $"پله اول ({P(r.Tier1KWh, "N3")} kWh)", ref idx);
                                if (r.Tier2KWh > 0) CostRow(tbl, r.Tier2Cost, $"{P(r.Tier2Rate)} ریال/kWh", $"پله دوم ({P(r.Tier2KWh, "N3")} kWh)", ref idx);
                                if (r.Tier3KWh > 0) CostRow(tbl, r.Tier3Cost, $"{P(r.Tier3Rate)} ریال/kWh", $"پله سوم ({P(r.Tier3KWh, "N3")} kWh)", ref idx);
                            }
                            else
                            {
                                if (r.OffPeakKWh > 0) CostRow(tbl, r.OffPeakCost, $"{P(r.OffPeakRate)} ریال/kWh", $"کم‌باری ({P(r.OffPeakKWh, "N3")} kWh)", ref idx);
                                if (r.MidPeakKWh > 0) CostRow(tbl, r.MidPeakCost, $"{P(r.MidPeakRate)} ریال/kWh", $"میان‌باری ({P(r.MidPeakKWh, "N3")} kWh)", ref idx);
                                if (r.PeakKWh > 0) CostRow(tbl, r.PeakCost, $"{P(r.PeakRate)} ریال/kWh", $"اوج‌باری ({P(r.PeakKWh, "N3")} kWh)", ref idx);
                            }

                            // Energy subtotal
                            CostCell(tbl, "#fffbeb", "جمع هزینه انرژی", true, "#d97706");
                            CostCell(tbl, "#fffbeb", "", false, "#d97706");
                            CostCell(tbl, "#fffbeb", $"{P(r.EnergyCost)} ریال", true, "#d97706");
                            CostCell(tbl, "#fffbeb", idx++.ToString(), true, "#d97706", true);

                            if (r.MonthlyFixedFeeTotal > 0)
                                CostRow(tbl, r.MonthlyFixedFeeTotal, $"{P(r.Months)} ماه", "آبونمان", ref idx);
                            if (r.DemandCost > 0)
                                CostRow(tbl, r.DemandCost, $"{P(r.MaxDemandKW, "N2")} kW", "هزینه دیماند", ref idx);
                            if (r.PeakPenalty > 0)
                                CostRow(tbl, r.PeakPenalty, "", "جریمه اوج مصرف", ref idx, "#fef2f2", "#dc2626");
                            if (r.OffPeakDiscount > 0)
                                CostRow(tbl, r.OffPeakDiscount, "", "تخفیف کم‌باری", ref idx, "#f0fdf4", "#16a34a");
                            if (r.Article16Cost > 0)
                                CostRow(tbl, r.Article16Cost, "", "قانون ماده ۱۶", ref idx, "#fefce8", "#a16207");
                            if (r.TollAmount > 0)
                                CostRow(tbl, r.TollAmount, "", "عوارض", ref idx);
                            if (r.ReactivePenalty > 0)
                                CostRow(tbl, r.ReactivePenalty, $"{P(r.ReactivePenaltyMultiplier, "F1")}×", $"جریمه راکتیو (PF {P(r.AveragePf, "F3")})", ref idx, "#fef2f2", "#dc2626");
                            if (r.ReactiveBonus > 0)
                                CostRow(tbl, r.ReactiveBonus, "", "پاداش راکتیو", ref idx, "#f0fdf4", "#16a34a");
                        });
                    });

                    // ─── Power Factor ───
                    if (r.AveragePf > 0)
                    {
                        content.Item().Background("#f8fafc").Border(1).BorderColor("#e2e8f0").Padding(10).Row(row =>
                        {
                            row.RelativeItem().AlignRight().Text(t =>
                            {
                                t.Span("ضریب توان (PF):  ").FontColor("#64748b");
                                t.Span($"A: {P(r.AveragePfA, "F3")}  ").FontColor("#f97316");
                                t.Span($"B: {P(r.AveragePfB, "F3")}  ").FontColor("#3b82f6");
                                t.Span($"C: {P(r.AveragePfC, "F3")}  ").FontColor("#ef4444");
                                t.Span($"میانگین: {P(r.AveragePf, "F3")}").Bold().FontColor("#1e293b");
                            });
                        });
                    }

                    // ═══ Summary ═══
                    content.Item().Background("#f0fdf4").Border(1.5f).BorderColor("#86efac").Padding(16).Column(sum =>
                    {
                        sum.Spacing(6);
                        sum.Item().AlignRight().Text($"جمع کل قبل از مالیات: {P(r.SubTotal)} ریال").FontSize(10).FontColor("#374151");
                        sum.Item().AlignRight().Text($"مالیات بر ارزش افزوده ({P(r.TaxPercent)}%): {P(r.TaxAmount)} ریال").FontSize(10).FontColor("#374151");
                        sum.Item().PaddingTop(6).BorderTop(2).BorderColor("#16a34a").AlignRight().Text(t =>
                        {
                            t.Span("قابل پرداخت: ").FontSize(12).FontColor("#374151");
                            t.Span($"{P(r.GrandTotal)} ریال").Bold().FontSize(18).FontColor("#16a34a");
                        });
                    });

                    // ═══ Daily Details ───
                    if (r.PeriodDetails.Count > 0)
                    {
                        content.Item().Column(sec =>
                        {
                            SectionTitle(sec, "جزئیات روزانه مصرف");
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
                                    h.Cell().Background("#f1f5f9").PaddingVertical(4).PaddingHorizontal(3).AlignRight().Text("تاریخ").Bold().FontSize(7).FontColor("#475569");
                                    h.Cell().Background("#f1f5f9").PaddingVertical(4).PaddingHorizontal(3).AlignRight().Text("کم‌باری").Bold().FontSize(7).FontColor("#475569");
                                    h.Cell().Background("#f1f5f9").PaddingVertical(4).PaddingHorizontal(3).AlignRight().Text("میان‌باری").Bold().FontSize(7).FontColor("#475569");
                                    h.Cell().Background("#f1f5f9").PaddingVertical(4).PaddingHorizontal(3).AlignRight().Text("اوج‌باری").Bold().FontSize(7).FontColor("#475569");
                                    h.Cell().Background("#f1f5f9").PaddingVertical(4).PaddingHorizontal(3).AlignRight().Text("جمع").Bold().FontSize(7).FontColor("#475569");
                                });

                                bool alt = false;
                                foreach (var day in r.PeriodDetails)
                                {
                                    var bg = alt ? "#f8fafc" : "#fff";
                                    alt = !alt;
                                    DayCell(tbl, bg, day.PersianDate);
                                    DayCell(tbl, bg, P(day.OffPeakKWh, "N3"));
                                    DayCell(tbl, bg, P(day.MidPeakKWh, "N3"));
                                    DayCell(tbl, bg, P(day.PeakKWh, "N3"));
                                    DayCell(tbl, bg, $"{P(day.TotalKWh, "N3")} kWh", true);
                                }
                            });
                        });
                    }
                });

                // ═══ FOOTER ═══
                page.Footer().Column(ft =>
                {
                    ft.Item().PaddingTop(8).BorderTop(1).BorderColor("#e2e8f0").Row(fr =>
                    {
                        fr.RelativeItem().AlignRight().Text("صادر شده توسط سامانه انرژی‌کال").FontSize(8).FontColor("#94a3b8");
                        fr.RelativeItem().AlignLeft().Text(ToPersian(DateTime.Now.ToString("yyyy/MM/dd HH:mm"))).FontSize(8).FontColor("#94a3b8");
                    });
                    ft.Item().PaddingTop(3).Background("#1e293b").Padding(8).Row(fr =>
                    {
                        fr.RelativeItem().AlignRight().Text("EnergyCal — سامانه پایش هوشمند مصرف انرژی").FontSize(7).FontColor("#94a3b8");
                        fr.RelativeItem().AlignLeft().Text(r.CenterName).FontSize(7).FontColor("#64748b");
                    });
                });
            });
        }).GeneratePdf();
    }

    // ─── Helpers ───

    private static void InfoCard(RowDescriptor row, string label, string value, int width)
    {
        row.ConstantItem(width).Background("#fff").Border(1).BorderColor("#e2e8f0").Padding(10).Column(c =>
        {
            c.Item().AlignRight().Text(label).FontSize(8).FontColor("#64748b");
            c.Item().PaddingTop(3).AlignRight().Text(value).Bold().FontSize(11).FontColor("#1e293b");
        });
    }

    private static void SectionTitle(ColumnDescriptor col, string title)
    {
        col.Item().Row(rt =>
        {
            rt.ConstantItem(4).Height(20).Background("#2563eb").AlignMiddle();
            rt.AutoItem().PaddingRight(10).AlignRight().Text(title).Bold().FontSize(13).FontColor("#1e293b");
        });
    }

    private static void PhaseRow(TableDescriptor tbl, string label, string color, PhasePeriodKWh p)
    {
        var total = p.OffPeak + p.MidPeak + p.Peak;
        BodyCell(tbl, "#fff", $"{P(total, "N3")} kWh", true, color);
        BodyCell(tbl, "#fff", P(p.Peak, "N3"));
        BodyCell(tbl, "#fff", P(p.MidPeak, "N3"));
        BodyCell(tbl, "#fff", P(p.OffPeak, "N3"));
        tbl.Cell().Border(0.5f).BorderColor("#e2e8f0").PaddingVertical(5).PaddingHorizontal(4).AlignRight().Row(rc =>
        {
            rc.ConstantItem(8).Background(color).Width(8).Height(8).AlignMiddle();
            rc.RelativeItem().PaddingRight(6).AlignRight().Text($"فاز {label}").FontSize(9);
        });
    }

    private static void TotalRow(TableDescriptor tbl, decimal total, decimal off, decimal mid, decimal peak)
    {
        var bg = "#eef2ff"; var clr = "#2563eb";
        BodyCell(tbl, bg, $"{P(total, "N3")} kWh", true, clr);
        BodyCell(tbl, bg, P(peak, "N3"), true, clr);
        BodyCell(tbl, bg, P(mid, "N3"), true, clr);
        BodyCell(tbl, bg, P(off, "N3"), true, clr);
        tbl.Cell().Background(bg).Border(0.5f).BorderColor("#c7d2fe").PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text("جمع کل").Bold().FontSize(9).FontColor(clr);
    }

    private static void CostRow(TableDescriptor tbl, decimal amount, string rate, string desc, ref int idx, string bg = "#fff", string clr = "#1e293b")
    {
        CostCell(tbl, bg, $"{P(amount)} ریال", true, clr);
        CostCell(tbl, bg, rate, false, clr);
        CostCell(tbl, bg, desc, false, clr);
        CostCell(tbl, bg, idx++.ToString(), false, clr, true);
    }

    private static void CostCell(TableDescriptor tbl, string bg, string text, bool bold, string clr = "#1e293b", bool center = false)
    {
        var c = tbl.Cell().Background(bg).Border(0.5f).BorderColor("#e2e8f0").PaddingVertical(5).PaddingHorizontal(4);
        if (center) c.AlignCenter(); else c.AlignRight();
        if (bold) c.Text(text).Bold().FontSize(9).FontColor(clr);
        else c.Text(text).FontSize(9).FontColor(clr);
    }

    private static void BodyCell(TableDescriptor tbl, string bg, string text, bool bold = false, string clr = "#1e293b")
    {
        var c = tbl.Cell().Background(bg).Border(0.5f).BorderColor("#e2e8f0").PaddingVertical(5).PaddingHorizontal(4).AlignRight();
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
