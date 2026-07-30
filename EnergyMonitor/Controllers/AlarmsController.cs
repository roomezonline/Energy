using EnergyMonitor.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Controllers;

[Authorize]
[ApiController]
[Route("api/alarms")]
public class AlarmsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AlarmsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? severity, [FromQuery] string? phase, [FromQuery] bool? resolved)
    {
        var query = _db.AlarmLogs.AsQueryable();
        if (!string.IsNullOrEmpty(severity)) query = query.Where(a => a.Severity == severity);
        if (!string.IsNullOrEmpty(phase)) query = query.Where(a => a.Phase == phase);
        if (resolved.HasValue) query = query.Where(a => a.IsResolved == resolved.Value);

        var alarms = await query.OrderByDescending(a => a.OccurredAt).Take(200).ToListAsync();
        return Ok(alarms);
    }

    [HttpPost("{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id)
    {
        var alarm = await _db.AlarmLogs.FindAsync(id);
        if (alarm == null) return NotFound();
        alarm.IsResolved = true;
        alarm.ResolvedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(alarm);
    }

    [HttpPost("clear")]
    public async Task<IActionResult> ClearAll()
    {
        var active = await _db.AlarmLogs.Where(a => !a.IsResolved).ToListAsync();
        var now = DateTime.UtcNow;
        foreach (var a in active)
        {
            a.IsResolved = true;
            a.ResolvedAt = now;
        }
        await _db.SaveChangesAsync();
        return Ok();
    }
}
