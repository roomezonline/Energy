using EnergyMonitor.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Controllers;

[Authorize]
[ApiController]
[Route("api/config")]
public class ConfigController : ControllerBase
{
    private readonly AppDbContext _db;

    public ConfigController(AppDbContext db) => _db = db;

    [HttpGet("{deviceId}")]
    public async Task<IActionResult> Get(string deviceId)
    {
        var cfg = await _db.DeviceConfigs
            .FirstOrDefaultAsync(c => c.DeviceId == deviceId);

        if (cfg is null)
        {
            cfg = new DeviceConfig { DeviceId = deviceId };
            _db.DeviceConfigs.Add(cfg);
            await _db.SaveChangesAsync();
        }

        return Ok(cfg);
    }

    [HttpPut("{deviceId}")]
    public async Task<IActionResult> Update(string deviceId, [FromBody] DeviceConfig updated)
    {
        var cfg = await _db.DeviceConfigs
            .FirstOrDefaultAsync(c => c.DeviceId == deviceId);

        if (cfg is null)
        {
            cfg = new DeviceConfig { DeviceId = deviceId };
            _db.DeviceConfigs.Add(cfg);
        }

        cfg.OverVoltageThreshold = updated.OverVoltageThreshold;
        cfg.UnderVoltageThreshold = updated.UnderVoltageThreshold;
        cfg.OverCurrentThreshold = updated.OverCurrentThreshold;
        cfg.PhaseImbalanceThreshold = updated.PhaseImbalanceThreshold;
        cfg.LowPFThreshold = updated.LowPFThreshold;
        cfg.FreqMinThreshold = updated.FreqMinThreshold;
        cfg.FreqMaxThreshold = updated.FreqMaxThreshold;
        cfg.HighPowerThreshold = updated.HighPowerThreshold;
        cfg.TemperatureThreshold = updated.TemperatureThreshold;
        cfg.PublishIntervalMs = updated.PublishIntervalMs;
        cfg.IsSavingEnabled = updated.IsSavingEnabled;
        cfg.AlarmSoundEnabled = updated.AlarmSoundEnabled;
        cfg.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(cfg);
    }
}
