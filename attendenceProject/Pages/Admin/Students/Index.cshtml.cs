using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using attendence.Data.Data;
using attendence.Domain.Entities;
using attendence.Services.Services;
using attendence.Services.Helpers;
using System.Text.Json;
using OfficeOpenXml;

namespace attendenceProject.Pages.Admin.Students;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly EmailService _emailService;
    private readonly PasswordHasher _passwordHasher;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(ApplicationDbContext context, EmailService emailService, PasswordHasher passwordHasher, ILogger<IndexModel> logger)
    {
        _context = context;
        _emailService = emailService;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    // General
    public string ActiveTab { get; set; } = "all";
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    // Statistics
    public int TotalStudents { get; set; }
    public int ActiveStudents { get; set; }
    public int TotalSections { get; set; }
    public int TotalBadges { get; set; }

    // All Students Tab
    public List<StudentWithDetails> Students { get; set; } = new();
    public string? SearchTerm { get; set; }
    public string? BadgeFilter { get; set; }
    public string? SemesterFilter { get; set; }
    public string? SessionFilter { get; set; }
    
    // Pagination
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalPages { get; set; }
    public int TotalRecords { get; set; }

    // Add Student Tab
    [BindProperty]
    public StudentInputModel NewStudent { get; set; } = new();

    // Import Tab
    [BindProperty]
    public IFormFile? ImportFile { get; set; }
    public ImportResult? ValidationResult { get; set; }
    public List<ImportedStudent> ValidatedStudents { get; set; } = new();

    public ImportResult? ImportValidation { get; set; }

    // Filters
    public List<SelectListItem> BadgeOptions { get; set; } = new();
    public List<object> AllSectionsForFiltering { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string? tab, string? searchTerm, string? badge, string? semester, string? session, int page = 1, int pageSize = 20)
    {
        ActiveTab = tab ?? "all";
        SearchTerm = searchTerm;
        BadgeFilter = badge;
        SemesterFilter = semester;
        SessionFilter = session;
        CurrentPage = page;
        PageSize = pageSize;

        await LoadStatistics();

        if (ActiveTab == "all")
        {
            await LoadAllStudents();
            
            // For AJAX requests, return JSON
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return new JsonResult(new
                {
                    items = Students.Select(s => new
                    {
                        id = s.Student.Id,
                        userId = s.User.Id,
                        fullName = s.User.FullName,
                        email = s.User.Email,
                        rollNo = s.Student.RollNo,
                        fatherName = s.Student.FatherName,
                        badgeName = s.Badge.Name,
                        sectionName = s.Section.Name,
                        semester = s.Section.Semester,
                        session = s.Section.Session,
                        isActive = s.User.IsActive
                    }),
                    currentPage = CurrentPage,
                    pageSize = PageSize,
                    totalPages = TotalPages,
                    totalRecords = TotalRecords
                });
            }
        }
        else if (ActiveTab == "add")
        {
            await LoadAddStudentData();
        }
        else if (ActiveTab == "import")
        {
            // Load validation results from TempData if available
            var validationResultJson = TempData["ValidationResult"] as string;
            var validatedStudentsJson = TempData["ValidatedStudents"] as string;
            
            if (!string.IsNullOrEmpty(validationResultJson))
            {
                ValidationResult = System.Text.Json.JsonSerializer.Deserialize<ImportResult>(validationResultJson);
                // Keep for one more request (for the import action)
                TempData.Keep("ValidationResult");
            }
            
            if (!string.IsNullOrEmpty(validatedStudentsJson))
            {
                ValidatedStudents = System.Text.Json.JsonSerializer.Deserialize<List<ImportedStudent>>(validatedStudentsJson) ?? new();
                // Keep for one more request (for the import action)
                TempData.Keep("ValidatedStudents");
            }
        }
        
        return Page();
    }

    private async Task LoadStatistics()
    {
        TotalStudents = await _context.Users.CountAsync(u => u.Role == "Student");
        ActiveStudents = await _context.Users.CountAsync(u => u.Role == "Student" && u.IsActive);
        TotalSections = await _context.Sections.CountAsync();
        TotalBadges = await _context.Badges.CountAsync();
    }

    private async Task LoadAllStudents()
    {
        var query = _context.Students
            .Include(s => s.User)
            .Include(s => s.Section)
                .ThenInclude(sec => sec.Badge)
            .AsQueryable();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            query = query.Where(s => 
                s.User.FullName.Contains(SearchTerm) ||
                s.User.Email.Contains(SearchTerm) ||
                s.RollNo.Contains(SearchTerm));
        }

        if (!string.IsNullOrWhiteSpace(BadgeFilter))
        {
            query = query.Where(s => s.Section.Badge.Name == BadgeFilter);
        }

        if (!string.IsNullOrWhiteSpace(SemesterFilter) && int.TryParse(SemesterFilter, out int semester))
        {
            query = query.Where(s => s.Section.Semester == semester);
        }

        if (!string.IsNullOrWhiteSpace(SessionFilter))
        {
            query = query.Where(s => s.Section.Session == SessionFilter);
        }

        // Get total count for pagination
        TotalRecords = await query.CountAsync();
        TotalPages = (int)Math.Ceiling(TotalRecords / (double)PageSize);

        // Apply pagination
        var students = await query
            .OrderBy(s => s.User.FullName)
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        Students = students.Select(s => new StudentWithDetails
        {
            Student = s,
            User = s.User,
            Section = s.Section,
            Badge = s.Section.Badge
        }).ToList();
    }

    private async Task LoadAddStudentData()
    {
        NewStudent.BadgeOptions = await _context.Badges
            .OrderBy(b => b.Name)
            .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name })
            .ToListAsync();

        // Load all sections for cascading dropdowns
        AllSectionsForFiltering = await _context.Sections
            .Include(s => s.Badge)
            .OrderBy(s => s.Badge.Name)
            .ThenBy(s => s.Semester)
            .ThenBy(s => s.Session)
            .ThenBy(s => s.Name)
            .Select(s => new 
            {
                id = s.Id,
                name = s.Name,
                badgeId = s.BadgeId,
                badgeName = s.Badge.Name,
                semester = s.Semester,
                session = s.Session
            })
            .ToListAsync<object>();
    }

    // Add Student Handler
    public async Task<IActionResult> OnPostAddStudentAsync()
    {
        try
        {
            if (!ModelState.IsValid)
            {
                ErrorMessage = "Please correct the errors and try again.";
                
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return new JsonResult(new { success = false, message = ErrorMessage });
                }
                
                await LoadAddStudentData();
                return Page();
            }

            // Check if email already exists
            var emailExists = await _context.Users.AnyAsync(u => u.Email == NewStudent.Email);
            if (emailExists)
            {
                ErrorMessage = $"Email {NewStudent.Email} is already registered.";
                
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return new JsonResult(new { success = false, message = ErrorMessage });
                }
                
                await LoadAddStudentData();
                return Page();
            }

            // Check if roll number already exists
            var rollNoExists = await _context.Students.AnyAsync(s => s.RollNo == NewStudent.RollNo);
            if (rollNoExists)
            {
                ErrorMessage = $"Roll number {NewStudent.RollNo} is already registered.";
                
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return new JsonResult(new { success = false, message = ErrorMessage });
                }
                
                await LoadAddStudentData();
                return Page();
            }

        // Generate password
        var password = PasswordGenerator.GeneratePassword();
        var passwordHash = _passwordHasher.HashPassword(password);

        // Create user account
        var user = new User
        {
            Email = NewStudent.Email!,
            FullName = NewStudent.FullName!,
            Role = "Student",
            PasswordHash = passwordHash,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Create student record
        var student = new attendence.Domain.Entities.Student
        {
            UserId = user.Id,
            RollNo = NewStudent.RollNo!,
            FatherName = NewStudent.FatherName,
            SectionId = NewStudent.SectionId!.Value
        };

        _context.Students.Add(student);
        await _context.SaveChangesAsync();

        // Send welcome email
        if (NewStudent.SendWelcomeEmail)
        {
            try
            {
                await _emailService.SendCredentialsEmailAsync(
                    user.Email,
                    user.FullName,
                    user.Email,
                    password,
                    "Student"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send welcome email");
            }
        }

        // Load section and badge details for response
        var section = await _context.Sections
            .Include(s => s.Badge)
            .FirstOrDefaultAsync(s => s.Id == student.SectionId);

        // For AJAX requests, return JSON with student data
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return new JsonResult(new
            {
                success = true,
                message = $"Student {user.FullName} added successfully!",
                student = new
                {
                    id = student.Id,
                    userId = user.Id,
                    fullName = user.FullName,
                    email = user.Email,
                    rollNo = student.RollNo,
                    fatherName = student.FatherName,
                    badgeName = section?.Badge?.Name ?? "",
                    sectionName = section?.Name ?? "",
                    semester = section?.Semester ?? 0,
                    session = section?.Session ?? "",
                    isActive = user.IsActive
                }
            });
        }

        SuccessMessage = $"Student {user.FullName} added successfully! Login credentials have been sent to {user.Email}.";
        return RedirectToPage(new { tab = "all" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding student");
            ErrorMessage = "An error occurred while adding the student. Please try again.";
            
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return new JsonResult(new { success = false, message = ErrorMessage });
            }
            
            await LoadAddStudentData();
            return Page();
        }
    }

    // Delete Student Handler
    public async Task<IActionResult> OnPostDeleteStudentAsync(int id)
    {
        try
        {
            var user = await _context.Users
                .Include(u => u.Student)
                .FirstOrDefaultAsync(u => u.Id == id && u.Role == "Student");

            if (user == null || user.Student == null)
            {
                var errorMsg = "Student not found.";
                
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return new JsonResult(new { success = false, message = errorMsg });
                }
                
                ErrorMessage = errorMsg;
                return RedirectToPage(new { tab = "all" });
            }

            var studentName = user.FullName;
            
            // Use ExecuteDeleteAsync for better performance - deletes directly in database
            await _context.AttendanceRecords
                .Where(ar => ar.StudentId == user.Student.Id)
                .ExecuteDeleteAsync();

            // Remove student record
            _context.Students.Remove(user.Student);

            // Remove user account
            _context.Users.Remove(user);
            
            await _context.SaveChangesAsync();

            // For AJAX requests, return JSON
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return new JsonResult(new
                {
                    success = true,
                    message = $"Student {studentName} deleted successfully."
                });
            }

            SuccessMessage = $"Student {studentName} and all related records deleted successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting student {Id}", id);
            
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "An error occurred while deleting the student."
                });
            }
            
            ErrorMessage = "An error occurred while deleting the student.";
        }

        return RedirectToPage(new { tab = "all" });
    }

    // Import Students Handler
    // Clear validation results handler
    public IActionResult OnPostClearValidation()
    {
        // Clear TempData
        TempData.Remove("ValidationResult");
        TempData.Remove("ValidatedStudents");
        return RedirectToPage(new { tab = "import" });
    }

    public async Task<IActionResult> OnPostValidateImportAsync()
    {
        // Clear any previous validation results
        TempData.Remove("ValidationResult");
        TempData.Remove("ValidatedStudents");
        ValidationResult = null;
        ValidatedStudents = new();

        if (ImportFile == null)
        {
            TempData["Error"] = "Please select a file to upload";
            return RedirectToPage(new { tab = "import" });
        }

        var errors = new List<string>();
        var validStudents = new List<ImportedStudent>();
        var fileExtension = Path.GetExtension(ImportFile.FileName).ToLower();

        try
        {
            using var stream = ImportFile.OpenReadStream();

            if (fileExtension == ".csv")
            {
                using var reader = new StreamReader(stream);
                await reader.ReadLineAsync(); // Skip header

                int lineNumber = 1;
                while (!reader.EndOfStream)
                {
                    lineNumber++;
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var values = line.Split(',');
                    if (values.Length < 8)
                    {
                        errors.Add($"Line {lineNumber}: Invalid format - Expected 8 columns (FullName, Email, RollNo, FatherName, BadgeName, Semester, Session, SectionName)");
                        continue;
                    }

                    // Parse semester
                    if (!int.TryParse(values[5].Trim(), out int semester))
                    {
                        errors.Add($"Line {lineNumber}: Invalid semester value - must be a number");
                        continue;
                    }

                    var student = new ImportedStudent
                    {
                        FullName = values[0].Trim(),
                        Email = values[1].Trim(),
                        RollNo = values[2].Trim(),
                        FatherName = values[3].Trim(),
                        BadgeName = values[4].Trim(),
                        Semester = semester,
                        Session = values[6].Trim(),
                        SectionName = values[7].Trim(),
                        LineNumber = lineNumber
                    };

                    // Validate required fields
                    if (string.IsNullOrWhiteSpace(student.FullName) || string.IsNullOrWhiteSpace(student.Email) ||
                        string.IsNullOrWhiteSpace(student.RollNo) || string.IsNullOrWhiteSpace(student.BadgeName) ||
                        string.IsNullOrWhiteSpace(student.Session) || string.IsNullOrWhiteSpace(student.SectionName))
                    {
                        errors.Add($"Line {lineNumber}: Missing required fields");
                        continue;
                    }

                    validStudents.Add(student);
                }
            }
            else // Excel
            {
                using var package = new ExcelPackage(stream);
                var worksheet = package.Workbook.Worksheets[0];
                var rowCount = worksheet.Dimension?.Rows ?? 0;

                for (int row = 2; row <= rowCount; row++)
                {
                    var fullName = worksheet.Cells[row, 1].Value?.ToString()?.Trim();
                    var email = worksheet.Cells[row, 2].Value?.ToString()?.Trim();
                    var rollNo = worksheet.Cells[row, 3].Value?.ToString()?.Trim();
                    var fatherName = worksheet.Cells[row, 4].Value?.ToString()?.Trim();
                    var badgeName = worksheet.Cells[row, 5].Value?.ToString()?.Trim();
                    var semesterStr = worksheet.Cells[row, 6].Value?.ToString()?.Trim();
                    var session = worksheet.Cells[row, 7].Value?.ToString()?.Trim();
                    var sectionName = worksheet.Cells[row, 8].Value?.ToString()?.Trim();

                    // Parse semester
                    if (!int.TryParse(semesterStr, out int semester))
                    {
                        errors.Add($"Row {row}: Invalid semester value - must be a number");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) ||
                        string.IsNullOrWhiteSpace(rollNo) || string.IsNullOrWhiteSpace(badgeName) ||
                        string.IsNullOrWhiteSpace(session) || string.IsNullOrWhiteSpace(sectionName))
                    {
                        errors.Add($"Row {row}: Missing required fields");
                        continue;
                    }

                    var student = new ImportedStudent
                    {
                        FullName = fullName,
                        Email = email,
                        RollNo = rollNo,
                        FatherName = fatherName,
                        BadgeName = badgeName,
                        Semester = semester,
                        Session = session,
                        SectionName = sectionName,
                        LineNumber = row
                    };

                    validStudents.Add(student);
                }
            }

            // Check for duplicates in database
            var existingEmails = await _context.Users.Select(u => u.Email).ToListAsync();
            var existingRollNos = await _context.Students.Select(s => s.RollNo).ToListAsync();
            var sections = await _context.Sections.Include(s => s.Badge).ToListAsync();

            var finalValidStudents = new List<ImportedStudent>();
            foreach (var student in validStudents)
            {
                bool hasError = false;

                // Check duplicate email
                if (existingEmails.Contains(student.Email))
                {
                    errors.Add($"Line {student.LineNumber}: Email '{student.Email}' already exists");
                    hasError = true;
                }

                // Check duplicate roll number
                if (existingRollNos.Contains(student.RollNo))
                {
                    errors.Add($"Line {student.LineNumber}: Roll number '{student.RollNo}' already exists");
                    hasError = true;
                }

                // Check if section exists with matching semester and session
                var section = sections.FirstOrDefault(s =>
                    s.Badge.Name == student.BadgeName &&
                    s.Semester == student.Semester &&
                    s.Session == student.Session &&
                    s.Name == student.SectionName);

                if (section == null)
                {
                    errors.Add($"Line {student.LineNumber}: Section not found - {student.BadgeName}, Semester {student.Semester}, Session {student.Session}, Section {student.SectionName}");
                    hasError = true;
                }

                if (!hasError)
                {
                    finalValidStudents.Add(student);
                }
            }

            ValidationResult = new ImportResult
            {
                ValidCount = finalValidStudents.Count,
                ErrorCount = errors.Count,
                Errors = errors
            };
            ValidatedStudents = finalValidStudents;

            // Store in TempData for next request
            TempData["ValidationResult"] = System.Text.Json.JsonSerializer.Serialize(ValidationResult);
            TempData["ValidatedStudents"] = System.Text.Json.JsonSerializer.Serialize(ValidatedStudents);

            ActiveTab = "import";
            return Page();
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error processing file: {ex.Message}";
            return RedirectToPage(new { tab = "import" });
        }
    }

    public async Task<IActionResult> OnPostImportStudentsAsync()
    {
        // Get validated students from TempData
        var validatedStudentsJson = TempData["ValidatedStudents"] as string;
        
        if (string.IsNullOrEmpty(validatedStudentsJson))
        {
            TempData["ErrorMessage"] = "No validated data found. Please validate your file first.";
            return RedirectToPage(new { tab = "import" });
        }

        var validatedStudents = System.Text.Json.JsonSerializer.Deserialize<List<ImportedStudent>>(validatedStudentsJson);
        
        if (validatedStudents == null || !validatedStudents.Any())
        {
            TempData["ErrorMessage"] = "No valid students to import.";
            return RedirectToPage(new { tab = "import" });
        }

        try
        {
            // Get sections for lookup
            var sections = await _context.Sections.Include(s => s.Badge).ToListAsync();

            int successCount = 0;
            var errors = new List<string>();

            foreach (var imported in validatedStudents)
            {
                // Find section (data is already validated)
                var section = sections.FirstOrDefault(s => 
                    s.Badge.Name == imported.BadgeName && 
                    s.Semester == imported.Semester &&
                    s.Session == imported.Session &&
                    s.Name == imported.SectionName);

                if (section == null)
                {
                    errors.Add($"Line {imported.LineNumber}: Section not found");
                    continue;
                }

                // Create account
                var password = PasswordGenerator.GeneratePassword();
                var passwordHash = _passwordHasher.HashPassword(password);

                var user = new User
                {
                    Email = imported.Email,
                    FullName = imported.FullName,
                    Role = "Student",
                    PasswordHash = passwordHash,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                var student = new attendence.Domain.Entities.Student
                {
                    UserId = user.Id,
                    RollNo = imported.RollNo,
                    FatherName = imported.FatherName,
                    SectionId = section.Id
                };

                _context.Students.Add(student);
                await _context.SaveChangesAsync();

                successCount++;
            }

            // Clear TempData after successful import
            TempData.Remove("ValidationResult");
            TempData.Remove("ValidatedStudents");

            if (successCount > 0)
            {
                TempData["SuccessMessage"] = $"Successfully imported {successCount} student(s).";
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

    // Enroll Students Handler
}

// Supporting Classes
public class StudentWithDetails
{
    public attendence.Domain.Entities.Student Student { get; set; } = null!;
    public User User { get; set; } = null!;
    public Section Section { get; set; } = null!;
    public Badge Badge { get; set; } = null!;
}

public class StudentInputModel
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? RollNo { get; set; }
    public string? FatherName { get; set; }
    public int? BadgeId { get; set; }
    public int? Semester { get; set; }
    public string? Session { get; set; }
    public int? SectionId { get; set; }
    public bool SendWelcomeEmail { get; set; } = true;
    public List<SelectListItem> BadgeOptions { get; set; } = new();
    public List<SelectListItem> SectionOptions { get; set; } = new();
}

public class ImportedStudent
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string RollNo { get; set; } = string.Empty;
    public string? FatherName { get; set; }
    public string BadgeName { get; set; } = string.Empty;
    public int Semester { get; set; }
    public string Session { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public int LineNumber { get; set; }
}

public class ImportResult
{
    public int ValidCount { get; set; }
    public int ErrorCount { get; set; }
    public List<string> Errors { get; set; } = new();
}
