namespace EnergyMonitor.Shared;

public class DashboardDataDto
{
    public CenterInfoDto? Center { get; set; }
    public DeviceInfoDto? SelectedDevice { get; set; }
    public List<DeviceInfoDto> Devices { get; set; } = new();
    public SnapshotDto? LatestSnapshot { get; set; }
    public List<SnapshotDto> ChartSnapshots { get; set; } = new();
    public List<AlarmItemDto> ActiveAlarms { get; set; } = new();
    public List<AlarmItemDto> RecentResolvedAlarms { get; set; } = new();
    public ConsumptionDto? Consumption { get; set; }
    public ConfigDto? DeviceConfig { get; set; }
    public bool Connected { get; set; }
    public string LastUpdateText { get; set; } = "";
}

public class CenterInfoDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string? Province { get; set; }
    public string? ImageFileName { get; set; }
    public Guid? TariffId { get; set; }
}

public class DeviceInfoDto
{
    public string DeviceId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? MacAddress { get; set; }
    public string? Location { get; set; }
    public Guid CenterId { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public bool PhaseAConnected { get; set; } = true;
    public bool PhaseBConnected { get; set; } = true;
    public bool PhaseCConnected { get; set; } = true;
    public int PhaseCount { get; set; } = 3;
}

public class SnapshotDto
{
    public DateTime Timestamp { get; set; }
    public decimal VoltageA { get; set; }
    public decimal CurrentA { get; set; }
    public decimal PowerA { get; set; }
    public decimal PfA { get; set; }
    public decimal EnergyA { get; set; }
    public decimal VoltageB { get; set; }
    public decimal CurrentB { get; set; }
    public decimal PowerB { get; set; }
    public decimal PfB { get; set; }
    public decimal EnergyB { get; set; }
    public decimal VoltageC { get; set; }
    public decimal CurrentC { get; set; }
    public decimal PowerC { get; set; }
    public decimal PfC { get; set; }
    public decimal EnergyC { get; set; }
    public decimal Frequency { get; set; }
    public decimal Temperature { get; set; }
    public decimal TotalPower { get; set; }
    public decimal TotalEnergyKWh { get; set; }
}

public class AlarmItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Phase { get; set; } = "";
    public string? DeviceId { get; set; }
    public decimal? Value { get; set; }
    public DateTime OccurredAt { get; set; }
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public class ConsumptionDto
{
    public decimal TodayKWh { get; set; }
    public decimal MonthKWh { get; set; }
    public decimal PeakCurrentA { get; set; }
    public decimal PeakCurrentB { get; set; }
    public decimal PeakCurrentC { get; set; }
    public decimal PeakPowerA { get; set; }
    public decimal PeakPowerB { get; set; }
    public decimal PeakPowerC { get; set; }
    public bool HasBackfill { get; set; }
    public string? LastBackfillTime { get; set; }
    public decimal LastBackfillKWh { get; set; }
}

public class ConfigDto
{
    public bool AlarmSoundEnabled { get; set; } = true;
}
