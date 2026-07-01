using System.Security.Claims;
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
public class ProgressController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ActionPlanApi.Services.ActivityStatusService _status;
    public ProgressController(AppDbContext db,
        ActionPlanApi.Services.ActivityStatusService status)
    {
        _db = db;
        _status = status;
    }

    // آیا فعالیت در این دوره فعال است؟ (برای نمایش/پنهان در ثبت پیشرفت)
    [HttpGet("is-active/{activityId}/{periodId}")]
    public async Task<IActionResult> IsActive(
        int activityId, int periodId, [FromQuery] bool isKpi)
    {
        var active = await _status.IsActiveInPeriod(activityId, periodId, isKpi);
        return Ok(new { active });
    }

    // صف تأیید برای یک دوره
    [HttpGet("approval-queue/{periodId}")]
    public async Task<IActionResult> ApprovalQueue(
        int periodId,
        [FromQuery] bool onlyMyTurn = false,
        [FromQuery] bool showAll = false,
        [FromQuery] bool includeInactive = false)
    {
        var userId = CurrentUserId;
        var userRoleCodes = await _db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Code)
            .ToListAsync();

        var period = await _db.Periods.FindAsync(periodId);
        if (period == null) return Ok(new List<ApprovalQueueItemDto>());
        var yearId = period.YearId;

        var activities = await _db.Activities.ToListAsync();
        var withTarget = (await _db.Targets
                .Where(t => t.YearId == yearId)
                .Select(t => t.ActivityId).Distinct().ToListAsync())
            .ToHashSet();

        var users = await _db.Users.ToDictionaryAsync(u => u.Id, u => u.FullName);
        var result = new List<ApprovalQueueItemDto>();

        foreach (var activity in activities)
        {
            if (!withTarget.Contains(activity.Id)) continue;
            var isKpi = activity.ActivityType == "kpi";
            if (!includeInactive &&
                !await _status.IsActiveInPeriod(activity.Id, periodId, isKpi))
                continue;

            var steps = await GetEffectiveSteps(activity.UnitId);

            bool userInChain = false;
            foreach (var s in steps)
            {
                if (await UserMatchesStep(s, userId, userRoleCodes))
                {
                    userInChain = true;
                    break;
                }
            }
            var isOwner = activity.OwnerUserId == userId;
            if (!showAll && !userInChain && !isOwner) continue;

            var entry = await _db.ProgressEntries.FirstOrDefaultAsync(e =>
                e.ActivityId == activity.Id && e.PeriodId == periodId);

            var ownerName = activity.OwnerUserId != null &&
                            users.ContainsKey(activity.OwnerUserId.Value)
                ? users[activity.OwnerUserId.Value] : null;

            double? targetThis = null;
            var target = await _db.Targets
                .Include(t => t.Periods)
                .FirstOrDefaultAsync(t =>
                    t.ActivityId == activity.Id && t.YearId == yearId);
            if (target != null)
            {
                var tp = target.Periods.FirstOrDefault(p => p.PeriodId == periodId);
                targetThis = tp?.TargetValue;
            }

            string status;
            int currentStepIndex;
            double progressValue;
            string? holderName;
            bool isMyTurn = false;
            int progressEntryId;

            if (entry == null || entry.Status == "draft")
            {
                status = "pending_owner";
                currentStepIndex = 0;
                progressValue = entry?.ProgressValue ?? 0;
                holderName = $"در دست مسئول: {ownerName ?? "نامشخص"}";
                progressEntryId = entry?.Id ?? -1;
            }
            else if (entry.Status == "final")
            {
                status = "final";
                currentStepIndex = entry.CurrentStepIndex;
                progressValue = entry.ProgressValue;
                holderName = null;
                progressEntryId = entry.Id;
            }
            else
            {
                status = "submitted";
                currentStepIndex = entry.CurrentStepIndex;
                progressValue = entry.ProgressValue;
                progressEntryId = entry.Id;
                if (steps.Count == 0)
                {
                    holderName = "تأیید نهایی (بدون زنجیره)";
                }
                else if (currentStepIndex < steps.Count)
                {
                    var currentStep = steps[currentStepIndex];
                    holderName = await StepHolderName(currentStep, users);
                    isMyTurn = await UserMatchesStep(currentStep, userId, userRoleCodes);
                }
                else
                {
                    holderName = null;
                }
            }

            if (onlyMyTurn && !isMyTurn) continue;

            result.Add(new ApprovalQueueItemDto
            {
                ProgressEntryId = progressEntryId,
                ActivityId = activity.Id,
                ActivityName = activity.Name,
                ActivityNumber = activity.ActivityNumber,
                UnitId = activity.UnitId,
                ProgressValue = progressValue,
                TargetThis = targetThis,
                Weight = activity.Weight,
                Status = status,
                CurrentStepIndex = currentStepIndex,
                CurrentHolderName = holderName,
                IsMyTurn = isMyTurn,
            });
        }

        return Ok(result);
    }

    private async Task<bool> UserMatchesStep(
        ApprovalStep step, int userId, List<string> userRoleCodes)
    {
        if (step.UserId != null)
            return step.UserId == userId;
        if (step.RoleId == null) return false;
        var role = await _db.Roles.FindAsync(step.RoleId.Value);
        if (role == null) return false;
        return userRoleCodes.Contains(role.Code);
    }

    private async Task<string> StepHolderName(
        ApprovalStep step, Dictionary<int, string> users)
    {
        if (step.UserId != null)
            return users.ContainsKey(step.UserId.Value)
                ? users[step.UserId.Value] : "کاربر";
        if (step.RoleId != null)
        {
            var r = await _db.Roles.FindAsync(step.RoleId.Value);
            return r != null ? $"نقش: {r.Title}" : "تأییدکننده";
        }
        return "تأییدکننده";
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue("uid") ?? "0");
    private string CurrentUserName =>
        User.FindFirstValue("fullName") ?? "";

    // پیشرفت یک فعالیت در یک سال (همه‌ی دوره‌ها)
    [HttpGet("{activityId}/year/{yearId}")]
    public async Task<IActionResult> GetForActivityYear(int activityId, int yearId)
    {
        var periodIds = await _db.Periods
            .Where(p => p.YearId == yearId)
            .Select(p => p.Id).ToListAsync();
        var entries = await _db.ProgressEntries
            .Where(e => e.ActivityId == activityId && periodIds.Contains(e.PeriodId))
            .ToListAsync();
        return Ok(entries.Select(ToDto));
    }

    [HttpGet("period/{periodId}")]
    public async Task<IActionResult> GetForPeriod(int periodId)
    {
        var entries = await _db.ProgressEntries
            .Where(e => e.PeriodId == periodId).ToListAsync();
        return Ok(entries.Select(ToDto));
    }

    // ذخیره‌ی پیش‌نویس
    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] SaveProgressRequest req)
    {
        var entry = await _db.ProgressEntries.FirstOrDefaultAsync(e =>
            e.ActivityId == req.ActivityId && e.PeriodId == req.PeriodId);

        double? oldValue = null;
        if (entry == null)
        {
            entry = new ProgressEntry
            {
                ActivityId = req.ActivityId, PeriodId = req.PeriodId,
                ProgressValue = req.ProgressValue, Note = req.Note,
                Status = "draft", CurrentStepIndex = 0,
                UpdatedAt = DateTime.UtcNow
            };
            _db.ProgressEntries.Add(entry);
        }
        else
        {
            if (entry.Status != "draft")
                return BadRequest(new { message = "این مورد قفل است و قابل ویرایش نیست" });
            oldValue = entry.ProgressValue; // مقدار قبلی پیش از تغییر
            entry.ProgressValue = req.ProgressValue;
            entry.Note = req.Note;
            entry.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();

        // فقط اگر مقدار واقعاً تغییر کرده، تاریخچه‌ی modify با مقدار قبلی ثبت شود
        var changed = oldValue == null || Math.Abs(oldValue.Value - req.ProgressValue) > 0.001;
        if (changed)
        {
            _db.ProgressHistory.Add(new ProgressHistory
            {
                ProgressEntryId = entry.Id, UserId = CurrentUserId,
                UserName = CurrentUserName, Action = "modify",
                OldValue = oldValue, NewValue = req.ProgressValue, Note = req.Note,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
        return Ok(new { id = entry.Id });
    }

    // ثبت و ارسال (شروع گردش کار)
    [HttpPost("submit")]
    public async Task<IActionResult> Submit([FromBody] SaveProgressRequest req)
    {
        var entry = await _db.ProgressEntries.FirstOrDefaultAsync(e =>
            e.ActivityId == req.ActivityId && e.PeriodId == req.PeriodId);
        if (entry == null)
        {
            entry = new ProgressEntry
            {
                ActivityId = req.ActivityId, PeriodId = req.PeriodId,
                ProgressValue = req.ProgressValue, Note = req.Note,
                UpdatedAt = DateTime.UtcNow
            };
            _db.ProgressEntries.Add(entry);
        }
        else
        {
            // مقدار موجود حفظ می‌شود؛ فقط اگر درخواست مقدار معتبر داشت به‌روز کن
            if (req.ProgressValue > 0) entry.ProgressValue = req.ProgressValue;
            if (req.Note != null) entry.Note = req.Note;
        }

        var activity = await _db.Activities.FindAsync(req.ActivityId);
        var steps = await GetEffectiveSteps(activity!.UnitId);
        if (steps.Count == 0)
        {
            entry.Status = "final";
        }
        else
        {
            entry.Status = "submitted";
            entry.CurrentStepIndex = 0;
        }
        entry.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _db.ProgressHistory.Add(new ProgressHistory
        {
            ProgressEntryId = entry.Id, UserId = CurrentUserId,
            UserName = CurrentUserName, Action = "submit",
            NewValue = entry.ProgressValue, Note = entry.Note,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return Ok();
    }

    // تأیید (و احتمالاً اصلاح مقدار)
    [HttpPost("approve")]
    public async Task<IActionResult> Approve([FromBody] ProgressActionRequest req)
    {
        var entry = await _db.ProgressEntries.FirstOrDefaultAsync(e =>
            e.ActivityId == req.ActivityId && e.PeriodId == req.PeriodId);
        if (entry == null || entry.Status != "submitted")
            return BadRequest(new { message = "این مورد در وضعیت تأیید نیست" });

        var oldVal = entry.ProgressValue;
        if (req.NewValue.HasValue && req.NewValue.Value != entry.ProgressValue)
        {
            entry.ProgressValue = req.NewValue.Value;
            _db.ProgressHistory.Add(new ProgressHistory
            {
                ProgressEntryId = entry.Id, UserId = CurrentUserId,
                UserName = CurrentUserName, Action = "modify",
                OldValue = oldVal, NewValue = req.NewValue.Value,
                CreatedAt = DateTime.UtcNow
            });
        }

        var activity = await _db.Activities.FindAsync(req.ActivityId);
        var steps = await GetEffectiveSteps(activity!.UnitId);

        entry.CurrentStepIndex++;
        string action;
        if (entry.CurrentStepIndex >= steps.Count)
        {
            entry.Status = "final";
            action = "final";
        }
        else
        {
            action = "approve";
        }
        entry.UpdatedAt = DateTime.UtcNow;

        _db.ProgressHistory.Add(new ProgressHistory
        {
            ProgressEntryId = entry.Id, UserId = CurrentUserId,
            UserName = CurrentUserName, Action = action,
            Note = req.Note, CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return Ok();
    }

    // رد (برگشت به مسئول یا یک مرحله عقب)
    [HttpPost("reject")]
    public async Task<IActionResult> Reject([FromBody] ProgressActionRequest req)
    {
        var entry = await _db.ProgressEntries.FirstOrDefaultAsync(e =>
            e.ActivityId == req.ActivityId && e.PeriodId == req.PeriodId);
        if (entry == null) return NotFound();

        string action;
        if (req.ToOwner)
        {
            entry.Status = "draft";
            entry.CurrentStepIndex = 0;
            action = "reject_owner";
        }
        else
        {
            entry.CurrentStepIndex =
                entry.CurrentStepIndex > 0 ? entry.CurrentStepIndex - 1 : 0;
            action = "reject_back";
        }
        entry.UpdatedAt = DateTime.UtcNow;

        _db.ProgressHistory.Add(new ProgressHistory
        {
            ProgressEntryId = entry.Id, UserId = CurrentUserId,
            UserName = CurrentUserName, Action = action,
            Note = req.Note, CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return Ok();
    }

    // بازگرداندن نهایی به جریان
    [HttpPost("reopen")]
    [Authorize(Roles = "super_admin,admin,admin2")]
    public async Task<IActionResult> Reopen([FromBody] ProgressActionRequest req)
    {
        var entry = await _db.ProgressEntries.FirstOrDefaultAsync(e =>
            e.ActivityId == req.ActivityId && e.PeriodId == req.PeriodId);
        if (entry == null || entry.Status != "final")
            return BadRequest(new { message = "این مورد نهایی نیست" });

        var activity = await _db.Activities.FindAsync(req.ActivityId);
        var steps = await GetEffectiveSteps(activity!.UnitId);
        entry.Status = "submitted";
        entry.CurrentStepIndex = steps.Count > 0 ? steps.Count - 1 : 0;
        entry.UpdatedAt = DateTime.UtcNow;

        _db.ProgressHistory.Add(new ProgressHistory
        {
            ProgressEntryId = entry.Id, UserId = CurrentUserId,
            UserName = CurrentUserName, Action = "reopen",
            Note = req.Note, CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("{progressEntryId}/history")]
    public async Task<IActionResult> GetHistory(int progressEntryId)
    {
        var rows = await _db.ProgressHistory
            .Where(h => h.ProgressEntryId == progressEntryId)
            .OrderBy(h => h.CreatedAt)
            .ToListAsync();
        return Ok(rows.Select(h => new ProgressHistoryDto
        {
            Id = h.Id, UserName = h.UserName, Action = h.Action,
            OldValue = h.OldValue, NewValue = h.NewValue,
            Note = h.Note, CreatedAt = h.CreatedAt
        }));
    }

    // مراحل مؤثر (خود واحد + والدها)
    // ===== اکشن‌های مبتنی بر progressEntryId (هماهنگ با فلاتر) =====
    [HttpPost("approve-modify")]
    public async Task<IActionResult> ApproveModify([FromBody] ApproveRequest req)
    {
        var entry = await _db.ProgressEntries.FindAsync(req.ProgressEntryId);
        if (entry == null) return NotFound();
        var activity = await _db.Activities.FindAsync(entry.ActivityId);
        if (activity == null) return NotFound();
        var steps = await GetEffectiveSteps(activity.UnitId);

        var oldValue = entry.ProgressValue;
        var changed = Math.Abs(oldValue - req.NewValue) > 0.001;
        if (changed)
        {
            _db.ProgressHistory.Add(new ProgressHistory
            {
                ProgressEntryId = req.ProgressEntryId, UserId = CurrentUserId,
                UserName = CurrentUserName, Action = "modify",
                OldValue = oldValue, NewValue = req.NewValue, Note = req.Note,
                CreatedAt = DateTime.UtcNow
            });
        }
        _db.ProgressHistory.Add(new ProgressHistory
        {
            ProgressEntryId = req.ProgressEntryId, UserId = CurrentUserId,
            UserName = CurrentUserName, Action = "approve", Note = req.Note,
            CreatedAt = DateTime.UtcNow
        });

        var nextStep = req.StepIndex + 1;
        var isFinal = nextStep >= steps.Count;
        entry.ProgressValue = req.NewValue;
        entry.CurrentStepIndex = isFinal ? req.StepIndex : nextStep;
        entry.Status = isFinal ? "final" : "submitted";
        entry.UpdatedAt = DateTime.UtcNow;

        if (isFinal)
        {
            _db.ProgressHistory.Add(new ProgressHistory
            {
                ProgressEntryId = req.ProgressEntryId, UserId = CurrentUserId,
                UserName = CurrentUserName, Action = "final",
                NewValue = req.NewValue, CreatedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("reject-entry")]
    public async Task<IActionResult> RejectEntry([FromBody] RejectRequest req)
    {
        var entry = await _db.ProgressEntries.FindAsync(req.ProgressEntryId);
        if (entry == null) return NotFound();

        string action;
        if (req.ToOwner || req.StepIndex <= 0)
        {
            entry.Status = "draft";
            entry.CurrentStepIndex = 0;
            action = "reject_owner";
        }
        else
        {
            entry.CurrentStepIndex = req.StepIndex - 1;
            action = "reject_back";
        }
        entry.UpdatedAt = DateTime.UtcNow;
        _db.ProgressHistory.Add(new ProgressHistory
        {
            ProgressEntryId = req.ProgressEntryId, UserId = CurrentUserId,
            UserName = CurrentUserName, Action = action, Note = req.Note,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("reopen-entry")]
    [Authorize(Roles = "super_admin,admin,admin2")]
    public async Task<IActionResult> ReopenEntry([FromBody] ReopenRequest req)
    {
        var entry = await _db.ProgressEntries.FindAsync(req.ProgressEntryId);
        if (entry == null || entry.Status != "final")
            return BadRequest(new { message = "این مورد نهایی نیست" });
        var activity = await _db.Activities.FindAsync(entry.ActivityId);
        var steps = await GetEffectiveSteps(activity!.UnitId);
        entry.Status = "submitted";
        entry.CurrentStepIndex = steps.Count > 0 ? steps.Count - 1 : 0;
        entry.UpdatedAt = DateTime.UtcNow;
        _db.ProgressHistory.Add(new ProgressHistory
        {
            ProgressEntryId = req.ProgressEntryId, UserId = CurrentUserId,
            UserName = CurrentUserName, Action = "reopen", Note = req.Note,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return Ok();
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

    private static ProgressEntryDto ToDto(ProgressEntry e) => new()
    {
        Id = e.Id, ActivityId = e.ActivityId, PeriodId = e.PeriodId,
        ProgressValue = e.ProgressValue, Note = e.Note,
        Status = e.Status, CurrentStepIndex = e.CurrentStepIndex
    };
}
