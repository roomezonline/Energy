using System.ComponentModel.DataAnnotations;
using EnergyMonitor.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Controllers;

[Authorize]
[ApiController]
[Route("api/news")]
public class NewsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public NewsController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _db.NewsArticles
            .OrderBy(n => n.SortOrder)
            .ThenByDescending(n => n.CreatedAt)
            .ToListAsync();
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var item = await _db.NewsArticles.FindAsync(id);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromForm] NewsArticleDto dto)
    {
        var article = new NewsArticle
        {
            Title = dto.Title,
            Summary = dto.Summary,
            FullText = dto.FullText,
            SortOrder = dto.SortOrder,
            IsActive = dto.IsActive
        };

        if (dto.Image != null)
        {
            if (dto.Image.Length > 5 * 1024 * 1024)
                return BadRequest(new { error = "حجم فایل باید کمتر از ۵ مگابایت باشد" });
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/gif", "image/webp" };
            if (!allowed.Contains(dto.Image.ContentType))
                return BadRequest(new { error = "فرمت فایل باید jpeg, png, gif, یا webp باشد" });

            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(dto.Image.FileName);
            var uploads = Path.Combine(_env.WebRootPath, "uploads", "news");
            Directory.CreateDirectory(uploads);
            await using var stream = new FileStream(Path.Combine(uploads, fileName), FileMode.Create);
            await dto.Image.CopyToAsync(stream);
            article.ImageFileName = "news/" + fileName;
        }

        _db.NewsArticles.Add(article);
        await _db.SaveChangesAsync();
        return Ok(article);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromForm] NewsArticleDto dto)
    {
        var article = await _db.NewsArticles.FindAsync(id);
        if (article == null) return NotFound();

        article.Title = dto.Title;
        article.Summary = dto.Summary;
        article.FullText = dto.FullText;
        article.SortOrder = dto.SortOrder;
        article.IsActive = dto.IsActive;
        article.UpdatedAt = DateTime.UtcNow;

        if (dto.Image != null)
        {
            if (dto.Image.Length > 5 * 1024 * 1024)
                return BadRequest(new { error = "حجم فایل باید کمتر از ۵ مگابایت باشد" });
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/gif", "image/webp" };
            if (!allowed.Contains(dto.Image.ContentType))
                return BadRequest(new { error = "فرمت فایل باید jpeg, png, gif, یا webp باشد" });

            if (!string.IsNullOrEmpty(article.ImageFileName))
            {
                var oldPath = Path.Combine(_env.WebRootPath, "uploads", article.ImageFileName);
                if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
            }
            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(dto.Image.FileName);
            var uploads = Path.Combine(_env.WebRootPath, "uploads", "news");
            Directory.CreateDirectory(uploads);
            await using var stream = new FileStream(Path.Combine(uploads, fileName), FileMode.Create);
            await dto.Image.CopyToAsync(stream);
            article.ImageFileName = "news/" + fileName;
        }

        await _db.SaveChangesAsync();
        return Ok(article);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var article = await _db.NewsArticles.FindAsync(id);
        if (article == null) return NotFound();

        if (!string.IsNullOrEmpty(article.ImageFileName))
        {
            var path = Path.Combine(_env.WebRootPath, "uploads", article.ImageFileName);
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }

        _db.NewsArticles.Remove(article);
        await _db.SaveChangesAsync();
        return Ok();
    }
}

public class NewsArticleDto
{
    [Required(ErrorMessage = "عنوان الزامی است")]
    public string Title { get; set; } = string.Empty;
    [Required(ErrorMessage = "خلاصه الزامی است")]
    public string Summary { get; set; } = string.Empty;
    public string? FullText { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public IFormFile? Image { get; set; }
}
