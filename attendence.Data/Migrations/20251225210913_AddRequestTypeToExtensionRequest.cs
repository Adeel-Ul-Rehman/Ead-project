using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace attendence.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestTypeToExtensionRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequestType",
                table: "AttendanceExtensionRequests",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestType",
                table: "AttendanceExtensionRequests");
        }
    }
}
