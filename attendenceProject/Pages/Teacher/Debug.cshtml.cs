using attendence.Data.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace attendenceProject.Pages.Teacher;

[Authorize(Roles = "Teacher")]
public class DebugModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public DebugModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public int UserId { get; set; }
    public int TeacherId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public string TodayDayOfWeek { get; set; } = string.Empty;
    public string CurrentTime { get; set; } = string.Empty;
    public List<TeacherCourseInfo> TeacherCourses { get; set; } = new();
    public List<TimetableRuleInfo> TimetableRules { get; set; } = new();
    public List<LectureInfo> Lectures { get; set; } = new();
    public List<string> DebugMessages { get; set; } = new();

    public class TeacherCourseInfo
    {
        public int Id { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public string BadgeName { get; set; } = string.Empty;
        public int Semester { get; set; }
    }

    public class TimetableRuleInfo
    {
        public int Id { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string Days { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
    }

    public class LectureInfo
    {
        public int Id { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public string DateTime { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public async Task OnGetAsync()
    {
        TodayDayOfWeek = DateTime.Now.DayOfWeek.ToString();
        CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        
        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdClaim, out int userId))
        {
            UserId = userId;

            var teacher = await _context.Teachers
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (teacher != null)
            {
                TeacherId = teacher.Id;
                TeacherName = teacher.User.FullName;

                DebugMessages.Add($"Found teacher: {TeacherName} (ID: {TeacherId})");

                // Get Teacher Courses
                TeacherCourses = await _context.TeacherCourses
                    .Where(tc => tc.TeacherId == TeacherId)
                    .Include(tc => tc.Course)
                    .Include(tc => tc.Section)
                        .ThenInclude(s => s.Badge)
                    .Select(tc => new TeacherCourseInfo
                    {
                        Id = tc.Id,
                        CourseName = tc.Course.Title,
                        CourseCode = tc.Course.Code,
                        SectionName = tc.Section.Name,
                        BadgeName = tc.Section.Badge.Name,
                        Semester = tc.Section.Semester
                    })
                    .ToListAsync();

                DebugMessages.Add($"Found {TeacherCourses.Count} teacher course assignments");

                // Get Timetable Rules
                var teacherCourseIds = TeacherCourses.Select(tc => tc.Id).ToList();
                
                var timetableRulesQuery = await _context.TimetableRules
                    .Where(tr => teacherCourseIds.Contains(tr.TeacherCourseId))
                    .Include(tr => tr.TeacherCourse.Course)
                    .ToListAsync();

                DebugMessages.Add($"Found {timetableRulesQuery.Count} timetable rules");

                foreach (var rule in timetableRulesQuery)
                {
                    DebugMessages.Add($"Rule {rule.Id}: {rule.TeacherCourse.Course.Title} - Days: {rule.DaysOfWeek} - Start: {rule.StartDate:yyyy-MM-dd} - End: {rule.EndDate:yyyy-MM-dd} - Time: {rule.StartTime}");
                }

                TimetableRules = timetableRulesQuery.Select(tr => new TimetableRuleInfo
                {
                    Id = tr.Id,
                    CourseName = tr.TeacherCourse.Course.Title,
                    Days = tr.DaysOfWeek,
                    Time = tr.StartTime.ToString(@"hh\:mm") + " (" + tr.DurationMinutes + " min)",
                    Room = tr.Room ?? "N/A"
                }).ToList();

                // Get Lectures
                Lectures = await _context.Lectures
                    .Where(l => teacherCourseIds.Contains(l.TimetableRule.TeacherCourseId))
                    .Include(l => l.TimetableRule)
                        .ThenInclude(tr => tr.TeacherCourse)
                            .ThenInclude(tc => tc.Course)
                    .Include(l => l.TimetableRule.TeacherCourse.Section)
                    .OrderByDescending(l => l.StartDateTime)
                    .Select(l => new LectureInfo
                    {
                        Id = l.Id,
                        CourseName = l.TimetableRule.TeacherCourse.Course.Title,
                        SectionName = l.TimetableRule.TeacherCourse.Section.Name,
                        DateTime = l.StartDateTime.ToString("MMM dd, yyyy hh:mm tt"),
                        Status = l.Status
                    })
                    .ToListAsync();

                DebugMessages.Add($"Found {Lectures.Count} lecture records");
                
                // Check day matching
                var today = DateTime.Today;
                var dayShort = today.DayOfWeek.ToString().Substring(0, 3);
                var dayFull = today.DayOfWeek.ToString();
                DebugMessages.Add($"Today is: {dayFull} ({dayShort})");
                
                foreach (var rule in timetableRulesQuery)
                {
                    var days = rule.DaysOfWeek?.Split(',').Select(d => d.Trim()).ToList() ?? new List<string>();
                    var matches = days.Any(d => d.Equals(dayShort, StringComparison.OrdinalIgnoreCase) || d.Equals(dayFull, StringComparison.OrdinalIgnoreCase));
                    DebugMessages.Add($"Rule {rule.Id} days '{rule.DaysOfWeek}' matches today? {matches}");
                }
            }
            else
            {
                DebugMessages.Add("Teacher not found for current user");
            }
        }
        else
        {
            DebugMessages.Add("Could not parse user ID");
        }
    }
}
