namespace EnergyMonitor.Domain.Entities;

public class YearlyBaseRate
{
    public int Year { get; set; }
    public decimal BaseRatePerKwh { get; set; }
    public decimal SupplyCostPerKwh { get; set; }
    public string Currency { get; set; } = "Rial";
    public string? SourceDocument { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
