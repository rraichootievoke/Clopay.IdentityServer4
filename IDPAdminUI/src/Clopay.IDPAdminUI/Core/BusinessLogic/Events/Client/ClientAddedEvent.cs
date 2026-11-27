using Clopay.IDPAdminUI.Core.Auditing;
using Clopay.IDPAdminUI.Core.BusinessLogic.Dtos.Configuration;

namespace Clopay.IDPAdminUI.Core.BusinessLogic.Events.Client
{
    public class ClientAddedEvent : AuditEvent
    {
        public ClientDto Client { get; set; }

        public ClientAddedEvent(ClientDto client)
        {
            Client = client;
        }
    }
}