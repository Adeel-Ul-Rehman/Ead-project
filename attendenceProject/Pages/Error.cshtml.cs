using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace attendenceProject.Pages
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [IgnoreAntiforgeryToken]
    public class ErrorModel : PageModel
    {
        public string? RequestId { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
        public new int StatusCode { get; set; }
        public string? ErrorMessage { get; set; }

        private readonly ILogger<ErrorModel> _logger;

        public ErrorModel(ILogger<ErrorModel> logger)
        {
            _logger = logger;
        }

        public void OnGet(int? statusCode = null, string? message = null)
        {
            try
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
                StatusCode = statusCode ?? HttpContext.Response.StatusCode;
                ErrorMessage = message;

                _logger.LogWarning("Error page accessed. StatusCode: {StatusCode}, Message: {Message}, RequestId: {RequestId}", 
                    StatusCode, message, RequestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Error page OnGet");
                StatusCode = 500;
            }
        }
    }
}
