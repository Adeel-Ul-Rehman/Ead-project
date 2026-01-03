using attendence.Data.Data;
using attendence.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace attendenceProject.Pages.Admin.TeacherCourses;

[Authorize(Roles = "Admin")]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public EditModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public int Id { get; set; }

        [Required]
        public int SectionId { get; set; }

        [Required]
        public int CourseId { get; set; }

        [Required]
        public int TeacherId { get; set; }
    }

    public TeacherCourse? CurrentAssignment { get; set; }
    public List<Section> AllSections { get; set; } = new();
    public List<Course> AllCourses { get; set; } = new();
    public List<attendence.Domain.Entities.Teacher> AllTeachers { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        CurrentAssignment = await _context.TeacherCourses
            .Include(tc => tc.Teacher)
            .ThenInclude(t => t.User)
            .Include(tc => tc.Course)
            .Include(tc => tc.Section)
            .ThenInclude(s => s.Badge)
            .FirstOrDefaultAsync(tc => tc.Id == id);

        if (CurrentAssignment == null)
        {
            TempData["ErrorMessage"] = "Course assignment not found.";
            return RedirectToPage("./Index");
        }

        // Populate Input model with current values
        Input = new InputModel
        {
            Id = CurrentAssignment.Id,
            SectionId = CurrentAssignment.SectionId,
            CourseId = CurrentAssignment.CourseId,
            TeacherId = CurrentAssignment.TeacherId
        };

        // Load dropdowns
        await LoadDropdowns();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            CurrentAssignment = await _context.TeacherCourses
                .Include(tc => tc.Teacher)
                .ThenInclude(t => t.User)
                .Include(tc => tc.Course)
                .Include(tc => tc.Section)
                .ThenInclude(s => s.Badge)
                .FirstOrDefaultAsync(tc => tc.Id == Input.Id);

            await LoadDropdowns();
            return Page();
        }

        var teacherCourse = await _context.TeacherCourses.FindAsync(Input.Id);
        if (teacherCourse == null)
        {
            TempData["ErrorMessage"] = "Course assignment not found.";
            return RedirectToPage("./Index");
        }

        // Verify section exists
        var section = await _context.Sections
            .Include(s => s.Badge)
            .FirstOrDefaultAsync(s => s.Id == Input.SectionId);
        if (section == null)
        {
            ModelState.AddModelError("Input.SectionId", "Invalid section selected.");
            CurrentAssignment = await _context.TeacherCourses
                .Include(tc => tc.Teacher)
                .ThenInclude(t => t.User)
                .Include(tc => tc.Course)
                .Include(tc => tc.Section)
                .ThenInclude(s => s.Badge)
                .FirstOrDefaultAsync(tc => tc.Id == Input.Id);
            await LoadDropdowns();
            return Page();
        }

        // Verify course exists
        var course = await _context.Courses.FindAsync(Input.CourseId);
        if (course == null)
        {
            ModelState.AddModelError("Input.CourseId", "Invalid course selected.");
            CurrentAssignment = await _context.TeacherCourses
                .Include(tc => tc.Teacher)
                .ThenInclude(t => t.User)
                .Include(tc => tc.Course)
                .Include(tc => tc.Section)
                .ThenInclude(s => s.Badge)
                .FirstOrDefaultAsync(tc => tc.Id == Input.Id);
            await LoadDropdowns();
            return Page();
        }

        // Verify teacher exists
        var teacher = await _context.Teachers
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == Input.TeacherId);
        if (teacher == null)
        {
            ModelState.AddModelError("Input.TeacherId", "Invalid teacher selected.");
            CurrentAssignment = await _context.TeacherCourses
                .Include(tc => tc.Teacher)
                .ThenInclude(t => t.User)
                .Include(tc => tc.Course)
                .Include(tc => tc.Section)
                .ThenInclude(s => s.Badge)
                .FirstOrDefaultAsync(tc => tc.Id == Input.Id);
            await LoadDropdowns();
            return Page();
        }

        // Check for duplicate assignment (excluding current)
        var existingAssignment = await _context.TeacherCourses
            .FirstOrDefaultAsync(tc => 
                tc.Id != Input.Id &&
                tc.TeacherId == Input.TeacherId && 
                tc.CourseId == Input.CourseId && 
                tc.SectionId == Input.SectionId);

        if (existingAssignment != null)
        {
            ModelState.AddModelError("", "This course is already assigned to this teacher for this section.");
            CurrentAssignment = await _context.TeacherCourses
                .Include(tc => tc.Teacher)
                .ThenInclude(t => t.User)
                .Include(tc => tc.Course)
                .Include(tc => tc.Section)
                .ThenInclude(s => s.Badge)
                .FirstOrDefaultAsync(tc => tc.Id == Input.Id);
            await LoadDropdowns();
            return Page();
        }

        // Update assignment
        teacherCourse.TeacherId = Input.TeacherId;
        teacherCourse.CourseId = Input.CourseId;
        teacherCourse.SectionId = Input.SectionId;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Course assignment updated successfully! {teacher.User.FullName} assigned to {course.Code} for {section.Badge.Name} - Semester {section.Semester}.";
        return RedirectToPage("./Index");
    }

    private async Task LoadDropdowns()
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
}
