using Clopay.IDPAdminUI.Core.Auditing;

namespace Clopay.IDPAdminUI.Core.BusinessLogic.Events.PersistedGrant
{
    public class PersistedGrantsDeletedEvent : AuditEvent
    {
        public string UserId { get; set; }

        public PersistedGrantsDeletedEvent(string userId)
        {
            UserId = userId;
        }
    }
}