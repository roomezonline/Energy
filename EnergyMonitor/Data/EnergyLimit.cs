namespace EnergyMonitor.Data;

public class EnergyLimit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CenterId { get; set; }
    public string LimitType { get; set; } = string.Empty;
    public string PeriodType { get; set; } = string.Empty;
    public decimal MaxValue { get; set; }
    public decimal AlertThresholdPercent { get; set; } = 80;
    public bool IsActive { get; set; } = true;

    public Center? Center { get; set; }
}
