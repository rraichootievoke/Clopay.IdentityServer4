using Clopay.IDPAdminUI.Core.Auditing;
using Clopay.IDPAdminUI.Core.BusinessLogic.Dtos.Grant;

namespace Clopay.IDPAdminUI.Core.BusinessLogic.Events.PersistedGrant
{
    public class PersistedGrantRequestedEvent : AuditEvent
    {
        public PersistedGrantDto PersistedGrant { get; set; }

        public PersistedGrantRequestedEvent(PersistedGrantDto persistedGrant)
        {
            PersistedGrant = persistedGrant;
        }
    }
}