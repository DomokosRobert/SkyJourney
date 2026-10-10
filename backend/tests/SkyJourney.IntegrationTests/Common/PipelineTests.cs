using System.Net;
using Microsoft.AspNetCore.Http;
using Serilog.Events;
using SkyJourney.Api.Common;
using SkyJourney.IntegrationTests.Infrastructure;

namespace SkyJourney.IntegrationTests.Common;

public sealed class PipelineTests(SkyJourneyApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Successful_responses_carry_a_generated_correlation_id()
    {
        var response = await _client.GetAsync("/api/v1/airports?limit=1", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var id = Assert.Single(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName));
        Assert.Matches("^[0-9a-f]{32}$", id);
    }

    [Fact]
    public async Task Problem_responses_echo_a_safe_incoming_correlation_id_in_header_and_body()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/flights/00000000-0000-0000-0000-000000000000");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "client-abc-123");

        var response = await _client.SendAsync(request, Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("client-abc-123", Assert.Single(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName)));
        Assert.Contains("\"correlationId\":\"client-abc-123\"", body);
    }

    [Fact]
    public async Task Unsafe_incoming_correlation_ids_are_replaced()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/airports?limit=1");
        request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, "bad id\twith spaces");

        var response = await _client.SendAsync(request, Ct);

        Assert.Matches("^[0-9a-f]{32}$", Assert.Single(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName)));
    }

    [Theory]
    [InlineData(200, false, LogEventLevel.Information)]
    [InlineData(404, false, LogEventLevel.Information)]
    [InlineData(RequestLogLevel.ClientClosedRequest, false, LogEventLevel.Debug)]
    [InlineData(500, false, LogEventLevel.Error)]
    [InlineData(200, true, LogEventLevel.Error)]
    public void Request_log_level_reflects_the_final_status(int status, bool withException, LogEventLevel expected)
    {
        var context = new DefaultHttpContext { Response = { StatusCode = status } };

        var level = RequestLogLevel.For(context, 1, withException ? new InvalidOperationException() : null);

        Assert.Equal(expected, level);
    }
}
