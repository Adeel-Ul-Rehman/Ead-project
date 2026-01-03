# University Attendance Management System

A comprehensive attendance management system built with ASP.NET Core 8.

## Features

- Student, Teacher, and Admin portals
- Real-time attendance tracking
- Course management
- Reports and analytics
- Email notifications
- Special sessions support
- Timetable management

## Deployment

This application is configured for deployment on Koyeb using Docker.

### Environment Variables Required:

- `ASPNETCORE_ENVIRONMENT=Production`
- `ConnectionStrings__DefaultConnection=<your-postgresql-connection-string>`

## Local Development

1. Update `appsettings.json` with your SQL Server connection
2. Run migrations: `dotnet ef database update`
3. Run: `dotnet run`
4. Navigate to: `http://localhost:5100`

## Default Admin Credentials

- Username: admin
- Password: Admin@123

(Set via database seeding)
