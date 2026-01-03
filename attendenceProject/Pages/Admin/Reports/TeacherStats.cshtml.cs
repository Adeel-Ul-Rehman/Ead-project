using attendence.Data.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace attendenceProject.Pages.Admin.Reports
{
    public class TeacherStatsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public TeacherStatsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<TeacherStatData> TeacherStats { get; set; } = new();
        public double OverallMarkingRate { get; set; }
        public int TotalTeachers { get; set; }
        public int FullyCompliantTeachers { get; set; }

        public class TeacherStatData
        {
            public string BadgeNumber { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Designation { get; set; } = string.Empty;
            public int TotalLectures { get; set; }
            public int MarkedLectures { get; set; }
            public int LateMarked { get; set; }
            public double MarkingRate { get; set; }
            public int ExtensionRequests { get; set; }
        }

        public async Task OnGetAsync()
        {
            var teachers = await _context.Teachers
                .Include(t => t.User)
                .ToListAsync();

            TotalTeachers = teachers.Count;

            foreach (var teacher in teachers)
            {
                var totalLectures = await _context.Lectures
                    .CountAsync(l => l.TimetableRule.TeacherCourse.TeacherId == teacher.Id);

                var markedLectures = await _context.Lectures
                    .Where(l => l.TimetableRule.TeacherCourse.TeacherId == teacher.Id)
                    .Where(l => l.AttendanceRecords.Any())
                    .CountAsync();

                var lateMarked = await _context.AttendanceEditRequests
                    .CountAsync(r => r.TeacherId == teacher.Id && r.Status == "Approved");

                var extensionRequests = await _context.AttendanceExtensionRequests
                    .CountAsync(r => r.TeacherId == teacher.Id);

                var markingRate = totalLectures > 0 ? Math.Round((double)markedLectures / totalLectures * 100, 2) : 0;

                if (markingRate >= 95) FullyCompliantTeachers++;

                TeacherStats.Add(new TeacherStatData
                {
                    BadgeNumber = teacher.BadgeNumber,
                    Name = teacher.User.FullName,
                    Designation = teacher.Designation,
                    TotalLectures = totalLectures,
                    MarkedLectures = markedLectures,
                    LateMarked = lateMarked,
                    MarkingRate = markingRate,
                    ExtensionRequests = extensionRequests
                });
            }

            TeacherStats = TeacherStats.OrderByDescending(t => t.MarkingRate).ToList();
            OverallMarkingRate = TeacherStats.Any() ? Math.Round(TeacherStats.Average(t => t.MarkingRate), 2) : 0;
        }
    }
}
