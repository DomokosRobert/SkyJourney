using System.Net;
using Microsoft.Extensions.Time.Testing;
using SkyJourney.Application.Auth;
using SkyJourney.IntegrationTests.Infrastructure;

namespace SkyJourney.IntegrationTests.Auth;

public sealed class RateLimitTests(SkyJourneyApiFactory factory)
{
    [Fact]
    public async Task Auth_endpoints_are_rate_limited_per_client()
    {
        using var host = factory.WithOwnClock(new FakeTimeProvider(SkyJourneyApiFactory.StartTime),
            ("RateLimiting:AuthPermitLimit", "3"), ("RateLimiting:AuthWindowSeconds", "60"));
        var client = host.CreateClient();
        var login = new LoginRequest(AuthHelpers.UniqueEmail(), "Wrong-Password-1");

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            statuses.Add((await client.PostJsonAsync("/api/v1/auth/login", login)).StatusCode);
        }

        var limited = await client.PostJsonAsync("/api/v1/auth/login", login);

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests], statuses);
        Assert.Equal("rate_limited", (await limited.ReadAsAsync<ProblemResponse>()).Code);
        Assert.True(limited.Headers.Contains("Retry-After"));
        // Anonymous endpoints outside /auth are not limited.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/airports?limit=1", TestContext.Current.CancellationToken)).StatusCode);
    }
}
