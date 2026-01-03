using attendence.Data.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace attendenceProject.Pages.Admin.Reports
{
    public class AttendanceTrendsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public AttendanceTrendsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<TrendData> WeeklyTrends { get; set; } = new();
        public List<TrendData> MonthlyTrends { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public DateTime? StartDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? EndDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string ViewType { get; set; } = "weekly"; // weekly or monthly

        public class TrendData
        {
            public string Period { get; set; } = string.Empty;
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public int TotalLectures { get; set; }
            public int TotalRecords { get; set; }
            public int PresentCount { get; set; }
            public int AbsentCount { get; set; }
            public int LeaveCount { get; set; }
            public double AttendancePercentage { get; set; }
            public string Trend { get; set; } = string.Empty; // Up, Down, Stable
        }

        public async Task OnGetAsync()
        {
            if (!StartDate.HasValue)
                StartDate = DateTime.Today.AddMonths(-3);
            if (!EndDate.HasValue)
                EndDate = DateTime.Today;

            var lectures = await _context.Lectures
                .Include(l => l.TimetableRule)
                .Include(l => l.AttendanceRecords)
                .Where(l => l.StartDateTime.Date >= StartDate.Value &&
                           l.StartDateTime.Date <= EndDate.Value &&
                           l.AttendanceRecords.Any())
                .ToListAsync();

            var attendanceRecords = await _context.AttendanceRecords
                .Where(ar => lectures.Select(l => l.Id).Contains(ar.LectureId))
                .ToListAsync();

            if (ViewType == "weekly")
            {
                await GenerateWeeklyTrends(lectures, attendanceRecords);
            }
            else
            {
                await GenerateMonthlyTrends(lectures, attendanceRecords);
            }
        }

        private async Task GenerateWeeklyTrends(List<attendence.Domain.Entities.Lecture> lectures, List<attendence.Domain.Entities.AttendanceRecord> attendanceRecords)
        {
            var currentDate = StartDate.Value;
            double? previousPercentage = null;

            while (currentDate <= EndDate.Value)
            {
                var weekStart = currentDate;
                var weekEnd = currentDate.AddDays(6);

                var weekLectures = lectures.Where(l => l.StartDateTime.Date >= weekStart && l.StartDateTime.Date <= weekEnd).ToList();
                var weekRecords = attendanceRecords.Where(ar => weekLectures.Select(l => l.Id).Contains(ar.LectureId)).ToList();

                var totalLectures = weekLectures.Count;
                var totalRecords = weekRecords.Count;
                var presentCount = weekRecords.Count(ar => ar.Status == "Present");
                var absentCount = weekRecords.Count(ar => ar.Status == "Absent");
                var leaveCount = weekRecords.Count(ar => ar.Status == "Leave");

                var expectedRecords = weekLectures.Sum(l => _context.Students.Count(s => s.SectionId == l.TimetableRule.TeacherCourse.SectionId));
                var attendancePercentage = expectedRecords > 0
                    ? Math.Round((double)presentCount / expectedRecords * 100, 2)
                    : 0;

                string trend = "Stable";
                if (previousPercentage.HasValue)
                {
                    if (attendancePercentage > previousPercentage + 2)
                        trend = "Up ↗️";
                    else if (attendancePercentage < previousPercentage - 2)
                        trend = "Down ↘️";
                }
                previousPercentage = attendancePercentage;

                WeeklyTrends.Add(new TrendData
                {
                    Period = $"Week {weekStart:MMM dd} - {weekEnd:MMM dd, yyyy}",
                    StartDate = weekStart,
                    EndDate = weekEnd,
                    TotalLectures = totalLectures,
                    TotalRecords = totalRecords,
                    PresentCount = presentCount,
                    AbsentCount = absentCount,
                    LeaveCount = leaveCount,
                    AttendancePercentage = attendancePercentage,
                    Trend = trend
                });

                currentDate = currentDate.AddDays(7);
            }
        }

        private async Task GenerateMonthlyTrends(List<attendence.Domain.Entities.Lecture> lectures, List<attendence.Domain.Entities.AttendanceRecord> attendanceRecords)
        {
            var currentDate = new DateTime(StartDate.Value.Year, StartDate.Value.Month, 1);
            double? previousPercentage = null;

            while (currentDate <= EndDate.Value)
            {
                var monthStart = currentDate;
                var monthEnd = currentDate.AddMonths(1).AddDays(-1);

                var monthLectures = lectures.Where(l => l.StartDateTime.Date >= monthStart && l.StartDateTime.Date <= monthEnd).ToList();
                var monthRecords = attendanceRecords.Where(ar => monthLectures.Select(l => l.Id).Contains(ar.LectureId)).ToList();

                var totalLectures = monthLectures.Count;
                var totalRecords = monthRecords.Count;
                var presentCount = monthRecords.Count(ar => ar.Status == "Present");
                var absentCount = monthRecords.Count(ar => ar.Status == "Absent");
                var leaveCount = monthRecords.Count(ar => ar.Status == "Leave");

                var expectedRecords = monthLectures.Sum(l => _context.Students.Count(s => s.SectionId == l.TimetableRule.TeacherCourse.SectionId));
                var attendancePercentage = expectedRecords > 0
                    ? Math.Round((double)presentCount / expectedRecords * 100, 2)
                    : 0;

                string trend = "Stable";
                if (previousPercentage.HasValue)
                {
                    if (attendancePercentage > previousPercentage + 2)
                        trend = "Up ↗️";
                    else if (attendancePercentage < previousPercentage - 2)
                        trend = "Down ↘️";
                }
                previousPercentage = attendancePercentage;

                MonthlyTrends.Add(new TrendData
                {
                    Period = monthStart.ToString("MMMM yyyy"),
                    StartDate = monthStart,
                    EndDate = monthEnd,
                    TotalLectures = totalLectures,
                    TotalRecords = totalRecords,
                    PresentCount = presentCount,
                    AbsentCount = absentCount,
                    LeaveCount = leaveCount,
                    AttendancePercentage = attendancePercentage,
                    Trend = trend
                });

                currentDate = currentDate.AddMonths(1);
            }
        }

        public async Task<IActionResult> OnGetExportCsvAsync()
        {
            await OnGetAsync();

            var csv = new StringBuilder();
            csv.AppendLine("Period,Total Lectures,Present,Absent,Leave,Attendance %,Trend");

            var trends = ViewType == "weekly" ? WeeklyTrends : MonthlyTrends;

            foreach (var data in trends)
            {
                csv.AppendLine($"\"{data.Period}\",{data.TotalLectures},{data.PresentCount},{data.AbsentCount},{data.LeaveCount},{data.AttendancePercentage},{data.Trend}");
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"attendance_trends_{ViewType}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        }
    }
}
