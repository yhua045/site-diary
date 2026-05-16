using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SiteDiary.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDiaryTemplateRoleFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RoleId",
                table: "DiaryTemplates",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiaryTemplates_RoleId",
                table: "DiaryTemplates",
                column: "RoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_DiaryTemplates_Roles_RoleId",
                table: "DiaryTemplates",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiaryTemplates_Roles_RoleId",
                table: "DiaryTemplates");

            migrationBuilder.DropIndex(
                name: "IX_DiaryTemplates_RoleId",
                table: "DiaryTemplates");

            migrationBuilder.DropColumn(
                name: "RoleId",
                table: "DiaryTemplates");
        }
    }
}
