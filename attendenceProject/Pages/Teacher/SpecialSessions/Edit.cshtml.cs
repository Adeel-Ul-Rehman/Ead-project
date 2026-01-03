using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using attendence.Data.Data;
using attendence.Domain.Entities;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace attendenceProject.Pages.Teacher.SpecialSessions
{
    [Authorize(Roles = "Teacher")]
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public EditModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public EditSpecialSessionInput Input { get; set; } = new();

        public string CourseInfo { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(int id)
        {
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

            var lecture = await _context.Lectures
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Course)
                .Include(l => l.TimetableRule.TeacherCourse.Section)
                    .ThenInclude(s => s.Badge)
                .FirstOrDefaultAsync(l => l.Id == id && l.CreatedByTeacherId == teacher.Id);

            if (lecture == null)
            {
                TempData["Error"] = "Special session not found or you don't have permission to edit it.";
                return RedirectToPage("./Index");
            }

            // Check if session has already started
            if (lecture.StartDateTime <= DateTime.Now)
            {
                TempData["Error"] = "Cannot edit a session that has already started.";
                return RedirectToPage("./Index");
            }

            // Populate form
            Input.Id = lecture.Id;
            Input.LectureType = lecture.LectureType ?? "Quiz";
            Input.SessionDate = lecture.StartDateTime.Date;
            Input.StartTime = lecture.StartDateTime.TimeOfDay;
            Input.DurationMinutes = (int)(lecture.EndDateTime - lecture.StartDateTime).TotalMinutes;
            Input.Description = lecture.Description;

            CourseInfo = $"{lecture.TimetableRule.TeacherCourse.Course.Code} - {lecture.TimetableRule.TeacherCourse.Course.Title} | {lecture.TimetableRule.TeacherCourse.Section.Badge.Name} - {lecture.TimetableRule.TeacherCourse.Section.Name}";

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
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

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var lecture = await _context.Lectures
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Course)
                .Include(l => l.TimetableRule.TeacherCourse.Section)
                    .ThenInclude(s => s.Badge)
                .FirstOrDefaultAsync(l => l.Id == Input.Id && l.CreatedByTeacherId == teacher.Id);

            if (lecture == null)
            {
                TempData["Error"] = "Special session not found or you don't have permission to edit it.";
                return RedirectToPage("./Index");
            }

            // Check if session has already started
            if (lecture.StartDateTime <= DateTime.Now)
            {
                TempData["Error"] = "Cannot edit a session that has already started.";
                return RedirectToPage("./Index");
            }

            // Validate date is in future
            if (Input.SessionDate < DateTime.Today)
            {
                ModelState.AddModelError("Input.SessionDate", "Session date must be in the future.");
                CourseInfo = $"{lecture.TimetableRule.TeacherCourse.Course.Code} - {lecture.TimetableRule.TeacherCourse.Course.Title} | {lecture.TimetableRule.TeacherCourse.Section.Badge.Name} - {lecture.TimetableRule.TeacherCourse.Section.Name}";
                return Page();
            }

            // Create datetime from date and time
            var startDateTime = Input.SessionDate.Add(Input.StartTime);
            var endDateTime = startDateTime.AddMinutes(Input.DurationMinutes);

            // Check for overlaps with OTHER lectures for this teacher (excluding current one)
            var hasOverlap = await _context.Lectures
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                .AnyAsync(l => 
                    l.Id != Input.Id && // Exclude current lecture
                    l.TimetableRule.TeacherCourse.TeacherId == teacher.Id &&
                    ((startDateTime >= l.StartDateTime && startDateTime < l.EndDateTime) ||
                     (endDateTime > l.StartDateTime && endDateTime <= l.EndDateTime) ||
                     (startDateTime <= l.StartDateTime && endDateTime >= l.EndDateTime)));

            if (hasOverlap)
            {
                ModelState.AddModelError("", "This session overlaps with another lecture. Please choose a different time.");
                CourseInfo = $"{lecture.TimetableRule.TeacherCourse.Course.Code} - {lecture.TimetableRule.TeacherCourse.Course.Title} | {lecture.TimetableRule.TeacherCourse.Section.Badge.Name} - {lecture.TimetableRule.TeacherCourse.Section.Name}";
                return Page();
            }

            // Update the lecture
            lecture.StartDateTime = startDateTime;
            lecture.EndDateTime = endDateTime;
            lecture.AttendanceDeadline = endDateTime.AddMinutes(20);
            lecture.LectureType = Input.LectureType;
            lecture.Description = Input.Description;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"{Input.LectureType} updated successfully.";
            return RedirectToPage("./Index");
        }
    }

    public class EditSpecialSessionInput
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Please select session type")]
        [Display(Name = "Session Type")]
        public string LectureType { get; set; } = "Quiz";

        [Required(ErrorMessage = "Session date is required")]
        [Display(Name = "Date")]
        [DataType(DataType.Date)]
        public DateTime SessionDate { get; set; }

        [Required(ErrorMessage = "Start time is required")]
        [Display(Name = "Start Time")]
        [DataType(DataType.Time)]
        public TimeSpan StartTime { get; set; }

        [Required(ErrorMessage = "Duration is required")]
        [Range(15, 300, ErrorMessage = "Duration must be between 15 and 300 minutes")]
        [Display(Name = "Duration (minutes)")]
        public int DurationMinutes { get; set; } = 60;

        [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        [Display(Name = "Description (Optional)")]
        public string? Description { get; set; }
    }
}
