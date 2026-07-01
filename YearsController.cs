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
public class YearsController : ControllerBase
{
    private readonly AppDbContext _db;
    public YearsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var years = await _db.Years.OrderByDescending(y => y.YearValue).ToListAsync();
        return Ok(years.Select(ToDto));
    }

    [HttpGet("{yearId}/periods")]
    public async Task<IActionResult> GetPeriods(int yearId)
    {
        var periods = await _db.Periods
            .Where(p => p.YearId == yearId)
            .OrderBy(p => p.MonthNumber)
            .ToListAsync();
        return Ok(periods.Select(ToPeriodDto));
    }

    [HttpGet("open-periods")]
    public async Task<IActionResult> GetOpenPeriods()
    {
        var periods = await _db.Periods
            .Where(p => p.IsOpen)
            .OrderBy(p => p.MonthNumber)
            .ToListAsync();
        return Ok(periods.Select(ToPeriodDto));
    }

    [HttpPost]
    [Authorize(Roles = "super_admin,admin,admin2")]
    public async Task<IActionResult> Create([FromBody] YearDto dto)
    {
        var y = new Year
        {
            YearValue = dto.YearValue,
            Title = dto.Title,
            StartJy = dto.StartJy, StartJm = dto.StartJm, StartJd = dto.StartJd,
            EndJy = dto.EndJy, EndJm = dto.EndJm, EndJd = dto.EndJd,
            IsClosed = dto.IsClosed
        };
        _db.Years.Add(y);
        await _db.SaveChangesAsync();
        return Ok(new { id = y.Id });
    }

    [HttpPost("{yearId}/periods")]
    [Authorize(Roles = "super_admin,admin,admin2")]
    public async Task<IActionResult> CreatePeriod(int yearId, [FromBody] PeriodDto dto)
    {
        var p = new Period
        {
            YearId = yearId,
            MonthNumber = dto.MonthNumber,
            MonthName = dto.MonthName,
            Title = dto.Title,
            IsOpen = dto.IsOpen
        };
        _db.Periods.Add(p);
        await _db.SaveChangesAsync();
        return Ok(new { id = p.Id });
    }

    [HttpPut("periods/{periodId}/toggle")]
    [Authorize(Roles = "super_admin,admin,admin2")]
    public async Task<IActionResult> TogglePeriod(int periodId, [FromQuery] bool open)
    {
        var p = await _db.Periods.FindAsync(periodId);
        if (p == null) return NotFound();
        p.IsOpen = open;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{yearId}")]
    [Authorize(Roles = "super_admin,admin,admin2")]
    public async Task<IActionResult> DeleteYear(int yearId)
    {
        var y = await _db.Years.FindAsync(yearId);
        if (y == null) return NotFound();
        _db.Years.Remove(y);
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("periods/{periodId}")]
    [Authorize(Roles = "super_admin,admin,admin2")]
    public async Task<IActionResult> DeletePeriod(int periodId)
    {
        var p = await _db.Periods.FindAsync(periodId);
        if (p == null) return NotFound();
        _db.Periods.Remove(p);
        await _db.SaveChangesAsync();
        return Ok();
    }

    private static YearDto ToDto(Year y) => new()
    {
        Id = y.Id, YearValue = y.YearValue, Title = y.Title,
        StartJy = y.StartJy, StartJm = y.StartJm, StartJd = y.StartJd,
        EndJy = y.EndJy, EndJm = y.EndJm, EndJd = y.EndJd,
        IsClosed = y.IsClosed
    };

    private static PeriodDto ToPeriodDto(Period p) => new()
    {
        Id = p.Id, YearId = p.YearId, MonthNumber = p.MonthNumber,
        MonthName = p.MonthName, Title = p.Title, IsOpen = p.IsOpen
    };
}
