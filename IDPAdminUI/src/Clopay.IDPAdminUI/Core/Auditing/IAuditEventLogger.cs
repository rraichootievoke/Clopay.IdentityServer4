using System.Threading.Tasks;

namespace Clopay.IDPAdminUI.Core.Auditing
{
    public interface IAuditEventLogger
    {
        Task LogEventAsync(AuditEvent auditEvent);
    }
}
