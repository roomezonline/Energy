using System.ComponentModel.DataAnnotations;
using EnergyMonitor.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Controllers.V2;

[Authorize(Roles = "SuperAdmin,RegionalAdmin")]
[ApiController]
[Route("api/v2/provinces")]
public class ProvincesController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProvincesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? regionId, CancellationToken ct)
    {
        var query = _db.Provinces.AsNoTracking();
        if (regionId.HasValue)
            query = query.Where(x => x.RegionId == regionId.Value);
        var list = await query.OrderBy(x => x.Code).ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var entity = await _db.Provinces.FindAsync(new object[] { id }, ct);
        if (entity is null) return NotFound();
        return Ok(entity);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ProvinceDto dto, CancellationToken ct)
    {
        if (await _db.Provinces.AnyAsync(x => x.Code == dto.Code, ct))
            return Conflict(new { error = "کد استان تکراری است" });
        if (!await _db.Regions.AnyAsync(x => x.Id == dto.RegionId, ct))
            return BadRequest(new { error = "منطقه مورد نظر یافت نشد" });

        var entity = new Province
        {
            Name = dto.Name,
            Code = dto.Code,
            RegionId = dto.RegionId,
            IsActive = dto.IsActive
        };
        _db.Provinces.Add(entity);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = entity.Id }, entity);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ProvinceDto dto, CancellationToken ct)
    {
        var entity = await _db.Provinces.FindAsync(new object[] { id }, ct);
        if (entity is null) return NotFound();

        if (await _db.Provinces.AnyAsync(x => x.Code == dto.Code && x.Id != id, ct))
            return Conflict(new { error = "کد استان تکراری است" });
        if (!await _db.Regions.AnyAsync(x => x.Id == dto.RegionId, ct))
            return BadRequest(new { error = "منطقه مورد نظر یافت نشد" });

        entity.Name = dto.Name;
        entity.Code = dto.Code;
        entity.RegionId = dto.RegionId;
        entity.IsActive = dto.IsActive;
        await _db.SaveChangesAsync(ct);
        return Ok(entity);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var entity = await _db.Provinces.FindAsync(new object[] { id }, ct);
        if (entity is null) return NotFound();

        if (await _db.Cities.AnyAsync(x => x.ProvinceId == id, ct))
            return Conflict(new { error = "این استان دارای شهر است. ابتدا شهرها را حذف کنید." });

        _db.Provinces.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

public class ProvinceDto
{
    [Required] public string Name { get; set; } = "";
    [Required] public string Code { get; set; } = "";
    [Required] public Guid RegionId { get; set; }
    public bool IsActive { get; set; } = true;
}
