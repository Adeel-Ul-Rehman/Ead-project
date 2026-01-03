using attendence.Data.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace attendenceProject.Pages.Admin.Students
{
    [Authorize(Roles = "Admin")]
    public class ProfileModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ProfileModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public attendence.Domain.Entities.Student Student { get; set; } = null!;
        public int TotalLectures { get; set; }
        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
        public int LeaveCount { get; set; }
        public double AttendancePercentage { get; set; }
        public List<CourseAttendanceData> CourseAttendance { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Student = await _context.Students
                .Include(s => s.User)
                .Include(s => s.Section)
                    .ThenInclude(sec => sec.Badge)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (Student == null)
            {
                return NotFound();
            }

            // Get all lectures for this student's section
            var sectionLectures = await _context.Lectures
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Course)
                .Where(l => l.TimetableRule.TeacherCourse.SectionId == Student.SectionId)
                .ToListAsync();

            TotalLectures = sectionLectures.Count;

            // Get all attendance records for this student
            var attendanceRecords = await _context.AttendanceRecords
                .Where(ar => ar.StudentId == id)
                .ToListAsync();

            PresentCount = attendanceRecords.Count(ar => ar.Status == "Present");
            AbsentCount = attendanceRecords.Count(ar => ar.Status == "Absent");
            LeaveCount = attendanceRecords.Count(ar => ar.Status == "Leave");

            AttendancePercentage = TotalLectures > 0
                ? Math.Round((double)PresentCount / TotalLectures * 100, 1)
                : 0;

            // Course-wise attendance
            var courses = sectionLectures
                .Select(l => l.TimetableRule.TeacherCourse.Course)
                .Distinct()
                .ToList();

            foreach (var course in courses)
            {
                var courseLectureIds = sectionLectures
                    .Where(l => l.TimetableRule.TeacherCourse.CourseId == course.Id)
                    .Select(l => l.Id)
                    .ToList();

                var courseTotal = courseLectureIds.Count;
                var coursePresent = attendanceRecords
                    .Count(ar => courseLectureIds.Contains(ar.LectureId) && ar.Status == "Present");

                var percentage = courseTotal > 0
                    ? Math.Round((double)coursePresent / courseTotal * 100, 1)
                    : 0;

                CourseAttendance.Add(new CourseAttendanceData
                {
                    CourseName = $"{course.Code} - {course.Title}",
                    Total = courseTotal,
                    Present = coursePresent,
                    Percentage = percentage
                });
            }

            return Page();
        }

        public class CourseAttendanceData
        {
            public string CourseName { get; set; } = string.Empty;
            public int Total { get; set; }
            public int Present { get; set; }
            public double Percentage { get; set; }
        }
    }
}
