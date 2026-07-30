using System.ComponentModel.DataAnnotations;
using EnergyMonitor.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Controllers;

[Authorize(Roles = "SuperAdmin,RegionalAdmin,Admin,Operator,Viewer")]
[ApiController]
[Route("api/devices")]
public class DevicesController : ControllerBase
{
    private readonly AppDbContext _db;

    public DevicesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var devices = await _db.Devices
            .OrderByDescending(d => d.LastSeenAt)
            .Select(d => new
            {
                d.Id,
                d.DeviceId,
                d.DisplayName,
                d.MacAddress,
                d.CenterId,
                d.IsActive,
                d.Location,
                d.LastSeenAt,
                d.PhaseCount,
                d.CreatedAt
            })
            .ToListAsync();
        return Ok(devices);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var device = await _db.Devices.FindAsync(id);
        if (device is null) return NotFound();
        return Ok(device);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DeviceCreateDto dto)
    {
        if (await _db.Devices.AnyAsync(d => d.DeviceId == dto.DeviceId))
            return Conflict(new { error = "DeviceId already exists" });

        if (!await _db.Centers.AnyAsync(c => c.Id == dto.CenterId))
            return BadRequest(new { error = "???? ???? ??? ???? ?????. ????? ?? ???? ??? ????." });

        if (dto.DeviceGroupId.HasValue && !await _db.DeviceGroups.AnyAsync(g => g.Id == dto.DeviceGroupId.Value))
            return BadRequest(new { error = "گروه دستگاه یافت نشد" });

        var device = new DeviceInfo
        {
            DeviceId = dto.DeviceId,
            DisplayName = dto.DisplayName,
            MacAddress = dto.MacAddress,
            CenterId = dto.CenterId,
            DeviceGroupId = dto.DeviceGroupId,
            IsActive = dto.IsActive,
            Location = dto.Location,
            PhaseCount = dto.PhaseCount
        };

        _db.Devices.Add(device);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = device.Id }, device);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] DeviceCreateDto dto)
    {
        var device = await _db.Devices.FindAsync(id);
        if (device is null) return NotFound();

        if (await _db.Devices.AnyAsync(d => d.DeviceId == dto.DeviceId && d.Id != id))
            return Conflict(new { error = "DeviceId already exists" });

        if (dto.DeviceGroupId.HasValue && !await _db.DeviceGroups.AnyAsync(g => g.Id == dto.DeviceGroupId.Value))
            return BadRequest(new { error = "گروه دستگاه یافت نشد" });

        device.DeviceId = dto.DeviceId;
        device.DisplayName = dto.DisplayName;
        device.MacAddress = dto.MacAddress;
        device.CenterId = dto.CenterId;
        device.DeviceGroupId = dto.DeviceGroupId;
        device.IsActive = dto.IsActive;
        device.Location = dto.Location;
        device.PhaseCount = dto.PhaseCount;

        await _db.SaveChangesAsync();
        return Ok(device);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var device = await _db.Devices.FindAsync(id);
        if (device is null) return NotFound();

        if (await _db.EnergySnapshots.AnyAsync(s => s.DeviceId == device.DeviceId))
            return Conflict(new { error = "??? ?????? ????? ???? ???. ????? ??????? ?? ??? ????." });
        if (await _db.EnergyConsumptions.AnyAsync(c => c.DeviceId == device.DeviceId))
            return Conflict(new { error = "??? ?????? ????? ???? ???? ???. ????? ??????? ?? ??? ????." });

        _db.Devices.Remove(device);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public class DeviceCreateDto
{
    [Required(ErrorMessage = "DeviceId الزامی است")]
    public string DeviceId { get; set; } = string.Empty;
    [Required]
    public string DisplayName { get; set; } = string.Empty;
    public string? MacAddress { get; set; }
    [Required]
    public Guid CenterId { get; set; }
    public Guid? DeviceGroupId { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Location { get; set; }
    [Range(1, 3, ErrorMessage = "تعداد فاز باید ۱ یا ۳ باشد")]
    public int PhaseCount { get; set; } = 3;
}
