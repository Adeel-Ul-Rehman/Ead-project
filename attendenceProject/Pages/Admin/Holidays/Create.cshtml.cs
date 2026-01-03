using attendence.Data.Data;
using attendence.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace attendenceProject.Pages.Admin.Holidays;

[Authorize(Roles = "Admin")]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public CreateModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        public DateTime Date { get; set; }

        [Required]
        [StringLength(500)]
        public string Reason { get; set; } = string.Empty;
    }

    public IActionResult OnGet()
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var holiday = new Holiday
        {
            Date = Input.Date,
            Reason = Input.Reason,
            CreatedAt = DateTime.Now
        };

        _context.Holidays.Add(holiday);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Holiday created successfully!";
        return RedirectToPage("./Index");
    }
}
