using System.ComponentModel.DataAnnotations;
using EnergyMonitor.Domain.Entities;
using EnergyMonitor.Domain.Interfaces;
using EnergyMonitor.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnergyMonitor.Controllers.V2;

[Authorize(Roles = "SuperAdmin,RegionalAdmin,Admin,Operator,Viewer")]
[ApiController]
[Route("api/v2/devices")]
public class DevicesController : ControllerBase
{
    private readonly IDeviceRepository _devices;
    private readonly ICenterRepository _centers;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public DevicesController(IDeviceRepository devices, ICenterRepository centers, IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _devices = devices;
        _centers = centers;
        _uow = uow;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var list = await _devices.GetAllAsync(ct);
        return Ok(list);
    }

    [HttpGet("by-center/{centerId:guid}")]
    public async Task<IActionResult> GetByCenter(Guid centerId, CancellationToken ct)
    {
        if (!_currentUser.CanAccessCenter(centerId)) return Forbid();
        var list = await _devices.GetByCenterAsync(centerId, ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var device = await _devices.GetByIdAsync(id, ct);
        if (device is null) return NotFound();
        return Ok(device);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDeviceRequest dto, CancellationToken ct)
    {
        if (!_currentUser.CanAccessCenter(dto.CenterId)) return Forbid();
        if (await _devices.GetByDeviceIdAsync(dto.DeviceId, ct) is not null)
            return Conflict(new { error = "DeviceId تکراری است" });
        var center = await _centers.GetByIdAsync(dto.CenterId, ct);
        if (center is null)
            return BadRequest(new { error = "مرکز مشخص شده وجود ندارد" });

        var device = new Device
        {
            DeviceId = dto.DeviceId,
            DisplayName = dto.DisplayName,
            MacAddress = dto.MacAddress,
            CenterId = dto.CenterId,
            DeviceGroupId = dto.DeviceGroupId,
            IsActive = dto.IsActive,
            Location = dto.Location ?? $"{center.Name} - {center.Code}",
            PhaseCount = dto.PhaseCount,
            CreatedAt = DateTime.UtcNow
        };
        _devices.Add(device);
        await _uow.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = device.Id }, device);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateDeviceRequest dto, CancellationToken ct)
    {
        if (!_currentUser.CanAccessCenter(dto.CenterId)) return Forbid();
        var device = await _devices.GetByIdAsync(id, ct);
        if (device is null) return NotFound();

        var dup = await _devices.GetByDeviceIdAsync(dto.DeviceId, ct);
        if (dup is not null && dup.Id != id)
            return Conflict(new { error = "DeviceId تکراری است" });

        var center = await _centers.GetByIdAsync(dto.CenterId, ct);

        device.DeviceId = dto.DeviceId;
        device.DisplayName = dto.DisplayName;
        device.MacAddress = dto.MacAddress;
        device.CenterId = dto.CenterId;
        device.DeviceGroupId = dto.DeviceGroupId;
        device.IsActive = dto.IsActive;
        device.Location = dto.Location ?? (center?.Name is not null ? $"{center.Name} - {center.Code}" : device.Location);
        device.PhaseCount = dto.PhaseCount;
        _devices.Update(device);
        await _uow.SaveChangesAsync(ct);
        return Ok(device);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var device = await _devices.GetByIdAsync(id, ct);
        if (device is null) return NotFound();
        _devices.Remove(device);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }
}

public class CreateDeviceRequest
{
    [Required] public string DeviceId { get; set; } = "";
    [Required] public string DisplayName { get; set; } = "";
    public string? MacAddress { get; set; }
    [Required] public Guid CenterId { get; set; }
    public Guid? DeviceGroupId { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Location { get; set; }
    public int PhaseCount { get; set; } = 3;
}
