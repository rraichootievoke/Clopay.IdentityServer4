using Skoruba.AuditLogging.Events;
namespace Clopay.IdentityServer4.Admin.BusinessLogic.Identity.Events.Identity
{
    public class RolesRequestedEvent<TRolesDto> : AuditEvent
    {
        public TRolesDto Roles { get; set; }

        public RolesRequestedEvent(TRolesDto roles)
        {
            Roles = roles;
        }
    }
}