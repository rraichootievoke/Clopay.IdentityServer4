using System;

namespace Skoruba.IdentityServer4.Admin.BusinessLogic.Dtos.Configuration
{
    /// <summary>
    /// Result of a secret rotation. <see cref="NewSecret"/> is the only time the plain-text value exists -
    /// only its hash is stored - so the UI shows it once and never again.
    /// </summary>
    public class ClientSecretRotationResultDto
    {
        public int ClientId { get; set; }

        public string ClientIdentifier { get; set; }

        public string ClientName { get; set; }

        public string NewSecret { get; set; }

        /// <summary>Number of previous secrets that were retired (deleted or given an expiry date).</summary>
        public int RetiredSecrets { get; set; }

        /// <summary>0 = previous secrets were revoked immediately.</summary>
        public int GracePeriodDays { get; set; }

        public DateTime? GracePeriodEndsUtc { get; set; }
    }
}
