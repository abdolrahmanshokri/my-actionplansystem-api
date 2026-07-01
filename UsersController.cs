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
[Authorize(Roles = "super_admin,admin")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    public UsersController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .ToListAsync();

        var result = users.Select(u => new UserDto
        {
            Id = u.Id,
            Username = u.Username,
            FullName = u.FullName,
            IsActive = u.IsActive,
            Roles = u.UserRoles.Where(r => r.Role != null)
                .Select(r => r.Role!.Code).ToList()
        }).ToList();

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req)
    {
        if (await _db.Users.AnyAsync(u => u.Username == req.Username))
            return BadRequest(new { message = "این نام کاربری قبلاً وجود دارد" });

        var user = new User
        {
            Username = req.Username,
            FullName = req.FullName,
            IsActive = req.IsActive,
            PasswordHash = PasswordHasher.Hash(
                string.IsNullOrEmpty(req.Password) ? "1234" : req.Password)
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await SetRoles(user.Id, req.Roles);
        return Ok(new { id = user.Id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest req)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.FullName = req.FullName;
        user.IsActive = req.IsActive;
        await _db.SaveChangesAsync();

        await SetRoles(id, req.Roles);
        return Ok();
    }

    [HttpPut("{id}/password")]
    public async Task<IActionResult> ChangePassword(
        int id, [FromBody] ChangePasswordRequest req)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
        user.PasswordHash = PasswordHasher.Hash(req.NewPassword);
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return Ok();
    }

    private async Task SetRoles(int userId, List<string> roleCodes)
    {
        var existing = _db.UserRoles.Where(ur => ur.UserId == userId);
        _db.UserRoles.RemoveRange(existing);

        var roles = await _db.Roles
            .Where(r => roleCodes.Contains(r.Code)).ToListAsync();
        foreach (var r in roles)
            _db.UserRoles.Add(new UserRole { UserId = userId, RoleId = r.Id });

        await _db.SaveChangesAsync();
    }
}
