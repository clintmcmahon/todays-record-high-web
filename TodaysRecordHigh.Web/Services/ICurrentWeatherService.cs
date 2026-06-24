namespace TodaysRecordHigh.Web.Services;

public record CurrentConditions(double TempF, double FeelsLikeF, DateTime AsOf, string TimezoneAbbr, int WeatherCode, bool IsDay)
{
    public string WeatherEmoji => (WeatherCode, IsDay) switch
    {
        (0, true)       => "☀️",
        (0, false)      => "🌙",
        (1, true)       => "🌤️",
        (1, false)      => "🌙",
        (2, _)          => "⛅",
        (3, _)          => "☁️",
        (45, _)         => "🌫️",
        (48, _)         => "🌫️",
        (51 or 53, _)   => "🌦️",
        (55, _)         => "🌧️",
        (56 or 57, _)   => "🌧️",
        (61 or 63, _)   => "🌧️",
        (65, _)         => "🌧️",
        (66 or 67, _)   => "🌨️",
        (71 or 73, _)   => "🌨️",
        (75 or 77, _)   => "❄️",
        (80, _)         => "🌦️",
        (81, _)         => "🌧️",
        (82, _)         => "⛈️",
        (85 or 86, _)   => "🌨️",
        (95, _)         => "⛈️",
        (96 or 99, _)   => "🌩️",
        _               => "🌡️"
    };
}

public interface ICurrentWeatherService
{
    Task<CurrentConditions?> GetCurrentAsync(double lat, double lon);
}
