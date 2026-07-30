using System.ComponentModel.DataAnnotations;
using EnergyMonitor.Data;
using EnergyMonitor.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Invoice = EnergyMonitor.Data.Invoice;
using InvoiceDetail = EnergyMonitor.Data.InvoiceDetail;

namespace EnergyMonitor.Controllers;

[Authorize]
[ApiController]
[Route("api/billing")]
public class BillingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<BillingController> _log;

    public BillingController(AppDbContext db, ILogger<BillingController> log)
    {
        _db = db;
        _log = log;
    }

    [HttpPost("calculate")]
    public async Task<IActionResult> Calculate([FromBody] BillingRequest request,
        [FromQuery] bool saveInvoice = false)
    {
        if (string.IsNullOrEmpty(request.FromDate) || string.IsNullOrEmpty(request.ToDate))
            return BadRequest(new { error = "بازه تاریخ را وارد کنید" });
        if (request.CenterId == Guid.Empty)
            return BadRequest(new { error = "شناسه مرکز را وارد کنید" });

        var center = await _db.Centers.FirstOrDefaultAsync(c => c.Id == request.CenterId);
        if (center is null) return NotFound(new { error = "مرکز یافت نشد" });

        Tariff? tariff = null;

        // Priority: explicit tariffId > center.TariffId > first active tariff
        if (request.TariffId.HasValue && request.TariffId.Value != Guid.Empty)
            tariff = await _db.Tariffs.FirstOrDefaultAsync(t => t.Id == request.TariffId.Value);
        else if (center.TariffId.HasValue)
            tariff = await _db.Tariffs.FirstOrDefaultAsync(t => t.Id == center.TariffId.Value);
        else
            tariff = await _db.Tariffs.Where(t => t.IsActive).OrderByDescending(t => t.CreatedAt).FirstOrDefaultAsync();

        if (tariff is null)
            return BadRequest(new { error = "مرکز فاقد تعرفه است. ابتدا یک تعرفه به مرکز اختصاص دهید یا از لیست تعرفه انتخاب کنید" });

        var device = await _db.Devices.Where(d => d.CenterId == center.Id && d.IsActive)
            .OrderBy(d => d.DisplayName).FirstOrDefaultAsync();
        if (device is null)
            return NotFound(new { error = "مرکز فاقد دستگاه فعال است" });

        var iranTz = TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");
        var pc = new PersianCalendar();

        DateTime fromPersian, toPersian;
        try
        {
            fromPersian = ParsePersianDate(request.FromDate, pc);
            toPersian = ParsePersianDate(request.ToDate, pc).AddDays(1);
        }
        catch
        {
            return BadRequest(new { error = "فرمت تاریخ نامعتبر است (مورد نیاز: 1403/04/01)" });
        }

        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(fromPersian, iranTz);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(toPersian, iranTz);
        var monthsCovered = CountPersianMonths(fromPersian, toPersian.AddDays(-1), pc);

        // Per-phase hourly consumption via raw SQL
        var hourlyData = await GetHourlyConsumptionPerPhase(device.DeviceId, fromUtc, toUtc);

        // Sanitize: zero out impossible hourly sums (> 10000 kWh per hour per phase)
        const decimal maxHourlyKWh = 10000;
        for (int i = 0; i < hourlyData.Count; i++)
        {
            var (date, hour, dA, dB, dC) = hourlyData[i];
            if (dA > maxHourlyKWh || dB > maxHourlyKWh || dC > maxHourlyKWh)
            {
                _log.LogWarning("Billing sanitize: impossible hourly sum at {Date} hour {Hour}: A={dA}, B={dB}, C={dC}",
                    date, hour, dA, dB, dC);
                hourlyData[i] = (date, hour,
                    dA > maxHourlyKWh ? 0 : dA,
                    dB > maxHourlyKWh ? 0 : dB,
                    dC > maxHourlyKWh ? 0 : dC);
            }
        }

        // Classify by TOU period per phase
        var phaseA = new PhasePeriodKWh();
        var phaseB = new PhasePeriodKWh();
        var phaseC = new PhasePeriodKWh();
        decimal offPeakKWh = 0, midPeakKWh = 0, peakKWh = 0;
        var periodDetails = new List<BillingPeriodDetail>();

        foreach (var (date, hour, dA, dB, dC) in hourlyData)
        {
            var persianDt = TimeZoneInfo.ConvertTimeFromUtc(date, iranTz);
            var pMonth = pc.GetMonth(persianDt);
            var period = GetPeriodType(tariff, pMonth, hour);

            switch (period)
            {
                case "OffPeak":
                    phaseA.OffPeak += dA; phaseB.OffPeak += dB; phaseC.OffPeak += dC;
                    offPeakKWh += dA + dB + dC;
                    break;
                case "MidPeak":
                    phaseA.MidPeak += dA; phaseB.MidPeak += dB; phaseC.MidPeak += dC;
                    midPeakKWh += dA + dB + dC;
                    break;
                case "Peak":
                    phaseA.Peak += dA; phaseB.Peak += dB; phaseC.Peak += dC;
                    peakKWh += dA + dB + dC;
                    break;
            }

            var pDateStr = $"{pc.GetYear(persianDt):D4}/{pc.GetMonth(persianDt):D2}/{pc.GetDayOfMonth(persianDt):D2}";
            var existing = periodDetails.FirstOrDefault(d => d.PersianDate == pDateStr);
            var totalRow = dA + dB + dC;
            if (existing is null)
            {
                periodDetails.Add(new BillingPeriodDetail
                {
                    PersianDate = pDateStr,
                    OffPeakKWh = period == "OffPeak" ? totalRow : 0,
                    MidPeakKWh = period == "MidPeak" ? totalRow : 0,
                    PeakKWh = period == "Peak" ? totalRow : 0,
                    TotalKWh = totalRow
                });
            }
            else
            {
                if (period == "OffPeak") existing.OffPeakKWh += totalRow;
                else if (period == "MidPeak") existing.MidPeakKWh += totalRow;
                else if (period == "Peak") existing.PeakKWh += totalRow;
                existing.TotalKWh += totalRow;
            }
        }

        var totalKWh = offPeakKWh + midPeakKWh + peakKWh;

        // Load per-phase per-period rates from TariffRate table (override defaults)
        var tariffRates = await _db.TariffRates
            .Where(r => r.TariffId == tariff.Id)
            .ToListAsync();

        decimal GetRate(string phase, string period) =>
            tariffRates.FirstOrDefault(r => r.Phase == phase && r.PeriodType == period)?.RatePerKWh
                ?? (period switch
                {
                    "OffPeak" => tariff.OffPeakRate,
                    "MidPeak" => tariff.MidPeakRate,
                    "Peak" => tariff.PeakRate,
                    _ => 0m
                });

        // Compute cost per period using the corresponding rate
        var offPeakCost = phaseA.OffPeak * GetRate("A", "OffPeak")
            + phaseB.OffPeak * GetRate("B", "OffPeak")
            + phaseC.OffPeak * GetRate("C", "OffPeak");
        var midPeakCost = phaseA.MidPeak * GetRate("A", "MidPeak")
            + phaseB.MidPeak * GetRate("B", "MidPeak")
            + phaseC.MidPeak * GetRate("C", "MidPeak");
        var peakCost = phaseA.Peak * GetRate("A", "Peak")
            + phaseB.Peak * GetRate("B", "Peak")
            + phaseC.Peak * GetRate("C", "Peak");
        var energyCost = offPeakCost + midPeakCost + peakCost;

        // Display rates (use phase A rates as representative)
        var displayOffPeakRate = GetRate("A", "OffPeak");
        var displayMidPeakRate = GetRate("A", "MidPeak");
        var displayPeakRate = GetRate("A", "Peak");

        // Average PF for reactive penalty
        var avgPf = await _db.EnergySnapshots
            .Where(s => s.DeviceId == device.DeviceId && s.Timestamp >= fromUtc && s.Timestamp < toUtc
                && s.PfA > 0 && s.PfB > 0 && s.PfC > 0)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                pfA = g.Average(s => s.PfA),
                pfB = g.Average(s => s.PfB),
                pfC = g.Average(s => s.PfC)
            }).FirstOrDefaultAsync();

        decimal avgPfA = avgPf?.pfA ?? 1;
        decimal avgPfB = avgPf?.pfB ?? 1;
        decimal avgPfC = avgPf?.pfC ?? 1;
        decimal minPf = Math.Min(Math.Min(avgPfA, avgPfB), avgPfC);

        decimal reactivePenalty = 0;
        bool hasPenalty = false;
        if (minPf < tariff.ReactivePenaltyThreshold && totalKWh > 0)
        {
            var pfShortfall = tariff.ReactivePenaltyThreshold - minPf;
            reactivePenalty = energyCost * pfShortfall * tariff.ReactivePenaltyMultiplier;
            hasPenalty = true;
        }

        var monthlyFeeTotal = tariff.MonthlyFixedFee * Math.Max(1, monthsCovered);
        var subTotal = energyCost + reactivePenalty + monthlyFeeTotal;
        var taxAmount = subTotal * 0.09m;
        var grandTotal = subTotal + taxAmount;

        var result = new BillingResultDto
        {
            CenterName = center.Name,
            TariffName = tariff.Name,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            Days = (int)(toUtc - fromUtc).TotalDays,
            Months = monthsCovered,

            OffPeakKWh = Math.Round(offPeakKWh, 4),
            MidPeakKWh = Math.Round(midPeakKWh, 4),
            PeakKWh = Math.Round(peakKWh, 4),
            TotalKWh = Math.Round(totalKWh, 4),

            PhaseA = new PhasePeriodKWh
            {
                OffPeak = Math.Round(phaseA.OffPeak, 4),
                MidPeak = Math.Round(phaseA.MidPeak, 4),
                Peak = Math.Round(phaseA.Peak, 4)
            },
            PhaseB = new PhasePeriodKWh
            {
                OffPeak = Math.Round(phaseB.OffPeak, 4),
                MidPeak = Math.Round(phaseB.MidPeak, 4),
                Peak = Math.Round(phaseB.Peak, 4)
            },
            PhaseC = new PhasePeriodKWh
            {
                OffPeak = Math.Round(phaseC.OffPeak, 4),
                MidPeak = Math.Round(phaseC.MidPeak, 4),
                Peak = Math.Round(phaseC.Peak, 4)
            },

            OffPeakRate = displayOffPeakRate,
            MidPeakRate = displayMidPeakRate,
            PeakRate = displayPeakRate,


            OffPeakCost = Math.Round(offPeakCost, 0),
            MidPeakCost = Math.Round(midPeakCost, 0),
            PeakCost = Math.Round(peakCost, 0),
            EnergyCost = Math.Round(energyCost, 0),

            MonthlyFixedFee = tariff.MonthlyFixedFee,
            MonthlyFixedFeeTotal = Math.Round(monthlyFeeTotal, 0),

            AveragePfA = Math.Round(avgPfA, 4),
            AveragePfB = Math.Round(avgPfB, 4),
            AveragePfC = Math.Round(avgPfC, 4),
            ReactivePenaltyThreshold = tariff.ReactivePenaltyThreshold,
            ReactivePenaltyMultiplier = tariff.ReactivePenaltyMultiplier,
            ReactivePenalty = Math.Round(reactivePenalty, 0),
            HasReactivePenalty = hasPenalty,

            SubTotal = Math.Round(subTotal, 0),
            TaxPercent = 9,
            TaxAmount = Math.Round(taxAmount, 0),
            GrandTotal = Math.Round(grandTotal, 0),

            PeriodDetails = periodDetails
        };

        // Save invoice if requested
        if (saveInvoice)
        {
            var invoice = new Invoice
            {
                CenterId = center.Id,
                TariffId = tariff.Id,
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                Days = result.Days,
                Months = result.Months,
                TotalKWh = result.TotalKWh,
                EnergyCost = result.EnergyCost,
                MonthlyFixedFeeTotal = result.MonthlyFixedFeeTotal,
                ReactivePenalty = result.ReactivePenalty,
                SubTotal = result.SubTotal,
                TaxAmount = result.TaxAmount,
                GrandTotal = result.GrandTotal,
                Status = "Final"
            };

            void AddDetail(string phase, string period, decimal kWh)
            {
                if (kWh <= 0) return;
                var rate = GetRate(phase, period);
                invoice.Details.Add(new InvoiceDetail
                {
                    Phase = phase,
                    PeriodType = period,
                    KWh = Math.Round(kWh, 4),
                    RatePerKWh = rate,
                    Amount = Math.Round(kWh * rate, 0)
                });
            }

            AddDetail("A", "OffPeak", phaseA.OffPeak);
            AddDetail("A", "MidPeak", phaseA.MidPeak);
            AddDetail("A", "Peak", phaseA.Peak);
            AddDetail("B", "OffPeak", phaseB.OffPeak);
            AddDetail("B", "MidPeak", phaseB.MidPeak);
            AddDetail("B", "Peak", phaseB.Peak);
            AddDetail("C", "OffPeak", phaseC.OffPeak);
            AddDetail("C", "MidPeak", phaseC.MidPeak);
            AddDetail("C", "Peak", phaseC.Peak);

            _db.Invoices.Add(invoice);
            await _db.SaveChangesAsync();
            _log.LogInformation("Invoice saved: {InvoiceId} for center {CenterId}", invoice.Id, center.Id);
        }

        return Ok(result);
    }

    [HttpGet("invoices/{centerId}")]
    public async Task<IActionResult> GetInvoices(Guid centerId)
    {
        var invoices = await _db.Invoices
            .Where(i => i.CenterId == centerId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new
            {
                i.Id,
                i.FromDate,
                i.ToDate,
                i.Days,
                i.Months,
                i.TotalKWh,
                i.EnergyCost,
                i.MonthlyFixedFeeTotal,
                i.ReactivePenalty,
                i.SubTotal,
                i.TaxAmount,
                i.GrandTotal,
                i.Status,
                i.CreatedAt
            })
            .ToListAsync();

        return Ok(invoices);
    }

    private async Task<List<(DateTime Date, int Hour, decimal DA, decimal DB, decimal DC)>> GetHourlyConsumptionPerPhase(
        string deviceId, DateTime fromUtc, DateTime toUtc)
    {
        var iranTz = TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");

        var consumptions = await _db.EnergyConsumptions
            .Where(c => c.DeviceId == deviceId && c.Timestamp >= fromUtc && c.Timestamp < toUtc)
            .OrderBy(c => c.Timestamp)
            .ToListAsync();

        var hourly = new Dictionary<(DateTime Date, int Hour), (decimal A, decimal B, decimal C)>();
        foreach (var c in consumptions)
        {
            if (c.DeltaA <= 0 && c.DeltaB <= 0 && c.DeltaC <= 0) continue;
            var iran = TimeZoneInfo.ConvertTimeFromUtc(c.Timestamp, iranTz);
            var key = (iran.Date, iran.Hour);
            if (!hourly.TryGetValue(key, out var cur))
                cur = (0, 0, 0);
            hourly[key] = (cur.A + c.DeltaA, cur.B + c.DeltaB, cur.C + c.DeltaC);
        }

        return hourly
            .OrderBy(kv => kv.Key.Date)
            .ThenBy(kv => kv.Key.Hour)
            .Select(kv => (kv.Key.Date, kv.Key.Hour, kv.Value.A, kv.Value.B, kv.Value.C))
            .ToList();
    }

    private static DateTime ParsePersianDate(string persianDate, PersianCalendar pc)
    {
        var parts = persianDate.Split('/');
        if (parts.Length != 3) throw new ArgumentException();
        return pc.ToDateTime(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), 0, 0, 0, 0);
    }

    private static int CountPersianMonths(DateTime from, DateTime to, PersianCalendar pc)
    {
        if (from > to) return 0;
        return (pc.GetYear(to) - pc.GetYear(from)) * 12 + (pc.GetMonth(to) - pc.GetMonth(from));
    }

    private static string GetPeriodType(Tariff tariff, int persianMonth, int hour)
    {
        bool summer = persianMonth >= 4 && persianMonth <= 9;
        int mins = hour * 60;

        var (offS, offE) = summer
            ? (tariff.SummerOffPeakStart, tariff.SummerOffPeakEnd)
            : (tariff.WinterOffPeakStart, tariff.WinterOffPeakEnd);
        var (midS, midE) = summer
            ? (tariff.SummerMidPeakStart, tariff.SummerMidPeakEnd)
            : (tariff.WinterMidPeakStart, tariff.WinterMidPeakEnd);
        var (peakS, peakE) = summer
            ? (tariff.SummerPeakStart, tariff.SummerPeakEnd)
            : (tariff.WinterPeakStart, tariff.WinterPeakEnd);

        if (InRange(mins, TimeToMinutes(offS), TimeToMinutes(offE))) return "OffPeak";
        if (InRange(mins, TimeToMinutes(midS), TimeToMinutes(midE))) return "MidPeak";
        if (InRange(mins, TimeToMinutes(peakS), TimeToMinutes(peakE))) return "Peak";
        return "OffPeak";
    }

    private static bool InRange(int mins, int start, int end)
    {
        if (start <= end) return mins >= start && mins < end;
        return mins >= start || mins < end; // wraps around midnight
    }

    private static int TimeToMinutes(string time)
    {
        var p = time.Split(':');
        if (p.Length != 2 || !int.TryParse(p[0], out var h) || !int.TryParse(p[1], out var m))
            return 0;
        return h * 60 + m;
    }
}

public class BillingRequest
{
    [Required(ErrorMessage = "شناسه مرکز الزامی است")]
    public Guid CenterId { get; set; }
    [Required(ErrorMessage = "تاریخ شروع الزامی است")]
    public string FromDate { get; set; } = "";
    [Required(ErrorMessage = "تاریخ پایان الزامی است")]
    public string ToDate { get; set; } = "";
    public Guid? TariffId { get; set; }
}
