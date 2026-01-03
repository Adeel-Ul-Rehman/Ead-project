using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using attendence.Data.Data;
using attendence.Domain.Entities;

namespace attendenceProject.Pages.Teacher.Requests
{
    [Authorize(Roles = "Teacher")]
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public EditModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<LectureOption> CompletedLectures { get; set; } = new();
        public List<EditRequestViewModel> MyRequests { get; set; } = new();

        [BindProperty]
        public int LectureId { get; set; }

        [BindProperty]
        public string Reason { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            var teacherEmail = User.Identity?.Name;
            if (string.IsNullOrEmpty(teacherEmail))
            {
                return Unauthorized();
            }

            var teacher = await _context.Teachers
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.User.Email == teacherEmail);

            if (teacher == null)
            {
                return Unauthorized();
            }

            var teacherCourseIds = await _context.TeacherCourses
                .Where(tc => tc.TeacherId == teacher.Id)
                .Select(tc => tc.Id)
                .ToListAsync();

            // Get completed lectures (with attendance marked) from last 7 days
            var completedLectures = await _context.Lectures
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Course)
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Section)
                .Include(l => l.AttendanceRecords)
                .Where(l => teacherCourseIds.Contains(l.TimetableRule.TeacherCourseId) &&
                           l.AttendanceRecords.Any() &&
                           l.StartDateTime >= DateTime.Now.AddDays(-7))
                .OrderByDescending(l => l.StartDateTime)
                .ToListAsync();

            var existingRequestLectureIds = await _context.AttendanceEditRequests
                .Where(r => r.Status == "Pending")
                .Select(r => r.LectureId)
                .ToListAsync();

            CompletedLectures = completedLectures
                .Where(l => !existingRequestLectureIds.Contains(l.Id))
                .Select(l => new LectureOption
                {
                    Id = l.Id,
                    Display = $"{l.TimetableRule.TeacherCourse.Course.Title} - {l.TimetableRule.TeacherCourse.Section.Name} - {l.StartDateTime:MMM dd, hh:mm tt}"
                })
                .ToList();

            // Get all edit requests for this teacher
            var requests = await _context.AttendanceEditRequests
                .Include(r => r.Lecture)
                    .ThenInclude(l => l.TimetableRule)
                        .ThenInclude(tr => tr.TeacherCourse)
                            .ThenInclude(tc => tc.Course)
                .Include(r => r.Lecture)
                    .ThenInclude(l => l.TimetableRule)
                        .ThenInclude(tr => tr.TeacherCourse)
                            .ThenInclude(tc => tc.Section)
                .Where(r => teacherCourseIds.Contains(r.Lecture.TimetableRule.TeacherCourseId))
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();

            MyRequests = requests.Select(r => new EditRequestViewModel
            {
                Id = r.Id,
                CourseName = r.Lecture.TimetableRule.TeacherCourse.Course.Title,
                SectionName = r.Lecture.TimetableRule.TeacherCourse.Section.Name,
                LectureDate = r.Lecture.StartDateTime,
                Reason = r.Reason,
                Status = r.Status,
                RequestedAt = r.RequestedAt,
                ApprovedAt = r.ReviewedAt,
                RejectedAt = r.ReviewedAt,
                AdminComments = "",
                LectureId = r.LectureId
            }).ToList();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(Reason))
            {
                TempData["Error"] = "Please provide a valid reason for the edit request.";
                return RedirectToPage();
            }

            var teacherEmail = User.Identity?.Name;
            if (string.IsNullOrEmpty(teacherEmail))
            {
                return Unauthorized();
            }

            var teacher = await _context.Teachers
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.User.Email == teacherEmail);

            if (teacher == null)
            {
                return Unauthorized();
            }

            var lecture = await _context.Lectures
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                .Include(l => l.AttendanceRecords)
                .FirstOrDefaultAsync(l => l.Id == LectureId);

            if (lecture == null)
            {
                TempData["Error"] = "Lecture not found.";
                return RedirectToPage();
            }

            if (lecture.TimetableRule.TeacherCourse.TeacherId != teacher.Id)
            {
                return Forbid();
            }

            if (!lecture.AttendanceRecords.Any())
            {
                TempData["Error"] = "Cannot request edit for lecture without attendance records.";
                return RedirectToPage();
            }

            // Check if already has pending request
            var existingRequest = await _context.AttendanceEditRequests
                .Where(r => r.LectureId == LectureId && r.Status == "Pending")
                .FirstOrDefaultAsync();

            if (existingRequest != null)
            {
                TempData["Error"] = "A pending edit request for this lecture already exists.";
                return RedirectToPage();
            }

            var request = new AttendanceEditRequest
            {
                LectureId = LectureId,
                Reason = Reason,
                Status = "Pending",
                RequestedAt = DateTime.Now
            };

            _context.AttendanceEditRequests.Add(request);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Edit request submitted successfully. Admin will review it soon.";
            return RedirectToPage();
        }
    }

    public class EditRequestViewModel
    {
        public int Id { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public DateTime LectureDate { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? RejectedAt { get; set; }
        public string? AdminComments { get; set; }
        public int LectureId { get; set; }
    }
}
