using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using attendence.Data.Data;
using System.Security.Claims;

namespace attendenceProject.Pages.Teacher.SpecialSessions
{
    [Authorize(Roles = "Teacher")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<SpecialSessionViewModel> SpecialSessions { get; set; } = new();
        public string? FilterType { get; set; }
        public string? FilterStatus { get; set; }

        public async Task<IActionResult> OnGetAsync(string? type, string? status)
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

            FilterType = type;
            FilterStatus = status;

            var query = _context.Lectures
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Course)
                .Include(l => l.TimetableRule.TeacherCourse.Section)
                    .ThenInclude(s => s.Badge)
                .Include(l => l.AttendanceRecords)
                .Where(l => l.CreatedByTeacherId == teacher.Id);

            // Apply filters
            if (!string.IsNullOrEmpty(type) && type != "all")
            {
                query = query.Where(l => l.LectureType == type);
            }

            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                query = query.Where(l => l.Status == status);
            }

            var lectures = await query
                .OrderByDescending(l => l.StartDateTime)
                .ToListAsync();

            SpecialSessions = lectures.Select(l => new SpecialSessionViewModel
            {
                Id = l.Id,
                LectureType = l.LectureType,
                CourseName = l.TimetableRule.TeacherCourse.Course.Title,
                CourseCode = l.TimetableRule.TeacherCourse.Course.Code,
                SectionName = l.TimetableRule.TeacherCourse.Section.Name,
                BadgeName = l.TimetableRule.TeacherCourse.Section.Badge.Name,
                StartDateTime = l.StartDateTime,
                EndDateTime = l.EndDateTime,
                Status = l.Status,
                Description = l.Description,
                AttendanceMarked = l.AttendanceRecords.Any(),
                TotalStudents = l.AttendanceRecords.Count,
                PresentCount = l.AttendanceRecords.Count(a => a.Status == "Present"),
                CanEdit = l.StartDateTime > DateTime.Now,
                CanDelete = l.StartDateTime > DateTime.Now && !l.AttendanceRecords.Any()
            }).ToList();

            return Page();
        }
    }

    public class SpecialSessionViewModel
    {
        public int Id { get; set; }
        public string LectureType { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public string BadgeName { get; set; } = string.Empty;
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool AttendanceMarked { get; set; }
        public int TotalStudents { get; set; }
        public int PresentCount { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }
}
