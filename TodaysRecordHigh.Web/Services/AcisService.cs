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

        // All daily maxt from POR through yesterday (includes current year so far).
        // cnt_gt90 reduce is not supported on Threadex via ACIS, so we compute counts here.
        var today = DateTime.Now;
        var daily = await GetDailyMaxtAsync(threadexSid);
        if (daily.Count == 0)
            return [];

        var byYear = new Dictionary<int, int>();
        foreach (var (date, maxT) in daily)
        {
            if (maxT.HasValue && maxT.Value >= thresholdF)
                byYear[date.Year] = byYear.GetValueOrDefault(date.Year) + 1;
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
            SDate = "por",
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
            SDate = "por",
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

    // ── Heat wave events and stats ────────────────────────────────────────

    public async Task<HeatWaveData?> GetHeatWaveDataAsync(string threadexSid)
    {
        var cacheKey = $"acis:heatwave:{threadexSid}";
        if (_cache.TryGetValue(cacheKey, out HeatWaveData? cached))
            return cached;

        var today = DateTime.Now;
        var edate = today.AddDays(-1).ToString("yyyy-MM-dd");

        var request = new AcisStnDataRequest
        {
            Sid = threadexSid,
            SDate = "por",
            EDate = edate,
            Elems =
            [
                new AcisElement { Name = "maxt" },
                new AcisElement { Name = "mint" }
            ]
        };

        var response = await PostStnDataAsync(request);
        if (response?.Data is null)
            return null;

        int? ParseTemp(JsonElement el)
        {
            var d = ParseDouble(el);
            return d.HasValue ? (int?)Math.Round(d.Value) : null;
        }

        // Parse all rows into a typed list
        var data = new List<(DateTime Date, int? MaxT, int? MinT)>();
        foreach (var row in response.Data)
        {
            if (row.Count < 3) continue;
            if (!DateTime.TryParse(row[0].GetString(), out var date)) continue;
            var maxT = ParseTemp(row[1]);
            var minT = ParseTemp(row[2]);
            data.Add((date, maxT, minT));
        }

        // Compute station-specific heat wave threshold: 90th percentile of all historical
        // Jun–Aug daily highs (complete years only). This makes the threshold relative to
        // local climate — 90°F is unremarkable in Phoenix but genuinely hot in Minneapolis.
        var summerMaxTs = data
            .Where(d => d.Date.Month >= 6 && d.Date.Month <= 8
                     && d.Date.Year < today.Year
                     && d.MaxT.HasValue)
            .Select(d => d.MaxT!.Value)
            .OrderBy(t => t)
            .ToList();

        int threshold;
        if (summerMaxTs.Count >= 30)
        {
            var idx = (int)Math.Floor(0.90 * summerMaxTs.Count);
            threshold = summerMaxTs[Math.Min(idx, summerMaxTs.Count - 1)];
        }
        else
        {
            threshold = 90; // fallback for stations with very limited data
        }

        const int minEventDays = 3;

        // Build a local helper to turn a slice into a HeatWaveEvent
        HeatWaveEvent BuildEvent(List<(DateTime Date, int? MaxT, int? MinT)> slice)
        {
            var peakDay = slice.Where(d => d.MaxT.HasValue).OrderByDescending(d => d.MaxT).First();
            return new HeatWaveEvent(
                StartDate:      slice[0].Date.ToString("yyyy-MM-dd"),
                EndDate:        slice[^1].Date.ToString("yyyy-MM-dd"),
                DurationDays:   slice.Count,
                PeakTemp:       peakDay.MaxT!.Value,
                PeakTempDate:   peakDay.Date.ToString("yyyy-MM-dd"),
                AvgDailyMax:    Math.Round(slice.Where(d => d.MaxT.HasValue).Average(d => (double)d.MaxT!.Value), 1),
                AvgOvernightLow: slice.Any(d => d.MinT.HasValue)
                                    ? Math.Round(slice.Where(d => d.MinT.HasValue).Average(d => (double)d.MinT!.Value), 1)
                                    : 0,
                Year:           slice[0].Date.Year
            );
        }

        // Find heat wave events
        var events = new List<HeatWaveEvent>();
        int streakStart = -1;
        for (int i = 0; i <= data.Count; i++)
        {
            bool hot = i < data.Count && data[i].MaxT.HasValue && data[i].MaxT!.Value >= threshold;
            if (hot)
            {
                if (streakStart < 0) streakStart = i;
            }
            else
            {
                if (streakStart >= 0)
                {
                    int duration = i - streakStart;
                    var slice = data.GetRange(streakStart, duration);
                    if (duration >= minEventDays)
                        events.Add(BuildEvent(slice));
                    streakStart = -1;
                }
            }
        }

        // Current streak (consecutive hot days at end of data, regardless of event threshold)
        int currentStreak = 0;
        string? streakStartDate = null;
        for (int i = data.Count - 1; i >= 0; i--)
        {
            if (data[i].MaxT.HasValue && data[i].MaxT!.Value >= threshold)
            {
                currentStreak++;
                streakStartDate = data[i].Date.ToString("yyyy-MM-dd");
            }
            else break;
        }
        bool streakIsHeatWave = currentStreak >= 3;

        // Top 10 events by duration
        var topEvents = events
            .OrderByDescending(e => e.DurationDays)
            .ThenByDescending(e => e.PeakTemp)
            .Take(10)
            .ToList();

        // Most recent event
        var mostRecent = events.Count > 0 ? events[^1] : null;

        // Events per year — fill zeros from first data year through thisYear
        var thisYear = today.Year;
        var firstDataYear = data.Count > 0 ? data[0].Date.Year : 1970;
        var eventsDict = events.GroupBy(e => e.Year).ToDictionary(g => g.Key, g => g.Count());
        var eventsPerYear = Enumerable.Range(firstDataYear, thisYear - firstDataYear + 1)
            .Select(y => (Year: y, Count: eventsDict.GetValueOrDefault(y, 0)))
            .ToList();

        // Avg duration by decade (complete decades only)
        var completedDecadeCutoff = (thisYear / 10) * 10;
        var decadeAvgDuration = events
            .Where(e => e.Year < completedDecadeCutoff)
            .GroupBy(e => (e.Year / 10) * 10)
            .Where(g => g.Count() >= 2)
            .OrderBy(g => g.Key)
            .Select(g => (Decade: $"{g.Key}s", AvgDuration: Math.Round(g.Average(e => e.DurationDays), 1)))
            .ToList();

        // Dangerous nights (mint >= 70°F) per year
        var dangerousNights = data
            .Where(d => d.MinT.HasValue && d.MinT!.Value >= 70)
            .GroupBy(d => d.Date.Year)
            .ToDictionary(g => g.Key, g => g.Count());
        var dangerousNightsPerYear = Enumerable.Range(firstDataYear, thisYear - firstDataYear + 1)
            .Select(y => (Year: y, Count: dangerousNights.GetValueOrDefault(y, 0)))
            .ToList();
        var dangerousNightsThisYear = dangerousNights.GetValueOrDefault(thisYear, 0);

        // Historical averages (exclude current partial year)
        var completeEvents = events.Where(e => e.Year < thisYear).ToList();
        double? historicalAvgDuration = completeEvents.Count > 0
            ? completeEvents.Average(e => e.DurationDays) : (double?)null;
        double? historicalAvgPeak = completeEvents.Count > 0
            ? completeEvents.Average(e => e.PeakTemp) : (double?)null;

        // Avg daily high by decade — full year, complete decades only
        var completedDecadeCutoffHigh = (thisYear / 10) * 10;
        var avgHighByDecade = data
            .Where(d => d.MaxT.HasValue && d.Date.Year < completedDecadeCutoffHigh)
            .GroupBy(d => (d.Date.Year / 10) * 10)
            .OrderBy(g => g.Key)
            .Select(g => (Decade: $"{g.Key}s", AvgHigh: Math.Round(g.Average(d => (double)d.MaxT!.Value), 1)))
            .ToList();

        var result = new HeatWaveData(
            CurrentStreak:            currentStreak,
            StreakStartDate:          streakStartDate,
            StreakIsHeatWave:         streakIsHeatWave,
            TopEvents:                topEvents,
            MostRecentEvent:          mostRecent,
            EventsPerYear:            eventsPerYear,
            AvgDurationByDecade:      decadeAvgDuration,
            DangerousNightsThisYear:  dangerousNightsThisYear,
            DangerousNightsPerYear:   dangerousNightsPerYear,
            HistoricalAvgDuration:    historicalAvgDuration,
            HistoricalAvgPeak:        historicalAvgPeak,
            AvgHighByDecade:          avgHighByDecade,
            HeatWaveThreshold:        threshold
        );

        _cache.Set(cacheKey, result, RecordCacheTtl);
        return result;
    }

    // ── This calendar date across every year on record ────────────────────

    public async Task<List<OnThisDayRecord>> GetOnThisDayHistoryAsync(string threadexSid, string monthDay)
    {
        var cacheKey = $"acis:onthisday:{threadexSid}:{monthDay}";
        if (_cache.TryGetValue(cacheKey, out List<OnThisDayRecord>? cached) && cached is not null)
            return cached;

        var today = DateTime.Now;
        var request = new AcisStnDataRequest
        {
            Sid = threadexSid,
            SDate = $"1871-{monthDay}",
            EDate = $"{today.Year}-{monthDay}",
            Elems =
            [
                new AcisElement { Name = "maxt", Interval = new[] { 1, 0, 0 }, Duration = 1 },
                new AcisElement { Name = "mint", Interval = new[] { 1, 0, 0 }, Duration = 1 },
                new AcisElement { Name = "pcpn", Interval = new[] { 1, 0, 0 }, Duration = 1 },
                new AcisElement { Name = "snow", Interval = new[] { 1, 0, 0 }, Duration = 1 },
            ]
        };

        var response = await PostStnDataAsync(request);
        if (response?.Data is null)
            return [];

        var results = new List<OnThisDayRecord>();
        foreach (var row in response.Data)
        {
            if (row.Count < 5) continue;
            if (!DateTime.TryParse(row[0].GetString(), out var date)) continue;

            int? ParseTempCell(JsonElement el) => ParseInt(el);
            double? ParsePrecipCell(JsonElement el) => el.GetString() == "T" ? 0.0 : ParseDouble(el);

            results.Add(new OnThisDayRecord(
                date.Year,
                ParseTempCell(row[1]),
                ParseTempCell(row[2]),
                ParsePrecipCell(row[3]),
                ParsePrecipCell(row[4])
            ));
        }

        results = results.OrderByDescending(r => r.Year).ToList();

        _cache.Set(cacheKey, results, RecordCacheTtl);
        return results;
    }

    // ── Monthly mean temperatures across the period of record ─────────────

    // A month needs at least this many observed days to be ranked. Ranking a month
    // built from a handful of days would put partial months at the top of the list.
    private const int MaxMissingDaysPerMonth = 3;

    public async Task<List<MonthlyMean>> GetMonthlyMeansAsync(string threadexSid)
    {
        var cacheKey = $"acis:monthlymeans:{threadexSid}";
        if (_cache.TryGetValue(cacheKey, out List<MonthlyMean>? cached) && cached is not null)
            return cached;

        // "mcnt" adds a missing-day count alongside each mean, so partial months
        // (the in-progress one, and gaps early in the record) can be filtered out.
        var request = new AcisStnDataRequest
        {
            Sid = threadexSid,
            SDate = "por",
            EDate = "por",
            Elems =
            [
                new AcisElement
                {
                    Name = "avgt",
                    Interval = new[] { 0, 1 },
                    Duration = "mly",
                    Reduce = new AcisReduce { Reduce = "mean", Add = "mcnt" }
                }
            ]
        };

        var response = await PostStnDataAsync(request);
        if (response?.Data is null)
            return [];

        var results = new List<MonthlyMean>();
        foreach (var row in response.Data)
        {
            if (row.Count < 2) continue;

            var key = row[0].GetString();
            if (key is null || key.Length < 7) continue;
            if (!int.TryParse(key[..4], out var year)) continue;
            if (!int.TryParse(key.Substring(5, 2), out var month)) continue;

            // With "add": "mcnt" the cell is ["78.85", 0]; without it, a bare value.
            var cell = row[1];
            double? avg;
            int missing = 0;
            if (cell.ValueKind == JsonValueKind.Array && cell.GetArrayLength() >= 2)
            {
                var items = cell.EnumerateArray().ToList();
                avg = ParseDouble(items[0]);
                missing = ParseInt(items[1]) ?? 0;
            }
            else
            {
                avg = ParseDouble(cell);
            }

            if (avg.HasValue && missing <= MaxMissingDaysPerMonth)
                results.Add(new MonthlyMean(year, month, avg.Value));
        }

        _cache.Set(cacheKey, results, TimeSpan.FromHours(12));
        return results;
    }

    // ── Monthly precipitation totals across the period of record ──────────

    /// <summary>
    /// One summed total per calendar month for the whole record. A month with an
    /// unmeasured day is short by that day's rain, so this is stricter than the
    /// temperature series: any missing day disqualifies the month from ranking.
    /// </summary>
    public async Task<List<MonthlyPrecip>> GetMonthlyPrecipTotalsAsync(string threadexSid)
    {
        var cacheKey = $"acis:monthlyprecip:{threadexSid}";
        if (_cache.TryGetValue(cacheKey, out List<MonthlyPrecip>? cached) && cached is not null)
            return cached;

        var request = new AcisStnDataRequest
        {
            Sid = threadexSid,
            SDate = "por",
            EDate = "por",
            Elems =
            [
                new AcisElement
                {
                    Name = "pcpn",
                    Interval = new[] { 0, 1 },
                    Duration = "mly",
                    Reduce = new AcisReduce { Reduce = "sum", Add = "mcnt" }
                }
            ]
        };

        var response = await PostStnDataAsync(request);
        if (response?.Data is null)
            return [];

        var results = new List<MonthlyPrecip>();
        foreach (var row in response.Data)
        {
            if (row.Count < 2) continue;

            var key = row[0].GetString();
            if (key is null || key.Length < 7) continue;
            if (!int.TryParse(key[..4], out var year)) continue;
            if (!int.TryParse(key.Substring(5, 2), out var month)) continue;

            // With "add": "mcnt" the cell is ["1.71", 0]; without it, a bare value.
            var cell = row[1];
            JsonElement valueEl;
            int missing = 0;
            if (cell.ValueKind == JsonValueKind.Array && cell.GetArrayLength() >= 2)
            {
                var items = cell.EnumerateArray().ToList();
                valueEl = items[0];
                missing = ParseInt(items[1]) ?? 0;
            }
            else
            {
                valueEl = cell;
            }

            // A month that only ever saw trace amounts reports "T", not a number.
            var total = valueEl.ValueKind == JsonValueKind.String && valueEl.GetString() == "T"
                ? 0.0
                : ParseDouble(valueEl);

            if (total.HasValue && missing == 0)
                results.Add(new MonthlyPrecip(year, month, Math.Round(total.Value, 2)));
        }

        _cache.Set(cacheKey, results, TimeSpan.FromHours(12));
        return results;
    }

    /// <summary>
    /// Least-squares fit of value against year. Returns null for series too short
    /// to say anything — a trend drawn through fifteen points is decoration.
    /// </summary>
    private static Trend? ComputeTrend(IReadOnlyList<(int Year, double Value)> series, int minPoints = 30)
    {
        if (series.Count < minPoints) return null;

        var meanX = series.Average(p => (double)p.Year);
        var meanY = series.Average(p => p.Value);

        double sxy = 0, sxx = 0;
        foreach (var (x, y) in series)
        {
            var dx = x - meanX;
            sxy += dx * (y - meanY);
            sxx += dx * dx;
        }

        if (sxx <= 0) return null;

        var slope = sxy / sxx;
        return new Trend(
            PerYear:   slope,
            Intercept: meanY - slope * meanX,
            FirstYear: series[0].Year,
            LastYear:  series[^1].Year,
            Count:     series.Count);
    }


    /// <summary>
    /// Buckets every monthly total on record so one month can be placed against the
    /// whole distribution. Bin width is picked to land near 20 buckets, which keeps
    /// a 2-inch desert record and a 19-inch coastal one equally readable.
    /// </summary>
    private static (List<PrecipBin> Bins, int TargetIndex) BuildPrecipHistogram(
        List<MonthlyPrecip> totals, double target)
    {
        if (totals.Count == 0) return ([], -1);

        var max = totals.Max(m => m.Total);
        if (max <= 0) return ([], -1);

        double[] widths = [0.1, 0.25, 0.5, 1, 2, 5];
        var width = widths.FirstOrDefault(w => max / w <= 22, 5.0);
        var binCount = Math.Max(1, (int)Math.Ceiling(max / width));

        var counts = new int[binCount];
        foreach (var m in totals)
        {
            var idx = Math.Clamp((int)(m.Total / width), 0, binCount - 1);
            counts[idx]++;
        }

        var bins = counts
            .Select((c, i) => new PrecipBin(
                Math.Round(i * width, 2),
                Math.Round((i + 1) * width, 2),
                c))
            .ToList();

        return (bins, Math.Clamp((int)(target / width), 0, binCount - 1));
    }

    /// <summary>
    /// The target month's rainfall against its own history: the 1991–2020 average,
    /// its rank among the same calendar month, its rank among every month on
    /// record, and the trend across the years.
    /// </summary>
    private async Task<MonthPrecipReview?> BuildMonthPrecipAsync(string threadexSid, int year, int month)
    {
        var totals = await GetMonthlyPrecipTotalsAsync(threadexSid);
        if (totals.Count == 0) return null;

        var target = totals.FirstOrDefault(m => m.Year == year && m.Month == month);
        if (target is null) return null;

        var sameMonth = totals.Where(m => m.Month == month).ToList();

        // Counted from both ends so ties never shift a month's standing.
        var rankWettest = sameMonth.Count(m => m.Total > target.Total) + 1;
        var rankDriest  = sameMonth.Count(m => m.Total < target.Total) + 1;
        var rankWettestAll = totals.Count(m => m.Total > target.Total) + 1;
        var rankDriestAll  = totals.Count(m => m.Total < target.Total) + 1;

        // Same 1991–2020 window used for the temperature normal, derived from the
        // station's own totals rather than a second API call.
        var baseline = sameMonth.Where(m => m.Year is >= 1991 and <= 2020).ToList();
        double? normal = baseline.Count >= 15
            ? Math.Round(baseline.Average(m => m.Total), 2)
            : null;

        var history = sameMonth.OrderBy(m => m.Year).ToList();
        var trend = ComputeTrend(history.Select(m => (m.Year, m.Total)).ToList());

        var (bins, targetBin) = BuildPrecipHistogram(totals, target.Total);

        return new MonthPrecipReview(
            Year:                  year,
            Month:                 month,
            Total:                 target.Total,
            NormalTotal:           normal,
            RankWettestSameMonth:  rankWettest,
            RankDriestSameMonth:   rankDriest,
            TotalSameMonths:       sameMonth.Count,
            RankWettestAllMonths:  rankWettestAll,
            RankDriestAllMonths:   rankDriestAll,
            TotalMonths:           totals.Count,
            WettestSameMonthEver:  sameMonth.OrderByDescending(m => m.Total).FirstOrDefault(),
            DriestSameMonthEver:   sameMonth.OrderBy(m => m.Total).FirstOrDefault(),
            WettestMonthEver:      totals.OrderByDescending(m => m.Total).FirstOrDefault(),
            TopSameMonths:         sameMonth.OrderByDescending(m => m.Total).Take(10).ToList(),
            SameMonthHistory:      history,
            SameMonthTrend:        trend,
            AllMonthsDistribution: bins,
            TargetBinIndex:        targetBin);
    }

    // ── Month in Review ───────────────────────────────────────────────────

    public async Task<MonthInReview?> GetMonthInReviewAsync(
        string threadexSid, IReadOnlyList<Station> peerStations)
    {
        var monthlyMeans = await GetMonthlyMeansAsync(threadexSid);
        if (monthlyMeans.Count == 0)
            return null;

        // Review the most recent complete month. The in-progress month is already
        // excluded by the missing-day filter, so this is simply the latest entry
        // that starts before the current one.
        var now = DateTime.Now;
        var currentMonthStart = new DateTime(now.Year, now.Month, 1);
        var target = monthlyMeans
            .Where(m => new DateTime(m.Year, m.Month, 1) < currentMonthStart)
            .OrderByDescending(m => m.Year).ThenByDescending(m => m.Month)
            .FirstOrDefault();

        if (target is null)
            return null;

        var cacheKey = $"acis:monthreview:{threadexSid}:{target.Year}-{target.Month:00}";
        if (_cache.TryGetValue(cacheKey, out MonthInReview? cachedReview) && cachedReview is not null)
            return cachedReview;

        // ── Rankings ──────────────────────────────────────────────────────
        var warmerAnywhere = monthlyMeans.Count(m => m.AvgTemp > target.AvgTemp);
        var rankAllMonths  = warmerAnywhere + 1;

        var sameMonth = monthlyMeans.Where(m => m.Month == target.Month).ToList();
        var rankSameMonth = sameMonth.Count(m => m.AvgTemp > target.AvgTemp) + 1;

        var topMonths = monthlyMeans
            .OrderByDescending(m => m.AvgTemp).Take(10).ToList();
        var topSameMonths = sameMonth
            .OrderByDescending(m => m.AvgTemp).Take(10).ToList();
        var sameMonthHistory = sameMonth
            .OrderBy(m => m.Year).ToList();

        // 1991–2020 average for this calendar month, matching the normals period
        // used elsewhere on the page.
        // Kept at full precision — rounding happens at display time so rankings and
        // departures are computed from the underlying values.
        var baseline = sameMonth.Where(m => m.Year is >= 1991 and <= 2020).ToList();
        double? normalAvg = baseline.Count >= 15
            ? baseline.Average(m => m.AvgTemp)
            : null;

        // ── Daily-series stats: 90° streaks and the year's peak ───────────
        var daily = await GetDailyMaxtAsync(threadexSid);

        const int hotThreshold = 90;

        var monthStart = new DateTime(target.Year, target.Month, 1);
        var monthEnd   = monthStart.AddMonths(1).AddDays(-1);

        // Streaks are scoped to the calendar month and compared against the same month
        // in other years. A run spilling in from May is not a July statistic, and an
        // unfinished run would be measured against completed ones.
        var streakByYear = daily
            .Where(d => d.Date.Month == target.Month)
            .GroupBy(d => d.Date.Year)
            .Select(g => BuildStreaks(g.ToList(), hotThreshold)
                .OrderByDescending(s => s.Days).ThenByDescending(s => s.PeakTemp)
                .FirstOrDefault())
            .Where(s => s is not null)
            .Select(s => s!)
            .ToList();

        var longestInMonth = streakByYear.FirstOrDefault(s => s.Year == target.Year);

        var longestEver = streakByYear
            .OrderByDescending(s => s.Days).ThenByDescending(s => s.PeakTemp)
            .FirstOrDefault();

        // "Longest since YYYY" — the most recent earlier year whose run in this same
        // month was longer, which is what makes a streak newsworthy. Ties are tracked
        // separately so a matched streak is never reported as an outright record.
        int? lastYearWithLongerStreak = null;
        int? lastYearTiedStreak = null;
        if (longestInMonth is not null)
        {
            var prior = streakByYear.Where(s => s.Year < target.Year).ToList();

            var longer = prior.Where(s => s.Days > longestInMonth.Days).ToList();
            if (longer.Count > 0) lastYearWithLongerStreak = longer.Max(s => s.Year);

            var tied = prior.Where(s => s.Days == longestInMonth.Days).ToList();
            if (tied.Count > 0) lastYearTiedStreak = tied.Max(s => s.Year);
        }

        var hotDaysInMonth = daily.Count(d =>
            d.Date >= monthStart && d.Date <= monthEnd &&
            d.MaxT.HasValue && d.MaxT.Value >= hotThreshold);

        // 90°F is only a meaningful yardstick where it actually happens, and a single
        // hot day is not a streak worth a callout.
        var totalHotDaysEver = daily.Count(d => d.MaxT.HasValue && d.MaxT.Value >= hotThreshold);
        var showStreak = totalHotDaysEver >= 20 && longestInMonth is { Days: >= 2 };

        // ── Year's peak high and the milestone it cleared ─────────────────
        var thisYear = daily.Where(d => d.Date.Year == target.Year && d.MaxT.HasValue).ToList();
        int? yearPeak = null;
        string? yearPeakDate = null;
        if (thisYear.Count > 0)
        {
            var peakDay = thisYear.OrderByDescending(d => d.MaxT!.Value).ThenBy(d => d.Date).First();
            yearPeak = (int)Math.Round(peakDay.MaxT!.Value);
            yearPeakDate = peakDay.Date.ToString("yyyy-MM-dd");
        }

        int? milestone = yearPeak switch
        {
            >= 100 => 100,
            >= 95  => 95,
            >= 90  => 90,
            _      => null
        };

        int? lastYearAtMilestone = null;
        if (milestone.HasValue)
        {
            var priorYears = daily
                .Where(d => d.Date.Year < target.Year
                         && d.MaxT.HasValue && d.MaxT.Value >= milestone.Value)
                .Select(d => d.Date.Year)
                .ToList();
            if (priorYears.Count > 0)
                lastYearAtMilestone = priorYears.Max();
        }

        // ── Regional peers ────────────────────────────────────────────────
        var peers = await GetPeerRanksAsync(peerStations, target.Year, target.Month);

        // ── Precipitation and the temperature trend for this calendar month ──
        var precipReview = await BuildMonthPrecipAsync(threadexSid, target.Year, target.Month);

        var tempTrend = ComputeTrend(sameMonthHistory.Select(m => (m.Year, m.AvgTemp)).ToList());

        var review = new MonthInReview(
            Year:                     target.Year,
            Month:                    target.Month,
            AvgTemp:                  target.AvgTemp,
            NormalAvgTemp:            normalAvg,
            RankAllMonths:            rankAllMonths,
            TotalMonths:              monthlyMeans.Count,
            RankSameMonth:            rankSameMonth,
            TotalSameMonths:          sameMonth.Count,
            WarmestMonthEver:         topMonths.FirstOrDefault(),
            WarmestSameMonthEver:     topSameMonths.FirstOrDefault(),
            TopMonths:                topMonths,
            TopSameMonths:            topSameMonths,
            SameMonthHistory:         sameMonthHistory,
            LongestStreakInMonth:     longestInMonth,
            LongestStreakEver:        longestEver,
            LastYearWithLongerStreak: lastYearWithLongerStreak,
            LastYearTiedStreak:       lastYearTiedStreak,
            HotDayThreshold:          hotThreshold,
            HotDaysInMonth:           hotDaysInMonth,
            ShowStreak:               showStreak,
            YearPeakHigh:             yearPeak,
            YearPeakHighDate:         yearPeakDate,
            PeakMilestone:            milestone,
            LastYearAtMilestone:      lastYearAtMilestone,
            Peers:                    peers,
            SameMonthTrend:           tempTrend,
            Precip:                   precipReview);

        // Only changes once a month, but keep it short enough that a station whose
        // data lands late still refreshes the same day.
        _cache.Set(cacheKey, review, TimeSpan.FromHours(12));
        return review;
    }

    private async Task<List<PeerMonthRank>> GetPeerRanksAsync(
        IReadOnlyList<Station> peerStations, int year, int month)
    {
        if (peerStations.Count == 0)
            return [];

        var tasks = peerStations
            .Where(s => s.HasAcisData)
            .Select(async station =>
            {
                var means = await GetMonthlyMeansAsync(station.ThreadexSid!);
                var entry = means.FirstOrDefault(m => m.Year == year && m.Month == month);
                if (entry is null) return null;

                var sameMonth = means.Where(m => m.Month == month).ToList();
                var rank = sameMonth.Count(m => m.AvgTemp > entry.AvgTemp) + 1;

                return new PeerMonthRank(
                    station.City, station.State, station.Slug,
                    entry.AvgTemp, rank, sameMonth.Count);
            })
            .ToList();

        var results = await Task.WhenAll(tasks);
        return results.Where(r => r is not null).Select(r => r!).ToList();
    }

    /// <summary>
    /// Runs of consecutive calendar days at or above <paramref name="thresholdF"/>.
    /// A missing observation or a gap in the record ends the run.
    /// </summary>
    private static List<HotStreak> BuildStreaks(
        List<(DateTime Date, double? MaxT)> daily, int thresholdF)
    {
        var streaks = new List<HotStreak>();
        var run = new List<(DateTime Date, double MaxT)>();

        void Flush()
        {
            if (run.Count == 0) return;
            streaks.Add(new HotStreak(
                Days:      run.Count,
                StartDate: run[0].Date.ToString("yyyy-MM-dd"),
                EndDate:   run[^1].Date.ToString("yyyy-MM-dd"),
                Year:      run[0].Date.Year,
                PeakTemp:  (int)Math.Round(run.Max(r => r.MaxT))));
            run.Clear();
        }

        foreach (var (date, maxT) in daily)
        {
            var isHot = maxT.HasValue && maxT.Value >= thresholdF;
            var isConsecutive = run.Count == 0 || run[^1].Date.AddDays(1) == date;

            if (isHot && isConsecutive)
            {
                run.Add((date, maxT!.Value));
            }
            else
            {
                Flush();
                if (isHot) run.Add((date, maxT!.Value));
            }
        }
        Flush();

        return streaks;
    }

    /// <summary>Daily maximum temperatures for the full period of record through yesterday.</summary>
    private async Task<List<(DateTime Date, double? MaxT)>> GetDailyMaxtAsync(string threadexSid)
    {
        var cacheKey = $"acis:dailymaxt:{threadexSid}";
        if (_cache.TryGetValue(cacheKey, out List<(DateTime, double?)>? cached) && cached is not null)
            return cached;

        var edate = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");
        var request = new AcisStnDataRequest
        {
            Sid = threadexSid,
            SDate = "por",
            EDate = edate,
            Elems = [new AcisElement { Name = "maxt" }]
        };

        var response = await PostStnDataAsync(request);
        if (response?.Data is null)
            return [];

        var results = new List<(DateTime, double?)>(response.Data.Count);
        foreach (var row in response.Data)
        {
            if (row.Count < 2) continue;
            if (!DateTime.TryParse(row[0].GetString(), out var date)) continue;
            results.Add((date, ParseDouble(row[1])));
        }

        _cache.Set(cacheKey, results, TimeSpan.FromHours(3));
        return results;
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
