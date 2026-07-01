using ActionPlanApi.Data;
using ActionPlanApi.Dtos;
using ActionPlanApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ActionPlanApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ActivityStatusService _status;
    public ReportsController(AppDbContext db, ActivityStatusService status)
    {
        _db = db;
        _status = status;
    }

    [HttpGet]
    public async Task<IActionResult> GetReport(
        [FromQuery] int yearId,
        [FromQuery] int? periodId,
        [FromQuery] int? filterUnitId,
        [FromQuery] bool includeInactive = false,
        [FromQuery] string typeFilter = "all")
    {
        var activities = await _db.Activities.ToListAsync();

        if (filterUnitId != null)
            activities = activities.Where(a => a.UnitId == filterUnitId).ToList();
        if (typeFilter == "kpi")
            activities = activities.Where(a => a.ActivityType == "kpi").ToList();
        else if (typeFilter == "project")
            activities = activities.Where(a => a.ActivityType == "project").ToList();

        var withTarget = await _db.Targets
            .Where(t => t.YearId == yearId)
            .Select(t => t.ActivityId).Distinct().ToListAsync();
        activities = activities.Where(a => withTarget.Contains(a.Id)).ToList();

        var periods = await _db.Periods
            .Where(p => p.YearId == yearId)
            .OrderBy(p => p.MonthNumber).ToListAsync();

        var result = new List<ReportRowDto>();

        foreach (var a in activities)
        {
            var parts = await UnitPathParts(a.UnitId);
            var target = await _db.Targets
                .Include(t => t.Periods)
                .FirstOrDefaultAsync(t => t.ActivityId == a.Id && t.YearId == yearId);

            double targetVal = 0, progressVal = 0;
            string status = "not_started";
            bool active;
            var isKpi = a.ActivityType == "kpi";

            if (periodId != null)
            {
                active = await _status.IsActiveInPeriod(a.Id, periodId.Value, isKpi);
                if (!active && !includeInactive) continue;
                if (target != null)
                {
                    var tp = target.Periods.FirstOrDefault(p => p.PeriodId == periodId);
                    targetVal = tp?.TargetValue ?? 0;
                }
                var entry = await _db.ProgressEntries.FirstOrDefaultAsync(e =>
                    e.ActivityId == a.Id && e.PeriodId == periodId);
                progressVal = entry?.ProgressValue ?? 0;
                status = entry?.Status ?? "not_started";
            }
            else
            {
                // کل سال
                bool anyActiveMonth = false;
                foreach (var p in periods)
                {
                    if (await _status.IsActiveInPeriod(a.Id, p.Id, isKpi))
                    {
                        anyActiveMonth = true;
                        break;
                    }
                }
                active = anyActiveMonth;
                if (!active && !includeInactive) continue;

                if (!isKpi)
                {
                    if (target != null && target.Periods.Any())
                    {
                        var sorted = target.Periods
                            .Select(tp => new
                            {
                                tp.TargetValue,
                                Month = periods.FirstOrDefault(p => p.Id == tp.PeriodId)?.MonthNumber ?? 0
                            })
                            .OrderByDescending(x => x.Month).ToList();
                        targetVal = sorted.First().TargetValue;
                    }
                    double lastProg = 0;
                    string lastStatus = "not_started";
                    foreach (var p in periods)
                    {
                        var entry = await _db.ProgressEntries.FirstOrDefaultAsync(e =>
                            e.ActivityId == a.Id && e.PeriodId == p.Id);
                        if (entry != null && entry.ProgressValue > 0)
                        {
                            lastProg = entry.ProgressValue;
                            lastStatus = entry.Status;
                        }
                    }
                    progressVal = lastProg;
                    status = lastStatus;
                }
                else
                {
                    double sumP = 0, sumT = 0;
                    int cnt = 0;
                    foreach (var p in periods)
                    {
                        if (!await _status.IsActiveInPeriod(a.Id, p.Id, true)) continue;
                        cnt++;
                        sumT += 100;
                        var entry = await _db.ProgressEntries.FirstOrDefaultAsync(e =>
                            e.ActivityId == a.Id && e.PeriodId == p.Id);
                        sumP += entry?.ProgressValue ?? 0;
                    }
                    targetVal = cnt == 0 ? 0 : sumT / cnt;
                    progressVal = cnt == 0 ? 0 : sumP / cnt;
                    status = "kpi";
                }
            }

            var achievement = targetVal == 0 ? 0 : (progressVal / targetVal * 100);
            result.Add(new ReportRowDto
            {
                ActivityNumber = a.ActivityNumber ?? "",
                ActivityName = a.Name,
                UnitRoot = parts.Count > 0 ? parts[0] : "",
                UnitMid = parts.Count > 1 ? parts[1] : "",
                UnitLeaf = parts.Count > 2 ? parts[2] : "",
                Type = isKpi ? "KPI" : "پروژه",
                Weight = a.Weight,
                Target = targetVal,
                Progress = progressVal,
                Achievement = achievement,
                Status = status,
                IsActive = active
            });
        }

        return Ok(result.OrderBy(r => r.ActivityNumber));
    }

    private async Task<List<string>> UnitPathParts(int unitId)
    {
        var parts = new List<string>();
        int? id = unitId;
        int guard = 0;
        while (id != null && guard < 10)
        {
            var u = await _db.Units.FindAsync(id.Value);
            if (u == null) break;
            parts.Insert(0, u.Name);
            id = u.ParentId;
            guard++;
        }
        return parts;
    }
}
