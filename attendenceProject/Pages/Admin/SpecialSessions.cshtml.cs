using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using attendence.Data.Data;

namespace attendenceProject.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class SpecialSessionsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public SpecialSessionsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<SpecialSessionInfo> Sessions { get; set; } = new();
        
        // Statistics Properties
        public int TotalSessions { get; set; }
        public int ScheduledCount { get; set; }
        public int OngoingCount { get; set; }
        public int CompletedCount { get; set; }
        public Dictionary<string, int> SessionsByType { get; set; } = new();
        public int TotalStudentsReached { get; set; }
        public double AverageAttendanceRate { get; set; }
        
        // Filter Properties
        public string? FilterType { get; set; }
        public string? FilterStatus { get; set; }
        public string? FilterTeacher { get; set; }
        public int? FilterBadge { get; set; }
        public int? FilterSection { get; set; }
        public int? FilterCourse { get; set; }
        public string? SearchTerm { get; set; }
        
        // Filter Options
        public List<BadgeOption> Badges { get; set; } = new();
        public List<SectionOption> Sections { get; set; } = new();
        public List<CourseOption> Courses { get; set; } = new();
        public List<TeacherOption> Teachers { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(string? type, string? status, string? teacher, int? badge, int? section, int? course, string? search)
        {
            FilterType = type;
            FilterStatus = status;
            FilterTeacher = teacher;
            FilterBadge = badge;
            FilterSection = section;
            FilterCourse = course;
            SearchTerm = search;

            // Load filter options
            Badges = await _context.Badges
                .OrderBy(b => b.Name)
                .Select(b => new BadgeOption { Id = b.Id, Name = b.Name })
                .ToListAsync();

            Sections = await _context.Sections
                .Include(s => s.Badge)
                .OrderBy(s => s.Badge.Name)
                .ThenBy(s => s.Name)
                .Select(s => new SectionOption 
                { 
                    Id = s.Id, 
                    Name = $"{s.Badge.Name} - {s.Name}",
                    BadgeId = s.BadgeId
                })
                .ToListAsync();

            Courses = await _context.Courses
                .OrderBy(c => c.Code)
                .Select(c => new CourseOption 
                { 
                    Id = c.Id, 
                    Name = $"{c.Code} - {c.Title}" 
                })
                .ToListAsync();

            Teachers = await _context.Teachers
                .Include(t => t.User)
                .OrderBy(t => t.User.FullName)
                .Select(t => new TeacherOption 
                { 
                    Id = t.Id, 
                    Name = t.User.FullName 
                })
                .ToListAsync();

            var query = _context.Lectures
                .Include(l => l.CreatedByTeacher)
                    .ThenInclude(t => t!.User)
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Course)
                .Include(l => l.TimetableRule.TeacherCourse.Section)
                    .ThenInclude(s => s.Badge)
                .Include(l => l.TimetableRule.TeacherCourse.Section.Students)
                .Include(l => l.AttendanceRecords)
                .Where(l => l.CreatedByTeacherId != null); // Only special sessions

            // Apply filters
            if (!string.IsNullOrEmpty(type) && type != "All")
            {
                query = query.Where(l => l.LectureType == type);
            }

            if (!string.IsNullOrEmpty(status))
            {
                var now = DateTime.Now;
                query = status switch
                {
                    "Scheduled" => query.Where(l => l.StartDateTime > now),
                    "Ongoing" => query.Where(l => l.StartDateTime <= now && l.EndDateTime >= now),
                    "Completed" => query.Where(l => l.EndDateTime < now),
                    _ => query
                };
            }

            if (!string.IsNullOrEmpty(teacher) && int.TryParse(teacher, out int teacherId))
            {
                query = query.Where(l => l.CreatedByTeacherId == teacherId);
            }

            if (badge.HasValue)
            {
                query = query.Where(l => l.TimetableRule.TeacherCourse.Section.BadgeId == badge.Value);
            }

            if (section.HasValue)
            {
                query = query.Where(l => l.TimetableRule.TeacherCourse.SectionId == section.Value);
            }

            if (course.HasValue)
            {
                query = query.Where(l => l.TimetableRule.TeacherCourse.CourseId == course.Value);
            }

            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(l => 
                    l.CreatedByTeacher!.User.FullName.ToLower().Contains(searchLower) ||
                    l.TimetableRule.TeacherCourse.Course.Code.ToLower().Contains(searchLower) ||
                    l.TimetableRule.TeacherCourse.Course.Title.ToLower().Contains(searchLower));
            }

            var lectures = await query
                .OrderByDescending(l => l.StartDateTime)
                .ToListAsync();

            Sessions = lectures.Select(l => new SpecialSessionInfo
            {
                Id = l.Id,
                LectureType = l.LectureType ?? "Session",
                CourseName = l.TimetableRule.TeacherCourse.Course.Code + " - " + l.TimetableRule.TeacherCourse.Course.Title,
                SectionName = $"{l.TimetableRule.TeacherCourse.Section.Badge.Name} - {l.TimetableRule.TeacherCourse.Section.Name}",
                TeacherName = l.CreatedByTeacher?.User?.FullName ?? "Unknown",
                StartDateTime = l.StartDateTime,
                EndDateTime = l.EndDateTime,
                Description = l.Description,
                TotalStudents = l.TimetableRule.TeacherCourse.Section.Students?.Count ?? 0,
                PresentCount = l.AttendanceRecords.Count(a => a.Status == "Present"),
                AbsentCount = l.AttendanceRecords.Count(a => a.Status == "Absent"),
                LateCount = l.AttendanceRecords.Count(a => a.Status == "Late")
            }).ToList();

            // Calculate statistics
            var now2 = DateTime.Now;
            TotalSessions = Sessions.Count;
            ScheduledCount = Sessions.Count(s => s.StartDateTime > now2);
            OngoingCount = Sessions.Count(s => s.StartDateTime <= now2 && s.EndDateTime >= now2);
            CompletedCount = Sessions.Count(s => s.EndDateTime < now2);

            // Sessions by type
            SessionsByType = Sessions
                .GroupBy(s => s.LectureType)
                .ToDictionary(g => g.Key, g => g.Count())
                .OrderByDescending(kvp => kvp.Value)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // Total students reached (unique count)
            TotalStudentsReached = Sessions.Sum(s => s.TotalStudents);

            // Average attendance rate
            var completedSessions = Sessions.Where(s => s.EndDateTime < now2 && s.TotalStudents > 0).ToList();
            if (completedSessions.Any())
            {
                AverageAttendanceRate = Math.Round(
                    completedSessions.Average(s => 
                        (double)(s.PresentCount + s.LateCount) / s.TotalStudents * 100
                    ), 1);
            }

            return Page();
        }
    }

    public class SpecialSessionInfo
    {
        public int Id { get; set; }
        public string LectureType { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public string? Description { get; set; }
        public int TotalStudents { get; set; }
        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
        public int LateCount { get; set; }

        public string StatusClass
        {
            get
            {
                var now = DateTime.Now;
                if (StartDateTime > now) return "scheduled";
                if (EndDateTime >= now) return "ongoing";
                return "completed";
            }
        }

        public string StatusText
        {
            get
            {
                var now = DateTime.Now;
                if (StartDateTime > now) return "Scheduled";
                if (EndDateTime >= now) return "Ongoing";
                return "Completed";
            }
        }

        public string TypeColor
        {
            get
            {
                return LectureType switch
                {
                    "Quiz" => "border-yellow-400 bg-yellow-50",
                    "Test" => "border-red-400 bg-red-50",
                    "Lab" => "border-purple-400 bg-purple-50",
                    "Practical" => "border-green-400 bg-green-50",
                    "Workshop" => "border-orange-400 bg-orange-50",
                    "GuestLecture" => "border-indigo-400 bg-indigo-50",
                    "Review" => "border-blue-400 bg-blue-50",
                    "MakeUp" => "border-teal-400 bg-teal-50",
                    "Extra" => "border-pink-400 bg-pink-50",
                    _ => "border-gray-400 bg-gray-50"
                };
            }
        }

        public string TypeIcon
        {
            get
            {
                return LectureType switch
                {
                    "Quiz" => "🎯",
                    "Test" => "📝",
                    "Lab" => "🔬",
                    "Practical" => "⚙️",
                    "Workshop" => "🛠️",
                    "GuestLecture" => "🎤",
                    "Review" => "📚",
                    "MakeUp" => "🔄",
                    "Extra" => "➕",
                    _ => "📅"
                };
            }
        }
    }

    public class BadgeOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class SectionOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int BadgeId { get; set; }
    }

    public class CourseOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class TeacherOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
