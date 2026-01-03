using attendence.Data.Data;
using attendence.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace attendenceProject.Pages.Admin.TeacherCourses;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public IndexModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public List<TeacherCourse> TeacherCourses { get; set; } = new();
    public List<Badge> AllBadges { get; set; } = new();
    public List<Section> AllSections { get; set; } = new();
    
    public string? SearchTerm { get; set; }
    public int? SelectedBadgeId { get; set; }
    public int? SelectedSectionId { get; set; }

    public int TotalAssignments { get; set; }
    public int ActiveTeachers { get; set; }
    public int SectionsCovered { get; set; }
    public int TotalCourses { get; set; }

    public async Task OnGetAsync(string? searchTerm, int? badgeId, int? sectionId)
    {
        SearchTerm = searchTerm;
        SelectedBadgeId = badgeId;
        SelectedSectionId = sectionId;

        // Load all badges for dropdown
        AllBadges = await _context.Badges
            .OrderBy(b => b.Name)
            .ToListAsync();

        // Load all sections for dropdown
        AllSections = await _context.Sections
            .Include(s => s.Badge)
            .OrderBy(s => s.Badge.Name)
            .ThenBy(s => s.Semester)
            .ThenBy(s => s.Name)
            .ToListAsync();

        // Get stats
        TotalAssignments = await _context.TeacherCourses.CountAsync();
        ActiveTeachers = await _context.TeacherCourses
            .Select(tc => tc.TeacherId)
            .Distinct()
            .CountAsync();
        SectionsCovered = await _context.TeacherCourses
            .Select(tc => tc.SectionId)
            .Distinct()
            .CountAsync();
        TotalCourses = await _context.TeacherCourses
            .Select(tc => tc.CourseId)
            .Distinct()
            .CountAsync();

        // Build query
        var query = _context.TeacherCourses
            .Include(tc => tc.Teacher)
            .ThenInclude(t => t.User)
            .Include(tc => tc.Course)
            .Include(tc => tc.Section)
            .ThenInclude(s => s.Badge)
            .AsQueryable();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(tc => tc.Teacher.User.FullName.Contains(searchTerm));
        }

        if (badgeId.HasValue && badgeId.Value > 0)
        {
            query = query.Where(tc => tc.Section.BadgeId == badgeId.Value);
        }

        if (sectionId.HasValue && sectionId.Value > 0)
        {
            query = query.Where(tc => tc.SectionId == sectionId.Value);
        }

        TeacherCourses = await query
            .OrderBy(tc => tc.Teacher.User.FullName)
            .ThenBy(tc => tc.Section.Badge.Name)
            .ThenBy(tc => tc.Course.Code)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            var teacherCourse = await _context.TeacherCourses
                .Include(tc => tc.TimetableRules)
                .ThenInclude(tr => tr.Lectures)
                .FirstOrDefaultAsync(tc => tc.Id == id);

            if (teacherCourse == null)
            {
                TempData["ErrorMessage"] = "Teacher course assignment not found.";
                return RedirectToPage();
            }

            // Delete cascade: TeacherCourse -> TimetableRules -> Lectures -> AttendanceRecords
            foreach (var timetableRule in teacherCourse.TimetableRules)
            {
                foreach (var lecture in timetableRule.Lectures)
                {
                    var attendanceRecords = await _context.AttendanceRecords
                        .Where(ar => ar.LectureId == lecture.Id)
                        .ToListAsync();
                    _context.AttendanceRecords.RemoveRange(attendanceRecords);
                }
                _context.Lectures.RemoveRange(timetableRule.Lectures);
            }
            _context.TimetableRules.RemoveRange(teacherCourse.TimetableRules);
            
            // Delete the teacher course assignment
            _context.TeacherCourses.Remove(teacherCourse);
            
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["SuccessMessage"] = "Teacher course assignment and all related data deleted successfully!";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            TempData["ErrorMessage"] = $"Error deleting assignment: {ex.Message}";
        }

        return RedirectToPage();
    }
}
