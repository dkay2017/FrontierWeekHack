using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TireForge.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentCalls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentCalls",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    AgentName = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TraceId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ReadingId = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: true),
                    PromptTokens = table.Column<int>(type: "int", nullable: false),
                    CompletionTokens = table.Column<int>(type: "int", nullable: false),
                    ToolCalls = table.Column<int>(type: "int", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentCalls", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentCalls_AgentName",
                table: "AgentCalls",
                column: "AgentName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentCalls");
        }
    }
}
