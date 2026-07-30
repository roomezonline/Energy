using EnergyMonitor.Domain.Enums;

namespace EnergyMonitor.Domain.Entities;

public class ConsumerType
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ConsumerCategory Category { get; set; } = ConsumerCategory.Industrial;
    public BillingModel BillingModel { get; set; } = BillingModel.TOU;
    public bool HasTOU { get; set; } = true;
    public bool HasTieredRates { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ConsumerTypeYearlyConfig> YearlyConfigs { get; set; } = new List<ConsumerTypeYearlyConfig>();
}
