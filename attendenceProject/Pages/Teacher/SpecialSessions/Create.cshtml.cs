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
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public CreateModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public CreateSpecialSessionInput Input { get; set; } = new();

        public List<TeacherCourseOption> TeacherCourses { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
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

            await LoadTeacherCoursesAsync(teacher.Id);
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

            await LoadTeacherCoursesAsync(teacher.Id);

            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Validate that the selected timetable rule belongs to this teacher
            var timetableRule = await _context.TimetableRules
                .Include(tr => tr.TeacherCourse)
                .FirstOrDefaultAsync(tr => tr.Id == Input.TimetableRuleId 
                    && tr.TeacherCourse.TeacherId == teacher.Id);

            if (timetableRule == null)
            {
                ModelState.AddModelError("", "Invalid course selection.");
                return Page();
            }

            // Validate date is in future
            if (Input.SessionDate < DateTime.Today)
            {
                ModelState.AddModelError("Input.SessionDate", "Session date must be in the future.");
                return Page();
            }

            // Create datetime from date and time
            var startDateTime = Input.SessionDate.Add(Input.StartTime);
            var endDateTime = startDateTime.AddMinutes(Input.DurationMinutes);

            // Check for overlaps with existing lectures for this teacher
            var hasOverlap = await _context.Lectures
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                .AnyAsync(l => 
                    l.TimetableRule.TeacherCourse.TeacherId == teacher.Id &&
                    ((startDateTime >= l.StartDateTime && startDateTime < l.EndDateTime) ||
                     (endDateTime > l.StartDateTime && endDateTime <= l.EndDateTime) ||
                     (startDateTime <= l.StartDateTime && endDateTime >= l.EndDateTime)));

            if (hasOverlap)
            {
                ModelState.AddModelError("", "This session overlaps with another lecture. Please choose a different time.");
                return Page();
            }

            // Create the special session
            var lecture = new Lecture
            {
                TimetableRuleId = Input.TimetableRuleId,
                StartDateTime = startDateTime,
                EndDateTime = endDateTime,
                AttendanceDeadline = endDateTime.AddMinutes(20),
                Status = "Scheduled",
                LectureType = Input.LectureType,
                CreatedByTeacherId = teacher.Id,
                Description = Input.Description
            };

            _context.Lectures.Add(lecture);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"{Input.LectureType} scheduled successfully for {startDateTime:MMM dd, yyyy hh:mm tt}";
            return RedirectToPage("./Index");
        }

        private async Task LoadTeacherCoursesAsync(int teacherId)
        {
            TeacherCourses = await _context.TimetableRules
                .Include(tr => tr.TeacherCourse)
                    .ThenInclude(tc => tc.Course)
                .Include(tr => tr.TeacherCourse.Section)
                    .ThenInclude(s => s.Badge)
                .Where(tr => tr.TeacherCourse.TeacherId == teacherId)
                .Select(tr => new TeacherCourseOption
                {
                    TimetableRuleId = tr.Id,
                    DisplayText = $"{tr.TeacherCourse.Course.Code} - {tr.TeacherCourse.Course.Title} | {tr.TeacherCourse.Section.Badge.Name} - {tr.TeacherCourse.Section.Name}"
                })
                .Distinct()
                .ToListAsync();
        }
    }

    public class CreateSpecialSessionInput
    {
        [Required(ErrorMessage = "Please select a course and section")]
        [Display(Name = "Course & Section")]
        public int TimetableRuleId { get; set; }

        [Required(ErrorMessage = "Please select session type")]
        [Display(Name = "Session Type")]
        public string LectureType { get; set; } = "Quiz";

        [Required(ErrorMessage = "Session date is required")]
        [Display(Name = "Date")]
        [DataType(DataType.Date)]
        public DateTime SessionDate { get; set; } = DateTime.Today.AddDays(1);

        [Required(ErrorMessage = "Start time is required")]
        [Display(Name = "Start Time")]
        [DataType(DataType.Time)]
        public TimeSpan StartTime { get; set; } = new TimeSpan(9, 0, 0);

        [Required(ErrorMessage = "Duration is required")]
        [Range(15, 300, ErrorMessage = "Duration must be between 15 and 300 minutes")]
        [Display(Name = "Duration (minutes)")]
        public int DurationMinutes { get; set; } = 60;

        [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        [Display(Name = "Description (Optional)")]
        public string? Description { get; set; }
    }

    public class TeacherCourseOption
    {
        public int TimetableRuleId { get; set; }
        public string DisplayText { get; set; } = string.Empty;
    }
}
