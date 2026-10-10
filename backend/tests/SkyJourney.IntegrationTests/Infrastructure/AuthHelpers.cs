using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using SkyJourney.Application.Auth;

namespace SkyJourney.IntegrationTests.Infrastructure;

public static class AuthHelpers
{
    public const string Password = "Correct-Horse-9";

    public static string UniqueEmail(string prefix = "user") => $"{prefix}-{Guid.NewGuid():N}@example.com";

    public static async Task<AuthResponse> RegisterAsync(this HttpClient client, string? email = null, string password = Password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest(email ?? UniqueEmail(), password, "Ada", "Lovelace"), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.ReadAsAsync<AuthResponse>();
    }

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PostAsJsonAsync(url, body, TestContext.Current.CancellationToken);

    public static async Task<HttpResponseMessage> GetWithTokenAsync(this HttpClient client, string url, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// A second in-memory host on the same database with its own clock (and its own ephemeral signing key),
    /// for tests that move time forward without disturbing tests that rely on the frozen shared clock.
    /// </summary>
    public static WebApplicationFactory<Program> WithOwnClock(this SkyJourneyApiFactory factory, FakeTimeProvider clock,
        params (string Key, string Value)[] settings) =>
        factory.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);
            });
        });
}
