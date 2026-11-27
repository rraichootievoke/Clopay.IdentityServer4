using Clopay.IDPAdminUI.Core.Auditing;
using Clopay.IDPAdminUI.Core.BusinessLogic.Dtos.Configuration;

namespace Clopay.IDPAdminUI.Core.BusinessLogic.Events.ApiResource
{
    public class ApiResourceAddedEvent : AuditEvent
    {
        public ApiResourceDto ApiResource { get; set; }

        public ApiResourceAddedEvent(ApiResourceDto apiResource)
        {
            ApiResource = apiResource;
        }
    }
}