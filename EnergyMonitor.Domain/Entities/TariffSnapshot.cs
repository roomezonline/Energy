namespace EnergyMonitor.Domain.Entities;

public class TariffSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public Guid OriginalTariffId { get; set; }
    public string TariffName { get; set; } = string.Empty;
    public string SummerOffPeakStart { get; set; } = string.Empty;
    public string SummerOffPeakEnd { get; set; } = string.Empty;
    public string SummerMidPeakStart { get; set; } = string.Empty;
    public string SummerMidPeakEnd { get; set; } = string.Empty;
    public string SummerPeakStart { get; set; } = string.Empty;
    public string SummerPeakEnd { get; set; } = string.Empty;
    public string WinterOffPeakStart { get; set; } = string.Empty;
    public string WinterOffPeakEnd { get; set; } = string.Empty;
    public string WinterMidPeakStart { get; set; } = string.Empty;
    public string WinterMidPeakEnd { get; set; } = string.Empty;
    public string WinterPeakStart { get; set; } = string.Empty;
    public string WinterPeakEnd { get; set; } = string.Empty;
    public decimal OffPeakRate { get; set; }
    public decimal MidPeakRate { get; set; }
    public decimal PeakRate { get; set; }
    public decimal MonthlyFixedFee { get; set; }
    public decimal ReactivePenaltyThreshold { get; set; }
    public decimal ReactivePenaltyMultiplier { get; set; }

    // === Formula derivation snapshot fields (nullable, only set for Automatic mode) ===
    public string? ConsumerTypeCode { get; set; }
    public string? ConsumerTypeName { get; set; }
    public int? Year { get; set; }
    public decimal? BaseEcaRate { get; set; }
    public decimal? EcaCoefficient { get; set; }
    public decimal? TouOffPeakMultiplier { get; set; }
    public decimal? TouMidPeakMultiplier { get; set; }
    public decimal? TouPeakMultiplier { get; set; }
    public decimal? EffectiveOffPeakRate { get; set; }
    public decimal? EffectiveMidPeakRate { get; set; }
    public decimal? EffectivePeakRate { get; set; }
    public decimal? PeakPenaltyAmount { get; set; }
    public decimal? OffPeakDiscountAmount { get; set; }
    public decimal? Article16Amount { get; set; }
    public decimal? DemandCost { get; set; }
    public decimal? TotalPenaltyBeforeTax { get; set; }

    // Override details stored as JSON
    public string? OverrideDetailsJson { get; set; }
}
