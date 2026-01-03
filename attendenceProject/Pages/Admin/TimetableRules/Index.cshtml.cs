using attendence.Data.Data;
using attendence.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace attendenceProject.Pages.Admin.TimetableRules;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public IndexModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public List<TimetableRule> TimetableRules { get; set; } = new();
    public List<Badge> AllBadges { get; set; } = new();
    public List<TeacherCourse> AllTeacherCourses { get; set; } = new();
    
    public string? SearchTerm { get; set; }
    public int? SelectedBadgeId { get; set; }
    public int? SelectedTeacherCourseId { get; set; }
    
    public int TotalRules { get; set; }
    public int ActiveTeachers { get; set; }
    public int TotalLectures { get; set; }
    public int AverageWeeklyHours { get; set; }

    public async Task OnGetAsync(string? searchTerm, int? badgeId, int? teacherCourseId)
    {
        SearchTerm = searchTerm;
        SelectedBadgeId = badgeId;
        SelectedTeacherCourseId = teacherCourseId;

        // Load filter options
        AllBadges = await _context.Badges
            .OrderBy(b => b.Name)
            .ToListAsync();

        AllTeacherCourses = await _context.TeacherCourses
            .Include(tc => tc.Teacher)
            .ThenInclude(t => t.User)
            .Include(tc => tc.Course)
            .Include(tc => tc.Section)
            .ThenInclude(s => s.Badge)
            .OrderBy(tc => tc.Teacher.User.FullName)
            .ThenBy(tc => tc.Course.Code)
            .ToListAsync();

        // Calculate statistics
        TotalRules = await _context.TimetableRules.CountAsync();
        ActiveTeachers = await _context.TimetableRules
            .Select(tr => tr.TeacherCourse.TeacherId)
            .Distinct()
            .CountAsync();
        TotalLectures = await _context.Lectures
            .CountAsync();
        
        var rules = await _context.TimetableRules
            .Include(tr => tr.TeacherCourse)
            .ToListAsync();
        
        int totalMinutes = 0;
        foreach (var rule in rules)
        {
            var days = rule.DaysOfWeek.Split(',', StringSplitOptions.RemoveEmptyEntries).Length;
            totalMinutes += rule.DurationMinutes * days;
        }
        AverageWeeklyHours = rules.Any() ? totalMinutes / 60 : 0;

        // Build filtered query
        var query = _context.TimetableRules
            .Include(tr => tr.TeacherCourse)
            .ThenInclude(tc => tc.Teacher)
            .ThenInclude(t => t.User)
            .Include(tr => tr.TeacherCourse)
            .ThenInclude(tc => tc.Course)
            .Include(tr => tr.TeacherCourse)
            .ThenInclude(tc => tc.Section)
            .ThenInclude(s => s.Badge)
            .AsQueryable();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(tr => 
                tr.TeacherCourse.Teacher.User.FullName.Contains(searchTerm) ||
                tr.TeacherCourse.Course.Code.Contains(searchTerm) ||
                tr.TeacherCourse.Course.Title.Contains(searchTerm));
        }

        if (badgeId.HasValue && badgeId.Value > 0)
        {
            query = query.Where(tr => tr.TeacherCourse.Section.BadgeId == badgeId.Value);
        }

        if (teacherCourseId.HasValue && teacherCourseId.Value > 0)
        {
            query = query.Where(tr => tr.TeacherCourseId == teacherCourseId.Value);
        }

        TimetableRules = await query
            .OrderBy(tr => tr.TeacherCourse.Teacher.User.FullName)
            .ThenBy(tr => tr.TeacherCourse.Course.Code)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            var rule = await _context.TimetableRules
                .Include(tr => tr.Lectures)
                .FirstOrDefaultAsync(tr => tr.Id == id);

            if (rule == null)
            {
                TempData["ErrorMessage"] = "Timetable rule not found.";
                return RedirectToPage();
            }

            // Delete lectures generated from this rule
            if (rule.Lectures?.Any() == true)
            {
                // Delete attendance records for these lectures
                foreach (var lecture in rule.Lectures)
                {
                    var attendanceRecords = await _context.AttendanceRecords
                        .Where(ar => ar.LectureId == lecture.Id)
                        .ToListAsync();
                    _context.AttendanceRecords.RemoveRange(attendanceRecords);
                }

                _context.Lectures.RemoveRange(rule.Lectures);
            }

            // Delete the timetable rule
            _context.TimetableRules.Remove(rule);
            
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["SuccessMessage"] = "Timetable rule deleted successfully!";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            TempData["ErrorMessage"] = $"Error deleting timetable rule: {ex.Message}";
        }

        return RedirectToPage();
    }
}
