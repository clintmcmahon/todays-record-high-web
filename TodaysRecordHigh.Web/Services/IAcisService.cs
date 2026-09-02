using TodaysRecordHigh.Web.Models;

namespace TodaysRecordHigh.Web.Services;

public interface IAcisService
{
    Task<List<(int Temp, string Date)>> GetRecordHighsForDateAsync(string threadexSid, string monthDay, int topN = 10);
    Task<List<(int Temp, string Date)>> GetRecordLowsForDateAsync(string threadexSid, string monthDay, int topN = 5);
    Task<double?> GetNormalMaxTempAsync(string threadexSid, string monthDay);
    Task<double?> GetNormalMinTempAsync(string threadexSid, string monthDay);
    Task<(string Start, string End)?> GetPeriodOfRecordAsync(string threadexSid);
    Task<AllTimeExtremes?> GetAllTimeExtremesAsync(string threadexSid);
    Task<List<(int Year, int Count)>> GetHotDaysPerYearAsync(string threadexSid, int thresholdF = 90);
    Task<List<DailyDeparture>> GetYearToDateDeparturesAsync(string threadexSid, int year);
    Task<WarmSummerNightsData?> GetWarmSummerNightsAsync(string threadexSid);
    Task<PrecipData?> GetPrecipDataAsync(string threadexSid);
    Task<HeatWaveData?> GetHeatWaveDataAsync(string threadexSid);
    Task<List<OnThisDayRecord>> GetOnThisDayHistoryAsync(string threadexSid, string monthDay);
    Task<List<MonthlyMean>> GetMonthlyMeansAsync(string threadexSid);
    Task<List<MonthlyPrecip>> GetMonthlyPrecipTotalsAsync(string threadexSid);
    Task<MonthInReview?> GetMonthInReviewAsync(string threadexSid, IReadOnlyList<Station> peerStations,
        int? requestedYear = null, int? requestedMonth = null);
    Task<FallReview?> GetFallReviewAsync(string threadexSid, int? requestedYear = null);
}
