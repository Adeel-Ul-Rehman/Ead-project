using attendence.Data.Data;
using attendence.Domain.Entities;
using attendence.Services.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace attendenceProject.Pages.Admin.Courses;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly EmailService _emailService;

    public IndexModel(ApplicationDbContext context, EmailService emailService)
    {
        _context = context;
        _emailService = emailService;
    }

    // Navigation
    public string ActiveTab { get; set; } = "all";
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    // Statistics
    public int TotalCourses { get; set; }
    public int TheoryCourses { get; set; }
    public int LabCourses { get; set; }
    public int AssignedCourses { get; set; }

    // All Courses Tab
    public List<Course> Courses { get; set; } = new();
    public string? SearchTerm { get; set; }
    public string? CourseTypeFilter { get; set; }
    public string? CreditFilter { get; set; }

    // Add Course Tab
    [BindProperty]
    public CourseInputModel NewCourse { get; set; } = new();

    // Import Tab
    [BindProperty]
    public IFormFile? ImportFile { get; set; }
    public ImportResult? ValidationResult { get; set; }
    public List<ImportedCourse> ValidatedCourses { get; set; } = new();

    public async Task OnGetAsync(string? tab, string? searchTerm, string? courseType, string? credits)
    {
        ActiveTab = tab ?? "all";
        SearchTerm = searchTerm;
        CourseTypeFilter = courseType;
        CreditFilter = credits;

        // Load messages from TempData
        SuccessMessage = TempData["SuccessMessage"] as string;
        ErrorMessage = TempData["ErrorMessage"] as string;

        await LoadStatistics();

        if (ActiveTab == "all")
        {
            await LoadAllCourses();
        }
        else if (ActiveTab == "import")
        {
            LoadValidationResults();
        }
    }

    private async Task LoadStatistics()
    {
        TotalCourses = await _context.Courses.CountAsync();
        TheoryCourses = await _context.Courses.CountAsync(c => !c.IsLab);
        LabCourses = await _context.Courses.CountAsync(c => c.IsLab);
        AssignedCourses = await _context.Courses
            .Where(c => c.TeacherCourses.Any())
            .CountAsync();
    }

    private async Task LoadAllCourses()
    {
        var query = _context.Courses
            .Include(c => c.TeacherCourses)
            .AsQueryable();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            query = query.Where(c => c.Title.Contains(SearchTerm) || c.Code.Contains(SearchTerm));
        }

        if (!string.IsNullOrWhiteSpace(CourseTypeFilter))
        {
            bool isLab = CourseTypeFilter.Equals("Lab", StringComparison.OrdinalIgnoreCase);
            query = query.Where(c => c.IsLab == isLab);
        }

        if (!string.IsNullOrWhiteSpace(CreditFilter) && int.TryParse(CreditFilter, out int credits))
        {
            query = query.Where(c => c.CreditHours == credits);
        }

        Courses = await query
            .OrderBy(c => c.Code)
            .ToListAsync();
    }

    private void LoadValidationResults()
    {
        // Load validation results from TempData if available
        var validationResultJson = TempData["ValidationResult"] as string;
        var validatedCoursesJson = TempData["ValidatedCourses"] as string;

        if (!string.IsNullOrEmpty(validationResultJson))
        {
            ValidationResult = JsonSerializer.Deserialize<ImportResult>(validationResultJson);
            TempData.Keep("ValidationResult");
        }

        if (!string.IsNullOrEmpty(validatedCoursesJson))
        {
            ValidatedCourses = JsonSerializer.Deserialize<List<ImportedCourse>>(validatedCoursesJson) ?? new();
            TempData.Keep("ValidatedCourses");
        }
    }

    // Add Course Handler
    public async Task<IActionResult> OnPostAddCourseAsync()
    {
        if (!ModelState.IsValid)
        {
            ErrorMessage = "Please correct the errors and try again.";
            return Page();
        }

        // Check if course code already exists
        var codeExists = await _context.Courses.AnyAsync(c => c.Code == NewCourse.Code);
        if (codeExists)
        {
            ErrorMessage = $"Course code '{NewCourse.Code}' already exists.";
            return Page();
        }

        // Create course
        var course = new Course
        {
            Code = NewCourse.Code!,
            Title = NewCourse.Title!,
            CreditHours = NewCourse.CreditHours!.Value,
            IsLab = NewCourse.IsLab
        };

        _context.Courses.Add(course);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Course '{course.Code} - {course.Title}' added successfully!";
        return RedirectToPage(new { tab = "all" });
    }

    // Delete Course Handler
    public async Task<IActionResult> OnPostDeleteCourseAsync(int id)
    {
        try
        {
            // Get TeacherCourse IDs first
            var teacherCourseIds = await _context.TeacherCourses
                .Where(tc => tc.CourseId == id)
                .Select(tc => tc.Id)
                .ToListAsync();

            if (teacherCourseIds.Any())
            {
                // Get TimetableRule IDs
                var timetableRuleIds = await _context.TimetableRules
                    .Where(tr => teacherCourseIds.Contains(tr.TeacherCourseId))
                    .Select(tr => tr.Id)
                    .ToListAsync();

                if (timetableRuleIds.Any())
                {
                    // Get Lecture IDs
                    var lectureIds = await _context.Lectures
                        .Where(l => timetableRuleIds.Contains(l.TimetableRuleId))
                        .Select(l => l.Id)
                        .ToListAsync();

                    if (lectureIds.Any())
                    {
                        // 1. Delete AttendanceRecords
                        await _context.AttendanceRecords
                            .Where(ar => lectureIds.Contains(ar.LectureId))
                            .ExecuteDeleteAsync();

                        // 2. Delete Lectures
                        await _context.Lectures
                            .Where(l => timetableRuleIds.Contains(l.TimetableRuleId))
                            .ExecuteDeleteAsync();
                    }

                    // 3. Delete TimetableRules
                    await _context.TimetableRules
                        .Where(tr => teacherCourseIds.Contains(tr.TeacherCourseId))
                        .ExecuteDeleteAsync();
                }

                // 4. Delete TeacherCourses
                await _context.TeacherCourses
                    .Where(tc => tc.CourseId == id)
                    .ExecuteDeleteAsync();
            }

            // 5. Delete the Course
            var course = await _context.Courses.FindAsync(id);
            if (course != null)
            {
                var courseName = $"{course.Code} - {course.Title}";
                _context.Courses.Remove(course);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Course '{courseName}' deleted successfully!";
            }
            else
            {
                TempData["ErrorMessage"] = "Course not found.";
            }

            // Clear TempData to prevent stale data
            TempData.Remove("ValidationResult");
            TempData.Remove("ValidatedCourses");
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error deleting course: {ex.Message}";
        }

        return RedirectToPage(new { tab = "all" });
    }

    // Validate Import Handler
    public async Task<IActionResult> OnPostValidateImportAsync()
    {
        // Clear old validation results
        TempData.Remove("ValidationResult");
        TempData.Remove("ValidatedCourses");

        if (ImportFile == null || ImportFile.Length == 0)
        {
            TempData["ErrorMessage"] = "Please select a file to upload.";
            return RedirectToPage(new { tab = "import" });
        }

        var validatedCourses = new List<ImportedCourse>();
        var errors = new List<ValidationError>();
        int lineNumber = 1;

        try
        {
            using var stream = ImportFile.OpenReadStream();
            using var reader = new StreamReader(stream);

            // Skip header
            var header = await reader.ReadLineAsync();
            lineNumber++;

            // Read all existing course codes
            var existingCodes = await _context.Courses.Select(c => c.Code.ToLower()).ToListAsync();
            var codesInFile = new HashSet<string>();

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var columns = line.Split(',').Select(c => c.Trim()).ToArray();

                if (columns.Length < 4)
                {
                    errors.Add(new ValidationError { LineNumber = lineNumber, Message = "Invalid format - expected 4 columns (Code, Title, CreditHours, IsLab)" });
                    lineNumber++;
                    continue;
                }

                var imported = new ImportedCourse
                {
                    LineNumber = lineNumber,
                    Code = columns[0],
                    Title = columns[1],
                    CreditHoursStr = columns[2],
                    IsLabStr = columns[3]
                };

                // Validation
                if (string.IsNullOrWhiteSpace(imported.Code))
                {
                    errors.Add(new ValidationError { LineNumber = lineNumber, Message = "Course code is required" });
                }
                else if (existingCodes.Contains(imported.Code.ToLower()))
                {
                    errors.Add(new ValidationError { LineNumber = lineNumber, Message = $"Course code '{imported.Code}' already exists in database" });
                }
                else if (codesInFile.Contains(imported.Code.ToLower()))
                {
                    errors.Add(new ValidationError { LineNumber = lineNumber, Message = $"Duplicate course code '{imported.Code}' in file" });
                }
                else
                {
                    codesInFile.Add(imported.Code.ToLower());
                }

                if (string.IsNullOrWhiteSpace(imported.Title))
                {
                    errors.Add(new ValidationError { LineNumber = lineNumber, Message = "Course title is required" });
                }

                if (!int.TryParse(imported.CreditHoursStr, out int credits) || credits < 1 || credits > 6)
                {
                    errors.Add(new ValidationError { LineNumber = lineNumber, Message = $"Invalid credit hours '{imported.CreditHoursStr}' (must be 1-6)" });
                }
                else
                {
                    imported.CreditHours = credits;
                }

                if (!bool.TryParse(imported.IsLabStr, out bool isLab))
                {
                    // Try alternate formats
                    var isLabLower = imported.IsLabStr.ToLower();
                    if (isLabLower == "yes" || isLabLower == "lab" || isLabLower == "1")
                    {
                        imported.IsLab = true;
                    }
                    else if (isLabLower == "no" || isLabLower == "theory" || isLabLower == "0")
                    {
                        imported.IsLab = false;
                    }
                    else
                    {
                        errors.Add(new ValidationError { LineNumber = lineNumber, Message = $"Invalid lab status '{imported.IsLabStr}' (use true/false, yes/no, lab/theory, or 1/0)" });
                        lineNumber++;
                        continue;
                    }
                }
                else
                {
                    imported.IsLab = isLab;
                }

                validatedCourses.Add(imported);
                lineNumber++;
            }

            var result = new ImportResult
            {
                TotalRows = lineNumber - 2,
                ValidCount = validatedCourses.Count - errors.Count,
                ErrorCount = errors.Count,
                Errors = errors
            };

            // Store in TempData
            TempData["ValidationResult"] = JsonSerializer.Serialize(result);
            TempData["ValidatedCourses"] = JsonSerializer.Serialize(validatedCourses.Where(c => 
                !errors.Any(e => e.LineNumber == c.LineNumber)).ToList());
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error processing file: {ex.Message}";
        }

        return RedirectToPage(new { tab = "import" });
    }

    // Import Courses Handler
    public async Task<IActionResult> OnPostImportCoursesAsync()
    {
        var validatedCoursesJson = TempData["ValidatedCourses"] as string;

        if (string.IsNullOrEmpty(validatedCoursesJson))
        {
            TempData["ErrorMessage"] = "No validated data found. Please validate your file first.";
            return RedirectToPage(new { tab = "import" });
        }

        var validatedCourses = JsonSerializer.Deserialize<List<ImportedCourse>>(validatedCoursesJson);

        if (validatedCourses == null || !validatedCourses.Any())
        {
            TempData["ErrorMessage"] = "No valid courses to import.";
            return RedirectToPage(new { tab = "import" });
        }

        try
        {
            int successCount = 0;

            foreach (var imported in validatedCourses)
            {
                var course = new Course
                {
                    Code = imported.Code!,
                    Title = imported.Title!,
                    CreditHours = imported.CreditHours!.Value,
                    IsLab = imported.IsLab!.Value
                };

                _context.Courses.Add(course);
                successCount++;
            }

            await _context.SaveChangesAsync();

            // Clear validation data
            TempData.Remove("ValidationResult");
            TempData.Remove("ValidatedCourses");

            TempData["SuccessMessage"] = $"Successfully imported {successCount} courses!";
            return RedirectToPage(new { tab = "all" });
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error importing courses: {ex.Message}";
            return RedirectToPage(new { tab = "import" });
        }
    }

    // Clear Validation Handler
    public IActionResult OnPostClearValidation()
    {
        TempData.Remove("ValidationResult");
        TempData.Remove("ValidatedCourses");
        return RedirectToPage(new { tab = "import" });
    }
}

// Supporting Classes
public class CourseInputModel
{
    public string? Code { get; set; }
    public string? Title { get; set; }
    public int? CreditHours { get; set; }
    public bool IsLab { get; set; }
}

public class ImportedCourse
{
    public int LineNumber { get; set; }
    public string? Code { get; set; }
    public string? Title { get; set; }
    public string? CreditHoursStr { get; set; }
    public int? CreditHours { get; set; }
    public string? IsLabStr { get; set; }
    public bool? IsLab { get; set; }
}

public class ImportResult
{
    public int TotalRows { get; set; }
    public int ValidCount { get; set; }
    public int ErrorCount { get; set; }
    public List<ValidationError> Errors { get; set; } = new();
    public bool IsValid => ErrorCount == 0;
}

public class ValidationError
{
    public int LineNumber { get; set; }
    public string Message { get; set; } = string.Empty;
}
