namespace EnergyMonitor.Application.Interfaces;

public class LoginResult
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? Error { get; set; }
    public Guid? UserId { get; set; }
    public string? Username { get; set; }
    public string? Role { get; set; }
    public Guid? CenterId { get; set; }
    public List<Guid> CenterIds { get; set; } = new();
}

public interface IAuthenticationService
{
    Task<LoginResult> LoginAsync(string username, string password, CancellationToken ct = default);
}
