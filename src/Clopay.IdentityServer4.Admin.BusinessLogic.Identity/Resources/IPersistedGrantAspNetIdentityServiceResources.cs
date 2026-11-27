using Clopay.IdentityServer4.Admin.BusinessLogic.Identity.Helpers;

namespace Clopay.IdentityServer4.Admin.BusinessLogic.Identity.Resources
{
    public interface IPersistedGrantAspNetIdentityServiceResources
    {
        ResourceMessage PersistedGrantDoesNotExist();

        ResourceMessage PersistedGrantWithSubjectIdDoesNotExist();
    }
}
