using attendence.Data.Data;
using attendence.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace attendenceProject.Pages.Admin.Courses;

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
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public bool IsLab { get; set; }
        public int CreditHours { get; set; }
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Check for duplicate course code
        var existingCourse = await _context.Courses
            .FirstOrDefaultAsync(c => c.Code.ToLower() == Input.Code.ToLower());

        if (existingCourse != null)
        {
            ModelState.AddModelError("Input.Code", "A course with this code already exists.");
            return Page();
        }

        var course = new Course
        {
            Code = Input.Code,
            Title = Input.Title,
            IsLab = Input.IsLab,
            CreditHours = Input.CreditHours
        };

        _context.Courses.Add(course);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Course '{Input.Code} - {Input.Title}' created successfully!";
        return RedirectToPage("./Index");
    }
}
