using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using attendence.Data.Data;
using System.Security.Claims;

namespace attendenceProject.Pages.Teacher
{
    [Authorize(Roles = "Teacher")]
    public class StudentProfileModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public StudentProfileModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public StudentInfo Student { get; set; } = new();
        public AttendanceSummary OverallAttendance { get; set; } = new();
        public List<CoursePerformance> CoursePerformance { get; set; } = new();
        public List<RecentAttendanceRecord> RecentAttendance { get; set; } = new();
        public List<TrendDataPoint> TrendData { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int studentId)
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

            // Get student info
            var student = await _context.Students
                .Include(s => s.User)
                .Include(s => s.Section)
                    .ThenInclude(s => s.Badge)
                .FirstOrDefaultAsync(s => s.Id == studentId);

            if (student == null)
            {
                return NotFound();
            }

            // Verify teacher teaches this student's section
            var teachesSection = await _context.TeacherCourses
                .AnyAsync(tc => tc.TeacherId == teacher.Id && tc.SectionId == student.SectionId);

            if (!teachesSection)
            {
                return Forbid();
            }

            Student = new StudentInfo
            {
                Id = student.Id,
                RollNo = student.RollNo,
                FullName = student.User.FullName,
                Email = student.User.Email,
                FatherName = student.FatherName ?? "N/A",
                SectionName = student.Section.Name,
                BadgeName = student.Section.Badge.Name
            };

            // Get all attendance records for this student from teacher's courses
            var teacherCourseIds = await _context.TeacherCourses
                .Where(tc => tc.TeacherId == teacher.Id && tc.SectionId == student.SectionId)
                .Select(tc => tc.Id)
                .ToListAsync();

            var attendanceRecords = await _context.AttendanceRecords
                .Include(ar => ar.Lecture)
                    .ThenInclude(l => l.TimetableRule)
                        .ThenInclude(tr => tr.TeacherCourse)
                            .ThenInclude(tc => tc.Course)
                .Where(ar => ar.StudentId == studentId && 
                            teacherCourseIds.Contains(ar.Lecture.TimetableRule.TeacherCourseId))
                .OrderByDescending(ar => ar.Lecture.StartDateTime)
                .ToListAsync();

            // Calculate overall attendance
            if (attendanceRecords.Any())
            {
                var totalLectures = attendanceRecords.Count;
                var presentCount = attendanceRecords.Count(ar => ar.Status == "Present");
                var absentCount = attendanceRecords.Count(ar => ar.Status == "Absent");
                var lateCount = attendanceRecords.Count(ar => ar.Status == "Late");
                
                OverallAttendance = new AttendanceSummary
                {
                    TotalLectures = totalLectures,
                    PresentCount = presentCount,
                    AbsentCount = absentCount,
                    LateCount = lateCount,
                    AttendancePercentage = totalLectures > 0 ? (double)presentCount / totalLectures * 100 : 0
                };
            }

            // Calculate course-wise performance
            var courseGroups = attendanceRecords
                .GroupBy(ar => new
                {
                    CourseId = ar.Lecture.TimetableRule.TeacherCourse.CourseId,
                    CourseName = ar.Lecture.TimetableRule.TeacherCourse.Course.Title,
                    CourseCode = ar.Lecture.TimetableRule.TeacherCourse.Course.Code
                });

            foreach (var group in courseGroups)
            {
                var totalLectures = group.Count();
                var presentCount = group.Count(ar => ar.Status == "Present");
                var absentCount = group.Count(ar => ar.Status == "Absent");
                var lateCount = group.Count(ar => ar.Status == "Late");

                CoursePerformance.Add(new CoursePerformance
                {
                    CourseId = group.Key.CourseId,
                    CourseName = group.Key.CourseName,
                    CourseCode = group.Key.CourseCode,
                    TotalLectures = totalLectures,
                    PresentCount = presentCount,
                    AbsentCount = absentCount,
                    LateCount = lateCount,
                    AttendancePercentage = totalLectures > 0 ? (double)presentCount / totalLectures * 100 : 0
                });
            }

            // Get recent attendance (last 20 records)
            RecentAttendance = attendanceRecords
                .Take(20)
                .Select(ar => new RecentAttendanceRecord
                {
                    Date = ar.Lecture.StartDateTime,
                    CourseName = ar.Lecture.TimetableRule.TeacherCourse.Course.Title,
                    CourseCode = ar.Lecture.TimetableRule.TeacherCourse.Course.Code,
                    Time = ar.Lecture.StartDateTime.ToString("hh:mm tt"),
                    Status = ar.Status
                })
                .ToList();

            // Get trend data (last 10 lectures)
            TrendData = attendanceRecords
                .Take(10)
                .OrderBy(ar => ar.Lecture.StartDateTime)
                .Select(ar => new TrendDataPoint
                {
                    Date = ar.Lecture.StartDateTime.ToString("MMM dd"),
                    Status = ar.Status
                })
                .ToList();

            return Page();
        }
    }

    public class StudentInfo
    {
        public int Id { get; set; }
        public string RollNo { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FatherName { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public string BadgeName { get; set; } = string.Empty;
    }

    public class AttendanceSummary
    {
        public int TotalLectures { get; set; }
        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
        public int LateCount { get; set; }
        public double AttendancePercentage { get; set; }
    }

    public class CoursePerformance
    {
        public int CourseId { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public int TotalLectures { get; set; }
        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
        public int LateCount { get; set; }
        public double AttendancePercentage { get; set; }
    }

    public class RecentAttendanceRecord
    {
        public DateTime Date { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class TrendDataPoint
    {
        public string Date { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
