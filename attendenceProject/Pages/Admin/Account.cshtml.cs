using attendence.Data.Data;
using attendence.Services.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace attendenceProject.Pages.Admin;

[Authorize(Roles = "Admin")]
public class AccountModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly PasswordHasher _passwordHasher;

    public AccountModel(ApplicationDbContext context, PasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public async Task OnGetAsync()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdClaim, out int userId))
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                FullName = user.FullName;
                Email = user.Email;
            }
        }
    }

    public async Task<IActionResult> OnPostChangePasswordAsync(string currentPassword, string newPassword, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
        {
            TempData["ErrorMessage"] = "All password fields are required.";
            return RedirectToPage();
        }

        if (newPassword != confirmPassword)
        {
            TempData["ErrorMessage"] = "New password and confirmation do not match.";
            return RedirectToPage();
        }

        if (newPassword.Length < 6)
        {
            TempData["ErrorMessage"] = "Password must be at least 6 characters long.";
            return RedirectToPage();
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out int userId))
        {
            TempData["ErrorMessage"] = "Unable to identify user.";
            return RedirectToPage();
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            TempData["ErrorMessage"] = "User not found.";
            return RedirectToPage();
        }

        // Verify current password
        if (!_passwordHasher.VerifyPassword(currentPassword, user.PasswordHash))
        {
            TempData["ErrorMessage"] = "Current password is incorrect.";
            return RedirectToPage();
        }

        // Update password
        user.PasswordHash = _passwordHasher.HashPassword(newPassword);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Password changed successfully!";
        return RedirectToPage();
    }
}
