namespace EnergyMonitor.Domain.Entities;

public class TariffRate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TariffId { get; set; }
    public Tariff? Tariff { get; set; }
    public string Phase { get; set; } = string.Empty;
    public string PeriodType { get; set; } = string.Empty;
    public decimal RatePerKWh { get; set; }
}
