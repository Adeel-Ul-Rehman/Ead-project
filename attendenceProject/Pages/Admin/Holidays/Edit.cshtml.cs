using attendence.Data.Data;
using attendence.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace attendenceProject.Pages.Admin.Holidays;

[Authorize(Roles = "Admin")]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public EditModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public int Id { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        [StringLength(500)]
        public string Reason { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var holiday = await _context.Holidays.FindAsync(id);
        if (holiday == null)
        {
            return NotFound();
        }

        Input = new InputModel
        {
            Id = holiday.Id,
            Date = holiday.Date,
            Reason = holiday.Reason
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var holiday = await _context.Holidays.FindAsync(Input.Id);
        if (holiday == null)
        {
            return NotFound();
        }

        holiday.Date = Input.Date;
        holiday.Reason = Input.Reason;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await HolidayExists(Input.Id))
            {
                return NotFound();
            }
            throw;
        }

        TempData["SuccessMessage"] = "Holiday updated successfully!";
        return RedirectToPage("./Index");
    }

    private async Task<bool> HolidayExists(int id)
    {
        return await _context.Holidays.AnyAsync(h => h.Id == id);
    }
}
