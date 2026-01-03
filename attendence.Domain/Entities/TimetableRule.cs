namespace attendence.Domain.Entities;

public class TimetableRule
{
    public int Id { get; set; }
    public int TeacherCourseId { get; set; }
    public string DaysOfWeek { get; set; } = string.Empty; // Mon,Wed,Fri
    public TimeSpan StartTime { get; set; }
    public int DurationMinutes { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Room { get; set; }
    public string? LectureType { get; set; } // Theory / Lab

    // Navigation properties
    public TeacherCourse TeacherCourse { get; set; } = null!;
    public ICollection<Lecture> Lectures { get; set; } = new List<Lecture>();
}