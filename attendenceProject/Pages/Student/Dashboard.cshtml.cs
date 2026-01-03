using attendence.Data.Data;
using attendence.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace attendenceProject.Pages.Student;

[Authorize(Roles = "Student")]
public class DashboardModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public DashboardModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string RollNo { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string BadgeName { get; set; } = string.Empty;
    public int Semester { get; set; }
    public int TotalLectures { get; set; }
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public decimal AttendancePercentage { get; set; }
    public int ClassRank { get; set; }
    public int TotalStudents { get; set; }
    public List<Lecture> TodayLectures { get; set; } = new();
    public List<Lecture> WeekLectures { get; set; } = new();
    public List<Lecture> SpecialSessions { get; set; } = new();
    public List<CourseAttendanceViewModel> CourseAttendance { get; set; } = new();

    public async Task OnGetAsync()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdClaim, out int userId))
        {
            var student = await _context.Students
                .Include(s => s.User)
                .Include(s => s.Section)
                    .ThenInclude(sec => sec.Badge)
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (student != null)
            {
                StudentId = student.Id;
                StudentName = student.User.FullName;
                RollNo = student.RollNo;
                SectionName = student.Section.Name;
                BadgeName = student.Section.Badge.Name;
                Semester = student.Section.Semester;

                // Calculate overall attendance
                var attendanceRecords = await _context.AttendanceRecords
                    .Where(ar => ar.StudentId == StudentId)
                    .ToListAsync();

                TotalLectures = attendanceRecords.Count;
                PresentCount = attendanceRecords.Count(ar => ar.Status == "Present");
                AbsentCount = attendanceRecords.Count(ar => ar.Status == "Absent");

                if (TotalLectures > 0)
                {
                    AttendancePercentage = Math.Round((decimal)PresentCount / TotalLectures * 100, 2);
                }

                // Calculate class ranking
                var sectionStudents = await _context.Students
                    .Where(s => s.SectionId == student.SectionId)
                    .Select(s => new
                    {
                        s.Id,
                        AttendancePercentage = _context.AttendanceRecords
                            .Where(ar => ar.StudentId == s.Id && ar.Status == "Present")
                            .Count() * 100.0 / 
                            Math.Max(1, _context.AttendanceRecords.Where(ar => ar.StudentId == s.Id).Count())
                    })
                    .ToListAsync();

                TotalStudents = sectionStudents.Count;
                var orderedStudents = sectionStudents.OrderByDescending(s => s.AttendancePercentage).ToList();
                ClassRank = orderedStudents.FindIndex(s => s.Id == StudentId) + 1;

                // Get today's lectures
                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);

                var sectionTeacherCourseIds = await _context.TeacherCourses
                    .Where(tc => tc.SectionId == student.SectionId)
                    .Select(tc => tc.Id)
                    .ToListAsync();

                TodayLectures = await _context.Lectures
                    .Include(l => l.TimetableRule)
                        .ThenInclude(tr => tr.TeacherCourse)
                            .ThenInclude(tc => tc.Course)
                    .Include(l => l.TimetableRule.TeacherCourse.Teacher)
                        .ThenInclude(t => t.User)
                    .Where(l => sectionTeacherCourseIds.Contains(l.TimetableRule.TeacherCourseId) &&
                                l.StartDateTime >= today && l.StartDateTime < tomorrow &&
                                l.Status != "Cancelled" &&
                                l.LectureType == "Regular")
                    .OrderBy(l => l.StartDateTime)
                    .ToListAsync();

                // Get week's lectures (current week)
                var startOfWeek = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
                var endOfWeek = startOfWeek.AddDays(7);

                WeekLectures = await _context.Lectures
                    .Include(l => l.TimetableRule)
                        .ThenInclude(tr => tr.TeacherCourse)
                            .ThenInclude(tc => tc.Course)
                    .Include(l => l.TimetableRule.TeacherCourse.Teacher)
                        .ThenInclude(t => t.User)
                    .Where(l => sectionTeacherCourseIds.Contains(l.TimetableRule.TeacherCourseId) &&
                                l.StartDateTime >= startOfWeek && l.StartDateTime < endOfWeek &&
                                l.Status != "Cancelled" &&
                                l.LectureType == "Regular")
                    .OrderBy(l => l.StartDateTime)
                    .ToListAsync();

                // Get special sessions (not completed/locked)
                SpecialSessions = await _context.Lectures
                    .Include(l => l.TimetableRule)
                        .ThenInclude(tr => tr.TeacherCourse)
                            .ThenInclude(tc => tc.Course)
                    .Include(l => l.CreatedByTeacher)
                        .ThenInclude(t => t!.User)
                    .Where(l => sectionTeacherCourseIds.Contains(l.TimetableRule.TeacherCourseId) &&
                                l.LectureType != "Regular" &&
                                l.Status != "Locked" &&
                                l.Status != "Cancelled" &&
                                l.StartDateTime >= DateTime.Now.AddDays(-1))
                    .OrderBy(l => l.StartDateTime)
                    .ToListAsync();

                // Get course-wise attendance
                var courses = await _context.TeacherCourses
                    .Include(tc => tc.Course)
                    .Where(tc => tc.SectionId == student.SectionId)
                    .Select(tc => new
                    {
                        tc.CourseId,
                        tc.Course.Title,
                        tc.Course.Code,
                        tc.Id
                    })
                    .Distinct()
                    .ToListAsync();

                foreach (var course in courses)
                {
                    var lectureIds = await _context.Lectures
                        .Include(l => l.TimetableRule)
                        .Where(l => l.TimetableRule.TeacherCourse.CourseId == course.CourseId &&
                                   l.TimetableRule.TeacherCourse.SectionId == student.SectionId)
                        .Select(l => l.Id)
                        .ToListAsync();

                    var courseRecords = attendanceRecords.Where(ar => lectureIds.Contains(ar.LectureId)).ToList();
                    var courseTotal = courseRecords.Count;
                    var coursePresent = courseRecords.Count(ar => ar.Status == "Present");

                    if (courseTotal > 0)
                    {
                        CourseAttendance.Add(new CourseAttendanceViewModel
                        {
                            CourseName = course.Title,
                            CourseCode = course.Code,
                            TotalLectures = courseTotal,
                            PresentCount = coursePresent,
                            Percentage = Math.Round((decimal)coursePresent / courseTotal * 100, 2)
                        });
                    }
                }
            }
        }
    }
}

public class CourseAttendanceViewModel
{
    public string CourseName { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public int TotalLectures { get; set; }
    public int PresentCount { get; set; }
    public decimal Percentage { get; set; }
}