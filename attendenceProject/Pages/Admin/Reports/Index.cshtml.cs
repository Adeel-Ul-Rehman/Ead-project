using attendence.Data.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text;
using OfficeOpenXml;

namespace attendenceProject.Pages.Admin.Reports
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<ReportType> AvailableReports { get; set; } = new();

        // Statistics Properties
        public int TotalReports { get; set; }
        public int TotalStudents { get; set; }
        public int TotalTeachers { get; set; }
        public int TotalCourses { get; set; }
        public int TotalSections { get; set; }
        public double OverallAttendanceRate { get; set; }

        public class ReportType
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Icon { get; set; } = string.Empty;
            public string PageUrl { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
        }

        public async Task OnGetAsync()
        {
            await LoadStatistics();
            LoadReportTypes();
        }

        private async Task LoadStatistics()
        {
            // Calculate all statistics from database
            TotalStudents = await _context.Users.CountAsync(u => u.Role == "Student");
            TotalTeachers = await _context.Users.CountAsync(u => u.Role == "Teacher");
            TotalCourses = await _context.Courses.CountAsync();
            TotalSections = await _context.Sections.CountAsync();

            // Calculate overall attendance rate
            var totalRecords = await _context.AttendanceRecords.CountAsync();
            var presentRecords = await _context.AttendanceRecords.CountAsync(a => a.Status == "Present");
            OverallAttendanceRate = totalRecords > 0 ? Math.Round((double)presentRecords / totalRecords * 100, 2) : 0;
        }

        private void LoadReportTypes()
        {
            AvailableReports = new List<ReportType>
            {
                // Student Reports
                new ReportType
                {
                    Id = "student-attendance",
                    Name = "Student Attendance Report",
                    Description = "View attendance percentage by student for a specific date range",
                    Icon = "👨‍🎓",
                    PageUrl = "/Admin/Reports/StudentAttendance",
                    Category = "Student Reports"
                },
                new ReportType
                {
                    Id = "defaulters",
                    Name = "Defaulter Students Report",
                    Description = "Identify students with attendance below specified threshold",
                    Icon = "⚠️",
                    PageUrl = "/Admin/Reports/Defaulters",
                    Category = "Student Reports"
                },
                new ReportType
                {
                    Id = "student-analytics",
                    Name = "Student Analytics Report",
                    Description = "Top performers, defaulters, improvement/decline trends",
                    Icon = "📈",
                    PageUrl = "/Admin/Reports/StudentAnalytics",
                    Category = "Student Reports"
                },
                
                // Teacher Reports
                new ReportType
                {
                    Id = "teacher-stats",
                    Name = "Teacher Marking Statistics",
                    Description = "Track teacher attendance marking performance and timeliness",
                    Icon = "👨‍🏫",
                    PageUrl = "/Admin/Reports/TeacherStats",
                    Category = "Teacher Reports"
                },
                new ReportType
                {
                    Id = "late-marking",
                    Name = "Late Attendance Marking Report",
                    Description = "View lectures where attendance was marked late or extended",
                    Icon = "⏰",
                    PageUrl = "/Admin/Reports/LateMarking",
                    Category = "Teacher Reports"
                },
                
                // Course Reports
                new ReportType
                {
                    Id = "course-attendance",
                    Name = "Course/Section Attendance Report",
                    Description = "Analyze attendance statistics by course or section",
                    Icon = "📚",
                    PageUrl = "/Admin/Reports/CourseAttendance",
                    Category = "Course Reports"
                },
                new ReportType
                {
                    Id = "section-comparison",
                    Name = "Section Comparison Report",
                    Description = "Compare attendance performance across different sections",
                    Icon = "🔄",
                    PageUrl = "/Admin/Reports/SectionComparison",
                    Category = "Course Reports"
                },
                
                // Trend Reports
                new ReportType
                {
                    Id = "attendance-trends",
                    Name = "Attendance Trends Report",
                    Description = "Shows attendance trends over time with weekly/monthly charts",
                    Icon = "📉",
                    PageUrl = "/Admin/Reports/AttendanceTrends",
                    Category = "Trend Reports"
                }
            };

            TotalReports = AvailableReports.Count;
        }

        public async Task<IActionResult> OnPostExportReportAsync(string reportType, string format)
        {
            if (string.IsNullOrEmpty(reportType) || string.IsNullOrEmpty(format))
            {
                return BadRequest("Report type and format are required");
            }

            try
            {
                if (format.ToLower() == "excel")
                {
                    return await ExportToExcel(reportType);
                }
                else if (format.ToLower() == "pdf")
                {
                    return await ExportToPdf(reportType);
                }
                else
                {
                    return BadRequest("Invalid format");
                }
            }
            catch (Exception ex)
            {
                return BadRequest($"Export failed: {ex.Message}");
            }
        }

        private async Task<IActionResult> ExportToExcel(string reportType)
        {
            // Set EPPlus license context (Required for EPPlus 8)
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
            
            var stream = new MemoryStream();
            using var package = new ExcelPackage(stream);
            var worksheet = package.Workbook.Worksheets.Add("Report");

            // Set headers and data based on report type
            switch (reportType)
            {
                case "student-attendance":
                    await GenerateStudentAttendanceExcel(worksheet);
                    break;
                case "teacher-stats":
                    await GenerateTeacherStatsExcel(worksheet);
                    break;
                case "course-attendance":
                    await GenerateCourseAttendanceExcel(worksheet);
                    break;
                case "defaulters":
                    await GenerateDefaultersExcel(worksheet);
                    break;
                case "student-analytics":
                    await GenerateStudentAnalyticsExcel(worksheet);
                    break;
                case "teacher-performance":
                    await GenerateTeacherPerformanceExcel(worksheet);
                    break;
                case "course-statistics":
                    await GenerateCourseStatisticsExcel(worksheet);
                    break;
                case "section-comparison":
                    await GenerateSectionComparisonExcel(worksheet);
                    break;
                case "attendance-trends":
                    await GenerateAttendanceTrendsExcel(worksheet);
                    break;
                case "summary":
                    await GenerateSummaryExcel(worksheet);
                    break;
                default:
                    await GenerateGenericExcel(worksheet, reportType);
                    break;
            }

            // Auto-fit columns
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

            package.Save();
            stream.Position = 0;

            var fileName = $"{reportType}_report_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        private async Task<IActionResult> ExportToPdf(string reportType)
        {
            // Simple text-based PDF export (placeholder)
            var reportData = await GenerateReportData(reportType);
            
            var content = new StringBuilder();
            content.AppendLine($"=== {reportType.ToUpper()} REPORT ===");
            content.AppendLine($"Generated: {DateTime.Now}");
            content.AppendLine();
            content.AppendLine(reportData);

            var bytes = Encoding.UTF8.GetBytes(content.ToString());
            var fileName = $"{reportType}_report_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            
            return File(bytes, "text/plain", fileName);
        }

        private async Task GenerateStudentAttendanceExcel(OfficeOpenXml.ExcelWorksheet worksheet)
        {
            worksheet.Cells[1, 1].Value = "Roll No";
            worksheet.Cells[1, 2].Value = "Student Name";
            worksheet.Cells[1, 3].Value = "Section";
            worksheet.Cells[1, 4].Value = "Total Lectures";
            worksheet.Cells[1, 5].Value = "Present";
            worksheet.Cells[1, 6].Value = "Attendance %";

            var students = await _context.Students
                .Include(s => s.User)
                .Include(s => s.Section)
                .ToListAsync();

            int row = 2;
            foreach (var student in students)
            {
                var totalLectures = await _context.AttendanceRecords.CountAsync(a => a.StudentId == student.Id);
                var present = await _context.AttendanceRecords.CountAsync(a => a.StudentId == student.Id && a.Status == "Present");
                var percentage = totalLectures > 0 ? Math.Round((double)present / totalLectures * 100, 2) : 0;

                worksheet.Cells[row, 1].Value = student.RollNo;
                worksheet.Cells[row, 2].Value = student.User.FullName;
                worksheet.Cells[row, 3].Value = student.Section.Name;
                worksheet.Cells[row, 4].Value = totalLectures;
                worksheet.Cells[row, 5].Value = present;
                worksheet.Cells[row, 6].Value = percentage;
                row++;
            }
        }

        private async Task GenerateTeacherStatsExcel(OfficeOpenXml.ExcelWorksheet worksheet)
        {
            worksheet.Cells[1, 1].Value = "Badge Number";
            worksheet.Cells[1, 2].Value = "Teacher Name";
            worksheet.Cells[1, 3].Value = "Designation";
            worksheet.Cells[1, 4].Value = "Total Lectures";
            worksheet.Cells[1, 5].Value = "Marked Lectures";
            worksheet.Cells[1, 6].Value = "Marking Rate %";

            var teachers = await _context.Teachers
                .Include(t => t.User)
                .ToListAsync();

            int row = 2;
            foreach (var teacher in teachers)
            {
                var totalLectures = await _context.Lectures.CountAsync(l => l.TimetableRule.TeacherCourse.TeacherId == teacher.Id);
                var markedLectures = await _context.Lectures.CountAsync(l => l.TimetableRule.TeacherCourse.TeacherId == teacher.Id && l.AttendanceRecords.Any());
                var markingRate = totalLectures > 0 ? Math.Round((double)markedLectures / totalLectures * 100, 2) : 0;

                worksheet.Cells[row, 1].Value = teacher.BadgeNumber;
                worksheet.Cells[row, 2].Value = teacher.User.FullName;
                worksheet.Cells[row, 3].Value = teacher.Designation;
                worksheet.Cells[row, 4].Value = totalLectures;
                worksheet.Cells[row, 5].Value = markedLectures;
                worksheet.Cells[row, 6].Value = markingRate;
                row++;
            }
        }

        private async Task GenerateCourseAttendanceExcel(OfficeOpenXml.ExcelWorksheet worksheet)
        {
            worksheet.Cells[1, 1].Value = "Course Code";
            worksheet.Cells[1, 2].Value = "Course Name";
            worksheet.Cells[1, 3].Value = "Total Lectures";
            worksheet.Cells[1, 4].Value = "Avg Attendance %";

            var courses = await _context.Courses.ToListAsync();

            int row = 2;
            foreach (var course in courses)
            {
                var lectures = await _context.Lectures
                    .Where(l => l.TimetableRule.TeacherCourse.CourseId == course.Id)
                    .Select(l => l.Id)
                    .ToListAsync();

                var totalRecords = await _context.AttendanceRecords
                    .CountAsync(a => lectures.Contains(a.LectureId));
                var presentRecords = await _context.AttendanceRecords
                    .CountAsync(a => lectures.Contains(a.LectureId) && a.Status == "Present");
                
                var avgAttendance = totalRecords > 0 ? Math.Round((double)presentRecords / totalRecords * 100, 2) : 0;

                worksheet.Cells[row, 1].Value = course.Code;
                worksheet.Cells[row, 2].Value = course.Title;
                worksheet.Cells[row, 3].Value = lectures.Count;
                worksheet.Cells[row, 4].Value = avgAttendance;
                row++;
            }
        }

        private async Task GenerateDefaultersExcel(OfficeOpenXml.ExcelWorksheet worksheet)
        {
            worksheet.Cells[1, 1].Value = "Roll No";
            worksheet.Cells[1, 2].Value = "Student Name";
            worksheet.Cells[1, 3].Value = "Section";
            worksheet.Cells[1, 4].Value = "Attendance %";
            worksheet.Cells[1, 5].Value = "Status";

            var students = await _context.Students
                .Include(s => s.User)
                .Include(s => s.Section)
                .ToListAsync();

            int row = 2;
            foreach (var student in students)
            {
                var totalLectures = await _context.AttendanceRecords.CountAsync(a => a.StudentId == student.Id);
                var present = await _context.AttendanceRecords.CountAsync(a => a.StudentId == student.Id && a.Status == "Present");
                var percentage = totalLectures > 0 ? Math.Round((double)present / totalLectures * 100, 2) : 0;

                if (percentage < 75)
                {
                    worksheet.Cells[row, 1].Value = student.RollNo;
                    worksheet.Cells[row, 2].Value = student.User.FullName;
                    worksheet.Cells[row, 3].Value = student.Section.Name;
                    worksheet.Cells[row, 4].Value = percentage;
                    worksheet.Cells[row, 5].Value = percentage < 50 ? "Critical" : "Warning";
                    row++;
                }
            }
        }

        private async Task GenerateStudentAnalyticsExcel(OfficeOpenXml.ExcelWorksheet worksheet)
        {
            worksheet.Cells[1, 1].Value = "Roll No";
            worksheet.Cells[1, 2].Value = "Student Name";
            worksheet.Cells[1, 3].Value = "Attendance %";
            worksheet.Cells[1, 4].Value = "Performance";

            var students = await _context.Students
                .Include(s => s.User)
                .ToListAsync();

            var studentData = new List<(string RollNo, string Name, double Percentage)>();
            
            foreach (var student in students)
            {
                var totalLectures = await _context.AttendanceRecords.CountAsync(a => a.StudentId == student.Id);
                var present = await _context.AttendanceRecords.CountAsync(a => a.StudentId == student.Id && a.Status == "Present");
                var percentage = totalLectures > 0 ? Math.Round((double)present / totalLectures * 100, 2) : 0;
                studentData.Add((student.RollNo, student.User.FullName, percentage));
            }

            var orderedStudents = studentData.OrderByDescending(s => s.Percentage).ToList();

            int row = 2;
            foreach (var student in orderedStudents)
            {
                var performance = student.Percentage >= 90 ? "Excellent" : 
                                 student.Percentage >= 75 ? "Good" : 
                                 student.Percentage >= 50 ? "Average" : "Poor";

                worksheet.Cells[row, 1].Value = student.RollNo;
                worksheet.Cells[row, 2].Value = student.Name;
                worksheet.Cells[row, 3].Value = student.Percentage;
                worksheet.Cells[row, 4].Value = performance;
                row++;
            }
        }

        private async Task GenerateTeacherPerformanceExcel(OfficeOpenXml.ExcelWorksheet worksheet)
        {
            worksheet.Cells[1, 1].Value = "Teacher Name";
            worksheet.Cells[1, 2].Value = "Marking Rate %";
            worksheet.Cells[1, 3].Value = "Special Sessions";
            worksheet.Cells[1, 4].Value = "Avg Attendance %";

            var teachers = await _context.Teachers
                .Include(t => t.User)
                .ToListAsync();

            int row = 2;
            foreach (var teacher in teachers)
            {
                var totalLectures = await _context.Lectures.CountAsync(l => l.TimetableRule.TeacherCourse.TeacherId == teacher.Id);
                var markedLectures = await _context.Lectures.CountAsync(l => l.TimetableRule.TeacherCourse.TeacherId == teacher.Id && l.AttendanceRecords.Any());
                var markingRate = totalLectures > 0 ? Math.Round((double)markedLectures / totalLectures * 100, 2) : 0;
                
                var specialSessions = await _context.Lectures.CountAsync(l => l.TimetableRule.TeacherCourse.TeacherId == teacher.Id && l.CreatedByTeacherId != null);
                
                var lectureIds = await _context.Lectures.Where(l => l.TimetableRule.TeacherCourse.TeacherId == teacher.Id).Select(l => l.Id).ToListAsync();
                var totalRecords = await _context.AttendanceRecords.CountAsync(a => lectureIds.Contains(a.LectureId));
                var presentRecords = await _context.AttendanceRecords.CountAsync(a => lectureIds.Contains(a.LectureId) && a.Status == "Present");
                var avgAttendance = totalRecords > 0 ? Math.Round((double)presentRecords / totalRecords * 100, 2) : 0;

                worksheet.Cells[row, 1].Value = teacher.User.FullName;
                worksheet.Cells[row, 2].Value = markingRate;
                worksheet.Cells[row, 3].Value = specialSessions;
                worksheet.Cells[row, 4].Value = avgAttendance;
                row++;
            }
        }

        private async Task GenerateCourseStatisticsExcel(OfficeOpenXml.ExcelWorksheet worksheet)
        {
            worksheet.Cells[1, 1].Value = "Course Name";
            worksheet.Cells[1, 2].Value = "Total Lectures";
            worksheet.Cells[1, 3].Value = "Avg Attendance %";
            worksheet.Cells[1, 4].Value = "Rank";

            var courses = await _context.Courses.ToListAsync();
            var courseStats = new List<(string Name, int Lectures, double Attendance)>();

            foreach (var course in courses)
            {
                var lectureIds = await _context.Lectures.Where(l => l.TimetableRule.TeacherCourse.CourseId == course.Id).Select(l => l.Id).ToListAsync();
                var totalRecords = await _context.AttendanceRecords.CountAsync(a => lectureIds.Contains(a.LectureId));
                var presentRecords = await _context.AttendanceRecords.CountAsync(a => lectureIds.Contains(a.LectureId) && a.Status == "Present");
                var avgAttendance = totalRecords > 0 ? Math.Round((double)presentRecords / totalRecords * 100, 2) : 0;

                courseStats.Add((course.Title, lectureIds.Count, avgAttendance));
            }

            var rankedCourses = courseStats.OrderByDescending(c => c.Attendance).ToList();

            int row = 2;
            int rank = 1;
            foreach (var course in rankedCourses)
            {
                worksheet.Cells[row, 1].Value = course.Name;
                worksheet.Cells[row, 2].Value = course.Lectures;
                worksheet.Cells[row, 3].Value = course.Attendance;
                worksheet.Cells[row, 4].Value = rank;
                row++;
                rank++;
            }
        }

        private async Task GenerateSectionComparisonExcel(OfficeOpenXml.ExcelWorksheet worksheet)
        {
            worksheet.Cells[1, 1].Value = "Badge";
            worksheet.Cells[1, 2].Value = "Semester";
            worksheet.Cells[1, 3].Value = "Section";
            worksheet.Cells[1, 4].Value = "Students";
            worksheet.Cells[1, 5].Value = "Avg Attendance %";

            var sections = await _context.Sections
                .Include(s => s.Badge)
                .ToListAsync();

            int row = 2;
            foreach (var section in sections)
            {
                var studentCount = await _context.Students.CountAsync(s => s.SectionId == section.Id);
                var studentIds = await _context.Students.Where(s => s.SectionId == section.Id).Select(s => s.Id).ToListAsync();
                var totalRecords = await _context.AttendanceRecords.CountAsync(a => studentIds.Contains(a.StudentId));
                var presentRecords = await _context.AttendanceRecords.CountAsync(a => studentIds.Contains(a.StudentId) && a.Status == "Present");
                var avgAttendance = totalRecords > 0 ? Math.Round((double)presentRecords / totalRecords * 100, 2) : 0;

                worksheet.Cells[row, 1].Value = section.Badge.Name;
                worksheet.Cells[row, 2].Value = section.Semester;
                worksheet.Cells[row, 3].Value = section.Name;
                worksheet.Cells[row, 4].Value = studentCount;
                worksheet.Cells[row, 5].Value = avgAttendance;
                row++;
            }
        }

        private async Task GenerateAttendanceTrendsExcel(OfficeOpenXml.ExcelWorksheet worksheet)
        {
            worksheet.Cells[1, 1].Value = "Week";
            worksheet.Cells[1, 2].Value = "Date Range";
            worksheet.Cells[1, 3].Value = "Total Records";
            worksheet.Cells[1, 4].Value = "Attendance %";

            var startDate = DateTime.Now.AddMonths(-3);
            var records = await _context.AttendanceRecords
                .Include(a => a.Lecture)
                .Where(a => a.Lecture.StartDateTime.Date >= startDate)
                .ToListAsync();

            var weeklyData = records
                .GroupBy(a => new { Year = a.Lecture.StartDateTime.Year, Week = GetWeekOfYear(a.Lecture.StartDateTime.Date) })
                .Select(g => new
                {
                    Week = $"{g.Key.Year}-W{g.Key.Week}",
                    Total = g.Count(),
                    Present = g.Count(a => a.Status == "Present"),
                    FirstDate = g.Min(a => a.Lecture.StartDateTime.Date),
                    LastDate = g.Max(a => a.Lecture.StartDateTime.Date)
                })
                .OrderBy(x => x.FirstDate)
                .ToList();

            int row = 2;
            foreach (var week in weeklyData)
            {
                var percentage = week.Total > 0 ? Math.Round((double)week.Present / week.Total * 100, 2) : 0;
                worksheet.Cells[row, 1].Value = week.Week;
                worksheet.Cells[row, 2].Value = $"{week.FirstDate:MMM dd} - {week.LastDate:MMM dd}";
                worksheet.Cells[row, 3].Value = week.Total;
                worksheet.Cells[row, 4].Value = percentage;
                row++;
            }
        }

        private async Task GenerateSummaryExcel(OfficeOpenXml.ExcelWorksheet worksheet)
        {
            worksheet.Cells[1, 1].Value = "Metric";
            worksheet.Cells[1, 2].Value = "Value";

            int row = 2;
            worksheet.Cells[row, 1].Value = "Total Students";
            worksheet.Cells[row++, 2].Value = TotalStudents;
            
            worksheet.Cells[row, 1].Value = "Total Teachers";
            worksheet.Cells[row++, 2].Value = TotalTeachers;
            
            worksheet.Cells[row, 1].Value = "Total Courses";
            worksheet.Cells[row++, 2].Value = TotalCourses;
            
            worksheet.Cells[row, 1].Value = "Total Sections";
            worksheet.Cells[row++, 2].Value = TotalSections;
            
            worksheet.Cells[row, 1].Value = "Overall Attendance Rate";
            worksheet.Cells[row++, 2].Value = $"{OverallAttendanceRate}%";
        }

        private async Task GenerateGenericExcel(OfficeOpenXml.ExcelWorksheet worksheet, string reportType)
        {
            worksheet.Cells[1, 1].Value = "Report Type";
            worksheet.Cells[1, 2].Value = reportType;
            worksheet.Cells[2, 1].Value = "Generated";
            worksheet.Cells[2, 2].Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private async Task<string> GenerateReportData(string reportType)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Total Students: {TotalStudents}");
            sb.AppendLine($"Total Teachers: {TotalTeachers}");
            sb.AppendLine($"Total Courses: {TotalCourses}");
            sb.AppendLine($"Total Sections: {TotalSections}");
            sb.AppendLine($"Overall Attendance Rate: {OverallAttendanceRate}%");
            return sb.ToString();
        }

        private int GetWeekOfYear(DateTime date)
        {
            var jan1 = new DateTime(date.Year, 1, 1);
            var daysOffset = (int)System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek - (int)jan1.DayOfWeek;
            var firstWeekDay = jan1.AddDays(daysOffset);
            var cal = System.Globalization.CultureInfo.CurrentCulture.Calendar;
            var weekOfYear = cal.GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            return weekOfYear;
        }
    }
}
