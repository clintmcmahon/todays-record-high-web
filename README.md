# Today's Record High

**[todaysrecordhigh.com](https://todaysrecordhigh.com)**

Ever hear on the news that a city "broke a record" and think — okay, but what *was* the record? How close did we actually get? Is this a fluke or a pattern?

That's the whole reason this exists. Pick a city, and you'll see the hottest it has ever been on today's date, the coldest, what's normal, and what it's doing right now. Around 435 U.S. weather stations, most with daily observations going back more than a century.

## What you can poke at

- **Today at a glance** — record high, record low, the 1991–2020 normal, and current conditions side by side for a set of major cities.
- **A map** — pan around and find whatever station is closest to you.
- **Station pages** — the fun part. How this year compares to normal, how summer overnight lows have shifted decade over decade, how rainfall stacks up against history, and where the heat wave threshold sits for that specific station.

All the historical data comes from [RCC ACIS](https://www.rcc-acis.org/), which distributes NOAA's Threadex archive. Current conditions come from [Open-Meteo](https://open-meteo.com/), and active alerts from the [National Weather Service](https://www.weather.gov/documentation/services-web-api). No scraping, no paid tiers — just good public data.

## Running it yourself

You'll need the [.NET 8 SDK](https://dotnet.microsoft.com/download). After that:

```bash
git clone https://github.com/clintmcmahon/todays-record-high-web.git
cd todays-record-high-web/TodaysRecordHigh.Web
dotnet run
```

That's genuinely it — no database, no API keys required, no migrations to run. The station list loads from JSON at startup and everything else is fetched live and cached in memory.

### One optional bit: the map

The map needs a Mapbox token. Without one the site works fine, you'll just get an empty rectangle where the map should be. If you want it:

```bash
dotnet user-secrets set "MapboxToken" "pk.your_token_here"
```

Grab a free `pk.` token from [Mapbox](https://account.mapbox.com/). User-secrets keep it off disk in the repo, which is why `appsettings.json` ships with the key blank.

## There's an API too

Everything under `/api/` is public, read-only, GET-only, and CORS-open — help yourself.

| Endpoint | What it gives you |
| --- | --- |
| `GET /api/stations` | Every station, with coordinates and slugs |
| `GET /api/stations/{slug}` | One station's details |
| `GET /api/records/today?slugs=denver-co,chicago-il` | Today's records + current conditions for a comma-separated batch |

Slugs look like `minneapolis-mn` — city, dash, state code.

## Under the hood

ASP.NET Core 8 MVC, Bootstrap 5, and Mapbox GL JS for the map. Responses from ACIS and Open-Meteo get cached in `IMemoryCache`, which keeps things quick and keeps the upstream APIs happy.

Deploys are a push to `main` — GitHub Actions publishes, rsyncs to a Vultr box, and restarts the systemd service.

## A fair warning about the data

Each station's "record" only covers its own period of record, and those vary a lot. A record high of 98°F at a station with data back to 1900 means something pretty different from the same number at one that started in 1975. Every station page shows its date range so you can judge for yourself.

## Say hi

Found a bug, spotted a record that looks wrong, or want a station added? I'd honestly like to hear about it — [hello@todaysrecordhigh.com](mailto:hello@todaysrecordhigh.com).

Built by [Clint McMahon](https://clintmcmahon.com) in Minneapolis, MN.
