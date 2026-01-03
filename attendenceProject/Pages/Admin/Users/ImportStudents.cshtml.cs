using attendence.Data.Data;
using attendence.Domain.Entities;
using attendence.Services.Models;
using attendence.Services.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace attendenceProject.Pages.Admin.Users;

[Authorize(Roles = "Admin")]
public class ImportStudentsModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly BulkImportService _bulkImportService;
    private readonly EmailService _emailService;
    private readonly IConfiguration _configuration;

    public int Step { get; set; } = 1;
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    
    [BindProperty]
    public int SelectedSemester { get; set; }
    
    [BindProperty]
    public string? SelectedSession { get; set; }
    
    [BindProperty]
    public int SelectedSectionId { get; set; }
    
    public Section? SelectedSection { get; set; }
    public List<SelectListItem> SemesterOptions { get; set; } = new();
    public List<SelectListItem> SectionOptions { get; set; } = new();
    public ImportValidationResult<StudentImportModel>? ValidationResult { get; set; }
    public BulkCreateResult? CreateResult { get; set; }
    public BulkEmailResult? EmailResult { get; set; }

    public ImportStudentsModel(
        ApplicationDbContext context,
        BulkImportService bulkImportService,
        EmailService emailService,
        IConfiguration configuration)
    {
        _context = context;
        _bulkImportService = bulkImportService;
        _emailService = emailService;
        _configuration = configuration;
    }

    public async Task OnGetAsync()
    {
        Step = 1;
        await LoadSemesterOptions();
    }

    private async Task LoadSemesterOptions()
    {
        // Get unique semester and session combinations
        var semesters = await _context.Sections
            .Select(s => new { s.Semester, s.Session })
            .Distinct()
            .OrderByDescending(s => s.Session) // Latest sessions first
            .ThenBy(s => s.Semester)
            .ToListAsync();

        SemesterOptions = new List<SelectListItem>
        {
            new SelectListItem { Value = "", Text = "-- Select Semester --", Disabled = true, Selected = true }
        };

        foreach (var sem in semesters)
        {
            string ordinal = sem.Semester switch
            {
                1 => "1st",
                2 => "2nd",
                3 => "3rd",
                _ => $"{sem.Semester}th"
            };
            
            SemesterOptions.Add(new SelectListItem
            {
                Value = $"{sem.Semester}|{sem.Session}",
                Text = $"{ordinal} Semester ({sem.Session})"
            });
        }
    }
    
    public async Task<JsonResult> OnGetSectionsAsync(int semester, string session)
    {
        var sections = await _context.Sections
            .Include(s => s.Badge)
            .Where(s => s.Semester == semester && s.Session == session)
            .OrderBy(s => s.Name)
            .Select(s => new 
            {
                value = s.Id,
                text = $"Section {s.Name} - {s.Badge.Name}"
            })
            .ToListAsync();

        return new JsonResult(sections);
    }

    public async Task<IActionResult> OnPostUploadAsync(IFormFile file)
    {
        await LoadSemesterOptions();

        if (SelectedSectionId == 0)
        {
            ErrorMessage = "Please select a semester and section.";
            Step = 1;
            return Page();
        }

        if (file == null || file.Length == 0)
        {
            ErrorMessage = "Please select a file to upload.";
            Step = 1;
            return Page();
        }

        // Validate file extension
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".csv" && extension != ".xlsx")
        {
            ErrorMessage = "Invalid file format. Please upload a CSV or Excel (.xlsx) file.";
            Step = 1;
            return Page();
        }

        try
        {
            // Get selected section info
            SelectedSection = await _context.Sections
                .Include(s => s.Badge)
                .FirstOrDefaultAsync(s => s.Id == SelectedSectionId);

            if (SelectedSection == null)
            {
                ErrorMessage = "Selected section not found.";
                Step = 1;
                return Page();
            }

            using var stream = file.OpenReadStream();

            // Validate based on file type
            if (extension == ".csv")
            {
                ValidationResult = await _bulkImportService.ValidateStudentsCsvAsync(stream, SelectedSectionId);
            }
            else
            {
                ValidationResult = await _bulkImportService.ValidateStudentsExcelAsync(stream, SelectedSectionId);
            }

            // Store validation result and section ID in TempData for next request
            TempData["ValidationResult"] = System.Text.Json.JsonSerializer.Serialize(ValidationResult);
            TempData["SelectedSectionId"] = SelectedSectionId;
            
            Step = 2;
            return Page();
        }
        catch (Exception ex)
        {
            var errorDetail = ex.InnerException != null ? $"{ex.Message} - {ex.InnerException.Message}" : ex.Message;
            ErrorMessage = $"Error processing file: {errorDetail}";
            Console.WriteLine($"[Import Error] {ex}");
            Step = 1;
            return Page();
        }
    }

    public async Task<IActionResult> OnPostConfirmAsync()
    {
        // Retrieve validation result from TempData
        var validationJson = TempData["ValidationResult"] as string;
        var sectionId = TempData["SelectedSectionId"] as int?;

        if (string.IsNullOrEmpty(validationJson) || !sectionId.HasValue)
        {
            return RedirectToPage();
        }

        ValidationResult = System.Text.Json.JsonSerializer.Deserialize<ImportValidationResult<StudentImportModel>>(validationJson);
        SelectedSectionId = sectionId.Value;
        
        // Get selected section info
        SelectedSection = await _context.Sections
            .Include(s => s.Badge)
            .FirstOrDefaultAsync(s => s.Id == SelectedSectionId);

        if (ValidationResult == null || ValidationResult.ValidCount == 0)
        {
            ErrorMessage = "No valid records to import.";
            Step = 2;
            await LoadSemesterOptions();
            return Page();
        }

        try
        {
            // Create students
            CreateResult = await _bulkImportService.CreateStudentsAsync(ValidationResult.ValidRecords, SelectedSectionId);

            // Send emails
            if (CreateResult.UserCredentials.Count > 0)
            {
                var loginUrl = _configuration.GetValue<string>("AppSettings:LoginUrl") ?? "http://localhost:5000/Account/Login";
                EmailResult = await _emailService.SendBulkCredentialsEmailsAsync(CreateResult.UserCredentials, loginUrl);
            }

            Step = 3;
            return Page();
        }
        catch (Exception ex)
        {
            var errorDetail = ex.InnerException != null ? $"{ex.Message} - {ex.InnerException.Message}" : ex.Message;
            ErrorMessage = $"Error creating students: {errorDetail}";
            Console.WriteLine($"[Student Creation Error] {ex}");
            Step = 2;
            await LoadSemesterOptions();
            return Page();
        }
    }
}
