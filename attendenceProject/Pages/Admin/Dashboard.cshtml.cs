using attendence.Data.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace attendenceProject.Pages.Admin;

[Authorize(Roles = "Admin")]
public class DashboardModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public DashboardModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public int TotalUsers { get; set; }
    public int AdminCount { get; set; }
    public int TeacherCount { get; set; }
    public int StudentCount { get; set; }

    public int TotalCourses { get; set; }
    public int AssignedCourses { get; set; }

    public int TotalSections { get; set; }
    public int TotalStudents { get; set; }

    public int TotalTimetables { get; set; }
    public int TotalHolidays { get; set; }

    public int TeacherAssignments { get; set; }
    public int ActiveTeachers { get; set; }

    public int TotalBadges { get; set; }
    public int TotalLectures { get; set; }
    public int TodayLectures { get; set; }
    public int TotalTeachers { get; set; }
    public int PendingExtensionRequests { get; set; }
    public int PendingEditRequests { get; set; }
    public int TotalAttendanceRecords { get; set; }
    public double OverallAttendancePercentage { get; set; }
    public int UnmarkedLectures { get; set; }

    // Chart data
    public string AttendanceTrendLabels { get; set; } = "[]";
    public string AttendanceTrendData { get; set; } = "[]";
    public string SectionAttendanceLabels { get; set; } = "[]";
    public string SectionAttendanceData { get; set; } = "[]";
    
    // New chart data for teacher and course performance
    public string TeacherPerformanceLabels { get; set; } = "[]";
    public string TeacherPerformanceData { get; set; } = "[]";
    public string CourseAttendanceLabels { get; set; } = "[]";
    public string CourseAttendanceData { get; set; } = "[]";
    public string AttendanceStatusLabels { get; set; } = "[]";
    public string AttendanceStatusData { get; set; } = "[]";
    public string MonthlyTrendLabels { get; set; } = "[]";
    public string MonthlyTrendData { get; set; } = "[]";
    public int TopPerformingTeachers { get; set; }
    public int LowPerformingCourses { get; set; }

    public async Task OnGetAsync()
    {
        // User Statistics
        TotalUsers = await _context.Users.CountAsync();
        AdminCount = await _context.Users.CountAsync(u => u.Role == "Admin");
        TeacherCount = await _context.Users.CountAsync(u => u.Role == "Teacher");
        StudentCount = await _context.Users.CountAsync(u => u.Role == "Student");
        TotalTeachers = TeacherCount;

        // Course Statistics
        TotalCourses = await _context.Courses.CountAsync();
        AssignedCourses = await _context.TeacherCourses.Select(tc => tc.CourseId).Distinct().CountAsync();

        // Section Statistics
        TotalSections = await _context.Sections.CountAsync();
        TotalStudents = await _context.Students.CountAsync();

        // Timetable Statistics
        TotalTimetables = await _context.TimetableRules.CountAsync();
        TotalHolidays = await _context.Holidays.CountAsync();

        // Extension Requests - Auto-expire old ones first
        var now = DateTime.Now;
        var expiredRequests = await _context.AttendanceExtensionRequests
            .Where(r => r.Status == "Pending" && r.RequestedAt.AddHours(24) < now)
            .ToListAsync();

        foreach (var request in expiredRequests)
        {
            request.Status = "Expired";
        }

        if (expiredRequests.Any())
        {
            await _context.SaveChangesAsync();
        }

        PendingExtensionRequests = await _context.AttendanceExtensionRequests
            .CountAsync(r => r.Status == "Pending");

        // Teacher Assignment Statistics
        TeacherAssignments = await _context.TeacherCourses.CountAsync();
        ActiveTeachers = await _context.TeacherCourses.Select(tc => tc.TeacherId).Distinct().CountAsync();

        // Badge Statistics
        TotalBadges = await _context.Badges.CountAsync();

        // Lecture Statistics
        TotalLectures = await _context.Lectures.CountAsync();
        
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        TodayLectures = await _context.Lectures
            .Where(l => l.StartDateTime >= today && l.StartDateTime < tomorrow)
            .CountAsync();

        // Attendance Statistics
        TotalAttendanceRecords = await _context.AttendanceRecords.CountAsync();
        var totalPresent = await _context.AttendanceRecords.CountAsync(ar => ar.Status == "Present");
        OverallAttendancePercentage = TotalAttendanceRecords > 0
            ? Math.Round((double)totalPresent / TotalAttendanceRecords * 100, 1)
            : 0;

        // Unmarked lectures (older than 2 hours)
        var twoHoursAgo = DateTime.Now.AddHours(-2);
        UnmarkedLectures = await _context.Lectures
            .Where(l => l.EndDateTime < twoHoursAgo && l.Status == "Scheduled")
            .CountAsync();

        // Pending Edit Requests
        PendingEditRequests = await _context.AttendanceEditRequests
            .CountAsync(r => r.Status == "Pending");

        // Attendance Trend (Last 7 days) - Optimized with single query
        var last7Days = Enumerable.Range(0, 7)
            .Select(i => today.AddDays(-6 + i))
            .ToList();

        var sevenDaysAgo = today.AddDays(-6);
        
        // Get all attendance records for the last 7 days in one query
        var recentAttendance = await _context.AttendanceRecords
            .Include(ar => ar.Lecture)
            .Where(ar => ar.Lecture.StartDateTime >= sevenDaysAgo && ar.Lecture.StartDateTime < tomorrow)
            .Select(ar => new { ar.Lecture.StartDateTime, ar.Status })
            .ToListAsync();

        var trendData = new List<double>();
        foreach (var date in last7Days)
        {
            var nextDay = date.AddDays(1);
            var dayRecords = recentAttendance
                .Where(ar => ar.StartDateTime >= date && ar.StartDateTime < nextDay)
                .ToList();

            if (dayRecords.Any())
            {
                var dayTotal = dayRecords.Count;
                var dayPresent = dayRecords.Count(ar => ar.Status == "Present");
                var percentage = dayTotal > 0 ? Math.Round((double)dayPresent / dayTotal * 100, 1) : 0;
                trendData.Add(percentage);
            }
            else
            {
                trendData.Add(0);
            }
        }

        AttendanceTrendLabels = System.Text.Json.JsonSerializer.Serialize(
            last7Days.Select(d => d.ToString("MMM dd")).ToList()
        );
        AttendanceTrendData = System.Text.Json.JsonSerializer.Serialize(trendData);

        // Section-wise Attendance - Optimized with single query
        var topSections = await _context.Sections
            .OrderBy(s => s.Name)
            .Take(5)
            .Select(s => new { s.Id, s.Name })
            .ToListAsync();

        var sectionIds = topSections.Select(s => s.Id).ToList();
        
        // Get all students for these sections in one query
        var sectionStudents = await _context.Students
            .Where(s => sectionIds.Contains(s.SectionId))
            .Select(s => new { s.Id, s.SectionId })
            .ToListAsync();

        // Get all attendance records for these students in one query
        var studentIds = sectionStudents.Select(s => s.Id).ToList();
        var sectionAttendanceRecords = await _context.AttendanceRecords
            .Where(ar => studentIds.Contains(ar.StudentId))
            .Select(ar => new { ar.StudentId, ar.Status })
            .ToListAsync();

        var sectionLabels = new List<string>();
        var sectionData = new List<double>();

        foreach (var section in topSections)
        {
            var sectionStudentIds = sectionStudents
                .Where(s => s.SectionId == section.Id)
                .Select(s => s.Id)
                .ToList();

            if (sectionStudentIds.Any())
            {
                var records = sectionAttendanceRecords
                    .Where(ar => sectionStudentIds.Contains(ar.StudentId))
                    .ToList();

                var sectionTotal = records.Count;
                var sectionPresent = records.Count(ar => ar.Status == "Present");

                var percentage = sectionTotal > 0 ? Math.Round((double)sectionPresent / sectionTotal * 100, 1) : 0;
                sectionLabels.Add(section.Name);
                sectionData.Add(percentage);
            }
        }

        SectionAttendanceLabels = System.Text.Json.JsonSerializer.Serialize(sectionLabels);
        SectionAttendanceData = System.Text.Json.JsonSerializer.Serialize(sectionData);

        // Teacher Performance Chart - Top 10 teachers by marking rate
        var teacherPerformance = await _context.Teachers
            .Include(t => t.User)
            .Select(t => new
            {
                TeacherId = t.Id,
                TeacherName = t.User.FullName,
                TotalLectures = _context.Lectures
                    .Count(l => l.TimetableRule.TeacherCourse.TeacherId == t.Id),
                MarkedLectures = _context.Lectures
                    .Count(l => l.TimetableRule.TeacherCourse.TeacherId == t.Id && l.AttendanceRecords.Any())
            })
            .Where(t => t.TotalLectures > 0)
            .ToListAsync();

        var topTeachers = teacherPerformance
            .Select(t => new
            {
                t.TeacherName,
                MarkingRate = t.TotalLectures > 0 ? Math.Round((double)t.MarkedLectures / t.TotalLectures * 100, 1) : 0
            })
            .OrderByDescending(t => t.MarkingRate)
            .Take(10)
            .ToList();

        TeacherPerformanceLabels = System.Text.Json.JsonSerializer.Serialize(
            topTeachers.Select(t => t.TeacherName).ToList()
        );
        TeacherPerformanceData = System.Text.Json.JsonSerializer.Serialize(
            topTeachers.Select(t => t.MarkingRate).ToList()
        );
        TopPerformingTeachers = topTeachers.Count(t => t.MarkingRate >= 95);

        // Course Attendance Chart - Top 8 courses by attendance
        var courseAttendance = await _context.TeacherCourses
            .Include(tc => tc.Course)
            .Include(tc => tc.Section)
            .Select(tc => new
            {
                CourseName = tc.Course.Title + " - " + tc.Section.Name,
                Lectures = _context.Lectures
                    .Where(l => l.TimetableRule.TeacherCourseId == tc.Id)
                    .ToList(),
                Students = _context.Students
                    .Where(s => s.SectionId == tc.SectionId)
                    .Count()
            })
            .ToListAsync();

        var courseStats = new List<dynamic>();
        foreach (var course in courseAttendance)
        {
            var lectureIds = course.Lectures.Select(l => l.Id).ToList();
            var records = await _context.AttendanceRecords
                .Where(ar => lectureIds.Contains(ar.LectureId))
                .ToListAsync();

            if (records.Any())
            {
                var totalExpected = course.Lectures.Count * course.Students;
                var presentCount = records.Count(r => r.Status == "Present");
                var attendanceRate = totalExpected > 0 ? Math.Round((double)presentCount / totalExpected * 100, 1) : 0;

                courseStats.Add(new
                {
                    CourseName = course.CourseName,
                    AttendanceRate = attendanceRate
                });
            }
        }

        var topCourses = courseStats
            .OrderByDescending(c => c.AttendanceRate)
            .Take(8)
            .ToList();

        CourseAttendanceLabels = System.Text.Json.JsonSerializer.Serialize(
            topCourses.Select(c => (string)c.CourseName).ToList()
        );
        CourseAttendanceData = System.Text.Json.JsonSerializer.Serialize(
            topCourses.Select(c => (double)c.AttendanceRate).ToList()
        );
        LowPerformingCourses = courseStats.Count(c => c.AttendanceRate < 70);

        // Attendance Status Distribution (Present, Absent, Leave)
        var statusCounts = await _context.AttendanceRecords
            .GroupBy(ar => ar.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        AttendanceStatusLabels = System.Text.Json.JsonSerializer.Serialize(
            statusCounts.Select(s => s.Status).ToList()
        );
        AttendanceStatusData = System.Text.Json.JsonSerializer.Serialize(
            statusCounts.Select(s => s.Count).ToList()
        );

        // Monthly Attendance Trend (Last 6 months)
        var last6Months = Enumerable.Range(0, 6)
            .Select(i => today.AddMonths(-5 + i))
            .Select(d => new DateTime(d.Year, d.Month, 1))
            .ToList();

        var monthlyData = new List<double>();
        foreach (var month in last6Months)
        {
            var nextMonth = month.AddMonths(1);
            var monthRecords = await _context.AttendanceRecords
                .Include(ar => ar.Lecture)
                .Where(ar => ar.Lecture.StartDateTime >= month && ar.Lecture.StartDateTime < nextMonth)
                .ToListAsync();

            if (monthRecords.Any())
            {
                var monthTotal = monthRecords.Count;
                var monthPresent = monthRecords.Count(ar => ar.Status == "Present");
                var percentage = monthTotal > 0 ? Math.Round((double)monthPresent / monthTotal * 100, 1) : 0;
                monthlyData.Add(percentage);
            }
            else
            {
                monthlyData.Add(0);
            }
        }

        MonthlyTrendLabels = System.Text.Json.JsonSerializer.Serialize(
            last6Months.Select(d => d.ToString("MMM yyyy")).ToList()
        );
        MonthlyTrendData = System.Text.Json.JsonSerializer.Serialize(monthlyData);
    }
}