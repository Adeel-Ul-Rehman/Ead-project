using attendence.Data.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace attendenceProject.Pages.Student;

[Authorize(Roles = "Student")]
public class TimetableModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public TimetableModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public List<string> Days { get; set; } = new() { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" };
    public List<string> TimeSlots { get; set; } = new();
    public List<TimetableEntry> Timetable { get; set; } = new();

    public async Task OnGetAsync()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdClaim, out int userId))
        {
            var student = await _context.Students
                .Include(s => s.Section)
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (student != null)
            {
                // Get all timetable rules for student's section
                var timetableRules = await _context.TimetableRules
                    .Include(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Course)
                    .Include(tr => tr.TeacherCourse.Teacher)
                        .ThenInclude(t => t.User)
                    .Where(tr => tr.TeacherCourse.SectionId == student.SectionId)
                    .ToListAsync();

                // Generate time slots
                var allStartTimes = timetableRules.Select(tr => tr.StartTime).Distinct().OrderBy(t => t).ToList();
                TimeSlots = allStartTimes.Select(t => DateTime.Today.Add(t).ToString("hh:mm tt")).ToList();

                // Build timetable by parsing DaysOfWeek
                foreach (var rule in timetableRules)
                {
                    var days = rule.DaysOfWeek.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    var endTime = rule.StartTime.Add(TimeSpan.FromMinutes(rule.DurationMinutes));
                    
                    foreach (var day in days)
                    {
                        // Normalize day name to full format (e.g., "Mon" -> "Monday")
                        var dayName = day.Trim();
                        var fullDayName = dayName.Length == 3 ? GetFullDayName(dayName) : dayName;
                        
                        Timetable.Add(new TimetableEntry
                        {
                            DayOfWeek = fullDayName,
                            StartTime = DateTime.Today.Add(rule.StartTime).ToString("hh:mm tt"),
                            EndTime = DateTime.Today.Add(endTime).ToString("hh:mm tt"),
                            CourseName = rule.TeacherCourse.Course.Title,
                            CourseCode = rule.TeacherCourse.Course.Code,
                            Room = rule.Room ?? "TBA",
                            TeacherName = rule.TeacherCourse.Teacher.User.FullName,
                            LectureType = rule.LectureType ?? "Theory"
                        });
                    }
                }
            }
        }
    }

    public TimetableEntry? GetLectureForSlot(string day, string timeSlot)
    {
        return Timetable.FirstOrDefault(t => t.DayOfWeek == day && t.StartTime == timeSlot);
    }

    private string GetFullDayName(string shortDay)
    {
        return shortDay.ToLower() switch
        {
            "mon" => "Monday",
            "tue" => "Tuesday",
            "wed" => "Wednesday",
            "thu" => "Thursday",
            "fri" => "Friday",
            "sat" => "Saturday",
            "sun" => "Sunday",
            _ => shortDay
        };
    }
}

public class TimetableEntry
{
    public string DayOfWeek { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public string LectureType { get; set; } = string.Empty;

    public string GradientClass => LectureType.ToLower() switch
    {
        "lab" => "from-purple-500 to-purple-600",
        "practical" => "from-green-500 to-green-600",
        "quiz" => "from-orange-500 to-orange-600",
        "test" => "from-red-500 to-red-600",
        _ => "from-blue-500 to-blue-600"
    };
}
