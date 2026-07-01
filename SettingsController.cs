using ActionPlanApi.Data;
using ActionPlanApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ActionPlanApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly AppDbContext _db;
    public SettingsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var all = await _db.AppSettings.ToListAsync();
        return Ok(all.ToDictionary(s => s.Key, s => s.Value));
    }

    [HttpGet("{key}")]
    public async Task<IActionResult> Get(string key)
    {
        var s = await _db.AppSettings.FirstOrDefaultAsync(x => x.Key == key);
        return Ok(new { key, value = s?.Value });
    }

    [HttpPut("{key}")]
    [Authorize(Roles = "super_admin,admin")]
    public async Task<IActionResult> Set(
        string key, [FromBody] ActionPlanApi.Dtos.SettingValueDto dto)
    {
        var value = dto.Value;
        var s = await _db.AppSettings.FirstOrDefaultAsync(x => x.Key == key);
        if (s == null)
        {
            s = new AppSetting { Key = key, Value = value };
            _db.AppSettings.Add(s);
        }
        else
        {
            s.Value = value;
        }
        await _db.SaveChangesAsync();
        return Ok();
    }

    // متن‌های ظاهری (بدون نیاز به توکن) — برای صفحه‌ی لاگین
    [HttpGet("public-branding")]
    [AllowAnonymous]
    public async Task<IActionResult> PublicBranding()
    {
        var keys = new[]
        {
            "app_title", "app_slogan", "browser_title", "system_name",
            "header_logo", "browser_favicon", "sso_enabled", "ad_enabled"
        };
        var rows = await _db.AppSettings
            .Where(s => keys.Contains(s.Key))
            .ToListAsync();
        return Ok(rows.ToDictionary(s => s.Key, s => s.Value));
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly AppDbContext _db;
    public RolesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var roles = await _db.Roles.ToListAsync();
        return Ok(roles.Select(r => new { r.Id, r.Code, r.Title }));
    }
}
