using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Kairos.Web;
using Kairos.Web.Services;
using Kairos.Shared.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddLocalization();


builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.Configure<SupabaseAuthOptions>(builder.Configuration.GetSection("Supabase"));

// Register services
builder.Services.AddScoped<IStorageService, BrowserStorageService>();
builder.Services.AddScoped<IActivityConfigurationService, ActivityConfigurationService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<ITimeTrackingService, TimeTrackingService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IPwaService, PwaService>();
builder.Services.AddScoped<ITutorialService, TutorialService>();
builder.Services.AddScoped<ITimeularService, TimeularService>();
builder.Services.AddScoped<ISupabaseAuthService, SupabaseAuthService>();
builder.Services.AddScoped<ISupabaseActivityStore, SupabaseActivityStore>();
// Load configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

var host = builder.Build();

// Load saved data on startup
var settingsService = host.Services.GetRequiredService<ISettingsService>();
await settingsService.LoadAsync();

var authService = host.Services.GetRequiredService<ISupabaseAuthService>();
await authService.InitializeAsync();

var timeService = host.Services.GetRequiredService<ITimeTrackingService>();
await timeService.LoadAsync();

await host.RunAsync();
