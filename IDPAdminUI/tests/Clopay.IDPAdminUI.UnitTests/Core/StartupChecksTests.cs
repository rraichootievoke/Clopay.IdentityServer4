using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Clopay.IDPAdminUI.Configuration;
using Xunit;

namespace Clopay.IDPAdminUI.UnitTests.Core
{
    [Trait("Category", "Unit")]
    public class StartupChecksTests
    {
        private const string GoodConnection = "Data Source=server;Initial Catalog=IdentityServerDB_DBTest;User ID=reader;Password=secret";

        private static IConfiguration Config(string connection = GoodConnection) => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionStrings:ConfigurationDbConnection"] = connection,
                ["ConnectionStrings:PersistedGrantDbConnection"] = connection
            })
            .Build();

        private static AdminConfiguration Admin() => new AdminConfiguration
        {
            IdentityServerBaseUrl = "https://test-iam.clopay.com",
            ClientId = "adminui-test",
            ClientSecret = "s3cret"
        };

        [Fact]
        public void AcceptsCompleteSettings()
        {
            Action act = () => StartupChecks.Validate(Admin(), Config());

            act.Should().NotThrow();
        }

        [Fact]
        public void RejectsPlaceholderInConnectionString()
        {
            Action act = () => StartupChecks.Validate(Admin(), Config("Data Source=server;Password=<DB_PASSWORD>"));

            act.Should().Throw<InvalidOperationException>().WithMessage("*<DB_PASSWORD>*");
        }

        [Fact]
        public void RejectsPlaceholderClientSecretAndMissingClientId()
        {
            var admin = Admin();
            admin.ClientSecret = "<ADMIN_UI_CLIENT_SECRET>";
            admin.ClientId = "";

            Action act = () => StartupChecks.Validate(admin, Config());

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*ClientSecret*placeholder*")
                .WithMessage("*ClientId is empty*");
        }
    }
}
