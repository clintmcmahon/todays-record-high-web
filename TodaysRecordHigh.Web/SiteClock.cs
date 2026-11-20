namespace TodaysRecordHigh.Web;

/// <summary>
/// The site's single definition of "now". The prod server runs on UTC, so DateTime.Now
/// rolled "today" over to tomorrow at 6 PM Central: records, normals, and Records Watch
/// all switched dates while most of the country was still in its afternoon. Central is
/// the middle of the CONUS, so its calendar date matches every mainland station's own
/// date except for an hour or two around midnight on the coasts.
/// </summary>
public static class SiteClock
{
    public static readonly TimeZoneInfo Central = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");

    public static DateTime Now => ToCentral(DateTime.UtcNow);

    public static DateTime ToCentral(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Central);
}
