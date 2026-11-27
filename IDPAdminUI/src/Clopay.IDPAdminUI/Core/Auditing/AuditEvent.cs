using System;

namespace Clopay.IDPAdminUI.Core.Auditing
{
    /// <summary>Base class of every audit event (who changed what). Events are written to the audit log file.</summary>
    public abstract class AuditEvent
    {
        public DateTime OccurredUtc { get; } = DateTime.UtcNow;

        public virtual string Name => GetType().Name;
    }
}
