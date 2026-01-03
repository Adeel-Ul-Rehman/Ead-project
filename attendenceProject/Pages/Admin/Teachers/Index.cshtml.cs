using attendence.Data.Data;
using attendence.Domain.Entities;
using attendence.Services.Helpers;
using attendence.Services.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.IO;

namespace attendenceProject.Pages.Admin.Teachers;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly PasswordHasher _passwordHasher;
    private readonly EmailService _emailService;

    public IndexModel(ApplicationDbContext context, PasswordHasher passwordHasher, EmailService emailService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
    }

    // Statistics
    public int TotalTeachers { get; set; }
    public int TotalAssignments { get; set; }
    public int ActiveTeachers { get; set; }
    public int SectionsCovered { get; set; }

    // Tab state
    public string? ActiveTab { get; set; }

    // All Teachers Tab
    public List<User> Teachers { get; set; } = new();
    public Dictionary<int, List<TeacherCourse>> TeacherAssignments { get; set; } = new();
    public string? SearchTerm { get; set; }
    public string? DesignationFilter { get; set; }

    // Add Teacher Tab
    [BindProperty]
    public TeacherInputModel NewTeacher { get; set; } = new();

    // Import Tab
    [BindProperty]
    public IFormFile? ImportFile { get; set; }
    public ImportResult? ValidationResult { get; set; }
    public List<ImportedTeacher> ValidatedTeachers { get; set; } = new();

    // Assign Courses Tab
    public List<SelectListItem> TeacherOptions { get; set; } = new();
    public List<SelectListItem> CourseOptions { get; set; } = new();
    public List<SelectListItem> BadgeOptions { get; set; } = new();
    public List<SelectListItem> SectionOptions { get; set; } = new();
    public List<object> AllSectionsForFiltering { get; set; } = new();
    public List<TeacherCourse> Assignments { get; set; } = new();
    [BindProperty]
    public int? SelectedTeacherId { get; set; }
    [BindProperty]
    public int? SelectedCourseId { get; set; }
    [BindProperty]
    public List<int>? SelectedSectionIds { get; set; }
    public string? AssignmentSearchTerm { get; set; }

    public async Task OnGetAsync(string? tab, string? searchTerm, string? designation, string? assignmentSearch)
    {
        ActiveTab = tab ?? "all";
        SearchTerm = searchTerm;
        DesignationFilter = designation;
        AssignmentSearchTerm = assignmentSearch;

        await LoadStatistics();

        if (ActiveTab == "all")
        {
            await LoadAllTeachers();
        }
        else if (ActiveTab == "add")
        {
            // Just render the form
        }
        else if (ActiveTab == "import")
        {
            // Load validation results from TempData if available
            var validationResultJson = TempData["ValidationResult"] as string;
            var validatedTeachersJson = TempData["ValidatedTeachers"] as string;
            
            if (!string.IsNullOrEmpty(validationResultJson))
            {
                ValidationResult = System.Text.Json.JsonSerializer.Deserialize<ImportResult>(validationResultJson);
                // Keep for one more request (for the import action)
                TempData.Keep("ValidationResult");
            }
            
            if (!string.IsNullOrEmpty(validatedTeachersJson))
            {
                ValidatedTeachers = System.Text.Json.JsonSerializer.Deserialize<List<ImportedTeacher>>(validatedTeachersJson) ?? new();
                // Keep for one more request (for the import action)
                TempData.Keep("ValidatedTeachers");
            }
        }
        else if (ActiveTab == "assign")
        {
            await LoadAssignmentData();
        }
    }

    private async Task LoadStatistics()
    {
        TotalTeachers = await _context.Users.CountAsync(u => u.Role == "Teacher");
        TotalAssignments = await _context.TeacherCourses.CountAsync();
        ActiveTeachers = await _context.TeacherCourses.Select(tc => tc.TeacherId).Distinct().CountAsync();
        SectionsCovered = await _context.TeacherCourses.Select(tc => tc.SectionId).Distinct().CountAsync();
    }

    private async Task LoadAllTeachers()
    {
        var query = _context.Users
            .Include(u => u.Teacher)
            .Where(u => u.Role == "Teacher");

        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            query = query.Where(u => u.FullName.Contains(SearchTerm) || u.Email.Contains(SearchTerm));
        }

        if (!string.IsNullOrWhiteSpace(DesignationFilter))
        {
            query = query.Where(u => u.Teacher!.Designation == DesignationFilter);
        }

        Teachers = await query
            .OrderBy(u => u.FullName)
            .ToListAsync();

        // Load assignments for each teacher
        var teacherIds = Teachers.Where(t => t.Teacher != null).Select(t => t.Teacher!.Id).ToList();
        var assignments = await _context.TeacherCourses
            .Include(tc => tc.Course)
            .Include(tc => tc.Section)
                .ThenInclude(s => s.Badge)
            .Where(tc => teacherIds.Contains(tc.TeacherId))
            .ToListAsync();

        TeacherAssignments = assignments.GroupBy(tc => tc.TeacherId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    private async Task LoadAssignmentData()
    {
        // Load teachers
        var teachers = await _context.Teachers
            .Include(t => t.User)
            .OrderBy(t => t.User.FullName)
            .ToListAsync();

        TeacherOptions = teachers.Select(t => new SelectListItem
        {
            Value = t.Id.ToString(),
            Text = $"{t.User.FullName} ({t.Designation})"
        }).ToList();

        // Load courses
        var courses = await _context.Courses.OrderBy(c => c.Code).ToListAsync();
        CourseOptions = courses.Select(c => new SelectListItem
        {
            Value = c.Id.ToString(),
            Text = $"{c.Code} - {c.Title}"
        }).ToList();

        // Load badges for cascading filter
        var badges = await _context.Badges.OrderBy(b => b.Name).ToListAsync();
        BadgeOptions = badges.Select(b => new SelectListItem
        {
            Value = b.Id.ToString(),
            Text = b.Name
        }).ToList();

        // Load all sections with badge info for JavaScript filtering
        var sections = await _context.Sections
            .Include(s => s.Badge)
            .OrderBy(s => s.Badge.Name)
            .ThenBy(s => s.Semester)
            .ThenBy(s => s.Session)
            .ThenBy(s => s.Name)
            .ToListAsync();

        // Prepare section data for JavaScript (cascading filters)
        AllSectionsForFiltering = sections.Select(s => new
        {
            id = s.Id,
            name = s.Name,
            badgeId = s.BadgeId,
            badgeName = s.Badge.Name,
            semester = s.Semester,
            session = s.Session
        }).Cast<object>().ToList();

        // Load existing assignments
        var assignmentQuery = _context.TeacherCourses
            .Include(tc => tc.Teacher)
                .ThenInclude(t => t.User)
            .Include(tc => tc.Course)
            .Include(tc => tc.Section)
                .ThenInclude(s => s.Badge)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(AssignmentSearchTerm))
        {
            assignmentQuery = assignmentQuery.Where(tc =>
                tc.Teacher.User.FullName.Contains(AssignmentSearchTerm) ||
                tc.Course.Code.Contains(AssignmentSearchTerm) ||
                tc.Course.Title.Contains(AssignmentSearchTerm));
        }

        Assignments = await assignmentQuery
            .OrderBy(tc => tc.Teacher.User.FullName)
            .ThenBy(tc => tc.Course.Code)
            .ToListAsync();
    }

    // Clear validation results handler
    public IActionResult OnPostClearValidation()
    {
        // Clear TempData
        TempData.Remove("ValidationResult");
        TempData.Remove("ValidatedTeachers");
        return RedirectToPage(new { tab = "import" });
    }

    // Add Teacher Action
    public async Task<IActionResult> OnPostAddTeacherAsync()
    {
        if (!ModelState.IsValid)
        {
            ActiveTab = "add";
            await LoadStatistics();
            TempData["ErrorMessage"] = "Please correct the errors in the form.";
            return Page();
        }

        // Check for duplicate email
        if (await _context.Users.AnyAsync(u => u.Email == NewTeacher.Email))
        {
            ActiveTab = "add";
            await LoadStatistics();
            TempData["ErrorMessage"] = "A user with this email already exists.";
            return Page();
        }

        // Generate password
        var password = attendence.Services.Helpers.PasswordGenerator.GeneratePassword();
        var hashedPassword = _passwordHasher.HashPassword(password);

        // Create user
        var user = new User
        {
            FullName = NewTeacher.FullName,
            Email = NewTeacher.Email,
            PasswordHash = hashedPassword,
            Role = "Teacher",
            IsActive = true,
            CreatedAt = DateTime.Now
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Create teacher record
        var teacherEntity = new attendence.Domain.Entities.Teacher
        {
            UserId = user.Id,
            BadgeNumber = NewTeacher.BadgeNumber,
            Designation = NewTeacher.Designation
        };

        _context.Teachers.Add(teacherEntity);
        await _context.SaveChangesAsync();

        // Send welcome email
        try
        {
            var loginUrl = $"{Request.Scheme}://{Request.Host}/Account/Login";
            await _emailService.SendCredentialsEmailAsync(
                user.Email, 
                user.FullName, 
                password, 
                "Teacher", 
                loginUrl
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send email: {ex.Message}");
        }

        TempData["SuccessMessage"] = $"Teacher {user.FullName} added successfully! Login credentials have been sent via email.";
        return RedirectToPage(new { tab = "all" });
    }

    // Import Teachers Validation - Step 1
    public async Task<IActionResult> OnPostValidateImportAsync()
    {
        // Clear any previous validation results
        TempData.Remove("ValidationResult");
        TempData.Remove("ValidatedTeachers");
        ValidationResult = null;
        ValidatedTeachers = new();

        if (ImportFile == null || ImportFile.Length == 0)
        {
            TempData["ErrorMessage"] = "Please select a file to upload.";
            return RedirectToPage(new { tab = "import" });
        }

        var fileExtension = Path.GetExtension(ImportFile.FileName).ToLower();
        if (fileExtension != ".csv" && fileExtension != ".xlsx")
        {
            TempData["ErrorMessage"] = "Only CSV and XLSX files are supported.";
            return RedirectToPage(new { tab = "import" });
        }

        var errors = new List<string>();
        var importedTeachers = new List<ImportedTeacher>();

        try
        {
            using (var stream = ImportFile.OpenReadStream())
            {
                if (fileExtension == ".csv")
                {
                    using (var reader = new StreamReader(stream))
                    {
                        await reader.ReadLineAsync(); // Skip header
                        int lineNumber = 1;

                        while (!reader.EndOfStream)
                        {
                            lineNumber++;
                            var line = await reader.ReadLineAsync();
                            if (string.IsNullOrWhiteSpace(line)) continue;

                            var values = line.Split(',').Select(v => v.Trim()).ToArray();
                            
                            if (values.Length < 4)
                            {
                                errors.Add($"Line {lineNumber}: Invalid format - Expected 4 columns");
                                continue;
                            }

                            importedTeachers.Add(new ImportedTeacher
                            {
                                FullName = values[0],
                                Email = values[1],
                                BadgeNumber = values[2],
                                Designation = values[3],
                                LineNumber = lineNumber
                            });
                        }
                    }
                }
                else // Excel
                {
                    using (var package = new OfficeOpenXml.ExcelPackage(stream))
                    {
                        var worksheet = package.Workbook.Worksheets[0];
                        var rowCount = worksheet.Dimension?.Rows ?? 0;

                        for (int row = 2; row <= rowCount; row++)
                        {
                            var fullName = worksheet.Cells[row, 1].Value?.ToString()?.Trim() ?? "";
                            var email = worksheet.Cells[row, 2].Value?.ToString()?.Trim() ?? "";
                            var badgeNumber = worksheet.Cells[row, 3].Value?.ToString()?.Trim() ?? "";
                            var designation = worksheet.Cells[row, 4].Value?.ToString()?.Trim() ?? "";

                            importedTeachers.Add(new ImportedTeacher
                            {
                                FullName = fullName,
                                Email = email,
                                BadgeNumber = badgeNumber,
                                Designation = designation,
                                LineNumber = row
                            });
                        }
                    }
                }
            }

            // Validate against database
            var existingEmails = await _context.Users.Select(u => u.Email).ToListAsync();
            var existingBadgeNumbers = await _context.Teachers.Select(t => t.BadgeNumber).ToListAsync();

            var validTeachers = new List<ImportedTeacher>();
            foreach (var teacher in importedTeachers)
            {
                bool hasError = false;

                // Check required fields
                if (string.IsNullOrWhiteSpace(teacher.FullName) || string.IsNullOrWhiteSpace(teacher.Email) ||
                    string.IsNullOrWhiteSpace(teacher.BadgeNumber) || string.IsNullOrWhiteSpace(teacher.Designation))
                {
                    errors.Add($"Line {teacher.LineNumber}: Missing required fields");
                    hasError = true;
                }

                // Check duplicate email
                if (existingEmails.Contains(teacher.Email))
                {
                    errors.Add($"Line {teacher.LineNumber}: Email '{teacher.Email}' already exists");
                    hasError = true;
                }

                // Check duplicate badge number
                if (existingBadgeNumbers.Contains(teacher.BadgeNumber))
                {
                    errors.Add($"Line {teacher.LineNumber}: Badge number '{teacher.BadgeNumber}' already exists");
                    hasError = true;
                }

                if (!hasError)
                {
                    validTeachers.Add(teacher);
                }
            }

            ValidationResult = new ImportResult
            {
                ValidCount = validTeachers.Count,
                ErrorCount = errors.Count,
                Errors = errors
            };
            ValidatedTeachers = validTeachers;

            // Store in TempData for next request
            TempData["ValidationResult"] = System.Text.Json.JsonSerializer.Serialize(ValidationResult);
            TempData["ValidatedTeachers"] = System.Text.Json.JsonSerializer.Serialize(ValidatedTeachers);

            ActiveTab = "import";
            return Page();
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Validation failed: {ex.Message}";
            return RedirectToPage(new { tab = "import" });
        }
    }

    // Import Teachers Action - Step 2
    public async Task<IActionResult> OnPostImportTeachersAsync(bool sendWelcomeEmails = true)
    {
        // Get validated teachers from TempData
        var validatedTeachersJson = TempData["ValidatedTeachers"] as string;
        
        if (string.IsNullOrEmpty(validatedTeachersJson))
        {
            TempData["ErrorMessage"] = "No validated data found. Please validate your file first.";
            return RedirectToPage(new { tab = "import" });
        }

        var validatedTeachers = System.Text.Json.JsonSerializer.Deserialize<List<ImportedTeacher>>(validatedTeachersJson);
        
        if (validatedTeachers == null || !validatedTeachers.Any())
        {
            TempData["ErrorMessage"] = "No valid teachers to import.";
            return RedirectToPage(new { tab = "import" });
        }

        try
        {
            int successCount = 0;
            var errors = new List<string>();

            foreach (var imported in validatedTeachers)
            {
                // Create user and teacher
                var password = attendence.Services.Helpers.PasswordGenerator.GeneratePassword();
                var hashedPassword = _passwordHasher.HashPassword(password);

                var user = new User
                {
                    FullName = imported.FullName,
                    Email = imported.Email,
                    PasswordHash = hashedPassword,
                    Role = "Teacher",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                var teacherEntity = new attendence.Domain.Entities.Teacher
                {
                    UserId = user.Id,
                    BadgeNumber = imported.BadgeNumber,
                    Designation = imported.Designation
                };

                _context.Teachers.Add(teacherEntity);
                await _context.SaveChangesAsync();

                successCount++;
            }

            // Clear TempData after successful import
            TempData.Remove("ValidationResult");
            TempData.Remove("ValidatedTeachers");

            if (successCount > 0)
            {
                TempData["SuccessMessage"] = $"Successfully imported {successCount} teacher(s).";
            }

            if (errors.Any())
            {
                TempData["WarningMessage"] = $"Import completed with {errors.Count} error(s): " + string.Join("; ", errors.Take(5));
            }

            return RedirectToPage(new { tab = "all" });
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Import failed: {ex.Message}";
            return RedirectToPage(new { tab = "import" });
        }
    }

    // Assign Courses Action

    // Delete Teacher Action
    public async Task<IActionResult> OnPostDeleteTeacherAsync(int id)
    {
        try
        {
            var user = await _context.Users
                .Include(u => u.Teacher)
                .FirstOrDefaultAsync(u => u.Id == id && u.Role == "Teacher");

            if (user == null || user.Teacher == null)
            {
                TempData["ErrorMessage"] = "Teacher not found.";
                return RedirectToPage(new { tab = "all" });
            }

            // Delete all related records in order
            var teacherId = user.Teacher.Id;

            // Get TeacherCourse IDs first (needed for TimetableRules)
            var teacherCourseIds = await _context.TeacherCourses
                .Where(tc => tc.TeacherId == teacherId)
                .Select(tc => tc.Id)
                .ToListAsync();

            // Use ExecuteDeleteAsync for better performance (EF Core 7+)
            // 1. Delete TimetableRules that reference TeacherCourses
            if (teacherCourseIds.Any())
            {
                await _context.TimetableRules
                    .Where(tr => teacherCourseIds.Contains(tr.TeacherCourseId))
                    .ExecuteDeleteAsync();
            }

            // 2. Delete TeacherCourse assignments
            await _context.TeacherCourses
                .Where(tc => tc.TeacherId == teacherId)
                .ExecuteDeleteAsync();

            // 3. Delete AttendanceEditRequests
            await _context.AttendanceEditRequests
                .Where(aer => aer.TeacherId == teacherId)
                .ExecuteDeleteAsync();

            // 4. Delete AttendanceExtensionRequests
            await _context.AttendanceExtensionRequests
                .Where(aer => aer.TeacherId == teacherId)
                .ExecuteDeleteAsync();

            // 5. Update Lectures created by this teacher (set CreatedByTeacherId to null)
            await _context.Lectures
                .Where(l => l.CreatedByTeacherId == teacherId)
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.CreatedByTeacherId, (int?)null));

            // 6. Finally, delete Teacher and User
            _context.Teachers.Remove(user.Teacher);
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Teacher {user.FullName} and all related records deleted successfully.";
            return RedirectToPage(new { tab = "all" });
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error deleting teacher: {ex.Message}";
            return RedirectToPage(new { tab = "all" });
        }
    }

    // Assign Courses Action
    public async Task<IActionResult> OnPostAssignCoursesAsync()
    {
        if (!SelectedTeacherId.HasValue || !SelectedCourseId.HasValue || SelectedSectionIds == null || !SelectedSectionIds.Any())
        {
            TempData["ErrorMessage"] = "Please select teacher, course, and at least one section.";
            return RedirectToPage(new { tab = "assign" });
        }

        var addedCount = 0;
        var skippedCount = 0;

        foreach (var sectionId in SelectedSectionIds)
        {
            // Check if assignment already exists
            var exists = await _context.TeacherCourses
                .AnyAsync(tc => tc.TeacherId == SelectedTeacherId.Value &&
                               tc.CourseId == SelectedCourseId.Value &&
                               tc.SectionId == sectionId);

            if (exists)
            {
                skippedCount++;
                continue;
            }

            var assignment = new TeacherCourse
            {
                TeacherId = SelectedTeacherId.Value,
                CourseId = SelectedCourseId.Value,
                SectionId = sectionId,
                AssignedAt = DateTime.Now
            };

            _context.TeacherCourses.Add(assignment);
            addedCount++;
        }

        await _context.SaveChangesAsync();

        var message = $"Added {addedCount} assignment(s)";
        if (skippedCount > 0)
        {
            message += $", skipped {skippedCount} duplicate(s)";
        }

        TempData["SuccessMessage"] = message + ".";
        return RedirectToPage(new { tab = "assign" });
    }

    // Delete Assignment Action
    public async Task<IActionResult> OnPostDeleteAssignmentAsync(int id)
    {
        var assignment = await _context.TeacherCourses.FindAsync(id);
        if (assignment == null)
        {
            TempData["ErrorMessage"] = "Assignment not found.";
            return RedirectToPage(new { tab = "assign" });
        }

        _context.TeacherCourses.Remove(assignment);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Assignment removed successfully.";
        return RedirectToPage(new { tab = "assign" });
    }

    public class TeacherInputModel
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string BadgeNumber { get; set; } = string.Empty;
        public string Designation { get; set; } = string.Empty;
    }

    public class ImportResult
    {
        public int ValidCount { get; set; }
        public int ErrorCount { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class ImportedTeacher
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string BadgeNumber { get; set; } = string.Empty;
        public string Designation { get; set; } = string.Empty;
        public int LineNumber { get; set; }
    }
}
