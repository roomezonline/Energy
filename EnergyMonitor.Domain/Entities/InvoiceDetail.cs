namespace EnergyMonitor.Domain.Entities;

public class InvoiceDetail
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public string Phase { get; set; } = string.Empty;
    public string PeriodType { get; set; } = string.Empty;
    public decimal KWh { get; set; }
    public decimal RatePerKWh { get; set; }
    public decimal Amount { get; set; }
    public decimal? Penalty { get; set; }
}
