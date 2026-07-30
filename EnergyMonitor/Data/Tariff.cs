using System.ComponentModel.DataAnnotations;

namespace EnergyMonitor.Data;

public class Tariff
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // === Rate derivation mode ===
    public Domain.Enums.RateDerivationMode RateDerivationMode { get; set; } = Domain.Enums.RateDerivationMode.Manual;
    public string? ConsumerTypeCode { get; set; }
    public int? Year { get; set; }
    [Required(ErrorMessage = "نام الزامی است")]
    public string Name { get; set; } = "";

    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // --- Summer time slots (Persian months 1-6) ---
    [Required] public string SummerOffPeakStart { get; set; } = "23:00";
    [Required] public string SummerOffPeakEnd { get; set; } = "06:00";
    [Required] public string SummerMidPeakStart { get; set; } = "06:00";
    [Required] public string SummerMidPeakEnd { get; set; } = "12:00";
    [Required] public string SummerPeakStart { get; set; } = "12:00";
    [Required] public string SummerPeakEnd { get; set; } = "23:00";

    // --- Winter time slots (Persian months 7-12) ---
    [Required] public string WinterOffPeakStart { get; set; } = "23:00";
    [Required] public string WinterOffPeakEnd { get; set; } = "06:00";
    [Required] public string WinterMidPeakStart { get; set; } = "06:00";
    [Required] public string WinterMidPeakEnd { get; set; } = "17:00";
    [Required] public string WinterPeakStart { get; set; } = "17:00";
    [Required] public string WinterPeakEnd { get; set; } = "23:00";

    // Default rates (ریال/kWh) — fallback when no per-phase TariffRate exists
    public decimal OffPeakRate { get; set; }
    public decimal MidPeakRate { get; set; }
    public decimal PeakRate { get; set; }

    // Validity period (Persian date strings e.g. "1403/01/01")
    public string? EffectiveFrom { get; set; }
    public string? EffectiveTo { get; set; }

    // Fixed monthly fee (آبونمان)
    public decimal MonthlyFixedFee { get; set; } = 121279M;

    // Reactive power settings
    public decimal ReactivePenaltyThreshold { get; set; } = 0.9M;
    public decimal ReactiveBonusThreshold { get; set; } = 0.95M;
    public decimal ReactivePenaltyMultiplier { get; set; } = 3M;

    // Demand charge
    public decimal DemandRate { get; set; }
    public bool DemandChargeEnabled { get; set; }

    public ICollection<TariffRate> Rates { get; set; } = new List<TariffRate>();
}
