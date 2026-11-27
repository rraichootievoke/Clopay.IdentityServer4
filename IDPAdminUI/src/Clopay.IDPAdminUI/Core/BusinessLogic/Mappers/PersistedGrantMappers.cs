using AutoMapper;
using IdentityServer4.EntityFramework.Entities;
using Clopay.IDPAdminUI.Core.BusinessLogic.Dtos.Grant;
using Clopay.IDPAdminUI.Infrastructure.Database.Entities;
using Clopay.IDPAdminUI.Infrastructure.Database.Extensions.Common;

namespace Clopay.IDPAdminUI.Core.BusinessLogic.Mappers
{
    public static class PersistedGrantMappers
    {
        static PersistedGrantMappers()
        {
            Mapper = new MapperConfiguration(cfg =>cfg.AddProfile<PersistedGrantMapperProfile>())
                .CreateMapper();
        }

        internal static IMapper Mapper { get; }

        public static PersistedGrantsDto ToModel(this PagedList<PersistedGrantDataView> grant)
        {
            return grant == null ? null : Mapper.Map<PersistedGrantsDto>(grant);
        }

        public static PersistedGrantsDto ToModel(this PagedList<PersistedGrant> grant)
        {
            return grant == null ? null : Mapper.Map<PersistedGrantsDto>(grant);
        }

        public static PersistedGrantDto ToModel(this PersistedGrant grant)
        {
            return grant == null ? null : Mapper.Map<PersistedGrantDto>(grant);
        }
    }
}