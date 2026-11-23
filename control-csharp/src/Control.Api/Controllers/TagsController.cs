using System.Security.Claims;
using Control.Api.Mapping;
using Control.Domain.Dtos;
using Control.Domain.Entities;
using Control.Infrastructure.Data;
using Control.Infrastructure.Mqtt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Control.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/tags")]
public sealed class TagsController : ControllerBase
{
    private readonly ControlDbContext _db;
    private readonly MqttService _mqtt;
    private readonly ILogger<TagsController> _logger;

    public TagsController(ControlDbContext db, MqttService mqtt, ILogger<TagsController> logger)
    {
        _db = db;
        _mqtt = mqtt;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TagDto>>> GetAll()
    {
        var tags = await _db.Tags
            .Include(x => x.Snapshot)
            .OrderBy(x => x.PlcId)
            .ThenBy(x => x.Address)
            .ToListAsync();
        return Ok(tags.Select(DtoMapping.ToDto).ToList());
    }

    [Authorize(Roles = "Admin,Engineer")]
    [HttpPut("{id}")]
    public async Task<ActionResult<TagDto>> Update(string id, [FromBody] UpdateTagRequest request)
    {
        var tag = await _db.Tags
            .Include(x => x.Plc)
            .ThenInclude(x => x!.Gateway)
            .Include(x => x.Snapshot)
            .SingleOrDefaultAsync(x => x.Id == id);
        if (tag is null) return NotFound();

        if (request.Writable is { } writable) tag.Writable = writable;
        if (request.MinValue is { } min) tag.MinValue = min;
        if (request.MaxValue is { } max) tag.MaxValue = max;
        if (request.AlarmHigh is { } high) tag.AlarmHigh = high;
        if (request.AlarmLow is { } low) tag.AlarmLow = low;
        if (request.Hysteresis is { } hysteresis) tag.Hysteresis = Math.Max(0, hysteresis);

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Action = "tag.update",
            EntityType = "Tag",
            EntityId = tag.Id,
            Detail = $"Writable={tag.Writable}; AlarmHigh={tag.AlarmHigh}; AlarmLow={tag.AlarmLow}"
        });
        await _db.SaveChangesAsync();

        if (tag.Plc?.Gateway is { } gateway)
        {
            try
            {
                await _mqtt.PublishAsync($"plc/{gateway.Id}/config", new
                {
                    schema = "v1",
                    gateway_id = gateway.Id,
                    plc_id = tag.PlcId,
                    cycle_ms = 300,
                    tags = new[]
                    {
                        new { address = tag.Address, data_type = tag.DataType, writable = tag.Writable }
                    }
                }, retain: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không publish được config cho tag {Tag}", tag.Id);
            }
        }

        return Ok(DtoMapping.ToDto(tag));
    }
}
