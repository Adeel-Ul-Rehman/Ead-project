using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace attendenceProject.Pages;

public class IndexModel : PageModel
{
    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var role = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value;
            
            return role switch
            {
                "Admin" => RedirectToPage("/Admin/Dashboard"),
                "Teacher" => RedirectToPage("/Teacher/Dashboard"),
                "Student" => RedirectToPage("/Student/Dashboard"),
                _ => RedirectToPage("/Account/Login")
            };
        }
        
        return RedirectToPage("/Account/Login");
    }
}
