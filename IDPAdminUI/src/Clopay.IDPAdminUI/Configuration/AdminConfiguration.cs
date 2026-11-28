using System;

namespace Clopay.IDPAdminUI.Configuration
{
    /// <summary>
    /// Settings for the admin site itself (appsettings "AdminConfiguration" section).
    /// Administrators sign in through the Clopay IDP (OpenID Connect); only the users in
    /// <see cref="AllowedAdmins"/> may use the site, and nothing can be changed while
    /// <see cref="ReadOnlyMode"/> is on.
    /// </summary>
    public class AdminConfiguration
    {
        public string PageTitle { get; set; } = "IAM Admin";
        public string FaviconUri { get; set; } = "~/favicon.ico";

        /// <summary>Base URL of the Clopay IDP that signs administrators in (the OIDC authority), e.g. https://test-iam.clopay.com</summary>
        public string IdentityServerBaseUrl { get; set; }

        /// <summary>Optional: forces the redirect URI sent to the IDP, e.g. https://test-iamui.clopay.com/signin-oidc</summary>
        public string IdentityAdminRedirectUri { get; set; }

        /// <summary>The IDP client used for administrator sign-in.</summary>
        public string ClientId { get; set; } = "admin_ui";
        public string ClientSecret { get; set; }
        public string OidcResponseType { get; set; } = "code";
        public string[] Scopes { get; set; } = new[] { "openid", "admin_ui_profile" };
        public bool RequireHttpsMetadata { get; set; } = true;

        public string IdentityAdminCookieName { get; set; } = "ClopayIdpAdminUi";
        public double IdentityAdminCookieExpiresUtcHours { get; set; } = 12;

        /// <summary>Claim shown as the user's name in the header.</summary>
        public string TokenValidationClaimName { get; set; } = "preferred_username";

        /// <summary>
        /// Users allowed to use the site. A user matches when ANY of their claims (preferred_username, name, email, sub)
        /// equals one of these values (case-insensitive). Empty list = nobody is allowed.
        /// </summary>
        public string[] AllowedAdmins { get; set; } = Array.Empty<string>();

        /// <summary>When true (the default) every request that would change data (anything other than GET/HEAD) is refused.</summary>
        public bool ReadOnlyMode { get; set; } = true;
    }
}
