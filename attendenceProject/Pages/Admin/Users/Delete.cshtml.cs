using attendence.Data.Data;
using attendence.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace attendenceProject.Pages.Admin.Users;

[Authorize(Roles = "Admin")]
public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public DeleteModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public new User User { get; set; } = new();

    public bool CannotDelete { get; set; }
    public int TeacherCourseCount { get; set; }
    public int AttendanceRecordCount { get; set; }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var user = await _context.Users
            .Include(u => u.Student)
            .Include(u => u.Teacher)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return NotFound();
        }

        User = user;

        // Check for related data
        if (user.Role == "Teacher" && user.Teacher != null)
        {
            TeacherCourseCount = await _context.TeacherCourses.CountAsync(tc => tc.TeacherId == user.Teacher.Id);
            CannotDelete = TeacherCourseCount > 0;
        }
        else if (user.Role == "Student" && user.Student != null)
        {
            AttendanceRecordCount = await _context.AttendanceRecords.CountAsync(ar => ar.StudentId == user.Student.Id);
            CannotDelete = AttendanceRecordCount > 0;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _context.Users
            .Include(u => u.Student)
            .Include(u => u.Teacher)
            .FirstOrDefaultAsync(u => u.Id == User.Id);

        if (user == null)
        {
            return NotFound();
        }

        // Check for related data before deletion
        if (user.Role == "Teacher" && user.Teacher != null)
        {
            var teacherCourseCount = await _context.TeacherCourses.CountAsync(tc => tc.TeacherId == user.Teacher.Id);
            if (teacherCourseCount > 0)
            {
                TempData["ErrorMessage"] = $"Cannot delete teacher '{user.FullName}' because they have {teacherCourseCount} course assignment(s). Please remove all assignments first.";
                return RedirectToPage("./Index");
            }

            // Delete teacher record
            _context.Teachers.Remove(user.Teacher);
        }
        else if (user.Role == "Student" && user.Student != null)
        {
            var attendanceCount = await _context.AttendanceRecords.CountAsync(ar => ar.StudentId == user.Student.Id);
            if (attendanceCount > 0)
            {
                TempData["ErrorMessage"] = $"Cannot delete student '{user.FullName}' because they have {attendanceCount} attendance record(s). Users with attendance history cannot be deleted.";
                return RedirectToPage("./Index");
            }

            // Delete student record
            _context.Students.Remove(user.Student);
        }

        // Delete user
        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"User '{user.FullName}' deleted successfully!";
        return RedirectToPage("./Index");
    }
}
