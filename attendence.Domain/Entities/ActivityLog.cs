namespace attendence.Domain.Entities;

public class ActivityLog
{
    public int Id { get; set; }
    public int ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;

    // Navigation properties
    public User Actor { get; set; } = null!;
}