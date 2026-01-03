using attendence.Data.Data;
using attendence.Domain.Entities;
using attendence.Services.Models;
using attendence.Services.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace attendenceProject.Pages.Admin.Sections;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly BulkImportService _importService;

    public IndexModel(ApplicationDbContext context, BulkImportService importService)
    {
        _context = context;
        _importService = importService;
    }

    // Messages
    [TempData]
    public string? SuccessMessage { get; set; }
    [TempData]
    public string? ErrorMessage { get; set; }

    // Tab Navigation
    public string ActiveTab { get; set; } = "all";

    // Statistics
    public int TotalSections { get; set; }
    public int TotalStudents { get; set; }
    public string? MostPopularSection { get; set; }
    public int MostPopularSectionStudents { get; set; }

    // All Sections
    public List<SectionViewModel> Sections { get; set; } = new();
    public string? SearchTerm { get; set; }
    public int? BadgeFilter { get; set; }
    public int? SemesterFilter { get; set; }

    // Add Section
    public List<Badge> AllBadges { get; set; } = new();
    [BindProperty]
    public int BadgeId { get; set; }
    [BindProperty]
    public string SectionName { get; set; } = string.Empty;
    [BindProperty]
    public int Semester { get; set; }
    [BindProperty]
    public string Session { get; set; } = string.Empty;

    // Import
    [BindProperty]
    public IFormFile? ImportFile { get; set; }
    public List<SectionImportModel> ValidatedSections { get; set; } = new();
    public List<string> ValidationErrors { get; set; } = new();
    public ImportValidationResult? ValidationResult { get; set; }

    public async Task OnGetAsync(string tab = "all", string? searchTerm = null, int? badgeFilter = null, int? semesterFilter = null)
    {
        ActiveTab = tab;
        SearchTerm = searchTerm;
        BadgeFilter = badgeFilter;
        SemesterFilter = semesterFilter;
        
        await LoadStatisticsAsync();
        await LoadSectionsAsync();
        await LoadBadgesAsync();

        // Load validation data from TempData
        if (TempData["ValidatedSections"] is string validatedJson)
        {
            ValidatedSections = JsonSerializer.Deserialize<List<SectionImportModel>>(validatedJson) ?? new List<SectionImportModel>();
            TempData.Keep("ValidatedSections");
            
            // Create validation result for display
            ValidationResult = new ImportValidationResult
            {
                IsValid = true,
                TotalRows = ValidatedSections.Count,
                ErrorCount = 0
            };
        }

        if (TempData["ValidationErrors"] is string errorsJson)
        {
            ValidationErrors = JsonSerializer.Deserialize<List<string>>(errorsJson) ?? new List<string>();
            
            // Create validation result for display
            ValidationResult = new ImportValidationResult
            {
                IsValid = false,
                TotalRows = 0,
                ErrorCount = ValidationErrors.Count
            };
        }
    }

    private async Task LoadStatisticsAsync()
    {
        TotalSections = await _context.Sections.CountAsync();
        TotalStudents = await _context.Students.CountAsync();

        var mostPopular = await _context.Sections
            .Include(s => s.Badge)
            .Include(s => s.Students)
            .Select(s => new
            {
                Section = s,
                StudentCount = s.Students.Count
            })
            .OrderByDescending(x => x.StudentCount)
            .FirstOrDefaultAsync();

        if (mostPopular != null)
        {
            MostPopularSection = $"{mostPopular.Section.Badge.Name} - {mostPopular.Section.Name}";
            MostPopularSectionStudents = mostPopular.StudentCount;
        }
    }

    private async Task LoadBadgesAsync()
    {
        AllBadges = await _context.Badges.OrderBy(b => b.Name).ToListAsync();
    }

    private async Task LoadSectionsAsync()
    {
        var query = _context.Sections
            .Include(s => s.Badge)
            .Include(s => s.Students)
            .Include(s => s.TeacherCourses)
            .ThenInclude(tc => tc.Course)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            query = query.Where(s => s.Name.Contains(SearchTerm) || s.Badge.Name.Contains(SearchTerm));
        }

        if (BadgeFilter.HasValue)
        {
            query = query.Where(s => s.BadgeId == BadgeFilter.Value);
        }

        if (SemesterFilter.HasValue)
        {
            query = query.Where(s => s.Semester == SemesterFilter.Value);
        }

        var sections = await query
            .OrderBy(s => s.Badge.Name)
            .ThenBy(s => s.Semester)
            .ThenBy(s => s.Name)
            .ToListAsync();

        Sections = sections.Select(s => new SectionViewModel
        {
            Id = s.Id,
            BadgeName = s.Badge.Name,
            SectionName = s.Name,
            Semester = s.Semester,
            Session = s.Session,
            StudentCount = s.Students.Count,
            CourseCount = s.TeacherCourses.Select(tc => tc.CourseId).Distinct().Count()
        }).ToList();
    }

    public async Task<IActionResult> OnPostAddAsync()
    {
        if (BadgeId <= 0)
        {
            ErrorMessage = "Please select a badge.";
            ActiveTab = "add";
            await LoadStatisticsAsync();
            await LoadSectionsAsync();
            await LoadBadgesAsync();
            return Page();
        }

        if (string.IsNullOrWhiteSpace(SectionName))
        {
            ErrorMessage = "Section name is required.";
            ActiveTab = "add";
            await LoadStatisticsAsync();
            await LoadSectionsAsync();
            await LoadBadgesAsync();
            return Page();
        }

        if (Semester < 1 || Semester > 8)
        {
            ErrorMessage = "Semester must be between 1 and 8.";
            ActiveTab = "add";
            await LoadStatisticsAsync();
            await LoadSectionsAsync();
            await LoadBadgesAsync();
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Session))
        {
            ErrorMessage = "Session is required.";
            ActiveTab = "add";
            await LoadStatisticsAsync();
            await LoadSectionsAsync();
            await LoadBadgesAsync();
            return Page();
        }

        // Check for duplicate
        var exists = await _context.Sections
            .AnyAsync(s => s.BadgeId == BadgeId && s.Name.ToLower() == SectionName.Trim().ToLower() && s.Semester == Semester && s.Session == Session);

        if (exists)
        {
            ErrorMessage = $"Section '{SectionName.Trim()}' already exists for this badge, semester, and session.";
            ActiveTab = "add";
            await LoadStatisticsAsync();
            await LoadSectionsAsync();
            await LoadBadgesAsync();
            return Page();
        }

        var section = new Section
        {
            BadgeId = BadgeId,
            Name = SectionName.Trim(),
            Semester = Semester,
            Session = Session.Trim()
        };

        _context.Sections.Add(section);
        await _context.SaveChangesAsync();

        SuccessMessage = $"Section '{section.Name}' added successfully!";
        return RedirectToPage(new { tab = "all" });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            var section = await _context.Sections
                .Include(s => s.Students)
                .ThenInclude(st => st.AttendanceRecords)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (section == null)
            {
                TempData["ErrorMessage"] = "Section not found.";
                return RedirectToPage();
            }

            // Delete cascade: Section -> Students -> AttendanceRecords + Users
            foreach (var student in section.Students)
            {
                // Delete attendance records
                _context.AttendanceRecords.RemoveRange(student.AttendanceRecords);
                
                // Delete student record
                _context.Students.Remove(student);
                
                // Delete user account
                var user = await _context.Users.FindAsync(student.UserId);
                if (user != null)
                {
                    // Delete user's notifications
                    var notifications = await _context.Notifications
                        .Where(n => n.UserId == user.Id)
                        .ToListAsync();
                    _context.Notifications.RemoveRange(notifications);
                    
                    // Delete user's activity logs
                    var activityLogs = await _context.ActivityLogs
                        .Where(al => al.ActorId == user.Id)
                        .ToListAsync();
                    _context.ActivityLogs.RemoveRange(activityLogs);
                    
                    _context.Users.Remove(user);
                }
            }

            // Delete teacher course assignments for this section
            var teacherCourses = await _context.TeacherCourses
                .Include(tc => tc.TimetableRules)
                .ThenInclude(tr => tr.Lectures)
                .Where(tc => tc.SectionId == id)
                .ToListAsync();

            foreach (var tc in teacherCourses)
            {
                foreach (var rule in tc.TimetableRules)
                {
                    // Delete attendance for lectures
                    foreach (var lecture in rule.Lectures)
                    {
                        var attendanceRecords = await _context.AttendanceRecords
                            .Where(ar => ar.LectureId == lecture.Id)
                            .ToListAsync();
                        _context.AttendanceRecords.RemoveRange(attendanceRecords);
                    }
                    _context.Lectures.RemoveRange(rule.Lectures);
                }
                _context.TimetableRules.RemoveRange(tc.TimetableRules);
            }
            _context.TeacherCourses.RemoveRange(teacherCourses);
            
            // Delete the section
            _context.Sections.Remove(section);
            
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["SuccessMessage"] = $"Section '{section.Name}' and all related data (students, assignments) deleted successfully!";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            TempData["ErrorMessage"] = $"Error deleting section: {ex.Message}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostValidateImportAsync()
    {
        if (ImportFile == null || ImportFile.Length == 0)
        {
            TempData["ErrorMessage"] = "Please select a file to upload.";
            return RedirectToPage(new { tab = "import" });
        }

        if (!ImportFile.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Please upload a valid CSV file.";
            return RedirectToPage(new { tab = "import" });
        }

        var result = await _importService.ValidateSectionsImportAsync(ImportFile);
        
        if (!result.IsValid)
        {
            // Store errors in TempData
            TempData["ValidationErrors"] = JsonSerializer.Serialize(result.Errors);
            TempData["ShowValidationResults"] = true;
            TempData["ValidationFailed"] = true;
            return RedirectToPage(new { tab = "import" });
        }

        // Store validated sections in TempData
        TempData["ValidatedSections"] = JsonSerializer.Serialize(result.ValidatedSections);
        TempData["ShowValidationResults"] = true;
        TempData["ValidationSuccess"] = true;
        TempData.Keep("ValidatedSections");

        return RedirectToPage(new { tab = "import" });
    }

    public IActionResult OnPostClearValidation()
    {
        TempData.Remove("ValidatedSections");
        TempData.Remove("ValidationErrors");
        TempData.Remove("ShowValidationResults");
        return RedirectToPage(new { tab = "import" });
    }

    public async Task<IActionResult> OnPostImportAsync()
    {
        var validatedSectionsJson = TempData["ValidatedSections"] as string;

        if (string.IsNullOrEmpty(validatedSectionsJson))
        {
            TempData["ErrorMessage"] = "No validated data found. Please validate your file first.";
            return RedirectToPage(new { tab = "import" });
        }

        var validatedSections = JsonSerializer.Deserialize<List<SectionImportModel>>(validatedSectionsJson);

        if (validatedSections == null || !validatedSections.Any())
        {
            TempData["ErrorMessage"] = "No valid sections to import.";
            return RedirectToPage(new { tab = "import" });
        }

        try
        {
            var sectionsToAdd = new List<Section>();

            foreach (var sectionModel in validatedSections)
            {
                sectionsToAdd.Add(new Section
                {
                    BadgeId = sectionModel.BadgeId,
                    Name = sectionModel.SectionName,
                    Semester = sectionModel.Semester,
                    Session = sectionModel.Session
                });
            }

            if (sectionsToAdd.Any())
            {
                _context.Sections.AddRange(sectionsToAdd);
                await _context.SaveChangesAsync();

                // Clear validation data
                TempData.Remove("ValidatedSections");
                TempData.Remove("ShowValidationResults");

                TempData["SuccessMessage"] = $"Successfully imported {sectionsToAdd.Count} section(s)!";
            }

            return RedirectToPage(new { tab = "all" });
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error importing sections: {ex.Message}";
            return RedirectToPage(new { tab = "import" });
        }
    }

    public class SectionViewModel
    {
        public int Id { get; set; }
        public string BadgeName { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public int Semester { get; set; }
        public string Session { get; set; } = string.Empty;
        public int StudentCount { get; set; }
        public int CourseCount { get; set; }
    }

    public class ImportValidationResult
    {
        public bool IsValid { get; set; }
        public int TotalRows { get; set; }
        public int ErrorCount { get; set; }
    }

}
