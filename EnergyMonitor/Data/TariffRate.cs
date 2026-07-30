using System.ComponentModel.DataAnnotations;

namespace EnergyMonitor.Data;

public class TariffRate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TariffId { get; set; }
    public Tariff Tariff { get; set; } = null!;

    [Required(ErrorMessage = "فاز الزامی است")]
    public string Phase { get; set; } = ""; // A, B, C

    [Required(ErrorMessage = "نوع بازه الزامی است")]
    public string PeriodType { get; set; } = ""; // OffPeak, MidPeak, Peak

    public decimal RatePerKWh { get; set; }
}
