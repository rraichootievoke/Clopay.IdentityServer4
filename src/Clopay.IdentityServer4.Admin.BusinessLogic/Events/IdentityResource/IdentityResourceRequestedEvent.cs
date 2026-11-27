using Skoruba.AuditLogging.Events;
using Clopay.IdentityServer4.Admin.BusinessLogic.Dtos.Configuration;

namespace Clopay.IdentityServer4.Admin.BusinessLogic.Events.IdentityResource
{
    public class IdentityResourceRequestedEvent : AuditEvent
    {
        public IdentityResourceDto IdentityResource { get; set; }

        public IdentityResourceRequestedEvent(IdentityResourceDto identityResource)
        {
            IdentityResource = identityResource;
        }
    }
}