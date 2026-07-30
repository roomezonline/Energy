using EnergyMonitor.Domain.Enums;

namespace EnergyMonitor.Domain.Entities;

public class AlarmLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CenterId { get; set; }
    public Center? Center { get; set; }
    public string? DeviceId { get; set; }
    public AlarmSeverity Severity { get; set; } = AlarmSeverity.Info;
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? Phase { get; set; }
    public decimal? Value { get; set; }
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
