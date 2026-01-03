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

            [Required(ErrorMessage = "Please select a lecture date")]
            public DateTime LectureDate { get; set; }

            [Required(ErrorMessage = "Please enter start time")]
            public TimeSpan StartTime { get; set; }

            [Required(ErrorMessage = "Please enter duration")]
            [Range(15, 300, ErrorMessage = "Duration must be between 15 and 300 minutes")]
            public int DurationMinutes { get; set; }

            public string? Room { get; set; }
            public string? LectureType { get; set; }
        }

        public Lecture Lecture { get; set; } = null!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var lecture = await _context.Lectures
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Teacher)
                            .ThenInclude(t => t.User)
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Course)
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Section)
                            .ThenInclude(s => s.Badge)
                .Include(l => l.AttendanceRecords)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (lecture == null)
            {
                return NotFound();
            }

            Lecture = lecture;

            // Populate input
            Input.Id = lecture.Id;
            Input.LectureDate = lecture.StartDateTime.Date;
            Input.StartTime = lecture.StartDateTime.TimeOfDay;
            Input.DurationMinutes = (int)(lecture.EndDateTime - lecture.StartDateTime).TotalMinutes;
            Input.Room = lecture.TimetableRule.Room;
            Input.LectureType = lecture.TimetableRule.LectureType;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var lecture = await _context.Lectures
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Teacher)
                            .ThenInclude(t => t.User)
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Course)
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Section)
                            .ThenInclude(s => s.Badge)
                .Include(l => l.AttendanceRecords)
                .FirstOrDefaultAsync(l => l.Id == Input.Id);

            if (lecture == null)
            {
                return NotFound();
            }

            Lecture = lecture;

            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Check if lecture date is within timetable rule date range
            if (Input.LectureDate.Date < lecture.TimetableRule.StartDate.Date || 
                Input.LectureDate.Date > lecture.TimetableRule.EndDate.Date)
            {
                ModelState.AddModelError("Input.LectureDate", 
                    $"Lecture date must be between {lecture.TimetableRule.StartDate:yyyy-MM-dd} and {lecture.TimetableRule.EndDate:yyyy-MM-dd}");
                return Page();
            }

            // Check if it's a holiday
            var isHoliday = await _context.Holidays.AnyAsync(h => h.Date.Date == Input.LectureDate.Date);
            if (isHoliday)
            {
                ModelState.AddModelError("Input.LectureDate", "Cannot schedule lecture on a holiday.");
                return Page();
            }

            // Update lecture
            var startDateTime = Input.LectureDate.Date.Add(Input.StartTime);
            var endDateTime = startDateTime.AddMinutes(Input.DurationMinutes);

            // Check for duplicate lecture (excluding current)
            var duplicateLecture = await _context.Lectures
                .AnyAsync(l => l.Id != Input.Id && 
                              l.TimetableRuleId == lecture.TimetableRuleId && 
                              l.StartDateTime == startDateTime);

            if (duplicateLecture)
            {
                ModelState.AddModelError("", "A lecture already exists for this timetable rule at this date and time.");
                return Page();
            }

            lecture.StartDateTime = startDateTime;
            lecture.EndDateTime = endDateTime;
            // Note: Room and LectureType are on TimetableRule, not Lecture
            // If you want to override them, you need to update the TimetableRule

            await _context.SaveChangesAsync();

            TempData["Success"] = "Lecture updated successfully.";
            return RedirectToPage("/Admin/Lectures/Index");
        }
    }
}
