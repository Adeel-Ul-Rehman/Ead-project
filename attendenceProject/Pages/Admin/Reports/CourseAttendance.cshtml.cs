using attendence.Data.Data;
using attendence.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace attendenceProject.Pages.Admin.Reports
{
    public class CourseAttendanceModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public CourseAttendanceModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<CourseAttendanceData> CourseData { get; set; } = new();
        public List<Course> Courses { get; set; } = new();
        public List<Section> Sections { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public DateTime? StartDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? EndDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? SelectedCourseId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? SelectedSectionId { get; set; }

        public class CourseAttendanceData
        {
            public int CourseId { get; set; }
            public string CourseName { get; set; } = string.Empty;
            public string CourseCode { get; set; } = string.Empty;
            public string Section { get; set; } = string.Empty;
            public int TotalLectures { get; set; }
            public int TotalStudents { get; set; }
            public int TotalAttendanceRecords { get; set; }
            public int PresentCount { get; set; }
            public int AbsentCount { get; set; }
            public int LeaveCount { get; set; }
            public double AttendancePercentage { get; set; }
            public string Status { get; set; } = string.Empty;
        }

        public async Task OnGetAsync()
        {
            if (!StartDate.HasValue)
                StartDate = DateTime.Today.AddMonths(-1);
            if (!EndDate.HasValue)
                EndDate = DateTime.Today;

            Courses = await _context.Courses.OrderBy(c => c.Title).ToListAsync();
            Sections = await _context.Sections.Include(s => s.Badge).OrderBy(s => s.Name).ToListAsync();

            var teacherCoursesQuery = _context.TeacherCourses
                .Include(tc => tc.Course)
                .Include(tc => tc.Section)
                    .ThenInclude(s => s.Badge)
                .AsQueryable();

            if (SelectedCourseId.HasValue)
            {
                teacherCoursesQuery = teacherCoursesQuery.Where(tc => tc.CourseId == SelectedCourseId.Value);
            }

            if (SelectedSectionId.HasValue)
            {
                teacherCoursesQuery = teacherCoursesQuery.Where(tc => tc.SectionId == SelectedSectionId.Value);
            }

            var teacherCourses = await teacherCoursesQuery.ToListAsync();

            foreach (var tc in teacherCourses)
            {
                var lectures = await _context.Lectures
                    .Include(l => l.TimetableRule)
                    .Where(l => l.TimetableRule.TeacherCourseId == tc.Id &&
                               l.StartDateTime.Date >= StartDate.Value &&
                               l.StartDateTime.Date <= EndDate.Value)
                    .ToListAsync();

                var students = await _context.Students
                    .Where(s => s.SectionId == tc.SectionId)
                    .ToListAsync();

                var attendanceRecords = await _context.AttendanceRecords
                    .Where(ar => lectures.Select(l => l.Id).Contains(ar.LectureId))
                    .ToListAsync();

                var totalLectures = lectures.Count;
                var totalStudents = students.Count;
                var totalRecords = attendanceRecords.Count;
                var presentCount = attendanceRecords.Count(ar => ar.Status == "Present");
                var absentCount = attendanceRecords.Count(ar => ar.Status == "Absent");
                var leaveCount = attendanceRecords.Count(ar => ar.Status == "Leave");

                var expectedRecords = totalLectures * totalStudents;
                var attendancePercentage = expectedRecords > 0
                    ? Math.Round((double)presentCount / expectedRecords * 100, 2)
                    : 0;

                var status = attendancePercentage >= 75 ? "Excellent" :
                            attendancePercentage >= 65 ? "Good" :
                            attendancePercentage >= 50 ? "Average" : "Poor";

                CourseData.Add(new CourseAttendanceData
                {
                    CourseId = tc.Course.Id,
                    CourseName = tc.Course.Title,
                    CourseCode = tc.Course.Code,
                    Section = tc.Section.Name + " (" + tc.Section.Badge?.Name + ")",
                    TotalLectures = totalLectures,
                    TotalStudents = totalStudents,
                    TotalAttendanceRecords = totalRecords,
                    PresentCount = presentCount,
                    AbsentCount = absentCount,
                    LeaveCount = leaveCount,
                    AttendancePercentage = attendancePercentage,
                    Status = status
                });
            }

            CourseData = CourseData.OrderByDescending(c => c.AttendancePercentage).ToList();
        }

        public async Task<IActionResult> OnGetExportCsvAsync()
        {
            await OnGetAsync();

            var csv = new StringBuilder();
            csv.AppendLine("Course Code,Course Name,Section,Total Lectures,Total Students,Present,Absent,Leave,Attendance %,Status");

            foreach (var data in CourseData)
            {
                csv.AppendLine($"\"{data.CourseCode}\",\"{data.CourseName}\",\"{data.Section}\",{data.TotalLectures},{data.TotalStudents},{data.PresentCount},{data.AbsentCount},{data.LeaveCount},{data.AttendancePercentage},{data.Status}");
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"course_attendance_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        }
    }
}
