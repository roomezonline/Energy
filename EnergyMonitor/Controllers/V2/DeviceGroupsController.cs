using System.ComponentModel.DataAnnotations;
using EnergyMonitor.Data;
using EnergyMonitor.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Controllers.V2;

[Authorize(Roles = "SuperAdmin,RegionalAdmin,Admin")]
[ApiController]
[Route("api/v2/device-groups")]
public class DeviceGroupsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public DeviceGroupsController(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? centerId, CancellationToken ct)
    {
        var query = _db.DeviceGroups.AsNoTracking();
        if (centerId.HasValue)
        {
            if (!_currentUser.CanAccessCenter(centerId.Value)) return Forbid();
            query = query.Where(x => x.CenterId == centerId.Value);
        }
        var list = await query.OrderBy(x => x.Name).ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var entity = await _db.DeviceGroups.FindAsync(new object[] { id }, ct);
        if (entity is null) return NotFound();
        return Ok(entity);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DeviceGroupDto dto, CancellationToken ct)
    {
        if (!_currentUser.CanAccessCenter(dto.CenterId)) return Forbid();
        if (!await _db.Centers.AnyAsync(x => x.Id == dto.CenterId, ct))
            return BadRequest(new { error = "مرکز مورد نظر یافت نشد" });

        var entity = new DeviceGroup
        {
            Name = dto.Name,
            CenterId = dto.CenterId,
            IsActive = dto.IsActive
        };
        _db.DeviceGroups.Add(entity);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = entity.Id }, entity);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] DeviceGroupDto dto, CancellationToken ct)
    {
        var entity = await _db.DeviceGroups.FindAsync(new object[] { id }, ct);
        if (entity is null) return NotFound();

        if (!_currentUser.CanAccessCenter(dto.CenterId)) return Forbid();
        if (!await _db.Centers.AnyAsync(x => x.Id == dto.CenterId, ct))
            return BadRequest(new { error = "مرکز مورد نظر یافت نشد" });

        entity.Name = dto.Name;
        entity.CenterId = dto.CenterId;
        entity.IsActive = dto.IsActive;
        await _db.SaveChangesAsync(ct);
        return Ok(entity);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var entity = await _db.DeviceGroups.FindAsync(new object[] { id }, ct);
        if (entity is null) return NotFound();

        if (await _db.Devices.AnyAsync(x => x.DeviceGroupId == id, ct))
            return Conflict(new { error = "این گروه دارای دستگاه است. ابتدا دستگاه‌ها را حذف کنید." });

        _db.DeviceGroups.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

public class DeviceGroupDto
{
    [Required] public string Name { get; set; } = "";
    [Required] public Guid CenterId { get; set; }
    public bool IsActive { get; set; } = true;
}
