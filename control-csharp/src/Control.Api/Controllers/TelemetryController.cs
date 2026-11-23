using Control.Domain.Dtos;
using Control.Infrastructure.Data;
using Control.Infrastructure.Telemetry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Control.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/telemetry")]
public sealed class TelemetryController : ControllerBase
{
    private readonly ControlDbContext _db;
    private readonly ITelemetryStore _store;

    public TelemetryController(ControlDbContext db, ITelemetryStore store)
    {
        _db = db;
        _store = store;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<HistoryPointDto>>> History(
        [FromQuery] string tagId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var tag = await _db.Tags.SingleOrDefaultAsync(x => x.Id == tagId, cancellationToken);
        if (tag is null) return NotFound(new { error = "Không tìm thấy tag." });

        var start = from ?? DateTimeOffset.UtcNow.AddHours(-1);
        var end = to ?? DateTimeOffset.UtcNow;
        if (start >= end) return BadRequest(new { error = "Khoảng thời gian không hợp lệ." });
        if (end - start > TimeSpan.FromDays(31)) return BadRequest(new { error = "Tối đa 31 ngày." });

        var points = await _store.QueryAsync(tag.Address, start, end, cancellationToken);
        return Ok(points);
    }
}
