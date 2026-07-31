using EnergyMonitor.Application.Interfaces;
using EnergyMonitor.Domain.Entities;
using EnergyMonitor.Domain.Enums;
using EnergyMonitor.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace EnergyMonitor.Application.Services;

public class BillingService : IBillingService
{
    private readonly ICenterRepository _centerRepo;
    private readonly IDeviceRepository _deviceRepo;
    private readonly IEnergySnapshotReader _snapshotReader;
    private readonly ITariffRepository _tariffRepo;
    private readonly ILogger<BillingService> _log;

    public BillingService(
        ICenterRepository centerRepo,
        IDeviceRepository deviceRepo,
        IEnergySnapshotReader snapshotReader,
        ITariffRepository tariffRepo,
        ILogger<BillingService> log)
    {
        _centerRepo = centerRepo;
        _deviceRepo = deviceRepo;
        _snapshotReader = snapshotReader;
        _tariffRepo = tariffRepo;
        _log = log;
    }

    public async Task<BillingCalculationResult> CalculateAsync(BillingCalculationRequest request, CancellationToken ct = default)
    {
        var center = await _centerRepo.GetByIdAsync(request.CenterId, ct);
        if (center is null)
            throw new InvalidOperationException("مرکز یافت نشد");

        var iranTz = TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");
        var pc = new System.Globalization.PersianCalendar();

        var fromPersian = ParsePersianDate(request.FromDate, pc);
        var toPersian = ParsePersianDate(request.ToDate, pc).AddDays(1);

        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(fromPersian, iranTz);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(toPersian, iranTz);

        // Resolve tariff
        var tariffId = request.TariffId ?? center.TariffId;
        Tariff? tariff = null;
        List<TieredRate> tieredRates = new();
        List<TariffOverride> overrides = new();

        // Consumer type / yearly config for automatic derivation
        ConsumerTypeYearlyConfig? typeConfig = null;
        YearlyBaseRate? yearlyBaseRate = null;
        ConsumerType? consumerType = null;

        if (tariffId.HasValue)
        {
            tariff = await _tariffRepo.GetByIdAsync(tariffId.Value, ct);
            if (tariff is not null)
            {
                tieredRates = await _tariffRepo.GetTieredRatesAsync(tariff.Id, ct);
                overrides = tariff.Overrides.ToList();

                // Load consumer type config if in Automatic mode
                if (tariff.RateDerivationMode == RateDerivationMode.Automatic && !string.IsNullOrEmpty(tariff.ConsumerTypeCode))
                {
                    consumerType = await _tariffRepo.GetConsumerTypeAsync(tariff.ConsumerTypeCode, ct);

                    // Auto-resolve yearly base rate:
                    // 1. Try tariff's selected year
                    // 2. Fall back to current Persian year
                    // 3. Fall back to latest available year
                    var iranNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, iranTz);
                    var currentYear = pc.GetYear(iranNow);
                    int? resolvedYear = tariff.Year;

                    if (resolvedYear.HasValue)
                        yearlyBaseRate = await _tariffRepo.GetYearlyBaseRateAsync(resolvedYear.Value, ct);

                    if (yearlyBaseRate is null)
                    {
                        yearlyBaseRate = await _tariffRepo.GetYearlyBaseRateAsync(currentYear, ct);
                        if (yearlyBaseRate is not null) resolvedYear = currentYear;
                    }

                    if (yearlyBaseRate is null)
                    {
                        yearlyBaseRate = await _tariffRepo.GetLatestYearlyBaseRateAsync(currentYear, ct);
                        if (yearlyBaseRate is not null) resolvedYear = yearlyBaseRate.Year;
                    }

                    if (resolvedYear.HasValue)
                        typeConfig = await _tariffRepo.GetConsumerTypeYearlyConfigAsync(tariff.ConsumerTypeCode, resolvedYear.Value, ct);
                }
            }
        }

        // Resolve effective consumer type code (center override > tariff consumer type)
        var effectiveConsumerTypeCode = center.ConsumerTypeCode ?? tariff?.ConsumerTypeCode;

        // Fetch consumption data
        List<EnergySnapshotRowDto> snaps;
        List<string> pfDeviceIds = new();
        if (!string.IsNullOrEmpty(request.DeviceId))
        {
            var device = await _deviceRepo.GetByDeviceIdAsync(request.DeviceId, ct);
            if (device is null || device.CenterId != center.Id)
                throw new InvalidOperationException("دستگاه مشخص شده برای این مرکز یافت نشد");
            snaps = (await _snapshotReader.GetRangeAsync(device.DeviceId, fromUtc, toUtc, ct)).ToList();
            pfDeviceIds.Add(device.DeviceId);
        }
        else
        {
            var activeDevices = (await _deviceRepo.GetByCenterAsync(center.Id, ct))
                .Where(d => d.IsActive).ToList();
            if (activeDevices.Count == 0)
                throw new InvalidOperationException("مرکز فاقد دستگاه فعال است");
            snaps = new List<EnergySnapshotRowDto>();
            foreach (var d in activeDevices)
            {
                var dSnaps = await _snapshotReader.GetRangeAsync(d.DeviceId, fromUtc, toUtc, ct);
                snaps.AddRange(dSnaps);
                pfDeviceIds.Add(d.DeviceId);
            }
        }

        // Aggregate consumption
        var hourly = new Dictionary<(DateTime Date, int Hour), (decimal A, decimal B, decimal C, decimal Power)>();
        decimal totalA = 0, totalB = 0, totalC = 0;
        decimal offPeakTotal = 0, midPeakTotal = 0, peakTotal = 0;
        decimal maxHourlyPower = 0;
        var phaseA = new PhasePeriodKWh();
        var phaseB = new PhasePeriodKWh();
        var phaseC = new PhasePeriodKWh();
        var periodDetails = new List<BillingPeriodDetail>();

        foreach (var snap in snaps)
        {
            decimal San(decimal v) => v < 0 || v > 5000 ? 0 : v;
            var deltaA = San(snap.DeltaA);
            var deltaB = San(snap.DeltaB);
            var deltaC = San(snap.DeltaC);
            var totalDelta = deltaA + deltaB + deltaC;
            var power = San(snap.TotalPower);

            if (totalDelta > 0.001m || power > 0)
            {
                var iran = TimeZoneInfo.ConvertTimeFromUtc(snap.Timestamp, iranTz);
                var key = (iran.Date, iran.Hour);
                if (!hourly.TryGetValue(key, out var cur))
                    cur = (0, 0, 0, 0);
                hourly[key] = (cur.A + deltaA, cur.B + deltaB, cur.C + deltaC, Math.Max(cur.Power, power));

                totalA += deltaA;
                totalB += deltaB;
                totalC += deltaC;

                maxHourlyPower = Math.Max(maxHourlyPower, power);

                var period = ClassifyHour(iran, tariff, typeConfig);
                offPeakTotal += period == 0 ? totalDelta : 0;
                midPeakTotal += period == 1 ? totalDelta : 0;
                peakTotal += period == 2 ? totalDelta : 0;

                if (period == 0) { phaseA.OffPeak += deltaA; phaseB.OffPeak += deltaB; phaseC.OffPeak += deltaC; }
                else if (period == 1) { phaseA.MidPeak += deltaA; phaseB.MidPeak += deltaB; phaseC.MidPeak += deltaC; }
                else { phaseA.Peak += deltaA; phaseB.Peak += deltaB; phaseC.Peak += deltaC; }
            }
        }

        var dailyGroups = hourly.GroupBy(h => h.Key.Date).OrderBy(g => g.Key);
        foreach (var day in dailyGroups)
        {
            var dayOffPeak = day.Where(h => ClassifyHour(iranTz, h.Key.Date, h.Key.Hour, tariff, typeConfig) == 0).Sum(h => h.Value.A + h.Value.B + h.Value.C);
            var dayMidPeak = day.Where(h => ClassifyHour(iranTz, h.Key.Date, h.Key.Hour, tariff, typeConfig) == 1).Sum(h => h.Value.A + h.Value.B + h.Value.C);
            var dayPeak = day.Where(h => ClassifyHour(iranTz, h.Key.Date, h.Key.Hour, tariff, typeConfig) == 2).Sum(h => h.Value.A + h.Value.B + h.Value.C);
            var pDate = $"{pc.GetYear(day.Key):D4}/{pc.GetMonth(day.Key):D2}/{pc.GetDayOfMonth(day.Key):D2}";
            periodDetails.Add(new BillingPeriodDetail
            {
                PersianDate = pDate,
                OffPeakKWh = Math.Round(dayOffPeak, 4),
                MidPeakKWh = Math.Round(dayMidPeak, 4),
                PeakKWh = Math.Round(dayPeak, 4),
                TotalKWh = Math.Round(dayOffPeak + dayMidPeak + dayPeak, 4)
            });
        }

        var dateSpan = toPersian - fromPersian;
        var totalDays = dateSpan.Days;
        var totalMonths = (toPersian.Year - fromPersian.Year) * 12 + toPersian.Month - fromPersian.Month;
        var totalKWh = totalA + totalB + totalC;

        // Build result
        var result = new BillingCalculationResult
        {
            CenterId = request.CenterId,
            TariffId = tariffId,
            CenterName = center.Name,
            TariffName = tariff?.Name ?? "بدون تعرفه",
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            Days = totalDays,
            Months = totalMonths,
            OffPeakKWh = Math.Round(offPeakTotal, 4),
            MidPeakKWh = Math.Round(midPeakTotal, 4),
            PeakKWh = Math.Round(peakTotal, 4),
            TotalKWh = Math.Round(totalKWh, 4),
            PhaseA = phaseA,
            PhaseB = phaseB,
            PhaseC = phaseC,
            PeriodDetails = periodDetails
        };

        if (tariff is not null)
        {
            // Populate consumer type info
            result.ConsumerTypeCode = effectiveConsumerTypeCode;
            result.ConsumerTypeName = consumerType?.Name;
            result.Year = tariff.Year;

            // === DETERMINE EFFECTIVE RATES ===
            decimal effOffPeakRate, effMidPeakRate, effPeakRate;

            if (tariff.RateDerivationMode == RateDerivationMode.Automatic && typeConfig is not null && yearlyBaseRate is not null)
            {
                result.BaseEcaRate = yearlyBaseRate.BaseRatePerKwh;
                result.EcaCoefficient = typeConfig.EcaCoefficient;

                if (consumerType?.BillingModel == BillingModel.Tiered)
                {
                    // === Tiered billing (Residential, etc.) ===
                    var supplyCost = yearlyBaseRate.SupplyCostPerKwh;
                    result.SupplyCostPerKwh = supplyCost;

                    var pattern = center.ConsumptionPatternKWh ?? typeConfig.ConsumptionPatternKWh;
                    result.ConsumptionPatternKWh = pattern;

                    result.MonthlyFixedFee = typeConfig.MonthlyFixedFee > 0 ? typeConfig.MonthlyFixedFee : tariff.MonthlyFixedFee;
                    result.MonthlyFixedFeeTotal = totalMonths > 0 ? result.MonthlyFixedFee * totalMonths : result.MonthlyFixedFee;
                    result.ReactivePenaltyThreshold = typeConfig.ReactivePenaltyThreshold;
                    result.ReactivePenaltyMultiplier = typeConfig.ReactivePenaltyMultiplier;
                    result.TaxPercent = (int)typeConfig.TaxPercent;

                    var numMonths = totalMonths > 0 ? totalMonths : 1;
                    var monthlyKWh = totalKWh / numMonths;

                    if (typeConfig.TieredRates.Count > 0 && pattern.HasValue && pattern > 0)
                    {
                        result.HasTieredRates = true;
                        ApplyTieredRatesMonthly(result, typeConfig.TieredRates.ToList(), supplyCost, monthlyKWh, numMonths);
                    }
                    else
                    {
                        result.EnergyCost = Math.Round(totalKWh * supplyCost, 0);
                    }

                    // Per-phase cost (proportional to each phase's share of total consumption)
                    if (totalKWh > 0)
                    {
                        result.PhaseACost = Math.Round(result.EnergyCost * phaseA.Total / totalKWh, 0);
                        result.PhaseBCost = Math.Round(result.EnergyCost * phaseB.Total / totalKWh, 0);
                        result.PhaseCCost = result.EnergyCost - result.PhaseACost - result.PhaseBCost;
                    }

                    // For tiered billing, all periods use the same supply cost
                    effOffPeakRate = effMidPeakRate = effPeakRate = supplyCost;
                    result.OffPeakRate = result.MidPeakRate = result.PeakRate = supplyCost;

                    // Per-period costs proportional to consumption share
                    if (totalKWh > 0 && result.EnergyCost > 0)
                    {
                        result.OffPeakCost = Math.Round(result.EnergyCost * offPeakTotal / totalKWh, 0);
                        result.MidPeakCost = Math.Round(result.EnergyCost * midPeakTotal / totalKWh, 0);
                        result.PeakCost = result.EnergyCost - result.OffPeakCost - result.MidPeakCost;
                    }
                }
                else
                {
                    // === TOU billing (Industrial, Commercial, etc.) ===
                    var baseRate = yearlyBaseRate.BaseRatePerKwh;
                    var coeff = typeConfig.EcaCoefficient;
                    var supplyCost = baseRate * coeff;

                    result.TouOffPeakMultiplier = typeConfig.TouOffPeakMultiplier;
                    result.TouMidPeakMultiplier = typeConfig.TouMidPeakMultiplier;
                    result.TouPeakMultiplier = typeConfig.TouPeakMultiplier;

                    effOffPeakRate = supplyCost * typeConfig.TouOffPeakMultiplier;
                    effMidPeakRate = supplyCost * typeConfig.TouMidPeakMultiplier;
                    effPeakRate = supplyCost * typeConfig.TouPeakMultiplier;

                    result.EffectiveOffPeakRate = effOffPeakRate;
                    result.EffectiveMidPeakRate = effMidPeakRate;
                    result.EffectivePeakRate = effPeakRate;

                    // Apply overrides
                    foreach (var ov in overrides.Where(o => !o.IsPercentage))
                    {
                        if (ov.FieldName == "OffPeakRate") effOffPeakRate = ov.OverrideValue;
                        else if (ov.FieldName == "MidPeakRate") effMidPeakRate = ov.OverrideValue;
                        else if (ov.FieldName == "PeakRate") effPeakRate = ov.OverrideValue;
                    }
                    foreach (var ov in overrides.Where(o => o.IsPercentage))
                    {
                        if (ov.FieldName == "OffPeakRate") effOffPeakRate += effOffPeakRate * ov.OverrideValue / 100m;
                        else if (ov.FieldName == "MidPeakRate") effMidPeakRate += effMidPeakRate * ov.OverrideValue / 100m;
                        else if (ov.FieldName == "PeakRate") effPeakRate += effPeakRate * ov.OverrideValue / 100m;
                    }

                    result.OffPeakRate = effOffPeakRate;
                    result.MidPeakRate = effMidPeakRate;
                    result.PeakRate = effPeakRate;
                    result.MonthlyFixedFee = typeConfig.MonthlyFixedFee > 0 ? typeConfig.MonthlyFixedFee : tariff.MonthlyFixedFee;
                    result.MonthlyFixedFeeTotal = totalMonths > 0 ? result.MonthlyFixedFee * totalMonths : result.MonthlyFixedFee;
                    result.ReactivePenaltyThreshold = typeConfig.ReactivePenaltyThreshold;
                    result.ReactivePenaltyMultiplier = typeConfig.ReactivePenaltyMultiplier;
                    result.TaxPercent = (int)typeConfig.TaxPercent;
                    result.DemandRate = typeConfig.DemandRate;

                    result.HasTieredRates = typeConfig.TieredRates.Count > 0;

                    if (typeConfig.TieredRates.Count > 0)
                    {
                        ApplyTieredRates(result, typeConfig.TieredRates.ToList(), supplyCost);
                        if (totalKWh > 0 && result.EnergyCost > 0)
                        {
                            result.OffPeakCost = Math.Round(result.EnergyCost * offPeakTotal / totalKWh, 0);
                            result.MidPeakCost = Math.Round(result.EnergyCost * midPeakTotal / totalKWh, 0);
                            result.PeakCost = result.EnergyCost - result.OffPeakCost - result.MidPeakCost;
                        }
                    }
                    else
                    {
                        result.OffPeakCost = Math.Round(result.OffPeakKWh * effOffPeakRate, 0);
                        result.MidPeakCost = Math.Round(result.MidPeakKWh * effMidPeakRate, 0);
                        result.PeakCost = Math.Round(result.PeakKWh * effPeakRate, 0);
                        result.EnergyCost = result.OffPeakCost + result.MidPeakCost + result.PeakCost;
                    }

                    // Demand charge
                    result.MaxDemandKW = Math.Round(maxHourlyPower, 3);
                    if (typeConfig.DemandChargeEnabled && typeConfig.DemandRate > 0 && maxHourlyPower > 0)
                    {
                        result.DemandCost = Math.Round(maxHourlyPower * typeConfig.DemandRate, 0);
                    }

                    // Peak penalty
                    var isHighConsumer = IsHighConsumer(effectiveConsumerTypeCode, totalKWh, center);
                    var peakCoeff = isHighConsumer ? typeConfig.PeakPenaltyCoefficient : typeConfig.PeakPenaltyNormalCoefficient;
                    if (peakCoeff > 0 && result.PeakKWh > 0)
                    {
                        result.PeakPenalty = Math.Round(result.PeakKWh * peakCoeff * (baseRate * coeff), 0);
                        result.HasPeakPenalty = true;
                    }

                    // Off-peak discount
                    if (typeConfig.OffPeakDiscountCoefficient > 0 && result.OffPeakKWh > 0)
                    {
                        result.OffPeakDiscount = Math.Round(result.OffPeakKWh * typeConfig.OffPeakDiscountCoefficient * (baseRate * coeff), 0);
                        result.HasOffPeakDiscount = true;
                    }

                    // Article 16
                    if (typeConfig.Article16Enabled && totalKWh > 0)
                    {
                        var centerPowerMW = center.ContractCapacityMW ?? 0;
                        if (centerPowerMW > 1 || IsHeavyIndustry(effectiveConsumerTypeCode))
                        {
                            var mandatedKWh = totalKWh * typeConfig.Article16Percent / 100m;
                            result.Article16Cost = Math.Round(mandatedKWh * typeConfig.Article16GreenEnergyRate, 0);
                            result.HasArticle16 = true;
                        }
                    }
                }
            }
            else
            {
                // Manual mode — use tariff's own rates (existing behavior)
                effOffPeakRate = tariff.OffPeakRate;
                effMidPeakRate = tariff.MidPeakRate;
                effPeakRate = tariff.PeakRate;

                result.OffPeakRate = tariff.OffPeakRate;
                result.MidPeakRate = tariff.MidPeakRate;
                result.PeakRate = tariff.PeakRate;
                result.MonthlyFixedFee = tariff.MonthlyFixedFee;
                result.MonthlyFixedFeeTotal = totalMonths > 0 ? tariff.MonthlyFixedFee * totalMonths : tariff.MonthlyFixedFee;
                result.ReactivePenaltyThreshold = tariff.ReactivePenaltyThreshold;
                result.ReactivePenaltyMultiplier = tariff.ReactivePenaltyMultiplier;
                result.DemandRate = tariff.DemandRate;
                result.HasTieredRates = tieredRates.Count > 0;

                if (tieredRates.Count > 0)
                {
                    ApplyTieredRates(result, tieredRates);
                }
                else
                {
                    result.OffPeakCost = Math.Round(result.OffPeakKWh * tariff.OffPeakRate, 0);
                    result.MidPeakCost = Math.Round(result.MidPeakKWh * tariff.MidPeakRate, 0);
                    result.PeakCost = Math.Round(result.PeakKWh * tariff.PeakRate, 0);
                    result.EnergyCost = result.OffPeakCost + result.MidPeakCost + result.PeakCost;
                }

                // Demand charge
                result.MaxDemandKW = Math.Round(maxHourlyPower, 3);
                if (tariff.DemandChargeEnabled && tariff.DemandRate > 0 && maxHourlyPower > 0)
                {
                    result.DemandCost = Math.Round(maxHourlyPower * tariff.DemandRate, 0);
                }
            }

            // === Per-phase cost (proportional to each phase's share of total) ===
            if (totalKWh > 0 && result.EnergyCost > 0 && result.PhaseACost == 0 && result.PhaseBCost == 0 && result.PhaseCCost == 0)
            {
                result.PhaseACost = Math.Round(result.EnergyCost * phaseA.Total / totalKWh, 0);
                result.PhaseBCost = Math.Round(result.EnergyCost * phaseB.Total / totalKWh, 0);
                result.PhaseCCost = result.EnergyCost - result.PhaseACost - result.PhaseBCost;
            }

            // === Reactive Power Penalty (common to both modes) ===
            if (pfDeviceIds.Count > 0)
            {
                decimal totalPfA = 0, totalPfB = 0, totalPfC = 0;
                int pfCount = 0;
                foreach (var pid in pfDeviceIds)
                {
                    var pa = await _snapshotReader.GetAveragePfAsync(pid, fromUtc, toUtc, ct);
                    if (pa.HasValue) { totalPfA += pa.Value.pfA; totalPfB += pa.Value.pfB; totalPfC += pa.Value.pfC; pfCount++; }
                }
                if (pfCount > 0)
                {
                    result.AveragePfA = totalPfA / pfCount;
                    result.AveragePfB = totalPfB / pfCount;
                    result.AveragePfC = totalPfC / pfCount;
                }
                else
                {
                    result.AveragePfA = 0.9m; result.AveragePfB = 0.9m; result.AveragePfC = 0.9m;
                }
            }
            else
            {
                result.AveragePfA = 0.9m; result.AveragePfB = 0.9m; result.AveragePfC = 0.9m;
            }
            result.AveragePf = Math.Min(Math.Min(result.AveragePfA, result.AveragePfB), result.AveragePfC);

            var minPf = Math.Min(Math.Min(result.AveragePfA, result.AveragePfB), result.AveragePfC);
            var isTiered = consumerType?.BillingModel == BillingModel.Tiered || consumerType?.BillingModel == BillingModel.TOU_Tiered;
            if (!isTiered && minPf < result.ReactivePenaltyThreshold && totalKWh > 0)
            {
                var pfShortfall = result.ReactivePenaltyThreshold - minPf;
                result.ReactivePenalty = Math.Round(result.EnergyCost * pfShortfall * result.ReactivePenaltyMultiplier, 0);
                result.HasReactivePenalty = true;
            }
            else
            {
                result.ReactivePenalty = 0;
                result.HasReactivePenalty = false;
            }

            // === Toll (عوارض) ===
            var tollBase = result.EnergyCost + result.MonthlyFixedFeeTotal + result.DemandCost + result.ReactivePenalty + result.PeakPenalty + result.Article16Cost - result.OffPeakDiscount;
            if (tollBase < 0) tollBase = 0;
            var tollPercent = typeConfig?.TollPercent ?? 10m;
            result.TollAmount = Math.Round(tollBase * tollPercent / 100m, 0);

            // === Totals ===
            result.SubTotal = tollBase;
            result.TaxAmount = Math.Round((result.SubTotal + result.TollAmount) * result.TaxPercent / 100m, 0);
            result.GrandTotal = result.SubTotal + result.TollAmount + result.TaxAmount;

            // === Build editable items ===
            BuildEditableItems(result);
        }

        return result;
    }

    private static void ApplyTieredRates(BillingCalculationResult result, List<TieredRate> tieredRates)
    {
        var totalPeriodKWh = result.OffPeakKWh + result.MidPeakKWh + result.PeakKWh;
        var tiers = tieredRates.Where(r => r.PeriodType == "All" || r.PeriodType == "Total")
            .OrderBy(r => r.TierFrom).ToList();
        if (tiers.Count == 0)
            tiers = tieredRates.OrderBy(r => r.TierFrom).ToList();

        decimal remaining = totalPeriodKWh;
        decimal totalCost = 0;
        var tierKWh = new List<decimal>();
        var tierCosts = new List<decimal>();
        var tierRates = new List<decimal>();

        foreach (var tier in tiers)
        {
            var tierSize = tier.TierTo > 0 ? (decimal)tier.TierTo - tier.TierFrom : 999999m;
            var kwh = Math.Min(remaining, tierSize);
            var cost = kwh * tier.RatePerKWh;
            tierKWh.Add(kwh);
            tierCosts.Add(Math.Round(cost, 0));
            tierRates.Add(tier.RatePerKWh);
            totalCost += cost;
            remaining -= kwh;
            if (remaining <= 0) break;
        }

        while (tierKWh.Count < 3) tierKWh.Add(0);
        while (tierCosts.Count < 3) tierCosts.Add(0);
        while (tierRates.Count < 3) tierRates.Add(0);

        result.Tier1KWh = tierKWh[0];
        result.Tier2KWh = tierKWh[1];
        result.Tier3KWh = tierKWh[2];
        result.Tier1Cost = tierCosts[0];
        result.Tier2Cost = tierCosts[1];
        result.Tier3Cost = tierCosts[2];
        result.Tier1Rate = tierRates[0];
        result.Tier2Rate = tierRates[1];
        result.Tier3Rate = tierRates[2];
        result.EnergyCost = Math.Round(totalCost, 0);
    }

    private static void ApplyTieredRates(BillingCalculationResult result, List<ConsumerTypeTieredRate> tieredRates, decimal supplyCost = 0)
    {
        var totalKWh = result.OffPeakKWh + result.MidPeakKWh + result.PeakKWh;
        var tiers = tieredRates.OrderBy(r => r.SortOrder).ThenBy(r => r.TierFrom).ToList();

        decimal remaining = totalKWh;
        decimal totalCost = 0;
        var tierKWh = new List<decimal>();
        var tierCosts = new List<decimal>();
        var tierRates = new List<decimal>();

        foreach (var tier in tiers)
        {
            var tierSize = tier.TierTo > 0 ? tier.TierTo - tier.TierFrom : 999999m;
            var kwh = Math.Min(remaining, tierSize);
            if (kwh <= 0) continue;

            var rate = tier.Coefficient.HasValue && supplyCost > 0
                ? tier.Coefficient.Value * supplyCost
                : tier.RatePerKwh > 0
                    ? tier.RatePerKwh
                    : supplyCost > 0
                        ? supplyCost
                        : 0;
            var cost = kwh * rate;
            tierKWh.Add(kwh);
            tierCosts.Add(Math.Round(cost, 0));
            tierRates.Add(Math.Round(rate, 0));
            totalCost += cost;
            remaining -= kwh;
            if (remaining <= 0) break;
        }

        while (tierKWh.Count < 3) tierKWh.Add(0);
        while (tierCosts.Count < 3) tierCosts.Add(0);
        while (tierRates.Count < 3) tierRates.Add(0);

        result.Tier1KWh = tierKWh[0];
        result.Tier2KWh = tierKWh[1];
        result.Tier3KWh = tierKWh[2];
        result.Tier1Cost = tierCosts[0];
        result.Tier2Cost = tierCosts[1];
        result.Tier3Cost = tierCosts[2];
        result.Tier1Rate = tierRates[0];
        result.Tier2Rate = tierRates[1];
        result.Tier3Rate = tierRates[2];
        result.EnergyCost = Math.Round(totalCost, 0);
    }

    private static void ApplyTieredRatesMonthly(BillingCalculationResult result, List<ConsumerTypeTieredRate> tieredRates, decimal supplyCost, decimal monthlyKWh, int numMonths)
    {
        var tiers = tieredRates.OrderBy(r => r.SortOrder).ThenBy(r => r.TierFrom).ToList();

        decimal remaining = monthlyKWh;
        decimal monthlyCost = 0;
        var tierKWh = new List<decimal>();
        var tierCosts = new List<decimal>();
        var tierRates = new List<decimal>();

        foreach (var tier in tiers)
        {
            var tierSize = tier.TierTo > 0 ? tier.TierTo - tier.TierFrom : 999999m;
            var kwh = Math.Min(remaining, tierSize);
            if (kwh <= 0) continue;

            var rate = tier.Coefficient.HasValue && supplyCost > 0
                ? tier.Coefficient.Value * supplyCost
                : tier.RatePerKwh > 0
                    ? tier.RatePerKwh
                    : supplyCost > 0
                        ? supplyCost
                        : 0;
            var cost = kwh * rate;
            tierKWh.Add(kwh);
            tierCosts.Add(Math.Round(cost, 0));
            tierRates.Add(Math.Round(rate, 0));
            monthlyCost += cost;
            remaining -= kwh;
            if (remaining <= 0) break;
        }

        while (tierKWh.Count < 3) tierKWh.Add(0);
        while (tierCosts.Count < 3) tierCosts.Add(0);
        while (tierRates.Count < 3) tierRates.Add(0);

        result.Tier1KWh = tierKWh[0] * numMonths;
        result.Tier2KWh = tierKWh[1] * numMonths;
        result.Tier3KWh = tierKWh[2] * numMonths;
        result.Tier1Cost = tierCosts[0] * numMonths;
        result.Tier2Cost = tierCosts[1] * numMonths;
        result.Tier3Cost = tierCosts[2] * numMonths;
        result.Tier1Rate = tierRates[0];
        result.Tier2Rate = tierRates[1];
        result.Tier3Rate = tierRates[2];
        result.EnergyCost = Math.Round(monthlyCost * numMonths, 0);
    }

    private static void BuildEditableItems(BillingCalculationResult result)
    {
        result.EditableItems = new List<BillingResultItem>
        {
            new() { FieldName = "OffPeakCost", Label = "هزینه کم‌باری", AutoValue = result.OffPeakCost },
            new() { FieldName = "MidPeakCost", Label = "هزینه میان‌باری", AutoValue = result.MidPeakCost },
            new() { FieldName = "PeakCost", Label = "هزینه اوج‌باری", AutoValue = result.PeakCost },
            new() { FieldName = "EnergyCost", Label = "جمع هزینه انرژی", AutoValue = result.EnergyCost },
            new() { FieldName = "MonthlyFixedFeeTotal", Label = "آبونمان", AutoValue = result.MonthlyFixedFeeTotal },
            new() { FieldName = "DemandCost", Label = "هزینه دیماند", AutoValue = result.DemandCost },
            new() { FieldName = "ReactivePenalty", Label = "جریمه راکتیو", AutoValue = result.ReactivePenalty },
            new() { FieldName = "PeakPenalty", Label = "جریمه اوج بار", AutoValue = result.PeakPenalty },
            new() { FieldName = "OffPeakDiscount", Label = "تخفیف کم‌باری", AutoValue = result.OffPeakDiscount },
            new() { FieldName = "Article16Cost", Label = "هزینه ماده ۱۶", AutoValue = result.Article16Cost },
            new() { FieldName = "TollAmount", Label = "عوارض", AutoValue = result.TollAmount },
            new() { FieldName = "TaxAmount", Label = "مالیات بر ارزش افزوده", AutoValue = result.TaxAmount },
            new() { FieldName = "GrandTotal", Label = "قابل پرداخت", AutoValue = result.GrandTotal }
        };
    }

    // --- TOU Classification ---

    private static int ClassifyHour(DateTime iranTime, Tariff? tariff, ConsumerTypeYearlyConfig? typeConfig)
    {
        // Type config takes priority if available
        if (typeConfig is not null)
            return ClassifyFromConfig(iranTime, typeConfig);

        if (tariff is null)
            return ClassifyHourDefault(iranTime.Hour);

        var isSummer = IsSummer(iranTime);
        var (offPeakStart, offPeakEnd) = ParseSlot(isSummer ? tariff.SummerOffPeakStart : tariff.WinterOffPeakStart,
                                                    isSummer ? tariff.SummerOffPeakEnd : tariff.WinterOffPeakEnd);
        var (peakStart, peakEnd) = ParseSlot(isSummer ? tariff.SummerPeakStart : tariff.WinterPeakStart,
                                              isSummer ? tariff.SummerPeakEnd : tariff.WinterPeakEnd);
        return Classify(iranTime.Hour, offPeakStart, offPeakEnd, peakStart, peakEnd);
    }

    private static int ClassifyFromConfig(DateTime iranTime, ConsumerTypeYearlyConfig cfg)
    {
        var isSummer = IsSummer(iranTime);
        var (offPeakStart, offPeakEnd) = ParseSlot(isSummer ? cfg.SummerOffPeakStart : cfg.WinterOffPeakStart,
                                                    isSummer ? cfg.SummerOffPeakEnd : cfg.WinterOffPeakEnd);
        var (peakStart, peakEnd) = ParseSlot(isSummer ? cfg.SummerPeakStart : cfg.WinterPeakStart,
                                              isSummer ? cfg.SummerPeakEnd : cfg.WinterPeakEnd);
        return Classify(iranTime.Hour, offPeakStart, offPeakEnd, peakStart, peakEnd);
    }

    private static int ClassifyHour(TimeZoneInfo iranTz, DateTime date, int hour, Tariff? tariff, ConsumerTypeYearlyConfig? typeConfig)
    {
        var iranTime = date.AddHours(hour);
        return ClassifyHour(iranTime, tariff, typeConfig);
    }

    private static int ClassifyHourDefault(int hour)
    {
        if (hour >= 23 || hour < 6) return 0;
        if (hour >= 17) return 2;
        return 1;
    }

    private static bool IsSummer(DateTime iranTime)
    {
        var pc = new System.Globalization.PersianCalendar();
        int month = pc.GetMonth(iranTime);
        return month >= 4 && month <= 9;
    }

    private static (int start, int end) ParseSlot(string start, string end)
    {
        return (int.Parse(start.Split(':')[0]), int.Parse(end.Split(':')[0]));
    }

    private static int Classify(int hour, int offPeakStart, int offPeakEnd, int peakStart, int peakEnd)
    {
        if (offPeakStart <= offPeakEnd)
        {
            if (hour >= offPeakStart && hour < offPeakEnd) return 0;
        }
        else
        {
            if (hour >= offPeakStart || hour < offPeakEnd) return 0;
        }
        if (peakStart <= peakEnd)
        {
            if (hour >= peakStart && hour < peakEnd) return 2;
        }
        else
        {
            if (hour >= peakStart || hour < peakEnd) return 2;
        }
        return 1;
    }

    private static DateTime ParsePersianDate(string persianDate, System.Globalization.PersianCalendar pc)
    {
        var parts = persianDate.Split('/');
        if (parts.Length != 3) throw new ArgumentException("Invalid date format");
        return pc.ToDateTime(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), 0, 0, 0, 0);
    }

    private static bool IsHighConsumer(string? consumerTypeCode, decimal totalKWh, Domain.Entities.Center center)
    {
        if (string.IsNullOrEmpty(consumerTypeCode)) return false;
        return consumerTypeCode == "1" && totalKWh > 600; // Residential > 600 kWh/month = high consumer
    }

    private static bool IsHeavyIndustry(string? consumerTypeCode)
    {
        if (string.IsNullOrEmpty(consumerTypeCode)) return false;
        return consumerTypeCode == "4-DAL" || consumerTypeCode == "4-HE";
    }
}
