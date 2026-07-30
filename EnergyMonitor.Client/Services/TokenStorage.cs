using Microsoft.JSInterop;

namespace EnergyMonitor.Client.Services;

public class TokenStorage
{
    private readonly IJSRuntime _js;

    public TokenStorage(IJSRuntime js) => _js = js;

    public async Task<string?> GetTokenAsync()
        => await _js.InvokeAsync<string?>("localStorage.getItem", "jwt_token");

    public async Task SetTokenAsync(string token)
        => await _js.InvokeVoidAsync("localStorage.setItem", "jwt_token", token);

    public async Task RemoveTokenAsync()
        => await _js.InvokeVoidAsync("localStorage.removeItem", "jwt_token");
}
