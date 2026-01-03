using attendence.Data.Data;
using attendence.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace attendenceProject.Pages.Admin.Reports
{
    public class DefaultersModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public DefaultersModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<Badge> Badges { get; set; } = new();
        public List<Section> Sections { get; set; } = new();
        public List<DefaulterData> DefaultersData { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public DateTime? StartDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? EndDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? SelectedBadgeId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? SelectedSectionId { get; set; }

        [BindProperty(SupportsGet = true)]
        public double ThresholdPercentage { get; set; } = 75.0;

        public class DefaulterData
        {
            public int StudentId { get; set; }
            public string RollNo { get; set; } = string.Empty;
            public string StudentName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Section { get; set; } = string.Empty;
            public string Badge { get; set; } = string.Empty;
            public int TotalLectures { get; set; }
            public int PresentCount { get; set; }
            public int AbsentCount { get; set; }
            public double AttendancePercentage { get; set; }
            public double Shortage { get; set; }
        }

        public async Task OnGetAsync()
        {
            if (!StartDate.HasValue)
                StartDate = DateTime.Today.AddMonths(-1);
            if (!EndDate.HasValue)
                EndDate = DateTime.Today;

            Badges = await _context.Badges.OrderBy(b => b.Name).ToListAsync();

            if (SelectedBadgeId.HasValue)
            {
                Sections = await _context.Sections
                    .Where(s => s.BadgeId == SelectedBadgeId.Value)
                    .OrderBy(s => s.Name)
                    .ToListAsync();
            }

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

            var students = await studentsQuery.ToListAsync();

            foreach (var student in students)
            {
                var lectures = await _context.Lectures
                    .Include(l => l.TimetableRule)
                        .ThenInclude(tr => tr.TeacherCourse)
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

                if (totalLectures == 0) continue;

                var attendancePercentage = Math.Round((double)presentCount / totalLectures * 100, 2);

                // Only include students below threshold
                if (attendancePercentage < ThresholdPercentage)
                {
                    DefaultersData.Add(new DefaulterData
                    {
                        StudentId = student.Id,
                        RollNo = student.RollNo,
                        StudentName = student.User?.FullName ?? "N/A",
                        Email = student.User?.Email ?? "N/A",
                        Section = student.Section?.Name ?? "N/A",
                        Badge = student.Section?.Badge?.Name ?? "N/A",
                        TotalLectures = totalLectures,
                        PresentCount = presentCount,
                        AbsentCount = absentCount,
                        AttendancePercentage = attendancePercentage,
                        Shortage = Math.Round(ThresholdPercentage - attendancePercentage, 2)
                    });
                }
            }

            DefaultersData = DefaultersData.OrderBy(d => d.AttendancePercentage).ToList();
        }

        public async Task<IActionResult> OnGetExportCsvAsync()
        {
            await OnGetAsync();

            var csv = new StringBuilder();
            csv.AppendLine("Roll No,Student Name,Email,Section,Badge,Total Lectures,Present,Absent,Attendance %,Shortage %");

            foreach (var data in DefaultersData)
            {
                csv.AppendLine($"\"{data.RollNo}\",\"{data.StudentName}\",\"{data.Email}\",\"{data.Section}\",\"{data.Badge}\",{data.TotalLectures},{data.PresentCount},{data.AbsentCount},{data.AttendancePercentage},{data.Shortage}");
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"defaulters_report_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        }
    }
}
