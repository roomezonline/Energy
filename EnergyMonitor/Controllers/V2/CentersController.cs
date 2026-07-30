using System.ComponentModel.DataAnnotations;
using EnergyMonitor.Domain.Entities;
using EnergyMonitor.Domain.Enums;
using EnergyMonitor.Domain.Interfaces;
using EnergyMonitor.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnergyMonitor.Controllers.V2;

[Authorize(Roles = "SuperAdmin,RegionalAdmin,Admin")]
[ApiController]
[Route("api/v2/centers")]
public class CentersController : ControllerBase
{
    private readonly ICenterRepository _centers;
    private readonly IDeviceRepository _devices;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public CentersController(ICenterRepository centers, IDeviceRepository devices, IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _centers = centers;
        _devices = devices;
        _uow = uow;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        try
        {
            var list = await _centers.GetAllAsync(ct);
            if (_currentUser.Role == UserRole.RegionalAdmin && _currentUser.RegionId.HasValue)
                list = list.Where(x => x.City != null && x.City.Province != null && x.City.Province.RegionId == _currentUser.RegionId.Value).ToList();
            else if (_currentUser.Role == UserRole.Admin && _currentUser.CenterId.HasValue)
                list = list.Where(x => x.Id == _currentUser.CenterId.Value).ToList();
            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, details = ex.ToString() });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var center = await _centers.GetByIdAsync(id, ct);
        if (center is null) return NotFound();
        if (!CanAccessCenter(center)) return Forbid();
        return Ok(center);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCenterRequest dto, CancellationToken ct)
    {
        var existing = await _centers.GetByCodeAsync(dto.Code, ct);
        if (existing is not null)
            return Conflict(new { error = "کد مرکز تکراری است" });

        var center = new Center
        {
            Name = dto.Name,
            Code = dto.Code,
            CityId = dto.CityId,
            IsActive = dto.IsActive
        };
        _centers.Add(center);
        await _uow.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = center.Id }, center);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateCenterRequest dto, CancellationToken ct)
    {
        var center = await _centers.GetByIdAsync(id, ct);
        if (center is null) return NotFound();
        if (!CanAccessCenter(center)) return Forbid();

        var dup = await _centers.GetByCodeAsync(dto.Code, ct);
        if (dup is not null && dup.Id != id)
            return Conflict(new { error = "کد مرکز تکراری است" });

        center.Name = dto.Name;
        center.Code = dto.Code;
        center.CityId = dto.CityId;
        center.IsActive = dto.IsActive;
        _centers.Update(center);
        await _uow.SaveChangesAsync(ct);
        return Ok(center);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var center = await _centers.GetByIdAsync(id, ct);
        if (center is null) return NotFound();
        if (!CanAccessCenter(center)) return Forbid();
        _centers.Remove(center);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/devices")]
    public async Task<IActionResult> GetDevices(Guid id, CancellationToken ct)
    {
        var center = await _centers.GetByIdAsync(id, ct);
        if (center is null) return NotFound();
        if (!CanAccessCenter(center)) return Forbid();
        var list = await _devices.GetByCenterAsync(id, ct);
        return Ok(list.Select(d => new { d.DeviceId, d.DisplayName }));
    }

    private bool CanAccessCenter(Center center)
    {
        if (_currentUser.IsInRole(UserRole.SuperAdmin)) return true;
        if (_currentUser.Role == UserRole.RegionalAdmin && _currentUser.RegionId.HasValue)
            return center.City?.Province?.RegionId == _currentUser.RegionId;
        if (_currentUser.Role == UserRole.Admin && _currentUser.CenterId.HasValue)
            return center.Id == _currentUser.CenterId;
        return false;
    }
}

public class CreateCenterRequest
{
    [Required] public string Name { get; set; } = "";
    [Required] public string Code { get; set; } = "";
    public Guid? CityId { get; set; }
    public bool IsActive { get; set; } = true;
}
