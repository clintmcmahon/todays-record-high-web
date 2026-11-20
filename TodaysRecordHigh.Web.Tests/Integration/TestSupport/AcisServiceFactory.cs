using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using TodaysRecordHigh.Web.Services;

namespace TodaysRecordHigh.Web.Tests.Integration.TestSupport;

/// <summary>
/// Builds a real AcisService wired to a FakeAcisHttpHandler instead of the network,
/// matching how Program.cs configures the client (same base address, same DI shape).
/// </summary>
public static class AcisServiceFactory
{
    public static (AcisService Service, FakeAcisHttpHandler Handler) Create()
    {
        var handler = new FakeAcisHttpHandler();
        return (CreateWithHandler(handler), handler);
    }

    /// <summary>For error-handling tests: wires AcisService to any HttpMessageHandler,
    /// e.g. one that always returns a 500 or malformed JSON.</summary>
    public static AcisService CreateWithHandler(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://data.rcc-acis.org/")
        };
        var cache = new MemoryCache(new MemoryCacheOptions());
        return new AcisService(http, cache, NullLogger<AcisService>.Instance);
    }
}
