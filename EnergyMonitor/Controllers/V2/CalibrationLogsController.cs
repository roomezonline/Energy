using EnergyMonitor.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewDbContext = EnergyMonitor.Infrastructure.Data.AppDbContext;

namespace EnergyMonitor.Controllers.V2;

[Authorize]
[ApiController]
[Route("api/v2/calibration-logs")]
public class CalibrationLogsController : ControllerBase
{
    private readonly NewDbContext _db;

    public CalibrationLogsController(NewDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? deviceId,
        CancellationToken ct)
    {
        var query = _db.CalibrationLogs.AsQueryable();
        if (!string.IsNullOrEmpty(deviceId))
            query = query.Where(c => c.DeviceId == deviceId);

        var list = await query.OrderByDescending(c => c.ChangedAt).Take(100).ToListAsync(ct);
        return Ok(list);
    }
}
