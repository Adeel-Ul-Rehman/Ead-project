using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using attendence.Data.Data;
using System.Text;
using System.Security.Claims;
using OfficeOpenXml;

namespace attendenceProject.Pages.Teacher
{
    [Authorize(Roles = "Teacher")]
    public class ReportsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ReportsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<CourseStats> CourseStatistics { get; set; } = new();
        public List<SectionStats> SectionStatistics { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var teacher = await _context.Teachers
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (teacher == null)
            {
                return Unauthorized();
            }

            var teacherCourses = await _context.TeacherCourses
                .Include(tc => tc.Course)
                .Include(tc => tc.Section)
                    .ThenInclude(s => s.Badge)
                .Where(tc => tc.TeacherId == teacher.Id)
                .ToListAsync();

            foreach (var tc in teacherCourses)
            {
                var lectures = await _context.Lectures
                    .Include(l => l.AttendanceRecords)
                    .Where(l => l.TimetableRule.TeacherCourseId == tc.Id)
                    .ToListAsync();

                var totalLectures = lectures.Count;
                var completedLectures = lectures.Count(l => l.AttendanceRecords.Any());
                var totalStudents = await _context.Students.CountAsync(s => s.SectionId == tc.SectionId);

                // Debug output
                Console.WriteLine($"Course: {tc.Course.Title}, Total Lectures: {totalLectures}, Completed: {completedLectures}, Students: {totalStudents}");

                if (completedLectures > 0 && totalStudents > 0)
                {
                    var totalPresent = lectures.SelectMany(l => l.AttendanceRecords).Count(a => a.Status == "Present");
                    var totalAbsent = lectures.SelectMany(l => l.AttendanceRecords).Count(a => a.Status == "Absent");
                    var totalLate = lectures.SelectMany(l => l.AttendanceRecords).Count(a => a.Status == "Late");
                    var totalExcused = lectures.SelectMany(l => l.AttendanceRecords).Count(a => a.Status == "Excused");

                    var totalPossibleAttendances = completedLectures * totalStudents;
                    var attendancePercentage = totalPossibleAttendances > 0 ? 
                        (double)totalPresent / totalPossibleAttendances * 100 : 0;

                    // Ensure the percentage is valid (not infinity or NaN)
                    if (double.IsFinite(attendancePercentage))
                    {
                        Console.WriteLine($"Adding course stat: {tc.Course.Title} - Attendance: {attendancePercentage:F2}%");
                        CourseStatistics.Add(new CourseStats
                        {
                            CourseName = tc.Course.Title,
                            SectionName = tc.Section.Name,
                            BadgeName = tc.Section.Badge.Name,
                            TotalLectures = totalLectures,
                            CompletedLectures = completedLectures,
                            TotalStudents = totalStudents,
                            TotalPresent = totalPresent,
                            TotalAbsent = totalAbsent,
                            TotalLate = totalLate,
                            TotalExcused = totalExcused,
                            AttendancePercentage = attendancePercentage
                        });
                    }
                    else
                    {
                        Console.WriteLine($"Skipping course {tc.Course.Title} - Invalid percentage: {attendancePercentage}");
                    }
                }
                else
                {
                    Console.WriteLine($"Skipping course {tc.Course.Title} - No completed lectures or no students");
                }
            }

            Console.WriteLine($"Total CourseStatistics added: {CourseStatistics.Count}");

            // Section-wise statistics
            var sections = teacherCourses.Select(tc => tc.Section).Distinct().ToList();
            foreach (var section in sections)
            {
                var sectionCourses = teacherCourses.Where(tc => tc.SectionId == section.Id).ToList();
                var allLectures = await _context.Lectures
                    .Include(l => l.AttendanceRecords)
                    .Where(l => sectionCourses.Select(tc => tc.Id).Contains(l.TimetableRule.TeacherCourseId))
                    .ToListAsync();

                var completedLectures = allLectures.Count(l => l.AttendanceRecords.Any());
                var totalStudents = await _context.Students.CountAsync(s => s.SectionId == section.Id);
                
                if (completedLectures > 0 && totalStudents > 0)
                {
                    var totalPresentRecords = allLectures.SelectMany(l => l.AttendanceRecords).Count(a => a.Status == "Present");
                    var totalPossibleAttendances = completedLectures * totalStudents;
                    
                    var averageAttendance = totalPossibleAttendances > 0 ? 
                        (double)totalPresentRecords / totalPossibleAttendances * 100 : 0;

                    // Ensure the percentage is valid (not infinity or NaN)
                    if (double.IsFinite(averageAttendance))
                    {
                        SectionStatistics.Add(new SectionStats
                        {
                            SectionName = section.Name,
                            BadgeName = section.Badge.Name,
                            Semester = section.Semester,
                            TotalCourses = sectionCourses.Count,
                            TotalLectures = allLectures.Count,
                            CompletedLectures = completedLectures,
                            AverageAttendance = averageAttendance
                        });
                    }
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnGetDownloadCourseReportAsync(int courseId)
        {
            // Generate CSV report
            var csv = new StringBuilder();
            csv.AppendLine("Course Attendance Report");
            // Add CSV data...
            
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "course-report.csv");
        }

        public async Task<IActionResult> OnGetExportToExcelAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var teacher = await _context.Teachers
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (teacher == null)
            {
                return Unauthorized();
            }

            // Reload statistics
            await LoadTeacherStatistics(teacher.Id);

            // Set EPPlus license context (Required for EPPlus 8)
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
            
            using var package = new ExcelPackage();

            // Course Statistics Sheet
            var courseSheet = package.Workbook.Worksheets.Add("Course Statistics");
            
            // Headers
            courseSheet.Cells[1, 1].Value = "Course";
            courseSheet.Cells[1, 2].Value = "Section";
            courseSheet.Cells[1, 3].Value = "Badge";
            courseSheet.Cells[1, 4].Value = "Total Lectures";
            courseSheet.Cells[1, 5].Value = "Completed Lectures";
            courseSheet.Cells[1, 6].Value = "Total Students";
            courseSheet.Cells[1, 7].Value = "Present";
            courseSheet.Cells[1, 8].Value = "Absent";
            courseSheet.Cells[1, 9].Value = "Late";
            courseSheet.Cells[1, 10].Value = "Excused";
            courseSheet.Cells[1, 11].Value = "Attendance %";

            // Style headers
            using (var range = courseSheet.Cells[1, 1, 1, 11])
            {
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(79, 70, 229)); // Indigo
                range.Style.Font.Color.SetColor(System.Drawing.Color.White);
            }

            // Data rows
            int row = 2;
            foreach (var course in CourseStatistics)
            {
                courseSheet.Cells[row, 1].Value = course.CourseName;
                courseSheet.Cells[row, 2].Value = course.SectionName;
                courseSheet.Cells[row, 3].Value = course.BadgeName;
                courseSheet.Cells[row, 4].Value = course.TotalLectures;
                courseSheet.Cells[row, 5].Value = course.CompletedLectures;
                courseSheet.Cells[row, 6].Value = course.TotalStudents;
                courseSheet.Cells[row, 7].Value = course.TotalPresent;
                courseSheet.Cells[row, 8].Value = course.TotalAbsent;
                courseSheet.Cells[row, 9].Value = course.TotalLate;
                courseSheet.Cells[row, 10].Value = course.TotalExcused;
                courseSheet.Cells[row, 11].Value = course.AttendancePercentage;
                courseSheet.Cells[row, 11].Style.Numberformat.Format = "0.00";
                
                // Color code attendance percentage
                if (course.AttendancePercentage >= 75)
                {
                    courseSheet.Cells[row, 11].Style.Font.Color.SetColor(System.Drawing.Color.Green);
                }
                else if (course.AttendancePercentage >= 50)
                {
                    courseSheet.Cells[row, 11].Style.Font.Color.SetColor(System.Drawing.Color.Orange);
                }
                else
                {
                    courseSheet.Cells[row, 11].Style.Font.Color.SetColor(System.Drawing.Color.Red);
                }
                
                row++;
            }

            courseSheet.Cells.AutoFitColumns();

            // Section Statistics Sheet
            var sectionSheet = package.Workbook.Worksheets.Add("Section Statistics");
            
            // Headers
            sectionSheet.Cells[1, 1].Value = "Section";
            sectionSheet.Cells[1, 2].Value = "Badge";
            sectionSheet.Cells[1, 3].Value = "Semester";
            sectionSheet.Cells[1, 4].Value = "Total Courses";
            sectionSheet.Cells[1, 5].Value = "Total Lectures";
            sectionSheet.Cells[1, 6].Value = "Completed Lectures";
            sectionSheet.Cells[1, 7].Value = "Average Attendance %";

            // Style headers
            using (var range = sectionSheet.Cells[1, 1, 1, 7])
            {
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(168, 85, 247)); // Purple
                range.Style.Font.Color.SetColor(System.Drawing.Color.White);
            }

            // Data rows
            row = 2;
            foreach (var section in SectionStatistics)
            {
                sectionSheet.Cells[row, 1].Value = section.SectionName;
                sectionSheet.Cells[row, 2].Value = section.BadgeName;
                sectionSheet.Cells[row, 3].Value = section.Semester;
                sectionSheet.Cells[row, 4].Value = section.TotalCourses;
                sectionSheet.Cells[row, 5].Value = section.TotalLectures;
                sectionSheet.Cells[row, 6].Value = section.CompletedLectures;
                sectionSheet.Cells[row, 7].Value = section.AverageAttendance;
                sectionSheet.Cells[row, 7].Style.Numberformat.Format = "0.00";
                
                // Color code attendance percentage
                if (section.AverageAttendance >= 75)
                {
                    sectionSheet.Cells[row, 7].Style.Font.Color.SetColor(System.Drawing.Color.Green);
                }
                else if (section.AverageAttendance >= 50)
                {
                    sectionSheet.Cells[row, 7].Style.Font.Color.SetColor(System.Drawing.Color.Orange);
                }
                else
                {
                    sectionSheet.Cells[row, 7].Style.Font.Color.SetColor(System.Drawing.Color.Red);
                }
                
                row++;
            }

            sectionSheet.Cells.AutoFitColumns();

            var fileName = $"Teacher_Reports_{teacher.User.FullName}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            var fileBytes = package.GetAsByteArray();
            
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        private async Task LoadTeacherStatistics(int teacherId)
        {
            var teacherCourses = await _context.TeacherCourses
                .Include(tc => tc.Course)
                .Include(tc => tc.Section)
                    .ThenInclude(s => s.Badge)
                .Where(tc => tc.TeacherId == teacherId)
                .ToListAsync();

            CourseStatistics.Clear();
            foreach (var tc in teacherCourses)
            {
                var lectures = await _context.Lectures
                    .Include(l => l.AttendanceRecords)
                    .Where(l => l.TimetableRule.TeacherCourseId == tc.Id)
                    .ToListAsync();

                var totalLectures = lectures.Count;
                var completedLectures = lectures.Count(l => l.AttendanceRecords.Any());
                var totalStudents = await _context.Students.CountAsync(s => s.SectionId == tc.SectionId);

                if (completedLectures > 0 && totalStudents > 0)
                {
                    var totalPresent = lectures.SelectMany(l => l.AttendanceRecords).Count(a => a.Status == "Present");
                    var totalAbsent = lectures.SelectMany(l => l.AttendanceRecords).Count(a => a.Status == "Absent");
                    var totalLate = lectures.SelectMany(l => l.AttendanceRecords).Count(a => a.Status == "Late");
                    var totalExcused = lectures.SelectMany(l => l.AttendanceRecords).Count(a => a.Status == "Excused");

                    var totalPossibleAttendances = completedLectures * totalStudents;
                    var attendancePercentage = totalPossibleAttendances > 0 ? 
                        (double)totalPresent / totalPossibleAttendances * 100 : 0;

                    if (double.IsFinite(attendancePercentage))
                    {
                        CourseStatistics.Add(new CourseStats
                        {
                            CourseName = tc.Course.Title,
                            SectionName = tc.Section.Name,
                            BadgeName = tc.Section.Badge.Name,
                            TotalLectures = totalLectures,
                            CompletedLectures = completedLectures,
                            TotalStudents = totalStudents,
                            TotalPresent = totalPresent,
                            TotalAbsent = totalAbsent,
                            TotalLate = totalLate,
                            TotalExcused = totalExcused,
                            AttendancePercentage = attendancePercentage
                        });
                    }
                }
            }

            // Section-wise statistics
            var sections = teacherCourses.Select(tc => tc.Section).Distinct().ToList();
            SectionStatistics.Clear();
            foreach (var section in sections)
            {
                var sectionCourses = teacherCourses.Where(tc => tc.SectionId == section.Id).ToList();
                var allLectures = await _context.Lectures
                    .Include(l => l.AttendanceRecords)
                    .Where(l => sectionCourses.Select(tc => tc.Id).Contains(l.TimetableRule.TeacherCourseId))
                    .ToListAsync();

                var completedLectures = allLectures.Count(l => l.AttendanceRecords.Any());
                var totalStudents = await _context.Students.CountAsync(s => s.SectionId == section.Id);
                
                if (completedLectures > 0 && totalStudents > 0)
                {
                    var totalPresentRecords = allLectures.SelectMany(l => l.AttendanceRecords).Count(a => a.Status == "Present");
                    var totalPossibleAttendances = completedLectures * totalStudents;
                    
                    var averageAttendance = totalPossibleAttendances > 0 ? 
                        (double)totalPresentRecords / totalPossibleAttendances * 100 : 0;

                    if (double.IsFinite(averageAttendance))
                    {
                        SectionStatistics.Add(new SectionStats
                        {
                            SectionName = section.Name,
                            BadgeName = section.Badge.Name,
                            Semester = section.Semester,
                            TotalCourses = sectionCourses.Count,
                            TotalLectures = allLectures.Count,
                            CompletedLectures = completedLectures,
                            AverageAttendance = averageAttendance
                        });
                    }
                }
            }
        }
    }

    public class CourseStats
    {
        public string CourseName { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public string BadgeName { get; set; } = string.Empty;
        public int TotalLectures { get; set; }
        public int CompletedLectures { get; set; }
        public int TotalStudents { get; set; }
        public int TotalPresent { get; set; }
        public int TotalAbsent { get; set; }
        public int TotalLate { get; set; }
        public int TotalExcused { get; set; }
        public double AttendancePercentage { get; set; }
    }

    public class SectionStats
    {
        public string SectionName { get; set; } = string.Empty;
        public string BadgeName { get; set; } = string.Empty;
        public int Semester { get; set; }
        public int TotalCourses { get; set; }
        public int TotalLectures { get; set; }
        public int CompletedLectures { get; set; }
        public double AverageAttendance { get; set; }
    }
}
