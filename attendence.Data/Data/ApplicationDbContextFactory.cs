using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace attendence.Data.Data;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        // Hard-coded connection string for design-time (migrations)
        optionsBuilder.UseSqlServer("Server=ADEEL\\SQLEXPRESS;Database=UniversityAttendanceDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;");

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}