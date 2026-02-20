using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Budgetr.Web;
using Budgetr.Web.Services;
using Budgetr.Shared.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddLocalization();


builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Register services
builder.Services.AddScoped<IStorageService, BrowserStorageService>();
builder.Services.AddScoped<IMeterConfigurationService, MeterConfigurationService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<ITimeTrackingService, TimeTrackingService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<SupabaseService>();
builder.Services.AddScoped<ISyncProvider, SupabaseSyncProvider>();
builder.Services.AddScoped<IAutoSyncService, AutoSyncService>();
builder.Services.AddScoped<IPwaService, PwaService>();
builder.Services.AddScoped<ITutorialService, TutorialService>();
builder.Services.AddScoped<ITimeularService, TimeularService>();
// Load configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

var host = builder.Build();

// Load saved data on startup
var settingsService = host.Services.GetRequiredService<ISettingsService>();
await settingsService.LoadAsync();

var timeService = host.Services.GetRequiredService<ITimeTrackingService>();
await timeService.LoadAsync();

await host.RunAsync();
