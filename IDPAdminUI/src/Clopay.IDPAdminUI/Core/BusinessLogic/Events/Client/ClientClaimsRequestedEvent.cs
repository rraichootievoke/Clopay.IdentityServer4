using Clopay.IDPAdminUI.Core.Auditing;
using Clopay.IDPAdminUI.Core.BusinessLogic.Dtos.Configuration;

namespace Clopay.IDPAdminUI.Core.BusinessLogic.Events.Client
{
    public class ClientClaimsRequestedEvent : AuditEvent
    {
        public ClientClaimsDto ClientClaims { get; set; }

        public ClientClaimsRequestedEvent(ClientClaimsDto clientClaims)
        {
            ClientClaims = clientClaims;
        }
    }
}