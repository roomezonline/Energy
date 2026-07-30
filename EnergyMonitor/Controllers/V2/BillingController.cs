using System.Globalization;
using System.Text;
using EnergyMonitor.Application.Interfaces;
using EnergyMonitor.Domain.Interfaces;
using EnergyMonitor.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnergyMonitor.Controllers.V2;

[Authorize(Roles = "SuperAdmin,RegionalAdmin,BillingOperator")]
[ApiController]
[Route("api/v2/billing")]
public class BillingController : ControllerBase
{
    private readonly IBillingService _billing;
    private readonly IInvoiceService _invoices;
    private readonly IInvoiceRepository _invoiceRepo;
    private readonly ICenterRepository _centerRepo;
    private readonly IDeviceRepository _deviceRepo;
    private readonly IEnergySnapshotReader _snapshotReader;
    private readonly IPdfReportService _pdf;
    private readonly ICurrentUserService _currentUser;

    public BillingController(
        IBillingService billing,
        IInvoiceService invoices,
        IInvoiceRepository invoiceRepo,
        ICenterRepository centerRepo,
        IDeviceRepository deviceRepo,
        IEnergySnapshotReader snapshotReader,
        IPdfReportService pdf,
        ICurrentUserService currentUser)
    {
        _billing = billing;
        _invoices = invoices;
        _invoiceRepo = invoiceRepo;
        _centerRepo = centerRepo;
        _deviceRepo = deviceRepo;
        _snapshotReader = snapshotReader;
        _pdf = pdf;
        _currentUser = currentUser;
    }

    [HttpPost("calculate")]
    public async Task<IActionResult> Calculate([FromBody] BillingCalculationRequest request, CancellationToken ct)
    {
        if (request.CenterId == Guid.Empty)
            return BadRequest(new { error = "شناسه مرکز را وارد کنید" });
        if (!_currentUser.CanAccessCenter(request.CenterId)) return Forbid();
        if (string.IsNullOrEmpty(request.FromDate) || string.IsNullOrEmpty(request.ToDate))
            return BadRequest(new { error = "بازه تاریخ را وارد کنید" });

        var result = await _billing.CalculateAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("save-invoice")]
    public async Task<IActionResult> SaveInvoice([FromBody] SaveInvoiceRequest request, CancellationToken ct)
    {
        if (request.CenterId == Guid.Empty)
            return BadRequest(new { error = "شناسه مرکز را وارد کنید" });
        if (!_currentUser.CanAccessCenter(request.CenterId)) return Forbid();

        var result = await _invoices.SaveInvoiceAsync(request, ct);
        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Ok(new { result.Invoice?.Id, result.IsDuplicate });
    }

    [HttpGet("invoices/{centerId:guid}")]
    public async Task<IActionResult> GetInvoices(Guid centerId, CancellationToken ct)
    {
        if (!_currentUser.CanAccessCenter(centerId)) return Forbid();
        var invoices = await _invoiceRepo.GetByCenterAsync(centerId, ct);
        var result = invoices.Select(i => new
        {
            i.Id,
            i.FromDate,
            i.ToDate,
            i.Days,
            i.Months,
            TotalKWh = (double)i.TotalKWh,
            EnergyCost = (double)i.EnergyCost,
            MonthlyFixedFeeTotal = (double)i.MonthlyFixedFeeTotal,
            ReactivePenalty = (double)i.ReactivePenalty,
            SubTotal = (double)i.SubTotal,
            TaxAmount = (double)i.TaxAmount,
            GrandTotal = (double)i.GrandTotal,
            Status = i.Status.ToString(),
            i.CreatedAt
        });
        return Ok(result);
    }

    [HttpPost("export-csv")]
    public async Task<IActionResult> ExportCsv([FromBody] BillingCalculationRequest request, CancellationToken ct)
    {
        if (request.CenterId == Guid.Empty)
            return BadRequest(new { error = "شناسه مرکز را وارد کنید" });
        if (!_currentUser.CanAccessCenter(request.CenterId)) return Forbid();
        if (string.IsNullOrEmpty(request.FromDate) || string.IsNullOrEmpty(request.ToDate))
            return BadRequest(new { error = "بازه تاریخ را وارد کنید" });

        var result = await _billing.CalculateAsync(request, ct);
        var sb = new StringBuilder();
        sb.AppendLine("شرح,مقدار");
        sb.AppendLine($"مرکز,{result.CenterName}");
        sb.AppendLine($"تعرفه,{result.TariffName}");
        sb.AppendLine($"از تاریخ,{result.FromDate}");
        sb.AppendLine($"تا تاریخ,{result.ToDate}");
        sb.AppendLine($"تعداد روز,{result.Days}");
        sb.AppendLine($"تعداد ماه,{result.Months}");
        sb.AppendLine($"");
        sb.AppendLine("مصرف بر اساس دوره زمانی");
        sb.AppendLine("دوره,مصرف (kWh),نرخ (ریال),هزینه (ریال)");
        sb.AppendLine($"کم‌باری,{result.OffPeakKWh},{result.OffPeakRate},{result.OffPeakCost}");
        sb.AppendLine($"میان‌باری,{result.MidPeakKWh},{result.MidPeakRate},{result.MidPeakCost}");
        sb.AppendLine($"اوج‌باری,{result.PeakKWh},{result.PeakRate},{result.PeakCost}");
        sb.AppendLine($"جمع کل,{result.TotalKWh},,{result.EnergyCost}");
        sb.AppendLine($"");
        sb.AppendLine("صورتحساب نهایی");
        sb.AppendLine($"هزینه انرژی,{result.EnergyCost}");
        sb.AppendLine($"آبونمان,{result.MonthlyFixedFeeTotal}");
        if (result.DemandCost > 0)
            sb.AppendLine($"هزینه دیماند (حداکثر {result.MaxDemandKW} kW),{result.DemandCost}");
        if (result.ReactivePenalty > 0)
            sb.AppendLine($"جریمه توان راکتیو,{result.ReactivePenalty}");
        if (result.ReactiveBonus > 0)
            sb.AppendLine($"پاداش توان راکتیو,{result.ReactiveBonus}");
        sb.AppendLine($"جمع قبل از مالیات,{result.SubTotal}");
        sb.AppendLine($"مالیات ({result.TaxPercent}%),{result.TaxAmount}");
        sb.AppendLine($"قابل پرداخت,{result.GrandTotal}");
        sb.AppendLine($"");
        sb.AppendLine("جزئیات روزانه");
        sb.AppendLine("تاریخ,کم‌باری (kWh),میان‌باری (kWh),اوج‌باری (kWh),کل (kWh)");
        foreach (var day in result.PeriodDetails)
            sb.AppendLine($"{day.PersianDate},{day.OffPeakKWh},{day.MidPeakKWh},{day.PeakKWh},{day.TotalKWh}");

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv; charset=utf-8",
            $"billing_{request.CenterId:N}_{request.FromDate}_{request.ToDate}.csv");
    }

    [HttpPost("forecast")]
    public async Task<IActionResult> Forecast([FromBody] ForecastRequest request, CancellationToken ct)
    {
        if (request.CenterId == Guid.Empty)
            return BadRequest(new { error = "شناسه مرکز را وارد کنید" });

        var center = await _centerRepo.GetByIdAsync(request.CenterId, ct);
        if (center is null)
            return NotFound(new { error = "مرکز یافت نشد" });

        var device = (await _deviceRepo.GetByCenterAsync(center.Id, ct))
            .FirstOrDefault(d => d.IsActive);
        if (device is null)
            return NotFound(new { error = "مرکز فاقد دستگاه فعال است" });

        var iranTz = TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");
        var pc = new PersianCalendar();
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, iranTz);
        var lookbackDays = request.LookbackDays > 0 ? request.LookbackDays : 30;

        // Get recent consumption history
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(now.AddDays(-lookbackDays), iranTz);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(now, iranTz);

        var snaps = await _snapshotReader.GetRangeAsync(device.DeviceId, fromUtc, toUtc, ct);
        var totalKWh = snaps.Sum(s => Math.Max(0, s.DeltaA) + Math.Max(0, s.DeltaB) + Math.Max(0, s.DeltaC));

        var dailyAvg = lookbackDays > 0 ? totalKWh / lookbackDays : 0;
        var monthlyEstimate = dailyAvg * 30;
        var costEstimate = monthlyEstimate * (decimal)(center.TariffId.HasValue ? 1500 : 1200);

        return Ok(new
        {
            CenterId = request.CenterId,
            CenterName = center.Name,
            AverageDailyKWh = Math.Round(dailyAvg, 2),
            EstimatedMonthlyKWh = Math.Round(monthlyEstimate, 2),
            EstimatedMonthlyCost = Math.Round(costEstimate, 0),
            LookbackDays = lookbackDays,
            BasedOnDays = Math.Min(lookbackDays, (int)(toUtc - fromUtc).TotalDays),
            Currency = "IRR",
            Note = "تخمین بر اساس میانگین مصرف روزانه در دوره گذشته"
        });
    }

    [HttpPost("export-pdf")]
    public async Task<IActionResult> ExportPdf([FromBody] BillingCalculationRequest request, CancellationToken ct)
    {
        if (request.CenterId == Guid.Empty)
            return BadRequest(new { error = "شناسه مرکز را وارد کنید" });
        if (!_currentUser.CanAccessCenter(request.CenterId)) return Forbid();
        if (string.IsNullOrEmpty(request.FromDate) || string.IsNullOrEmpty(request.ToDate))
            return BadRequest(new { error = "بازه تاریخ را وارد کنید" });

        try
        {
            var pdf = await _pdf.GenerateBillingReportAsync(request, ct);
            return File(pdf, "application/pdf",
                $"billing_{request.CenterId:N}_{request.FromDate}_{request.ToDate}.pdf");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }
}

public class ForecastRequest
{
    public Guid CenterId { get; set; }
    public int LookbackDays { get; set; } = 30;
}
