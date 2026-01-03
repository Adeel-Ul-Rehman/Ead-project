namespace attendence.Domain.Entities;

public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string TargetRole { get; set; } = "All"; // All, Student, Teacher, Admin
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation properties
    public User User { get; set; } = null!;
}