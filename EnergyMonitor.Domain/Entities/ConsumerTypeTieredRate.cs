namespace EnergyMonitor.Domain.Entities;

public class ConsumerTypeTieredRate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConsumerTypeYearlyConfigId { get; set; }
    public ConsumerTypeYearlyConfig? ConsumerTypeYearlyConfig { get; set; }
    public decimal TierFrom { get; set; }
    public decimal TierTo { get; set; }
    public decimal? Coefficient { get; set; }
    public decimal RatePerKwh { get; set; }
    public int SortOrder { get; set; }
}
