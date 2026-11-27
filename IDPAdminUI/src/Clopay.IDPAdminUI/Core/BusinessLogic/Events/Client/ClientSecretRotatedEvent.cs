using System;
using Clopay.IDPAdminUI.Core.Auditing;

namespace Clopay.IDPAdminUI.Core.BusinessLogic.Events.Client
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
