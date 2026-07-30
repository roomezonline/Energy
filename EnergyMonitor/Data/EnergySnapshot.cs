namespace EnergyMonitor.Data;

public class EnergySnapshot
{
    public int Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string PersianTimestamp { get; set; } = "";
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
}
