namespace EnergyMonitor.Domain.Entities;

public class UserCenter
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid CenterId { get; set; }
    public Center Center { get; set; } = null!;
}
