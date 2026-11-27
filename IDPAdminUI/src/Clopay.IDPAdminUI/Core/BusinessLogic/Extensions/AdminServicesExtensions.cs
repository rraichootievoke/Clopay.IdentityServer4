using Microsoft.EntityFrameworkCore;
using Clopay.IDPAdminUI.Core.BusinessLogic.Resources;
using Clopay.IDPAdminUI.Core.BusinessLogic.Services;
using Clopay.IDPAdminUI.Core.BusinessLogic.Services.Interfaces;
using Clopay.IDPAdminUI.Infrastructure.Database.Interfaces;
using Clopay.IDPAdminUI.Infrastructure.Database.Repositories;
using Clopay.IDPAdminUI.Infrastructure.Database.Repositories.Interfaces;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class AdminServicesExtensions
    {
        /// <summary>
        /// Registers the repositories, services and resource-message providers for the IdentityServer4 configuration
        /// store (clients, resources, scopes) and the operational store (persisted grants).
        /// </summary>
        public static IServiceCollection AddAdminServices<TConfigurationDbContext, TPersistedGrantDbContext>(this IServiceCollection services)
            where TPersistedGrantDbContext : DbContext, IAdminPersistedGrantDbContext
            where TConfigurationDbContext : DbContext, IAdminConfigurationDbContext
        {
            //Repositories
            services.AddTransient<IClientRepository, ClientRepository<TConfigurationDbContext>>();
            services.AddTransient<IIdentityResourceRepository, IdentityResourceRepository<TConfigurationDbContext>>();
            services.AddTransient<IApiResourceRepository, ApiResourceRepository<TConfigurationDbContext>>();
            services.AddTransient<IApiScopeRepository, ApiScopeRepository<TConfigurationDbContext>>();
            services.AddTransient<IPersistedGrantRepository, PersistedGrantRepository<TPersistedGrantDbContext>>();

            //Services
            services.AddTransient<IClientService, ClientService>();
            services.AddTransient<IApiResourceService, ApiResourceService>();
            services.AddTransient<IApiScopeService, ApiScopeService>();
            services.AddTransient<IIdentityResourceService, IdentityResourceService>();
            services.AddTransient<IPersistedGrantService, PersistedGrantService>();

            //Resources
            services.AddScoped<IApiResourceServiceResources, ApiResourceServiceResources>();
            services.AddScoped<IApiScopeServiceResources, ApiScopeServiceResources>();
            services.AddScoped<IClientServiceResources, ClientServiceResources>();
            services.AddScoped<IIdentityResourceServiceResources, IdentityResourceServiceResources>();
            services.AddScoped<IPersistedGrantServiceResources, PersistedGrantServiceResources>();

            return services;
        }
    }
}
