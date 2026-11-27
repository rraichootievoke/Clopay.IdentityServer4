using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Clopay.IDPAdminUI.Configuration.Constants;

namespace Clopay.IDPAdminUI.Configuration
{
    /// <summary>
    /// Fails fast, with a readable message, when the settings still contain placeholders such as &lt;DB_PASSWORD&gt;
    /// or required values are missing - instead of a cryptic error on the first request.
    /// </summary>
    internal static class StartupChecks
    {
        private static readonly Regex Placeholder = new Regex("<[A-Za-z0-9_]{3,}>", RegexOptions.Compiled);

        public static void Validate(AdminConfiguration admin, IConfiguration configuration)
        {
            var problems = new List<string>();

            Require(problems, "AdminConfiguration:IdentityServerBaseUrl", admin.IdentityServerBaseUrl);
            Require(problems, "AdminConfiguration:ClientId", admin.ClientId);
            Require(problems, "AdminConfiguration:ClientSecret", admin.ClientSecret);
            Require(problems, "AdminConfiguration:IdentityAdminRedirectUri", admin.IdentityAdminRedirectUri, optional: true);

            Require(problems, "ConnectionStrings:" + ConfigurationConsts.ConfigurationDbConnectionStringKey,
                configuration.GetConnectionString(ConfigurationConsts.ConfigurationDbConnectionStringKey));
            Require(problems, "ConnectionStrings:" + ConfigurationConsts.PersistedGrantDbConnectionStringKey,
                configuration.GetConnectionString(ConfigurationConsts.PersistedGrantDbConnectionStringKey));

            if (problems.Count > 0)
            {
                throw new InvalidOperationException(
                    "The IDP Admin UI settings are incomplete. Fix these (appsettings.<Environment>.json, or environment variables such as " +
                    "AdminConfiguration__ClientSecret / ConnectionStrings__ConfigurationDbConnection):" + Environment.NewLine +
                    " - " + string.Join(Environment.NewLine + " - ", problems));
            }
        }

        private static void Require(List<string> problems, string key, string value, bool optional = false)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                if (!optional) problems.Add($"{key} is empty");
                return;
            }

            var match = Placeholder.Match(value);
            if (match.Success)
            {
                problems.Add($"{key} still contains the placeholder {match.Value}");
            }
        }
    }
}
