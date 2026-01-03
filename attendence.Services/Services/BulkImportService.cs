using attendence.Data.Data;
using attendence.Domain.Entities;
using attendence.Services.Helpers;
using attendence.Services.Models;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using System.Globalization;
using System.Text.RegularExpressions;

namespace attendence.Services.Services;

public class BulkImportService
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public BulkImportService(ApplicationDbContext context, IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    #region Student Import

    /// <summary>
    /// Parse and validate students from CSV file
    /// </summary>
    public async Task<ImportValidationResult<StudentImportModel>> ValidateStudentsCsvAsync(Stream fileStream, int sectionId)
    {
        var result = new ImportValidationResult<StudentImportModel>();
        bool isLegacyFormat = sectionId == 0; // Legacy format has SectionName in CSV
        
        try
        {
            using var reader = new StreamReader(fileStream);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                TrimOptions = TrimOptions.Trim,
                MissingFieldFound = null, // Ignore missing fields for compatibility
                HeaderValidated = null // Don't validate headers - allows optional fields
            });

            var records = csv.GetRecords<StudentImportModel>().ToList();
            result.TotalRecords = records.Count;

            for (int i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var errors = await ValidateStudentRecord(record, sectionId, isLegacyFormat);

                if (errors.Count == 0)
                {
                    result.ValidRecords.Add(record);
                }
                else
                {
                    result.Errors.Add(new ImportError
                    {
                        RowNumber = i + 2, // +2 because of header and 0-index
                        FullName = record.FullName,
                        Email = record.Email,
                        ErrorMessages = errors
                    });
                }
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add(new ImportError
            {
                RowNumber = 0,
                ErrorMessages = new List<string> { $"File parsing error: {ex.Message}" }
            });
        }

        return result;
    }

    /// <summary>
    /// Parse and validate students from Excel file
    /// </summary>
    public async Task<ImportValidationResult<StudentImportModel>> ValidateStudentsExcelAsync(Stream fileStream, int sectionId)
    {
        var result = new ImportValidationResult<StudentImportModel>();
        bool isLegacyFormat = sectionId == 0; // Legacy format has SectionName in Excel

        try
        {
            using var package = new ExcelPackage(fileStream);
            var worksheet = package.Workbook.Worksheets[0];
            var rowCount = worksheet.Dimension?.Rows ?? 0;

            result.TotalRecords = rowCount - 1; // Exclude header

            for (int row = 2; row <= rowCount; row++)
            {
                var record = new StudentImportModel
                {
                    FullName = worksheet.Cells[row, 1].Value?.ToString()?.Trim() ?? "",
                    Email = worksheet.Cells[row, 2].Value?.ToString()?.Trim() ?? "",
                    FatherName = worksheet.Cells[row, 3].Value?.ToString()?.Trim() ?? "",
                    RollNumber = worksheet.Cells[row, 4].Value?.ToString()?.Trim() ?? "",
                    // Legacy format support
                    SectionName = isLegacyFormat ? worksheet.Cells[row, 3].Value?.ToString()?.Trim() : null,
                    BadgeNumber = isLegacyFormat ? worksheet.Cells[row, 4].Value?.ToString()?.Trim() : null
                };

                var errors = await ValidateStudentRecord(record, sectionId, isLegacyFormat);

                if (errors.Count == 0)
                {
                    result.ValidRecords.Add(record);
                }
                else
                {
                    result.Errors.Add(new ImportError
                    {
                        RowNumber = row,
                        FullName = record.FullName,
                        Email = record.Email,
                        ErrorMessages = errors
                    });
                }
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add(new ImportError
            {
                RowNumber = 0,
                ErrorMessages = new List<string> { $"File parsing error: {ex.Message}" }
            });
        }

        return result;
    }

    /// <summary>
    /// Validate a single student record
    /// </summary>
    private async Task<List<string>> ValidateStudentRecord(StudentImportModel record, int sectionId, bool isLegacyFormat = false)
    {
        var errors = new List<string>();

        // Validate FullName
        if (string.IsNullOrWhiteSpace(record.FullName))
            errors.Add("Full name is required");

        // Validate Email
        if (string.IsNullOrWhiteSpace(record.Email))
            errors.Add("Email is required");
        else if (!IsValidEmail(record.Email))
            errors.Add("Invalid email format");
        else if (await _context.Users.AnyAsync(u => u.Email == record.Email))
            errors.Add("Email already exists");

        if (isLegacyFormat)
        {
            // Legacy format validation (SectionName + BadgeNumber)
            if (string.IsNullOrWhiteSpace(record.SectionName))
                errors.Add("Section name is required");
            else if (!await _context.Sections.AnyAsync(s => s.Name == record.SectionName))
                errors.Add($"Section '{record.SectionName}' does not exist");

            if (string.IsNullOrWhiteSpace(record.BadgeNumber))
                errors.Add("Badge number is required");
            else if (await _context.Students.AnyAsync(s => s.RollNo == record.BadgeNumber))
                errors.Add("Badge number already exists");
        }
        else
        {
            // New format validation (FatherName + RollNumber)
            if (string.IsNullOrWhiteSpace(record.FatherName))
                errors.Add("Father name is recommended");

            // Validate RollNumber with format: YYYY-XX-NNN (e.g., 2023-CS-626)
            if (string.IsNullOrWhiteSpace(record.RollNumber))
                errors.Add("Roll number is required");
            else if (await _context.Students.AnyAsync(s => s.RollNo == record.RollNumber))
                errors.Add("Roll number already exists");
            else if (!IsValidRollNumberFormat(record.RollNumber))
                errors.Add("Invalid roll number format. Expected: YYYY-XX-NNN (e.g., 2023-CS-626)");
        }

        return errors;
    }

    /// <summary>
    /// Validate roll number format: YYYY-XX-NNN
    /// </summary>
    private bool IsValidRollNumberFormat(string rollNumber)
    {
        // Format: 2023-CS-626 or similar
        var parts = rollNumber.Split('-');
        if (parts.Length != 3) return false;
        
        // First part should be 4-digit year
        if (parts[0].Length != 4 || !int.TryParse(parts[0], out int year)) return false;
        
        // Second part should be 2-3 letter program code
        if (parts[1].Length < 2 || parts[1].Length > 3 || !parts[1].All(char.IsLetter)) return false;
        
        // Third part should be 1-4 digit number
        if (parts[2].Length < 1 || parts[2].Length > 4 || !int.TryParse(parts[2], out _)) return false;
        
        return true;
    }

    /// <summary>
    /// Create students from validated records
    /// </summary>
    public async Task<BulkCreateResult> CreateStudentsAsync(List<StudentImportModel> students, int sectionId)
    {
        var result = new BulkCreateResult
        {
            TotalAttempted = students.Count
        };

        bool isLegacyFormat = sectionId == 0; // Legacy format reads section from CSV

        foreach (var student in students)
        {
            try
            {
                // Check if email already exists (double-check)
                if (await _context.Users.AnyAsync(u => u.Email == student.Email))
                {
                    result.SkippedCount++;
                    result.SkippedEmails.Add(student.Email);
                    continue;
                }

                // Determine section for this student
                Section? section;
                if (isLegacyFormat)
                {
                    // Legacy: get section from CSV SectionName field
                    section = await _context.Sections
                        .Include(s => s.Badge)
                        .FirstOrDefaultAsync(s => s.Name == student.SectionName);
                    
                    if (section == null)
                    {
                        result.SkippedCount++;
                        result.SkippedEmails.Add(student.Email);
                        continue;
                    }
                }
                else
                {
                    // New format: use provided sectionId
                    section = await _context.Sections
                        .Include(s => s.Badge)
                        .FirstOrDefaultAsync(s => s.Id == sectionId);
                    
                    if (section == null)
                    {
                        throw new Exception($"Section with ID {sectionId} not found");
                    }
                }

                // Generate password
                var password = PasswordGenerator.GeneratePassword(10);
                var passwordHash = _passwordHasher.HashPassword(password);

                // Create User
                var user = new User
                {
                    FullName = student.FullName,
                    Email = student.Email,
                    PasswordHash = passwordHash,
                    Role = "Student",
                    CreatedAt = DateTime.Now
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Create Student
                var studentEntity = new Student
                {
                    UserId = user.Id,
                    SectionId = section.Id,
                    RollNo = isLegacyFormat ? (student.BadgeNumber ?? "N/A") : student.RollNumber,
                    FatherName = string.IsNullOrWhiteSpace(student.FatherName) ? "Not Provided" : student.FatherName
                };

                _context.Students.Add(studentEntity);
                await _context.SaveChangesAsync();

                // Store credentials for email
                result.UserCredentials.Add(new UserCredential
                {
                    Email = user.Email,
                    FullName = user.FullName,
                    Password = password,
                    Role = "Student",
                    SectionName = section.Name
                });

                result.SuccessCount++;
                result.CreatedUsers.Add(user.Email);
            }
            catch (Exception)
            {
                result.SkippedCount++;
                result.SkippedEmails.Add(student.Email);
            }
        }

        return result;
    }

    #endregion

    #region Teacher Import

    /// <summary>
    /// Parse and validate teachers from CSV file
    /// </summary>
    public async Task<ImportValidationResult<TeacherImportModel>> ValidateTeachersCsvAsync(Stream fileStream)
    {
        var result = new ImportValidationResult<TeacherImportModel>();

        try
        {
            using var reader = new StreamReader(fileStream);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                TrimOptions = TrimOptions.Trim
            });

            var records = csv.GetRecords<TeacherImportModel>().ToList();
            result.TotalRecords = records.Count;

            for (int i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var errors = await ValidateTeacherRecord(record);

                if (errors.Count == 0)
                {
                    result.ValidRecords.Add(record);
                }
                else
                {
                    result.Errors.Add(new ImportError
                    {
                        RowNumber = i + 2,
                        FullName = record.FullName,
                        Email = record.Email,
                        ErrorMessages = errors
                    });
                }
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add(new ImportError
            {
                RowNumber = 0,
                ErrorMessages = new List<string> { $"File parsing error: {ex.Message}" }
            });
        }

        return result;
    }

    /// <summary>
    /// Parse and validate teachers from Excel file
    /// </summary>
    public async Task<ImportValidationResult<TeacherImportModel>> ValidateTeachersExcelAsync(Stream fileStream)
    {
        var result = new ImportValidationResult<TeacherImportModel>();

        try
        {
            using var package = new ExcelPackage(fileStream);
            var worksheet = package.Workbook.Worksheets[0];
            var rowCount = worksheet.Dimension?.Rows ?? 0;

            result.TotalRecords = rowCount - 1;

            for (int row = 2; row <= rowCount; row++)
            {
                var record = new TeacherImportModel
                {
                    FullName = worksheet.Cells[row, 1].Value?.ToString()?.Trim() ?? "",
                    Email = worksheet.Cells[row, 2].Value?.ToString()?.Trim() ?? "",
                    BadgeNumber = worksheet.Cells[row, 3].Value?.ToString()?.Trim() ?? ""
                };

                var errors = await ValidateTeacherRecord(record);

                if (errors.Count == 0)
                {
                    result.ValidRecords.Add(record);
                }
                else
                {
                    result.Errors.Add(new ImportError
                    {
                        RowNumber = row,
                        FullName = record.FullName,
                        Email = record.Email,
                        ErrorMessages = errors
                    });
                }
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add(new ImportError
            {
                RowNumber = 0,
                ErrorMessages = new List<string> { $"File parsing error: {ex.Message}" }
            });
        }

        return result;
    }

    /// <summary>
    /// Validate a single teacher record
    /// </summary>
    private async Task<List<string>> ValidateTeacherRecord(TeacherImportModel record)
    {
        var errors = new List<string>();

        // Validate FullName
        if (string.IsNullOrWhiteSpace(record.FullName))
            errors.Add("Full name is required");

        // Validate Email
        if (string.IsNullOrWhiteSpace(record.Email))
            errors.Add("Email is required");
        else if (!IsValidEmail(record.Email))
            errors.Add("Invalid email format");
        else if (await _context.Users.AnyAsync(u => u.Email == record.Email))
            errors.Add("Email already exists");

        // Validate BadgeNumber (unique teacher ID)
        if (string.IsNullOrWhiteSpace(record.BadgeNumber))
            errors.Add("Badge number is required");
        else if (await _context.Teachers.AnyAsync(t => t.BadgeNumber == record.BadgeNumber))
            errors.Add($"Badge number '{record.BadgeNumber}' already exists");

        return errors;
    }

    /// <summary>
    /// Create teachers from validated records
    /// </summary>
    public async Task<BulkCreateResult> CreateTeachersAsync(List<TeacherImportModel> teachers)
    {
        var result = new BulkCreateResult
        {
            TotalAttempted = teachers.Count
        };

        foreach (var teacher in teachers)
        {
            try
            {
                // Check if email already exists
                if (await _context.Users.AnyAsync(u => u.Email == teacher.Email))
                {
                    result.SkippedCount++;
                    result.SkippedEmails.Add(teacher.Email);
                    continue;
                }

                // Generate password
                var password = PasswordGenerator.GeneratePassword(10);
                var passwordHash = _passwordHasher.HashPassword(password);

                // Create User
                var user = new User
                {
                    FullName = teacher.FullName,
                    Email = teacher.Email,
                    PasswordHash = passwordHash,
                    Role = "Teacher",
                    CreatedAt = DateTime.Now
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Create Teacher
                var teacherEntity = new Teacher
                {
                    UserId = user.Id,
                    BadgeNumber = teacher.BadgeNumber,
                    Designation = "Teacher" // Default designation
                };

                _context.Teachers.Add(teacherEntity);
                await _context.SaveChangesAsync();

                // Store credentials
                result.UserCredentials.Add(new UserCredential
                {
                    Email = user.Email,
                    FullName = user.FullName,
                    Password = password,
                    Role = "Teacher"
                });

                result.SuccessCount++;
                result.CreatedUsers.Add(user.Email);
            }
            catch (Exception)
            {
                result.SkippedCount++;
                result.SkippedEmails.Add(teacher.Email);
            }
        }

        return result;
    }

    #endregion

    #region Badges Import

    /// <summary>
    /// Validate badges import from CSV file
    /// </summary>
    public async Task<BadgesImportValidationResult> ValidateBadgesImportAsync(IFormFile file)
    {
        var result = new BadgesImportValidationResult();

        try
        {
            using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream);

            var lines = new List<string>();
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    lines.Add(line);
                }
            }

            if (lines.Count == 0)
            {
                result.Errors.Add("File is empty.");
                return result;
            }

            // Check header
            var header = lines[0].Trim();
            if (!header.Equals("BadgeName", StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add($"Line 1: Invalid header. Expected 'BadgeName', but found '{header}'.");
                return result;
            }

            if (lines.Count == 1)
            {
                result.Errors.Add("No data rows found in the file.");
                return result;
            }

            // Get existing badges for duplicate check
            var existingBadges = await _context.Badges
                .Select(b => b.Name.ToLower())
                .ToListAsync();

            var seenBadges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Validate each badge
            for (int i = 1; i < lines.Count; i++)
            {
                var lineNumber = i + 1;
                var badgeName = lines[i].Trim();

                // Empty line
                if (string.IsNullOrWhiteSpace(badgeName))
                {
                    result.Errors.Add($"Line {lineNumber}: Badge name is empty.");
                    continue;
                }

                // Duplicate in file
                if (seenBadges.Contains(badgeName))
                {
                    result.Errors.Add($"Line {lineNumber}: Duplicate badge '{badgeName}' found in the file.");
                    continue;
                }

                // Already exists in database
                if (existingBadges.Contains(badgeName.ToLower()))
                {
                    result.Errors.Add($"Line {lineNumber}: Badge '{badgeName}' already exists in the system.");
                    continue;
                }

                // Valid badge
                seenBadges.Add(badgeName);
                result.ValidatedBadges.Add(badgeName);
            }

            result.IsValid = result.Errors.Count == 0 && result.ValidatedBadges.Count > 0;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error reading file: {ex.Message}");
        }

        return result;
    }

    #endregion

    #region Sections Import

    public async Task<SectionsImportValidationResult> ValidateSectionsImportAsync(IFormFile file)
    {
        var result = new SectionsImportValidationResult();
        
        try
        {
            using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream);
            
            var lines = new List<string>();
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    lines.Add(line);
                }
            }
            
            if (lines.Count == 0)
            {
                result.Errors.Add("File is empty.");
                return result;
            }
            
            var header = lines[0].Trim();
            if (!header.Equals("BadgeName,Semester,Session,SectionName", StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add($"Line 1: Invalid header. Expected 'BadgeName,Semester,Session,SectionName'.");
                return result;
            }
            
            if (lines.Count == 1)
            {
                result.Errors.Add("No data rows found.");
                return result;
            }
            
            // Get existing badges
            var badges = await _context.Badges.ToDictionaryAsync(b => b.Name.ToLower(), b => b.Id);
            
            // Get existing sections
            var existingSections = await _context.Sections
                .Include(s => s.Badge)
                .Select(s => new { s.Badge.Name, s.Semester, s.Session, SectionName = s.Name })
                .ToListAsync();
            
            var seenSections = new HashSet<string>();
            
            for (int i = 1; i < lines.Count; i++)
            {
                var lineNumber = i + 1;
                var parts = lines[i].Split(',');
                
                if (parts.Length != 4)
                {
                    result.Errors.Add($"Line {lineNumber}: Expected 4 columns, found {parts.Length}.");
                    continue;
                }
                
                var badgeName = parts[0].Trim();
                var semesterStr = parts[1].Trim();
                var session = parts[2].Trim();
                var sectionName = parts[3].Trim();
                
                // Validate badge
                if (string.IsNullOrWhiteSpace(badgeName))
                {
                    result.Errors.Add($"Line {lineNumber}: Badge name is required.");
                    continue;
                }
                
                if (!badges.ContainsKey(badgeName.ToLower()))
                {
                    result.Errors.Add($"Line {lineNumber}: Badge '{badgeName}' does not exist.");
                    continue;
                }
                
                // Validate semester
                if (!int.TryParse(semesterStr, out int semester) || semester < 1 || semester > 8)
                {
                    result.Errors.Add($"Line {lineNumber}: Invalid semester '{semesterStr}' (must be 1-8).");
                    continue;
                }
                
                // Validate session
                if (string.IsNullOrWhiteSpace(session))
                {
                    result.Errors.Add($"Line {lineNumber}: Session is required.");
                    continue;
                }
                
                // Validate section name
                if (string.IsNullOrWhiteSpace(sectionName))
                {
                    result.Errors.Add($"Line {lineNumber}: Section name is required.");
                    continue;
                }
                
                // Check uniqueness
                var key = $"{badgeName.ToLower()}|{semester}|{session}|{sectionName.ToLower()}";
                
                if (seenSections.Contains(key))
                {
                    result.Errors.Add($"Line {lineNumber}: Duplicate section '{badgeName} - {sectionName} ({semester}/{session})' in file.");
                    continue;
                }
                
                if (existingSections.Any(s => s.Name.ToLower() == badgeName.ToLower() && s.Semester == semester && s.Session == session && s.SectionName.ToLower() == sectionName.ToLower()))
                {
                    result.Errors.Add($"Line {lineNumber}: Section '{badgeName} - {sectionName} ({semester}/{session})' already exists.");
                    continue;
                }
                
                seenSections.Add(key);
                result.ValidatedSections.Add(new SectionImportModel
                {
                    BadgeId = badges[badgeName.ToLower()],
                    BadgeName = badgeName,
                    SectionName = sectionName,
                    Semester = semester,
                    Session = session
                });
            }
            
            result.IsValid = result.Errors.Count == 0 && result.ValidatedSections.Count > 0;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error: {ex.Message}");
        }
        
        return result;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Validate email format
    /// </summary>
    private bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            var regex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
            return regex.IsMatch(email);
        }
        catch
        {
            return false;
        }
    }

    #endregion
}
