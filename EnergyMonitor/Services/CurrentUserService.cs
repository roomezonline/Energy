using System.Security.Claims;
using EnergyMonitor.Domain.Enums;

namespace EnergyMonitor.Services;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string Username { get; }
    UserRole Role { get; }
    Guid? CenterId { get; }
    List<Guid> CenterIds { get; }
    Guid? RegionId { get; }
    bool IsInRole(UserRole role);
    bool IsInAnyRole(params UserRole[] roles);
    bool CanAccessCenter(Guid centerId);
}

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _http;

    public CurrentUserService(IHttpContextAccessor http) => _http = http;

    private ClaimsPrincipal? User => _http.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var v = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return v is not null && Guid.TryParse(v, out var id) ? id : null;
        }
    }

    public string Username => User?.FindFirstValue(ClaimTypes.Name) ?? "";

    public UserRole Role
    {
        get
        {
            var v = User?.FindFirstValue(ClaimTypes.Role);
            return v is not null && Enum.TryParse<UserRole>(v, out var r) ? r : UserRole.Viewer;
        }
    }

    public Guid? CenterId
    {
        get
        {
            var v = User?.FindFirstValue("centerId");
            return v is not null && Guid.TryParse(v, out var id) ? id : null;
        }
    }

    public List<Guid> CenterIds
    {
        get
        {
            var v = User?.FindFirstValue("centerIds");
            if (string.IsNullOrEmpty(v)) return new();
            return v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => Guid.TryParse(s.Trim(), out var id) ? id : (Guid?)null)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .ToList();
        }
    }

    public Guid? RegionId
    {
        get
        {
            var v = User?.FindFirstValue("regionId");
            return v is not null && Guid.TryParse(v, out var id) ? id : null;
        }
    }

    public bool IsInRole(UserRole role) => Role == role;
    public bool IsInAnyRole(params UserRole[] roles) => roles.Contains(Role);
    public bool CanAccessCenter(Guid centerId)
    {
        if (Role == UserRole.SuperAdmin) return true;
        if (Role == UserRole.RegionalAdmin) return true;
        return CenterIds.Contains(centerId) || (CenterId.HasValue && CenterId.Value == centerId);
    }
}
