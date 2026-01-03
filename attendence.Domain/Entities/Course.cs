namespace attendence.Domain.Entities;

public class Course
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int CreditHours { get; set; }
    public bool IsLab { get; set; }

    // Navigation properties
    public ICollection<TeacherCourse> TeacherCourses { get; set; } = new List<TeacherCourse>();
}