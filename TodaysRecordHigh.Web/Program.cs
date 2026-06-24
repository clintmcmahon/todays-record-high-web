using TodaysRecordHigh.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();

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

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
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
