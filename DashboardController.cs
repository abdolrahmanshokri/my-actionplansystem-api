using System.Security.Claims;
using ActionPlanApi.Data;
using ActionPlanApi.Dtos;
using ActionPlanApi.Models;
using ActionPlanApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ActionPlanApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ActivityStatusService _status;
    public DashboardController(AppDbContext db, ActivityStatusService status)
    {
        _db = db;
        _status = status;
    }

    [HttpGet("{periodId}")]
    public async Task<IActionResult> Get(
        int periodId,
        [FromQuery] int? ownerUserId,
        [FromQuery] int? filterUnitId)
    {
        var period = await _db.Periods.FindAsync(periodId);
        if (period == null) return Ok(EmptyStats());
        var yearId = period.YearId;

        var activities = await _db.Activities.ToListAsync();

        // فیلتر بر اساس مالک/زنجیره (در صورت ارسال ownerUserId)
        if (ownerUserId != null)
        {
            var userRoleCodes = await _db.UserRoles
                .Where(ur => ur.UserId == ownerUserId)
                .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Code)
                .ToListAsync();
            var filtered = new List<Activity>();
            foreach (var a in activities)
            {
                if (a.OwnerUserId == ownerUserId) { filtered.Add(a); continue; }
                var steps = await GetEffectiveSteps(a.UnitId);
                bool inChain = false;
                foreach (var s in steps)
                {
                    if (await UserMatchesStep(s, ownerUserId.Value, userRoleCodes))
                    { inChain = true; break; }
                }
                if (inChain) filtered.Add(a);
            }
            activities = filtered;
        }

        if (filterUnitId != null)
            activities = activities.Where(a => a.UnitId == filterUnitId).ToList();

        var withTarget = (await _db.Targets
                .Where(t => t.YearId == yearId)
                .Select(t => t.ActivityId).Distinct().ToListAsync())
            .ToHashSet();
        activities = activities.Where(a => withTarget.Contains(a.Id)).ToList();

        // فقط فعالیت‌های فعال در این دوره
        var activeList = new List<Activity>();
        foreach (var a in activities)
        {
            var isKpi = a.ActivityType == "kpi";
            if (await _status.IsActiveInPeriod(a.Id, periodId, isKpi))
                activeList.Add(a);
        }
        activities = activeList;

        var unitNames = await _db.Units.ToDictionaryAsync(u => u.Id, u => u.Name);

        int draft = 0, submitted = 0, finalC = 0, notStarted = 0;
        double progressSum = 0;
        int progressCount = 0;

        var unitProgs = new Dictionary<int, List<double>>();
        var unitTargs = new Dictionary<int, List<double>>();
        var unitWeightedP = new Dictionary<int, double>();
        var unitWeightedT = new Dictionary<int, double>();
        var unitWeightSum = new Dictionary<int, double>();
        var unitCounts = new Dictionary<int, int>();
        var activityRows = new List<DashboardActivityRowDto>();

        foreach (var a in activities)
        {
            var entry = await _db.ProgressEntries.FirstOrDefaultAsync(e =>
                e.ActivityId == a.Id && e.PeriodId == periodId);

            string status;
            if (entry == null) { status = "not_started"; notStarted++; }
            else if (entry.Status == "draft") { status = "draft"; draft++; }
            else if (entry.Status == "submitted") { status = "submitted"; submitted++; }
            else { status = "final"; finalC++; }

            var pv = entry?.ProgressValue ?? 0;
            progressSum += pv;
            progressCount++;

            double targetThis = 0;
            var target = await _db.Targets
                .Include(t => t.Periods)
                .FirstOrDefaultAsync(t => t.ActivityId == a.Id && t.YearId == yearId);
            if (target != null)
            {
                var tp = target.Periods.FirstOrDefault(p => p.PeriodId == periodId);
                targetThis = tp?.TargetValue ?? 0;
            }
            else if (a.ActivityType == "kpi") targetThis = 100;

            var w = a.Weight;
            if (!unitProgs.ContainsKey(a.UnitId))
            {
                unitProgs[a.UnitId] = new();
                unitTargs[a.UnitId] = new();
            }
            unitProgs[a.UnitId].Add(pv);
            unitTargs[a.UnitId].Add(targetThis);
            unitWeightedP[a.UnitId] = (unitWeightedP.GetValueOrDefault(a.UnitId)) + pv * w;
            unitWeightedT[a.UnitId] = (unitWeightedT.GetValueOrDefault(a.UnitId)) + targetThis * w;
            unitWeightSum[a.UnitId] = (unitWeightSum.GetValueOrDefault(a.UnitId)) + w;
            unitCounts[a.UnitId] = unitCounts.GetValueOrDefault(a.UnitId) + 1;

            activityRows.Add(new DashboardActivityRowDto
            {
                ActivityId = a.Id,
                ActivityName = a.Name,
                ActivityNumber = a.ActivityNumber,
                UnitId = a.UnitId,
                UnitName = unitNames.GetValueOrDefault(a.UnitId, "—"),
                Progress = pv,
                Target = targetThis,
                Weight = w,
                Status = status,
            });
        }

        var unitRows = new List<DashboardUnitRowDto>();
        foreach (var uid in unitProgs.Keys)
        {
            var progs = unitProgs[uid];
            var targs = unitTargs[uid];
            var avgP = progs.Count == 0 ? 0 : progs.Average();
            var avgT = targs.Count == 0 ? 0 : targs.Average();
            var wSum = unitWeightSum.GetValueOrDefault(uid);
            var wP = wSum == 0 ? avgP : unitWeightedP.GetValueOrDefault(uid) / wSum;
            var wT = wSum == 0 ? avgT : unitWeightedT.GetValueOrDefault(uid) / wSum;
            unitRows.Add(new DashboardUnitRowDto
            {
                UnitId = uid,
                UnitName = unitNames.GetValueOrDefault(uid, "—"),
                ActivityCount = unitCounts.GetValueOrDefault(uid),
                AvgProgress = avgP,
                AvgTarget = avgT,
                WeightedProgress = wP,
                WeightedTarget = wT,
            });
        }
        unitRows = unitRows.OrderByDescending(r => r.AvgProgress).ToList();

        return Ok(new DashboardStatsDto
        {
            TotalActivities = activities.Count,
            DraftCount = draft,
            SubmittedCount = submitted,
            FinalCount = finalC,
            NotStartedCount = notStarted,
            AvgProgress = progressCount == 0 ? 0 : progressSum / progressCount,
            UnitRows = unitRows,
            ActivityRows = activityRows,
        });
    }

    private static DashboardStatsDto EmptyStats() => new();

    private async Task<bool> UserMatchesStep(
        ApprovalStep step, int userId, List<string> userRoleCodes)
    {
        if (step.UserId != null) return step.UserId == userId;
        if (step.RoleId == null) return false;
        var role = await _db.Roles.FindAsync(step.RoleId.Value);
        if (role == null) return false;
        return userRoleCodes.Contains(role.Code);
    }

    private async Task<List<ApprovalStep>> GetEffectiveSteps(int unitId)
    {
        var result = new List<ApprovalStep>();
        int? id = unitId;
        int guard = 0;
        while (id != null && guard < 10)
        {
            var steps = await _db.ApprovalSteps
                .Where(s => s.UnitId == id).OrderBy(s => s.StepOrder).ToListAsync();
            result.AddRange(steps);
            var u = await _db.Units.FindAsync(id.Value);
            id = u?.ParentId;
            guard++;
        }
        return result;
    }
}
