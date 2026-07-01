using ActionPlanApi.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ActionPlanApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    public HealthController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var canConnect = await _db.Database.CanConnectAsync();
        var roleCount = await _db.Roles.CountAsync();
        var userCount = await _db.Users.CountAsync();
        return Ok(new
        {
            status = "ok",
            environment = _env.EnvironmentName,
            server = _db.Database.GetDbConnection().DataSource,
            database = canConnect ? "connected" : "disconnected",
            roles = roleCount,
            users = userCount,
            serverTime = DateTime.Now
        });
    }
}
