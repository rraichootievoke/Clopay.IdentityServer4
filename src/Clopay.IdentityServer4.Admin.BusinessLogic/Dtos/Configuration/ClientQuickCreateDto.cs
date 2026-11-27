using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Clopay.IdentityServer4.Admin.BusinessLogic.Dtos.Configuration
{
    public enum QuickClientTemplate
    {
        /// <summary>Server-side web app: authorization code + PKCE, with a client secret.</summary>
        WebApp = 1,

        /// <summary>Browser / Angular app: authorization code + PKCE, no secret.</summary>
        Spa = 2,

        /// <summary>Service-to-service: client credentials with a client secret.</summary>
        Machine = 3,

        /// <summary>Username + password (resource owner password) with a client secret - used by legacy apps such as CCAApp.</summary>
        LegacyPassword = 4
    }

    /// <summary>Everything needed to create a working client in one step.</summary>
    public class ClientQuickCreateDto
    {
        public ClientQuickCreateDto()
        {
            SelectedScopes = new List<string>();
            AvailableScopes = new List<string>();
        }

        [Required]
        [RegularExpression(@"^[A-Za-z0-9_.\-:]+$", ErrorMessage = "Use letters, numbers and . _ - : only (no spaces).")]
        [StringLength(200)]
        public string ClientId { get; set; }

        [StringLength(200)]
        public string ClientName { get; set; }

        [StringLength(1000)]
        public string Description { get; set; }

        public QuickClientTemplate Template { get; set; } = QuickClientTemplate.WebApp;

        /// <summary>One URL per line.</summary>
        public string RedirectUris { get; set; }

        /// <summary>One URL per line.</summary>
        public string PostLogoutRedirectUris { get; set; }

        /// <summary>One origin per line, e.g. https://dev-cca.clopay.com</summary>
        public string AllowedCorsOrigins { get; set; }

        public List<string> SelectedScopes { get; set; }

        /// <summary>Extra scopes typed in by hand, separated by space, comma or new line.</summary>
        public string ExtraScopes { get; set; }

        /// <summary>Populated by the controller for the view; every scope that exists in the database.</summary>
        public List<string> AvailableScopes { get; set; }
    }

    public class ClientQuickCreateResultDto
    {
        public int Id { get; set; }

        public string ClientId { get; set; }

        public string ClientName { get; set; }

        /// <summary>Plain-text secret, shown once. Null for clients that do not use a secret.</summary>
        public string Secret { get; set; }

        public QuickClientTemplate Template { get; set; }
    }
}
