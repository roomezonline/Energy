namespace EnergyMonitor.Data;

public class City
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public Guid ProvinceId { get; set; }
    public Province? Province { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Center> Centers { get; set; } = new List<Center>();
}
