using attendence.Data.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace attendenceProject.Pages.Student;

[Authorize(Roles = "Student")]
public class CoursesModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public CoursesModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public List<CourseViewModel> Courses { get; set; } = new();

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
                // Get all teacher courses for this section
                var teacherCourses = await _context.TeacherCourses
                    .Include(tc => tc.Course)
                    .Include(tc => tc.Teacher)
                        .ThenInclude(t => t.User)
                    .Where(tc => tc.SectionId == student.SectionId)
                    .ToListAsync();

                // Get all attendance records for this student
                var allAttendanceRecords = await _context.AttendanceRecords
                    .Include(ar => ar.Lecture)
                        .ThenInclude(l => l.TimetableRule)
                    .Where(ar => ar.StudentId == student.Id)
                    .ToListAsync();

                // Get all timetable rules for calculating weekly hours
                var timetableRules = await _context.TimetableRules
                    .Include(tr => tr.TeacherCourse)
                    .Where(tr => tr.TeacherCourse.SectionId == student.SectionId)
                    .ToListAsync();

                foreach (var tc in teacherCourses)
                {
                    // Get lectures for this course
                    var courseLectureIds = await _context.Lectures
                        .Include(l => l.TimetableRule)
                        .Where(l => l.TimetableRule.TeacherCourseId == tc.Id)
                        .Select(l => l.Id)
                        .ToListAsync();

                    var courseRecords = allAttendanceRecords.Where(ar => courseLectureIds.Contains(ar.LectureId)).ToList();
                    var totalLectures = courseRecords.Count;
                    var presentCount = courseRecords.Count(ar => ar.Status == "Present" || ar.Status == "Late");
                    var absentCount = courseRecords.Count(ar => ar.Status == "Absent");

                    // Calculate weekly hours for this course
                    var courseRules = timetableRules.Where(tr => tr.TeacherCourseId == tc.Id).ToList();
                    var weeklyHours = courseRules.Sum(tr => 
                    {
                        var daysCount = tr.DaysOfWeek.Split(',', StringSplitOptions.RemoveEmptyEntries).Length;
                        var duration = (decimal)tr.DurationMinutes / 60;
                        return daysCount * duration;
                    });

                    Courses.Add(new CourseViewModel
                    {
                        CourseName = tc.Course.Title,
                        CourseCode = tc.Course.Code,
                        TeacherName = tc.Teacher.User.FullName,
                        TotalLectures = totalLectures,
                        PresentCount = presentCount,
                        AbsentCount = absentCount,
                        AttendancePercentage = totalLectures > 0 ? Math.Round((decimal)presentCount / totalLectures * 100, 2) : 0,
                        WeeklyHours = Math.Round(weeklyHours, 1)
                    });
                }

                // Sort by attendance percentage (lowest first to highlight problem areas)
                Courses = Courses.OrderBy(c => c.AttendancePercentage).ToList();
            }
        }
    }
}

public class CourseViewModel
{
    public string CourseName { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public int TotalLectures { get; set; }
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public decimal AttendancePercentage { get; set; }
    public decimal WeeklyHours { get; set; }

    public string GetGradientClass()
    {
        var gradients = new[]
        {
            "from-blue-500 to-blue-600",
            "from-purple-500 to-purple-600",
            "from-pink-500 to-pink-600",
            "from-indigo-500 to-indigo-600",
            "from-teal-500 to-teal-600",
            "from-cyan-500 to-cyan-600"
        };
        
        return gradients[Math.Abs(CourseName.GetHashCode()) % gradients.Length];
    }
}
