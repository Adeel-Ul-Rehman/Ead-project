using attendence.Data.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace attendenceProject.Pages.Admin.Reports
{
    public class StudentAnalyticsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public StudentAnalyticsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<StudentAnalyticData> TopPerformers { get; set; } = new();
        public List<StudentAnalyticData> Defaulters { get; set; } = new();
        public int TopPerformersCount { get; set; }
        public int AverageStudentsCount { get; set; }
        public int AtRiskCount { get; set; }
        public int CriticalCount { get; set; }
        public int TotalStudents { get; set; }

        public class StudentAnalyticData
        {
            public string RollNo { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Section { get; set; } = string.Empty;
            public double AttendancePercentage { get; set; }
            public int TotalLectures { get; set; }
            public int Present { get; set; }
        }

        public async Task OnGetAsync()
        {
            var students = await _context.Students
                .Include(s => s.User)
                .Include(s => s.Section)
                .ToListAsync();

            TotalStudents = students.Count;

            var studentData = new List<StudentAnalyticData>();

            foreach (var student in students)
            {
                var totalLectures = await _context.AttendanceRecords.CountAsync(a => a.StudentId == student.Id);
                var present = await _context.AttendanceRecords.CountAsync(a => a.StudentId == student.Id && a.Status == "Present");
                var percentage = totalLectures > 0 ? Math.Round((double)present / totalLectures * 100, 2) : 0;

                studentData.Add(new StudentAnalyticData
                {
                    RollNo = student.RollNo,
                    Name = student.User.FullName,
                    Section = student.Section.Name,
                    AttendancePercentage = percentage,
                    TotalLectures = totalLectures,
                    Present = present
                });

                // Count by category
                if (percentage >= 90) TopPerformersCount++;
                else if (percentage >= 75) AverageStudentsCount++;
                else if (percentage >= 60) AtRiskCount++;
                else CriticalCount++;
            }

            // Get top 10 performers
            TopPerformers = studentData
                .OrderByDescending(s => s.AttendancePercentage)
                .ThenByDescending(s => s.TotalLectures)
                .Take(10)
                .ToList();

            // Get defaulters (below 60%)
            Defaulters = studentData
                .Where(s => s.AttendancePercentage < 60)
                .OrderBy(s => s.AttendancePercentage)
                .ToList();
        }
    }
}
