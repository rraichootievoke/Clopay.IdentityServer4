using Skoruba.AuditLogging.Events;
using Clopay.IdentityServer4.Admin.BusinessLogic.Identity.Dtos.Identity;

namespace Clopay.IdentityServer4.Admin.BusinessLogic.Identity.Events.Identity
{
    public class RoleUsersRequestedEvent<TUsersDto> : AuditEvent
    {
        public TUsersDto Users { get; set; }

        public RoleUsersRequestedEvent(TUsersDto users)
        {
            Users = users;
        }
    }
}