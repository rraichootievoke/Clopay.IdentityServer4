using Clopay.IDPAdminUI.Core.Auditing;
using Clopay.IDPAdminUI.Core.BusinessLogic.Dtos.Configuration;

namespace Clopay.IDPAdminUI.Core.BusinessLogic.Events.ApiScope
{
    public class ApiScopeDeletedEvent : AuditEvent
    {
        public ApiScopeDto ApiScope { get; set; }

        public ApiScopeDeletedEvent(ApiScopeDto apiScope)
        {
            ApiScope = apiScope;
        }
    }
}