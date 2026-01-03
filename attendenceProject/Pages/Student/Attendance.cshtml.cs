using attendence.Data.Data;
using attendence.Domain.Entities;
using attendence.Services.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace attendenceProject.Pages.Student;

[Authorize(Roles = "Student")]
public class AttendanceModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public AttendanceModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public List<AttendanceRecord> AttendanceRecords { get; set; } = new();
    public List<CourseFilterViewModel> AvailableCourses { get; set; } = new();
    public int? SelectedCourseId { get; set; }
    public string? SelectedStatus { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int TotalRecords { get; set; }
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public decimal AttendancePercentage { get; set; }

    public async Task OnGetAsync(int? courseId, string? status, DateTime? fromDate, DateTime? toDate)
    {
        SelectedCourseId = courseId;
        SelectedStatus = status;
        FromDate = fromDate;
        ToDate = toDate;

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdClaim, out int userId))
        {
            var student = await _context.Students
                .Include(s => s.Section)
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (student != null)
            {
                // Get available courses for filter
                AvailableCourses = await _context.TeacherCourses
                    .Include(tc => tc.Course)
                    .Where(tc => tc.SectionId == student.SectionId)
                    .Select(tc => new CourseFilterViewModel
                    {
                        CourseId = tc.CourseId,
                        CourseName = $"{tc.Course.Title} ({tc.Course.Code})"
                    })
                    .Distinct()
                    .ToListAsync();

                // Build query
                var query = _context.AttendanceRecords
                    .Include(ar => ar.Lecture)
                        .ThenInclude(l => l.TimetableRule)
                            .ThenInclude(tr => tr.TeacherCourse)
                                .ThenInclude(tc => tc.Course)
                    .Where(ar => ar.StudentId == student.Id);

                // Apply filters
                if (courseId.HasValue)
                {
                    var lectureIds = await _context.Lectures
                        .Include(l => l.TimetableRule)
                        .Where(l => l.TimetableRule.TeacherCourse.CourseId == courseId.Value &&
                                   l.TimetableRule.TeacherCourse.SectionId == student.SectionId)
                        .Select(l => l.Id)
                        .ToListAsync();

                    query = query.Where(ar => lectureIds.Contains(ar.LectureId));
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(ar => ar.Status == status);
                }

                if (fromDate.HasValue)
                {
                    query = query.Where(ar => ar.Lecture.StartDateTime >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    var endOfDay = toDate.Value.AddDays(1);
                    query = query.Where(ar => ar.Lecture.StartDateTime < endOfDay);
                }

                // Get records
                AttendanceRecords = await query
                    .OrderByDescending(ar => ar.Lecture.StartDateTime)
                    .ToListAsync();

                // Calculate stats
                TotalRecords = AttendanceRecords.Count;
                PresentCount = AttendanceRecords.Count(ar => ar.Status == "Present" || ar.Status == "Late");
                AbsentCount = AttendanceRecords.Count(ar => ar.Status == "Absent");

                if (TotalRecords > 0)
                {
                    AttendancePercentage = Math.Round((decimal)PresentCount / TotalRecords * 100, 2);
                }
            }
        }
    }

}

public class CourseFilterViewModel
{
    public int CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
}
