using System;
using System.Threading.Tasks;
using Clopay.IdentityServer4.Admin.BusinessLogic.Dtos.Log;

namespace Clopay.IdentityServer4.Admin.BusinessLogic.Services.Interfaces
{
    public interface IAuditLogService
    {
        Task<AuditLogsDto> GetAsync(AuditLogFilterDto filters);

        Task DeleteLogsOlderThanAsync(DateTime deleteOlderThan);
    }
}
