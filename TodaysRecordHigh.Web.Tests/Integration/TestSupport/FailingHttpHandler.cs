using System.Net;

namespace TodaysRecordHigh.Web.Tests.Integration.TestSupport;

/// <summary>A handler that always fails the way a real network problem would.</summary>
public class FailingHttpHandler : HttpMessageHandler
{
    private readonly HttpStatusCode? _statusCode;
    private readonly string? _malformedBody;

    private FailingHttpHandler(HttpStatusCode? statusCode, string? malformedBody)
    {
        _statusCode = statusCode;
        _malformedBody = malformedBody;
    }

    public static FailingHttpHandler ReturningStatus(HttpStatusCode code) => new(code, null);
    public static FailingHttpHandler ReturningMalformedJson(string body) => new(null, body);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_statusCode.HasValue)
            return Task.FromResult(new HttpResponseMessage(_statusCode.Value));

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_malformedBody!, System.Text.Encoding.UTF8, "application/json")
        });
    }
}
