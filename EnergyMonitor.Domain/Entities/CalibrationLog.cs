namespace EnergyMonitor.Domain.Entities;

public class CalibrationLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DeviceId { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public decimal OldValue { get; set; }
    public decimal NewValue { get; set; }
    public string? ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
