using attendence.Data.Data;
using attendence.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace attendenceProject.Pages.Admin.TimetableRules;

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
        public int TeacherCourseId { get; set; }

        // Days are collected from checkboxes, not bound directly
        public string DaysOfWeek { get; set; } = string.Empty;

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public int DurationMinutes { get; set; }

        public string? Room { get; set; }

        public string? LectureType { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }
    }

    public List<TeacherCourse> AllTeacherCourses { get; set; } = new();

    public async Task OnGetAsync()
    {
        AllTeacherCourses = await _context.TeacherCourses
            .Include(tc => tc.Teacher)
            .ThenInclude(t => t.User)
            .Include(tc => tc.Course)
            .Include(tc => tc.Section)
            .ThenInclude(s => s.Badge)
            .OrderBy(tc => tc.Teacher.User.FullName)
            .ThenBy(tc => tc.Course.Code)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Collect selected days from form
        var selectedDays = Request.Form["daysOfWeek"].ToList();
        if (!selectedDays.Any())
        {
            ModelState.AddModelError("Input.DaysOfWeek", "Please select at least one day of the week.");
            await OnGetAsync();
            return Page();
        }

        if (Input.EndDate < Input.StartDate)
        {
            ModelState.AddModelError("Input.EndDate", "End date must be after start date.");
        }

        if (!ModelState.IsValid)
        {
            await OnGetAsync();
            return Page();
        }

        // Verify teacher course exists
        var teacherCourse = await _context.TeacherCourses.FindAsync(Input.TeacherCourseId);
        if (teacherCourse == null)
        {
            ModelState.AddModelError("Input.TeacherCourseId", "Invalid teacher course selected.");
            await OnGetAsync();
            return Page();
        }

        // Create timetable rule
        var rule = new TimetableRule
        {
            TeacherCourseId = Input.TeacherCourseId,
            DaysOfWeek = string.Join(",", selectedDays),
            StartTime = Input.StartTime,
            DurationMinutes = Input.DurationMinutes,
            Room = Input.Room,
            LectureType = Input.LectureType,
            StartDate = Input.StartDate,
            EndDate = Input.EndDate
        };

        _context.TimetableRules.Add(rule);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Timetable rule created successfully!";
        return RedirectToPage("./Index");
    }
}
