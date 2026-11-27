using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Clopay.IDPAdminUI.Core.Auditing
{
    /// <summary>
    /// Writes audit events to the log (Serilog routes them to Log\audit-YYYYMMDD.log). Nothing is stored in a database.
    /// Each line: who, from where, which event, and the event data as JSON. Secrets are never part of an event.
    /// </summary>
    public class FileAuditEventLogger : IAuditEventLogger
    {
        public const string AuditCategory = "Clopay.IDPAdminUI.Audit";

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None
        };

        private readonly ILogger _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public FileAuditEventLogger(ILoggerFactory loggerFactory, IHttpContextAccessor httpContextAccessor)
        {
            _logger = loggerFactory.CreateLogger(AuditCategory);
            _httpContextAccessor = httpContextAccessor;
        }

        public Task LogEventAsync(AuditEvent auditEvent)
        {
            if (auditEvent == null) return Task.CompletedTask;

            var context = _httpContextAccessor.HttpContext;
            var user = context?.User;
            var who = user?.FindFirst("preferred_username")?.Value
                      ?? user?.FindFirst(ClaimTypes.Name)?.Value
                      ?? user?.FindFirst("sub")?.Value
                      ?? "anonymous";
            var subject = user?.FindFirst("sub")?.Value ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var ip = context?.Connection?.RemoteIpAddress?.ToString();
            var path = context?.Request?.Path.Value;

            string data;
            try
            {
                data = JsonConvert.SerializeObject(auditEvent, JsonSettings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audit event {Event} could not be serialized", auditEvent.Name);
                data = "{}";
            }

            _logger.LogInformation("AUDIT {Event} user={User} subject={Subject} ip={Ip} path={Path} data={Data}",
                auditEvent.Name, who, subject, ip, path, data);

            return Task.CompletedTask;
        }
    }
}
