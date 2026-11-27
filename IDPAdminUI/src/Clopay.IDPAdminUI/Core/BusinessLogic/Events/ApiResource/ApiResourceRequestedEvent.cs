using Clopay.IDPAdminUI.Core.Auditing;
using Clopay.IDPAdminUI.Core.BusinessLogic.Dtos.Configuration;

namespace Clopay.IDPAdminUI.Core.BusinessLogic.Events.ApiResource
{
    public class ApiResourceRequestedEvent : AuditEvent
    {
        public int ApiResourceId { get; set; }
        public ApiResourceDto ApiResource { get; set; }

        public ApiResourceRequestedEvent(int apiResourceId, ApiResourceDto apiResource)
        {
            ApiResourceId = apiResourceId;
            ApiResource = apiResource;
        }
    }
}