using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Clopay.IDPAdminUI.Configuration;
using Clopay.IDPAdminUI.Core.Authorization;
using Xunit;

namespace Clopay.IDPAdminUI.UnitTests.Core
{
    [Trait("Category", "Unit")]
    public class AdminAllowListHandlerTests
    {
        private static async Task<bool> IsAllowed(string[] allowed, params Claim[] claims)
        {
            var handler = new AdminAllowListHandler(new AdminConfiguration { AllowedAdmins = allowed });
            var requirement = new AdminAllowListRequirement();
            var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
            var context = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement }, user, null);

            await handler.HandleAsync(context);

            return context.HasSucceeded;
        }

        [Fact]
        public async Task AllowsUserListedByPreferredUsername()
        {
            (await IsAllowed(new[] { "jane.doe" }, new Claim("preferred_username", "jane.doe"))).Should().BeTrue();
        }

        [Fact]
        public async Task MatchIsCaseInsensitive()
        {
            (await IsAllowed(new[] { "Jane.Doe" }, new Claim("preferred_username", "jane.doe"))).Should().BeTrue();
        }

        [Fact]
        public async Task AllowsUserListedBySubjectId()
        {
            (await IsAllowed(new[] { "5f2a-77" }, new Claim("sub", "5f2a-77"))).Should().BeTrue();
        }

        [Fact]
        public async Task DeniesUserWhoIsNotListed()
        {
            (await IsAllowed(new[] { "jane.doe" }, new Claim("preferred_username", "someone.else"))).Should().BeFalse();
        }

        [Fact]
        public async Task DeniesEveryoneWhenTheListIsEmpty()
        {
            (await IsAllowed(new string[0], new Claim("preferred_username", "jane.doe"))).Should().BeFalse();
        }

        [Fact]
        public async Task DeniesAnonymousUsers()
        {
            var handler = new AdminAllowListHandler(new AdminConfiguration { AllowedAdmins = new[] { "jane.doe" } });
            var requirement = new AdminAllowListRequirement();
            var context = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement }, new ClaimsPrincipal(new ClaimsIdentity()), null);

            await handler.HandleAsync(context);

            context.HasSucceeded.Should().BeFalse();
        }
    }
}
