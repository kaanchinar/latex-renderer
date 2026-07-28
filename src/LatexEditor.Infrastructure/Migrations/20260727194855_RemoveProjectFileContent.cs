using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LatexEditor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProjectFileContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Content",
                table: "ProjectFiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Content",
                table: "ProjectFiles",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
