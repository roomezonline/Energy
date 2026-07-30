using System.ComponentModel.DataAnnotations;
using EnergyMonitor.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Controllers.V2;

[Authorize(Roles = "SuperAdmin,RegionalAdmin")]
[ApiController]
[Route("api/v2/cities")]
public class CitiesController : ControllerBase
{
    private readonly AppDbContext _db;
    public CitiesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? provinceId, CancellationToken ct)
    {
        var query = _db.Cities.AsNoTracking();
        if (provinceId.HasValue)
            query = query.Where(x => x.ProvinceId == provinceId.Value);
        var list = await query.OrderBy(x => x.Code).ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var entity = await _db.Cities.FindAsync(new object[] { id }, ct);
        if (entity is null) return NotFound();
        return Ok(entity);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CityDto dto, CancellationToken ct)
    {
        if (await _db.Cities.AnyAsync(x => x.Code == dto.Code, ct))
            return Conflict(new { error = "کد شهر تکراری است" });
        if (!await _db.Provinces.AnyAsync(x => x.Id == dto.ProvinceId, ct))
            return BadRequest(new { error = "استان مورد نظر یافت نشد" });

        var entity = new City
        {
            Name = dto.Name,
            Code = dto.Code,
            ProvinceId = dto.ProvinceId,
            IsActive = dto.IsActive
        };
        _db.Cities.Add(entity);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = entity.Id }, entity);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CityDto dto, CancellationToken ct)
    {
        var entity = await _db.Cities.FindAsync(new object[] { id }, ct);
        if (entity is null) return NotFound();

        if (await _db.Cities.AnyAsync(x => x.Code == dto.Code && x.Id != id, ct))
            return Conflict(new { error = "کد شهر تکراری است" });
        if (!await _db.Provinces.AnyAsync(x => x.Id == dto.ProvinceId, ct))
            return BadRequest(new { error = "استان مورد نظر یافت نشد" });

        entity.Name = dto.Name;
        entity.Code = dto.Code;
        entity.ProvinceId = dto.ProvinceId;
        entity.IsActive = dto.IsActive;
        await _db.SaveChangesAsync(ct);
        return Ok(entity);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var entity = await _db.Cities.FindAsync(new object[] { id }, ct);
        if (entity is null) return NotFound();

        if (await _db.Centers.AnyAsync(x => x.CityId == id, ct))
            return Conflict(new { error = "این شهر دارای مرکز است. ابتدا مراکز را حذف کنید." });

        _db.Cities.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

public class CityDto
{
    [Required] public string Name { get; set; } = "";
    [Required] public string Code { get; set; } = "";
    [Required] public Guid ProvinceId { get; set; }
    public bool IsActive { get; set; } = true;
}
