using Skoruba.AuditLogging.Events;
using Clopay.IdentityServer4.Admin.BusinessLogic.Dtos.Configuration;

namespace Clopay.IdentityServer4.Admin.BusinessLogic.Events.IdentityResource
{
    public class IdentityResourcePropertiesRequestedEvent : AuditEvent
    {
        public IdentityResourcePropertiesDto IdentityResourceProperties { get; set; }

        public IdentityResourcePropertiesRequestedEvent(IdentityResourcePropertiesDto identityResourceProperties)
        {
            IdentityResourceProperties = identityResourceProperties;
        }
    }
}