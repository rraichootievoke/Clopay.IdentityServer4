using Clopay.IDPAdminUI.Core.Auditing;
using Clopay.IDPAdminUI.Core.BusinessLogic.Dtos.Configuration;

namespace Clopay.IDPAdminUI.Core.BusinessLogic.Events.Client
{
    public class ClientPropertyDeletedEvent : AuditEvent
    {
        public ClientPropertiesDto ClientProperty { get; set; }

        public ClientPropertyDeletedEvent(ClientPropertiesDto clientProperty)
        {
            ClientProperty = clientProperty;
        }
    }
}