namespace EnergyMonitor.Data;

public class Invoice
{
    public int Id { get; set; }
    public Guid CenterId { get; set; }
    public Center? Center { get; set; }
    public Guid TariffId { get; set; }
    public Tariff? Tariff { get; set; }
    public string FromDate { get; set; } = "";
    public string ToDate { get; set; } = "";
    public int Days { get; set; }
    public int Months { get; set; }

    public decimal TotalKWh { get; set; }
    public decimal EnergyCost { get; set; }
    public decimal MonthlyFixedFeeTotal { get; set; }
    public decimal ReactivePenalty { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }

    public string Status { get; set; } = "Draft";
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<InvoiceDetail> Details { get; set; } = new List<InvoiceDetail>();
}
