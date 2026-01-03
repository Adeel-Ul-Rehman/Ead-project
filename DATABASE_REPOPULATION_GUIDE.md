# DATABASE REPOPULATION GUIDE

## Overview
This guide provides instructions for repopulating the attendance system database with corrected data that ensures consistency across all panels (Student, Teacher, Admin).

## Issues Fixed

### 1. **Attendance Calculation Consistency**
- **Student Dashboard**: Now counts only "Present" status (was counting "Present" OR "Late")
- **Teacher Panel**: Already counting only "Present" (no change needed)
- **Admin Panel**: Already counting only "Present" (no change needed)
- **Result**: All three panels now show identical attendance percentages

### 2. **Teacher Panel Scope**
- Teacher panel correctly shows attendance for ONLY their specific course
- This is per-course attendance, not overall attendance
- Example: EAD teacher sees only EAD attendance for students

### 3. **Lecture Distribution**
- **OLD**: Lectures packed into Mon-Tue-Wed only
- **NEW**: Balanced distribution across all 5 weekdays (Mon-Fri)
- Friday has lighter schedule (fewer lectures)
- Proper gaps between lectures

## Database Scripts

### Script 1: PopulateDatabaseCorrected.sql
**Purpose**: Creates base data structure
**Includes**:
- 3 Badges (BSCS, BSSE, BSIT)
- 20 Courses (Theory & Lab courses)
- 6 Sections
- 7 Teachers with proper credentials
- 45 Students (15 in BSCS-5A, 20 in BSCS-5B, 10 in BSCS-7A)
- Teacher-Course assignments
- Timetable rules distributed Mon-Fri
- Holidays

**Key Features**:
- Monday-Thursday: Full schedule with gaps
- Friday: Lighter schedule (2-3 lectures only)
- Realistic time slots (8 AM - 3 PM)
- Mix of theory (90 min) and lab (120 min) sessions

### Script 2: GenerateLecturesAndAttendance.sql
**Purpose**: Generates lectures and attendance records
**Date Range**: October 15, 2025 to January 20, 2026
**Features**:
- Automatically generates lectures for each timetable rule
- Skips weekends (Saturday, Sunday)
- Skips holidays
- **Lectures before Dec 31, 2025**: Status = "Completed" with attendance records
- **Lectures on/after Dec 31, 2025**: Status = "Scheduled" (no attendance yet)
- **Target Attendance**: ~75%
  - 75% Present
  - 5% Late
  - 20% Absent

## Execution Steps

### Step 1: Backup Current Database (Optional)
```sql
BACKUP DATABASE [AttendenceSystem] 
TO DISK = 'C:\Backup\AttendenceSystem_Backup.bak'
WITH FORMAT, INIT, NAME = 'Pre-Population Backup';
```

### Step 2: Run Base Data Script
1. Open SQL Server Management Studio (SSMS)
2. Open `PopulateDatabaseCorrected.sql`
3. Ensure you're connected to the correct database
4. Execute the script (F5)
5. Verify success message

**Expected Output**:
```
Data cleaned successfully.
Inserting Badges...
Inserting Courses...
Inserting Sections...
Inserting Teachers...
Inserting Students...
Base Data Population Completed Successfully!
```

### Step 3: Run Lectures & Attendance Script
1. Open `GenerateLecturesAndAttendance.sql`
2. Execute the script (F5)
3. Wait for completion (may take 1-2 minutes)

**Expected Output**:
```
Generating lectures from 2025-10-15 to 2026-01-20
Today is: 2025-12-31

GENERATION COMPLETED SUCCESSFULLY!

Total Lectures Generated: ~850
Completed Lectures: ~600
Scheduled Lectures: ~250
Total Attendance Records: ~27000
Present: ~20250 (75%)
Late: ~1350 (5%)
Absent: ~5400 (20%)

SECTION-WISE STATISTICS:
[Statistics table showing attendance per section]
```

### Step 4: Verify Data
Run these queries to verify:

```sql
-- Check lecture distribution by day
SELECT DATENAME(WEEKDAY, StartDateTime) AS DayOfWeek, COUNT(*) AS LectureCount
FROM Lectures
GROUP BY DATENAME(WEEKDAY, StartDateTime)
ORDER BY 
    CASE DATENAME(WEEKDAY, StartDateTime)
        WHEN 'Monday' THEN 1
        WHEN 'Tuesday' THEN 2
        WHEN 'Wednesday' THEN 3
        WHEN 'Thursday' THEN 4
        WHEN 'Friday' THEN 5
    END;

-- Check overall attendance percentage
SELECT 
    COUNT(*) AS TotalRecords,
    COUNT(CASE WHEN Status = 'Present' THEN 1 END) AS Present,
    COUNT(CASE WHEN Status = 'Late' THEN 1 END) AS Late,
    COUNT(CASE WHEN Status = 'Absent' THEN 1 END) AS Absent,
    CAST(COUNT(CASE WHEN Status = 'Present' THEN 1 END) * 100.0 / COUNT(*) AS DECIMAL(5,2)) AS PresentPercentage
FROM AttendanceRecords;

-- Check student attendance in student panel perspective
SELECT TOP 5
    u.FullName,
    s.RollNo,
    COUNT(ar.Id) AS TotalLectures,
    COUNT(CASE WHEN ar.Status = 'Present' THEN 1 END) AS Present,
    CAST(COUNT(CASE WHEN ar.Status = 'Present' THEN 1 END) * 100.0 / COUNT(ar.Id) AS DECIMAL(5,2)) AS AttendancePercentage
FROM Students s
INNER JOIN Users u ON s.UserId = u.Id
LEFT JOIN AttendanceRecords ar ON ar.StudentId = s.Id
GROUP BY u.FullName, s.RollNo, s.Id
ORDER BY AttendancePercentage DESC;
```

## Test Credentials

### Admin Panel
- Email: `admin@university.edu`
- Password: `Admin123!`

### Teacher Panel (Examples)
- Email: `ahmed.khan@university.edu`
- Email: `fatima.ali@university.edu`
- Password: `Teacher123!` (all teachers)

### Student Panel (Examples)
- Email: `bscs5a.student1@student.edu`
- Email: `bscs5b.student1@student.edu`
- Password: `Student123!` (all students)

## Verification Checklist

After database population, verify:

- [ ] Student can login and see dashboard with ~75% attendance
- [ ] Student sees course-wise attendance breakdown
- [ ] Teacher can see their sections
- [ ] Teacher sees student list with attendance percentages for THEIR course
- [ ] Teacher attendance % matches what student sees for that specific course
- [ ] Admin can view all students with correct attendance
- [ ] Timetable shows lectures across all 5 weekdays (not just 3)
- [ ] Friday has fewer lectures than other days
- [ ] Today's lectures appear in "Today" view
- [ ] Future lectures appear but have no attendance yet
- [ ] Past lectures show as "Completed" with attendance records

## Expected Results

### Student Panel (Dashboard)
- Overall attendance: ~75%
- Course-wise attendance displayed
- Can see today's and week's lectures
- Rank displayed based on attendance

### Teacher Panel (Students Page)
- When selecting a section, all students display immediately
- Attendance percentage shown is for THAT teacher's course only
- If teacher teaches EAD and student has 80% in EAD, it shows 80%
- Doesn't show overall attendance across all courses

### Admin Panel (Student Profile)
- Shows overall attendance across all courses
- Course-wise breakdown available
- Statistics and graphs display correctly

## Troubleshooting

### Issue: "No students showing in teacher panel"
- Verify teacher is assigned to that section in TeacherCourses table
- Check that lectures were generated for that teacher's course

### Issue: "Attendance percentages don't match"
- Run verification query to check attendance calculation
- Ensure all three panels are counting only "Present" status
- Clear browser cache and re-login

### Issue: "No lectures on certain days"
- Check TimetableRules table - verify DaysOfWeek contains all 5 days
- Verify date range in lectures generation

### Issue: "All lectures showing as Scheduled"
- Check @Today variable in GenerateLecturesAndAttendance.sql
- Ensure current date is set correctly

## Notes

1. **Date Sensitivity**: The scripts use `GETDATE()` to determine "today". All lectures before today are marked "Completed", on/after today are "Scheduled".

2. **Random Attendance**: The 75% attendance is an average. Individual students will have slight variations (72-78%) for realism.

3. **Teacher-Specific View**: Remember that teachers only see attendance for THEIR course, not overall student attendance.

4. **Future Lectures**: Lectures scheduled for today and future dates will have no attendance records. Teachers must mark them when lecture time arrives.

5. **Weekends**: The system automatically skips Saturday and Sunday. No lectures are generated for weekends.

## Success Indicators

Database is correctly populated when:
1. ✅ All 3 panels show consistent attendance for the same course
2. ✅ Lectures are distributed across Mon-Fri (not just 3 days)
3. ✅ Friday has lighter schedule
4. ✅ ~75% attendance rate across all sections
5. ✅ Past lectures have attendance, future ones don't
6. ✅ Students and teachers see matching percentages for individual courses

---

**Ready to Deploy!** Once both scripts execute successfully and verification passes, the system is ready for production use.
