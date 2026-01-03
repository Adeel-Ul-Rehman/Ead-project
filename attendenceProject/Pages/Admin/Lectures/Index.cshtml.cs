using attendence.Data.Data;
using attendence.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace attendenceProject.Pages.Admin.Lectures
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<Lecture> Lectures { get; set; } = new();
        public List<Course> AllCourses { get; set; } = new();
        public List<Section> AllSections { get; set; } = new();
        public List<attendence.Domain.Entities.Teacher> AllTeachers { get; set; } = new();

        // Filter properties
        public int? CourseFilter { get; set; }
        public int? SectionFilter { get; set; }
        public int? TeacherFilter { get; set; }
        public string StatusFilter { get; set; } = "all";
        public string DateRangeFilter { get; set; } = "all";
        public string? SearchTerm { get; set; }

        // Statistics properties
        public int TotalLectures { get; set; }
        public int TodayLectures { get; set; }
        public int UpcomingLectures { get; set; }
        public int CompletedLectures { get; set; }

        // Calendar data properties
        public List<CalendarWeekDay> CalendarWeekDays { get; set; } = new();
        public DateTime CalendarStartDate { get; set; }
        public DateTime CalendarEndDate { get; set; }

        public async Task OnGetAsync(int? courseId, int? sectionId, int? teacherId, string? status, string? dateRange, string? searchTerm)
        {
            CourseFilter = courseId;
            SectionFilter = sectionId;
            TeacherFilter = teacherId;
            StatusFilter = status ?? "all";
            DateRangeFilter = dateRange ?? "all";
            SearchTerm = searchTerm;

            // Get all courses, sections, and teachers for filters
            AllCourses = await _context.Courses.OrderBy(c => c.Code).ToListAsync();
            AllSections = await _context.Sections
                .Include(s => s.Badge)
                .OrderBy(s => s.Badge.Name)
                .ThenBy(s => s.Name)
                .ToListAsync();
            AllTeachers = await _context.Teachers
                .Include(t => t.User)
                .OrderBy(t => t.User.FullName)
                .ToListAsync();

            // Build query
            var query = _context.Lectures
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Course)
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Section)
                            .ThenInclude(s => s.Badge)
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Teacher)
                            .ThenInclude(t => t.User)
                .AsQueryable();

            // Apply filters
            if (courseId.HasValue)
            {
                query = query.Where(l => l.TimetableRule.TeacherCourse.CourseId == courseId.Value);
            }

            if (sectionId.HasValue)
            {
                query = query.Where(l => l.TimetableRule.TeacherCourse.SectionId == sectionId.Value);
            }

            if (teacherId.HasValue)
            {
                query = query.Where(l => l.TimetableRule.TeacherCourse.TeacherId == teacherId.Value);
            }

            // Status filtering
            var now = DateTime.Now;
            switch (status?.ToLower())
            {
                case "scheduled":
                    query = query.Where(l => l.StartDateTime > now);
                    break;
                case "ongoing":
                    query = query.Where(l => l.StartDateTime <= now && l.EndDateTime >= now);
                    break;
                case "completed":
                    query = query.Where(l => l.EndDateTime < now);
                    break;
            }

            // Date range filters
            var today = DateTime.Today;
            switch (dateRange?.ToLower())
            {
                case "today":
                    query = query.Where(l => l.StartDateTime.Date == today);
                    break;
                case "week":
                    var weekEnd = today.AddDays(7);
                    query = query.Where(l => l.StartDateTime.Date >= today && l.StartDateTime.Date <= weekEnd);
                    break;
                case "month":
                    var monthEnd = today.AddMonths(1);
                    query = query.Where(l => l.StartDateTime.Date >= today && l.StartDateTime.Date <= monthEnd);
                    break;
                case "future":
                    query = query.Where(l => l.StartDateTime >= DateTime.Now);
                    break;
            }

            // Search filter
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(l =>
                    l.TimetableRule.TeacherCourse.Course.Code.Contains(searchTerm) ||
                    l.TimetableRule.TeacherCourse.Course.Title.Contains(searchTerm) ||
                    l.TimetableRule.TeacherCourse.Teacher.User.FullName.Contains(searchTerm));
            }

            // Get all lectures
            var allLectures = await query.ToListAsync();
            Lectures = allLectures.OrderBy(l => l.StartDateTime).ToList();

            // Calculate statistics
            TotalLectures = allLectures.Count;
            TodayLectures = allLectures.Count(l => l.StartDateTime.Date == today);
            UpcomingLectures = allLectures.Count(l => l.StartDateTime > now);
            CompletedLectures = allLectures.Count(l => l.EndDateTime < now);

            // Build calendar data for week view (current week)
            BuildCalendarWeekView(allLectures);
        }

        private void BuildCalendarWeekView(List<Lecture> lectures)
        {
            // Get current week (Monday to Sunday)
            var today = DateTime.Today;
            var dayOfWeek = (int)today.DayOfWeek;
            var diff = dayOfWeek == 0 ? -6 : 1 - dayOfWeek; // Adjust so Monday is first day
            CalendarStartDate = today.AddDays(diff);
            CalendarEndDate = CalendarStartDate.AddDays(6);

            CalendarWeekDays = new List<CalendarWeekDay>();

            for (int i = 0; i < 7; i++)
            {
                var date = CalendarStartDate.AddDays(i);
                var dayLectures = lectures
                    .Where(l => l.StartDateTime.Date == date)
                    .OrderBy(l => l.StartDateTime)
                    .Select(l => new CalendarLecture
                    {
                        Id = l.Id,
                        StartTime = l.StartDateTime,
                        EndTime = l.EndDateTime,
                        CourseCode = l.TimetableRule.TeacherCourse.Course.Code,
                        CourseTitle = l.TimetableRule.TeacherCourse.Course.Title,
                        TeacherName = l.TimetableRule.TeacherCourse.Teacher.User.FullName,
                        Room = l.TimetableRule.Room ?? "N/A",
                        Status = l.EndDateTime < DateTime.Now ? "completed" : 
                                (l.StartDateTime <= DateTime.Now && l.EndDateTime >= DateTime.Now ? "ongoing" : "scheduled")
                    })
                    .ToList();

                CalendarWeekDays.Add(new CalendarWeekDay
                {
                    Date = date,
                    DayName = date.ToString("dddd"),
                    DayNumber = date.Day,
                    IsToday = date.Date == DateTime.Today,
                    Lectures = dayLectures
                });
            }
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var lecture = await _context.Lectures
                .Include(l => l.AttendanceRecords)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (lecture == null)
            {
                TempData["Error"] = "Lecture not found.";
                return RedirectToPage();
            }

            // Check if attendance has been marked
            if (lecture.AttendanceRecords.Any())
            {
                TempData["Error"] = $"Cannot delete lecture - {lecture.AttendanceRecords.Count} attendance records exist.";
                return RedirectToPage();
            }

            _context.Lectures.Remove(lecture);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Lecture deleted successfully.";
            return RedirectToPage();
        }
    }

    // Helper classes for calendar view
    public class CalendarWeekDay
    {
        public DateTime Date { get; set; }
        public string DayName { get; set; } = string.Empty;
        public int DayNumber { get; set; }
        public bool IsToday { get; set; }
        public List<CalendarLecture> Lectures { get; set; } = new();
    }

    public class CalendarLecture
    {
        public int Id { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string CourseCode { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
