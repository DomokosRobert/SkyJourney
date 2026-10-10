using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace SkyJourney.Infrastructure.Identity;

/// <summary>
/// Grants the Admin role to an existing account. Invoked from the command line
/// (<c>dotnet SkyJourney.Api.dll promote-admin user@example.com</c>), never through configuration or an endpoint.
/// </summary>
public static class AdminProvisioning
{
    public const string CommandName = "promote-admin";

    public static async Task<(bool Succeeded, string Message)> PromoteAsync(IServiceProvider services, string email)
    {
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            return (false, $"No account with e-mail '{email}'. Register it in the app first.");
        }

        if (!await roles.RoleExistsAsync(Roles.Admin))
        {
            var created = await roles.CreateAsync(new IdentityRole<Guid>(Roles.Admin) { Id = Guid.CreateVersion7() });
            if (!created.Succeeded)
            {
                return (false, Describe(created));
            }
        }

        if (await users.IsInRoleAsync(user, Roles.Admin))
        {
            return (true, $"{email} is already an administrator.");
        }

        var result = await users.AddToRoleAsync(user, Roles.Admin);
        return result.Succeeded
            ? (true, $"{email} is now an administrator. Existing sessions get the role on their next token refresh.")
            : (false, Describe(result));
    }

    private static string Describe(IdentityResult result) => string.Join(" ", result.Errors.Select(e => e.Description));
}
