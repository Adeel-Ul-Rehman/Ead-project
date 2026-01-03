using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace attendence.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBadgeNumberToTeacher : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BadgeNumber",
                table: "Teachers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BadgeNumber",
                table: "Teachers");
        }
    }
}
