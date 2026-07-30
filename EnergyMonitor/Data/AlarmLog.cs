namespace EnergyMonitor.Data;

public class AlarmLog
{
    public int Id { get; set; }
    public Guid CenterId { get; set; }
    public Center? Center { get; set; }
    public string? DeviceId { get; set; }
    public string Severity { get; set; } = "Info";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Phase { get; set; }
    public decimal? Value { get; set; }
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
