using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TireForge.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDiagnosisDraftActionText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DraftActionText",
                table: "Diagnoses",
                type: "TEXT",
                maxLength: 1024,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DraftActionText",
                table: "Diagnoses");
        }
    }
}
