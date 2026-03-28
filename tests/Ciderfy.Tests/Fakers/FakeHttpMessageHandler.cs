using System.Net;
using Ciderfy.Web;

namespace Ciderfy.Tests.Fakers;

internal sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken ct
    ) => Task.FromResult(handler(request));

    internal static HttpClient Returning(HttpStatusCode status, string body = "") =>
        new(
            new FakeHttpMessageHandler(_ => new HttpResponseMessage(status)
            {
                Content = new StringContent(body),
            })
        );

    internal static HttpClient ReturningJson(
        string json,
        HttpStatusCode status = HttpStatusCode.OK
    ) =>
        new(
            new FakeHttpMessageHandler(_ => new HttpResponseMessage(status)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, MimeTypes.Json),
            })
        );

    internal static HttpClient ReturningResponse(
        HttpStatusCode status,
        Action<HttpResponseMessage>? configure = null
    ) =>
        new(
            new FakeHttpMessageHandler(_ =>
            {
                var response = new HttpResponseMessage(status);
                configure?.Invoke(response);
                return response;
            })
        );

    // When the HTTP client should never be called (e.g. short-circuit paths)
    internal static HttpClient ThrowOnCall() =>
        new(
            new FakeHttpMessageHandler(_ =>
                throw new InvalidOperationException("HTTP should not be called in this test")
            )
        );

    // Simulates a network-level failure
    internal static HttpClient ThrowingHttpRequestException(string message = "network error") =>
        new(new FakeHttpMessageHandler(_ => throw new HttpRequestException(message)));

    // Simulates a TaskCanceledException from HttpClient timeout
    // (not triggered by the caller's CancellationToken)
    internal static HttpClient ThrowingTimeoutCanceledException() =>
        new(new FakeHttpMessageHandler(_ => throw new TaskCanceledException("timeout")));
}
