using Clopay.IDPAdminUI.Core.BusinessLogic.Helpers;

namespace Clopay.IDPAdminUI.Core.BusinessLogic.Resources
{
    public interface IPersistedGrantServiceResources
    {
        ResourceMessage PersistedGrantDoesNotExist();

        ResourceMessage PersistedGrantWithSubjectIdDoesNotExist();
    }
}
