using System.Net.Http;
using System.Net.Http.Json;
using Control.Domain.Dtos;

namespace Control.Desktop;

public sealed class ApiClient
{
    private readonly HttpClient _http = new();
    public string BaseUrl { get; set; } = "http://localhost:5081";
    public string? Token { get; set; }

    public async Task<LoginResponse> LoginAsync(string username, string password)
    {
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));
        await EnsureSuccess(response);
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
    }

    public async Task<List<GatewayDto>> GetGatewaysAsync() => await GetAsync<List<GatewayDto>>("/api/gateways");
    public async Task<List<TagDto>> GetTagsAsync() => await GetAsync<List<TagDto>>("/api/tags");
    public async Task<List<AlarmDto>> GetAlarmsAsync() => await GetAsync<List<AlarmDto>>("/api/alarms?activeOnly=false");
    public async Task<List<CommandDto>> GetCommandsAsync() => await GetAsync<List<CommandDto>>("/api/commands");

    public async Task<AlarmDto> AckAlarmAsync(Guid id)
    {
        using var client = CreateClient();
        var response = await client.PostAsync($"/api/alarms/{id}/ack", null);
        await EnsureSuccess(response);
        return (await response.Content.ReadFromJsonAsync<AlarmDto>())!;
    }

    public async Task<CommandDto> CreateCommandAsync(string tagId, string value)
    {
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/commands", new CreateCommandRequest(tagId, value));
        await EnsureSuccess(response);
        return (await response.Content.ReadFromJsonAsync<CommandDto>())!;
    }

    private async Task<T> GetAsync<T>(string path)
    {
        using var client = CreateClient();
        var response = await client.GetAsync(path);
        await EnsureSuccess(response);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private HttpClient CreateClient()
    {
        var client = new HttpClient { BaseAddress = new Uri(BaseUrl.TrimEnd('/') + "/") };
        if (!string.IsNullOrWhiteSpace(Token))
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);
        return client;
    }

    private static async Task EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var text = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(text)
            ? $"HTTP {(int)response.StatusCode}"
            : text);
    }
}
