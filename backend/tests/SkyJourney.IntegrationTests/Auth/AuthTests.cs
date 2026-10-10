using System.Net;
using Microsoft.Extensions.Time.Testing;
using SkyJourney.Application.Auth;
using SkyJourney.Infrastructure.Identity;
using SkyJourney.IntegrationTests.Infrastructure;

namespace SkyJourney.IntegrationTests.Auth;

public sealed class AuthTests(SkyJourneyApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Registration_signs_the_user_in_and_the_access_token_reads_the_profile()
    {
        var email = AuthHelpers.UniqueEmail();

        var auth = await _client.RegisterAsync(email);
        var profile = await (await _client.GetWithTokenAsync("/api/v1/me", auth.AccessToken)).ReadAsAsync<UserProfileDto>();

        Assert.Equal(SkyJourneyApiFactory.StartTime.AddMinutes(15), auth.AccessTokenExpiresAt);
        Assert.Equal(SkyJourneyApiFactory.StartTime.AddDays(30), auth.RefreshTokenExpiresAt);
        Assert.Equal(email, profile.Email);
        Assert.Equal(("Ada", "Lovelace"), (profile.FirstName, profile.LastName));
        Assert.Empty(profile.Roles);
        Assert.Equal(auth.User, profile with { Roles = auth.User.Roles });
    }

    [Fact]
    public async Task Registration_rejects_an_email_that_is_taken_in_any_case()
    {
        var email = AuthHelpers.UniqueEmail();
        await _client.RegisterAsync(email);

        var response = await _client.PostJsonAsync("/api/v1/auth/register",
            new RegisterRequest(email.ToUpperInvariant(), AuthHelpers.Password, "Ada", "Lovelace"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("email_taken", (await response.ReadAsAsync<ProblemResponse>()).Code);
    }

    [Theory]
    [InlineData("alllowercase1")] // no upper case
    [InlineData("NoDigitsHere")]
    public async Task Registration_enforces_password_composition(string password)
    {
        var response = await _client.PostJsonAsync("/api/v1/auth/register",
            new RegisterRequest(AuthHelpers.UniqueEmail(), password, "Ada", "Lovelace"));
        var problem = await response.ReadAsAsync<ProblemResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_failed", problem.Code);
        Assert.True(problem.Errors!.ContainsKey("password"));
    }

    [Fact]
    public async Task Login_returns_tokens_for_valid_credentials()
    {
        var email = AuthHelpers.UniqueEmail();
        await _client.RegisterAsync(email);

        var response = await _client.PostJsonAsync("/api/v1/auth/login", new LoginRequest($"  {email.ToUpperInvariant()} ", AuthHelpers.Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(email, (await response.ReadAsAsync<AuthResponse>()).User.Email);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_fail_identically()
    {
        var email = AuthHelpers.UniqueEmail();
        await _client.RegisterAsync(email);

        var wrongPassword = await _client.PostJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Wrong-Password-1"));
        var unknownEmail = await _client.PostJsonAsync("/api/v1/auth/login", new LoginRequest(AuthHelpers.UniqueEmail(), "Wrong-Password-1"));

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        var first = await wrongPassword.ReadAsAsync<ProblemResponse>();
        var second = await unknownEmail.ReadAsAsync<ProblemResponse>();
        Assert.Equal("invalid_credentials", first.Code);
        Assert.Equal((first.Title, first.Code), (second.Title, second.Code));
    }

    [Fact]
    public async Task Account_locks_after_five_failed_attempts()
    {
        var email = AuthHelpers.UniqueEmail();
        await _client.RegisterAsync(email);

        for (var i = 0; i < 5; i++)
        {
            await _client.PostJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Wrong-Password-1"));
        }

        var response = await _client.PostJsonAsync("/api/v1/auth/login", new LoginRequest(email, AuthHelpers.Password));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("account_locked", (await response.ReadAsAsync<ProblemResponse>()).Code);
    }

    [Fact]
    public async Task Refresh_rotates_the_token()
    {
        var auth = await _client.RegisterAsync();

        var response = await _client.PostJsonAsync("/api/v1/auth/refresh", new RefreshRequest(auth.RefreshToken));
        var rotated = await response.ReadAsAsync<AuthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(auth.RefreshToken, rotated.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetWithTokenAsync("/api/v1/me", rotated.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task Reusing_a_rotated_refresh_token_revokes_the_whole_family()
    {
        var auth = await _client.RegisterAsync();
        var rotated = await (await _client.PostJsonAsync("/api/v1/auth/refresh", new RefreshRequest(auth.RefreshToken)))
            .ReadAsAsync<AuthResponse>();

        var reuse = await _client.PostJsonAsync("/api/v1/auth/refresh", new RefreshRequest(auth.RefreshToken));
        var legitimate = await _client.PostJsonAsync("/api/v1/auth/refresh", new RefreshRequest(rotated.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Equal("invalid_refresh_token", (await reuse.ReadAsAsync<ProblemResponse>()).Code);
        Assert.Equal(HttpStatusCode.Unauthorized, legitimate.StatusCode);
    }

    [Fact]
    public async Task Concurrent_refreshes_with_one_token_let_exactly_one_succeed()
    {
        var auth = await _client.RegisterAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            _client.PostJsonAsync("/api/v1/auth/refresh", new RefreshRequest(auth.RefreshToken))));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.OK),
            r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));
    }

    [Fact]
    public async Task Logout_revokes_the_session_and_is_idempotent()
    {
        var auth = await _client.RegisterAsync();
        var rotated = await (await _client.PostJsonAsync("/api/v1/auth/refresh", new RefreshRequest(auth.RefreshToken)))
            .ReadAsAsync<AuthResponse>();

        var logout = await _client.PostJsonAsync("/api/v1/auth/logout", new LogoutRequest(rotated.RefreshToken));
        var again = await _client.PostJsonAsync("/api/v1/auth/logout", new LogoutRequest(rotated.RefreshToken));
        var unknown = await _client.PostJsonAsync("/api/v1/auth/logout", new LogoutRequest("never-issued"));
        var refresh = await _client.PostJsonAsync("/api/v1/auth/refresh", new RefreshRequest(rotated.RefreshToken));

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Only_hashes_of_refresh_tokens_are_stored()
    {
        var auth = await _client.RegisterAsync();

        var stored = await factory.WithDbAsync(db => Task.FromResult(
            db.RefreshTokens.Where(t => t.UserId == auth.User.Id).Select(t => t.TokenHash).ToList()));

        Assert.Equal([RefreshTokenSecret.Hash(auth.RefreshToken)], stored);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-jwt")]
    public async Task Profile_requires_a_valid_access_token(string? token)
    {
        var response = token is null
            ? await _client.GetAsync("/api/v1/me", Ct)
            : await _client.GetWithTokenAsync("/api/v1/me", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthenticated", (await response.ReadAsAsync<ProblemResponse>()).Code);
    }

    [Fact]
    public async Task Profile_update_trims_and_validates_names()
    {
        var auth = await _client.RegisterAsync();

        using var update = new HttpRequestMessage(HttpMethod.Put, "/api/v1/me")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new UpdateProfileRequest("  Grace ", "Hopper")),
        };
        update.Headers.Authorization = new("Bearer", auth.AccessToken);
        var updated = await (await _client.SendAsync(update, Ct)).ReadAsAsync<UserProfileDto>();

        using var invalid = new HttpRequestMessage(HttpMethod.Put, "/api/v1/me")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new UpdateProfileRequest("", "Hopper")),
        };
        invalid.Headers.Authorization = new("Bearer", auth.AccessToken);
        var rejected = await _client.SendAsync(invalid, Ct);

        Assert.Equal(("Grace", "Hopper"), (updated.FirstName, updated.LastName));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
    }

    [Fact]
    public async Task Access_tokens_expire_and_refresh_tokens_last_thirty_days()
    {
        var clock = new FakeTimeProvider(SkyJourneyApiFactory.StartTime);
        using var host = factory.WithOwnClock(clock);
        var client = host.CreateClient();
        var auth = await client.RegisterAsync();

        clock.Advance(TimeSpan.FromMinutes(16));
        var expired = await client.GetWithTokenAsync("/api/v1/me", auth.AccessToken);
        var refreshed = await client.PostJsonAsync("/api/v1/auth/refresh", new RefreshRequest(auth.RefreshToken));
        var rotated = await refreshed.ReadAsAsync<AuthResponse>();

        clock.Advance(TimeSpan.FromDays(31));
        var stale = await client.PostJsonAsync("/api/v1/auth/refresh", new RefreshRequest(rotated.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);
    }

    [Fact]
    public async Task Promoted_admins_receive_the_role_on_their_next_token()
    {
        var email = AuthHelpers.UniqueEmail("admin");
        var auth = await _client.RegisterAsync(email);

        var (succeeded, _) = await AdminProvisioning.PromoteAsync(factory.Services, email);
        var (again, message) = await AdminProvisioning.PromoteAsync(factory.Services, email);
        var refreshed = await (await _client.PostJsonAsync("/api/v1/auth/refresh", new RefreshRequest(auth.RefreshToken)))
            .ReadAsAsync<AuthResponse>();

        Assert.True(succeeded);
        Assert.True(again);
        Assert.Contains("already", message);
        Assert.Empty(auth.User.Roles);
        Assert.Equal([Roles.Admin], refreshed.User.Roles);
    }

    [Fact]
    public async Task Promoting_an_unknown_email_fails() =>
        Assert.False((await AdminProvisioning.PromoteAsync(factory.Services, AuthHelpers.UniqueEmail())).Succeeded);
}
