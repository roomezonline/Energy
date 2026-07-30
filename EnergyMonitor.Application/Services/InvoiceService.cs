using System.Text.Json;
using EnergyMonitor.Application.Interfaces;
using EnergyMonitor.Domain.Entities;
using EnergyMonitor.Domain.Enums;
using EnergyMonitor.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace EnergyMonitor.Application.Services;

public class InvoiceService : IInvoiceService
{
    private readonly IInvoiceRepository _invoiceRepo;
    private readonly ICenterRepository _centerRepo;
    private readonly IDeviceRepository _deviceRepo;
    private readonly ITariffRepository _tariffRepo;
    private readonly IBillingService _billing;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<InvoiceService> _log;

    public InvoiceService(
        IInvoiceRepository invoiceRepo,
        ICenterRepository centerRepo,
        IDeviceRepository deviceRepo,
        ITariffRepository tariffRepo,
        IBillingService billing,
        IUnitOfWork uow,
        ILogger<InvoiceService> log)
    {
        _invoiceRepo = invoiceRepo;
        _centerRepo = centerRepo;
        _deviceRepo = deviceRepo;
        _tariffRepo = tariffRepo;
        _billing = billing;
        _uow = uow;
        _log = log;
    }

    public async Task<SaveInvoiceResult> SaveInvoiceAsync(SaveInvoiceRequest request, CancellationToken ct = default)
    {
        if (request.IdempotencyKey.HasValue)
        {
            var existing = await _invoiceRepo.GetByIdempotencyKeyAsync(request.IdempotencyKey.Value, ct);
            if (existing != null)
                return new SaveInvoiceResult { Success = true, Invoice = existing, IsDuplicate = true };
        }

        var center = await _centerRepo.GetByIdAsync(request.CenterId, ct);
        if (center is null)
            return new SaveInvoiceResult { Success = false, Error = "مرکز یافت نشد" };

        Guid tariffId;
        if (request.TariffId.HasValue && request.TariffId.Value != Guid.Empty)
            tariffId = request.TariffId.Value;
        else if (center.TariffId.HasValue)
            tariffId = center.TariffId.Value;
        else
            return new SaveInvoiceResult { Success = false, Error = "مرکز فاقد تعرفه است" };

        var tariff = await _tariffRepo.GetByIdAsync(tariffId, ct);
        if (tariff is null)
            return new SaveInvoiceResult { Success = false, Error = "تعرفه یافت نشد" };

        var billingRequest = new BillingCalculationRequest
        {
            CenterId = request.CenterId,
            TariffId = request.TariffId,
            FromDate = request.FromDate,
            ToDate = request.ToDate
        };

        BillingCalculationResult billing;
        try
        {
            billing = await _billing.CalculateAsync(billingRequest, ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Billing calculation failed for center {CenterId}", center.Id);
            return new SaveInvoiceResult { Success = false, Error = "خطا در محاسبه صورت‌حساب" };
        }

        // Apply overrides from request
        if (request.Overrides is not null && request.Overrides.Count > 0)
        {
            ApplyOverrides(billing, request.Overrides);
        }

        await _uow.BeginTransactionAsync(ct);

        try
        {
            var invoiceNumber = await _invoiceRepo.GetNextInvoiceNumberAsync(ct);

            var invoice = new Invoice
            {
                InvoiceNumber = invoiceNumber,
                CenterId = center.Id,
                TariffId = tariff.Id,
                IdempotencyKey = request.IdempotencyKey,
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                Days = billing.Days,
                Months = billing.Months,
                TotalKWh = billing.TotalKWh,
                EnergyCost = billing.EnergyCost,
                MonthlyFixedFeeTotal = billing.MonthlyFixedFeeTotal,
                ReactivePenalty = billing.ReactivePenalty,
                PeakPenalty = billing.PeakPenalty > 0 ? billing.PeakPenalty : null,
                OffPeakDiscount = billing.OffPeakDiscount > 0 ? billing.OffPeakDiscount : null,
                Article16Cost = billing.Article16Cost > 0 ? billing.Article16Cost : null,
                DemandCost = billing.DemandCost > 0 ? billing.DemandCost : null,
                TollAmount = billing.TollAmount > 0 ? billing.TollAmount : null,
                SubTotal = billing.SubTotal,
                TaxAmount = billing.TaxAmount,
                GrandTotal = billing.GrandTotal,
                Status = InvoiceStatus.Final,
                CreatedByUserId = request.CreatedByUserId,
                TariffSnapshot = new TariffSnapshot
                {
                    OriginalTariffId = tariff.Id,
                    TariffName = tariff.Name,
                    SummerOffPeakStart = GetSummerOffPeakStart(tariff, billing),
                    SummerOffPeakEnd = GetSummerOffPeakEnd(tariff, billing),
                    SummerMidPeakStart = GetSummerMidPeakStart(tariff, billing),
                    SummerMidPeakEnd = GetSummerMidPeakEnd(tariff, billing),
                    SummerPeakStart = GetSummerPeakStart(tariff, billing),
                    SummerPeakEnd = GetSummerPeakEnd(tariff, billing),
                    WinterOffPeakStart = GetWinterOffPeakStart(tariff, billing),
                    WinterOffPeakEnd = GetWinterOffPeakEnd(tariff, billing),
                    WinterMidPeakStart = GetWinterMidPeakStart(tariff, billing),
                    WinterMidPeakEnd = GetWinterMidPeakEnd(tariff, billing),
                    WinterPeakStart = GetWinterPeakStart(tariff, billing),
                    WinterPeakEnd = GetWinterPeakEnd(tariff, billing),
                    OffPeakRate = billing.OffPeakRate,
                    MidPeakRate = billing.MidPeakRate,
                    PeakRate = billing.PeakRate,
                    MonthlyFixedFee = billing.MonthlyFixedFee,
                    ReactivePenaltyThreshold = billing.ReactivePenaltyThreshold,
                    ReactivePenaltyMultiplier = billing.ReactivePenaltyMultiplier,

                    // Derivation snapshot
                    ConsumerTypeCode = billing.ConsumerTypeCode,
                    ConsumerTypeName = billing.ConsumerTypeName,
                    Year = billing.Year,
                    BaseEcaRate = billing.BaseEcaRate,
                    EcaCoefficient = billing.EcaCoefficient,
                    TouOffPeakMultiplier = billing.TouOffPeakMultiplier,
                    TouMidPeakMultiplier = billing.TouMidPeakMultiplier,
                    TouPeakMultiplier = billing.TouPeakMultiplier,
                    EffectiveOffPeakRate = billing.EffectiveOffPeakRate,
                    EffectiveMidPeakRate = billing.EffectiveMidPeakRate,
                    EffectivePeakRate = billing.EffectivePeakRate,
                    PeakPenaltyAmount = billing.PeakPenalty > 0 ? billing.PeakPenalty : null,
                    OffPeakDiscountAmount = billing.OffPeakDiscount > 0 ? billing.OffPeakDiscount : null,
                    Article16Amount = billing.Article16Cost > 0 ? billing.Article16Cost : null,
                    DemandCost = billing.DemandCost > 0 ? billing.DemandCost : null,
                    TotalPenaltyBeforeTax = billing.ReactivePenalty + billing.PeakPenalty + billing.Article16Cost - billing.OffPeakDiscount > 0
                        ? billing.ReactivePenalty + billing.PeakPenalty + billing.Article16Cost - billing.OffPeakDiscount : null,

                    OverrideDetailsJson = request.Overrides is not null && request.Overrides.Count > 0
                        ? JsonSerializer.Serialize(request.Overrides) : null
                }
            };

            // Per-period breakdown
            var periods = new[] {
                ("OffPeak", billing.OffPeakKWh, billing.OffPeakRate, billing.OffPeakCost),
                ("MidPeak", billing.MidPeakKWh, billing.MidPeakRate, billing.MidPeakCost),
                ("Peak",    billing.PeakKWh,    billing.PeakRate,    billing.PeakCost)
            };
            foreach (var (periodType, kwh, rate, cost) in periods)
            {
                if (kwh > 0)
                {
                    invoice.Details.Add(new InvoiceDetail
                    {
                        Phase = "Total",
                        PeriodType = periodType,
                        KWh = kwh,
                        RatePerKWh = rate,
                        Amount = cost
                    });
                }
            }

            // Per-phase breakdown
            var phases = new[] { ("A", billing.PhaseA), ("B", billing.PhaseB), ("C", billing.PhaseC) };
            foreach (var (phaseName, phase) in phases)
            {
                if (phase.OffPeak > 0)
                    invoice.Details.Add(new InvoiceDetail { Phase = phaseName, PeriodType = "OffPeak", KWh = phase.OffPeak, RatePerKWh = billing.OffPeakRate, Amount = Math.Round(phase.OffPeak * billing.OffPeakRate, 0) });
                if (phase.MidPeak > 0)
                    invoice.Details.Add(new InvoiceDetail { Phase = phaseName, PeriodType = "MidPeak", KWh = phase.MidPeak, RatePerKWh = billing.MidPeakRate, Amount = Math.Round(phase.MidPeak * billing.MidPeakRate, 0) });
                if (phase.Peak > 0)
                    invoice.Details.Add(new InvoiceDetail { Phase = phaseName, PeriodType = "Peak", KWh = phase.Peak, RatePerKWh = billing.PeakRate, Amount = Math.Round(phase.Peak * billing.PeakRate, 0) });
            }

            _invoiceRepo.Add(invoice);
            await _uow.SaveChangesAsync(ct);
            await _uow.CommitTransactionAsync(ct);

            _log.LogInformation("Invoice {InvoiceNumber} created for center {CenterId}: {TotalKWh:F3} kWh, {GrandTotal:N0} IRR",
                invoiceNumber, center.Id, billing.TotalKWh, billing.GrandTotal);

            return new SaveInvoiceResult { Success = true, Invoice = invoice };
        }
        catch (Exception ex)
        {
            await _uow.RollbackTransactionAsync(ct);
            _log.LogError(ex, "Failed to save invoice for center {CenterId}", center.Id);
            return new SaveInvoiceResult { Success = false, Error = "خطا در ذخیره صورتحساب" };
        }
    }

    private static void ApplyOverrides(BillingCalculationResult billing, List<BillingOverrideItem> overrides)
    {
        foreach (var ov in overrides)
        {
            if (!ov.OverrideValue.HasValue) continue;
            switch (ov.FieldName)
            {
                case "OffPeakCost": billing.OffPeakCost = ov.OverrideValue.Value; break;
                case "MidPeakCost": billing.MidPeakCost = ov.OverrideValue.Value; break;
                case "PeakCost": billing.PeakCost = ov.OverrideValue.Value; break;
                case "EnergyCost": billing.EnergyCost = ov.OverrideValue.Value; break;
                case "MonthlyFixedFeeTotal": billing.MonthlyFixedFeeTotal = ov.OverrideValue.Value; break;
                case "DemandCost": billing.DemandCost = ov.OverrideValue.Value; break;
                case "ReactivePenalty": billing.ReactivePenalty = ov.OverrideValue.Value; break;
                case "PeakPenalty": billing.PeakPenalty = ov.OverrideValue.Value; break;
                case "OffPeakDiscount": billing.OffPeakDiscount = ov.OverrideValue.Value; break;
                case "Article16Cost": billing.Article16Cost = ov.OverrideValue.Value; break;
                case "TollAmount": billing.TollAmount = ov.OverrideValue.Value; break;
                case "TaxAmount": billing.TaxAmount = ov.OverrideValue.Value; break;
                case "GrandTotal": billing.GrandTotal = ov.OverrideValue.Value; break;
            }
        }
        // Recompute subtotal and grand total
        billing.SubTotal = billing.EnergyCost + billing.MonthlyFixedFeeTotal + billing.DemandCost
                         + billing.ReactivePenalty + billing.PeakPenalty + billing.Article16Cost
                         - billing.OffPeakDiscount;
        if (billing.SubTotal < 0) billing.SubTotal = 0;
        billing.TaxAmount = Math.Round((billing.SubTotal + billing.TollAmount) * billing.TaxPercent / 100m, 0);
        billing.GrandTotal = billing.SubTotal + billing.TollAmount + billing.TaxAmount;
    }

    // Helpers to get time slots from tariff or type config
    private static string GetSummerOffPeakStart(Tariff t, BillingCalculationResult r) => t.SummerOffPeakStart;
    private static string GetSummerOffPeakEnd(Tariff t, BillingCalculationResult r) => t.SummerOffPeakEnd;
    private static string GetSummerMidPeakStart(Tariff t, BillingCalculationResult r) => t.SummerMidPeakStart;
    private static string GetSummerMidPeakEnd(Tariff t, BillingCalculationResult r) => t.SummerMidPeakEnd;
    private static string GetSummerPeakStart(Tariff t, BillingCalculationResult r) => t.SummerPeakStart;
    private static string GetSummerPeakEnd(Tariff t, BillingCalculationResult r) => t.SummerPeakEnd;
    private static string GetWinterOffPeakStart(Tariff t, BillingCalculationResult r) => t.WinterOffPeakStart;
    private static string GetWinterOffPeakEnd(Tariff t, BillingCalculationResult r) => t.WinterOffPeakEnd;
    private static string GetWinterMidPeakStart(Tariff t, BillingCalculationResult r) => t.WinterMidPeakStart;
    private static string GetWinterMidPeakEnd(Tariff t, BillingCalculationResult r) => t.WinterMidPeakEnd;
    private static string GetWinterPeakStart(Tariff t, BillingCalculationResult r) => t.WinterPeakStart;
    private static string GetWinterPeakEnd(Tariff t, BillingCalculationResult r) => t.WinterPeakEnd;
}
