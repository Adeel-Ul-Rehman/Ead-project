using attendence.Data.Data;
using attendence.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace attendenceProject.Pages.Admin.Users;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public IndexModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public List<User> Users { get; set; } = new();
    public string? SearchTerm { get; set; }
    public string? Role { get; set; }

    public int TotalUsers { get; set; }
    public int AdminCount { get; set; }
    public int TeacherCount { get; set; }
    public int StudentCount { get; set; }

    public async Task OnGetAsync(string? searchTerm, string? role)
    {
        SearchTerm = searchTerm;
        Role = role;

        // Get counts for stats
        TotalUsers = await _context.Users.CountAsync();
        AdminCount = await _context.Users.CountAsync(u => u.Role == "Admin");
        TeacherCount = await _context.Users.CountAsync(u => u.Role == "Teacher");
        StudentCount = await _context.Users.CountAsync(u => u.Role == "Student");

        // Build query
        var query = _context.Users
            .Include(u => u.Student)
            .Include(u => u.Teacher)
            .AsQueryable();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(u => u.FullName.Contains(searchTerm) || u.Email.Contains(searchTerm));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(u => u.Role == role);
        }

        Users = await query
            .OrderBy(u => u.Role)
            .ThenBy(u => u.FullName)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            var user = await _context.Users
                .Include(u => u.Student)
                .Include(u => u.Teacher)
                .FirstOrDefaultAsync(u => u.Id == id);
                
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToPage();
            }

            string userName = user.FullName;

            // Delete related records based on role
            if (user.Student != null)
            {
                // Delete student's attendance records
                var attendanceRecords = await _context.AttendanceRecords
                    .Where(ar => ar.StudentId == user.Student.Id)
                    .ToListAsync();
                _context.AttendanceRecords.RemoveRange(attendanceRecords);

                // Delete the student record
                _context.Students.Remove(user.Student);
            }

            if (user.Teacher != null)
            {
                // Delete teacher's course assignments
                var teacherCourses = await _context.TeacherCourses
                    .Where(tc => tc.TeacherId == user.Teacher.Id)
                    .ToListAsync();
                _context.TeacherCourses.RemoveRange(teacherCourses);

                // Delete teacher's attendance edit requests
                var editRequests = await _context.AttendanceEditRequests
                    .Where(aer => aer.TeacherId == user.Teacher.Id)
                    .ToListAsync();
                _context.AttendanceEditRequests.RemoveRange(editRequests);

                // Delete the teacher record
                _context.Teachers.Remove(user.Teacher);
            }

            // Delete notifications for this user
            var notifications = await _context.Notifications
                .Where(n => n.UserId == user.Id)
                .ToListAsync();
            _context.Notifications.RemoveRange(notifications);

            // Delete activity logs for this user (ActorId, not UserId)
            var activityLogs = await _context.ActivityLogs
                .Where(al => al.ActorId == user.Id)
                .ToListAsync();
            _context.ActivityLogs.RemoveRange(activityLogs);

            // Finally, delete the user
            _context.Users.Remove(user);
            
            // Save all changes
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            
            TempData["SuccessMessage"] = $"User '{userName}' has been deleted successfully.";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            TempData["ErrorMessage"] = $"Error deleting user: {ex.Message}";
        }

        return RedirectToPage();
    }
}
