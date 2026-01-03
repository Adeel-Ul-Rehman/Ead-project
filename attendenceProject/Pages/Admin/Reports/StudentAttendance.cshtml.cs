using attendence.Data.Data;
using attendence.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace attendenceProject.Pages.Admin.Reports
{
    public class StudentAttendanceModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public StudentAttendanceModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<Badge> Badges { get; set; } = new();
        public List<Section> Sections { get; set; } = new();
        public List<StudentAttendanceData> AttendanceData { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public DateTime? StartDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? EndDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? SelectedBadgeId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? SelectedSectionId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchRollNo { get; set; }

        public class StudentAttendanceData
        {
            public int StudentId { get; set; }
            public string RollNo { get; set; } = string.Empty;
            public string StudentName { get; set; } = string.Empty;
            public string Section { get; set; } = string.Empty;
            public string Badge { get; set; } = string.Empty;
            public int TotalLectures { get; set; }
            public int PresentCount { get; set; }
            public int AbsentCount { get; set; }
            public int LeaveCount { get; set; }
            public double AttendancePercentage { get; set; }
            public string Status { get; set; } = string.Empty; // Good, Warning, Critical
        }

        public async Task OnGetAsync()
        {
            // Set default dates if not provided
            if (!StartDate.HasValue)
                StartDate = DateTime.Today.AddMonths(-1);
            if (!EndDate.HasValue)
                EndDate = DateTime.Today;

            // Load filter data
            Badges = await _context.Badges.OrderBy(b => b.Name).ToListAsync();

            if (SelectedBadgeId.HasValue)
            {
                Sections = await _context.Sections
                    .Where(s => s.BadgeId == SelectedBadgeId.Value)
                    .OrderBy(s => s.Name)
                    .ToListAsync();
            }

            // Build attendance report
            var studentsQuery = _context.Students
                .Include(s => s.User)
                .Include(s => s.Section)
                    .ThenInclude(sec => sec.Badge)
                .AsQueryable();

            if (SelectedSectionId.HasValue)
            {
                studentsQuery = studentsQuery.Where(s => s.SectionId == SelectedSectionId.Value);
            }
            else if (SelectedBadgeId.HasValue)
            {
                studentsQuery = studentsQuery.Where(s => s.Section.BadgeId == SelectedBadgeId.Value);
            }

            if (!string.IsNullOrWhiteSpace(SearchRollNo))
            {
                studentsQuery = studentsQuery.Where(s => s.RollNo.Contains(SearchRollNo));
            }

            var students = await studentsQuery.ToListAsync();

            foreach (var student in students)
            {
                // Get all lectures for this student's section in the date range
                var lectures = await _context.Lectures
                    .Include(l => l.TimetableRule)
                        .ThenInclude(tr => tr.TeacherCourse)
                            .ThenInclude(tc => tc.Section)
                    .Where(l => l.TimetableRule.TeacherCourse.SectionId == student.SectionId &&
                               l.StartDateTime.Date >= StartDate.Value &&
                               l.StartDateTime.Date <= EndDate.Value)
                    .ToListAsync();

                var attendanceRecords = await _context.AttendanceRecords
                    .Where(ar => ar.StudentId == student.Id &&
                                lectures.Select(l => l.Id).Contains(ar.LectureId))
                    .ToListAsync();

                var totalLectures = lectures.Count;
                var presentCount = attendanceRecords.Count(ar => ar.Status == "Present");
                var absentCount = attendanceRecords.Count(ar => ar.Status == "Absent");
                var leaveCount = attendanceRecords.Count(ar => ar.Status == "Leave");

                var attendancePercentage = totalLectures > 0
                    ? Math.Round((double)presentCount / totalLectures * 100, 2)
                    : 0;

                var status = attendancePercentage >= 75 ? "Good" :
                            attendancePercentage >= 65 ? "Warning" : "Critical";

                AttendanceData.Add(new StudentAttendanceData
                {
                    StudentId = student.Id,
                    RollNo = student.RollNo,
                    StudentName = student.User?.FullName ?? "N/A",
                    Section = student.Section?.Name ?? "N/A",
                    Badge = student.Section?.Badge?.Name ?? "N/A",
                    TotalLectures = totalLectures,
                    PresentCount = presentCount,
                    AbsentCount = absentCount,
                    LeaveCount = leaveCount,
                    AttendancePercentage = attendancePercentage,
                    Status = status
                });
            }

            // Sort by attendance percentage (lowest first to highlight issues)
            AttendanceData = AttendanceData.OrderBy(a => a.AttendancePercentage).ToList();
        }

        public async Task<IActionResult> OnGetExportCsvAsync()
        {
            await OnGetAsync();

            var csv = new StringBuilder();
            csv.AppendLine("Roll No,Student Name,Section,Badge,Total Lectures,Present,Absent,Leave,Attendance %,Status");

            foreach (var data in AttendanceData)
            {
                csv.AppendLine($"\"{data.RollNo}\",\"{data.StudentName}\",\"{data.Section}\",\"{data.Badge}\",{data.TotalLectures},{data.PresentCount},{data.AbsentCount},{data.LeaveCount},{data.AttendancePercentage},{data.Status}");
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"student_attendance_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        }

        public async Task<IActionResult> OnGetExportPdfAsync()
        {
            // For now, return CSV (PDF generation requires additional library)
            return await OnGetExportCsvAsync();
        }
    }
}
