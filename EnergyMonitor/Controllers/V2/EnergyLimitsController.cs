using EnergyMonitor.Domain.Entities;
using EnergyMonitor.Domain.Interfaces;
using EnergyMonitor.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnergyMonitor.Controllers.V2;

[Authorize]
[ApiController]
[Route("api/v2/limits")]
public class EnergyLimitsController : ControllerBase
{
    private readonly IRepository<EnergyLimit> _limits;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public EnergyLimitsController(IRepository<EnergyLimit> limits, IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _limits = limits;
        _uow = uow;
        _currentUser = currentUser;
    }

    [HttpGet("by-center/{centerId:guid}")]
    public async Task<IActionResult> GetByCenter(Guid centerId, CancellationToken ct)
    {
        if (!_currentUser.CanAccessCenter(centerId)) return Forbid();
        var list = await _limits.FindAsync(l => l.CenterId == centerId, ct);
        return Ok(list);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] EnergyLimit limit, CancellationToken ct)
    {
        if (!_currentUser.CanAccessCenter(limit.CenterId)) return Forbid();
        _limits.Add(limit);
        await _uow.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetByCenter), new { centerId = limit.CenterId }, limit);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] EnergyLimit dto, CancellationToken ct)
    {
        var limit = await _limits.GetByIdAsync(id, ct);
        if (limit is null) return NotFound();

        limit.LimitType = dto.LimitType;
        limit.PeriodType = dto.PeriodType;
        limit.MaxValue = dto.MaxValue;
        limit.AlertThresholdPercent = dto.AlertThresholdPercent;
        limit.IsActive = dto.IsActive;
        _limits.Update(limit);
        await _uow.SaveChangesAsync(ct);
        return Ok(limit);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var limit = await _limits.GetByIdAsync(id, ct);
        if (limit is null) return NotFound();
        _limits.Remove(limit);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }
}
