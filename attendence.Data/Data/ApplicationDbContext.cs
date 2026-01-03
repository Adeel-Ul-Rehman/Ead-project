using attendence.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace attendence.Data.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // DbSets
    public DbSet<User> Users { get; set; }
    public DbSet<Badge> Badges { get; set; }
    public DbSet<Section> Sections { get; set; }
    public DbSet<Student> Students { get; set; }
    public DbSet<Teacher> Teachers { get; set; }
    public DbSet<Course> Courses { get; set; }
    public DbSet<TeacherCourse> TeacherCourses { get; set; }
    public DbSet<TimetableRule> TimetableRules { get; set; }
    public DbSet<Lecture> Lectures { get; set; }
    public DbSet<AttendanceRecord> AttendanceRecords { get; set; }
    public DbSet<AttendanceEditRequest> AttendanceEditRequests { get; set; }
    public DbSet<Holiday> Holidays { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<ActivityLog> ActivityLogs { get; set; }
    public DbSet<AttendanceExtensionRequest> AttendanceExtensionRequests { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configurations
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.FullName).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(150).IsRequired();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Role).HasMaxLength(20).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
        });

        // Badge configurations
        modelBuilder.Entity<Badge>(entity =>
        {
            entity.ToTable("Badges");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
        });

        // Section configurations
        modelBuilder.Entity<Section>(entity =>
        {
            entity.ToTable("Sections");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Session).HasMaxLength(20).IsRequired();
            entity.HasOne(e => e.Badge)
                .WithMany(b => b.Sections)
                .HasForeignKey(e => e.BadgeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Student configurations
        modelBuilder.Entity<Student>(entity =>
        {
            entity.ToTable("Students");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.RollNo).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.RollNo).IsUnique();
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.Property(e => e.FatherName).HasMaxLength(150);
            entity.HasOne(e => e.User)
                .WithOne(u => u.Student)
                .HasForeignKey<Student>(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Section)
                .WithMany(s => s.Students)
                .HasForeignKey(e => e.SectionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Teacher configurations
        modelBuilder.Entity<Teacher>(entity =>
        {
            entity.ToTable("Teachers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.Property(e => e.Designation).HasMaxLength(100);
            entity.HasOne(e => e.User)
                .WithOne(u => u.Teacher)
                .HasForeignKey<Teacher>(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Course configurations
        modelBuilder.Entity<Course>(entity =>
        {
            entity.ToTable("Courses");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Code).HasMaxLength(20).IsRequired();
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Title).HasMaxLength(150).IsRequired();
        });

        // TeacherCourse configurations
        modelBuilder.Entity<TeacherCourse>(entity =>
        {
            entity.ToTable("TeacherCourses");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.HasOne(e => e.Teacher)
                .WithMany(t => t.TeacherCourses)
                .HasForeignKey(e => e.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Course)
                .WithMany(c => c.TeacherCourses)
                .HasForeignKey(e => e.CourseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Section)
                .WithMany(s => s.TeacherCourses)
                .HasForeignKey(e => e.SectionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // TimetableRule configurations
        modelBuilder.Entity<TimetableRule>(entity =>
        {
            entity.ToTable("TimetableRules");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.DaysOfWeek).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Room).HasMaxLength(50);
            entity.Property(e => e.LectureType).HasMaxLength(20);
            entity.HasOne(e => e.TeacherCourse)
                .WithMany(tc => tc.TimetableRules)
                .HasForeignKey(e => e.TeacherCourseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Lecture configurations
        modelBuilder.Entity<Lecture>(entity =>
        {
            entity.ToTable("Lectures");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Scheduled");
            entity.Property(e => e.LectureType).HasMaxLength(50).IsRequired().HasDefaultValue("Regular");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.HasIndex(e => e.StartDateTime);
            entity.HasOne(e => e.TimetableRule)
                .WithMany(tr => tr.Lectures)
                .HasForeignKey(e => e.TimetableRuleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.CreatedByTeacher)
                .WithMany()
                .HasForeignKey(e => e.CreatedByTeacherId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // AttendanceRecord configurations
        modelBuilder.Entity<AttendanceRecord>(entity =>
        {
            entity.ToTable("AttendanceRecords");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
            entity.Property(e => e.MarkedAt).HasDefaultValueSql("GETDATE()");
            entity.HasIndex(e => e.LectureId);
            entity.HasIndex(e => e.StudentId);
            entity.HasOne(e => e.Lecture)
                .WithMany(l => l.AttendanceRecords)
                .HasForeignKey(e => e.LectureId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Student)
                .WithMany(s => s.AttendanceRecords)
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // AttendanceEditRequest configurations
        modelBuilder.Entity<AttendanceEditRequest>(entity =>
        {
            entity.ToTable("AttendanceEditRequests");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Reason).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Pending");
            entity.Property(e => e.RequestedAt).HasDefaultValueSql("GETDATE()");
            entity.HasOne(e => e.Lecture)
                .WithMany(l => l.AttendanceEditRequests)
                .HasForeignKey(e => e.LectureId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Teacher)
                .WithMany(t => t.AttendanceEditRequests)
                .HasForeignKey(e => e.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Holiday configurations
        modelBuilder.Entity<Holiday>(entity =>
        {
            entity.ToTable("Holidays");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.HasIndex(e => e.Date).IsUnique();
            entity.Property(e => e.Reason).HasMaxLength(200).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
        });

        // Notification configurations
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notifications");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Title).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Message).HasMaxLength(500).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
            entity.HasOne(e => e.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ActivityLog configurations
        modelBuilder.Entity<ActivityLog>(entity =>
        {
            entity.ToTable("ActivityLogs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Action).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Timestamp).HasDefaultValueSql("GETDATE()");
            entity.HasOne(e => e.Actor)
                .WithMany(u => u.ActivityLogs)
                .HasForeignKey(e => e.ActorId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}