using attendence.Data.Data;
using attendence.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace attendenceProject.Pages.Teacher;

[Authorize(Roles = "Teacher")]
public class DashboardModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public DashboardModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public int TeacherId { get; set; }
    public int TotalCourses { get; set; }
    public int TotalSections { get; set; }
    public int TodayLectures { get; set; }
    public int TotalScheduledClasses { get; set; }
    public int MarkedAttendance { get; set; }
    public int PendingAttendance { get; set; }
    public int PendingRequests { get; set; }
    public TimetableRuleViewModel? CurrentOrNextLecture { get; set; }
    public List<TimetableRuleViewModel> UpcomingLectures { get; set; } = new();
    public string? LectureMessage { get; set; }
    public string SelectedFilter { get; set; } = "today";
    public bool IsCurrentLectureOngoing { get; set; }

    public class TimetableRuleViewModel
    {
        public int TimetableRuleId { get; set; }
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public string CourseTitle { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public string? Room { get; set; }
        public string? LectureType { get; set; }
        public string Status { get; set; } = "Scheduled";
        public int? LectureId { get; set; }
        public bool CanMarkAttendance { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
    }

    public async Task OnGetAsync(string? filter = "today")
    {
        SelectedFilter = filter ?? "today";
        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdClaim, out int userId))
        {
            var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.UserId == userId);
            if (teacher != null)
            {
                TeacherId = teacher.Id;

                var teacherCourseIds = await _context.TeacherCourses
                    .Where(tc => tc.TeacherId == TeacherId)
                    .Select(tc => tc.Id)
                    .ToListAsync();

                TotalCourses = await _context.TeacherCourses
                    .Where(tc => tc.TeacherId == TeacherId)
                    .Select(tc => tc.CourseId)
                    .Distinct()
                    .CountAsync();

                TotalSections = await _context.TeacherCourses
                    .Where(tc => tc.TeacherId == TeacherId)
                    .Select(tc => tc.SectionId)
                    .Distinct()
                    .CountAsync();

                var today = DateTime.Today;
                var now = DateTime.Now;
                var currentDayOfWeek = today.DayOfWeek.ToString().Substring(0, 3); // Mon, Tue, Wed, Thu, Fri, Sat, Sun

                // Get all timetable rules for this teacher
                var timetableRules = await _context.TimetableRules
                    .Include(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Course)
                    .Include(tr => tr.TeacherCourse.Section)
                        .ThenInclude(s => s.Badge)
                    .Where(tr => teacherCourseIds.Contains(tr.TeacherCourseId) &&
                                tr.StartDate <= today &&
                                tr.EndDate >= today)
                    .ToListAsync();

                // Calculate today's lectures from timetable rules
                TodayLectures = timetableRules
                    .Where(tr => tr.DaysOfWeek != null && tr.DaysOfWeek.Contains(currentDayOfWeek))
                    .Count();

                // Total scheduled classes per week
                TotalScheduledClasses = timetableRules.Sum(tr =>
                {
                    if (string.IsNullOrEmpty(tr.DaysOfWeek)) return 0;
                    return tr.DaysOfWeek.Split(',').Length;
                });

                // Count marked and pending attendance
                MarkedAttendance = await _context.Lectures
                    .Where(l => teacherCourseIds.Contains(l.TimetableRule.TeacherCourseId) &&
                                l.Status == "Completed")
                    .CountAsync();

                PendingAttendance = await _context.Lectures
                    .Where(l => teacherCourseIds.Contains(l.TimetableRule.TeacherCourseId) &&
                                l.StartDateTime < DateTime.Now &&
                                l.Status == "Scheduled")
                    .CountAsync();

                PendingRequests = await _context.AttendanceEditRequests
                    .Where(r => r.TeacherId == TeacherId && r.Status == "Pending")
                    .CountAsync();

                var extensionRequests = await _context.AttendanceExtensionRequests
                    .Where(r => teacherCourseIds.Contains(r.Lecture.TimetableRule.TeacherCourseId) && r.Status == "Pending")
                    .CountAsync();

                PendingRequests += extensionRequests;

                // Build upcoming lectures from timetable rules
                var upcomingSchedule = new List<TimetableRuleViewModel>();
                var holidays = await _context.Holidays
                    .Where(h => h.Date >= today && h.Date <= today.AddDays(14))
                    .Select(h => h.Date.Date)
                    .ToListAsync();

                foreach (var rule in timetableRules)
                {
                    if (string.IsNullOrEmpty(rule.DaysOfWeek)) continue;

                    var days = rule.DaysOfWeek.Split(',').Select(d => d.Trim()).ToList();
                    
                    // Generate next 14 days of lectures from this rule
                    for (int i = 0; i < 14; i++)
                    {
                        var checkDate = today.AddDays(i);
                        var dayOfWeek = checkDate.DayOfWeek.ToString().Substring(0, 3); // Mon, Tue, Wed, etc.
                        var fullDayName = checkDate.DayOfWeek.ToString(); // Monday, Tuesday, etc.

                        // Match both short (Mon) and full (Monday) day names
                        if ((days.Any(d => d.Equals(dayOfWeek, StringComparison.OrdinalIgnoreCase)) || 
                             days.Any(d => d.Equals(fullDayName, StringComparison.OrdinalIgnoreCase))) && 
                            !holidays.Contains(checkDate.Date))
                        {
                            var lectureStart = checkDate.Date.Add(rule.StartTime);
                            var lectureEnd = lectureStart.AddMinutes(rule.DurationMinutes);

                            // Check if ANY lecture exists for this timetable rule on this DATE
                            // This handles cases where admin edited the lecture time
                            var existingLecture = await _context.Lectures
                                .Include(l => l.AttendanceRecords)
                                .FirstOrDefaultAsync(l => l.TimetableRuleId == rule.Id &&
                                                        l.StartDateTime.Date == checkDate.Date);

                            // If lecture exists but was edited to different time, use the actual lecture time
                            if (existingLecture != null)
                            {
                                lectureStart = existingLecture.StartDateTime;
                                lectureEnd = existingLecture.EndDateTime;
                            }

                            var hasAttendance = existingLecture?.AttendanceRecords.Any() ?? false;
                            var minutesFromStart = (now - lectureStart).TotalMinutes;

                            // Show lectures that are upcoming OR ongoing (even if attendance marked)
                            // Only hide if lecture has ended
                            bool shouldShow = false;
                            if (now < lectureStart) // Upcoming
                            {
                                shouldShow = lectureStart <= now.AddDays(14); // Show next 14 days
                            }
                            else if (now >= lectureStart && now <= lectureEnd) // Ongoing
                            {
                                shouldShow = true; // Always show ongoing lectures
                            }

                            if (shouldShow)
                            {
                                // Determine if can mark/edit based on time and attendance status
                                // Teacher can ONLY mark attendance AFTER lecture starts
                                bool canMarkAttendance = false;
                                string statusMessage = "";

                                if (minutesFromStart < 0)
                                {
                                    // Lecture hasn't started yet
                                    canMarkAttendance = false;
                                    statusMessage = "Lecture not started";
                                }
                                else if (minutesFromStart >= 0 && minutesFromStart <= 10)
                                {
                                    // Lecture started, within 10 minutes - can mark attendance
                                    if (!hasAttendance)
                                    {
                                        canMarkAttendance = true;
                                        statusMessage = $"Mark Attendance ({(10 - minutesFromStart):F0} min left)";
                                    }
                                    else
                                    {
                                        canMarkAttendance = true;
                                        statusMessage = $"Edit Attendance ({(10 - minutesFromStart):F0} min left)";
                                    }
                                }
                                else if (minutesFromStart > 10 && minutesFromStart <= 20)
                                {
                                    // Within 10-20 minute window - can still edit if already marked
                                    if (hasAttendance)
                                    {
                                        canMarkAttendance = true;
                                        statusMessage = $"Edit Attendance ({(20 - minutesFromStart):F0} min left)";
                                    }
                                    else
                                    {
                                        canMarkAttendance = false;
                                        statusMessage = "Attendance window closed - Request admin";
                                    }
                                }
                                else
                                {
                                    // After 20 minutes - locked
                                    canMarkAttendance = false;
                                    statusMessage = "Attendance locked";
                                }

                                upcomingSchedule.Add(new TimetableRuleViewModel
                                {
                                    TimetableRuleId = rule.Id,
                                    StartDateTime = lectureStart,
                                    EndDateTime = lectureEnd,
                                    CourseTitle = rule.TeacherCourse.Course.Title,
                                    CourseCode = rule.TeacherCourse.Course.Code,
                                    SectionName = rule.TeacherCourse.Section.Name,
                                    Room = rule.Room,
                                    LectureType = rule.LectureType,
                                    Status = existingLecture?.Status ?? "Scheduled",
                                    LectureId = existingLecture?.Id,
                                    CanMarkAttendance = canMarkAttendance,
                                    StatusMessage = statusMessage
                                });
                            }
                        }
                    }
                }

                // Sort all lectures
                upcomingSchedule = upcomingSchedule.OrderBy(l => l.StartDateTime).ToList();

                // Get current or next lecture
                CurrentOrNextLecture = upcomingSchedule.FirstOrDefault();
                
                if (CurrentOrNextLecture != null)
                {
                    var minutesFromStart = (now - CurrentOrNextLecture.StartDateTime).TotalMinutes;
                    IsCurrentLectureOngoing = now >= CurrentOrNextLecture.StartDateTime && now <= CurrentOrNextLecture.EndDateTime;
                    
                    // Check if this lecture has attendance
                    var lectureHasAttendance = CurrentOrNextLecture.LectureId.HasValue && 
                        await _context.Lectures
                            .Include(l => l.AttendanceRecords)
                            .Where(l => l.Id == CurrentOrNextLecture.LectureId)
                            .AnyAsync(l => l.AttendanceRecords.Any());
                    
                    if (IsCurrentLectureOngoing)
                    {
                        if (!lectureHasAttendance)
                        {
                            // Attendance not marked yet
                            if (minutesFromStart < 10)
                            {
                                LectureMessage = $"Lecture Started - Wait {(10 - minutesFromStart):F0} min to mark attendance";
                            }
                            else if (minutesFromStart >= 10 && minutesFromStart <= 20)
                            {
                                LectureMessage = $"Mark Attendance Now! ({(20 - minutesFromStart):F0} min left)";
                            }
                            else
                            {
                                LectureMessage = $"Marking Window Closed - Request Extension from Admin";
                            }
                        }
                        else
                        {
                            // Attendance already marked
                            if (minutesFromStart <= 20)
                            {
                                LectureMessage = $"Edit Window Active ({(20 - minutesFromStart):F0} min left)";
                            }
                            else
                            {
                                LectureMessage = $"Attendance Locked - Request to Edit";
                            }
                        }
                    }
                    else if (now < CurrentOrNextLecture.StartDateTime)
                    {
                        var minutesUntil = (CurrentOrNextLecture.StartDateTime - now).TotalMinutes;
                        if (minutesUntil <= 10)
                        {
                            LectureMessage = $"Starting in {Math.Ceiling(minutesUntil)} minutes";
                        }
                        else if (minutesUntil <= 60)
                        {
                            LectureMessage = $"Starting in {Math.Ceiling(minutesUntil)} minutes";
                        }
                        else
                        {
                            LectureMessage = $"Scheduled for {CurrentOrNextLecture.StartDateTime:hh:mm tt}";
                        }
                    }
                }

                // Filter upcoming lectures based on selected filter (exclude current)
                var filteredLectures = upcomingSchedule.Skip(1).AsEnumerable();
                
                // Apply date filter
                switch (SelectedFilter?.ToLower())
                {
                    case "today":
                        filteredLectures = filteredLectures.Where(l => l.StartDateTime.Date == today);
                        break;
                    case "week":
                        var weekEnd = today.AddDays(7);
                        filteredLectures = filteredLectures.Where(l => l.StartDateTime.Date >= today && l.StartDateTime.Date <= weekEnd);
                        break;
                    case "all":
                        // Show all (already filtered to 14 days in the loop above)
                        break;
                    default:
                        filteredLectures = filteredLectures.Where(l => l.StartDateTime.Date == today);
                        break;
                }

                UpcomingLectures = filteredLectures
                    .OrderBy(l => l.StartDateTime)
                    .ToList();
            }
        }
    }
}