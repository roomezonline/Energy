using EnergyMonitor.Data;
using EnergyMonitor.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Controllers.V2;

[Authorize(Roles = "SuperAdmin,RegionalAdmin,Admin,Operator,Viewer")]
[ApiController]
[Route("api/v2/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DashboardController(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet("{centerId:guid}")]
    public async Task<IActionResult> Get(Guid centerId, CancellationToken ct)
    {
        if (!_currentUser.CanAccessCenter(centerId)) return Forbid();
        var center = await _db.Centers.FindAsync(centerId);
        if (center is null) return NotFound();

        var device = await _db.Devices
            .Where(d => d.CenterId == centerId && d.IsActive)
            .OrderBy(d => d.DisplayName)
            .FirstOrDefaultAsync(ct);

        if (device is null)
            return Ok(new { center, devices = Array.Empty<object>(), alarms = Array.Empty<object>(), todayConsumption = new object[] { } });

        var latest = await _db.EnergySnapshots
            .Where(s => s.DeviceId == device.DeviceId)
            .OrderByDescending(s => s.Timestamp)
            .FirstOrDefaultAsync(ct);

        var iranTz = TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");
        var iranNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, iranTz);
        var todayUtc = TimeZoneInfo.ConvertTimeToUtc(iranNow.Date, iranTz);

        var todayKWh = await _db.EnergyConsumptions
            .Where(c => c.DeviceId == device.DeviceId && c.Timestamp >= todayUtc)
            .SumAsync(c => c.DeltaA + c.DeltaB + c.DeltaC, ct);

        var alarms = await _db.AlarmLogs
            .Where(a => a.CenterId == centerId)
            .OrderByDescending(a => a.OccurredAt)
            .Take(20)
            .ToListAsync(ct);

        return Ok(new
        {
            center,
            devices = new[] { device },
            latestSnapshot = latest,
            todayConsumption = new[] { new { totalKWh = Math.Round(todayKWh, 4) } },
            alarms
        });
    }

    [HttpGet("{centerId:guid}/summary")]
    public async Task<IActionResult> GetSummary(Guid centerId, CancellationToken ct)
    {
        if (!_currentUser.CanAccessCenter(centerId)) return Forbid();
        var center = await _db.Centers.FindAsync(centerId);
        if (center is null) return NotFound();

        var device = await _db.Devices
            .Where(d => d.CenterId == centerId && d.IsActive)
            .OrderBy(d => d.DisplayName)
            .FirstOrDefaultAsync(ct);

        if (device is null)
            return Ok(new { centerName = center.Name, totalDevices = 0 });

        var latest = await _db.EnergySnapshots
            .Where(s => s.DeviceId == device.DeviceId)
            .OrderByDescending(s => s.Timestamp)
            .FirstOrDefaultAsync(ct);

        var iranTz = TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");
        var iranNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, iranTz);
        var todayUtc = TimeZoneInfo.ConvertTimeToUtc(iranNow.Date, iranTz);
        var monthStart = TimeZoneInfo.ConvertTimeToUtc(new DateTime(iranNow.Year, iranNow.Month, 1, 0, 0, 0), iranTz);

        var todayKWh = await _db.EnergyConsumptions
            .Where(c => c.DeviceId == device.DeviceId && c.Timestamp >= todayUtc)
            .SumAsync(c => c.DeltaA + c.DeltaB + c.DeltaC, ct);

        var monthKWh = await _db.EnergyConsumptions
            .Where(c => c.DeviceId == device.DeviceId && c.Timestamp >= monthStart)
            .SumAsync(c => c.DeltaA + c.DeltaB + c.DeltaC, ct);

        return Ok(new
        {
            centerName = center.Name,
            totalDevices = await _db.Devices.CountAsync(d => d.CenterId == centerId, ct),
            activeDevices = await _db.Devices.CountAsync(d => d.CenterId == centerId && d.IsActive, ct),
            latestTimestamp = latest?.Timestamp,
            todayKWh = Math.Round(todayKWh, 2),
            monthKWh = Math.Round(monthKWh, 2),
            totalPower = latest?.TotalPower ?? 0
        });
    }
}
