using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace attendenceProject.Pages.Account;

public class VerifyOtpModel : PageModel
{
    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Otp { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return RedirectToPage("/Account/ForgotPassword");
        }

        Email = email;
        return Page();
    }

    public IActionResult OnPost()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Otp))
        {
            ErrorMessage = "Please enter the OTP code.";
            return Page();
        }

        // Retrieve OTP from session
        var storedOtp = HttpContext.Session.GetString($"OTP_{Email}");
        var otpExpiryStr = HttpContext.Session.GetString($"OTPExpiry_{Email}");

        if (string.IsNullOrEmpty(storedOtp) || string.IsNullOrEmpty(otpExpiryStr))
        {
            ErrorMessage = "OTP has expired or not found. Please request a new one.";
            return Page();
        }

        var otpExpiry = DateTime.Parse(otpExpiryStr);
        if (DateTime.UtcNow > otpExpiry)
        {
            HttpContext.Session.Remove($"OTP_{Email}");
            HttpContext.Session.Remove($"OTPExpiry_{Email}");
            ErrorMessage = "OTP has expired. Please request a new one.";
            return Page();
        }

        if (storedOtp != Otp)
        {
            ErrorMessage = "Invalid OTP code. Please try again.";
            return Page();
        }

        // OTP verified successfully, store verification in session
        HttpContext.Session.SetString($"OTPVerified_{Email}", "true");
        HttpContext.Session.Remove($"OTP_{Email}");
        HttpContext.Session.Remove($"OTPExpiry_{Email}");

        return RedirectToPage("/Account/ResetPassword", new { email = Email });
    }
}
