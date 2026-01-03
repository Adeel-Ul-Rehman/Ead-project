# EXECUTE DATABASE CLEANUP AND REPOPULATION

## Step 1: Open SQL Server Management Studio (SSMS)

1. Press Windows Key and search for "SQL Server Management Studio"
2. Connect to: **(localdb)\MSSQLLocalDB**
3. Select Database: **AttendenceSystem**

## Step 2: Run PopulateDatabaseCorrected.sql

1. In SSMS, go to **File → Open → File**
2. Navigate to: `C:\Users\ajade\source\repos\attendenceProject\PopulateDatabaseCorrected.sql`
3. Click **Execute** (or press F5)
4. Wait for "Base Data Population Completed Successfully!" message

## Step 3: Run GenerateLecturesAndAttendance.sql

1. In SSMS, go to **File → Open → File**
2. Navigate to: `C:\Users\ajade\source\repos\attendenceProject\GenerateLecturesAndAttendance.sql`
3. Click **Execute** (or press F5)
4. Wait for statistics showing lectures generated (~850 total)

## Step 4: Verify Lecture Distribution

Run this query to confirm lectures are distributed across ALL 5 days:

```sql
SELECT 
    DATENAME(WEEKDAY, StartDateTime) AS DayOfWeek,
    COUNT(*) AS LectureCount
FROM Lectures
WHERE Status = 'Completed'
GROUP BY DATENAME(WEEKDAY, StartDateTime)
ORDER BY 
    CASE DATENAME(WEEKDAY, StartDateTime)
        WHEN 'Monday' THEN 1
        WHEN 'Tuesday' THEN 2
        WHEN 'Wednesday' THEN 3
        WHEN 'Thursday' THEN 4
        WHEN 'Friday' THEN 5
    END;
```

**Expected Result:**
- Monday: ~100-120 lectures
- Tuesday: ~80-100 lectures
- Wednesday: ~100-120 lectures
- Thursday: ~80-100 lectures
- Friday: ~50-70 lectures (LIGHTER)

## Step 5: Test the Application

1. Stop the running application (Ctrl+C in terminal)
2. Start it again: `dotnet run`
3. Login and verify:
   - Student sees ~75% attendance
   - Teacher sees students with attendance
   - Timetable shows lectures on ALL days
   - Friday has fewer lectures

## Login Credentials

**Admin:**
- Email: admin@university.edu
- Password: Admin123!

**Teacher (Dr. Ahmed Khan - teaches DSA to both sections):**
- Email: ahmed.khan@university.edu
- Password: Teacher123!

**Student (BSCS-5A):**
- Email: bscs5a.student1@student.edu
- Password: Student123!

## Troubleshooting

**If lectures are still on 3 days only:**
- Make sure you ran BOTH scripts
- Check that TimetableRules table has entries for Thursday and Friday
- Run: `SELECT DISTINCT DaysOfWeek FROM TimetableRules`

**If attendance is 0%:**
- Make sure GenerateLecturesAndAttendance.sql completed
- Check: `SELECT COUNT(*) FROM AttendanceRecords` (should be ~27,000)

**If student/teacher panels don't match:**
- Verify the code fix in Student Dashboard was applied (counts only "Present", not "Late")
- Rebuild application: `dotnet build`
