using attendence.Data.Data;
using attendence.Services.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace attendenceProject.Pages.Student;

[Authorize(Roles = "Student")]
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
    public string RollNo { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FatherName { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string BadgeName { get; set; } = string.Empty;
    public int Semester { get; set; }
    public decimal AttendancePercentage { get; set; }
    public int TotalCourses { get; set; }

    public async Task OnGetAsync()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdClaim, out int userId))
        {
            var student = await _context.Students
                .Include(s => s.User)
                .Include(s => s.Section)
                    .ThenInclude(sec => sec.Badge)
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (student != null)
            {
                FullName = student.User.FullName;
                RollNo = student.RollNo;
                Email = student.User.Email;
                FatherName = student.FatherName;
                SectionName = student.Section.Name;
                BadgeName = student.Section.Badge.Name;
                Semester = student.Section.Semester;

                // Calculate attendance percentage
                var attendanceRecords = await _context.AttendanceRecords
                    .Where(ar => ar.StudentId == student.Id)
                    .ToListAsync();

                var totalLectures = attendanceRecords.Count;
                var presentCount = attendanceRecords.Count(ar => ar.Status == "Present" || ar.Status == "Late");

                if (totalLectures > 0)
                {
                    AttendancePercentage = Math.Round((decimal)presentCount / totalLectures * 100, 2);
                }

                // Total courses
                TotalCourses = await _context.TeacherCourses
                    .Where(tc => tc.SectionId == student.SectionId)
                    .Select(tc => tc.CourseId)
                    .Distinct()
                    .CountAsync();
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
