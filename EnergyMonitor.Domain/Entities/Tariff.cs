using EnergyMonitor.Domain.Enums;

namespace EnergyMonitor.Domain.Entities;

public class Tariff
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string SummerOffPeakStart { get; set; } = "23:00";
    public string SummerOffPeakEnd { get; set; } = "06:00";
    public string SummerMidPeakStart { get; set; } = "06:00";
    public string SummerMidPeakEnd { get; set; } = "12:00";
    public string SummerPeakStart { get; set; } = "12:00";
    public string SummerPeakEnd { get; set; } = "23:00";

    public string WinterOffPeakStart { get; set; } = "23:00";
    public string WinterOffPeakEnd { get; set; } = "06:00";
    public string WinterMidPeakStart { get; set; } = "06:00";
    public string WinterMidPeakEnd { get; set; } = "17:00";
    public string WinterPeakStart { get; set; } = "17:00";
    public string WinterPeakEnd { get; set; } = "23:00";

    public decimal OffPeakRate { get; set; }
    public decimal MidPeakRate { get; set; }
    public decimal PeakRate { get; set; }

    public string? EffectiveFrom { get; set; }
    public string? EffectiveTo { get; set; }

    public decimal MonthlyFixedFee { get; set; } = 121279;
    public decimal ReactivePenaltyThreshold { get; set; } = 0.9m;
    public decimal ReactiveBonusThreshold { get; set; } = 0.95m;
    public decimal ReactivePenaltyMultiplier { get; set; } = 3;

    // Demand charge
    public decimal DemandRate { get; set; }
    public bool DemandChargeEnabled { get; set; }

    // === New fields for automatic rate derivation ===
    public RateDerivationMode RateDerivationMode { get; set; } = RateDerivationMode.Manual;
    public string? ConsumerTypeCode { get; set; }
    public int? Year { get; set; }

    public ICollection<TariffRate> Rates { get; set; } = new List<TariffRate>();
    public ICollection<TieredRate> TieredRates { get; set; } = new List<TieredRate>();
    public ICollection<TariffOverride> Overrides { get; set; } = new List<TariffOverride>();
}
