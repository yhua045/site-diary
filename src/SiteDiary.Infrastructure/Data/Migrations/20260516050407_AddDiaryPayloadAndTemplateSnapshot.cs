using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SiteDiary.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDiaryPayloadAndTemplateSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Payload",
                table: "Diaries",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateSnapshot",
                table: "Diaries",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Payload",
                table: "Diaries");

            migrationBuilder.DropColumn(
                name: "TemplateSnapshot",
                table: "Diaries");
        }
    }
}
