using EnergyMonitor.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Controllers;

[Authorize]
[ApiController]
[Route("api/limits")]
public class EnergyLimitsController : ControllerBase
{
    private readonly AppDbContext _db;

    public EnergyLimitsController(AppDbContext db) => _db = db;

    [HttpGet("{centerId}")]
    public async Task<IActionResult> GetAll(Guid centerId)
    {
        var limits = await _db.EnergyLimits
            .Where(l => l.CenterId == centerId)
            .OrderBy(l => l.LimitType)
            .ToListAsync();
        return Ok(limits);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] EnergyLimit limit)
    {
        if (limit.CenterId == Guid.Empty)
            return BadRequest(new { error = "شناسه مرکز الزامی است" });
        if (string.IsNullOrEmpty(limit.LimitType))
            return BadRequest(new { error = "نوع محدودیت الزامی است" });
        if (limit.MaxValue <= 0)
            return BadRequest(new { error = "حداکثر مقدار باید بزرگتر از صفر باشد" });

        limit.Id = Guid.NewGuid();
        _db.EnergyLimits.Add(limit);
        await _db.SaveChangesAsync();
        return Ok(limit);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] EnergyLimit update)
    {
        var limit = await _db.EnergyLimits.FindAsync(id);
        if (limit is null) return NotFound();

        limit.LimitType = update.LimitType;
        limit.PeriodType = update.PeriodType;
        limit.MaxValue = update.MaxValue;
        limit.AlertThresholdPercent = update.AlertThresholdPercent;
        limit.IsActive = update.IsActive;
        await _db.SaveChangesAsync();
        return Ok(limit);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var limit = await _db.EnergyLimits.FindAsync(id);
        if (limit is null) return NotFound();
        _db.EnergyLimits.Remove(limit);
        await _db.SaveChangesAsync();
        return Ok();
    }
}
