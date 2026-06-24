using TodaysRecordHigh.Web.Models;

namespace TodaysRecordHigh.Web.Services;

public interface IStationService
{
    IReadOnlyList<Station> GetAll();
    Station? GetBySlug(string slug);
    string GetAllJson();
}
