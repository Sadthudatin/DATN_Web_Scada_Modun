using Control.Api.Hubs;
using Control.Api.Mapping;
using Control.Domain.Entities;
using Control.Domain.Rules;
using Control.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Control.Api.Services;

public sealed class AlarmService
{
    private readonly ControlDbContext _db;
    private readonly IHubContext<ScadaHub> _hub;

    public AlarmService(ControlDbContext db, IHubContext<ScadaHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    public async Task EvaluateAsync(Tag tag, double? numericValue, CancellationToken cancellationToken = default)
    {
        var active = await _db.Alarms
            .Include(x => x.Tag)
            .FirstOrDefaultAsync(x => x.TagId == tag.Id && x.IsActive, cancellationToken);

        var decision = AlarmRules.Evaluate(tag, numericValue, active is not null);
        if (!decision.Changed) return;

        if (decision.ShouldBeActive)
        {
            var alarm = new AlarmEvent
            {
                TagId = tag.Id,
                Tag = tag,
                Severity = decision.Severity ?? "Warning",
                Message = decision.Message ?? $"Cảnh báo tag {tag.Name}",
                StartedAt = DateTimeOffset.UtcNow,
                IsActive = true
            };
            _db.Alarms.Add(alarm);
            await _db.SaveChangesAsync(cancellationToken);
            await _hub.Clients.All.SendAsync("alarm", DtoMapping.ToDto(alarm), cancellationToken);
            return;
        }

        if (active is not null)
        {
            active.IsActive = false;
            active.ClearedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            await _hub.Clients.All.SendAsync("alarm", DtoMapping.ToDto(active), cancellationToken);
        }
    }
}
