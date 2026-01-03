using attendence.Data.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace attendenceProject.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class GenerateLectureCSVModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public GenerateLectureCSVModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required(ErrorMessage = "Teacher email is required")]
            [EmailAddress(ErrorMessage = "Invalid email address")]
            public string TeacherEmail { get; set; } = string.Empty;

            [Required(ErrorMessage = "Start date is required")]
            public DateTime StartDate { get; set; }

            [Required(ErrorMessage = "End date is required")]
            public DateTime EndDate { get; set; }
        }

        public void OnGet()
        {
            // Set defaults
            Input.StartDate = DateTime.Today;
            Input.EndDate = DateTime.Today.AddMonths(1);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Find teacher by email
            var teacher = await _context.Teachers
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.User.Email == Input.TeacherEmail);

            if (teacher == null)
            {
                ModelState.AddModelError("Input.TeacherEmail", $"No teacher found with email: {Input.TeacherEmail}");
                return Page();
            }

            // Validate dates
            if (Input.EndDate < Input.StartDate)
            {
                ModelState.AddModelError("Input.EndDate", "End date must be after start date");
                return Page();
            }

            if ((Input.EndDate - Input.StartDate).TotalDays > 365)
            {
                ModelState.AddModelError("Input.EndDate", "Maximum range is 365 days");
                return Page();
            }

            // Get teacher's timetable rules
            var timetableRules = await _context.TimetableRules
                .Include(tr => tr.TeacherCourse)
                    .ThenInclude(tc => tc.Course)
                .Include(tr => tr.TeacherCourse)
                    .ThenInclude(tc => tc.Section)
                        .ThenInclude(s => s.Badge)
                .Include(tr => tr.TeacherCourse)
                    .ThenInclude(tc => tc.Teacher)
                        .ThenInclude(t => t.User)
                .Where(tr => tr.TeacherCourse.TeacherId == teacher.Id &&
                            tr.StartDate <= Input.EndDate &&
                            tr.EndDate >= Input.StartDate)
                .ToListAsync();

            if (!timetableRules.Any())
            {
                ModelState.AddModelError("", $"No timetable rules found for {teacher.User.FullName} in the specified date range");
                return Page();
            }

            // Get holidays in the date range
            var holidays = await _context.Holidays
                .Where(h => h.Date >= Input.StartDate && h.Date <= Input.EndDate)
                .Select(h => h.Date.Date)
                .ToListAsync();

            // Generate CSV
            var csv = new StringBuilder();

            // Header
            csv.AppendLine("# Lecture Import CSV for " + teacher.User.FullName);
            csv.AppendLine("# Generated on: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            csv.AppendLine("# Date Range: " + Input.StartDate.ToString("yyyy-MM-dd") + " to " + Input.EndDate.ToString("yyyy-MM-dd"));
            csv.AppendLine("# Total Timetable Rules: " + timetableRules.Count);
            csv.AppendLine("#");
            csv.AppendLine("# CSV Format:");
            csv.AppendLine("# TimetableRuleId,Date,StartTime,EndTime,CourseCode,CourseName,Section,Badge,Room,LectureType,DayOfWeek");
            csv.AppendLine("#");
            csv.AppendLine("# Instructions:");
            csv.AppendLine("# 1. Review and edit this CSV if needed");
            csv.AppendLine("# 2. Do NOT change the TimetableRuleId column");
            csv.AppendLine("# 3. You can modify: Date, StartTime, EndTime, Room, LectureType");
            csv.AppendLine("# 4. Use the bulk import tool to create lectures from this CSV");
            csv.AppendLine("#");
            csv.AppendLine();

            // Data header
            csv.AppendLine("TimetableRuleId,Date,StartTime,EndTime,CourseCode,CourseName,Section,Badge,Room,LectureType,DayOfWeek,Status");

            int lectureCount = 0;

            // Process each timetable rule
            foreach (var rule in timetableRules)
            {
                var daysOfWeek = rule.DaysOfWeek.Split(',').Select(d => d.Trim()).ToList();

                // Iterate through each day in the date range
                for (var date = Input.StartDate.Date; date <= Input.EndDate.Date; date = date.AddDays(1))
                {
                    // Skip if it's a holiday
                    if (holidays.Contains(date))
                    {
                        continue;
                    }

                    // Skip if date is outside the rule's date range
                    if (date < rule.StartDate.Date || date > rule.EndDate.Date)
                    {
                        continue;
                    }

                    // Check if this day matches the timetable rule
                    var dayName = date.DayOfWeek.ToString().Substring(0, 3); // Mon, Tue, Wed, etc.
                    if (!daysOfWeek.Contains(dayName))
                    {
                        continue;
                    }

                    // Create lecture entry
                    var startTime = rule.StartTime;
                    var endTime = startTime.Add(TimeSpan.FromMinutes(rule.DurationMinutes));

                    csv.AppendLine($"{rule.Id}," +
                        $"{date:yyyy-MM-dd}," +
                        $"{startTime:hh\\:mm}," +
                        $"{endTime:hh\\:mm}," +
                        $"\"{rule.TeacherCourse.Course.Code}\"," +
                        $"\"{rule.TeacherCourse.Course.Title}\"," +
                        $"\"{rule.TeacherCourse.Section.Name}\"," +
                        $"\"{rule.TeacherCourse.Section.Badge.Name}\"," +
                        $"\"{rule.Room ?? ""}\"," +
                        $"\"{rule.LectureType ?? ""}\"," +
                        $"{dayName}," +
                        $"Scheduled");

                    lectureCount++;
                }
            }

            csv.AppendLine();
            csv.AppendLine($"# Total Lectures Generated: {lectureCount}");
            csv.AppendLine($"# Total Holidays Excluded: {holidays.Count}");

            // Return CSV file
            var fileName = $"lectures_{teacher.User.Email.Replace("@", "_")}_{Input.StartDate:yyyyMMdd}_to_{Input.EndDate:yyyyMMdd}.csv";
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
        }
    }
}
