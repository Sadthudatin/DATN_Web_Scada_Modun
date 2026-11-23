using System.Security.Claims;
using Control.Api.Hubs;
using Control.Api.Mapping;
using Control.Domain.Dtos;
using Control.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Control.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/alarms")]
public sealed class AlarmsController : ControllerBase
{
    private readonly ControlDbContext _db;
    private readonly IHubContext<ScadaHub> _hub;

    public AlarmsController(ControlDbContext db, IHubContext<ScadaHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AlarmDto>>> Get([FromQuery] bool activeOnly = false)
    {
        var query = _db.Alarms.Include(x => x.Tag).AsQueryable();
        if (activeOnly) query = query.Where(x => x.IsActive);
        var items = await query.Take(500).ToListAsync();
        return Ok(items
            .OrderByDescending(x => x.StartedAt)
            .Take(200)
            .Select(DtoMapping.ToDto)
            .ToList());
    }

    [Authorize(Roles = "Admin,Engineer,Operator")]
    [HttpPost("{id:guid}/ack")]
    public async Task<ActionResult<AlarmDto>> Acknowledge(Guid id)
    {
        var alarm = await _db.Alarms.Include(x => x.Tag).SingleOrDefaultAsync(x => x.Id == id);
        if (alarm is null) return NotFound();

        alarm.AckAt = DateTimeOffset.UtcNow;
        alarm.AckBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _db.SaveChangesAsync();
        var dto = DtoMapping.ToDto(alarm);
        await _hub.Clients.All.SendAsync("alarm", dto);
        return Ok(dto);
    }
}
