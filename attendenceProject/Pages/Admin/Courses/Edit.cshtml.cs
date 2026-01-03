using attendence.Data.Data;
using attendence.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace attendenceProject.Pages.Admin.Courses;

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
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public bool IsLab { get; set; }
        public int CreditHours { get; set; }
    }

    public int AssignmentCount { get; set; }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var course = await _context.Courses
            .Include(c => c.TeacherCourses)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (course == null)
        {
            return NotFound();
        }

        Input = new InputModel
        {
            Id = course.Id,
            Code = course.Code,
            Title = course.Title,
            IsLab = course.IsLab,
            CreditHours = course.CreditHours
        };
        AssignmentCount = course.TeacherCourses.Count;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Check for duplicate course code (excluding current course)
        var existingCourse = await _context.Courses
            .FirstOrDefaultAsync(c => c.Code.ToLower() == Input.Code.ToLower() && c.Id != Input.Id);

        if (existingCourse != null)
        {
            ModelState.AddModelError("Input.Code", "A course with this code already exists.");
            AssignmentCount = await _context.TeacherCourses.CountAsync(tc => tc.CourseId == Input.Id);
            return Page();
        }

        var course = await _context.Courses.FindAsync(Input.Id);
        if (course == null)
        {
            return NotFound();
        }

        course.Code = Input.Code;
        course.Title = Input.Title;
        course.IsLab = Input.IsLab;
        course.CreditHours = Input.CreditHours;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await CourseExists(Input.Id))
            {
                return NotFound();
            }
            throw;
        }

        TempData["SuccessMessage"] = $"Course '{Input.Code} - {Input.Title}' updated successfully!";
        return RedirectToPage("./Index");
    }

    private async Task<bool> CourseExists(int id)
    {
        return await _context.Courses.AnyAsync(c => c.Id == id);
    }
}
