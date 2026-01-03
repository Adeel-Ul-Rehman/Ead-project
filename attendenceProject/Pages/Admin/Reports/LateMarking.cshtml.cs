using attendence.Data.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace attendenceProject.Pages.Admin.Reports
{
    public class LateMarkingModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public LateMarkingModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<LateMarkingData> LateMarkings { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public DateTime? StartDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? EndDate { get; set; }

        public class LateMarkingData
        {
            public int LectureId { get; set; }
            public string CourseName { get; set; } = string.Empty;
            public string TeacherName { get; set; } = string.Empty;
            public string Section { get; set; } = string.Empty;
            public DateTime LectureDate { get; set; }
            public DateTime LectureEndTime { get; set; }
            public DateTime MarkedAt { get; set; }
            public double HoursLate { get; set; }
            public string Status { get; set; } = string.Empty;
        }

        public async Task OnGetAsync()
        {
            if (!StartDate.HasValue)
                StartDate = DateTime.Today.AddMonths(-1);
            if (!EndDate.HasValue)
                EndDate = DateTime.Today;

            var lectures = await _context.Lectures
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Teacher)
                            .ThenInclude(t => t.User)
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Course)
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Section)
                .Include(l => l.AttendanceRecords)
                .Where(l => l.StartDateTime.Date >= StartDate.Value &&
                           l.StartDateTime.Date <= EndDate.Value &&
                           l.AttendanceRecords.Any())
                .ToListAsync();

            foreach (var lecture in lectures)
            {
                // Get the first marked attendance time
                var firstMarkedAt = lecture.AttendanceRecords.Min(ar => ar.MarkedAt);
                var hoursLate = (firstMarkedAt - lecture.EndDateTime).TotalHours;
                
                var status = "On Time";
                if (hoursLate > 24)
                    status = "Very Late (>24h)";
                else if (hoursLate > 12)
                    status = "Late (12-24h)";
                else if (hoursLate > 0)
                    status = "Slightly Late (<12h)";

                // Only include late markings
                if (hoursLate > 0)
                {
                    LateMarkings.Add(new LateMarkingData
                    {
                        LectureId = lecture.Id,
                        CourseName = lecture.TimetableRule?.TeacherCourse?.Course?.Title ?? "N/A",
                        TeacherName = lecture.TimetableRule?.TeacherCourse?.Teacher?.User?.FullName ?? "N/A",
                        Section = lecture.TimetableRule?.TeacherCourse?.Section?.Name ?? "N/A",
                        LectureDate = lecture.StartDateTime.Date,
                        LectureEndTime = lecture.EndDateTime,
                        MarkedAt = firstMarkedAt,
                        HoursLate = Math.Round(hoursLate, 2),
                        Status = status
                    });
                }
            }
        }

        public async Task<IActionResult> OnGetExportCsvAsync()
        {
            await OnGetAsync();

            var csv = new StringBuilder();
            csv.AppendLine("Course,Teacher,Section,Lecture Date,Lecture End,Marked At,Hours Late,Status");

            foreach (var data in LateMarkings)
            {
                csv.AppendLine($"\"{data.CourseName}\",\"{data.TeacherName}\",\"{data.Section}\",{data.LectureDate:yyyy-MM-dd},{data.LectureEndTime:yyyy-MM-dd HH:mm},{data.MarkedAt:yyyy-MM-dd HH:mm},{data.HoursLate},{data.Status}");
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"late_marking_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        }
    }
}
