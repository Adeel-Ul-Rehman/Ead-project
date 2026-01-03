using attendence.Data.Data;
using attendence.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace attendenceProject.Pages.Admin.Timetables
{
    public class ViewTimetableModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ViewTimetableModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<Badge> Badges { get; set; } = new();
        public List<Section> Sections { get; set; } = new();
        public List<attendence.Domain.Entities.Teacher> Teachers { get; set; } = new();
        public List<Course> Courses { get; set; } = new();
        
        // Statistics properties
        public int TotalRules { get; set; }
        public int TotalLectures { get; set; }
        public int ActiveTeachers { get; set; }
        public int TotalSections { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public string ViewType { get; set; } = "section"; // section, teacher, course
        
        [BindProperty(SupportsGet = true)]
        public int? SelectedBadgeId { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public int? SelectedSectionId { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public int? SelectedTeacherId { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public int? SelectedCourseId { get; set; }

        public List<TimetableRule> TimetableRules { get; set; } = new();
        public Dictionary<string, List<TimetableEntry>> WeeklySchedule { get; set; } = new();

        public class TimetableEntry
        {
            public int RuleId { get; set; }
            public string CourseName { get; set; } = string.Empty;
            public string TeacherName { get; set; } = string.Empty;
            public string SectionName { get; set; } = string.Empty;
            public string BadgeName { get; set; } = string.Empty;
            public string StartTime { get; set; } = string.Empty;
            public string EndTime { get; set; } = string.Empty;
            public string RoomNumber { get; set; } = string.Empty;
            public bool HasConflict { get; set; }
        }

        public async Task OnGetAsync()
        {
            // Load filter data
            Badges = await _context.Badges.OrderBy(b => b.Name).ToListAsync();
            Teachers = await _context.Teachers
                .Include(t => t.User)
                .OrderBy(t => t.User.FullName)
                .ToListAsync();
            Courses = await _context.Courses.OrderBy(c => c.Code).ToListAsync();

            if (SelectedBadgeId.HasValue)
            {
                Sections = await _context.Sections
                    .Where(s => s.BadgeId == SelectedBadgeId.Value)
                    .Include(s => s.Badge)
                    .OrderBy(s => s.Name)
                    .ToListAsync();
            }
            else
            {
                Sections = await _context.Sections
                    .Include(s => s.Badge)
                    .OrderBy(s => s.Name)
                    .ToListAsync();
            }

            // Load timetable rules based on filters
            var query = _context.TimetableRules
                .Include(t => t.TeacherCourse)
                    .ThenInclude(tc => tc.Course)
                .Include(t => t.TeacherCourse)
                    .ThenInclude(tc => tc.Teacher)
                        .ThenInclude(t => t.User)
                .Include(t => t.TeacherCourse)
                    .ThenInclude(tc => tc.Section)
                        .ThenInclude(s => s.Badge)
                .AsQueryable();

            if (ViewType == "section" && SelectedSectionId.HasValue)
            {
                query = query.Where(t => t.TeacherCourse.SectionId == SelectedSectionId.Value);
            }
            else if (ViewType == "teacher" && SelectedTeacherId.HasValue)
            {
                query = query.Where(t => t.TeacherCourse.TeacherId == SelectedTeacherId.Value);
            }
            else if (ViewType == "course" && SelectedCourseId.HasValue)
            {
                query = query.Where(t => t.TeacherCourse.CourseId == SelectedCourseId.Value);
            }

            TimetableRules = await query.OrderBy(t => t.StartTime).ToListAsync();

            // Calculate statistics
            CalculateStatistics();

            // Build weekly schedule
            BuildWeeklySchedule();
        }

        private void CalculateStatistics()
        {
            TotalRules = TimetableRules.Count;
            
            // Calculate total lectures (rules can repeat on multiple days)
            TotalLectures = TimetableRules.Sum(r => 
            {
                var days = r.DaysOfWeek.Split(',', StringSplitOptions.RemoveEmptyEntries);
                return days.Length;
            });
            
            // Get distinct teachers
            ActiveTeachers = TimetableRules
                .Select(r => r.TeacherCourse?.TeacherId)
                .Where(id => id.HasValue)
                .Distinct()
                .Count();
            
            // Get distinct sections
            TotalSections = TimetableRules
                .Select(r => r.TeacherCourse?.SectionId)
                .Where(id => id.HasValue)
                .Distinct()
                .Count();
        }

        private void BuildWeeklySchedule()
        {
            var days = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
            
            foreach (var day in days)
            {
                WeeklySchedule[day] = new List<TimetableEntry>();
            }

            foreach (var rule in TimetableRules)
            {
                var daysList = rule.DaysOfWeek.Split(',', StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var day in daysList)
                {
                    var dayTrimmed = day.Trim();
                    if (WeeklySchedule.ContainsKey(dayTrimmed))
                    {
                        var endTime = rule.StartTime.Add(TimeSpan.FromMinutes(rule.DurationMinutes));
                        var entry = new TimetableEntry
                        {
                            RuleId = rule.Id,
                            CourseName = rule.TeacherCourse?.Course?.Code ?? "N/A",
                            TeacherName = rule.TeacherCourse?.Teacher?.User?.FullName ?? "N/A",
                            SectionName = rule.TeacherCourse?.Section?.Name ?? "N/A",
                            BadgeName = rule.TeacherCourse?.Section?.Badge?.Name ?? "N/A",
                            StartTime = rule.StartTime.ToString(@"hh\:mm"),
                            EndTime = endTime.ToString(@"hh\:mm"),
                            RoomNumber = rule.Room ?? "N/A"
                        };

                        // Check for conflicts
                        entry.HasConflict = CheckForConflict(dayTrimmed, rule);

                        WeeklySchedule[dayTrimmed].Add(entry);
                    }
                }
            }

            // Sort each day's entries by start time
            foreach (var day in days)
            {
                WeeklySchedule[day] = WeeklySchedule[day]
                    .OrderBy(e => TimeSpan.Parse(e.StartTime))
                    .ToList();
            }
        }

        private bool CheckForConflict(string day, TimetableRule currentRule)
        {
            var sameDayRules = TimetableRules
                .Where(r => r.Id != currentRule.Id && 
                           r.DaysOfWeek.Contains(day) &&
                           r.TeacherCourse.SectionId == currentRule.TeacherCourse.SectionId)
                .ToList();

            foreach (var rule in sameDayRules)
            {
                var currentEndTime = currentRule.StartTime.Add(TimeSpan.FromMinutes(currentRule.DurationMinutes));
                var ruleEndTime = rule.StartTime.Add(TimeSpan.FromMinutes(rule.DurationMinutes));
                
                // Check for time overlap
                if ((currentRule.StartTime < ruleEndTime && currentEndTime > rule.StartTime))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
