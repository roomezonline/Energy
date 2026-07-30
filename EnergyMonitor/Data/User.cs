namespace EnergyMonitor.Data;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "Operator";
    public Guid? CenterId { get; set; }
    public Center? Center { get; set; }
    public Guid? RegionId { get; set; }
    public Region? Region { get; set; }
}
