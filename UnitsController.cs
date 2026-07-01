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
public class UnitsController : ControllerBase
{
    private readonly AppDbContext _db;
    public UnitsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var units = await _db.Units.OrderBy(u => u.SortOrder).ToListAsync();
        return Ok(units.Select(u => new UnitDto
        {
            Id = u.Id, ParentId = u.ParentId, Name = u.Name, SortOrder = u.SortOrder
        }));
    }

    [HttpPost]
    [Authorize(Roles = "super_admin,admin,admin2")]
    public async Task<IActionResult> Create([FromBody] UnitDto dto)
    {
        var u = new Unit { ParentId = dto.ParentId, Name = dto.Name, SortOrder = dto.SortOrder };
        _db.Units.Add(u);
        await _db.SaveChangesAsync();
        return Ok(new { id = u.Id });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "super_admin,admin,admin2")]
    public async Task<IActionResult> Update(int id, [FromBody] UnitDto dto)
    {
        var u = await _db.Units.FindAsync(id);
        if (u == null) return NotFound();
        u.Name = dto.Name;
        u.SortOrder = dto.SortOrder;
        u.ParentId = dto.ParentId;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "super_admin,admin,admin2")]
    public async Task<IActionResult> Delete(int id)
    {
        // چک: واحد فعالیت دارد؟
        if (await _db.Activities.AnyAsync(a => a.UnitId == id))
            return BadRequest(new { message = "این واحد فعالیت دارد و قابل حذف نیست" });
        // چک: زیرواحد دارد؟
        if (await _db.Units.AnyAsync(u => u.ParentId == id))
            return BadRequest(new { message = "این واحد زیرواحد دارد و ابتدا باید آن‌ها حذف شوند" });

        var u = await _db.Units.FindAsync(id);
        if (u == null) return NotFound();
        // حذف مراحل تأیید این واحد
        var steps = _db.ApprovalSteps.Where(s => s.UnitId == id);
        _db.ApprovalSteps.RemoveRange(steps);
        _db.Units.Remove(u);
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("{unitId}/has-activities")]
    public async Task<IActionResult> HasActivities(int unitId)
    {
        var has = await _db.Activities.AnyAsync(a => a.UnitId == unitId);
        return Ok(new { hasActivities = has });
    }

    // ===== زنجیره‌ی تأیید =====
    [HttpGet("{unitId}/approval-steps")]
    public async Task<IActionResult> GetSteps(int unitId)
    {
        var steps = await _db.ApprovalSteps
            .Where(s => s.UnitId == unitId)
            .OrderBy(s => s.StepOrder)
            .ToListAsync();
        return Ok(steps.Select(ToStepDto));
    }

    // مراحل مؤثر = خودِ واحد + همه‌ی والدها تا ریشه (تجمعی)
    [HttpGet("{unitId}/effective-steps")]
    public async Task<IActionResult> GetEffectiveSteps(int unitId)
    {
        var result = new List<ApprovalStepDto>();
        int? id = unitId;
        int guard = 0;
        var chainUnitIds = new List<int>();
        while (id != null && guard < 10)
        {
            chainUnitIds.Add(id.Value);
            var u = await _db.Units.FindAsync(id.Value);
            id = u?.ParentId;
            guard++;
        }
        // از خود واحد تا ریشه
        foreach (var uid in chainUnitIds)
        {
            var steps = await _db.ApprovalSteps
                .Where(s => s.UnitId == uid)
                .OrderBy(s => s.StepOrder)
                .ToListAsync();
            result.AddRange(steps.Select(ToStepDto));
        }
        return Ok(result);
    }

    [HttpPost("{unitId}/approval-steps")]
    [Authorize(Roles = "super_admin,admin,admin2")]
    public async Task<IActionResult> AddStep(int unitId, [FromBody] ApprovalStepDto dto)
    {
        var maxOrder = await _db.ApprovalSteps
            .Where(s => s.UnitId == unitId)
            .Select(s => (int?)s.StepOrder).MaxAsync() ?? -1;
        var step = new ApprovalStep
        {
            UnitId = unitId,
            StepOrder = maxOrder + 1,
            ApprovalType = dto.ApprovalType,
            RoleId = dto.RoleId,
            UserId = dto.UserId
        };
        _db.ApprovalSteps.Add(step);
        await _db.SaveChangesAsync();
        return Ok(new { id = step.Id });
    }

    [HttpDelete("approval-steps/{stepId}")]
    [Authorize(Roles = "super_admin,admin,admin2")]
    public async Task<IActionResult> DeleteStep(int stepId)
    {
        var s = await _db.ApprovalSteps.FindAsync(stepId);
        if (s == null) return NotFound();
        _db.ApprovalSteps.Remove(s);
        await _db.SaveChangesAsync();
        return Ok();
    }

    private static ApprovalStepDto ToStepDto(ApprovalStep s) => new()
    {
        Id = s.Id, UnitId = s.UnitId, StepOrder = s.StepOrder,
        ApprovalType = s.ApprovalType, RoleId = s.RoleId, UserId = s.UserId
    };
}
