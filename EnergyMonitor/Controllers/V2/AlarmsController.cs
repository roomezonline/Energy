using EnergyMonitor.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OldDbContext = EnergyMonitor.Data.AppDbContext;

namespace EnergyMonitor.Controllers.V2;

[Authorize]
[ApiController]
[Route("api/v2/alarms")]
public class AlarmsController : ControllerBase
{
    private readonly OldDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AlarmsController(OldDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? centerId,
        [FromQuery] string? deviceId,
        [FromQuery] string? severity,
        [FromQuery] bool? resolved,
        CancellationToken ct)
    {
        var query = _db.AlarmLogs.AsQueryable();
        if (centerId.HasValue)
        {
            if (!_currentUser.CanAccessCenter(centerId.Value)) return Forbid();
            query = query.Where(a => a.CenterId == centerId.Value);
        }
        if (!string.IsNullOrEmpty(deviceId))
            query = query.Where(a => a.DeviceId == deviceId);
        if (!string.IsNullOrEmpty(severity))
            query = query.Where(a => a.Severity == severity);
        if (resolved.HasValue)
            query = query.Where(a => a.IsResolved == resolved.Value);

        var list = await query.OrderByDescending(a => a.OccurredAt).Take(100).ToListAsync(ct);
        return Ok(list);
    }

    [HttpPost("{id}/resolve")]
    public async Task<IActionResult> Resolve(int id, CancellationToken ct)
    {
        var alarm = await _db.AlarmLogs.FindAsync(new object[] { id }, ct);
        if (alarm is null) return NotFound();

        alarm.IsResolved = true;
        alarm.ResolvedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(alarm);
    }
}
