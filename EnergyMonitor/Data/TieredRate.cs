namespace EnergyMonitor.Data;

public class TieredRate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TariffId { get; set; }
    public Tariff? Tariff { get; set; }
    public string PeriodType { get; set; } = string.Empty;
    public decimal TierFrom { get; set; }
    public decimal? TierTo { get; set; }
    public decimal RatePerKWh { get; set; }
    public int SortOrder { get; set; }
}
