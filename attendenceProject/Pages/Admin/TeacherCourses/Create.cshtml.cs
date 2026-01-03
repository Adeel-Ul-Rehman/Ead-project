using attendence.Data.Data;
using attendence.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace attendenceProject.Pages.Admin.TeacherCourses;

[Authorize(Roles = "Admin")]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public CreateModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        public int SectionId { get; set; }

        [Required]
        public int CourseId { get; set; }

        [Required]
        public int TeacherId { get; set; }
    }

    public List<Section> AllSections { get; set; } = new();
    public List<Course> AllCourses { get; set; } = new();
    public List<attendence.Domain.Entities.Teacher> AllTeachers { get; set; } = new();

    public async Task OnGetAsync()
    {
        AllSections = await _context.Sections
            .Include(s => s.Badge)
            .OrderBy(s => s.Badge.Name)
            .ThenBy(s => s.Semester)
            .ThenBy(s => s.Name)
            .ToListAsync();

        AllCourses = await _context.Courses
            .OrderBy(c => c.Code)
            .ToListAsync();

        AllTeachers = await _context.Teachers
            .Include(t => t.User)
            .OrderBy(t => t.User.FullName)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await OnGetAsync();
            return Page();
        }

        // Verify section exists
        var section = await _context.Sections
            .Include(s => s.Badge)
            .FirstOrDefaultAsync(s => s.Id == Input.SectionId);
        if (section == null)
        {
            ModelState.AddModelError("Input.SectionId", "Invalid section selected.");
            await OnGetAsync();
            return Page();
        }

        // Verify course exists
        var course = await _context.Courses.FindAsync(Input.CourseId);
        if (course == null)
        {
            ModelState.AddModelError("Input.CourseId", "Invalid course selected.");
            await OnGetAsync();
            return Page();
        }

        // Verify teacher exists
        var teacher = await _context.Teachers
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == Input.TeacherId);
        if (teacher == null)
        {
            ModelState.AddModelError("Input.TeacherId", "Invalid teacher selected.");
            await OnGetAsync();
            return Page();
        }

        // Check for duplicate assignment
        var existingAssignment = await _context.TeacherCourses
            .FirstOrDefaultAsync(tc => 
                tc.TeacherId == Input.TeacherId && 
                tc.CourseId == Input.CourseId && 
                tc.SectionId == Input.SectionId);

        if (existingAssignment != null)
        {
            ModelState.AddModelError("", "This course is already assigned to this teacher for this section.");
            await OnGetAsync();
            return Page();
        }

        // Create assignment
        var teacherCourse = new TeacherCourse
        {
            TeacherId = Input.TeacherId,
            CourseId = Input.CourseId,
            SectionId = Input.SectionId
        };

        _context.TeacherCourses.Add(teacherCourse);
        await _context.SaveChangesAsync();

        var teacherName = teacher.User?.FullName ?? "Unknown";
        
        TempData["SuccessMessage"] = $"Course '{course.Code}' assigned to {teacherName} for {section.Badge.Name} - Semester {section.Semester}!";
        return RedirectToPage("./Index");
    }
}
