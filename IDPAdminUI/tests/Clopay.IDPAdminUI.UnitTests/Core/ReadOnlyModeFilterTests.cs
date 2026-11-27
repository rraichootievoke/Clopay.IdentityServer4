using System.Collections.Generic;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Clopay.IDPAdminUI.Configuration;
using Clopay.IDPAdminUI.Core.Filters;
using Xunit;

namespace Clopay.IDPAdminUI.UnitTests.Core
{
    [Trait("Category", "Unit")]
    public class ReadOnlyModeFilterTests
    {
        private static ActionExecutingContext Run(bool readOnly, string method)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = method;
            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
            var context = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object>(), controller: null);

            new ReadOnlyModeFilter(new AdminConfiguration { ReadOnlyMode = readOnly }).OnActionExecuting(context);

            return context;
        }

        [Theory]
        [InlineData("POST")]
        [InlineData("PUT")]
        [InlineData("DELETE")]
        [InlineData("PATCH")]
        public void RefusesEveryChangingRequestInReadOnlyMode(string method)
        {
            var context = Run(readOnly: true, method);

            var result = context.Result.Should().BeOfType<ViewResult>().Subject;
            result.ViewName.Should().Be("ReadOnly");
            result.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        }

        [Theory]
        [InlineData("GET")]
        [InlineData("HEAD")]
        [InlineData("OPTIONS")]
        public void AllowsReadingInReadOnlyMode(string method)
        {
            Run(readOnly: true, method).Result.Should().BeNull();
        }

        [Fact]
        public void AllowsChangesWhenReadOnlyModeIsOff()
        {
            Run(readOnly: false, "POST").Result.Should().BeNull();
        }
    }
}
