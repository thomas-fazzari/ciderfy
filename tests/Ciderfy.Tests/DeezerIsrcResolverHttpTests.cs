using System.Net;
using Ciderfy.Configuration.Options;
using Ciderfy.Matching;
using Ciderfy.Tests.Fakers;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ciderfy.Tests;

public class DeezerIsrcResolverHttpTests
{
    private static readonly IOptions<DeezerClientOptions> _fastOptions = Options.Create(
        new DeezerClientOptions { RateLimitDelayMs = 1 }
    );

    private static readonly TrackMetadata _track = SpotifyTrackFaker.Default.Generate();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ResolveIsrcsAsync_HttpError_ReturnsTrackWithNullIsrc()
    {
        using var client = FakeHttpMessageHandler.Returning(HttpStatusCode.InternalServerError);
        using var resolver = new DeezerIsrcResolver(client, _fastOptions);

        var results = await resolver.ResolveIsrcsAsync([_track], ct: Ct);

        Assert.Single(results);
        Assert.Null(results[0].Isrc);
    }

    [Fact]
    public async Task ResolveIsrcsAsync_HttpRequestException_ReturnsTrackWithNullIsrc()
    {
        using var client = FakeHttpMessageHandler.ThrowingHttpRequestException();
        using var resolver = new DeezerIsrcResolver(client, _fastOptions);

        var results = await resolver.ResolveIsrcsAsync([_track], ct: Ct);

        Assert.Single(results);
        Assert.Null(results[0].Isrc);
    }

    [Fact]
    public async Task ResolveIsrcsAsync_TimeoutCanceledException_ReturnsTrackWithNullIsrc()
    {
        using var client = FakeHttpMessageHandler.ThrowingTimeoutCanceledException();
        using var resolver = new DeezerIsrcResolver(client, _fastOptions);

        var results = await resolver.ResolveIsrcsAsync([_track], ct: Ct);

        Assert.Single(results);
        Assert.Null(results[0].Isrc);
    }

    [Fact]
    public async Task ResolveIsrcsAsync_EmptyDataArray_ReturnsTrackWithNullIsrc()
    {
        const string json = """{ "data": [] }""";
        using var client = FakeHttpMessageHandler.ReturningJson(json);
        using var resolver = new DeezerIsrcResolver(client, _fastOptions);

        var results = await resolver.ResolveIsrcsAsync([_track], ct: Ct);

        Assert.Single(results);
        Assert.Null(results[0].Isrc);
    }

    [Fact]
    public async Task ResolveIsrcsAsync_ValidResponse_PopulatesIsrc()
    {
        const string json = """{ "data": [{ "isrc": "GBAYE0601498" }] }""";
        using var client = FakeHttpMessageHandler.ReturningJson(json);
        using var resolver = new DeezerIsrcResolver(client, _fastOptions);

        var results = await resolver.ResolveIsrcsAsync([_track], ct: Ct);

        Assert.Single(results);
        Assert.Equal("GBAYE0601498", results[0].Isrc);
    }
}
