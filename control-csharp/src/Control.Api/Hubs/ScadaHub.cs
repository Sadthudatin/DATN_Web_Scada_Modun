using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Control.Api.Hubs;

[Authorize]
public sealed class ScadaHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("connected", new { at = DateTimeOffset.UtcNow });
        await base.OnConnectedAsync();
    }
}
