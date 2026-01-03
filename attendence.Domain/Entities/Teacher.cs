namespace attendence.Domain.Entities;

public class Teacher
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string BadgeNumber { get; set; } = string.Empty;
    public string? Designation { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public ICollection<TeacherCourse> TeacherCourses { get; set; } = new List<TeacherCourse>();
    public ICollection<AttendanceEditRequest> AttendanceEditRequests { get; set; } = new List<AttendanceEditRequest>();
}