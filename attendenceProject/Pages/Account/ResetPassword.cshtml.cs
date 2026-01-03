using attendence.Data.Data;
using attendence.Services.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace attendenceProject.Pages.Account;

public class ResetPasswordModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly PasswordHasher _passwordHasher;

    public ResetPasswordModel(ApplicationDbContext context, PasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string NewPassword { get; set; } = string.Empty;

    [BindProperty]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return RedirectToPage("/Account/ForgotPassword");
        }

        // Check if OTP was verified
        var otpVerified = HttpContext.Session.GetString($"OTPVerified_{email}");
        if (otpVerified != "true")
        {
            return RedirectToPage("/Account/ForgotPassword");
        }

        Email = email;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Verify OTP was verified
        var otpVerified = HttpContext.Session.GetString($"OTPVerified_{Email}");
        if (otpVerified != "true")
        {
            ErrorMessage = "Session expired. Please start the password reset process again.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(NewPassword) || string.IsNullOrWhiteSpace(ConfirmPassword))
        {
            ErrorMessage = "All fields are required.";
            return Page();
        }

        if (NewPassword != ConfirmPassword)
        {
            ErrorMessage = "Passwords do not match.";
            return Page();
        }

        if (NewPassword.Length < 6)
        {
            ErrorMessage = "Password must be at least 6 characters long.";
            return Page();
        }

        // Find user
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == Email);
        if (user == null)
        {
            ErrorMessage = "User not found.";
            return Page();
        }

        // Update password
        user.PasswordHash = _passwordHasher.HashPassword(NewPassword);
        await _context.SaveChangesAsync();

        // Clear session
        HttpContext.Session.Remove($"OTPVerified_{Email}");

        // Redirect to login with success message
        TempData["SuccessMessage"] = "Password reset successfully! Please login with your new password.";
        return RedirectToPage("/Account/Login");
    }
}
