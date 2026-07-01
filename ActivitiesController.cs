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
public class ActivitiesController : ControllerBase
{
    private readonly AppDbContext _db;
    public ActivitiesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var activities = await _db.Activities
            .Include(a => a.Collaborators)
            .ToListAsync();
        var users = await _db.Users.ToDictionaryAsync(u => u.Id, u => u.FullName);
        return Ok(activities.Select(a => ToDto(a, users)));
    }

    [HttpGet("owned-by/{userId}")]
    public async Task<IActionResult> GetOwnedBy(int userId)
    {
        var activities = await _db.Activities
            .Include(a => a.Collaborators)
            .Where(a => a.OwnerUserId == userId)
            .ToListAsync();
        var users = await _db.Users.ToDictionaryAsync(u => u.Id, u => u.FullName);
        return Ok(activities.Select(a => ToDto(a, users)));
    }

    [HttpPost]
    [Authorize(Roles = "super_admin,admin,admin2")]
    public async Task<IActionResult> Create([FromBody] ActivityDto dto)
    {
        if (!string.IsNullOrEmpty(dto.ActivityNumber) &&
            await _db.Activities.AnyAsync(a => a.ActivityNumber == dto.ActivityNumber))
            return BadRequest(new { message = "این شماره‌ی فعالیت قبلاً استفاده شده" });

        var a = new Activity
        {
            UnitId = dto.UnitId,
            ActivityNumber = dto.ActivityNumber,
            Name = dto.Name,
            ActivityType = dto.ActivityType,
            Weight = dto.Weight,
            OwnerUserId = dto.OwnerUserId,
            Description = dto.Description,
            IsActive = dto.IsActive
        };
        _db.Activities.Add(a);
        await _db.SaveChangesAsync();
        await SetCollaborators(a.Id, dto.Collaborators);
        return Ok(new { id = a.Id });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "super_admin,admin,admin2")]
    public async Task<IActionResult> Update(int id, [FromBody] ActivityDto dto)
    {
        var a = await _db.Activities.FindAsync(id);
        if (a == null) return NotFound();

        if (!string.IsNullOrEmpty(dto.ActivityNumber) &&
            await _db.Activities.AnyAsync(x =>
                x.ActivityNumber == dto.ActivityNumber && x.Id != id))
            return BadRequest(new { message = "این شماره‌ی فعالیت قبلاً استفاده شده" });

        a.UnitId = dto.UnitId;
        a.ActivityNumber = dto.ActivityNumber;
        a.Name = dto.Name;
        a.ActivityType = dto.ActivityType;
        a.Weight = dto.Weight;
        a.OwnerUserId = dto.OwnerUserId;
        a.Description = dto.Description;
        await _db.SaveChangesAsync();
        await SetCollaborators(id, dto.Collaborators);
        return Ok();
    }

    [HttpPut("{id}/active")]
    [Authorize(Roles = "super_admin,admin,admin2")]
    public async Task<IActionResult> SetActive(int id, [FromQuery] bool active)
    {
        var a = await _db.Activities.FindAsync(id);
        if (a == null) return NotFound();
        a.IsActive = active;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "super_admin,admin,admin2")]
    public async Task<IActionResult> Delete(int id)
    {
        var a = await _db.Activities.FindAsync(id);
        if (a == null) return NotFound();
        _db.Activities.Remove(a);
        await _db.SaveChangesAsync();
        return Ok();
    }

    private async Task SetCollaborators(int activityId, List<CollaboratorDto> collabs)
    {
        var existing = _db.ActivityCollaborators.Where(c => c.ActivityId == activityId);
        _db.ActivityCollaborators.RemoveRange(existing);
        foreach (var c in collabs)
            _db.ActivityCollaborators.Add(new ActivityCollaborator
            {
                ActivityId = activityId, UnitId = c.UnitId, Weight = c.Weight
            });
        await _db.SaveChangesAsync();
    }

    private static ActivityDto ToDto(Activity a, Dictionary<int, string> users) => new()
    {
        Id = a.Id, UnitId = a.UnitId, ActivityNumber = a.ActivityNumber,
        Name = a.Name, ActivityType = a.ActivityType, Weight = a.Weight,
        OwnerUserId = a.OwnerUserId,
        OwnerName = a.OwnerUserId != null && users.ContainsKey(a.OwnerUserId.Value)
            ? users[a.OwnerUserId.Value] : null,
        Description = a.Description, IsActive = a.IsActive,
        Collaborators = a.Collaborators.Select(c => new CollaboratorDto
        {
            UnitId = c.UnitId, Weight = c.Weight
        }).ToList()
    };
}
