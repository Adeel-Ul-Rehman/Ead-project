using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace attendence.Data.Migrations
{
    /// <inheritdoc />
    public partial class DatabaseBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Empty - database already exists with correct schema
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Empty - we don't support downgrading from baseline
        }
    }
}