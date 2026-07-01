using ActionPlanApi.Data;
using ActionPlanApi.Dtos;
using ActionPlanApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ActionPlanApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TargetsController : ControllerBase
{
    private readonly AppDbContext _db;
    public TargetsController(AppDbContext db) => _db = db;

    [HttpGet("{activityId}/{yearId}")]
    public async Task<IActionResult> Get(int activityId, int yearId)
    {
        var target = await _db.Targets
            .Include(t => t.Periods)
            .FirstOrDefaultAsync(t => t.ActivityId == activityId && t.YearId == yearId);
        if (target == null) return Ok((TargetDto?)null);

        var periods = await _db.Periods
            .Where(p => p.YearId == yearId)
            .OrderBy(p => p.MonthNumber)
            .ToListAsync();

        var dto = new TargetDto
        {
            Id = target.Id, ActivityId = target.ActivityId, YearId = target.YearId,
            StartValue = target.StartValue, DistributionType = target.DistributionType,
            Periods = periods.Select(p =>
            {
                var tp = target.Periods.FirstOrDefault(x => x.PeriodId == p.Id);
                return new TargetPeriodDto
                {
                    PeriodId = p.Id, MonthNumber = p.MonthNumber, MonthName = p.MonthName,
                    TargetValue = tp?.TargetValue ?? 0,
                    IsActive = tp?.IsActive ?? true
                };
            }).ToList()
        };
        return Ok(dto);
    }

    [HttpGet("year/{yearId}/activity-ids")]
    public async Task<IActionResult> ActivityIdsWithTarget(int yearId)
    {
        var ids = await _db.Targets
            .Where(t => t.YearId == yearId)
            .Select(t => t.ActivityId)
            .Distinct()
            .ToListAsync();
        return Ok(ids);
    }

    [HttpPost]
    [Authorize(Roles = "super_admin,admin,admin2")]
    public async Task<IActionResult> Save([FromBody] SaveTargetRequest req)
    {
        var target = await _db.Targets
            .FirstOrDefaultAsync(t =>
                t.ActivityId == req.ActivityId && t.YearId == req.YearId);

        if (target == null)
        {
            target = new Target
            {
                ActivityId = req.ActivityId, YearId = req.YearId,
                StartValue = req.StartValue, DistributionType = req.DistributionType
            };
            _db.Targets.Add(target);
            await _db.SaveChangesAsync();
        }
        else
        {
            target.StartValue = req.StartValue;
            target.DistributionType = req.DistributionType;
            var old = _db.TargetPeriods.Where(tp => tp.TargetId == target.Id);
            _db.TargetPeriods.RemoveRange(old);
            await _db.SaveChangesAsync();
        }

        foreach (var kv in req.PeriodValues)
        {
            var active = req.ActiveValues != null &&
                         req.ActiveValues.ContainsKey(kv.Key)
                ? req.ActiveValues[kv.Key] : true;
            _db.TargetPeriods.Add(new TargetPeriod
            {
                TargetId = target.Id, PeriodId = kv.Key,
                TargetValue = kv.Value, IsActive = active
            });
        }
        await _db.SaveChangesAsync();
        return Ok(new { id = target.Id });
    }

    [HttpDelete("{targetId}")]
    [Authorize(Roles = "super_admin,admin,admin2")]
    public async Task<IActionResult> Delete(int targetId)
    {
        var t = await _db.Targets.FindAsync(targetId);
        if (t == null) return NotFound();
        _db.Targets.Remove(t);
        await _db.SaveChangesAsync();
        return Ok();
    }
}
