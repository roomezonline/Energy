using EnergyMonitor.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<EnergyMonitor.Client.App>("#app");
builder.RootComponents.Add<Microsoft.AspNetCore.Components.Web.HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<TokenStorage>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton<ToastService>();

var host = builder.Build();

// Try restoring saved auth session
var auth = host.Services.GetRequiredService<AuthService>();
await auth.TryLoadFromStorageAsync();

await host.RunAsync();
