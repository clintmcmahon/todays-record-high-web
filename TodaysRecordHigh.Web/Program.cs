using TodaysRecordHigh.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();

// /api/* is all public, read-only station data (no auth/cookies), so allowing any
// origin is safe — needed for the Expo web build and any browser-based client.
builder.Services.AddCors(options =>
{
    options.AddPolicy("ApiClients", policy =>
        policy.AllowAnyOrigin().WithMethods("GET").AllowAnyHeader());
});

// Station list — singleton, loaded once from JSON at startup
builder.Services.AddSingleton<IStationService, StationService>();

// RCC ACIS HTTP client — responses cached via IMemoryCache inside the service
builder.Services.AddHttpClient<IAcisService, AcisService>(client =>
{
    client.BaseAddress = new Uri("https://data.rcc-acis.org/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

// Open-Meteo current conditions — cached 20 min inside the service
builder.Services.AddHttpClient<ICurrentWeatherService, CurrentWeatherService>(client =>
{
    client.BaseAddress = new Uri("https://api.open-meteo.com/");
    client.Timeout = TimeSpan.FromSeconds(8);
});

// NWS active alerts — api.weather.gov requires an identifying User-Agent (no API key)
builder.Services.AddHttpClient<IWeatherAlertsService, WeatherAlertsService>(client =>
{
    client.BaseAddress = new Uri("https://api.weather.gov/");
    client.Timeout = TimeSpan.FromSeconds(8);
    client.DefaultRequestHeaders.Add("User-Agent", "TodaysRecordHigh.com (https://todaysrecordhigh.com)");
    client.DefaultRequestHeaders.Add("Accept", "application/geo+json");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("ApiClients");
app.UseAuthorization();

// Attribute-routed API controllers (e.g. /api/records/today)
app.MapControllers();

app.MapControllerRoute(
    name: "station",
    pattern: "stations/{slug}",
    defaults: new { controller = "Stations", action = "Index" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
