using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using TodaysRecordHigh.Web.Models;

namespace TodaysRecordHigh.Web.Services;

public class AcisService : IAcisService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AcisService> _logger;

    private static readonly TimeSpan RecordCacheTtl = TimeSpan.FromHours(6);
    private static readonly TimeSpan NormalCacheTtl = TimeSpan.FromHours(24);

    public AcisService(HttpClient http, IMemoryCache cache, ILogger<AcisService> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    // ── Record highs for a specific calendar date ─────────────────────────

    public async Task<List<(int Temp, string Date)>> GetRecordHighsForDateAsync(
        string threadexSid, string monthDay, int topN = 10)
    {
        var acisN = Math.Max(topN, 5);
        var cacheKey = $"acis:records:hi:{threadexSid}:{monthDay}:{acisN}";
        if (_cache.TryGetValue(cacheKey, out List<(int, string)>? cached) && cached is not null)
            return cached.Take(topN).ToList();

        var request = new AcisStnDataRequest
        {
            Sid = threadexSid,
            SDate = "por",
            EDate = "por",
            Elems =
            [
                new AcisElement
                {
                    Name = "maxt",
                    Interval = "dly",
                    Duration = "dly",
                    Smry = new AcisSmry { Reduce = "max", Add = "date", N = acisN },
                    SmryOnly = 1,
                    GroupBy = new string[] { "year", monthDay, monthDay }
                }
            ]
        };

        var response = await PostStnDataAsync(request);
        var records = ParseGroupBySmry(response?.Smry);

        _cache.Set(cacheKey, records, RecordCacheTtl);
        return records.Take(topN).ToList();
    }

    // ── Record lows for a specific calendar date ──────────────────────────

    public async Task<List<(int Temp, string Date)>> GetRecordLowsForDateAsync(
        string threadexSid, string monthDay, int topN = 5)
    {
        var acisN = Math.Max(topN, 5);
        var cacheKey = $"acis:records:lo:{threadexSid}:{monthDay}:{acisN}";
        if (_cache.TryGetValue(cacheKey, out List<(int, string)>? cached) && cached is not null)
            return cached.Take(topN).ToList();

        var request = new AcisStnDataRequest
        {
            Sid = threadexSid,
            SDate = "por",
            EDate = "por",
            Elems =
            [
                new AcisElement
                {
                    Name = "mint",
                    Interval = "dly",
                    Duration = "dly",
                    Smry = new AcisSmry { Reduce = "min", Add = "date", N = acisN },
                    SmryOnly = 1,
                    GroupBy = new string[] { "year", monthDay, monthDay }
                }
            ]
        };

        var response = await PostStnDataAsync(request);
        var records = ParseGroupBySmry(response?.Smry);

        _cache.Set(cacheKey, records, RecordCacheTtl);
        return records.Take(topN).ToList();
    }

    // ── Climate normals ────────────────────────────────────────────────────

    public async Task<double?> GetNormalMaxTempAsync(string threadexSid, string monthDay)
        => await GetNormalForElemAsync(threadexSid, monthDay, "maxt");

    public async Task<double?> GetNormalMinTempAsync(string threadexSid, string monthDay)
        => await GetNormalForElemAsync(threadexSid, monthDay, "mint");

    private async Task<double?> GetNormalForElemAsync(string threadexSid, string monthDay, string elem)
    {
        var cacheKey = $"acis:normal:{elem}:{threadexSid}:{monthDay}";
        if (_cache.TryGetValue(cacheKey, out double? cached))
            return cached;

        var year = DateTime.Now.Year;
        var request = new AcisStnDataRequest
        {
            Sid = threadexSid,
            SDate = $"{year}-{monthDay}",
            EDate = $"{year}-{monthDay}",
            Elems = [new AcisElement { Name = elem, Normal = "1" }]
        };

        var result = await PostStnDataAsync(request);

        double? normal = null;
        if (result?.Data is { Count: > 0 })
        {
            var row = result.Data[0];
            if (row.Count > 1)
                normal = ParseDouble(row[1]);
        }

        _cache.Set(cacheKey, normal, NormalCacheTtl);
        return normal;
    }

    // ── Period of record ──────────────────────────────────────────────────

    public async Task<(string Start, string End)?> GetPeriodOfRecordAsync(string threadexSid)
    {
        var cacheKey = $"acis:por:{threadexSid}";
        if (_cache.TryGetValue(cacheKey, out (string, string)? cached))
            return cached;

        var payload = $"{{\"sids\":\"{threadexSid}\",\"meta\":[\"valid_daterange\"],\"elems\":\"maxt\"}}";
        try
        {
            using var content = new FormUrlEncodedContent([
                new KeyValuePair<string, string>("params", payload)
            ]);
            using var resp = await _http.PostAsync("StnMeta", content);
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            if (doc.RootElement.TryGetProperty("meta", out var arr)
                && arr.GetArrayLength() > 0
                && arr[0].TryGetProperty("valid_daterange", out var ranges)
                && ranges.GetArrayLength() > 0)
            {
                var range = ranges[0];
                if (range.GetArrayLength() >= 2)
                {
                    var por = (range[0].GetString() ?? "", range[1].GetString() ?? "");
                    _cache.Set(cacheKey, por, NormalCacheTtl);
                    return por;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ACIS StnMeta failed for {Sid}", threadexSid);
        }

        return null;
    }

    // ── All-time extremes (hottest/coldest/wettest/snowiest day ever) ─────

    public async Task<AllTimeExtremes?> GetAllTimeExtremesAsync(string threadexSid)
    {
        var cacheKey = $"acis:extremes:{threadexSid}";
        if (_cache.TryGetValue(cacheKey, out AllTimeExtremes? cached))
            return cached;

        var request = new AcisStnDataRequest
        {
            Sid = threadexSid,
            SDate = "por",
            EDate = "por",
            Elems =
            [
                new AcisElement { Name = "maxt", Interval = "dly", Duration = "dly",
                    Smry = new AcisSmry { Reduce = "max", Add = "date", N = 5 }, SmryOnly = 1 },
                new AcisElement { Name = "mint", Interval = "dly", Duration = "dly",
                    Smry = new AcisSmry { Reduce = "min", Add = "date", N = 5 }, SmryOnly = 1 },
                new AcisElement { Name = "pcpn", Interval = "dly", Duration = "dly",
                    Smry = new AcisSmry { Reduce = "max", Add = "date", N = 5 }, SmryOnly = 1 },
                new AcisElement { Name = "snow", Interval = "dly", Duration = "dly",
                    Smry = new AcisSmry { Reduce = "max", Add = "date", N = 5 }, SmryOnly = 1 }
            ]
        };

        var response = await PostStnDataAsync(request);
        if (response?.Smry is null)
            return null;

        // No-groupby smry structure: smry[elem_idx] = list of [value, date] pairs
        (int? temp, string? date) GetFirst(int idx)
        {
            if (response.Smry.Count <= idx) return (null, null);
            var elem = response.Smry[idx];
            if (elem.Count == 0) return (null, null);
            var pair = elem[0];
            if (pair.ValueKind != JsonValueKind.Array || pair.GetArrayLength() < 2) return (null, null);
            var items = pair.EnumerateArray().ToList();
            if (int.TryParse(items[0].GetString() ?? items[0].ToString(), out var v))
                return (v, items[1].GetString());
            return (null, null);
        }

        (double? val, string? date) GetFirstDouble(int idx)
        {
            if (response.Smry.Count <= idx) return (null, null);
            var elem = response.Smry[idx];
            if (elem.Count == 0) return (null, null);
            var pair = elem[0];
            if (pair.ValueKind != JsonValueKind.Array || pair.GetArrayLength() < 2) return (null, null);
            var items = pair.EnumerateArray().ToList();
            var d = ParseDouble(items[0]);
            return (d, items[1].GetString());
        }

        var (maxTemp, maxDate) = GetFirst(0);
        var (minTemp, minDate) = GetFirst(1);
        var (maxPcpn, maxPcpnDate) = GetFirstDouble(2);
        var (maxSnow, maxSnowDate) = GetFirstDouble(3);

        var extremes = new AllTimeExtremes(maxTemp, maxDate, minTemp, minDate,
            maxPcpn, maxPcpnDate, maxSnow, maxSnowDate);

        _cache.Set(cacheKey, extremes, NormalCacheTtl);
        return extremes;
    }

    // ── Annual hot-day counts (e.g. days ≥90°F per year) ─────────────────

    public async Task<List<(int Year, int Count)>> GetHotDaysPerYearAsync(string threadexSid, int thresholdF = 90)
    {
        var cacheKey = $"acis:hotdays:{threadexSid}:{thresholdF}";
        if (_cache.TryGetValue(cacheKey, out List<(int, int)>? cached) && cached is not null)
            return cached;

        // Fetch all daily maxt from 1970 through yesterday (includes current year so far).
        // cnt_gt90 reduce is not supported on Threadex via ACIS, so we compute counts here.
        var today = DateTime.Now;
        var edate = today.AddDays(-1).ToString("yyyy-MM-dd");
        var request = new AcisStnDataRequest
        {
            Sid = threadexSid,
            SDate = "1970-01-01",
            EDate = edate,
            Elems = [new AcisElement { Name = "maxt" }]
        };

        var response = await PostStnDataAsync(request);
        if (response?.Data is null)
            return [];

        var byYear = new Dictionary<int, int>();
        foreach (var row in response.Data)
        {
            if (row.Count < 2) continue;
            var dateStr = row[0].GetString();
            if (dateStr is null || dateStr.Length < 4) continue;
            if (!int.TryParse(dateStr[..4], out var year)) continue;

            var val = ParseDouble(row[1]);
            if (val.HasValue && val.Value >= thresholdF)
                byYear[year] = byYear.GetValueOrDefault(year) + 1;
        }

        // Guarantee current year appears even when it has zero qualifying days so far
        byYear.TryAdd(today.Year, 0);

        var result = byYear.OrderBy(kv => kv.Key)
                           .Select(kv => (kv.Key, kv.Value))
                           .ToList();

        // Current year is partial — cache for 3 hours so today's readings stay fresh
        _cache.Set(cacheKey, result, TimeSpan.FromHours(3));
        return result;
    }

    // ── Year-to-date daily departures from normal ────────────────────────

    public async Task<List<DailyDeparture>> GetYearToDateDeparturesAsync(string threadexSid, int year)
    {
        var cacheKey = $"acis:ytd:{threadexSid}:{year}";
        if (_cache.TryGetValue(cacheKey, out List<DailyDeparture>? cached) && cached is not null)
            return cached;

        var today = DateTime.Now;
        var edate = year == today.Year
            ? today.AddDays(-1).ToString("yyyy-MM-dd")  // through yesterday (today may be missing)
            : $"{year}-12-31";

        var request = new AcisStnDataRequest
        {
            Sid = threadexSid,
            SDate = $"{year}-01-01",
            EDate = edate,
            Elems =
            [
                new AcisElement { Name = "maxt" },
                new AcisElement { Name = "maxt", Normal = "1" }
            ]
        };

        var response = await PostStnDataAsync(request);
        if (response?.Data is null)
            return [];

        var results = new List<DailyDeparture>();
        foreach (var row in response.Data)
        {
            if (row.Count < 3) continue;
            var date = row[0].GetString() ?? "";
            var actual = ParseInt(row[1]);
            var normal = ParseDouble(row[2]);
            if (!string.IsNullOrEmpty(date))
                results.Add(new DailyDeparture(date, actual, normal));
        }

        // Cache current year for 3 hours, historical for 24 hours
        var ttl = year == today.Year ? TimeSpan.FromHours(3) : NormalCacheTtl;
        _cache.Set(cacheKey, results, ttl);
        return results;
    }

    // ── Precipitation: YTD / monthly / annual / heavy-rain-day counts ────

    public async Task<PrecipData?> GetPrecipDataAsync(string threadexSid)
    {
        var cacheKey = $"acis:precip:{threadexSid}";
        if (_cache.TryGetValue(cacheKey, out PrecipData? cached))
            return cached;

        var today = DateTime.Now;
        var edate = today.AddDays(-1).ToString("yyyy-MM-dd");

        var request = new AcisStnDataRequest
        {
            Sid = threadexSid,
            SDate = "1970-01-01",
            EDate = edate,
            Elems =
            [
                new AcisElement { Name = "pcpn" },
                new AcisElement { Name = "pcpn", Normal = "1" }
            ]
        };

        var response = await PostStnDataAsync(request);
        if (response?.Data is null) return null;

        var curYear  = today.Year;
        var curMonth = today.Month;
        var inSummer = curMonth is >= 6 and <= 8;

        // Accumulators
        var annualActual  = new Dictionary<int, double>();
        var annualNormal  = new Dictionary<int, double>();
        var monthlyActual = new Dictionary<(int Y, int M), double>();
        var monthlyNormal = new Dictionary<(int Y, int M), double>();
        var heavyDays     = new Dictionary<int, int>();

        double ytdA = 0, ytdN = 0;
        double monA = 0, monN = 0;
        double sumA = 0, sumN = 0;   // summer

        foreach (var row in response.Data)
        {
            if (row.Count < 3) continue;
            if (!DateTime.TryParse(row[0].GetString(), out var date)) continue;

            var y = date.Year;
            var m = date.Month;

            // "T" = trace → treat as 0.0 for totals; "M" / "" → null
            double? actual = row[1].GetString() == "T" ? 0.0 : ParseDouble(row[1]);
            double? normal = row[2].GetString() == "T" ? 0.0 : ParseDouble(row[2]);

            if (actual.HasValue)
            {
                annualActual[y]  = annualActual.GetValueOrDefault(y) + actual.Value;
                monthlyActual[(y, m)] = monthlyActual.GetValueOrDefault((y, m)) + actual.Value;
                if (actual.Value >= 1.0)
                    heavyDays[y] = heavyDays.GetValueOrDefault(y) + 1;
            }
            if (normal.HasValue)
            {
                annualNormal[y]  = annualNormal.GetValueOrDefault(y) + normal.Value;
                monthlyNormal[(y, m)] = monthlyNormal.GetValueOrDefault((y, m)) + normal.Value;
            }

            // Current-year YTD slices
            if (y == curYear)
            {
                if (actual.HasValue) ytdA += actual.Value;
                if (normal.HasValue) ytdN += normal.Value;

                if (m == curMonth)
                {
                    if (actual.HasValue) monA += actual.Value;
                    if (normal.HasValue) monN += normal.Value;
                }
                if (inSummer && m is >= 6 and <= 8)
                {
                    if (actual.HasValue) sumA += actual.Value;
                    if (normal.HasValue) sumN += normal.Value;
                }
            }
        }

        // Full calendar-month normal: sum daily normals for the entire month.
        // Since daily normals are the same every year, use last year's same month.
        var prevYear = curYear - 1;
        var monthFullNormal = monthlyNormal.GetValueOrDefault((prevYear, curMonth));

        // Full Jun–Aug normal: same idea using last year's complete summer months
        double? summerFullNormal = inSummer
            ? (double?)(monthlyNormal.GetValueOrDefault((prevYear, 6))
               + monthlyNormal.GetValueOrDefault((prevYear, 7))
               + monthlyNormal.GetValueOrDefault((prevYear, 8)))
            : null;

        // Annual totals list (all years with data)
        var annualTotals = annualActual.Keys
            .OrderBy(y => y)
            .Select(y => (
                Year:   y,
                Actual: Math.Round(annualActual[y], 2),
                Normal: annualNormal.TryGetValue(y, out var n) ? (double?)Math.Round(n, 2) : null
            ))
            .ToList();

        // Monthly last 24 months
        var cutoff = new DateTime(curYear, curMonth, 1).AddMonths(-23);
        var monthly24 = monthlyActual.Keys
            .Where(k => new DateTime(k.Y, k.M, 1) >= cutoff)
            .OrderBy(k => k.Y).ThenBy(k => k.M)
            .Select(k => (
                Year:   k.Y,
                Month:  k.M,
                Actual: Math.Round(monthlyActual[k], 2),
                Normal: monthlyNormal.TryGetValue(k, out var n) ? (double?)Math.Round(n, 2) : null
            ))
            .ToList();

        // Heavy rain days list
        heavyDays.TryAdd(curYear, 0);
        var heavyList = heavyDays.OrderBy(kv => kv.Key)
            .Select(kv => (Year: kv.Key, Count: kv.Value)).ToList();
        heavyDays.TryGetValue(curYear, out var curHeavy);

        var result = new PrecipData(
            Math.Round(ytdA, 2), Math.Round(ytdN, 2),
            Math.Round(monA, 2), Math.Round(monN, 2),
            Math.Round(monthFullNormal, 2),
            inSummer ? Math.Round(sumA, 2) : null,
            inSummer ? Math.Round(sumN, 2) : null,
            summerFullNormal.HasValue ? Math.Round(summerFullNormal.Value, 2) : null,
            annualTotals, monthly24, heavyList, curHeavy
        );

        _cache.Set(cacheKey, result, TimeSpan.FromHours(6));
        return result;
    }

    // ── Warm summer nights (Jun–Aug mint > normal mint) per year ─────────

    public async Task<WarmSummerNightsData?> GetWarmSummerNightsAsync(string threadexSid)
    {
        var cacheKey = $"acis:wsn:{threadexSid}";
        if (_cache.TryGetValue(cacheKey, out WarmSummerNightsData? cached))
            return cached;

        var today = DateTime.Now;
        var edate = today.AddDays(-1).ToString("yyyy-MM-dd");

        var request = new AcisStnDataRequest
        {
            Sid = threadexSid,
            SDate = "1970-01-01",
            EDate = edate,
            Elems =
            [
                new AcisElement { Name = "mint" },
                new AcisElement { Name = "mint", Normal = "1" }
            ]
        };

        var response = await PostStnDataAsync(request);
        if (response?.Data is null)
            return null;

        var aboveNormal = new Dictionary<int, int>();   // year → count above-normal nights
        var mintSums    = new Dictionary<int, (double Sum, int Count)>(); // year → sum/count for avg

        foreach (var row in response.Data)
        {
            if (row.Count < 3) continue;
            var dateStr = row[0].GetString();
            if (!DateTime.TryParse(dateStr, out var date)) continue;
            if (date.Month < 6 || date.Month > 8) continue;   // summer only

            var year   = date.Year;
            var actual = ParseInt(row[1]);
            var normal = ParseDouble(row[2]);
            if (!actual.HasValue || !normal.HasValue) continue;

            aboveNormal.TryAdd(year, 0);
            if (actual.Value > normal.Value)
                aboveNormal[year]++;

            var prev = mintSums.GetValueOrDefault(year);
            mintSums[year] = (prev.Sum + actual.Value, prev.Count + 1);
        }

        // Ensure current year appears even if zero nights so far
        aboveNormal.TryAdd(today.Year, 0);

        var warmPerYear = aboveNormal.OrderBy(kv => kv.Key)
            .Select(kv => (Year: kv.Key, Count: kv.Value)).ToList();

        // Historical average baseline: full summers 1970–2020
        var baselineYears = warmPerYear.Where(x => x.Year >= 1970 && x.Year <= 2020).ToList();
        var histAvg = baselineYears.Count > 0 ? baselineYears.Average(x => x.Count) : (double?)null;

        // Record summer (exclude current partial year)
        int? recYear = null;
        int? recCount = null;
        var bestHistoric = warmPerYear.Where(x => x.Year < today.Year)
            .OrderByDescending(x => x.Count).FirstOrDefault();
        if (bestHistoric != default)
        {
            recYear  = bestHistoric.Year;
            recCount = bestHistoric.Count;
        }

        // This summer's count
        aboveNormal.TryGetValue(today.Year, out var thisSummer);

        // Average Jun–Aug low by decade (blue-cool → red-warm shows the shift visually)
        var avgLowByDecade = mintSums
            .GroupBy(kv => (kv.Key / 10) * 10)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var label  = $"{g.Key}s";
                var avgLow = g.Average(kv => kv.Value.Count > 0 ? kv.Value.Sum / kv.Value.Count : 0);
                return (label, avgLow);
            })
            .ToList();

        var result = new WarmSummerNightsData(
            warmPerYear, avgLowByDecade, thisSummer, histAvg, recYear, recCount);

        _cache.Set(cacheKey, result, TimeSpan.FromHours(3));
        return result;
    }

    // ── HTTP + serialization helpers ──────────────────────────────────────

    private async Task<AcisStnDataResponse?> PostStnDataAsync(AcisStnDataRequest request)
    {
        try
        {
            var payload = JsonSerializer.Serialize(request, JsonOpts);
            using var content = new FormUrlEncodedContent([
                new KeyValuePair<string, string>("params", payload)
            ]);

            using var response = await _http.PostAsync("StnData", content);
            response.EnsureSuccessStatusCode();

            return JsonSerializer.Deserialize<AcisStnDataResponse>(
                await response.Content.ReadAsStringAsync(), JsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ACIS StnData request failed for {Sid}", request.Sid);
            return null;
        }
    }

    // Parses the groupby smry structure: smry[0][0] = JsonElement array of [value, date] pairs
    private static List<(int Temp, string Date)> ParseGroupBySmry(List<List<JsonElement>>? smry)
    {
        var results = new List<(int, string)>();
        if (smry is null || smry.Count == 0) return results;

        var elemGroups = smry[0];
        if (elemGroups.Count == 0) return results;

        var groupEl = elemGroups[0];
        if (groupEl.ValueKind != JsonValueKind.Array) return results;

        foreach (var record in groupEl.EnumerateArray())
        {
            if (record.ValueKind != JsonValueKind.Array) continue;
            var items = record.EnumerateArray().ToList();
            if (items.Count < 2) continue;

            var temp = ParseInt(items[0]);
            var date = items[1].GetString() ?? "";
            if (temp.HasValue && !string.IsNullOrEmpty(date))
                results.Add((temp.Value, date));
        }

        return results;
    }

    private static int? ParseInt(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Number) return el.GetInt32();
        if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var v)) return v;
        return null;
    }

    private static double? ParseDouble(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Number) return el.GetDouble();
        if (el.ValueKind == JsonValueKind.String && double.TryParse(el.GetString(), out var v)) return v;
        return null;
    }
}
