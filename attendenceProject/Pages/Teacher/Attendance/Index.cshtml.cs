using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using attendence.Data.Data;
using attendence.Domain.Entities;
using System.Security.Claims;

namespace attendenceProject.Pages.Teacher.Attendance
{
    [Authorize(Roles = "Teacher")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<LectureViewModel> TodayLectures { get; set; } = new();
        public List<LectureViewModel> UpcomingLectures { get; set; } = new();
        public List<LectureViewModel> CompletedLectures { get; set; } = new();

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

            var teacherCourseIds = await _context.TeacherCourses
                .Where(tc => tc.TeacherId == teacher.Id)
                .Select(tc => tc.Id)
                .ToListAsync();

            var today = DateTime.Today;
            var now = DateTime.Now;

            // Get timetable rules for this teacher
            var timetableRules = await _context.TimetableRules
                .Include(tr => tr.TeacherCourse)
                    .ThenInclude(tc => tc.Course)
                .Include(tr => tr.TeacherCourse.Section)
                .Where(tr => teacherCourseIds.Contains(tr.TeacherCourseId))
                .ToListAsync();

            // Get holidays
            var holidays = await _context.Holidays
                .Where(h => h.Date >= today.AddDays(-30) && h.Date <= today.AddDays(30))
                .Select(h => h.Date.Date)
                .ToListAsync();

            // Generate schedule from timetable rules
            var allScheduledLectures = new List<LectureViewModel>();
            var processedLectureIds = new HashSet<int>(); // Track which lecture IDs we've already added

            foreach (var rule in timetableRules)
            {
                if (string.IsNullOrEmpty(rule.DaysOfWeek)) continue;

                var days = rule.DaysOfWeek.Split(',').Select(d => d.Trim()).ToList();

                // Generate lectures for past 30 days and next 30 days
                for (int i = -30; i <= 30; i++)
                {
                    var checkDate = today.AddDays(i);
                    
                    // CRITICAL: Only include lectures within the timetable rule's tenure (StartDate to EndDate)
                    if (checkDate < rule.StartDate.Date || checkDate > rule.EndDate.Date)
                    {
                        continue; // Skip dates outside tenure
                    }
                    
                    var dayOfWeek = checkDate.DayOfWeek.ToString().Substring(0, 3); // Mon, Tue, Wed, etc.
                    var fullDayName = checkDate.DayOfWeek.ToString(); // Monday, Tuesday, etc.

                    // Match both short (Mon) and full (Monday) day names
                    if ((days.Any(d => d.Equals(dayOfWeek, StringComparison.OrdinalIgnoreCase)) || 
                         days.Any(d => d.Equals(fullDayName, StringComparison.OrdinalIgnoreCase))) && 
                        !holidays.Contains(checkDate.Date))
                    {
                        // Check if ANY lecture exists for this timetable rule on this DATE
                        // This handles cases where admin edited the lecture time
                        var existingLecture = await _context.Lectures
                            .Include(l => l.AttendanceRecords)
                            .FirstOrDefaultAsync(l => l.TimetableRuleId == rule.Id &&
                                                    l.StartDateTime.Date == checkDate.Date);

                        // If a lecture exists but with different time (edited), use the actual lecture time
                        if (existingLecture != null)
                        {
                            // Only add if we haven't already processed this lecture
                            if (!processedLectureIds.Contains(existingLecture.Id))
                            {
                                var viewModel = MapToViewModelFromLecture(existingLecture, now);
                                allScheduledLectures.Add(viewModel);
                                processedLectureIds.Add(existingLecture.Id);
                            }
                        }
                        else
                        {
                            // No lecture exists yet, show scheduled time from timetable rule
                            var lectureStart = checkDate.Date.Add(rule.StartTime);
                            var lectureEnd = lectureStart.AddMinutes(rule.DurationMinutes);
                            var viewModel = MapToViewModel(rule, lectureStart, lectureEnd, null, now);
                            allScheduledLectures.Add(viewModel);
                        }
                    }
                }
            }

            // Also include manually created lectures that don't match any timetable rule schedule
            var manualLectures = await _context.Lectures
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Course)
                .Include(l => l.TimetableRule.TeacherCourse.Section)
                .Include(l => l.AttendanceRecords)
                .Where(l => teacherCourseIds.Contains(l.TimetableRule.TeacherCourseId))
                .ToListAsync();

            // Add manual lectures that aren't already processed and are within tenure
            foreach (var lecture in manualLectures)
            {
                // Check if lecture is within its timetable rule's tenure
                var lectureDate = lecture.StartDateTime.Date;
                if (lectureDate >= lecture.TimetableRule.StartDate.Date && 
                    lectureDate <= lecture.TimetableRule.EndDate.Date &&
                    !processedLectureIds.Contains(lecture.Id)) // Only add if not already processed
                {
                    var viewModel = MapToViewModelFromLecture(lecture, now);
                    allScheduledLectures.Add(viewModel);
                    processedLectureIds.Add(lecture.Id);
                }
            }

            // Sort and categorize
            TodayLectures = allScheduledLectures
                .Where(l => l.StartDateTime.Date == today)
                .OrderBy(l => l.StartDateTime)
                .ToList();

            UpcomingLectures = allScheduledLectures
                .Where(l => l.StartDateTime.Date > today)
                .OrderBy(l => l.StartDateTime)
                .Take(10)
                .ToList();

            CompletedLectures = allScheduledLectures
                .Where(l => l.StartDateTime.Date < today || (l.StartDateTime.Date == today && l.HasAttendance))
                .OrderByDescending(l => l.StartDateTime)
                .Take(20)
                .ToList();

            return Page();
        }

        private LectureViewModel MapToViewModel(TimetableRule rule, DateTime startDateTime, DateTime endDateTime, Lecture? existingLecture, DateTime now)
        {
            var minutesFromStart = (now - startDateTime).TotalMinutes;
            var hasAttendance = existingLecture?.AttendanceRecords.Any() ?? false;

            string status;
            bool canMark;

            // Determine status and marking ability
            if (minutesFromStart < 0)
            {
                // Lecture hasn't started yet
                status = "Scheduled";
                canMark = false;
            }
            else if (minutesFromStart >= 0 && minutesFromStart <= 10)
            {
                // Within first 10 minutes - can mark
                if (!hasAttendance)
                {
                    status = "Ongoing";
                    canMark = true;
                }
                else
                {
                    status = "Ongoing";
                    canMark = true; // Can edit
                }
            }
            else if (minutesFromStart > 10 && minutesFromStart <= 20)
            {
                // 10-20 minute window - can only edit if already marked
                if (hasAttendance)
                {
                    status = "Ongoing";
                    canMark = true;
                }
                else
                {
                    status = "Missed";
                    canMark = false;
                }
            }
            else
            {
                // After 20 minutes - locked
                if (hasAttendance)
                {
                    status = "Completed";
                }
                else
                {
                    status = "Missed";
                }
                canMark = false;
            }

            return new LectureViewModel
            {
                TimetableRuleId = rule.Id,
                LectureId = existingLecture?.Id,
                StartDateTime = startDateTime,
                EndDateTime = endDateTime,
                CourseCode = rule.TeacherCourse.Course.Code,
                CourseTitle = rule.TeacherCourse.Course.Title,
                SectionName = rule.TeacherCourse.Section.Name,
                Room = rule.Room,
                LectureType = rule.LectureType,
                Status = status,
                CanMark = canMark,
                HasAttendance = hasAttendance,
                TotalStudents = 0,
                PresentStudents = existingLecture?.AttendanceRecords.Count(ar => ar.Status == "Present") ?? 0,
                MinutesFromStart = minutesFromStart
            };
        }

        private LectureViewModel MapToViewModelFromLecture(Lecture lecture, DateTime now)
        {
            var minutesFromStart = (now - lecture.StartDateTime).TotalMinutes;
            var hasAttendance = lecture.AttendanceRecords.Any();

            string status;
            bool canMark;

            // Determine status and marking ability
            if (minutesFromStart < 0)
            {
                // Lecture hasn't started yet
                status = "Scheduled";
                canMark = false;
            }
            else if (minutesFromStart >= 0 && minutesFromStart <= 10)
            {
                // Within first 10 minutes - can mark
                if (!hasAttendance)
                {
                    status = "Ongoing";
                    canMark = true;
                }
                else
                {
                    status = "Ongoing";
                    canMark = true; // Can edit
                }
            }
            else if (minutesFromStart > 10 && minutesFromStart <= 20)
            {
                // 10-20 minute window - can only edit if already marked
                if (hasAttendance)
                {
                    status = "Ongoing";
                    canMark = true;
                }
                else
                {
                    status = "Missed";
                    canMark = false;
                }
            }
            else
            {
                // After 20 minutes - locked
                if (hasAttendance)
                {
                    status = "Completed";
                }
                else
                {
                    status = "Missed";
                }
                canMark = false;
            }

            return new LectureViewModel
            {
                TimetableRuleId = lecture.TimetableRuleId,
                LectureId = lecture.Id,
                StartDateTime = lecture.StartDateTime,
                EndDateTime = lecture.EndDateTime,
                CourseCode = lecture.TimetableRule.TeacherCourse.Course.Code,
                CourseTitle = lecture.TimetableRule.TeacherCourse.Course.Title,
                SectionName = lecture.TimetableRule.TeacherCourse.Section.Name,
                Room = lecture.TimetableRule.Room,
                LectureType = lecture.TimetableRule.LectureType,
                Status = status,
                CanMark = canMark,
                HasAttendance = hasAttendance,
                TotalStudents = 0,
                PresentStudents = lecture.AttendanceRecords.Count(ar => ar.Status == "Present"),
                MinutesFromStart = minutesFromStart
            };
        }
    }

    public class LectureViewModel
    {
        public int? LectureId { get; set; }
        public int TimetableRuleId { get; set; }
        public string CourseCode { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public string? Room { get; set; }
        public string? LectureType { get; set; }
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool CanMark { get; set; }
        public bool HasAttendance { get; set; }
        public int TotalStudents { get; set; }
        public int PresentStudents { get; set; }
        public double MinutesFromStart { get; set; }
    }
}
