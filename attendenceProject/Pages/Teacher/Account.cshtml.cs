using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using attendence.Domain.Entities;
using attendence.Data.Data;
using attendence.Services.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace attendenceProject.Pages.Teacher;

public class AccountModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly PasswordHasher _passwordHasher;

    public AccountModel(ApplicationDbContext context, PasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public attendence.Domain.Entities.Teacher Teacher { get; set; } = null!;

    [BindProperty]

    [Required(ErrorMessage = "Current password is required")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "New password is required")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Please confirm your password")]
    [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        
        Teacher = await _context.Teachers
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.UserId == userId);

        if (Teacher == null)
        {
            return RedirectToPage("/Account/Login");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostChangePasswordAsync()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        
        Teacher = await _context.Teachers
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.UserId == userId);

        if (Teacher == null)
        {
            return RedirectToPage("/Account/Login");
        }

        // Validate password fields
        if (string.IsNullOrWhiteSpace(CurrentPassword))
        {
            ErrorMessage = "Current password is required";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            ErrorMessage = "New password is required";
            return Page();
        }

        if (NewPassword.Length < 6)
        {
            ErrorMessage = "New password must be at least 6 characters long";
            return Page();
        }

        if (NewPassword != ConfirmPassword)
        {
            ErrorMessage = "New password and confirmation do not match";
            return Page();
        }

        try
        {
            // Verify current password
            if (!_passwordHasher.VerifyPassword(CurrentPassword, Teacher.User.PasswordHash))
            {
                ErrorMessage = "Current password is incorrect";
                return Page();
            }

            // Check if new password is same as current
            if (_passwordHasher.VerifyPassword(NewPassword, Teacher.User.PasswordHash))
            {
                ErrorMessage = "New password must be different from current password";
                return Page();
            }

            // Hash and update the new password
            Teacher.User.PasswordHash = _passwordHasher.HashPassword(NewPassword);
            await _context.SaveChangesAsync();

            // Log the activity
            var activityLog = new ActivityLog
            {
                ActorId = userId,
                Action = "Password Changed",
                Details = "Teacher changed account password",
                Timestamp = DateTime.Now
            };
            _context.ActivityLogs.Add(activityLog);
            await _context.SaveChangesAsync();

            // Clear password fields
            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;

            SuccessMessage = "Password changed successfully!";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error changing password: {ex.Message}";
            return Page();
        }
    }
}
