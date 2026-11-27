using System;
using Skoruba.AuditLogging.Events;

namespace Clopay.IdentityServer4.Admin.BusinessLogic.Events.Client
{
    public class ClientSecretRotatedEvent : AuditEvent
    {
        public int ClientId { get; set; }

        public int RetiredSecrets { get; set; }

        public int GracePeriodDays { get; set; }

        public DateTime? GracePeriodEndsUtc { get; set; }

        public ClientSecretRotatedEvent(int clientId, int retiredSecrets, int gracePeriodDays, DateTime? gracePeriodEndsUtc)
        {
            ClientId = clientId;
            RetiredSecrets = retiredSecrets;
            GracePeriodDays = gracePeriodDays;
            GracePeriodEndsUtc = gracePeriodEndsUtc;
        }
    }
}
