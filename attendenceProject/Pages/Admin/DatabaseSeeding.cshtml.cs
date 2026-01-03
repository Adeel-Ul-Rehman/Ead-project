using attendence.Data.Data;
using attendence.Domain.Entities;
using attendence.Services.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace attendenceProject.Pages.Admin;

[Authorize(Roles = "Admin")]
public class DatabaseSeedingModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly PasswordHasher _passwordHasher;

    public DatabaseSeedingModel(ApplicationDbContext context, PasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public string? Message { get; set; }
    public bool IsSuccess { get; set; }
    public bool IsProcessing { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            IsProcessing = true;

            // Step 1: Clean database except Admin
            await CleanDatabase();

            // Step 2: Create Badges
            var badges = await CreateBadges();

            // Step 3: Create Sections
            var sections = await CreateSections(badges);

            // Step 4: Create Courses
            var courses = await CreateCourses();

            // Step 5: Create Teachers
            var teachers = await CreateTeachers();

            // Step 6: Create Students (120 students across 10 sections)
            await CreateStudents(sections);

            // Step 7: Assign Teachers to Courses
            var teacherCourses = await AssignTeachersToCourses(teachers, courses, sections);

            // Step 8: Create Timetable Rules (Mon-Fri distributed, Friday lighter)
            await CreateTimetableRules(teacherCourses);

            // Step 9: Create Lectures (Oct 15, 2025 - Jan 20, 2026)
            var lectures = await CreateLectures();

            // Step 10: Create Attendance Records (~75% attendance)
            await CreateAttendanceRecords(lectures);

            // Step 11: Add Holidays
            await CreateHolidays();

            await _context.SaveChangesAsync();

            IsSuccess = true;
            Message = $"✅ Database successfully populated! 10 Teachers, 120 Students across 10 sections (BSCS 22-25), {courses.Count} Courses, timetable created for all teacher-course assignments from Oct 15, 2025 to Jan 20, 2026 with ~75% attendance rate. Schedule includes free periods, labs, and 1-3 credit hour lectures Mon-Fri 8AM-4PM.";
        }
        catch (Exception ex)
        {
            IsSuccess = false;
            Message = $"❌ Error: {ex.Message}\n{ex.InnerException?.Message}";
        }
        finally
        {
            IsProcessing = false;
        }

        return Page();
    }

    private async Task CleanDatabase()
    {
        // Delete in correct order to avoid foreign key constraints
        _context.AttendanceRecords.RemoveRange(_context.AttendanceRecords);
        _context.Lectures.RemoveRange(_context.Lectures);
        _context.TimetableRules.RemoveRange(_context.TimetableRules);
        _context.TeacherCourses.RemoveRange(_context.TeacherCourses);
        _context.AttendanceEditRequests.RemoveRange(_context.AttendanceEditRequests);
        _context.AttendanceExtensionRequests.RemoveRange(_context.AttendanceExtensionRequests);
        _context.Notifications.RemoveRange(_context.Notifications);
        _context.ActivityLogs.RemoveRange(_context.ActivityLogs);
        
        // Delete users except Admin
        var nonAdminUsers = await _context.Users.Where(u => u.Role != "Admin").ToListAsync();
        _context.Users.RemoveRange(nonAdminUsers);
        
        _context.Students.RemoveRange(_context.Students);
        _context.Teachers.RemoveRange(_context.Teachers);
        _context.Courses.RemoveRange(_context.Courses);
        _context.Sections.RemoveRange(_context.Sections);
        _context.Badges.RemoveRange(_context.Badges);
        _context.Holidays.RemoveRange(_context.Holidays);

        await _context.SaveChangesAsync();
    }

    private async Task<List<Badge>> CreateBadges()
    {
        var badges = new List<Badge>
        {
            new Badge { Name = "BSCS", CreatedAt = DateTime.Now }
        };

        _context.Badges.AddRange(badges);
        await _context.SaveChangesAsync();
        return badges;
    }

    private async Task<List<Section>> CreateSections(List<Badge> badges)
    {
        var sections = new List<Section>
        {
            // BSCS 22
            new Section { Name = "A", BadgeId = badges[0].Id, Semester = 1, Session = "2022" },
            new Section { Name = "B", BadgeId = badges[0].Id, Semester = 1, Session = "2022" },
            // BSCS 23
            new Section { Name = "A", BadgeId = badges[0].Id, Semester = 3, Session = "2023" },
            new Section { Name = "B", BadgeId = badges[0].Id, Semester = 3, Session = "2023" },
            new Section { Name = "C", BadgeId = badges[0].Id, Semester = 3, Session = "2023" },
            // BSCS 24
            new Section { Name = "A", BadgeId = badges[0].Id, Semester = 5, Session = "2024" },
            new Section { Name = "B", BadgeId = badges[0].Id, Semester = 5, Session = "2024" },
            new Section { Name = "C", BadgeId = badges[0].Id, Semester = 5, Session = "2024" },
            // BSCS 25
            new Section { Name = "A", BadgeId = badges[0].Id, Semester = 7, Session = "2025" },
            new Section { Name = "B", BadgeId = badges[0].Id, Semester = 7, Session = "2025" }
        };

        _context.Sections.AddRange(sections);
        await _context.SaveChangesAsync();
        return sections;
    }

    private async Task<List<Course>> CreateCourses()
    {
        var courses = new List<Course>
        {
            // Semester 1
            new Course { Code = "CS101", Title = "Programming Fundamentals", CreditHours = 3, IsLab = false },
            new Course { Code = "CS102", Title = "PF Lab", CreditHours = 1, IsLab = true },
            new Course { Code = "CS103", Title = "Applied ICT", CreditHours = 3, IsLab = false },
            new Course { Code = "CS104", Title = "AICT Lab", CreditHours = 1, IsLab = true },
            new Course { Code = "MT101", Title = "Calculus", CreditHours = 3, IsLab = false },
            new Course { Code = "MT102", Title = "Discrete Mathematics", CreditHours = 3, IsLab = false },
            new Course { Code = "EN101", Title = "Functional English", CreditHours = 2, IsLab = false },

            // Semester 3
            new Course { Code = "CS201", Title = "Data Structures & Algorithms", CreditHours = 3, IsLab = false },
            new Course { Code = "CS202", Title = "DSA Lab", CreditHours = 1, IsLab = true },
            new Course { Code = "CS203", Title = "Computer Networks", CreditHours = 3, IsLab = false },
            new Course { Code = "CS204", Title = "CN Lab", CreditHours = 1, IsLab = true },
            new Course { Code = "MT201", Title = "Linear Algebra", CreditHours = 3, IsLab = false },
            new Course { Code = "PH201", Title = "Applied Physics", CreditHours = 3, IsLab = false },
            new Course { Code = "IS201", Title = "Islamiyat", CreditHours = 2, IsLab = false },

            // Semester 5
            new Course { Code = "SE301", Title = "Enterprise Application Development", CreditHours = 3, IsLab = false },
            new Course { Code = "CS301", Title = "Computer Architecture", CreditHours = 3, IsLab = false },
            new Course { Code = "CS302", Title = "Introduction to Data Science", CreditHours = 3, IsLab = false },
            new Course { Code = "HS301", Title = "Human Computer Interaction", CreditHours = 2, IsLab = false },
            new Course { Code = "CS303", Title = "Operating Systems", CreditHours = 3, IsLab = false },
            new Course { Code = "CS304", Title = "OS Lab", CreditHours = 1, IsLab = true },
            new Course { Code = "PS301", Title = "Psychology", CreditHours = 2, IsLab = false },

            // Semester 7
            new Course { Code = "CS401", Title = "Software Engineering", CreditHours = 3, IsLab = false },
            new Course { Code = "CS402", Title = "Web Development", CreditHours = 3, IsLab = false },
            new Course { Code = "CS403", Title = "Machine Learning", CreditHours = 3, IsLab = false },
            new Course { Code = "SE401", Title = "Final Year Project", CreditHours = 3, IsLab = false },
            new Course { Code = "CS404", Title = "Software Engineering Lab", CreditHours = 1, IsLab = true },
            new Course { Code = "CS405", Title = "Web Development Lab", CreditHours = 1, IsLab = true }
        };

        _context.Courses.AddRange(courses);
        await _context.SaveChangesAsync();
        return courses;
    }

    private async Task<List<attendence.Domain.Entities.Teacher>> CreateTeachers()
    {
        var teacherData = new[]
        {
            new { Email = "ajadeel229@gmail.com", Name = "Hafiz Danish", Designation = "Professor" },
            new { Email = "annieeve749@gmail.com", Name = "Alina Munir", Designation = "Associate professor" },
            new { Email = "rehmantanzeel052@gmail.com", Name = "Muhammad Nadeem", Designation = "Professor" },
            new { Email = "hadibooksstore01@gmail.com", Name = "Usman ghani", Designation = "assistant professor" },
            new { Email = "vibedchecking@gmail.com", Name = "Miss Rida", Designation = "Lecturer" },
            new { Email = "dominoriderexpense@gmail.com", Name = "Hira Azam", Designation = "senior lecturer" },
            new { Email = "zackcrriss@gmail.com", Name = "Miss Maryam", Designation = "lecturer" },
            new { Email = "syedshayanarshad.1@gmail.com", Name = "Miss Anam", Designation = "associate professor" },
            new { Email = "hadeedhaider59@gmail.com", Name = "miss Ayesha", Designation = "Professor" },
            new { Email = "aneebullah66@gmail.com", Name = "Umer Qasim", Designation = "professor" }
        };

        var teachers = new List<attendence.Domain.Entities.Teacher>();
        var password = "Teacher123";

        foreach (var data in teacherData)
        {
            var user = new User
            {
                Email = data.Email,
                PasswordHash = _passwordHasher.HashPassword(password),
                Role = "Teacher",
                FullName = data.Name,
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var teacher = new attendence.Domain.Entities.Teacher
            {
                UserId = user.Id,
                BadgeNumber = $"TCH{teachers.Count + 1:000}",
                Designation = data.Designation
            };
            _context.Teachers.Add(teacher);
            teachers.Add(teacher);
        }

        await _context.SaveChangesAsync();
        return teachers;
    }

    private async Task CreateStudents(List<Section> sections)
    {
        var maleNames = new[] { "Muhammad Ahmed", "Ali Hassan", "Hassan Raza", "Usman Khan", "Bilal Ahmad", "Fahad Iqbal", "Zain Abbas", "Haris Ali", "Usama Saeed", "Talha Akram", "Adnan Raza", "Arslan Javed", "Faisal Mahmood", "Imran Shah", "Omar Farooq", "Saad Malik", "Hamza Tariq", "Abdullah Khan", "Yusuf Ali", "Ibrahim Hassan" };
        var femaleNames = new[] { "Ayesha Khan", "Fatima Noor", "Hira Aslam", "Sana Malik", "Zainab Ali", "Maryam Sheikh", "Amna Tariq", "Nida Hassan", "Sara Ahmad", "Bushra Iqbal", "Rabia Zahid", "Mahnoor Riaz", "Iqra Shahid", "Sidra Jamil", "Laiba Usman", "Amina Khan", "Hafsa Ali", "Khadija Hassan", "Asma Malik", "Sadia Ahmed" };
        var fatherNames = new[] { "Muhammad Akram", "Ahmad Shah", "Tariq Mahmood", "Malik Saeed", "Khan Raza", "Iqbal Hussain", "Abbas Jamil", "Ali Asghar", "Hassan Riaz", "Usman Farooq", "Bilal Aziz", "Fahad Qureshi", "Zain Malik", "Hamza Siddiqui", "Abdullah Bashir", "Omar Khan", "Saad Ali", "Yusuf Hassan", "Ibrahim Raza", "Talha Mahmood" };
        var random = new Random();

        // BSCS 22 A: 10 students (2022cs601 to 2022cs610)
        for (int i = 601; i <= 610; i++)
        {
            var rollNo = $"2022cs{i}";
            var email = $"{rollNo}@student.uet.edu.pk";
            var isMale = random.Next(0, 2) == 0;
            var name = isMale ? maleNames[random.Next(maleNames.Length)] : femaleNames[random.Next(femaleNames.Length)];
            
            var user = new User
            {
                Email = email,
                PasswordHash = _passwordHasher.HashPassword(rollNo),
                Role = "Student",
                FullName = name,
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var student = new attendence.Domain.Entities.Student
            {
                UserId = user.Id,
                RollNo = rollNo,
                SectionId = sections[0].Id, // BSCS 22 A
                FatherName = fatherNames[random.Next(fatherNames.Length)]
            };
            _context.Students.Add(student);
        }

        // BSCS 22 B: 10 students (2022cs611 to 2022cs620)
        for (int i = 611; i <= 620; i++)
        {
            var rollNo = $"2022cs{i}";
            var email = $"{rollNo}@student.uet.edu.pk";
            var isMale = random.Next(0, 2) == 0;
            var name = isMale ? maleNames[random.Next(maleNames.Length)] : femaleNames[random.Next(femaleNames.Length)];
            
            var user = new User
            {
                Email = email,
                PasswordHash = _passwordHasher.HashPassword(rollNo),
                Role = "Student",
                FullName = name,
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var student = new attendence.Domain.Entities.Student
            {
                UserId = user.Id,
                RollNo = rollNo,
                SectionId = sections[1].Id, // BSCS 22 B
                FatherName = fatherNames[random.Next(fatherNames.Length)]
            };
            _context.Students.Add(student);
        }

        // BSCS 23 A: 15 students (2023cs601 to 2023cs615)
        for (int i = 601; i <= 615; i++)
        {
            var rollNo = $"2023cs{i}";
            var email = $"{rollNo}@student.uet.edu.pk";
            var isMale = random.Next(0, 2) == 0;
            var name = isMale ? maleNames[random.Next(maleNames.Length)] : femaleNames[random.Next(femaleNames.Length)];
            
            var user = new User
            {
                Email = email,
                PasswordHash = _passwordHasher.HashPassword(rollNo),
                Role = "Student",
                FullName = name,
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var student = new attendence.Domain.Entities.Student
            {
                UserId = user.Id,
                RollNo = rollNo,
                SectionId = sections[2].Id, // BSCS 23 A
                FatherName = fatherNames[random.Next(fatherNames.Length)]
            };
            _context.Students.Add(student);
        }

        // BSCS 23 B: 15 students (2023cs616 to 2023cs630)
        for (int i = 616; i <= 630; i++)
        {
            var rollNo = $"2023cs{i}";
            var email = $"{rollNo}@student.uet.edu.pk";
            var isMale = random.Next(0, 2) == 0;
            var name = isMale ? maleNames[random.Next(maleNames.Length)] : femaleNames[random.Next(femaleNames.Length)];
            
            var user = new User
            {
                Email = email,
                PasswordHash = _passwordHasher.HashPassword(rollNo),
                Role = "Student",
                FullName = name,
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var student = new attendence.Domain.Entities.Student
            {
                UserId = user.Id,
                RollNo = rollNo,
                SectionId = sections[3].Id, // BSCS 23 B
                FatherName = fatherNames[random.Next(fatherNames.Length)]
            };
            _context.Students.Add(student);
        }

        // BSCS 23 C: 20 students (2023cs631 to 2023cs650)
        for (int i = 631; i <= 650; i++)
        {
            var rollNo = $"2023cs{i}";
            var email = $"{rollNo}@student.uet.edu.pk";
            var isMale = random.Next(0, 2) == 0;
            var name = isMale ? maleNames[random.Next(maleNames.Length)] : femaleNames[random.Next(femaleNames.Length)];
            
            var user = new User
            {
                Email = email,
                PasswordHash = _passwordHasher.HashPassword(rollNo),
                Role = "Student",
                FullName = name,
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var student = new attendence.Domain.Entities.Student
            {
                UserId = user.Id,
                RollNo = rollNo,
                SectionId = sections[4].Id, // BSCS 23 C
                FatherName = fatherNames[random.Next(fatherNames.Length)]
            };
            _context.Students.Add(student);
        }

        // BSCS 24 A: 10 students (2024cs601 to 2024cs610)
        for (int i = 601; i <= 610; i++)
        {
            var rollNo = $"2024cs{i}";
            var email = $"{rollNo}@student.uet.edu.pk";
            var isMale = random.Next(0, 2) == 0;
            var name = isMale ? maleNames[random.Next(maleNames.Length)] : femaleNames[random.Next(femaleNames.Length)];
            
            var user = new User
            {
                Email = email,
                PasswordHash = _passwordHasher.HashPassword(rollNo),
                Role = "Student",
                FullName = name,
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var student = new attendence.Domain.Entities.Student
            {
                UserId = user.Id,
                RollNo = rollNo,
                SectionId = sections[5].Id, // BSCS 24 A
                FatherName = fatherNames[random.Next(fatherNames.Length)]
            };
            _context.Students.Add(student);
        }

        // BSCS 24 B: 10 students (2024cs611 to 2024cs620)
        for (int i = 611; i <= 620; i++)
        {
            var rollNo = $"2024cs{i}";
            var email = $"{rollNo}@student.uet.edu.pk";
            var isMale = random.Next(0, 2) == 0;
            var name = isMale ? maleNames[random.Next(maleNames.Length)] : femaleNames[random.Next(femaleNames.Length)];
            
            var user = new User
            {
                Email = email,
                PasswordHash = _passwordHasher.HashPassword(rollNo),
                Role = "Student",
                FullName = name,
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var student = new attendence.Domain.Entities.Student
            {
                UserId = user.Id,
                RollNo = rollNo,
                SectionId = sections[6].Id, // BSCS 24 B
                FatherName = fatherNames[random.Next(fatherNames.Length)]
            };
            _context.Students.Add(student);
        }

        // BSCS 24 C: 10 students (2024cs621 to 2024cs630)
        for (int i = 621; i <= 630; i++)
        {
            var rollNo = $"2024cs{i}";
            var email = $"{rollNo}@student.uet.edu.pk";
            var isMale = random.Next(0, 2) == 0;
            var name = isMale ? maleNames[random.Next(maleNames.Length)] : femaleNames[random.Next(femaleNames.Length)];
            
            var user = new User
            {
                Email = email,
                PasswordHash = _passwordHasher.HashPassword(rollNo),
                Role = "Student",
                FullName = name,
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var student = new attendence.Domain.Entities.Student
            {
                UserId = user.Id,
                RollNo = rollNo,
                SectionId = sections[7].Id, // BSCS 24 C
                FatherName = fatherNames[random.Next(fatherNames.Length)]
            };
            _context.Students.Add(student);
        }

        // BSCS 25 A: 10 students (2025cs601 to 2025cs610)
        for (int i = 601; i <= 610; i++)
        {
            var rollNo = $"2025cs{i}";
            var email = $"{rollNo}@student.uet.edu.pk";
            var isMale = random.Next(0, 2) == 0;
            var name = isMale ? maleNames[random.Next(maleNames.Length)] : femaleNames[random.Next(femaleNames.Length)];
            
            var user = new User
            {
                Email = email,
                PasswordHash = _passwordHasher.HashPassword(rollNo),
                Role = "Student",
                FullName = name,
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var student = new attendence.Domain.Entities.Student
            {
                UserId = user.Id,
                RollNo = rollNo,
                SectionId = sections[8].Id, // BSCS 25 A
                FatherName = fatherNames[random.Next(fatherNames.Length)]
            };
            _context.Students.Add(student);
        }

        // BSCS 25 B: 10 students (2025cs611 to 2025cs620)
        for (int i = 611; i <= 620; i++)
        {
            var rollNo = $"2025cs{i}";
            var email = $"{rollNo}@student.uet.edu.pk";
            var isMale = random.Next(0, 2) == 0;
            var name = isMale ? maleNames[random.Next(maleNames.Length)] : femaleNames[random.Next(femaleNames.Length)];
            
            var user = new User
            {
                Email = email,
                PasswordHash = _passwordHasher.HashPassword(rollNo),
                Role = "Student",
                FullName = name,
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var student = new attendence.Domain.Entities.Student
            {
                UserId = user.Id,
                RollNo = rollNo,
                SectionId = sections[9].Id, // BSCS 25 B
                FatherName = fatherNames[random.Next(fatherNames.Length)]
            };
            _context.Students.Add(student);
        }

        await _context.SaveChangesAsync();
    }

    private async Task<List<TeacherCourse>> AssignTeachersToCourses(List<attendence.Domain.Entities.Teacher> teachers, List<Course> courses, List<Section> sections)
    {
        var teacherCourses = new List<TeacherCourse>();

        // Assign teachers to courses based on semesters
        // Semester 1: teachers 0-1
        var sem1Sections = sections.Where(s => s.Semester == 1).ToList();
        var sem1Courses = courses.Where(c => c.Code.StartsWith("CS1") || c.Code.StartsWith("MT1") || c.Code.StartsWith("EN1")).ToList();
        foreach (var section in sem1Sections)
        {
            foreach (var course in sem1Courses)
            {
                teacherCourses.Add(new TeacherCourse { TeacherId = teachers[0].Id, CourseId = course.Id, SectionId = section.Id, AssignedAt = DateTime.Now });
            }
        }

        // Semester 3: teachers 1-2
        var sem3Sections = sections.Where(s => s.Semester == 3).ToList();
        var sem3Courses = courses.Where(c => c.Code.StartsWith("CS2") || c.Code.StartsWith("MT2") || c.Code.StartsWith("PH2") || c.Code.StartsWith("IS2")).ToList();
        foreach (var section in sem3Sections)
        {
            foreach (var course in sem3Courses)
            {
                teacherCourses.Add(new TeacherCourse { TeacherId = teachers[1].Id, CourseId = course.Id, SectionId = section.Id, AssignedAt = DateTime.Now });
            }
        }

        // Semester 5: teachers 2-4
        var sem5Sections = sections.Where(s => s.Semester == 5).ToList();
        var sem5Courses = courses.Where(c => c.Code.StartsWith("SE3") || c.Code.StartsWith("CS3") || c.Code.StartsWith("HS3") || c.Code.StartsWith("PS3")).ToList();
        foreach (var section in sem5Sections)
        {
            foreach (var course in sem5Courses)
            {
                teacherCourses.Add(new TeacherCourse { TeacherId = teachers[2].Id, CourseId = course.Id, SectionId = section.Id, AssignedAt = DateTime.Now });
            }
        }

        // Semester 7: teachers 3-5
        var sem7Sections = sections.Where(s => s.Semester == 7).ToList();
        var sem7Courses = courses.Where(c => c.Code.StartsWith("CS4") || c.Code.StartsWith("SE4")).ToList();
        foreach (var section in sem7Sections)
        {
            foreach (var course in sem7Courses)
            {
                teacherCourses.Add(new TeacherCourse { TeacherId = teachers[3].Id, CourseId = course.Id, SectionId = section.Id, AssignedAt = DateTime.Now });
            }
        }

        // Additional assignments for remaining teachers
        for (int i = 4; i < teachers.Count; i++)
        {
            var teacher = teachers[i];
            var randomSections = sections.OrderBy(x => Guid.NewGuid()).Take(2).ToList();
            var randomCourses = courses.OrderBy(x => Guid.NewGuid()).Take(3).ToList();
            foreach (var section in randomSections)
            {
                foreach (var course in randomCourses)
                {
                    teacherCourses.Add(new TeacherCourse { TeacherId = teacher.Id, CourseId = course.Id, SectionId = section.Id, AssignedAt = DateTime.Now });
                }
            }
        }

        _context.TeacherCourses.AddRange(teacherCourses);
        await _context.SaveChangesAsync();
        return teacherCourses;
    }

    private async Task CreateTimetableRules(List<TeacherCourse> teacherCourses)
    {
        var days = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" };
        var times = new[] { new TimeSpan(8, 0, 0), new TimeSpan(9, 0, 0), new TimeSpan(10, 30, 0), new TimeSpan(11, 30, 0), new TimeSpan(13, 0, 0), new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0) };
        var rooms = new[] { "Room 101", "Room 102", "Room 103", "Lab 201", "Lab 202", "Room 104", "Room 105" };

        for (int i = 0; i < teacherCourses.Count; i++)
        {
            var teacherCourse = teacherCourses[i];
            var course = await _context.Courses.FindAsync(teacherCourse.CourseId);
            var duration = course.IsLab ? 120 : 60; // Labs 2 hours, theory 1 hour
            var day = days[i % days.Length];
            var time = times[i % times.Length];
            var room = course.IsLab ? rooms[3 + (i % 2)] : rooms[i % 3]; // Labs in lab rooms

            _context.TimetableRules.Add(new TimetableRule
            {
                TeacherCourseId = teacherCourse.Id,
                StartTime = time,
                DurationMinutes = duration,
                DaysOfWeek = day,
                Room = room,
                LectureType = "Regular",
                StartDate = new DateTime(2025, 10, 15),
                EndDate = new DateTime(2026, 1, 20)
            });
        }

        await _context.SaveChangesAsync();
    }

    private async Task<List<Lecture>> CreateLectures()
    {
        var lectures = new List<Lecture>();
        var timetableRules = await _context.TimetableRules.ToListAsync();
        var startDate = new DateTime(2025, 10, 15);
        var endDate = new DateTime(2026, 1, 20);
        var now = DateTime.Now;

        var holidays = new List<DateTime>
        {
            new DateTime(2025, 12, 25), // Christmas
            new DateTime(2025, 12, 31), // New Year Eve
            new DateTime(2026, 1, 1)    // New Year
        };

        foreach (var rule in timetableRules)
        {
            var dayOfWeek = rule.DaysOfWeek switch
            {
                "Monday" => DayOfWeek.Monday,
                "Tuesday" => DayOfWeek.Tuesday,
                "Wednesday" => DayOfWeek.Wednesday,
                "Thursday" => DayOfWeek.Thursday,
                "Friday" => DayOfWeek.Friday,
                _ => DayOfWeek.Monday
            };

            var currentDate = startDate;
            while (currentDate <= endDate)
            {
                if (currentDate.DayOfWeek == dayOfWeek && !holidays.Contains(currentDate.Date))
                {
                    var startDateTime = currentDate.Date.Add(rule.StartTime);
                    var endDateTime = startDateTime.AddMinutes(rule.DurationMinutes);

                    string status;
                    if (currentDate.Date < now.Date)
                    {
                        status = "Completed";
                    }
                    else if (currentDate.Date == now.Date)
                    {
                        status = "Open";
                    }
                    else
                    {
                        status = "Scheduled";
                    }

                    var lecture = new Lecture
                    {
                        TimetableRuleId = rule.Id,
                        StartDateTime = startDateTime,
                        EndDateTime = endDateTime,
                        Status = status,
                        LectureType = rule.LectureType ?? "Regular",
                        AttendanceDeadline = status == "Completed" ? startDateTime.AddHours(2) : null
                    };
                    _context.Lectures.Add(lecture);
                    lectures.Add(lecture);
                }
                currentDate = currentDate.AddDays(1);
            }
        }

        await _context.SaveChangesAsync();
        return lectures;
    }

    private async Task CreateAttendanceRecords(List<Lecture> lectures)
    {
        var random = new Random();
        var completedLectures = lectures.Where(l => l.Status == "Completed").ToList();

        foreach (var lecture in completedLectures)
        {
            var timetableRule = await _context.TimetableRules
                .Include(tr => tr.TeacherCourse)
                .FirstOrDefaultAsync(tr => tr.Id == lecture.TimetableRuleId);

            if (timetableRule != null)
            {
                var students = await _context.Students
                    .Where(s => s.SectionId == timetableRule.TeacherCourse.SectionId)
                    .ToListAsync();

                foreach (var student in students)
                {
                    // 75% Present, 5% Late, 20% Absent
                    var randomValue = random.Next(100);
                    string status;
                    if (randomValue < 75)
                        status = "Present";
                    else if (randomValue < 80)
                        status = "Late";
                    else
                        status = "Absent";

                    var attendanceRecord = new AttendanceRecord
                    {
                        LectureId = lecture.Id,
                        StudentId = student.Id,
                        Status = status,
                        MarkedAt = lecture.StartDateTime.AddMinutes(random.Next(5, 30))
                    };
                    _context.AttendanceRecords.Add(attendanceRecord);
                }
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task CreateHolidays()
    {
        var holidays = new List<Holiday>
        {
            new Holiday { Date = new DateTime(2025, 12, 25), Reason = "Christmas Day", CreatedAt = DateTime.Now },
            new Holiday { Date = new DateTime(2025, 12, 31), Reason = "New Year Holiday", CreatedAt = DateTime.Now },
            new Holiday { Date = new DateTime(2026, 1, 1), Reason = "New Year Day", CreatedAt = DateTime.Now }
        };

        _context.Holidays.AddRange(holidays);
        await _context.SaveChangesAsync();
    }
}
