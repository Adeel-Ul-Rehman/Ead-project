using attendence.Data.Data;
using attendence.Domain.Entities;
using attendence.Services.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace attendenceProject.Pages.Admin.Users;

[Authorize(Roles = "Admin")]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly PasswordHasher _passwordHasher;

    public EditModel(ApplicationDbContext context, PasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public int Id { get; set; }

        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [MinLength(6)]
        public string? Password { get; set; }

        [Required]
        public string Role { get; set; } = string.Empty;

        // Teacher fields
        public string? Designation { get; set; }

        // Student fields
        public string? RollNo { get; set; }
        public string? FatherName { get; set; }
        public int? SectionId { get; set; }
    }

    public List<Section> Sections { get; set; } = new();
    public bool HasRelatedData { get; set; }
    public int TeacherCourseCount { get; set; }
    public int AttendanceRecordCount { get; set; }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var user = await _context.Users
            .Include(u => u.Student)
            .Include(u => u.Teacher)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return NotFound();
        }

        Input = new InputModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role
        };

        // Load role-specific data
        if (user.Role == "Teacher" && user.Teacher != null)
        {
            Input.Designation = user.Teacher.Designation;
            TeacherCourseCount = await _context.TeacherCourses.CountAsync(tc => tc.TeacherId == user.Teacher.Id);
            HasRelatedData = TeacherCourseCount > 0;
        }
        else if (user.Role == "Student" && user.Student != null)
        {
            Input.RollNo = user.Student.RollNo;
            Input.FatherName = user.Student.FatherName;
            Input.SectionId = user.Student.SectionId;
            AttendanceRecordCount = await _context.AttendanceRecords.CountAsync(ar => ar.StudentId == user.Student.Id);
            HasRelatedData = AttendanceRecordCount > 0;
        }

        Sections = await _context.Sections
            .Include(s => s.Badge)
            .OrderBy(s => s.Badge.Name)
            .ThenBy(s => s.Semester)
            .ThenBy(s => s.Name)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Load sections for dropdown
        Sections = await _context.Sections
            .Include(s => s.Badge)
            .OrderBy(s => s.Badge.Name)
            .ThenBy(s => s.Semester)
            .ThenBy(s => s.Name)
            .ToListAsync();

        // Validate role-specific fields
        if (Input.Role == "Student")
        {
            if (string.IsNullOrWhiteSpace(Input.RollNo))
            {
                ModelState.AddModelError("Input.RollNo", "Roll Number is required for students.");
            }
            if (Input.SectionId == null || Input.SectionId == 0)
            {
                ModelState.AddModelError("Input.SectionId", "Section is required for students.");
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await _context.Users
            .Include(u => u.Student)
            .Include(u => u.Teacher)
            .FirstOrDefaultAsync(u => u.Id == Input.Id);

        if (user == null)
        {
            return NotFound();
        }

        // Check for duplicate email (excluding current user)
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == Input.Email.ToLower() && u.Id != Input.Id);

        if (existingUser != null)
        {
            ModelState.AddModelError("Input.Email", "A user with this email already exists.");
            return Page();
        }

        // Check for duplicate roll number if student (excluding current student)
        if (Input.Role == "Student" && !string.IsNullOrWhiteSpace(Input.RollNo))
        {
            var existingStudent = await _context.Students
                .FirstOrDefaultAsync(s => s.RollNo.ToLower() == Input.RollNo.ToLower() && s.UserId != Input.Id);

            if (existingStudent != null)
            {
                ModelState.AddModelError("Input.RollNo", "A student with this roll number already exists.");
                return Page();
            }
        }

        // Update user
        user.FullName = Input.FullName;
        user.Email = Input.Email;
        
        if (!string.IsNullOrWhiteSpace(Input.Password))
        {
            user.PasswordHash = _passwordHasher.HashPassword(Input.Password);
        }

        var oldRole = user.Role;
        user.Role = Input.Role;

        // Handle role changes and role-specific updates
        if (Input.Role == "Teacher")
        {
            if (user.Teacher == null)
            {
                // Create teacher record if switching to teacher role
                var teacher = new attendence.Domain.Entities.Teacher
                {
                    UserId = user.Id,
                    Designation = Input.Designation
                };
                _context.Teachers.Add(teacher);
            }
            else
            {
                // Update existing teacher
                user.Teacher.Designation = Input.Designation;
            }
        }
        else if (Input.Role == "Student")
        {
            if (user.Student == null)
            {
                // Create student record if switching to student role
                var student = new attendence.Domain.Entities.Student
                {
                    UserId = user.Id,
                    SectionId = Input.SectionId!.Value,
                    RollNo = Input.RollNo!,
                    FatherName = Input.FatherName
                };
                _context.Students.Add(student);
            }
            else
            {
                // Update existing student
                user.Student.RollNo = Input.RollNo!;
                user.Student.FatherName = Input.FatherName;
                user.Student.SectionId = Input.SectionId!.Value;
            }
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await UserExists(Input.Id))
            {
                return NotFound();
            }
            throw;
        }

        TempData["SuccessMessage"] = $"User '{Input.FullName}' updated successfully!";
        return RedirectToPage("./Index");
    }

    private async Task<bool> UserExists(int id)
    {
        return await _context.Users.AnyAsync(u => u.Id == id);
    }
}
