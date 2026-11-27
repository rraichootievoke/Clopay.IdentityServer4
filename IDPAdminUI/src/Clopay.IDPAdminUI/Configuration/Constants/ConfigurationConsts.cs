namespace Clopay.IDPAdminUI.Configuration.Constants
{
    public class ConfigurationConsts
    {
        /// <summary>The IDP database - IdentityServer4 configuration store (clients, resources, scopes).</summary>
        public const string ConfigurationDbConnectionStringKey = "ConfigurationDbConnection";

        /// <summary>The IDP database - IdentityServer4 operational store (persisted grants).</summary>
        public const string PersistedGrantDbConnectionStringKey = "PersistedGrantDbConnection";

        public const string ResourcesPath = "Resources";
    }
}
