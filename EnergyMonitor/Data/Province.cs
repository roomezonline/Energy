namespace EnergyMonitor.Data;

public class Province
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public Guid RegionId { get; set; }
    public Region? Region { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<City> Cities { get; set; } = new List<City>();
}
