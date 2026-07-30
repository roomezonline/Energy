using EnergyMonitor.Application.Interfaces;
using EnergyMonitor.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace EnergyMonitor.Application.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly IUserRepository _userRepo;
    private readonly ITokenGenerator _jwt;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<AuthenticationService> _log;

    public AuthenticationService(IUserRepository userRepo, ITokenGenerator jwt, IPasswordHasher passwordHasher, ILogger<AuthenticationService> log)
    {
        _userRepo = userRepo;
        _jwt = jwt;
        _passwordHasher = passwordHasher;
        _log = log;
    }

    public async Task<LoginResult> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var user = await _userRepo.GetByUsernameAsync(username, ct);
        if (user is null || !user.IsActive)
        {
            _log.LogWarning("Login failed for user {Username}: not found or inactive", username);
            return new LoginResult { Success = false, Error = "نام کاربری یا رمز عبور اشتباه است" };
        }

        if (!_passwordHasher.Verify(password, user.PasswordHash))
        {
            _log.LogWarning("Login failed for user {Username}: wrong password", username);
            return new LoginResult { Success = false, Error = "نام کاربری یا رمز عبور اشتباه است" };
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _userRepo.SaveChangesAsync(ct);

        var token = _jwt.GenerateToken(user);

        _log.LogInformation("User {Username} logged in successfully", username);

        var centerIds = user.UserCenters?.Select(uc => uc.CenterId).ToList() ?? new();
        if (user.CenterId.HasValue && !centerIds.Contains(user.CenterId.Value))
            centerIds.Insert(0, user.CenterId.Value);

        return new LoginResult
        {
            Success = true,
            Token = token,
            UserId = user.Id,
            Username = user.Username,
            Role = user.Role.ToString(),
            CenterId = user.CenterId,
            CenterIds = centerIds
        };
    }
}
