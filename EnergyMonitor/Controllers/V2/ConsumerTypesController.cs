using System.ComponentModel.DataAnnotations;
using EnergyMonitor.Data;
using EnergyMonitor.Domain.Entities;
using EnergyMonitor.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Controllers.V2;

[Authorize(Roles = "SuperAdmin,RegionalAdmin,BillingOperator")]
[ApiController]
[Route("api/v2/consumer-types")]
public class ConsumerTypesController : ControllerBase
{
    private readonly AppDbContext _db;

    public ConsumerTypesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var list = await _db.ConsumerTypes.OrderBy(c => c.SortOrder).ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("{code}")]
    public async Task<IActionResult> Get(string code, CancellationToken ct)
    {
        var item = await _db.ConsumerTypes.FindAsync([code], ct);
        if (item is null) return NotFound();
        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ConsumerTypeRequest dto, CancellationToken ct)
    {
        if (await _db.ConsumerTypes.AnyAsync(c => c.Code == dto.Code, ct))
            return Conflict(new { error = "کد تکراری است" });

        var entity = new ConsumerType
        {
            Code = dto.Code,
            Name = dto.Name,
            Description = dto.Description,
            Category = Enum.TryParse<ConsumerCategory>(dto.Category, out var cat) ? cat : ConsumerCategory.Other,
            BillingModel = Enum.TryParse<BillingModel>(dto.BillingModel, out var bm) ? bm : BillingModel.TOU,
            HasTOU = dto.HasTOU,
            HasTieredRates = dto.HasTieredRates,
            SortOrder = dto.SortOrder,
            IsActive = dto.IsActive
        };
        _db.ConsumerTypes.Add(entity);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { code = entity.Code }, entity);
    }

    [HttpPut("{code}")]
    public async Task<IActionResult> Update(string code, [FromBody] ConsumerTypeRequest dto, CancellationToken ct)
    {
        var entity = await _db.ConsumerTypes.FindAsync([code], ct);
        if (entity is null) return NotFound();

        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.Category = Enum.TryParse<ConsumerCategory>(dto.Category, out var cat) ? cat : ConsumerCategory.Other;
        entity.BillingModel = Enum.TryParse<BillingModel>(dto.BillingModel, out var bm) ? bm : BillingModel.TOU;
        entity.HasTOU = dto.HasTOU;
        entity.HasTieredRates = dto.HasTieredRates;
        entity.SortOrder = dto.SortOrder;
        entity.IsActive = dto.IsActive;
        await _db.SaveChangesAsync(ct);
        return Ok(entity);
    }

    [HttpDelete("{code}")]
    public async Task<IActionResult> Delete(string code, CancellationToken ct)
    {
        var entity = await _db.ConsumerTypes.FindAsync([code], ct);
        if (entity is null) return NotFound();
        _db.ConsumerTypes.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

public class ConsumerTypeRequest
{
    [Required] public string Code { get; set; } = "";
    [Required] public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Category { get; set; } = "Industrial";
    public string BillingModel { get; set; } = "TOU";
    public bool HasTOU { get; set; } = true;
    public bool HasTieredRates { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
