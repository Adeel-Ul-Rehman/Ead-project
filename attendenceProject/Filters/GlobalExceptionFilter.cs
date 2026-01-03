using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Net;

namespace attendenceProject.Filters
{
    /// <summary>
    /// Global exception filter to handle all unhandled exceptions
    /// </summary>
    public class GlobalExceptionFilter : IExceptionFilter
    {
        private readonly ILogger<GlobalExceptionFilter> _logger;
        private readonly IWebHostEnvironment _environment;

        public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger, IWebHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
        }

        public void OnException(ExceptionContext context)
        {
            _logger.LogError(context.Exception, "Unhandled exception occurred: {Message}", context.Exception.Message);

            var statusCode = context.Exception switch
            {
                UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
                KeyNotFoundException => (int)HttpStatusCode.NotFound,
                ArgumentException => (int)HttpStatusCode.BadRequest,
                InvalidOperationException => (int)HttpStatusCode.BadRequest,
                _ => (int)HttpStatusCode.InternalServerError
            };

            var errorMessage = _environment.IsDevelopment()
                ? context.Exception.Message
                : "An error occurred while processing your request.";

            var stackTrace = _environment.IsDevelopment()
                ? context.Exception.StackTrace
                : null;

            // Check if request is AJAX/API call
            var isAjaxCall = context.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                           context.HttpContext.Request.Path.StartsWithSegments("/api") ||
                           context.HttpContext.Request.Headers["Accept"].ToString().Contains("application/json");

            if (isAjaxCall)
            {
                // Return JSON response for AJAX calls
                context.Result = new JsonResult(new
                {
                    success = false,
                    message = errorMessage,
                    statusCode,
                    stackTrace
                })
                {
                    StatusCode = statusCode
                };
            }
            else
            {
                // Redirect to error page for regular page requests
                context.Result = new RedirectResult($"/Error?statusCode={statusCode}");
            }

            context.ExceptionHandled = true;
        }
    }

    /// <summary>
    /// Model state validation filter for API controllers
    /// </summary>
    public class ValidateModelStateFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (!context.ModelState.IsValid)
            {
                var errors = context.ModelState
                    .Where(e => e.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray()
                    );

                context.Result = new BadRequestObjectResult(new
                {
                    success = false,
                    message = "Validation failed",
                    errors
                });
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            // Not needed
        }
    }
}
