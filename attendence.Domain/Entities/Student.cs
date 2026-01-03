namespace attendence.Domain.Entities;

public class Student
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int SectionId { get; set; }
    public string RollNo { get; set; } = string.Empty;
    public string? FatherName { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public Section Section { get; set; } = null!;
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
}