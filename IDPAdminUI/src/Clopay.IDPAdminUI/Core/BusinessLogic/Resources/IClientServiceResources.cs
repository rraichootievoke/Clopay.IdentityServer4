using Clopay.IDPAdminUI.Core.BusinessLogic.Helpers;

namespace Clopay.IDPAdminUI.Core.BusinessLogic.Resources
{
    public interface IClientServiceResources
    {
        ResourceMessage ClientClaimDoesNotExist();

        ResourceMessage ClientDoesNotExist();

        ResourceMessage ClientExistsKey();

        ResourceMessage ClientExistsValue();

        ResourceMessage ClientPropertyDoesNotExist();

        ResourceMessage ClientSecretDoesNotExist();
    }
}
