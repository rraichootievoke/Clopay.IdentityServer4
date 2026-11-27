using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Clopay.IDPAdminUI.Configuration;

namespace Clopay.IDPAdminUI.Core.Authorization
{
    /// <summary>The user must appear in AdminConfiguration:AllowedAdmins.</summary>
    public class AdminAllowListRequirement : IAuthorizationRequirement
    {
    }

    public class AdminAllowListHandler : AuthorizationHandler<AdminAllowListRequirement>
    {
        // The IDP only sends some of these (e.g. no preferred_username for users without a dealer), so any of them can match.
        private static readonly string[] IdentifyingClaims =
        {
            "preferred_username", "name", "email", "sub",
            ClaimTypes.Name, ClaimTypes.Email, ClaimTypes.NameIdentifier
        };

        private readonly AdminConfiguration _configuration;

        public AdminAllowListHandler(AdminConfiguration configuration)
        {
            _configuration = configuration;
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminAllowListRequirement requirement)
        {
            var allowed = (_configuration.AllowedAdmins ?? Array.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToArray();

            if (allowed.Length == 0 || context.User?.Identity?.IsAuthenticated != true) return Task.CompletedTask;

            var identifiers = IdentifyingClaims
                .SelectMany(type => context.User.FindAll(type))
                .Select(c => c.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v));

            if (identifiers.Any(v => allowed.Contains(v, StringComparer.OrdinalIgnoreCase)))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
