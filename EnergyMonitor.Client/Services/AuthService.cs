using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;

namespace EnergyMonitor.Client.Services;

public class AuthService
{
    private readonly HttpClient _http;
    private readonly NavigationManager _nav;
    private readonly TokenStorage _storage;

    public UserInfo? CurrentUser { get; private set; }
    public bool IsLoggedIn => CurrentUser is not null;
    public event Action? StateChanged;

    public AuthService(HttpClient http, NavigationManager nav, TokenStorage storage)
    {
        _http = http;
        _nav = nav;
        _storage = storage;
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/v2/auth/login", new { username, password });
            if (!response.IsSuccessStatusCode) return false;

            var json = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (json is null || string.IsNullOrEmpty(json.Token)) return false;

            await _storage.SetTokenAsync(json.Token);
            _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", json.Token);
            CurrentUser = new UserInfo { UserId = json.UserId, Username = json.Username, FullName = json.FullName, Role = json.Role, CenterId = json.CenterId, CenterIds = json.CenterIds };
            StateChanged?.Invoke();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task TryLoadFromStorageAsync()
    {
        var token = await _storage.GetTokenAsync();
        if (string.IsNullOrEmpty(token)) return;

        _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        try
        {
            var me = await _http.GetFromJsonAsync<JsonElement>("api/v2/auth/me");
            CurrentUser = new UserInfo
            {
                UserId = me.GetProperty("id").GetGuid(),
                Username = me.GetProperty("username").GetString() ?? "",
                FullName = me.TryGetProperty("fullName", out var fn) ? fn.GetString() ?? "" : "",
                Role = me.GetProperty("role").GetString() ?? "",
            };
            if (me.TryGetProperty("centerId", out var cid) && cid.ValueKind == JsonValueKind.String)
                CurrentUser.CenterId = cid.GetGuid();
            if (me.TryGetProperty("centerIds", out var cids) && cids.ValueKind == JsonValueKind.Array)
                CurrentUser.CenterIds = JsonSerializer.Deserialize<List<Guid>>(cids.GetRawText()) ?? new();
            StateChanged?.Invoke();
        }
        catch
        {
            await LogoutAsync();
        }
    }

    public async Task LogoutAsync()
    {
        await _storage.RemoveTokenAsync();
        CurrentUser = null;
        _http.DefaultRequestHeaders.Authorization = null;
        StateChanged?.Invoke();
        _nav.NavigateTo("/login", true);
    }

    public bool IsInRole(string role)
    {
        if (CurrentUser?.Role is null) return false;
        if (CurrentUser.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase)) return true;
        return CurrentUser.Role.Equals(role, StringComparison.OrdinalIgnoreCase);
    }
}

public class UserInfo
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Role { get; set; } = "";
    public Guid? CenterId { get; set; }
    public List<Guid> CenterIds { get; set; } = new();
}

public class LoginResponse
{
    public string Token { get; set; } = "";
    public Guid UserId { get; set; }
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Role { get; set; } = "";
    public Guid? CenterId { get; set; }
    public List<Guid> CenterIds { get; set; } = new();
}
