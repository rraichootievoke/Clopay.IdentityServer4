using Clopay.IdentityServer4.Admin.BusinessLogic.Helpers;

namespace Clopay.IdentityServer4.Admin.BusinessLogic.Resources
{
    public interface IPersistedGrantServiceResources
    {
        ResourceMessage PersistedGrantDoesNotExist();

        ResourceMessage PersistedGrantWithSubjectIdDoesNotExist();
    }
}
