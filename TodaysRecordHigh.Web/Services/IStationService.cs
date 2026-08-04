using TodaysRecordHigh.Web.Models;

namespace TodaysRecordHigh.Web.Services;

public interface IStationService
{
    IReadOnlyList<Station> GetAll();
    Station? GetBySlug(string slug);
    string GetAllJson();

    /// <summary>Nearest stations with ACIS data, closest first, within <paramref name="maxMiles"/>.</summary>
    IReadOnlyList<Station> GetNearest(Station origin, int count, double maxMiles = 350);
}
