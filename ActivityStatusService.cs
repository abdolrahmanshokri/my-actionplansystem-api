using ActionPlanApi.Data;
using Microsoft.EntityFrameworkCore;

namespace ActionPlanApi.Services;

public class ActivityStatusService
{
    private readonly AppDbContext _db;
    public ActivityStatusService(AppDbContext db) => _db = db;

    // آیا فعالیت در این دوره فعال است؟
    public async Task<bool> IsActiveInPeriod(int activityId, int periodId, bool isKpi)
    {
        var act = await _db.Activities.FindAsync(activityId);
        if (act == null || !act.IsActive) return false;

        var period = await _db.Periods.FindAsync(periodId);
        if (period == null) return false;

        var target = await _db.Targets
            .Include(t => t.Periods)
            .FirstOrDefaultAsync(t =>
                t.ActivityId == activityId && t.YearId == period.YearId);
        if (target == null) return false;

        var tp = target.Periods.FirstOrDefault(p => p.PeriodId == periodId);
        if (tp == null) return false;

        if (isKpi)
        {
            return tp.IsActive;
        }
        else
        {
            if (!tp.IsActive) return false;
            if (tp.TargetValue <= 0) return false;

            // بررسی آخرین ماهی که پیشرفت واقعی ثبت شده است.
            // پروژه فقط وقتی «تمام» محسوب می‌شود که در آن ماه
            // هم تارگت >= ۱۰۰ و هم پیشرفت >= ۱۰۰ باشد.
            // (اگر پیشرفت ۱۰۰ ولی تارگت <۱۰۰ باشد، یعنی پروژه جلو زده
            //  ولی هنوز تمام نشده، پس ماه‌های بعد نمایش داده می‌شوند.)
            var prevPeriodIds = await _db.Periods
                .Where(p => p.YearId == period.YearId &&
                            p.MonthNumber < period.MonthNumber)
                .OrderByDescending(p => p.MonthNumber)
                .Select(p => p.Id)
                .ToListAsync();

            foreach (var prevPid in prevPeriodIds)
            {
                var entry = await _db.ProgressEntries.FirstOrDefaultAsync(e =>
                    e.ActivityId == activityId && e.PeriodId == prevPid);
                if (entry != null)
                {
                    // نزدیک‌ترین ماه قبل که پیشرفت دارد، پیدا شد
                    var prevTp = target.Periods
                        .FirstOrDefault(p => p.PeriodId == prevPid);
                    var prevTarget = prevTp?.TargetValue ?? 0;
                    if (entry.ProgressValue >= 100 && prevTarget >= 100)
                        return false; // پروژه واقعاً تمام شده
                    break; // آخرین پیشرفت واقعی همین بود؛ تمام نشده پس فعال است
                }
            }
            return true;
        }
    }
}
