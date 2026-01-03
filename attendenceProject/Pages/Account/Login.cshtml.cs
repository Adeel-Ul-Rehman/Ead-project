using System.Security.Claims;
using attendence.Services.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace attendenceProject.Pages.Account;

public class LoginModel : PageModel
{
    private readonly IAuthService _authService;

    public LoginModel(IAuthService authService)
    {
        _authService = authService;
    }

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    public bool RememberMe { get; set; }

    public string? ErrorMessage { get; set; }
    public string? ReturnUrl { get; set; }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;

        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Email and password are required.";
            return Page();
        }

        var user = await _authService.AuthenticateAsync(Email, Password);

        if (user == null)
        {
            ErrorMessage = "Invalid email or password.";
            return Page();
        }

        // Create claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
        };

        // Add additional claims based on role
        if (user.Role == "Student" && user.Student != null)
        {
            claims.Add(new Claim("StudentId", user.Student.Id.ToString()));
            claims.Add(new Claim("RollNo", user.Student.RollNo));
        }
        else if (user.Role == "Teacher" && user.Teacher != null)
        {
            claims.Add(new Claim("TeacherId", user.Teacher.Id.ToString()));
        }

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = false, // Always session cookie - expires when browser closes
            AllowRefresh = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) // Maximum session time
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        // Redirect based on role
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return user.Role switch
        {
            "Admin" => RedirectToPage("/Admin/Dashboard"),
            "Teacher" => RedirectToPage("/Teacher/Dashboard"),
            "Student" => RedirectToPage("/Student/Dashboard"),
            _ => RedirectToPage("/Index")
        };
    }
}