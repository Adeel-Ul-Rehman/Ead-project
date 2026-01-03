using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using attendence.Data.Data;
using System.Security.Claims;

namespace attendenceProject.Pages.Teacher.SpecialSessions
{
    [Authorize(Roles = "Teacher")]
    public class DeleteModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public DeleteModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public SessionDeleteInfo SessionInfo { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
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

            var lecture = await _context.Lectures
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Course)
                .Include(l => l.TimetableRule.TeacherCourse.Section)
                    .ThenInclude(s => s.Badge)
                .Include(l => l.AttendanceRecords)
                .FirstOrDefaultAsync(l => l.Id == id && l.CreatedByTeacherId == teacher.Id);

            if (lecture == null)
            {
                TempData["Error"] = "Special session not found or you don't have permission to delete it.";
                return RedirectToPage("./Index");
            }

            // Check if session has already started
            if (lecture.StartDateTime <= DateTime.Now)
            {
                TempData["Error"] = "Cannot delete a session that has already started.";
                return RedirectToPage("./Index");
            }

            // Check if attendance has been marked
            if (lecture.AttendanceRecords.Any())
            {
                TempData["Error"] = "Cannot delete a session with attendance records. Please contact admin if needed.";
                return RedirectToPage("./Index");
            }

            SessionInfo = new SessionDeleteInfo
            {
                Id = lecture.Id,
                LectureType = lecture.LectureType ?? "Session",
                CourseName = lecture.TimetableRule.TeacherCourse.Course.Code + " - " + lecture.TimetableRule.TeacherCourse.Course.Title,
                SectionName = $"{lecture.TimetableRule.TeacherCourse.Section.Badge.Name} - {lecture.TimetableRule.TeacherCourse.Section.Name}",
                StartDateTime = lecture.StartDateTime,
                EndDateTime = lecture.EndDateTime,
                Description = lecture.Description
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
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

            var lecture = await _context.Lectures
                .Include(l => l.AttendanceRecords)
                .FirstOrDefaultAsync(l => l.Id == id && l.CreatedByTeacherId == teacher.Id);

            if (lecture == null)
            {
                TempData["Error"] = "Special session not found or you don't have permission to delete it.";
                return RedirectToPage("./Index");
            }

            // Revalidate before deletion
            if (lecture.StartDateTime <= DateTime.Now)
            {
                TempData["Error"] = "Cannot delete a session that has already started.";
                return RedirectToPage("./Index");
            }

            if (lecture.AttendanceRecords.Any())
            {
                TempData["Error"] = "Cannot delete a session with attendance records.";
                return RedirectToPage("./Index");
            }

            var lectureType = lecture.LectureType ?? "Session";
            _context.Lectures.Remove(lecture);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"{lectureType} deleted successfully.";
            return RedirectToPage("./Index");
        }
    }

    public class SessionDeleteInfo
    {
        public int Id { get; set; }
        public string LectureType { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public string? Description { get; set; }
    }
}
