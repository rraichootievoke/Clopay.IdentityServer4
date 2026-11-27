using Clopay.IDPAdminUI.Core.Auditing;
using Clopay.IDPAdminUI.Core.BusinessLogic.Dtos.Configuration;

namespace Clopay.IDPAdminUI.Core.BusinessLogic.Events.Client
{
    public class ClientPropertyAddedEvent : AuditEvent
    {
        public ClientPropertiesDto ClientProperties { get; set; }

        public ClientPropertyAddedEvent(ClientPropertiesDto clientProperties)
        {
            ClientProperties = clientProperties;
        }
    }
}