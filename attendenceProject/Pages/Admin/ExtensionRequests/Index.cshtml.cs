using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace attendenceProject.Pages.Admin.ExtensionRequests
{
    /// <summary>
    /// This page has been deprecated. Extension Requests are now managed
    /// in the unified Attendance Requests area along with Edit Requests.
    /// Redirecting to the unified interface.
    /// </summary>
    public class IndexModel : PageModel
    {
        public IActionResult OnGet()
        {
            // Redirect to the unified Attendance Requests area
            // which handles both Extension and Edit requests
            return RedirectToPage("/Admin/AttendanceEditRequests/Index");
        }

        public IActionResult OnPost()
        {
            // Redirect any POST requests as well
            return RedirectToPage("/Admin/AttendanceEditRequests/Index");
        }
    }
}
