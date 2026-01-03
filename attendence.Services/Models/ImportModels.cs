namespace attendence.Services.Models;

/// <summary>
/// User credential model for bulk email sending
/// </summary>
public class UserCredential
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? SectionName { get; set; }
}

/// <summary>
/// Model for importing student data from CSV/Excel
/// </summary>
public class StudentImportModel
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FatherName { get; set; } = string.Empty;
    public string RollNumber { get; set; } = string.Empty;
    
    // Legacy fields for old import system (ImportStudents.cshtml)
    public string? SectionName { get; set; }
    public string? BadgeNumber { get; set; }
}

/// <summary>
/// Model for importing teacher data from CSV/Excel
/// </summary>
public class TeacherImportModel
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string BadgeNumber { get; set; } = string.Empty;
}

/// <summary>
/// Result of import validation
/// </summary>
public class ImportValidationResult<T>
{
    public List<T> ValidRecords { get; set; } = new();
    public List<ImportError> Errors { get; set; } = new();
    public int TotalRecords { get; set; }
    public int ValidCount => ValidRecords.Count;
    public int ErrorCount => Errors.Count;
}

/// <summary>
/// Import error details
/// </summary>
public class ImportError
{
    public int RowNumber { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<string> ErrorMessages { get; set; } = new();
}

/// <summary>
/// Result of bulk user creation
/// </summary>
public class BulkCreateResult
{
    public int TotalAttempted { get; set; }
    public int SuccessCount { get; set; }
    public int SkippedCount { get; set; }
    public List<string> CreatedUsers { get; set; } = new();
    public List<string> SkippedEmails { get; set; } = new();
    public List<UserCredential> UserCredentials { get; set; } = new();
}

/// <summary>
/// Result of badges import validation
/// </summary>
public class BadgesImportValidationResult
{
    public bool IsValid { get; set; }
    public List<string> ValidatedBadges { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Model for importing section data
/// </summary>
public class SectionImportModel
{
    public int BadgeId { get; set; }
    public string BadgeName { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public int Semester { get; set; }
    public string Session { get; set; } = string.Empty;
}

/// <summary>
/// Result of sections import validation
/// </summary>
public class SectionsImportValidationResult
{
    public bool IsValid { get; set; }
    public List<SectionImportModel> ValidatedSections { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}
