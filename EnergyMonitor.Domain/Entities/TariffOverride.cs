namespace EnergyMonitor.Domain.Entities;

public class TariffOverride
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TariffId { get; set; }
    public Tariff? Tariff { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public decimal OverrideValue { get; set; }
    public bool IsPercentage { get; set; }
    public string? Reason { get; set; }
}
