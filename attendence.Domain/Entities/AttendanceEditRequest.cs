namespace attendence.Domain.Entities;

public class AttendanceEditRequest
{
    public int Id { get; set; }
    public int LectureId { get; set; }
    public int TeacherId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
    public DateTime RequestedAt { get; set; } = DateTime.Now;
    public DateTime? ReviewedAt { get; set; }

    // Navigation properties
    public Lecture Lecture { get; set; } = null!;
    public Teacher Teacher { get; set; } = null!;
}