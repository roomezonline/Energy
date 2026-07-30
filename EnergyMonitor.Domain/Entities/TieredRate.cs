namespace EnergyMonitor.Domain.Entities;

public class TieredRate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TariffId { get; set; }
    public Tariff? Tariff { get; set; }
    public string PeriodType { get; set; } = string.Empty; // OffPeak, MidPeak, Peak
    public decimal TierFrom { get; set; } // kWh start (inclusive)
    public decimal TierTo { get; set; }   // kWh end (exclusive), null means infinity
    public decimal RatePerKWh { get; set; }
    public int SortOrder { get; set; }
}
