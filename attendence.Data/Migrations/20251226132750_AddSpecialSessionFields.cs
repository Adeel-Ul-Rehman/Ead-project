using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace attendence.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecialSessionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreatedByTeacherId",
                table: "Lectures",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Lectures",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LectureType",
                table: "Lectures",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Regular");

            migrationBuilder.CreateIndex(
                name: "IX_Lectures_CreatedByTeacherId",
                table: "Lectures",
                column: "CreatedByTeacherId");

            migrationBuilder.AddForeignKey(
                name: "FK_Lectures_Teachers_CreatedByTeacherId",
                table: "Lectures",
                column: "CreatedByTeacherId",
                principalTable: "Teachers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Lectures_Teachers_CreatedByTeacherId",
                table: "Lectures");

            migrationBuilder.DropIndex(
                name: "IX_Lectures_CreatedByTeacherId",
                table: "Lectures");

            migrationBuilder.DropColumn(
                name: "CreatedByTeacherId",
                table: "Lectures");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Lectures");

            migrationBuilder.DropColumn(
                name: "LectureType",
                table: "Lectures");
        }
    }
}
