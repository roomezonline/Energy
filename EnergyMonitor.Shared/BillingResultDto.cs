namespace EnergyMonitor.Shared;

public class BillingResultDto
{
    public string CenterName { get; set; } = "";
    public string TariffName { get; set; } = "";
    public string FromDate { get; set; } = "";
    public string ToDate { get; set; } = "";
    public int Days { get; set; }
    public int Months { get; set; }

    // Total per period
    public decimal OffPeakKWh { get; set; }
    public decimal MidPeakKWh { get; set; }
    public decimal PeakKWh { get; set; }
    public decimal TotalKWh { get; set; }

    // Per-phase per period
    public PhasePeriodKWh PhaseA { get; set; } = new();
    public PhasePeriodKWh PhaseB { get; set; } = new();
    public PhasePeriodKWh PhaseC { get; set; } = new();

    // Rates
    public decimal OffPeakRate { get; set; }
    public decimal MidPeakRate { get; set; }
    public decimal PeakRate { get; set; }

    // Per-phase costs
    public decimal PhaseACost { get; set; }
    public decimal PhaseBCost { get; set; }
    public decimal PhaseCCost { get; set; }

    // Derivatives info
    public string? ConsumerTypeCode { get; set; }
    public string? ConsumerTypeName { get; set; }
    public int? Year { get; set; }
    public decimal? BaseEcaRate { get; set; }
    public decimal? EcaCoefficient { get; set; }
    public decimal? SupplyCostPerKwh { get; set; }
    public decimal? ConsumptionPatternKWh { get; set; }
    public decimal? EffectiveOffPeakRate { get; set; }
    public decimal? EffectiveMidPeakRate { get; set; }
    public decimal? EffectivePeakRate { get; set; }

    // Costs per period
    public decimal OffPeakCost { get; set; }
    public decimal MidPeakCost { get; set; }
    public decimal PeakCost { get; set; }
    public decimal EnergyCost { get; set; }

    // Monthly fee
    public decimal MonthlyFixedFee { get; set; }
    public decimal MonthlyFixedFeeTotal { get; set; }

    // Reactive penalty
    public decimal AveragePfA { get; set; }
    public decimal AveragePfB { get; set; }
    public decimal AveragePfC { get; set; }
    public decimal AveragePf { get; set; }
    public decimal ReactivePenaltyThreshold { get; set; }
    public decimal ReactivePenaltyMultiplier { get; set; }
    public decimal ReactivePenalty { get; set; }
    public bool HasReactivePenalty { get; set; }

    // TOU multipliers (Automatic mode)
    public decimal? TouOffPeakMultiplier { get; set; }
    public decimal? TouMidPeakMultiplier { get; set; }
    public decimal? TouPeakMultiplier { get; set; }

    // Tiered billing
    public bool HasTieredRates { get; set; }
    public decimal Tier1KWh { get; set; }
    public decimal Tier2KWh { get; set; }
    public decimal Tier3KWh { get; set; }
    public decimal Tier1Rate { get; set; }
    public decimal Tier2Rate { get; set; }
    public decimal Tier3Rate { get; set; }
    public decimal Tier1Cost { get; set; }
    public decimal Tier2Cost { get; set; }
    public decimal Tier3Cost { get; set; }

    // New cost components
    public decimal PeakPenalty { get; set; }
    public bool HasPeakPenalty { get; set; }
    public decimal OffPeakDiscount { get; set; }
    public bool HasOffPeakDiscount { get; set; }
    public decimal Article16Cost { get; set; }
    public bool HasArticle16 { get; set; }
    public decimal DemandCost { get; set; }
    public decimal MaxDemandKW { get; set; }
    public decimal DemandRate { get; set; }
    public decimal TollAmount { get; set; }
    public decimal ReactiveBonus { get; set; }
    public bool HasReactiveBonus { get; set; }

    public decimal SubTotal { get; set; }
    public decimal TaxPercent { get; set; } = 9;
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }

    public List<BillingPeriodDetail> PeriodDetails { get; set; } = new();

    // Editable items for manual override
    public List<BillingResultItem> EditableItems { get; set; } = new();
}

public class BillingResultItem
{
    public string FieldName { get; set; } = "";
    public string Label { get; set; } = "";
    public decimal AutoValue { get; set; }
    public decimal? OverrideValue { get; set; }
    public decimal FinalValue => OverrideValue ?? AutoValue;
    public bool IsOverridden => OverrideValue.HasValue;
    public string? OverrideReason { get; set; }
}

public class PhasePeriodKWh
{
    public decimal OffPeak { get; set; }
    public decimal MidPeak { get; set; }
    public decimal Peak { get; set; }
    public decimal Total => OffPeak + MidPeak + Peak;
}

public class BillingPeriodDetail
{
    public string PersianDate { get; set; } = "";
    public decimal OffPeakKWh { get; set; }
    public decimal MidPeakKWh { get; set; }
    public decimal PeakKWh { get; set; }
    public decimal TotalKWh { get; set; }
}
