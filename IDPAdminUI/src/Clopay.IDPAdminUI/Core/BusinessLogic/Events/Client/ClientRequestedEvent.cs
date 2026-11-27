using Clopay.IDPAdminUI.Core.Auditing;
using Clopay.IDPAdminUI.Core.BusinessLogic.Dtos.Configuration;

namespace Clopay.IDPAdminUI.Core.BusinessLogic.Events.Client
{
    public class ClientRequestedEvent : AuditEvent
    {
        public ClientDto ClientDto { get; set; }

        public ClientRequestedEvent(ClientDto clientDto)
        {
            ClientDto = clientDto;
        }
    }
}