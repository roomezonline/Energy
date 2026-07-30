using System.ComponentModel.DataAnnotations;
using EnergyMonitor.Application.Interfaces;
using EnergyMonitor.Domain.Entities;
using EnergyMonitor.Domain.Enums;
using EnergyMonitor.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnergyMonitor.Controllers.V2;

[Authorize(Roles = "SuperAdmin")]
[ApiController]
[Route("api/v2/users")]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly ICenterRepository _centers;
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _hasher;

    public UsersController(IUserRepository users, ICenterRepository centers, IUnitOfWork uow, IPasswordHasher hasher)
    {
        _users = users;
        _centers = centers;
        _uow = uow;
        _hasher = hasher;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var list = await _users.GetAllAsync(ct);
        var result = list.Select(u => new UserListItem
        {
            Id = u.Id,
            Username = u.Username,
            FullName = u.FullName,
            Role = u.Role.ToString(),
            CenterId = u.CenterId,
            CenterIds = u.UserCenters?.Select(uc => uc.CenterId).ToList() ?? new(),
            RegionId = u.RegionId,
            IsActive = u.IsActive,
            LastLoginAt = u.LastLoginAt,
            CreatedAt = u.CreatedAt
        });
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(id, ct);
        if (user is null) return NotFound();
        return Ok(new UserListItem
        {
            Id = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role.ToString(),
            CenterId = user.CenterId,
            CenterIds = user.UserCenters?.Select(uc => uc.CenterId).ToList() ?? new(),
            RegionId = user.RegionId,
            IsActive = user.IsActive,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest dto, CancellationToken ct)
    {
        if (await _users.AnyAsync(x => x.Username == dto.Username, ct))
            return Conflict(new { error = "نام کاربری تکراری است" });

        var allCenterIds = (dto.CenterIds ?? new()).Where(id => id != Guid.Empty).Distinct().ToList();
        if (dto.CenterId.HasValue && dto.CenterId.Value != Guid.Empty && !allCenterIds.Contains(dto.CenterId.Value))
            allCenterIds.Insert(0, dto.CenterId.Value);

        foreach (var cid in allCenterIds)
        {
            if (!await _centers.AnyAsync(x => x.Id == cid, ct))
                return BadRequest(new { error = $"مرکز {cid} یافت نشد" });
        }

        if (!Enum.TryParse<UserRole>(dto.Role, true, out var role))
            return BadRequest(new { error = "نقش نامعتبر است" });

        var user = new User
        {
            Username = dto.Username,
            PasswordHash = _hasher.Hash(dto.Password),
            FullName = dto.FullName,
            Role = role,
            CenterId = allCenterIds.Count > 0 ? allCenterIds[0] : null,
            IsActive = dto.IsActive
        };
        user.UserCenters = allCenterIds.Select(cid => new UserCenter { CenterId = cid }).ToList();
        _users.Add(user);
        await _uow.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = user.Id }, new { user.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest dto, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(id, ct);
        if (user is null) return NotFound();

        var dup = await _users.FirstOrDefaultAsync(x => x.Username == dto.Username && x.Id != id, ct);
        if (dup is not null)
            return Conflict(new { error = "نام کاربری تکراری است" });

        var allCenterIds = (dto.CenterIds ?? new()).Where(id => id != Guid.Empty).Distinct().ToList();
        if (dto.CenterId.HasValue && dto.CenterId.Value != Guid.Empty && !allCenterIds.Contains(dto.CenterId.Value))
            allCenterIds.Insert(0, dto.CenterId.Value);

        foreach (var cid in allCenterIds)
        {
            if (!await _centers.AnyAsync(x => x.Id == cid, ct))
                return BadRequest(new { error = $"مرکز {cid} یافت نشد" });
        }

        if (!Enum.TryParse<UserRole>(dto.Role, true, out var role))
            return BadRequest(new { error = "نقش نامعتبر است" });

        user.Username = dto.Username;
        user.FullName = dto.FullName;
        user.Role = role;
        user.CenterId = allCenterIds.Count > 0 ? allCenterIds[0] : null;
        user.IsActive = dto.IsActive;

        user.UserCenters.Clear();
        foreach (var cid in allCenterIds)
            user.UserCenters.Add(new UserCenter { CenterId = cid });

        if (!string.IsNullOrEmpty(dto.Password))
            user.PasswordHash = _hasher.Hash(dto.Password);

        _users.Update(user);
        await _uow.SaveChangesAsync(ct);
        return Ok(new { user.Id });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(id, ct);
        if (user is null) return NotFound();
        _users.Remove(user);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }
}

public class UserListItem
{
    public Guid Id { get; set; }
    public string Username { get; set; } = "";
    public string? FullName { get; set; }
    public string Role { get; set; } = "";
    public Guid? CenterId { get; set; }
    public List<Guid> CenterIds { get; set; } = new();
    public Guid? RegionId { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateUserRequest
{
    [Required] public string Username { get; set; } = "";
    [Required] public string Password { get; set; } = "";
    public string? FullName { get; set; }
    [Required] public string Role { get; set; } = "Operator";
    public Guid? CenterId { get; set; }
    public List<Guid> CenterIds { get; set; } = new();
    public bool IsActive { get; set; } = true;
}

public class UpdateUserRequest
{
    [Required] public string Username { get; set; } = "";
    public string? Password { get; set; }
    public string? FullName { get; set; }
    [Required] public string Role { get; set; } = "Operator";
    public Guid? CenterId { get; set; }
    public List<Guid> CenterIds { get; set; } = new();
    public bool IsActive { get; set; } = true;
}
