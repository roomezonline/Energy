using System.ComponentModel.DataAnnotations;
using EnergyMonitor.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Controllers;

[Authorize(Roles = "SuperAdmin,RegionalAdmin,Admin")]
[ApiController]
[Route("api/centers")]
public class CentersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public CentersController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var centers = await _db.Centers.AsNoTracking().ToListAsync();
        return Ok(centers);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var center = await _db.Centers.FindAsync(id);
        if (center == null) return NotFound();
        return Ok(center);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CenterCreateDto dto)
    {
        if (await _db.Centers.AnyAsync(c => c.Code == dto.Code))
            return Conflict(new { error = "?? ???? ?????? ???" });

        var center = new Center
        {
            Name = dto.Name,
            Code = dto.Code,
            CityId = dto.CityId,
            IsActive = dto.IsActive
        };

        _db.Centers.Add(center);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = center.Id }, center);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CenterCreateDto dto)
    {
        var center = await _db.Centers.FindAsync(id);
        if (center == null) return NotFound();

        if (await _db.Centers.AnyAsync(c => c.Code == dto.Code && c.Id != id))
            return Conflict(new { error = "?? ???? ?????? ???" });

        center.Name = dto.Name;
        center.Code = dto.Code;
        center.CityId = dto.CityId;
        center.IsActive = dto.IsActive;

        await _db.SaveChangesAsync();
        return Ok(center);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var center = await _db.Centers.FindAsync(id);
        if (center == null) return NotFound();

        if (await _db.Devices.AnyAsync(d => d.CenterId == id))
            return Conflict(new { error = "??? ???? ????? ?????? ???? ???. ????? ????????? ?? ??? ????." });
        if (await _db.AlarmLogs.AnyAsync(a => a.CenterId == id))
            return Conflict(new { error = "??? ???? ????? ????? ???. ????? ???????? ?? ??? ????." });
        if (await _db.EnergyLimits.AnyAsync(l => l.CenterId == id))
            return Conflict(new { error = "??? ???? ????? ??????? ???? ???. ????? ?????????? ?? ??? ????." });

        _db.Centers.Remove(center);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static readonly HashSet<string> _allowedImageTypes = new(StringComparer.OrdinalIgnoreCase)
        { "image/jpeg", "image/png", "image/gif", "image/webp" };
    private const int _maxImageSize = 5 * 1024 * 1024;

    private static bool IsValidImage(IFormFile file, out string error)
    {
        if (file.Length > _maxImageSize) { error = "??? ???? ???? ???? ?? ? ??????? ????"; return false; }
        if (!_allowedImageTypes.Contains(file.ContentType)) { error = "???? ???? ???? jpeg, png, gif, ?? webp ????"; return false; }
        error = "";
        return true;
    }

    [HttpPut("{id:guid}/image")]
    public async Task<IActionResult> UploadImage(Guid id, [FromForm] CenterImageDto dto)
    {
        var center = await _db.Centers.FindAsync(id);
        if (center == null) return NotFound();

        if (dto.Image != null)
        {
            if (!IsValidImage(dto.Image, out var imgErr)) return BadRequest(new { error = imgErr });

            if (!string.IsNullOrEmpty(center.ImageFileName))
            {
                var oldPath = Path.Combine(_env.WebRootPath, "uploads", center.ImageFileName);
                if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
            }
            var ext = Path.GetExtension(dto.Image.FileName);
            var fileName = "center_" + id.ToString("N") + ext;
            var uploads = Path.Combine(_env.WebRootPath, "uploads", "centers");
            Directory.CreateDirectory(uploads);
            await using var stream = new FileStream(Path.Combine(uploads, fileName), FileMode.Create);
            await dto.Image.CopyToAsync(stream);
            center.ImageFileName = "centers/" + fileName;
            await _db.SaveChangesAsync();
        }

        return Ok(center);
    }

    [HttpDelete("{id:guid}/image")]
    public async Task<IActionResult> DeleteImage(Guid id)
    {
        var center = await _db.Centers.FindAsync(id);
        if (center == null) return NotFound();

        if (!string.IsNullOrEmpty(center.ImageFileName))
        {
            var path = Path.Combine(_env.WebRootPath, "uploads", center.ImageFileName);
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            center.ImageFileName = null;
            await _db.SaveChangesAsync();
        }
        return Ok();
    }
}

public class CenterCreateDto
{
    [Required(ErrorMessage = "نام مرکز الزامی است")]
    public string Name { get; set; } = "";
    [Required(ErrorMessage = "کد مرکز الزامی است")]
    public string Code { get; set; } = "";
    public Guid? CityId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CenterImageDto
{
    public IFormFile? Image { get; set; }
}