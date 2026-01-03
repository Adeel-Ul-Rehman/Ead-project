using attendence.Data.Data;
using attendence.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace attendenceProject.Pages.Admin.Lectures
{
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
            [Required(ErrorMessage = "Please select a timetable rule")]
            public int TimetableRuleId { get; set; }

            [Required(ErrorMessage = "Please select a lecture date")]
            public DateTime LectureDate { get; set; }

            [Required(ErrorMessage = "Please enter start time")]
            public TimeSpan StartTime { get; set; }

            [Required(ErrorMessage = "Please enter duration")]
            [Range(15, 300, ErrorMessage = "Duration must be between 15 and 300 minutes")]
            public int DurationMinutes { get; set; } = 90;

            public string? Room { get; set; }
            public string? LectureType { get; set; }
        }

        public List<TimetableRule> AllTimetableRules { get; set; } = new();

        public async Task OnGetAsync()
        {
            // Get all timetable rules with full navigation
            AllTimetableRules = await _context.TimetableRules
                .Include(tr => tr.TeacherCourse)
                    .ThenInclude(tc => tc.Teacher)
                        .ThenInclude(t => t.User)
                .Include(tr => tr.TeacherCourse)
                    .ThenInclude(tc => tc.Course)
                .Include(tr => tr.TeacherCourse)
                    .ThenInclude(tc => tc.Section)
                        .ThenInclude(s => s.Badge)
                .OrderBy(tr => tr.TeacherCourse.Teacher.User.FullName)
                .ThenBy(tr => tr.TeacherCourse.Course.Code)
                .ToListAsync();

            // Set default values
            Input.LectureDate = DateTime.Today.AddDays(1);
            Input.StartTime = new TimeSpan(9, 0, 0);
            Input.DurationMinutes = 90;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await OnGetAsync();
                return Page();
            }

            // Verify timetable rule exists
            var timetableRule = await _context.TimetableRules
                .Include(tr => tr.TeacherCourse)
                    .ThenInclude(tc => tc.Section)
                .FirstOrDefaultAsync(tr => tr.Id == Input.TimetableRuleId);

            if (timetableRule == null)
            {
                ModelState.AddModelError("Input.TimetableRuleId", "Invalid timetable rule selected.");
                await OnGetAsync();
                return Page();
            }

            // Check if lecture date is within timetable rule date range
            if (Input.LectureDate.Date < timetableRule.StartDate.Date || Input.LectureDate.Date > timetableRule.EndDate.Date)
            {
                ModelState.AddModelError("Input.LectureDate", 
                    $"Lecture date must be between {timetableRule.StartDate:yyyy-MM-dd} and {timetableRule.EndDate:yyyy-MM-dd}");
                await OnGetAsync();
                return Page();
            }

            // Check if this day matches the timetable rule's days
            var lectureDay = Input.LectureDate.DayOfWeek.ToString().Substring(0, 3);
            if (!timetableRule.DaysOfWeek.Contains(lectureDay))
            {
                ModelState.AddModelError("Input.LectureDate", 
                    $"The selected date ({Input.LectureDate:ddd}) doesn't match the timetable rule days ({timetableRule.DaysOfWeek})");
                await OnGetAsync();
                return Page();
            }

            // Check if it's a holiday
            var isHoliday = await _context.Holidays.AnyAsync(h => h.Date.Date == Input.LectureDate.Date);
            if (isHoliday)
            {
                ModelState.AddModelError("Input.LectureDate", "Cannot create lecture on a holiday.");
                await OnGetAsync();
                return Page();
            }

            // Create lecture datetime
            var startDateTime = Input.LectureDate.Date.Add(Input.StartTime);
            var endDateTime = startDateTime.AddMinutes(Input.DurationMinutes);

            // Check for duplicate lecture
            var duplicateLecture = await _context.Lectures
                .AnyAsync(l => l.TimetableRuleId == Input.TimetableRuleId && 
                              l.StartDateTime == startDateTime);

            if (duplicateLecture)
            {
                ModelState.AddModelError("", "A lecture already exists for this timetable rule at this date and time.");
                await OnGetAsync();
                return Page();
            }

            // Create the lecture
            var lecture = new Lecture
            {
                TimetableRuleId = Input.TimetableRuleId,
                StartDateTime = startDateTime,
                EndDateTime = endDateTime,
                Status = "Scheduled"
            };

            _context.Lectures.Add(lecture);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Lecture created successfully for {startDateTime:dddd, MMMM dd, yyyy} at {startDateTime:hh:mm tt}";
            return RedirectToPage("/Admin/Lectures/Index");
        }
    }
}
