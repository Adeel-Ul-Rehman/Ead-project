using attendence.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace attendence.Data.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // Check if we already have users
        if (await context.Users.AnyAsync())
        {
            return; // Database has been seeded
        }

        // Create Admin User
        var adminUser = new User
        {
            FullName = "Admin User",
            Email = "admin@university.edu",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
            Role = "Admin",
            CreatedAt = DateTime.Now
        };
        context.Users.Add(adminUser);

        // Create Teacher User
        var teacherUser = new User
        {
            FullName = "John Teacher",
            Email = "teacher@university.edu",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
            Role = "Teacher",
            CreatedAt = DateTime.Now
        };
        context.Users.Add(teacherUser);

        await context.SaveChangesAsync();

        // Create Teacher entity
        var teacher = new Teacher
        {
            UserId = teacherUser.Id,
            Designation = "Assistant Professor"
        };
        context.Teachers.Add(teacher);

        // Create Badge
        var badge = new Badge
        {
            Name = "Computer Science",
            CreatedAt = DateTime.Now
        };
        context.Badges.Add(badge);

        await context.SaveChangesAsync();

        // Create Section
        var section = new Section
        {
            Name = "CS-A",
            BadgeId = badge.Id,
            Semester = 3,
            Session = "2023-2027"
        };
        context.Sections.Add(section);

        await context.SaveChangesAsync();

        // Create Student User
        var studentUser = new User
        {
            FullName = "Alice Student",
            Email = "student@university.edu",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
            Role = "Student",
            CreatedAt = DateTime.Now
        };
        context.Users.Add(studentUser);

        await context.SaveChangesAsync();

        // Create Student entity
        var student = new Student
        {
            UserId = studentUser.Id,
            RollNo = "CS-2023-001",
            FatherName = "John Smith",
            SectionId = section.Id
        };
        context.Students.Add(student);

        // Create a sample course
        var course = new Course
        {
            Code = "CS101",
            Title = "Introduction to Programming",
            CreditHours = 3,
            IsLab = false
        };
        context.Courses.Add(course);

        await context.SaveChangesAsync();

        // Assign teacher to course
        var teacherCourse = new TeacherCourse
        {
            TeacherId = teacher.Id,
            CourseId = course.Id,
            SectionId = section.Id
        };
        context.TeacherCourses.Add(teacherCourse);

        await context.SaveChangesAsync();

        Console.WriteLine("✅ Database seeded successfully!");
    }
}
