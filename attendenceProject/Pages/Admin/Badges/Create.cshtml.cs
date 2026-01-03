using attendence.Data.Data;
using attendence.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace attendenceProject.Pages.Admin.Badges;

[Authorize(Roles = "Admin")]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public CreateModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Badge Badge { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Check for duplicate badge name
        var existingBadge = await _context.Badges
            .FirstOrDefaultAsync(b => b.Name.ToLower() == Badge.Name.ToLower());

        if (existingBadge != null)
        {
            ModelState.AddModelError("Badge.Name", "A badge with this name already exists.");
            return Page();
        }

        _context.Badges.Add(Badge);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Badge '{Badge.Name}' created successfully!";
        return RedirectToPage("./Index");
    }
}
