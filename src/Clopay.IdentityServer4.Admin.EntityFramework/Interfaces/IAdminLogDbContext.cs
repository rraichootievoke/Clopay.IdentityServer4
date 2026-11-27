using Microsoft.EntityFrameworkCore;
using Clopay.IdentityServer4.Admin.EntityFramework.Entities;

namespace Clopay.IdentityServer4.Admin.EntityFramework.Interfaces
{
    public interface IAdminLogDbContext
    {
        DbSet<Log> Logs { get; set; }
    }
}
