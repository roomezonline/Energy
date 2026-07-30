using System.ComponentModel.DataAnnotations;
using EnergyMonitor.Data;
using EnergyMonitor.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Controllers.V2;

[Authorize(Roles = "SuperAdmin,RegionalAdmin,BillingOperator")]
[ApiController]
[Route("api/v2/yearly-base-rates")]
public class YearlyBaseRatesController : ControllerBase
{
    private readonly AppDbContext _db;

    public YearlyBaseRatesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var list = await _db.YearlyBaseRates.OrderByDescending(r => r.Year).ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("{year:int}")]
    public async Task<IActionResult> Get(int year, CancellationToken ct)
    {
        var item = await _db.YearlyBaseRates.FindAsync([year], ct);
        if (item is null) return NotFound();
        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] YearlyBaseRateRequest dto, CancellationToken ct)
    {
        if (await _db.YearlyBaseRates.AnyAsync(r => r.Year == dto.Year, ct))
            return Conflict(new { error = "این سال قبلاً ثبت شده است" });

        var entity = new YearlyBaseRate
        {
            Year = dto.Year,
            BaseRatePerKwh = dto.BaseRatePerKwh,
            SourceDocument = dto.SourceDocument,
            Description = dto.Description,
            IsActive = dto.IsActive
        };
        _db.YearlyBaseRates.Add(entity);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { year = entity.Year }, entity);
    }

    [HttpPut("{year:int}")]
    public async Task<IActionResult> Update(int year, [FromBody] YearlyBaseRateRequest dto, CancellationToken ct)
    {
        var entity = await _db.YearlyBaseRates.FindAsync([year], ct);
        if (entity is null) return NotFound();

        entity.BaseRatePerKwh = dto.BaseRatePerKwh;
        entity.SourceDocument = dto.SourceDocument;
        entity.Description = dto.Description;
        entity.IsActive = dto.IsActive;
        await _db.SaveChangesAsync(ct);
        return Ok(entity);
    }

    [HttpDelete("{year:int}")]
    public async Task<IActionResult> Delete(int year, CancellationToken ct)
    {
        var entity = await _db.YearlyBaseRates.FindAsync([year], ct);
        if (entity is null) return NotFound();
        _db.YearlyBaseRates.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

public class YearlyBaseRateRequest
{
    [Required] public int Year { get; set; }
    [Required] public decimal BaseRatePerKwh { get; set; }
    public string? SourceDocument { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
