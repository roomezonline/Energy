using EnergyMonitor.Domain.Enums;

namespace EnergyMonitor.Domain.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public UserRole Role { get; set; } = UserRole.Operator;
    public Guid? CenterId { get; set; }
    public Center? Center { get; set; }
    public ICollection<UserCenter> UserCenters { get; set; } = new List<UserCenter>();
    public Guid? RegionId { get; set; }
    public Region? Region { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
