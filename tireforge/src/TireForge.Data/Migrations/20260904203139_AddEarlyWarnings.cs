using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TireForge.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEarlyWarnings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EarlyWarnings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    ReadingId = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    MachineId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Sensor = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CurrentValue = table.Column<double>(type: "float", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RateOfChangePerHour = table.Column<double>(type: "float", nullable: false),
                    BoundApproached = table.Column<double>(type: "float", nullable: false),
                    ProjectedBreachAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    HoursToBreachAt = table.Column<double>(type: "float", nullable: false),
                    Confidence = table.Column<double>(type: "float", nullable: false),
                    NarrativeText = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ReviewerNote = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    TraceId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RaisedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EarlyWarnings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EarlyWarnings_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EarlyWarnings_MachineId",
                table: "EarlyWarnings",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_EarlyWarnings_Status",
                table: "EarlyWarnings",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EarlyWarnings");
        }
    }
}
