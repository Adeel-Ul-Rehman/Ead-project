using attendence.Data.Data;
using attendence.Domain.Entities;
using attendence.Services.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace attendenceProject.Pages.Admin.Badges;

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
    public string SearchTerm { get; set; } = string.Empty;

    // Statistics
    public int TotalBadges { get; set; }
    public int TotalSections { get; set; }
    public string? MostUsedBadge { get; set; }
    public int MostUsedBadgeCount { get; set; }

    // All Badges
    public List<BadgeViewModel> Badges { get; set; } = new();

    // Add Badge
    [BindProperty]
    public string BadgeName { get; set; } = string.Empty;

    // Import
    [BindProperty]
    public IFormFile? ImportFile { get; set; }
    public List<string> ValidatedBadges { get; set; } = new();
    public List<string> ValidationErrors { get; set; } = new();
    public ImportValidationResult? ValidationResult { get; set; }

    public async Task OnGetAsync(string tab = "all", string searchTerm = "")
    {
        ActiveTab = tab;
        SearchTerm = searchTerm;
        
        await LoadStatisticsAsync();
        await LoadBadgesAsync();

        // Load validation data from TempData
        if (TempData["ValidatedBadges"] is string validatedJson)
        {
            ValidatedBadges = JsonSerializer.Deserialize<List<string>>(validatedJson) ?? new List<string>();
            TempData.Keep("ValidatedBadges");
            
            // Create validation result for display
            ValidationResult = new ImportValidationResult
            {
                IsValid = true,
                TotalRows = ValidatedBadges.Count,
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
        TotalBadges = await _context.Badges.CountAsync();
        TotalSections = await _context.Sections.CountAsync();

        var mostUsed = await _context.Badges
            .Select(b => new
            {
                Badge = b,
                SectionCount = b.Sections.Count
            })
            .OrderByDescending(x => x.SectionCount)
            .FirstOrDefaultAsync();

        if (mostUsed != null)
        {
            MostUsedBadge = mostUsed.Badge.Name;
            MostUsedBadgeCount = mostUsed.SectionCount;
        }
    }

    private async Task LoadBadgesAsync()
    {
        var query = _context.Badges.AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            query = query.Where(b => b.Name.ToLower().Contains(SearchTerm.ToLower()));
        }

        Badges = await query
            .Select(b => new BadgeViewModel
            {
                Id = b.Id,
                Name = b.Name,
                CreatedAt = b.CreatedAt,
                SectionCount = b.Sections.Count
            })
            .OrderBy(b => b.Name)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostAddAsync()
    {
        if (string.IsNullOrWhiteSpace(BadgeName))
        {
            ErrorMessage = "Badge name is required.";
            ActiveTab = "add";
            await LoadStatisticsAsync();
            await LoadBadgesAsync();
            return Page();
        }

        // Check for duplicate
        var exists = await _context.Badges.AnyAsync(b => b.Name.ToLower() == BadgeName.Trim().ToLower());
        if (exists)
        {
            ErrorMessage = $"Badge '{BadgeName.Trim()}' already exists.";
            ActiveTab = "add";
            await LoadStatisticsAsync();
            await LoadBadgesAsync();
            return Page();
        }

        var badge = new Badge
        {
            Name = BadgeName.Trim(),
            CreatedAt = DateTime.Now
        };

        _context.Badges.Add(badge);
        await _context.SaveChangesAsync();

        SuccessMessage = $"Badge '{badge.Name}' added successfully!";
        return RedirectToPage(new { tab = "all" });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var badge = await _context.Badges
            .Include(b => b.Sections)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (badge == null)
        {
            return NotFound();
        }

        if (badge.Sections.Any())
        {
            ErrorMessage = $"Cannot delete badge '{badge.Name}'. It is assigned to {badge.Sections.Count} section(s).";
            return RedirectToPage(new { tab = "all" });
        }

        _context.Badges.Remove(badge);
        await _context.SaveChangesAsync();

        SuccessMessage = $"Badge '{badge.Name}' deleted successfully!";
        return RedirectToPage(new { tab = "all" });
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

        var result = await _importService.ValidateBadgesImportAsync(ImportFile);
        
        if (!result.IsValid)
        {
            // Store errors in TempData
            TempData["ValidationErrors"] = JsonSerializer.Serialize(result.Errors);
            TempData["ShowValidationResults"] = true;
            TempData["ValidationFailed"] = true;
            return RedirectToPage(new { tab = "import" });
        }

        // Store validated badges in TempData
        TempData["ValidatedBadges"] = JsonSerializer.Serialize(result.ValidatedBadges);
        TempData["ShowValidationResults"] = true;
        TempData["ValidationSuccess"] = true;
        TempData.Keep("ValidatedBadges");

        return RedirectToPage(new { tab = "import" });
    }

    public async Task<IActionResult> OnPostImportAsync()
    {
        var validatedBadgesJson = TempData["ValidatedBadges"] as string;

        if (string.IsNullOrEmpty(validatedBadgesJson))
        {
            TempData["ErrorMessage"] = "No validated data found. Please validate your file first.";
            return RedirectToPage(new { tab = "import" });
        }

        var validatedBadges = JsonSerializer.Deserialize<List<string>>(validatedBadgesJson);

        if (validatedBadges == null || !validatedBadges.Any())
        {
            TempData["ErrorMessage"] = "No valid badges to import.";
            return RedirectToPage(new { tab = "import" });
        }

        try
        {
            // Import badges
            var existingBadges = await _context.Badges.Select(b => b.Name.ToLower()).ToListAsync();
            var badgesToAdd = new List<Badge>();

            foreach (var badgeName in validatedBadges)
            {
                if (!existingBadges.Contains(badgeName.ToLower()))
                {
                    badgesToAdd.Add(new Badge
                    {
                        Name = badgeName,
                        CreatedAt = DateTime.Now
                    });
                }
            }

            if (badgesToAdd.Any())
            {
                _context.Badges.AddRange(badgesToAdd);
                await _context.SaveChangesAsync();

                // Clear validation data
                TempData.Remove("ValidatedBadges");
                TempData.Remove("ShowValidationResults");

                TempData["SuccessMessage"] = $"Successfully imported {badgesToAdd.Count} badge(s)!";
            }
            else
            {
                TempData["ErrorMessage"] = "All badges already exist in the system.";
            }

            return RedirectToPage(new { tab = "all" });
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error importing badges: {ex.Message}";
            return RedirectToPage(new { tab = "import" });
        }
    }

    public class BadgeViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int SectionCount { get; set; }
    }

    public class ImportValidationResult
    {
        public bool IsValid { get; set; }
        public int TotalRows { get; set; }
        public int ErrorCount { get; set; }
    }
}
