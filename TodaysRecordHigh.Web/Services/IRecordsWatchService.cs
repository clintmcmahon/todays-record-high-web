using TodaysRecordHigh.Web.Models;

namespace TodaysRecordHigh.Web.Services;

public interface IRecordsWatchService
{
    /// <summary>The most recent completed scan, or null if the first scan hasn't finished yet.</summary>
    RecordWatchSnapshot? GetLatest();
}
