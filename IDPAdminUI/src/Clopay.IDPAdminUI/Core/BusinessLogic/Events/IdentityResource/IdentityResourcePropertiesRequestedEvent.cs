using Clopay.IDPAdminUI.Core.Auditing;
using Clopay.IDPAdminUI.Core.BusinessLogic.Dtos.Configuration;

namespace Clopay.IDPAdminUI.Core.BusinessLogic.Events.IdentityResource
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