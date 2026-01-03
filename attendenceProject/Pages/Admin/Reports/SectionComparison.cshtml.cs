using attendence.Data.Data;
using attendence.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace attendenceProject.Pages.Admin.Reports
{
    public class SectionComparisonModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public SectionComparisonModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<SectionComparisonData> SectionData { get; set; } = new();
        public List<Badge> Badges { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public DateTime? StartDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? EndDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? SelectedBadgeId { get; set; }

        public class SectionComparisonData
        {
            public int SectionId { get; set; }
            public string SectionName { get; set; } = string.Empty;
            public string BadgeName { get; set; } = string.Empty;
            public int TotalStudents { get; set; }
            public int TotalLectures { get; set; }
            public double AverageAttendance { get; set; }
            public int ExcellentStudents { get; set; }
            public int DefaulterStudents { get; set; }
            public string OverallPerformance { get; set; } = string.Empty;
        }

        public async Task OnGetAsync()
        {
            if (!StartDate.HasValue)
                StartDate = DateTime.Today.AddMonths(-1);
            if (!EndDate.HasValue)
                EndDate = DateTime.Today;

            Badges = await _context.Badges.OrderBy(b => b.Name).ToListAsync();

            var sectionsQuery = _context.Sections
                .Include(s => s.Badge)
                .AsQueryable();

            if (SelectedBadgeId.HasValue)
            {
                sectionsQuery = sectionsQuery.Where(s => s.BadgeId == SelectedBadgeId.Value);
            }

            var sections = await sectionsQuery.OrderBy(s => s.Name).ToListAsync();

            foreach (var section in sections)
            {
                var students = await _context.Students
                    .Where(s => s.SectionId == section.Id)
                    .ToListAsync();

                var lectures = await _context.Lectures
                    .Include(l => l.TimetableRule)
                        .ThenInclude(tr => tr.TeacherCourse)
                    .Where(l => l.TimetableRule.TeacherCourse.SectionId == section.Id &&
                               l.StartDateTime.Date >= StartDate.Value &&
                               l.StartDateTime.Date <= EndDate.Value)
                    .ToListAsync();

                var attendanceRecords = await _context.AttendanceRecords
                    .Where(ar => students.Select(s => s.Id).Contains(ar.StudentId) &&
                                lectures.Select(l => l.Id).Contains(ar.LectureId))
                    .ToListAsync();

                var totalStudents = students.Count;
                var totalLectures = lectures.Count;

                var studentAttendances = new List<double>();

                foreach (var student in students)
                {
                    var studentRecords = attendanceRecords.Where(ar => ar.StudentId == student.Id).ToList();
                    var presentCount = studentRecords.Count(ar => ar.Status == "Present");
                    var percentage = totalLectures > 0 ? (double)presentCount / totalLectures * 100 : 0;
                    studentAttendances.Add(percentage);
                }

                var averageAttendance = studentAttendances.Any() ? Math.Round(studentAttendances.Average(), 2) : 0;
                var excellentStudents = studentAttendances.Count(a => a >= 85);
                var defaulterStudents = studentAttendances.Count(a => a < 75);

                var overallPerformance = averageAttendance >= 80 ? "Excellent" :
                                        averageAttendance >= 70 ? "Good" :
                                        averageAttendance >= 60 ? "Average" : "Poor";

                SectionData.Add(new SectionComparisonData
                {
                    SectionId = section.Id,
                    SectionName = section.Name,
                    BadgeName = section.Badge?.Name ?? "N/A",
                    TotalStudents = totalStudents,
                    TotalLectures = totalLectures,
                    AverageAttendance = averageAttendance,
                    ExcellentStudents = excellentStudents,
                    DefaulterStudents = defaulterStudents,
                    OverallPerformance = overallPerformance
                });
            }

            SectionData = SectionData.OrderByDescending(s => s.AverageAttendance).ToList();
        }

        public async Task<IActionResult> OnGetExportCsvAsync()
        {
            await OnGetAsync();

            var csv = new StringBuilder();
            csv.AppendLine("Section,Badge,Total Students,Total Lectures,Avg Attendance %,Excellent Students,Defaulters,Performance");

            foreach (var data in SectionData)
            {
                csv.AppendLine($"\"{data.SectionName}\",\"{data.BadgeName}\",{data.TotalStudents},{data.TotalLectures},{data.AverageAttendance},{data.ExcellentStudents},{data.DefaulterStudents},{data.OverallPerformance}");
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"section_comparison_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        }
    }
}
