using attendence.Data.Data;
using attendence.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace attendenceProject.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class TeacherDataExportModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public TeacherDataExportModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public attendence.Domain.Entities.Teacher? Teacher { get; set; }
        public List<TeacherCourse> TeacherCourses { get; set; } = new();
        public List<TimetableRule> TimetableRules { get; set; } = new();
        public List<Lecture> Lectures { get; set; } = new();
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(string? email)
        {
            // Default to dominoriderexpense@gmail.com if no email provided
            email ??= "dominoriderexpense@gmail.com";

            // Find the teacher by email
            Teacher = await _context.Teachers
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.User.Email == email);

            if (Teacher == null)
            {
                ErrorMessage = $"No teacher found with email: {email}";
                return Page();
            }

            // Get all TeacherCourses
            TeacherCourses = await _context.TeacherCourses
                .Include(tc => tc.Course)
                .Include(tc => tc.Section)
                    .ThenInclude(s => s.Badge)
                .Include(tc => tc.TimetableRules)
                .Where(tc => tc.TeacherId == Teacher.Id)
                .OrderBy(tc => tc.Course.Code)
                .ToListAsync();

            // Get all TimetableRules
            TimetableRules = await _context.TimetableRules
                .Include(tr => tr.TeacherCourse)
                    .ThenInclude(tc => tc.Course)
                .Include(tr => tr.TeacherCourse)
                    .ThenInclude(tc => tc.Section)
                .Where(tr => tr.TeacherCourse.TeacherId == Teacher.Id)
                .OrderBy(tr => tr.StartDate)
                .ToListAsync();

            // Get all generated Lectures
            Lectures = await _context.Lectures
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Course)
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Section)
                .Where(l => l.TimetableRule.TeacherCourse.TeacherId == Teacher.Id)
                .OrderBy(l => l.StartDateTime)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnGetExportCSVAsync(string? email)
        {
            // Default to dominoriderexpense@gmail.com if no email provided
            email ??= "dominoriderexpense@gmail.com";

            // Find the teacher by email
            var teacher = await _context.Teachers
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.User.Email == email);

            if (teacher == null)
            {
                return NotFound($"No teacher found with email: {email}");
            }

            var csv = new StringBuilder();
            
            // Header
            csv.AppendLine("=== TEACHER INFORMATION ===");
            csv.AppendLine($"Name,{teacher.User.FullName}");
            csv.AppendLine($"Email,{teacher.User.Email}");
            csv.AppendLine($"Teacher ID,{teacher.Id}");
            csv.AppendLine($"Designation,{teacher.Designation ?? "N/A"}");
            csv.AppendLine();

            // TeacherCourses
            csv.AppendLine("=== ASSIGNED COURSES ===");
            csv.AppendLine("TeacherCourse ID,Course Code,Course Name,Section,Badge,Semester");
            
            var teacherCourses = await _context.TeacherCourses
                .Include(tc => tc.Course)
                .Include(tc => tc.Section)
                    .ThenInclude(s => s.Badge)
                .Where(tc => tc.TeacherId == teacher.Id)
                .OrderBy(tc => tc.Course.Code)
                .ToListAsync();

            foreach (var tc in teacherCourses)
            {
                csv.AppendLine($"{tc.Id},{tc.Course.Code},{tc.Course.Title},{tc.Section.Name},{tc.Section.Badge.Name},{tc.Section.Semester}");
            }
            csv.AppendLine();

            // TimetableRules
            csv.AppendLine("=== TIMETABLE RULES ===");
            csv.AppendLine("Rule ID,Course Code,Section,Days of Week,Start Time,Duration (min),Room,Lecture Type,Start Date,End Date");
            
            var timetableRules = await _context.TimetableRules
                .Include(tr => tr.TeacherCourse)
                    .ThenInclude(tc => tc.Course)
                .Include(tr => tr.TeacherCourse)
                    .ThenInclude(tc => tc.Section)
                .Where(tr => tr.TeacherCourse.TeacherId == teacher.Id)
                .OrderBy(tr => tr.StartDate)
                .ToListAsync();

            foreach (var rule in timetableRules)
            {
                csv.AppendLine($"{rule.Id},{rule.TeacherCourse.Course.Code},{rule.TeacherCourse.Section.Name},{rule.DaysOfWeek}," +
                    $"{rule.StartTime:hh\\:mm},{rule.DurationMinutes},{rule.Room ?? "N/A"},{rule.LectureType ?? "N/A"}," +
                    $"{rule.StartDate:yyyy-MM-dd},{rule.EndDate:yyyy-MM-dd}");
            }
            csv.AppendLine();

            // Lectures
            csv.AppendLine("=== GENERATED LECTURES ===");
            csv.AppendLine("Lecture ID,Course Code,Section,Date,Day,Start Time,End Time,Room,Lecture Type");
            
            var lectures = await _context.Lectures
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Course)
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Section)
                .Where(l => l.TimetableRule.TeacherCourse.TeacherId == teacher.Id)
                .OrderBy(l => l.StartDateTime)
                .ToListAsync();

            foreach (var lecture in lectures)
            {
                csv.AppendLine($"{lecture.Id},{lecture.TimetableRule.TeacherCourse.Course.Code}," +
                    $"{lecture.TimetableRule.TeacherCourse.Section.Name},{lecture.StartDateTime:yyyy-MM-dd}," +
                    $"{lecture.StartDateTime:ddd},{lecture.StartDateTime:HH:mm},{lecture.EndDateTime:HH:mm}," +
                    $"{lecture.TimetableRule.Room ?? "N/A"},{lecture.TimetableRule.LectureType ?? "N/A"}");
            }

            csv.AppendLine();
            csv.AppendLine("=== SUMMARY ===");
            csv.AppendLine($"Total Assigned Courses,{teacherCourses.Count}");
            csv.AppendLine($"Total Timetable Rules,{timetableRules.Count}");
            csv.AppendLine($"Total Generated Lectures,{lectures.Count}");

            var fileName = $"teacher_data_{teacher.User.Email.Replace("@", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
        }
    }
}
