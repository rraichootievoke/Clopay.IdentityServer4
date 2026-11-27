using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Clopay.IDPAdminUI.Configuration;

namespace Clopay.IDPAdminUI.Core.Filters
{
    /// <summary>
    /// Safety switch: while AdminConfiguration:ReadOnlyMode is true, every request that is not GET/HEAD/OPTIONS
    /// (i.e. every create / update / delete) is refused before it reaches a controller, so nothing can change.
    /// </summary>
    public class ReadOnlyModeFilter : IActionFilter
    {
        private readonly AdminConfiguration _configuration;

        public ReadOnlyModeFilter(AdminConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (!_configuration.ReadOnlyMode) return;

            var method = context.HttpContext.Request.Method;
            if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method)) return;

            context.Result = new ViewResult
            {
                ViewName = "ReadOnly",
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
