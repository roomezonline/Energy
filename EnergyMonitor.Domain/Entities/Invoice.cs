using EnergyMonitor.Domain.Enums;

namespace EnergyMonitor.Domain.Entities;

public class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid CenterId { get; set; }
    public Center? Center { get; set; }
    public Guid TariffId { get; set; }
    public Tariff? Tariff { get; set; }
    public Guid? IdempotencyKey { get; set; }

    public string FromDate { get; set; } = string.Empty;
    public string ToDate { get; set; } = string.Empty;
    public int Days { get; set; }
    public int Months { get; set; }

    public decimal TotalKWh { get; set; }
    public decimal EnergyCost { get; set; }
    public decimal MonthlyFixedFeeTotal { get; set; }
    public decimal ReactivePenalty { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }

    // New cost components (nullable for backward compat)
    public decimal? PeakPenalty { get; set; }
    public decimal? OffPeakDiscount { get; set; }
    public decimal? Article16Cost { get; set; }
    public decimal? DemandCost { get; set; }
    public decimal? TollAmount { get; set; }

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public string? Notes { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public TariffSnapshot? TariffSnapshot { get; set; }
    public ICollection<InvoiceDetail> Details { get; set; } = new List<InvoiceDetail>();
}
