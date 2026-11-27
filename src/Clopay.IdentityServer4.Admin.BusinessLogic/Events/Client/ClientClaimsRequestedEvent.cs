using Skoruba.AuditLogging.Events;
using Clopay.IdentityServer4.Admin.BusinessLogic.Dtos.Configuration;

namespace Clopay.IdentityServer4.Admin.BusinessLogic.Events.Client
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