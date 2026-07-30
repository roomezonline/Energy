using EnergyMonitor.Domain.Entities;

namespace EnergyMonitor.Application.Interfaces;

public class BillingOverrideItem
{
    public string FieldName { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal? OverrideValue { get; set; }
    public string? OverrideReason { get; set; }
}

public class SaveInvoiceRequest
{
    public Guid CenterId { get; set; }
    public Guid? TariffId { get; set; }
    public string FromDate { get; set; } = string.Empty;
    public string ToDate { get; set; } = string.Empty;
    public Guid? IdempotencyKey { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public List<BillingOverrideItem>? Overrides { get; set; }
}

public class SaveInvoiceResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public Invoice? Invoice { get; set; }
    public bool IsDuplicate { get; set; }
}

public interface IInvoiceService
{
    Task<SaveInvoiceResult> SaveInvoiceAsync(SaveInvoiceRequest request, CancellationToken ct = default);
}
