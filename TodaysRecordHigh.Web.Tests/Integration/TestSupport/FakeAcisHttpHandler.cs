using System.Net;
using System.Text.Json;

namespace TodaysRecordHigh.Web.Tests.Integration.TestSupport;

/// <summary>One outgoing HTTP call captured for later assertion.</summary>
public record CapturedRequest(string Method, string PathAndQuery, string? Body)
{
    /// <summary>
    /// The request body with form-urlencoding undone. StnData posts a form field named
    /// "params" containing the JSON body (so the wire body is "params=%7B...%7D");
    /// StnMeta calls in this codebase build that same JSON directly, unencoded. Either
    /// way, this is the literal JSON text — safe for both substring matching and parsing.
    /// </summary>
    public string? DecodedBody => Body is null ? null
        : Body.StartsWith("params=") ? Uri.UnescapeDataString(Body["params=".Length..])
        : Body;

    public JsonElement ParamsJson()
    {
        if (DecodedBody is null) throw new InvalidOperationException("Request had no body");
        return JsonDocument.Parse(DecodedBody).RootElement;
    }
}

/// <summary>
/// A test double for the ACIS HTTP endpoint. Routes are matched in registration order
/// against the captured request; the first match's canned JSON is returned. Every
/// request is recorded regardless of whether a route matches, so a test can assert
/// on exactly what AcisService sent even when it doesn't care what came back.
/// </summary>
public class FakeAcisHttpHandler : HttpMessageHandler
{
    private readonly List<(Func<CapturedRequest, bool> Match, string Json)> _routes = [];
    public List<CapturedRequest> Requests { get; } = [];

    /// <summary>Register a canned response for requests where <paramref name="match"/> is true.</summary>
    public FakeAcisHttpHandler On(Func<CapturedRequest, bool> match, string json)
    {
        _routes.Add((match, json));
        return this;
    }

    /// <summary>Matches on the request path only (e.g. "StnData" vs "StnMeta").</summary>
    public FakeAcisHttpHandler OnPath(string path, string json) =>
        On(r => r.PathAndQuery.TrimStart('/').StartsWith(path, StringComparison.OrdinalIgnoreCase), json);

    /// <summary>
    /// Matches StnData requests whose params JSON contains every one of the given
    /// substrings — a pragmatic way to distinguish "monthly avgt" from "monthly pcpn"
    /// from "daily maxt/mint/snow" without fully modeling the request shape twice.
    /// </summary>
    public FakeAcisHttpHandler OnBodyContains(string json, params string[] mustContain) =>
        On(r => r.DecodedBody is not null && mustContain.All(s => r.DecodedBody.Contains(s, StringComparison.Ordinal)), json);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var captured = new CapturedRequest(
            request.Method.Method,
            request.RequestUri?.PathAndQuery ?? "",
            body);
        Requests.Add(captured);

        var route = _routes.FirstOrDefault(r => r.Match(captured));
        if (route.Json is null)
        {
            throw new InvalidOperationException(
                $"FakeAcisHttpHandler: no route matched {captured.Method} {captured.PathAndQuery}\nBody: {body}");
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(route.Json, System.Text.Encoding.UTF8, "application/json")
        };
    }
}
