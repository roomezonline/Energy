using System.ComponentModel.DataAnnotations;
using EnergyMonitor.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Controllers.V2;

[Authorize]
[ApiController]
[Route("api/v2/sliders")]
public class SlidersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public SlidersController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _db.SliderImages.Where(s => s.IsActive).OrderBy(s => s.SortOrder).ToListAsync();
        return Ok(items);
    }

    [HttpGet("admin")]
    public async Task<IActionResult> GetAdmin()
    {
        var items = await _db.SliderImages.OrderBy(s => s.SortOrder).ThenByDescending(s => s.CreatedAt).ToListAsync();
        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromForm] V2SliderDto dto)
    {
        var item = new SliderImage { Title = dto.Title, SortOrder = dto.SortOrder, IsActive = dto.IsActive };
        if (dto.Image != null)
            item.ImageUrl = await SaveFile(dto.Image);
        _db.SliderImages.Add(item);
        await _db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromForm] V2SliderDto dto)
    {
        var item = await _db.SliderImages.FindAsync(id);
        if (item == null) return NotFound();
        item.Title = dto.Title;
        item.SortOrder = dto.SortOrder;
        item.IsActive = dto.IsActive;
        if (dto.Image != null)
        {
            DeleteFile(item.ImageUrl);
            item.ImageUrl = await SaveFile(dto.Image);
        }
        await _db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var item = await _db.SliderImages.FindAsync(id);
        if (item == null) return NotFound();
        DeleteFile(item.ImageUrl);
        _db.SliderImages.Remove(item);
        await _db.SaveChangesAsync();
        return Ok();
    }

    private static readonly HashSet<string> _allowedImageTypes = new(StringComparer.OrdinalIgnoreCase)
        { "image/jpeg", "image/png", "image/gif", "image/webp" };
    private const int _maxImageSize = 5 * 1024 * 1024;

    private async Task<string> SaveFile(IFormFile file)
    {
        if (file.Length > _maxImageSize)
            throw new InvalidOperationException("حجم فایل باید کمتر از ۵ مگابایت باشد");
        if (!_allowedImageTypes.Contains(file.ContentType))
            throw new InvalidOperationException("فرمت فایل باید jpeg, png, gif, یا webp باشد");
        var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
        var uploads = Path.Combine(_env.WebRootPath, "uploads", "sliders");
        Directory.CreateDirectory(uploads);
        await using var stream = new FileStream(Path.Combine(uploads, fileName), FileMode.Create);
        await file.CopyToAsync(stream);
        return "/uploads/sliders/" + fileName;
    }

    private static void DeleteFile(string? imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl)) return;
        var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", imageUrl.TrimStart('/'));
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
    }
}

public class V2SliderDto
{
    public string? Title { get; set; }
    [Range(0, 999)] public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public IFormFile? Image { get; set; }
}
