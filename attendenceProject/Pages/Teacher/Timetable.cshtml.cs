using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using attendence.Data.Data;
using attendence.Domain.Entities;
using System.Security.Claims;

namespace attendenceProject.Pages.Teacher
{
    [Authorize(Roles = "Teacher")]
    public class TimetableModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public TimetableModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<DaySchedule> WeekSchedule { get; set; } = new();
        public List<TimetableRule> TodayRules { get; set; } = new();
        public string ViewMode { get; set; } = "week"; // week or day

        public async Task<IActionResult> OnGetAsync(string? mode)
        {
            ViewMode = mode ?? "week";

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var teacher = await _context.Teachers
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (teacher == null)
            {
                return Unauthorized();
            }

            var rules = await _context.TimetableRules
                .Include(tr => tr.TeacherCourse)
                    .ThenInclude(tc => tc.Course)
                .Include(tr => tr.TeacherCourse)
                    .ThenInclude(tc => tc.Section)
                        .ThenInclude(s => s.Badge)
                .Where(tr => tr.TeacherCourse.TeacherId == teacher.Id)
                .OrderBy(tr => tr.DaysOfWeek)
                .ThenBy(tr => tr.StartTime)
                .ToListAsync();

            if (ViewMode == "day")
            {
                var today = DateTime.Today.DayOfWeek.ToString();
                var todayShort = today.Substring(0, 3); // Mon, Tue, Wed, etc.
                TodayRules = rules.Where(r => 
                    !string.IsNullOrEmpty(r.DaysOfWeek) && 
                    (r.DaysOfWeek.Contains(today, StringComparison.OrdinalIgnoreCase) || 
                     r.DaysOfWeek.Contains(todayShort, StringComparison.OrdinalIgnoreCase))).ToList();
            }
            else
            {
                var days = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
                WeekSchedule = days.Select(day => new DaySchedule
                {
                    Day = day,
                    Rules = rules.Where(r => 
                        !string.IsNullOrEmpty(r.DaysOfWeek) && 
                        (r.DaysOfWeek.Contains(day, StringComparison.OrdinalIgnoreCase) || 
                         r.DaysOfWeek.Contains(day.Substring(0, 3), StringComparison.OrdinalIgnoreCase))).ToList()
                }).ToList();
            }

            return Page();
        }
    }

    public class DaySchedule
    {
        public string Day { get; set; } = string.Empty;
        public List<TimetableRule> Rules { get; set; } = new();
    }
}
