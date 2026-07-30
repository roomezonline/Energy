using System.ComponentModel.DataAnnotations;
using EnergyMonitor.Data;
using EnergyMonitor.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Controllers;

[Authorize]
[ApiController]
[Route("api/tariffs")]
public class TariffsController : ControllerBase
{
    private readonly AppDbContext _db;

    public TariffsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tariffs = await _db.Tariffs
            .Include(t => t.Rates)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
        return Ok(tariffs);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var tariff = await _db.Tariffs.Include(t => t.Rates).FirstOrDefaultAsync(t => t.Id == id);
        if (tariff == null) return NotFound();
        return Ok(tariff);
    }

    private static bool IsValidTime(string time)
    {
        if (string.IsNullOrEmpty(time) || time.Length != 5) return false;
        var p = time.Split(':');
        return p.Length == 2 && int.TryParse(p[0], out var h) && int.TryParse(p[1], out var m)
            && h >= 0 && h <= 23 && m >= 0 && m <= 59;
    }

    private static bool ValidateTariffTimes(TariffDto dto, out string error)
    {
        var fields = new (string val, string name)[] {
            (dto.SummerOffPeakStart, "شروع کم‌باری تابستان"),
            (dto.SummerOffPeakEnd, "پایان کم‌باری تابستان"),
            (dto.SummerMidPeakStart, "شروع میان‌باری تابستان"),
            (dto.SummerMidPeakEnd, "پایان میان‌باری تابستان"),
            (dto.SummerPeakStart, "شروع اوج‌باری تابستان"),
            (dto.SummerPeakEnd, "پایان اوج‌باری تابستان"),
            (dto.WinterOffPeakStart, "شروع کم‌باری زمستان"),
            (dto.WinterOffPeakEnd, "پایان کم‌باری زمستان"),
            (dto.WinterMidPeakStart, "شروع میان‌باری زمستان"),
            (dto.WinterMidPeakEnd, "پایان میان‌باری زمستان"),
            (dto.WinterPeakStart, "شروع اوج‌باری زمستان"),
            (dto.WinterPeakEnd, "پایان اوج‌باری زمستان"),
        };
        foreach (var (val, name) in fields)
        {
            if (!IsValidTime(val)) { error = $"فرمت زمان نامعتبر: {name}"; return false; }
        }
        error = "";
        return true;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TariffDto dto)
    {
        if (!ValidateTariffTimes(dto, out var timeErr)) return BadRequest(new { error = timeErr });

        var mode = string.IsNullOrEmpty(dto.RateDerivationMode) || dto.RateDerivationMode == "Manual"
            ? RateDerivationMode.Manual : RateDerivationMode.Automatic;
        var tariff = new Tariff
        {
            Name = dto.Name,
            Description = dto.Description,
            IsActive = dto.IsActive,
            RateDerivationMode = mode,
            ConsumerTypeCode = string.IsNullOrEmpty(dto.ConsumerTypeCode) ? null : dto.ConsumerTypeCode,
            Year = dto.Year > 0 ? dto.Year : null,
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
            OffPeakRate = dto.OffPeakRate,
            MidPeakRate = dto.MidPeakRate,
            PeakRate = dto.PeakRate,
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            MonthlyFixedFee = dto.MonthlyFixedFee,
            ReactivePenaltyThreshold = dto.ReactivePenaltyThreshold,
            ReactiveBonusThreshold = dto.ReactiveBonusThreshold,
            ReactivePenaltyMultiplier = dto.ReactivePenaltyMultiplier
        };
        _db.Tariffs.Add(tariff);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = tariff.Id }, tariff);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] TariffDto dto)
    {
        if (!ValidateTariffTimes(dto, out var timeErr)) return BadRequest(new { error = timeErr });

        var tariff = await _db.Tariffs.FindAsync(id);
        if (tariff == null) return NotFound();

        tariff.Name = dto.Name;
        tariff.Description = dto.Description;
        tariff.IsActive = dto.IsActive;
        tariff.RateDerivationMode = string.IsNullOrEmpty(dto.RateDerivationMode) || dto.RateDerivationMode == "Manual"
            ? RateDerivationMode.Manual : RateDerivationMode.Automatic;
        tariff.ConsumerTypeCode = string.IsNullOrEmpty(dto.ConsumerTypeCode) ? null : dto.ConsumerTypeCode;
        tariff.Year = dto.Year > 0 ? dto.Year : null;
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
        if (tariff.RateDerivationMode != RateDerivationMode.Automatic)
        {
            tariff.OffPeakRate = dto.OffPeakRate;
            tariff.MidPeakRate = dto.MidPeakRate;
            tariff.PeakRate = dto.PeakRate;
        }
        tariff.EffectiveFrom = dto.EffectiveFrom;
        tariff.EffectiveTo = dto.EffectiveTo;
        tariff.MonthlyFixedFee = dto.MonthlyFixedFee;
        tariff.ReactivePenaltyThreshold = dto.ReactivePenaltyThreshold;
        tariff.ReactiveBonusThreshold = dto.ReactiveBonusThreshold;
        tariff.ReactivePenaltyMultiplier = dto.ReactivePenaltyMultiplier;

        await _db.SaveChangesAsync();
        return Ok(tariff);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var tariff = await _db.Tariffs.FindAsync(id);
        if (tariff == null) return NotFound();

        if (await _db.Centers.AnyAsync(c => c.TariffId == id))
            return Conflict(new { error = "این تعرفه به یک مرکز اختصاص دارد. ابتدا تعرفه مرکز را تغییر دهید." });

        _db.Tariffs.Remove(tariff);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public class TariffDto
{
    [Required(ErrorMessage = "نام تعرفه الزامی است")]
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public string RateDerivationMode { get; set; } = "Manual";
    public string? ConsumerTypeCode { get; set; }
    public int Year { get; set; }
    public string SummerOffPeakStart { get; set; } = "23:00";
    public string SummerOffPeakEnd { get; set; } = "06:00";
    public string SummerMidPeakStart { get; set; } = "06:00";
    public string SummerMidPeakEnd { get; set; } = "12:00";
    public string SummerPeakStart { get; set; } = "12:00";
    public string SummerPeakEnd { get; set; } = "23:00";
    public string WinterOffPeakStart { get; set; } = "23:00";
    public string WinterOffPeakEnd { get; set; } = "06:00";
    public string WinterMidPeakStart { get; set; } = "06:00";
    public string WinterMidPeakEnd { get; set; } = "17:00";
    public string WinterPeakStart { get; set; } = "17:00";
    public string WinterPeakEnd { get; set; } = "23:00";
    public decimal OffPeakRate { get; set; }
    public decimal MidPeakRate { get; set; }
    public decimal PeakRate { get; set; }
    public string? EffectiveFrom { get; set; }
    public string? EffectiveTo { get; set; }
    public decimal MonthlyFixedFee { get; set; } = 121279;
    public decimal ReactivePenaltyThreshold { get; set; } = 0.9m;
    public decimal ReactiveBonusThreshold { get; set; } = 0.95m;
    public decimal ReactivePenaltyMultiplier { get; set; } = 3;
}
