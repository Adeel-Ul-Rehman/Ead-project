namespace attendence.Domain.Entities;

public class AttendanceRecord
{
    public int Id { get; set; }
    public int LectureId { get; set; }
    public int StudentId { get; set; }
    public string Status { get; set; } = "Present"; // Present/Absent/Late/Sick/Excused
    public DateTime MarkedAt { get; set; } = DateTime.Now;

    // Navigation properties
    public Lecture Lecture { get; set; } = null!;
    public Student Student { get; set; } = null!;
}