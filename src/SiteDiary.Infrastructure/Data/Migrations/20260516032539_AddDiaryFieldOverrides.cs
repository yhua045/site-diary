using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SiteDiary.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDiaryFieldOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FieldOverrides",
                table: "Diaries",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FieldOverrides",
                table: "Diaries");
        }
    }
}
