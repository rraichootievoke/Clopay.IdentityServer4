using Clopay.IdentityServer4.Shared.Configuration.Configuration.Identity;
using Clopay.IdentityServer4.STS.Identity.Configuration.Interfaces;

namespace Clopay.IdentityServer4.STS.Identity.Configuration
{
    public class RootConfiguration : IRootConfiguration
    {      
        public AdminConfiguration AdminConfiguration { get; } = new AdminConfiguration();
        public RegisterConfiguration RegisterConfiguration { get; } = new RegisterConfiguration();
    }
}