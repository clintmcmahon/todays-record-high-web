namespace TodaysRecordHigh.Web.Services;

public record WeatherAlert(
    string Event,
    string Headline,
    string Severity,
    DateTime? Expires
);

public interface IWeatherAlertsService
{
    Task<List<WeatherAlert>> GetActiveAlertsAsync(double lat, double lon);
}
