using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using IdentityServer4.EntityFramework.Options;
using Microsoft.EntityFrameworkCore;
using Moq;
using Clopay.IDPAdminUI.Core.Auditing;
using Clopay.IDPAdminUI.Core.BusinessLogic.Resources;
using Clopay.IDPAdminUI.Core.BusinessLogic.Services;
using Clopay.IDPAdminUI.Infrastructure.Database.Repositories;
using Clopay.IDPAdminUI.Infrastructure.Database.Shared.DbContexts;
using Clopay.IDPAdminUI.UnitTests.Mocks;
using Xunit;

namespace Clopay.IDPAdminUI.UnitTests.Services
{
    [Trait("Category", "Unit")]
    public class PersistedGrantServiceTests
    {
        private readonly DbContextOptions<IdentityServerPersistedGrantDbContext> _options =
            new DbContextOptionsBuilder<IdentityServerPersistedGrantDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

        private readonly OperationalStoreOptions _storeOptions = new OperationalStoreOptions();

        private PersistedGrantService CreateService(IdentityServerPersistedGrantDbContext context)
        {
            var repository = new PersistedGrantRepository<IdentityServerPersistedGrantDbContext>(context);

            return new PersistedGrantService(
                repository,
                new Mock<IPersistedGrantServiceResources>().Object,
                new Mock<IAuditEventLogger>().Object);
        }

        [Fact]
        public async Task ListsOneRowPerSubject()
        {
            using (var context = new IdentityServerPersistedGrantDbContext(_options, _storeOptions))
            {
                await context.PersistedGrants.AddRangeAsync(
                    PersistedGrantMock.GenerateRandomPersistedGrant("key-1", "subject-a"),
                    PersistedGrantMock.GenerateRandomPersistedGrant("key-2", "subject-a"),
                    PersistedGrantMock.GenerateRandomPersistedGrant("key-3", "subject-b"));
                await context.SaveChangesAsync();

                var result = await CreateService(context).GetPersistedGrantsByUsersAsync(search: string.Empty);

                result.TotalCount.Should().Be(2);
                result.PersistedGrants.Select(x => x.SubjectId).Should().BeEquivalentTo(new[] { "subject-a", "subject-b" });
            }
        }

        [Fact]
        public async Task DeletesOneGrantByKey()
        {
            using (var context = new IdentityServerPersistedGrantDbContext(_options, _storeOptions))
            {
                await context.PersistedGrants.AddRangeAsync(
                    PersistedGrantMock.GenerateRandomPersistedGrant("key-1", "subject-a"),
                    PersistedGrantMock.GenerateRandomPersistedGrant("key-2", "subject-a"));
                await context.SaveChangesAsync();

                await CreateService(context).DeletePersistedGrantAsync("key-1");

                context.PersistedGrants.Select(x => x.Key).Should().BeEquivalentTo(new[] { "key-2" });
            }
        }
    }
}
