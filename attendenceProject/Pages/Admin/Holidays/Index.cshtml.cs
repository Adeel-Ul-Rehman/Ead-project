using attendence.Data.Data;
using attendence.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace attendenceProject.Pages.Admin.Holidays;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public IndexModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public List<Holiday> Holidays { get; set; } = new();
    public int TotalHolidays { get; set; }

    public async Task OnGetAsync()
    {
        TotalHolidays = await _context.Holidays.CountAsync();

        Holidays = await _context.Holidays
            .OrderBy(h => h.Date)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        try
        {
            var holiday = await _context.Holidays.FindAsync(id);

            if (holiday == null)
            {
                TempData["ErrorMessage"] = "Holiday not found.";
                return RedirectToPage();
            }

            _context.Holidays.Remove(holiday);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Holiday on {holiday.Date.ToString("MMM dd, yyyy")} deleted successfully!";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error deleting holiday: {ex.Message}";
        }

        return RedirectToPage();
    }
}
