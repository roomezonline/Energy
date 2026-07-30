using EnergyMonitor.Application.Interfaces;
using EnergyMonitor.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnergyMonitor.Controllers.V2;

[Authorize]
[ApiController]
[Route("api/v2/consumption")]
public class ConsumptionController : ControllerBase
{
    private readonly IConsumptionService _consumption;
    private readonly IDeviceRepository _devices;

    public ConsumptionController(IConsumptionService consumption, IDeviceRepository devices)
    {
        _consumption = consumption;
        _devices = devices;
    }

    [HttpGet("daily/{deviceId}")]
    public async Task<IActionResult> GetDaily(
        string deviceId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var fromUtc = from ?? DateTime.UtcNow.AddDays(-7);
        var toUtc = to ?? DateTime.UtcNow;

        var result = await _consumption.GetDailyAsync(deviceId, fromUtc, toUtc, ct);
        return Ok(result);
    }

    [HttpGet("monthly/{deviceId}")]
    public async Task<IActionResult> GetMonthly(
        string deviceId,
        [FromQuery] int? fromYear, [FromQuery] int? fromMonth,
        [FromQuery] int? toYear, [FromQuery] int? toMonth,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var fy = fromYear ?? now.Year;
        var fm = fromMonth ?? 1;
        var ty = toYear ?? now.Year;
        var tm = toMonth ?? now.Month;

        var result = await _consumption.GetMonthlyAsync(deviceId, fy, fm, ty, tm, ct);
        return Ok(result);
    }
}
