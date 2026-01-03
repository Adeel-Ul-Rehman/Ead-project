-- =============================================
-- GENERATE LECTURES AND ATTENDANCE RECORDS
-- Date Range: Oct 15, 2025 to Jan 20, 2026
-- Target Attendance: ~75%
-- =============================================

USE [AttendenceSystem];
GO

SET NOCOUNT ON;

DECLARE @StartDate DATE = '2025-10-15';
DECLARE @EndDate DATE = '2026-01-20';
DECLARE @CurrentDate DATE = @StartDate;
DECLARE @Today DATE = CAST(GETDATE() AS DATE);

PRINT 'Generating lectures from ' + CAST(@StartDate AS VARCHAR) + ' to ' + CAST(@EndDate AS VARCHAR);
PRINT 'Today is: ' + CAST(@Today AS VARCHAR);

-- =============================================
-- GENERATE LECTURES FOR ALL TIMETABLE RULES
-- =============================================

WHILE @CurrentDate <= @EndDate
BEGIN
    DECLARE @DayName NVARCHAR(20) = DATENAME(WEEKDAY, @CurrentDate);
    
    -- Skip weekends
    IF @DayName NOT IN ('Saturday', 'Sunday')
    BEGIN
        -- Check if it's a holiday
        IF NOT EXISTS (SELECT 1 FROM Holidays WHERE Date = @CurrentDate)
        BEGIN
            -- Get all timetable rules for this day
            DECLARE @RuleId INT, @TeacherCourseId INT, @StartTime TIME, @Duration INT, @Room NVARCHAR(50);
            
            DECLARE rule_cursor CURSOR FOR
            SELECT Id, TeacherCourseId, StartTime, DurationMinutes, Room
            FROM TimetableRules
            WHERE DaysOfWeek LIKE '%' + @DayName + '%'
              AND @CurrentDate BETWEEN StartDate AND EndDate;
            
            OPEN rule_cursor;
            FETCH NEXT FROM rule_cursor INTO @RuleId, @TeacherCourseId, @StartTime, @Duration, @Room;
            
            WHILE @@FETCH_STATUS = 0
            BEGIN
                -- Create lecture datetime
                DECLARE @LectureStart DATETIME = CAST(@CurrentDate AS DATETIME) + CAST(@StartTime AS DATETIME);
                DECLARE @LectureEnd DATETIME = DATEADD(MINUTE, @Duration, @LectureStart);
                
                -- Determine lecture status
                DECLARE @Status NVARCHAR(20);
                IF @CurrentDate < @Today
                    SET @Status = 'Completed';
                ELSE IF @CurrentDate = @Today
                    SET @Status = 'Scheduled';
                ELSE
                    SET @Status = 'Scheduled';
                
                -- Get teacher ID for this course
                DECLARE @TeacherId INT;
                SELECT @TeacherId = TeacherId FROM TeacherCourses WHERE Id = @TeacherCourseId;
                
                -- Insert lecture
                INSERT INTO Lectures (TimetableRuleId, StartDateTime, EndDateTime, Status, LectureType, CreatedByTeacherId, AttendanceDeadline)
                VALUES (@RuleId, @LectureStart, @LectureEnd, @Status, 'Regular', @TeacherId, DATEADD(MINUTE, @Duration + 10, @LectureStart));
                
                DECLARE @LectureId INT = SCOPE_IDENTITY();
                
                -- Generate attendance records only for completed lectures
                IF @Status = 'Completed'
                BEGIN
                    -- Get section ID and students
                    DECLARE @SectionId INT;
                    SELECT @SectionId = SectionId FROM TeacherCourses WHERE Id = @TeacherCourseId;
                    
                    -- Get all students in this section
                    DECLARE @StudentId INT;
                    DECLARE student_cursor CURSOR FOR
                    SELECT Id FROM Students WHERE SectionId = @SectionId;
                    
                    OPEN student_cursor;
                    FETCH NEXT FROM student_cursor INTO @StudentId;
                    
                    WHILE @@FETCH_STATUS = 0
                    BEGIN
                        -- Random attendance with ~75% present rate
                        DECLARE @RandomValue FLOAT = RAND();
                        DECLARE @AttendanceStatus NVARCHAR(20);
                        DECLARE @MarkedTime DATETIME;
                        
                        -- 75% Present, 5% Late, 20% Absent
                        IF @RandomValue <= 0.75
                        BEGIN
                            SET @AttendanceStatus = 'Present';
                            -- Mark attendance within first 5 minutes of lecture
                            SET @MarkedTime = DATEADD(MINUTE, FLOOR(RAND() * 5), @LectureStart);
                        END
                        ELSE IF @RandomValue <= 0.80
                        BEGIN
                            SET @AttendanceStatus = 'Late';
                            -- Mark attendance 5-10 minutes after start
                            SET @MarkedTime = DATEADD(MINUTE, 5 + FLOOR(RAND() * 5), @LectureStart);
                        END
                        ELSE
                        BEGIN
                            SET @AttendanceStatus = 'Absent';
                            -- Mark attendance within marking window
                            SET @MarkedTime = DATEADD(MINUTE, FLOOR(RAND() * 15), @LectureStart);
                        END
                        
                        -- Insert attendance record
                        INSERT INTO AttendanceRecords (LectureId, StudentId, Status, MarkedAt)
                        VALUES (@LectureId, @StudentId, @AttendanceStatus, @MarkedTime);
                        
                        FETCH NEXT FROM student_cursor INTO @StudentId;
                    END
                    
                    CLOSE student_cursor;
                    DEALLOCATE student_cursor;
                END
                
                FETCH NEXT FROM rule_cursor INTO @RuleId, @TeacherCourseId, @StartTime, @Duration, @Room;
            END
            
            CLOSE rule_cursor;
            DEALLOCATE rule_cursor;
        END
    END
    
    SET @CurrentDate = DATEADD(DAY, 1, @CurrentDate);
END

-- =============================================
-- GENERATE STATISTICS
-- =============================================

PRINT '';
PRINT '============================================='
PRINT 'GENERATION COMPLETED SUCCESSFULLY!'
PRINT '============================================='
PRINT '';

-- Count lectures by status
DECLARE @CompletedCount INT, @ScheduledCount INT, @TotalCount INT;
SELECT @CompletedCount = COUNT(*) FROM Lectures WHERE Status = 'Completed';
SELECT @ScheduledCount = COUNT(*) FROM Lectures WHERE Status = 'Scheduled';
SELECT @TotalCount = COUNT(*) FROM Lectures;

PRINT 'Total Lectures Generated: ' + CAST(@TotalCount AS VARCHAR);
PRINT 'Completed Lectures: ' + CAST(@CompletedCount AS VARCHAR);
PRINT 'Scheduled Lectures: ' + CAST(@ScheduledCount AS VARCHAR);

-- Count attendance records
DECLARE @AttendanceCount INT, @PresentCount INT, @AbsentCount INT, @LateCount INT;
SELECT @AttendanceCount = COUNT(*) FROM AttendanceRecords;
SELECT @PresentCount = COUNT(*) FROM AttendanceRecords WHERE Status = 'Present';
SELECT @AbsentCount = COUNT(*) FROM AttendanceRecords WHERE Status = 'Absent';
SELECT @LateCount = COUNT(*) FROM AttendanceRecords WHERE Status = 'Late';

PRINT '';
PRINT 'Total Attendance Records: ' + CAST(@AttendanceCount AS VARCHAR);
PRINT 'Present: ' + CAST(@PresentCount AS VARCHAR) + ' (' + CAST(CAST(@PresentCount * 100.0 / @AttendanceCount AS DECIMAL(5,2)) AS VARCHAR) + '%)';
PRINT 'Late: ' + CAST(@LateCount AS VARCHAR) + ' (' + CAST(CAST(@LateCount * 100.0 / @AttendanceCount AS DECIMAL(5,2)) AS VARCHAR) + '%)';
PRINT 'Absent: ' + CAST(@AbsentCount AS VARCHAR) + ' (' + CAST(CAST(@AbsentCount * 100.0 / @AttendanceCount AS DECIMAL(5,2)) AS VARCHAR) + '%)';

-- Show section-wise statistics
PRINT '';
PRINT 'SECTION-WISE STATISTICS:';
PRINT '============================================='

SELECT 
    s.Name + '-' + b.Name AS Section,
    COUNT(DISTINCT st.Id) AS Students,
    COUNT(DISTINCT l.Id) AS TotalLectures,
    COUNT(DISTINCT CASE WHEN l.Status = 'Completed' THEN l.Id END) AS CompletedLectures,
    COUNT(ar.Id) AS AttendanceRecords,
    COUNT(CASE WHEN ar.Status = 'Present' THEN 1 END) AS PresentCount,
    CAST(COUNT(CASE WHEN ar.Status = 'Present' THEN 1 END) * 100.0 / NULLIF(COUNT(ar.Id), 0) AS DECIMAL(5,2)) AS AttendancePercentage
FROM Sections s
INNER JOIN Badges b ON s.BadgeId = b.Id
INNER JOIN Students st ON st.SectionId = s.Id
LEFT JOIN TeacherCourses tc ON tc.SectionId = s.Id
LEFT JOIN TimetableRules tr ON tr.TeacherCourseId = tc.Id
LEFT JOIN Lectures l ON l.TimetableRuleId = tr.Id
LEFT JOIN AttendanceRecords ar ON ar.LectureId = l.Id AND ar.StudentId = st.Id
GROUP BY s.Name, b.Name, s.Id
ORDER BY s.Id;

PRINT '';
PRINT '============================================='
PRINT 'DATABASE READY FOR USE!'
PRINT '============================================='

SET NOCOUNT OFF;
