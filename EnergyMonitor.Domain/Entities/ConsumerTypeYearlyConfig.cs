namespace EnergyMonitor.Domain.Entities;

public class ConsumerTypeYearlyConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ConsumerTypeCode { get; set; } = string.Empty;
    public int Year { get; set; }

    // Tiered billing: monthly consumption pattern (الگوی مصرف ماهانه kWh)
    public decimal? ConsumptionPatternKWh { get; set; }

    // ECA coefficient
    public decimal EcaCoefficient { get; set; } = 1.0m;
    public decimal? MinPowerMW { get; set; }
    public decimal? MaxPowerMW { get; set; }

    // TOU multipliers — fully configurable per type/year
    public decimal TouOffPeakMultiplier { get; set; } = 0.5m;
    public decimal TouMidPeakMultiplier { get; set; } = 1.0m;
    public decimal TouPeakMultiplier { get; set; } = 2.0m;

    // Summer TOU time slots
    public string SummerOffPeakStart { get; set; } = "23:00";
    public string SummerOffPeakEnd { get; set; } = "06:00";
    public string SummerMidPeakStart { get; set; } = "06:00";
    public string SummerMidPeakEnd { get; set; } = "12:00";
    public string SummerPeakStart { get; set; } = "12:00";
    public string SummerPeakEnd { get; set; } = "23:00";

    // Winter TOU time slots
    public string WinterOffPeakStart { get; set; } = "23:00";
    public string WinterOffPeakEnd { get; set; } = "06:00";
    public string WinterMidPeakStart { get; set; } = "06:00";
    public string WinterMidPeakEnd { get; set; } = "17:00";
    public string WinterPeakStart { get; set; } = "17:00";
    public string WinterPeakEnd { get; set; } = "23:00";

    // Monthly fixed fee
    public decimal MonthlyFixedFee { get; set; } = 121279;

    // Reactive power
    public decimal ReactivePenaltyThreshold { get; set; } = 0.91m;
    public decimal ReactiveBonusThreshold { get; set; } = 0.95m;
    public decimal ReactivePenaltyMultiplier { get; set; } = 3m;

    // Demand charge
    public bool DemandChargeEnabled { get; set; }
    public decimal DemandRate { get; set; }

    // Article 16 — renewable energy mandate
    public bool Article16Enabled { get; set; }
    public decimal Article16Percent { get; set; } = 4m;
    public decimal Article16GreenEnergyRate { get; set; } = 63850m;

    // Peak penalty / off-peak discount coefficients
    public decimal PeakPenaltyCoefficient { get; set; } = 0.44m;
    public decimal PeakPenaltyNormalCoefficient { get; set; } = 0.146m;
    public decimal OffPeakDiscountCoefficient { get; set; } = 0.073m;
    public decimal OffPeakDiscountTwoRateCoefficient { get; set; } = 0.0292m;

    // Overload violation multiplier
    public decimal OverloadViolationMultiplier { get; set; } = 1.3m;

    // Tax & toll percents
    public decimal TaxPercent { get; set; } = 9m;
    public decimal TollPercent { get; set; } = 10m;

    // Voltage discount JSON (e.g., {"400kV": 0.9, "132kV": 0.94})
    public string? VoltageDiscountJson { get; set; }

    // Consumer type reference
    public ConsumerType? ConsumerType { get; set; }

    // Tiered rates for residential/commercial
    public ICollection<ConsumerTypeTieredRate> TieredRates { get; set; } = new List<ConsumerTypeTieredRate>();
}
