using System.ComponentModel.DataAnnotations;
using EnergyMonitor.Application.Interfaces;
using EnergyMonitor.Data;
using EnergyMonitor.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Controllers.V2;

[Authorize(Roles = "SuperAdmin,RegionalAdmin,BillingOperator")]
[ApiController]
[Route("api/v2/tariffs")]
public class TariffsController : ControllerBase
{
    private readonly AppDbContext _db;

    public TariffsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var list = await _db.Tariffs.OrderByDescending(t => t.CreatedAt).ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var tariff = await _db.Tariffs.FindAsync([id], ct);
        if (tariff is null) return NotFound();
        return Ok(tariff);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTariffRequest dto, CancellationToken ct)
    {
        var mode = dto.RateDerivationMode == "Automatic" ? RateDerivationMode.Automatic : RateDerivationMode.Manual;
        var tariff = new Tariff
        {
            Name = dto.Name,
            Description = dto.Description,
            RateDerivationMode = mode,
            ConsumerTypeCode = string.IsNullOrEmpty(dto.ConsumerTypeCode) ? null : dto.ConsumerTypeCode,
            Year = dto.Year > 0 ? dto.Year : null,
            VoltageLevelKV = (decimal?)dto.VoltageLevelKV,
            OffPeakRate = dto.OffPeakRate,
            MidPeakRate = dto.MidPeakRate,
            PeakRate = dto.PeakRate,
            MonthlyFixedFee = dto.MonthlyFixedFee,
            ReactivePenaltyThreshold = dto.ReactivePenaltyThreshold,
            ReactivePenaltyMultiplier = dto.ReactivePenaltyMultiplier,
            SummerOffPeakStart = dto.SummerOffPeakStart,
            SummerOffPeakEnd = dto.SummerOffPeakEnd,
            SummerMidPeakStart = dto.SummerMidPeakStart,
            SummerMidPeakEnd = dto.SummerMidPeakEnd,
            SummerPeakStart = dto.SummerPeakStart,
            SummerPeakEnd = dto.SummerPeakEnd,
            WinterOffPeakStart = dto.WinterOffPeakStart,
            WinterOffPeakEnd = dto.WinterOffPeakEnd,
            WinterMidPeakStart = dto.WinterMidPeakStart,
            WinterMidPeakEnd = dto.WinterMidPeakEnd,
            WinterPeakStart = dto.WinterPeakStart,
            WinterPeakEnd = dto.WinterPeakEnd,
            IsActive = dto.IsActive
        };
        _db.Tariffs.Add(tariff);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = tariff.Id }, tariff);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateTariffRequest dto, CancellationToken ct)
    {
        var tariff = await _db.Tariffs.FindAsync([id], ct);
        if (tariff is null) return NotFound();

        tariff.Name = dto.Name;
        tariff.Description = dto.Description;
        tariff.RateDerivationMode = dto.RateDerivationMode == "Automatic" ? RateDerivationMode.Automatic : RateDerivationMode.Manual;
        tariff.ConsumerTypeCode = string.IsNullOrEmpty(dto.ConsumerTypeCode) ? null : dto.ConsumerTypeCode;
        tariff.Year = dto.Year > 0 ? dto.Year : null;
        tariff.VoltageLevelKV = (decimal?)dto.VoltageLevelKV;
        if (tariff.RateDerivationMode != RateDerivationMode.Automatic)
        {
            tariff.OffPeakRate = dto.OffPeakRate;
            tariff.MidPeakRate = dto.MidPeakRate;
            tariff.PeakRate = dto.PeakRate;
        }
        tariff.MonthlyFixedFee = dto.MonthlyFixedFee;
        tariff.ReactivePenaltyThreshold = dto.ReactivePenaltyThreshold;
        tariff.ReactivePenaltyMultiplier = dto.ReactivePenaltyMultiplier;
        tariff.SummerOffPeakStart = dto.SummerOffPeakStart;
        tariff.SummerOffPeakEnd = dto.SummerOffPeakEnd;
        tariff.SummerMidPeakStart = dto.SummerMidPeakStart;
        tariff.SummerMidPeakEnd = dto.SummerMidPeakEnd;
        tariff.SummerPeakStart = dto.SummerPeakStart;
        tariff.SummerPeakEnd = dto.SummerPeakEnd;
        tariff.WinterOffPeakStart = dto.WinterOffPeakStart;
        tariff.WinterOffPeakEnd = dto.WinterOffPeakEnd;
        tariff.WinterMidPeakStart = dto.WinterMidPeakStart;
        tariff.WinterMidPeakEnd = dto.WinterMidPeakEnd;
        tariff.WinterPeakStart = dto.WinterPeakStart;
        tariff.WinterPeakEnd = dto.WinterPeakEnd;
        tariff.IsActive = dto.IsActive;
        await _db.SaveChangesAsync(ct);
        return Ok(tariff);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var tariff = await _db.Tariffs.FindAsync([id], ct);
        if (tariff is null) return NotFound();

        if (await _db.Centers.AnyAsync(c => c.TariffId == id, ct))
            return Conflict(new { error = "این تعرفه به یک مرکز اختصاص دارد. ابتدا تعرفه مرکز را تغییر دهید." });

        _db.Tariffs.Remove(tariff);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

public class CreateTariffRequest
{
    [Required] public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string RateDerivationMode { get; set; } = "Manual";
    public string? ConsumerTypeCode { get; set; }
    public int Year { get; set; }
    public decimal? VoltageLevelKV { get; set; }
    public decimal OffPeakRate { get; set; }
    public decimal MidPeakRate { get; set; }
    public decimal PeakRate { get; set; }
    public decimal MonthlyFixedFee { get; set; }
    public decimal ReactivePenaltyThreshold { get; set; } = 0.9m;
    public decimal ReactivePenaltyMultiplier { get; set; } = 2m;
    public string SummerOffPeakStart { get; set; } = "23:00";
    public string SummerOffPeakEnd { get; set; } = "07:00";
    public string SummerMidPeakStart { get; set; } = "07:00";
    public string SummerMidPeakEnd { get; set; } = "19:00";
    public string SummerPeakStart { get; set; } = "19:00";
    public string SummerPeakEnd { get; set; } = "23:00";
    public string WinterOffPeakStart { get; set; } = "23:00";
    public string WinterOffPeakEnd { get; set; } = "07:00";
    public string WinterMidPeakStart { get; set; } = "07:00";
    public string WinterMidPeakEnd { get; set; } = "19:00";
    public string WinterPeakStart { get; set; } = "19:00";
    public string WinterPeakEnd { get; set; } = "23:00";
    public bool IsActive { get; set; } = true;
}
