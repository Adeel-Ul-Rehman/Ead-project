using attendence.Data.Data;
using attendence.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace attendenceProject.Pages.Admin.Badges;

[Authorize(Roles = "Admin")]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public EditModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Badge Badge { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var badge = await _context.Badges
            .Include(b => b.Sections)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (badge == null)
        {
            return NotFound();
        }

        Badge = badge;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Check for duplicate badge name (excluding current badge)
        var existingBadge = await _context.Badges
            .FirstOrDefaultAsync(b => b.Name.ToLower() == Badge.Name.ToLower() && b.Id != Badge.Id);

        if (existingBadge != null)
        {
            ModelState.AddModelError("Badge.Name", "A badge with this name already exists.");
            // Reload sections for display
            Badge.Sections = await _context.Sections.Where(s => s.BadgeId == Badge.Id).ToListAsync();
            return Page();
        }

        _context.Attach(Badge).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await BadgeExists(Badge.Id))
            {
                return NotFound();
            }
            throw;
        }

        TempData["SuccessMessage"] = $"Badge '{Badge.Name}' updated successfully!";
        return RedirectToPage("./Index");
    }

    private async Task<bool> BadgeExists(int id)
    {
        return await _context.Badges.AnyAsync(b => b.Id == id);
    }
}
