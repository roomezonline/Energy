using System.ComponentModel.DataAnnotations;
using EnergyMonitor.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Controllers.V2;

[Authorize(Roles = "SuperAdmin,RegionalAdmin")]
[ApiController]
[Route("api/v2/regions")]
public class RegionsController : ControllerBase
{
    private readonly AppDbContext _db;
    public RegionsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var list = await _db.Regions.AsNoTracking().OrderBy(x => x.Code).ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var entity = await _db.Regions.FindAsync(new object[] { id }, ct);
        if (entity is null) return NotFound();
        return Ok(entity);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] RegionDto dto, CancellationToken ct)
    {
        if (await _db.Regions.AnyAsync(x => x.Code == dto.Code, ct))
            return Conflict(new { error = "کد منطقه تکراری است" });

        var entity = new Region
        {
            Name = dto.Name,
            Code = dto.Code,
            IsActive = dto.IsActive
        };
        _db.Regions.Add(entity);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = entity.Id }, entity);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] RegionDto dto, CancellationToken ct)
    {
        var entity = await _db.Regions.FindAsync(new object[] { id }, ct);
        if (entity is null) return NotFound();

        if (await _db.Regions.AnyAsync(x => x.Code == dto.Code && x.Id != id, ct))
            return Conflict(new { error = "کد منطقه تکراری است" });

        entity.Name = dto.Name;
        entity.Code = dto.Code;
        entity.IsActive = dto.IsActive;
        await _db.SaveChangesAsync(ct);
        return Ok(entity);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var entity = await _db.Regions.FindAsync(new object[] { id }, ct);
        if (entity is null) return NotFound();

        if (await _db.Provinces.AnyAsync(x => x.RegionId == id, ct))
            return Conflict(new { error = "این منطقه دارای استان است. ابتدا استان‌ها را حذف کنید." });

        _db.Regions.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

public class RegionDto
{
    [Required] public string Name { get; set; } = "";
    [Required] public string Code { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
