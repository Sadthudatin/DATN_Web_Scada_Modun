using Control.Api.Hubs;
using Control.Api.Mapping;
using Control.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Control.Api.BackgroundServices;

public sealed class CommandTimeoutService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<ScadaHub> _hub;
    private readonly ILogger<CommandTimeoutService> _logger;

    public CommandTimeoutService(
        IServiceScopeFactory scopeFactory,
        IHubContext<ScadaHub> hub,
        ILogger<CommandTimeoutService> logger)
    {
        _scopeFactory = scopeFactory;
        _hub = hub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ControlDbContext>();
                var now = DateTimeOffset.UtcNow;
                var pending = await db.Commands
                    .Where(x => x.Status == "pending")
                    .ToListAsync(stoppingToken);

                var changed = false;
                foreach (var command in pending)
                {
                    if (now - command.RequestedAt <= TimeSpan.FromMilliseconds(command.TtlMs)) continue;
                    command.Status = "failed";
                    command.Error = "Timeout: không nhận được ACK từ Gateway.";
                    changed = true;
                    await _hub.Clients.All.SendAsync("commandStatus", DtoMapping.ToDto(command), stoppingToken);
                }

                if (changed) await db.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Command timeout worker lỗi");
            }
        }
    }
}

public sealed class GatewayOfflineService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<ScadaHub> _hub;
    private readonly ILogger<GatewayOfflineService> _logger;

    public GatewayOfflineService(
        IServiceScopeFactory scopeFactory,
        IHubContext<ScadaHub> hub,
        ILogger<GatewayOfflineService> logger)
    {
        _scopeFactory = scopeFactory;
        _hub = hub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ControlDbContext>();
                var cutoff = DateTimeOffset.UtcNow.AddSeconds(-10);
                var online = await db.Gateways
                    .Where(x => x.Status == "online")
                    .ToListAsync(stoppingToken);
                var stale = online.Where(x => x.LastSeen < cutoff).ToList();

                foreach (var gateway in stale)
                {
                    gateway.Status = "offline";
                    await _hub.Clients.All.SendAsync("gatewayStatus", new
                    {
                        id = gateway.Id,
                        status = gateway.Status,
                        lastSeen = gateway.LastSeen
                    }, stoppingToken);
                }

                if (stale.Count > 0) await db.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, " Gateway offline worker lỗi");
            }
        }
    }
}
