using System.Security.Claims;
using EnergyMonitor.Application.Interfaces;
using EnergyMonitor.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnergyMonitor.Controllers.V2;

[ApiController]
[Route("api/v2/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _auth;
    private readonly ITokenGenerator _tokenGen;
    private readonly IUserRepository _users;

    public AuthController(IAuthenticationService auth, ITokenGenerator tokenGen, IUserRepository users)
    {
        _auth = auth;
        _tokenGen = tokenGen;
        _users = users;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _auth.LoginAsync(request.Username, request.Password, ct);
        if (!result.Success)
            return Unauthorized(new { error = result.Error ?? "نام کاربری یا رمز عبور اشتباه است" });

        return Ok(new LoginResponse
        {
            Token = result.Token!,
            UserId = result.UserId!.Value,
            Username = result.Username!,
            FullName = result.FullName ?? "",
            Role = result.Role!,
            CenterId = result.CenterId,
            CenterIds = result.CenterIds
        });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var user = await _users.GetByIdAsync(Guid.Parse(userId), ct);
        if (user is null) return NotFound(new { error = "کاربر یافت نشد" });

        var centerIds = user.UserCenters?.Select(uc => uc.CenterId).ToList() ?? new();
        if (user.CenterId.HasValue && !centerIds.Contains(user.CenterId.Value))
            centerIds.Insert(0, user.CenterId.Value);

        return Ok(new
        {
            user.Id,
            user.Username,
            user.FullName,
            Role = user.Role.ToString(),
            user.CenterId,
            CenterIds = centerIds,
            user.IsActive,
            user.LastLoginAt
        });
    }
}

public class LoginRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public class LoginResponse
{
    public string Token { get; set; } = "";
    public Guid UserId { get; set; }
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Role { get; set; } = "";
    public Guid? CenterId { get; set; }
    public List<Guid> CenterIds { get; set; } = new();
}
