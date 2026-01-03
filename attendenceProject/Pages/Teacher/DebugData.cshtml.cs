using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using attendence.Domain.Entities;
using attendence.Data.Data;
using System.Security.Claims;

namespace attendenceProject.Pages.Teacher;

public class DebugDataModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public DebugDataModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public List<TeacherCourseDebugInfo> TeacherCourses { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.UserId == userId);

        if (teacher == null)
        {
            return RedirectToPage("/Account/Login");
        }

        var teacherCourses = await _context.TeacherCourses
            .Include(tc => tc.Course)
            .Include(tc => tc.Section)
            .Where(tc => tc.TeacherId == teacher.Id)
            .ToListAsync();

        foreach (var tc in teacherCourses)
        {
            var lectures = await _context.Lectures
                .Include(l => l.AttendanceRecords)
                .Where(l => l.TimetableRule.TeacherCourseId == tc.Id)
                .ToListAsync();

            var totalStudents = await _context.Students.CountAsync(s => s.SectionId == tc.SectionId);

            TeacherCourses.Add(new TeacherCourseDebugInfo
            {
                Course = tc.Course,
                Section = tc.Section,
                Lectures = lectures,
                TotalStudents = totalStudents
            });
        }

        return Page();
    }
}

public class TeacherCourseDebugInfo
{
    public Course Course { get; set; } = null!;
    public Section Section { get; set; } = null!;
    public List<Lecture> Lectures { get; set; } = new();
    public int TotalStudents { get; set; }
}
