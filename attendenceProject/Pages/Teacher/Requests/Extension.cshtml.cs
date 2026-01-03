using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using attendence.Data.Data;
using attendence.Domain.Entities;
using System.Security.Claims;

namespace attendenceProject.Pages.Teacher.Requests
{
    [Authorize(Roles = "Teacher")]
    public class ExtensionModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ExtensionModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<LectureOption> MissedLectures { get; set; } = new();
        public List<LectureOption> EditRequestLectures { get; set; } = new();
        public List<ExtensionRequestViewModel> MyRequests { get; set; } = new();

        [BindProperty]
        public int LectureId { get; set; }

        [BindProperty]
        public string RequestType { get; set; } = string.Empty;

        [BindProperty]
        public string Reason { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(int? lectureId)
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

            var teacherCourseIds = await _context.TeacherCourses
                .Where(tc => tc.TeacherId == teacher.Id)
                .Select(tc => tc.Id)
                .ToListAsync();

            var now = DateTime.Now;

            // Get MISSED lectures: past the 10-minute window and NO attendance marked
            var missedLectures = await _context.Lectures
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Course)
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Section)
                .Include(l => l.AttendanceRecords)
                .Where(l => teacherCourseIds.Contains(l.TimetableRule.TeacherCourseId) &&
                           l.StartDateTime.AddMinutes(10) < now && // Past the marking window
                           !l.AttendanceRecords.Any() && // No attendance marked
                           l.StartDateTime >= now.AddDays(-7)) // Only last 7 days
                .OrderByDescending(l => l.StartDateTime)
                .ToListAsync();

            // Get EDIT REQUEST lectures: past the 20-minute window and attendance WAS marked
            var editRequestLectures = await _context.Lectures
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Course)
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Section)
                .Include(l => l.AttendanceRecords)
                .Where(l => teacherCourseIds.Contains(l.TimetableRule.TeacherCourseId) &&
                           l.StartDateTime.AddMinutes(20) < now && // Past the edit window (20 minutes)
                           l.AttendanceRecords.Any() && // Attendance WAS marked
                           l.StartDateTime >= now.AddDays(-7)) // Only last 7 days
                .OrderByDescending(l => l.StartDateTime)
                .ToListAsync();

            var existingRequestLectureIds = await _context.AttendanceExtensionRequests
                .Where(r => r.Status == "Pending" || r.Status == "Approved")
                .Select(r => r.LectureId)
                .ToListAsync();

            MissedLectures = missedLectures
                .Where(l => !existingRequestLectureIds.Contains(l.Id))
                .Select(l => new LectureOption
                {
                    Id = l.Id,
                    Display = $"{l.TimetableRule.TeacherCourse.Course.Title} - {l.TimetableRule.TeacherCourse.Section.Name} - {l.StartDateTime:MMM dd, hh:mm tt}"
                })
                .ToList();

            EditRequestLectures = editRequestLectures
                .Where(l => !existingRequestLectureIds.Contains(l.Id))
                .Select(l => new LectureOption
                {
                    Id = l.Id,
                    Display = $"{l.TimetableRule.TeacherCourse.Course.Title} - {l.TimetableRule.TeacherCourse.Section.Name} - {l.StartDateTime:MMM dd, hh:mm tt}"
                })
                .ToList();

            // Get all requests for this teacher
            var requests = await _context.AttendanceExtensionRequests
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

            MyRequests = requests.Select(r => new ExtensionRequestViewModel
            {
                Id = r.Id,
                CourseName = r.Lecture.TimetableRule.TeacherCourse.Course.Title,
                SectionName = r.Lecture.TimetableRule.TeacherCourse.Section.Name,
                LectureDate = r.Lecture.StartDateTime,
                RequestType = r.RequestType,
                Reason = r.Reason,
                Status = r.Status,
                RequestedAt = r.RequestedAt,
                ApprovedAt = r.ApprovedAt,
                RejectedAt = r.RejectedAt,
                AdminComments = r.AdminNotes ?? "",
                LectureId = r.LectureId
            }).ToList();

            if (lectureId.HasValue)
            {
                LectureId = lectureId.Value;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(Reason) || string.IsNullOrWhiteSpace(RequestType))
            {
                TempData["Error"] = "Please provide all required information for the extension request.";
                return RedirectToPage();
            }

            // Validate RequestType
            if (RequestType != "Missed" && RequestType != "Edit")
            {
                TempData["Error"] = "Invalid request type.";
                return RedirectToPage();
            }

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

            var lecture = await _context.Lectures
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
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

            // Check if already has pending or approved request
            var existingRequest = await _context.AttendanceExtensionRequests
                .Where(r => r.LectureId == LectureId && (r.Status == "Pending" || r.Status == "Approved"))
                .FirstOrDefaultAsync();

            if (existingRequest != null)
            {
                TempData["Error"] = "An extension request for this lecture already exists.";
                return RedirectToPage();
            }

            var request = new AttendanceExtensionRequest
            {
                LectureId = LectureId,
                RequestType = RequestType,
                Reason = Reason,
                Status = "Pending",
                RequestedAt = DateTime.Now,
                TeacherId = teacher.Id
            };

            _context.AttendanceExtensionRequests.Add(request);
            await _context.SaveChangesAsync();

            var requestTypeDisplay = RequestType == "Missed" ? "missed attendance" : "attendance edit";
            TempData["Success"] = $"Extension request for {requestTypeDisplay} submitted successfully. Admin will review it soon.";
            return RedirectToPage();
        }
    }

    public class LectureOption
    {
        public int Id { get; set; }
        public string Display { get; set; } = string.Empty;
    }

    public class ExtensionRequestViewModel
    {
        public int Id { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public DateTime LectureDate { get; set; }
        public string RequestType { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? RejectedAt { get; set; }
        public string? AdminComments { get; set; }
        public int LectureId { get; set; }
    }
}
