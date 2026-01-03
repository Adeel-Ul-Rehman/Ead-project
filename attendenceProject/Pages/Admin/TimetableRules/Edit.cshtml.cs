using attendence.Data.Data;
using attendence.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace attendenceProject.Pages.Admin.TimetableRules;

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

    public TimetableRule CurrentRule { get; set; } = null!;

    public class InputModel
    {
        public int Id { get; set; }

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

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var rule = await _context.TimetableRules
            .Include(tr => tr.TeacherCourse)
                .ThenInclude(tc => tc.Teacher)
                    .ThenInclude(t => t.User)
            .Include(tr => tr.TeacherCourse)
                .ThenInclude(tc => tc.Course)
            .Include(tr => tr.TeacherCourse)
                .ThenInclude(tc => tc.Section)
                    .ThenInclude(s => s.Badge)
            .FirstOrDefaultAsync(tr => tr.Id == id);

        if (rule == null)
        {
            return NotFound();
        }

        CurrentRule = rule;

        Input = new InputModel
        {
            Id = rule.Id,
            DaysOfWeek = rule.DaysOfWeek,
            StartTime = rule.StartTime,
            DurationMinutes = rule.DurationMinutes,
            Room = rule.Room,
            LectureType = rule.LectureType,
            StartDate = rule.StartDate,
            EndDate = rule.EndDate
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var selectedDays = Request.Form["daysOfWeek"].ToList();
        if (!selectedDays.Any())
        {
            ModelState.AddModelError("Input.DaysOfWeek", "Please select at least one day of the week.");
            
            // Reload CurrentRule for display
            var currentRule = await _context.TimetableRules
                .Include(tr => tr.TeacherCourse)
                    .ThenInclude(tc => tc.Teacher)
                        .ThenInclude(t => t.User)
                .Include(tr => tr.TeacherCourse)
                    .ThenInclude(tc => tc.Course)
                .Include(tr => tr.TeacherCourse)
                    .ThenInclude(tc => tc.Section)
                        .ThenInclude(s => s.Badge)
                .FirstOrDefaultAsync(tr => tr.Id == Input.Id);
            
            if (currentRule != null)
            {
                CurrentRule = currentRule;
            }
            
            return Page();
        }

        if (Input.EndDate < Input.StartDate)
        {
            ModelState.AddModelError("Input.EndDate", "End date must be after start date.");
        }

        if (!ModelState.IsValid)
        {
            // Reload CurrentRule for display
            var currentRule = await _context.TimetableRules
                .Include(tr => tr.TeacherCourse)
                    .ThenInclude(tc => tc.Teacher)
                        .ThenInclude(t => t.User)
                .Include(tr => tr.TeacherCourse)
                    .ThenInclude(tc => tc.Course)
                .Include(tr => tr.TeacherCourse)
                    .ThenInclude(tc => tc.Section)
                        .ThenInclude(s => s.Badge)
                .FirstOrDefaultAsync(tr => tr.Id == Input.Id);
            
            if (currentRule != null)
            {
                CurrentRule = currentRule;
            }
            
            return Page();
        }

        var rule = await _context.TimetableRules.FindAsync(Input.Id);
        if (rule == null)
        {
            return NotFound();
        }

        rule.DaysOfWeek = string.Join(",", selectedDays);
        rule.StartTime = Input.StartTime;
        rule.DurationMinutes = Input.DurationMinutes;
        rule.Room = Input.Room;
        rule.LectureType = Input.LectureType;
        rule.StartDate = Input.StartDate;
        rule.EndDate = Input.EndDate;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await RuleExists(Input.Id))
            {
                return NotFound();
            }
            throw;
        }

        TempData["SuccessMessage"] = "Timetable rule updated successfully!";
        return RedirectToPage("./Index");
    }

    private async Task<bool> RuleExists(int id)
    {
        return await _context.TimetableRules.AnyAsync(tr => tr.Id == id);
    }
}
