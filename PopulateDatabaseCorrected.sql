-- =============================================
-- COMPREHENSIVE DATABASE POPULATION SCRIPT
-- Date Range: Oct 15, 2025 to Jan 20, 2026
-- Target Attendance: ~75%
-- Lecture Distribution: Mon-Fri (balanced, Friday lighter)
-- =============================================

USE [AttendenceSystem];
GO

-- Clean existing data (keep Admin user)
PRINT 'Cleaning existing data...';

DELETE FROM AttendanceEditRequests;
DELETE FROM AttendanceExtensionRequests;
DELETE FROM ActivityLogs;
DELETE FROM Notifications;
DELETE FROM AttendanceRecords;
DELETE FROM Lectures;
DELETE FROM TimetableRules;
DELETE FROM TeacherCourses;
DELETE FROM Students;
DELETE FROM Teachers;
DELETE FROM Users WHERE Role != 'Admin';
DELETE FROM Sections;
DELETE FROM Courses;
DELETE FROM Badges;
DELETE FROM Holidays;

-- Reset identity seeds
DBCC CHECKIDENT ('AttendanceRecords', RESEED, 0);
DBCC CHECKIDENT ('Lectures', RESEED, 0);
DBCC CHECKIDENT ('TimetableRules', RESEED, 0);
DBCC CHECKIDENT ('TeacherCourses', RESEED, 0);
DBCC CHECKIDENT ('Students', RESEED, 0);
DBCC CHECKIDENT ('Teachers', RESEED, 0);
DBCC CHECKIDENT ('Sections', RESEED, 0);
DBCC CHECKIDENT ('Courses', RESEED, 0);
DBCC CHECKIDENT ('Badges', RESEED, 0);

PRINT 'Data cleaned successfully.';
GO

-- =============================================
-- INSERT BADGES
-- =============================================
PRINT 'Inserting Badges...';

INSERT INTO Badges (Name, CreatedAt) VALUES
('BSCS', GETDATE()),
('BSSE', GETDATE()),
('BSIT', GETDATE());
GO

-- =============================================
-- INSERT COURSES
-- =============================================
PRINT 'Inserting Courses...';

INSERT INTO Courses (Code, Title, CreditHours, IsLab) VALUES
('CS301', 'Data Structures & Algorithms', 3, 0),
('CS302', 'DSA Lab', 1, 1),
('CS303', 'Operating Systems', 3, 0),
('CS304', 'OS Lab', 1, 1),
('CS305', 'Database Systems', 3, 0),
('CS401', 'Software Engineering', 3, 0),
('CS402', 'Web Development', 3, 0),
('CS403', 'Mobile App Development', 3, 0),
('CS501', 'Artificial Intelligence', 3, 0),
('CS502', 'Machine Learning', 3, 0),
('SE301', 'Enterprise Application Development', 3, 0),
('SE302', 'EAD Lab', 1, 1),
('SE303', 'Software Design Patterns', 3, 0),
('SE401', 'Final Year Project', 3, 0),
('IT301', 'Network Security', 3, 0),
('IT302', 'Cloud Computing', 3, 0),
('MT301', 'Linear Algebra', 3, 0),
('MT302', 'Discrete Mathematics', 3, 0),
('HS301', 'Technical Writing', 2, 0),
('HS302', 'Human Computer Interaction', 2, 0);
GO

-- =============================================
-- INSERT SECTIONS
-- =============================================
PRINT 'Inserting Sections...';

INSERT INTO Sections (Name, BadgeId, Semester, Session) VALUES
('A', 1, 5, 2023),
('B', 1, 5, 2023),
('A', 1, 7, 2022),
('A', 2, 5, 2023),
('B', 2, 5, 2023),
('A', 3, 3, 2024);
GO

-- =============================================
-- INSERT TEACHERS & USERS
-- =============================================
PRINT 'Inserting Teachers...';

-- Teacher 1: Dr. Ahmed Khan
INSERT INTO Users (FullName, Email, PasswordHash, Role, IsActive, CreatedAt) 
VALUES ('Dr. Ahmed Khan', 'ahmed.khan@university.edu', 'AQAAAAIAAYagAAAAEKxK8cJWvH8xK7VN1F9lqGzPQvH3xDLyMY9wN5fKq3+YhRtJ0eP2mLkNxS4vW8==', 'Teacher', 1, GETDATE());
INSERT INTO Teachers (UserId, BadgeNumber, Designation) VALUES (SCOPE_IDENTITY(), 'TCH001', 'Associate Professor');

-- Teacher 2: Ms. Fatima Ali
INSERT INTO Users (FullName, Email, PasswordHash, Role, IsActive, CreatedAt) 
VALUES ('Ms. Fatima Ali', 'fatima.ali@university.edu', 'AQAAAAIAAYagAAAAEKxK8cJWvH8xK7VN1F9lqGzPQvH3xDLyMY9wN5fKq3+YhRtJ0eP2mLkNxS4vW8==', 'Teacher', 1, GETDATE());
INSERT INTO Teachers (UserId, BadgeNumber, Designation) VALUES (SCOPE_IDENTITY(), 'TCH002', 'Assistant Professor');

-- Teacher 3: Dr. Hassan Raza
INSERT INTO Users (FullName, Email, PasswordHash, Role, IsActive, CreatedAt) 
VALUES ('Dr. Hassan Raza', 'hassan.raza@university.edu', 'AQAAAAIAAYagAAAAEKxK8cJWvH8xK7VN1F9lqGzPQvH3xDLyMY9wN5fKq3+YhRtJ0eP2mLkNxS4vW8==', 'Teacher', 1, GETDATE());
INSERT INTO Teachers (UserId, BadgeNumber, Designation) VALUES (SCOPE_IDENTITY(), 'TCH003', 'Professor');

-- Teacher 4: Ms. Ayesha Malik
INSERT INTO Users (FullName, Email, PasswordHash, Role, IsActive, CreatedAt) 
VALUES ('Ms. Ayesha Malik', 'ayesha.malik@university.edu', 'AQAAAAIAAYagAAAAEKxK8cJWvH8xK7VN1F9lqGzPQvH3xDLyMY9wN5fKq3+YhRtJ0eP2mLkNxS4vW8==', 'Teacher', 1, GETDATE());
INSERT INTO Teachers (UserId, BadgeNumber, Designation) VALUES (SCOPE_IDENTITY(), 'TCH004', 'Lecturer');

-- Teacher 5: Dr. Imran Qureshi
INSERT INTO Users (FullName, Email, PasswordHash, Role, IsActive, CreatedAt) 
VALUES ('Dr. Imran Qureshi', 'imran.qureshi@university.edu', 'AQAAAAIAAYagAAAAEKxK8cJWvH8xK7VN1F9lqGzPQvH3xDLyMY9wN5fKq3+YhRtJ0eP2mLkNxS4vW8==', 'Teacher', 1, GETDATE());
INSERT INTO Teachers (UserId, BadgeNumber, Designation) VALUES (SCOPE_IDENTITY(), 'TCH005', 'Assistant Professor');

-- Teacher 6: Ms. Sara Ahmed
INSERT INTO Users (FullName, Email, PasswordHash, Role, IsActive, CreatedAt) 
VALUES ('Ms. Sara Ahmed', 'sara.ahmed@university.edu', 'AQAAAAIAAYagAAAAEKxK8cJWvH8xK7VN1F9lqGzPQvH3xDLyMY9wN5fKq3+YhRtJ0eP2mLkNxS4vW8==', 'Teacher', 1, GETDATE());
INSERT INTO Teachers (UserId, BadgeNumber, Designation) VALUES (SCOPE_IDENTITY(), 'TCH006', 'Lecturer');

-- Teacher 7: Dr. Bilal Shah
INSERT INTO Users (FullName, Email, PasswordHash, Role, IsActive, CreatedAt) 
VALUES ('Dr. Bilal Shah', 'bilal.shah@university.edu', 'AQAAAAIAAYagAAAAEKxK8cJWvH8xK7VN1F9lqGzPQvH3xDLyMY9wN5fKq3+YhRtJ0eP2mLkNxS4vW8==', 'Teacher', 1, GETDATE());
INSERT INTO Teachers (UserId, BadgeNumber, Designation) VALUES (SCOPE_IDENTITY(), 'TCH007', 'Associate Professor');
GO

-- =============================================
-- INSERT STUDENTS FOR SECTION: BSCS-5A (15 students)
-- =============================================
PRINT 'Inserting Students for BSCS-5A...';

DECLARE @SectionId1 INT = 1;
DECLARE @Counter INT = 1;

WHILE @Counter <= 15
BEGIN
    DECLARE @Email NVARCHAR(150) = 'bscs5a.student' + CAST(@Counter AS NVARCHAR) + '@student.edu';
    DECLARE @Name NVARCHAR(150) = 'Student ' + CAST(@Counter AS NVARCHAR) + ' Khan';
    DECLARE @RollNo NVARCHAR(50) = 'BSCS23F' + RIGHT('000' + CAST(@Counter AS NVARCHAR), 3);
    
    INSERT INTO Users (FullName, Email, PasswordHash, Role, IsActive, CreatedAt) 
    VALUES (@Name, @Email, 'AQAAAAIAAYagAAAAEKxK8cJWvH8xK7VN1F9lqGzPQvH3xDLyMY9wN5fKq3+YhRtJ0eP2mLkNxS4vW8==', 'Student', 1, GETDATE());
    
    INSERT INTO Students (UserId, RollNo, SectionId, FatherName) 
    VALUES (SCOPE_IDENTITY(), @RollNo, @SectionId1, 'Father of ' + @Name);
    
    SET @Counter = @Counter + 1;
END
GO

-- =============================================
-- INSERT STUDENTS FOR SECTION: BSCS-5B (20 students)
-- =============================================
PRINT 'Inserting Students for BSCS-5B...';

DECLARE @SectionId2 INT = 2;
DECLARE @Counter2 INT = 1;

WHILE @Counter2 <= 20
BEGIN
    DECLARE @Email2 NVARCHAR(150) = 'bscs5b.student' + CAST(@Counter2 AS NVARCHAR) + '@student.edu';
    DECLARE @Name2 NVARCHAR(150) = 'Student ' + CAST(@Counter2 AS NVARCHAR) + ' Ali';
    DECLARE @RollNo2 NVARCHAR(50) = 'BSCS23F' + RIGHT('000' + CAST(100 + @Counter2 AS NVARCHAR), 3);
    
    INSERT INTO Users (FullName, Email, PasswordHash, Role, IsActive, CreatedAt) 
    VALUES (@Name2, @Email2, 'AQAAAAIAAYagAAAAEKxK8cJWvH8xK7VN1F9lqGzPQvH3xDLyMY9wN5fKq3+YhRtJ0eP2mLkNxS4vW8==', 'Student', 1, GETDATE());
    
    INSERT INTO Students (UserId, RollNo, SectionId, FatherName) 
    VALUES (SCOPE_IDENTITY(), @RollNo2, @SectionId2, 'Father of ' + @Name2);
    
    SET @Counter2 = @Counter2 + 1;
END
GO

-- =============================================
-- INSERT STUDENTS FOR SECTION: BSCS-7A (10 students)
-- =============================================
PRINT 'Inserting Students for BSCS-7A...';

DECLARE @SectionId3 INT = 3;
DECLARE @Counter3 INT = 1;

WHILE @Counter3 <= 10
BEGIN
    DECLARE @Email3 NVARCHAR(150) = 'bscs7a.student' + CAST(@Counter3 AS NVARCHAR) + '@student.edu';
    DECLARE @Name3 NVARCHAR(150) = 'Student ' + CAST(@Counter3 AS NVARCHAR) + ' Hassan';
    DECLARE @RollNo3 NVARCHAR(50) = 'BSCS22F' + RIGHT('000' + CAST(@Counter3 AS NVARCHAR), 3);
    
    INSERT INTO Users (FullName, Email, PasswordHash, Role, IsActive, CreatedAt) 
    VALUES (@Name3, @Email3, 'AQAAAAIAAYagAAAAEKxK8cJWvH8xK7VN1F9lqGzPQvH3xDLyMY9wN5fKq3+YhRtJ0eP2mLkNxS4vW8==', 'Student', 1, GETDATE());
    
    INSERT INTO Students (UserId, RollNo, SectionId, FatherName) 
    VALUES (SCOPE_IDENTITY(), @RollNo3, @SectionId3, 'Father of ' + @Name3);
    
    SET @Counter3 = @Counter3 + 1;
END
GO

-- =============================================
-- ASSIGN TEACHERS TO COURSES (TeacherCourses)
-- =============================================
PRINT 'Assigning Teachers to Courses...';

-- BSCS-5A Section (SectionId=1)
INSERT INTO TeacherCourses (TeacherId, CourseId, SectionId, AssignedAt) VALUES
(1, 1, 1, GETDATE()),  -- Dr. Ahmed Khan -> DSA (Theory)
(1, 2, 1, GETDATE()),  -- Dr. Ahmed Khan -> DSA Lab
(2, 3, 1, GETDATE()),  -- Ms. Fatima Ali -> OS (Theory)
(2, 4, 1, GETDATE()),  -- Ms. Fatima Ali -> OS Lab
(3, 5, 1, GETDATE()),  -- Dr. Hassan Raza -> Database Systems
(4, 17, 1, GETDATE()), -- Ms. Ayesha Malik -> Linear Algebra
(5, 20, 1, GETDATE()); -- Dr. Imran Qureshi -> HCI

-- BSCS-5B Section (SectionId=2)
INSERT INTO TeacherCourses (TeacherId, CourseId, SectionId, AssignedAt) VALUES
(1, 1, 2, GETDATE()),  -- Dr. Ahmed Khan -> DSA (Theory)
(1, 2, 2, GETDATE()),  -- Dr. Ahmed Khan -> DSA Lab
(3, 3, 2, GETDATE()),  -- Dr. Hassan Raza -> OS (Theory)
(3, 4, 2, GETDATE()),  -- Dr. Hassan Raza -> OS Lab
(4, 5, 2, GETDATE()),  -- Ms. Ayesha Malik -> Database Systems
(5, 17, 2, GETDATE()), -- Dr. Imran Qureshi -> Linear Algebra
(6, 19, 2, GETDATE()); -- Ms. Sara Ahmed -> Technical Writing

-- BSCS-7A Section (SectionId=3)
INSERT INTO TeacherCourses (TeacherId, CourseId, SectionId, AssignedAt) VALUES
(3, 9, 3, GETDATE()),  -- Dr. Hassan Raza -> AI
(4, 10, 3, GETDATE()), -- Ms. Ayesha Malik -> Machine Learning
(5, 11, 3, GETDATE()), -- Dr. Imran Qureshi -> EAD (Theory)
(5, 12, 3, GETDATE()), -- Dr. Imran Qureshi -> EAD Lab
(7, 14, 3, GETDATE()); -- Dr. Bilal Shah -> FYP
GO

-- =============================================
-- CREATE TIMETABLE RULES (PROPERLY DISTRIBUTED MON-FRI)
-- Each day has 2-3 lectures with gaps
-- Friday is lighter (1-2 lectures)
-- =============================================
PRINT 'Creating Timetable Rules with BALANCED distribution across ALL 5 days...';

-- ===== BSCS-5A TIMETABLE (SectionId=1, 7 Courses) =====
-- Monday (3 lectures with gaps)
INSERT INTO TimetableRules (TeacherCourseId, StartTime, DurationMinutes, DaysOfWeek, Room, LectureType, StartDate, EndDate) 
VALUES 
(1, '08:00:00', 90, 'Monday', 'Room 101', 'Regular', '2025-10-15', '2026-01-20'),  -- DSA Theory
(6, '11:00:00', 90, 'Monday', 'Room 104', 'Regular', '2025-10-15', '2026-01-20'),  -- Linear Algebra
(5, '02:00:00', 90, 'Monday', 'Room 103', 'Regular', '2025-10-15', '2026-01-20');  -- Database

-- Tuesday (3 lectures with gaps)
INSERT INTO TimetableRules (TeacherCourseId, StartTime, DurationMinutes, DaysOfWeek, Room, LectureType, StartDate, EndDate) 
VALUES 
(2, '08:00:00', 120, 'Tuesday', 'Lab 201', 'Regular', '2025-10-15', '2026-01-20'),  -- DSA Lab
(3, '11:00:00', 90, 'Tuesday', 'Room 102', 'Regular', '2025-10-15', '2026-01-20'),  -- OS Theory
(7, '02:00:00', 60, 'Tuesday', 'Room 105', 'Regular', '2025-10-15', '2026-01-20');  -- HCI

-- Wednesday (3 lectures with gaps)
INSERT INTO TimetableRules (TeacherCourseId, StartTime, DurationMinutes, DaysOfWeek, Room, LectureType, StartDate, EndDate) 
VALUES 
(1, '08:30:00', 90, 'Wednesday', 'Room 101', 'Regular', '2025-10-15', '2026-01-20'),  -- DSA Theory
(5, '11:00:00', 90, 'Wednesday', 'Room 103', 'Regular', '2025-10-15', '2026-01-20'),  -- Database
(6, '01:30:00', 90, 'Wednesday', 'Room 104', 'Regular', '2025-10-15', '2026-01-20');  -- Linear Algebra

-- Thursday (3 lectures with gaps)
INSERT INTO TimetableRules (TeacherCourseId, StartTime, DurationMinutes, DaysOfWeek, Room, LectureType, StartDate, EndDate) 
VALUES 
(4, '08:00:00', 120, 'Thursday', 'Lab 202', 'Regular', '2025-10-15', '2026-01-20'),  -- OS Lab
(3, '11:00:00', 90, 'Thursday', 'Room 102', 'Regular', '2025-10-15', '2026-01-20'),  -- OS Theory
(7, '01:30:00', 60, 'Thursday', 'Room 105', 'Regular', '2025-10-15', '2026-01-20');  -- HCI

-- Friday (2 lectures - LIGHTER)
INSERT INTO TimetableRules (TeacherCourseId, StartTime, DurationMinutes, DaysOfWeek, Room, LectureType, StartDate, EndDate) 
VALUES 
(5, '08:00:00', 90, 'Friday', 'Room 103', 'Regular', '2025-10-15', '2026-01-20'),  -- Database
(6, '10:30:00', 90, 'Friday', 'Room 104', 'Regular', '2025-10-15', '2026-01-20');  -- Linear Algebra

-- ===== BSCS-5B TIMETABLE (SectionId=2, 7 Courses) =====
-- Monday (3 lectures with gaps)
INSERT INTO TimetableRules (TeacherCourseId, StartTime, DurationMinutes, DaysOfWeek, Room, LectureType, StartDate, EndDate) 
VALUES 
(8, '08:30:00', 90, 'Monday', 'Room 201', 'Regular', '2025-10-15', '2026-01-20'),  -- DSA Theory
(12, '11:00:00', 90, 'Monday', 'Room 203', 'Regular', '2025-10-15', '2026-01-20'), -- Database
(13, '02:00:00', 90, 'Monday', 'Room 204', 'Regular', '2025-10-15', '2026-01-20'); -- Linear Algebra

-- Tuesday (3 lectures with gaps)
INSERT INTO TimetableRules (TeacherCourseId, StartTime, DurationMinutes, DaysOfWeek, Room, LectureType, StartDate, EndDate) 
VALUES 
(9, '08:00:00', 120, 'Tuesday', 'Lab 301', 'Regular', '2025-10-15', '2026-01-20'),  -- DSA Lab
(10, '11:00:00', 90, 'Tuesday', 'Room 202', 'Regular', '2025-10-15', '2026-01-20'), -- OS Theory
(14, '01:30:00', 60, 'Tuesday', 'Room 205', 'Regular', '2025-10-15', '2026-01-20'); -- Technical Writing

-- Wednesday (3 lectures with gaps)
INSERT INTO TimetableRules (TeacherCourseId, StartTime, DurationMinutes, DaysOfWeek, Room, LectureType, StartDate, EndDate) 
VALUES 
(8, '08:00:00', 90, 'Wednesday', 'Room 201', 'Regular', '2025-10-15', '2026-01-20'),  -- DSA Theory
(12, '10:30:00', 90, 'Wednesday', 'Room 203', 'Regular', '2025-10-15', '2026-01-20'), -- Database
(13, '01:00:00', 90, 'Wednesday', 'Room 204', 'Regular', '2025-10-15', '2026-01-20'); -- Linear Algebra

-- Thursday (3 lectures with gaps)
INSERT INTO TimetableRules (TeacherCourseId, StartTime, DurationMinutes, DaysOfWeek, Room, LectureType, StartDate, EndDate) 
VALUES 
(11, '08:00:00', 120, 'Thursday', 'Lab 302', 'Regular', '2025-10-15', '2026-01-20'), -- OS Lab
(10, '11:00:00', 90, 'Thursday', 'Room 202', 'Regular', '2025-10-15', '2026-01-20'), -- OS Theory
(14, '01:30:00', 60, 'Thursday', 'Room 205', 'Regular', '2025-10-15', '2026-01-20'); -- Technical Writing

-- Friday (2 lectures - LIGHTER)
INSERT INTO TimetableRules (TeacherCourseId, StartTime, DurationMinutes, DaysOfWeek, Room, LectureType, StartDate, EndDate) 
VALUES 
(12, '09:00:00', 90, 'Friday', 'Room 203', 'Regular', '2025-10-15', '2026-01-20'), -- Database
(13, '11:30:00', 90, 'Friday', 'Room 204', 'Regular', '2025-10-15', '2026-01-20'); -- Linear Algebra

-- ===== BSCS-7A TIMETABLE (SectionId=3, 5 Courses) =====
-- Monday (3 lectures with gaps)
INSERT INTO TimetableRules (TeacherCourseId, StartTime, DurationMinutes, DaysOfWeek, Room, LectureType, StartDate, EndDate) 
VALUES 
(15, '08:00:00', 90, 'Monday', 'Room 301', 'Regular', '2025-10-15', '2026-01-20'), -- AI
(17, '10:30:00', 90, 'Monday', 'Room 302', 'Regular', '2025-10-15', '2026-01-20'), -- EAD Theory
(19, '01:00:00', 90, 'Monday', 'Room 303', 'Regular', '2025-10-15', '2026-01-20'); -- FYP

-- Tuesday (2 lectures with gap)
INSERT INTO TimetableRules (TeacherCourseId, StartTime, DurationMinutes, DaysOfWeek, Room, LectureType, StartDate, EndDate) 
VALUES 
(16, '08:00:00', 90, 'Tuesday', 'Room 304', 'Regular', '2025-10-15', '2026-01-20'), -- Machine Learning
(18, '11:00:00', 120, 'Tuesday', 'Lab 401', 'Regular', '2025-10-15', '2026-01-20'); -- EAD Lab

-- Wednesday (3 lectures with gaps)
INSERT INTO TimetableRules (TeacherCourseId, StartTime, DurationMinutes, DaysOfWeek, Room, LectureType, StartDate, EndDate) 
VALUES 
(15, '09:00:00', 90, 'Wednesday', 'Room 301', 'Regular', '2025-10-15', '2026-01-20'), -- AI
(17, '11:30:00', 90, 'Wednesday', 'Room 302', 'Regular', '2025-10-15', '2026-01-20'), -- EAD Theory
(16, '02:00:00', 90, 'Wednesday', 'Room 304', 'Regular', '2025-10-15', '2026-01-20'); -- Machine Learning

-- Thursday (2 lectures with gap)
INSERT INTO TimetableRules (TeacherCourseId, StartTime, DurationMinutes, DaysOfWeek, Room, LectureType, StartDate, EndDate) 
VALUES 
(19, '08:00:00', 90, 'Thursday', 'Room 303', 'Regular', '2025-10-15', '2026-01-20'), -- FYP
(15, '11:00:00', 90, 'Thursday', 'Room 301', 'Regular', '2025-10-15', '2026-01-20'); -- AI

-- Friday (2 lectures - LIGHTER)
INSERT INTO TimetableRules (TeacherCourseId, StartTime, DurationMinutes, DaysOfWeek, Room, LectureType, StartDate, EndDate) 
VALUES 
(19, '09:00:00', 120, 'Friday', 'Room 303', 'Regular', '2025-10-15', '2026-01-20'), -- FYP (Extended session)
(17, '11:30:00', 90, 'Friday', 'Room 302', 'Regular', '2025-10-15', '2026-01-20'); -- EAD Theory

GO

-- =============================================
-- ADD HOLIDAYS
-- =============================================
PRINT 'Adding Holidays...';

INSERT INTO Holidays (Date, Reason, CreatedAt) VALUES
('2025-12-25', 'Christmas Day', GETDATE()),
('2025-12-31', 'New Year Holiday', GETDATE()),
('2026-01-01', 'New Year Day', GETDATE());
GO

PRINT '============================================='
PRINT 'Base Data Population Completed Successfully!'
PRINT 'Next: Run GenerateLecturesAndAttendance.sql'
PRINT '============================================='
