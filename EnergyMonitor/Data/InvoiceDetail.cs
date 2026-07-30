namespace EnergyMonitor.Data;

public class InvoiceDetail
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public string Phase { get; set; } = "";
    public string PeriodType { get; set; } = "";
    public decimal KWh { get; set; }
    public decimal RatePerKWh { get; set; }
    public decimal Amount { get; set; }
    public decimal? Penalty { get; set; }
}
