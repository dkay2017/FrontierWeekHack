using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TireForge.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Machines",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    SeedStatus = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    LastMaintenance = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Temperature_Min = table.Column<double>(type: "REAL", nullable: false),
                    Temperature_Max = table.Column<double>(type: "REAL", nullable: false),
                    Temperature_Unit = table.Column<string>(type: "TEXT", nullable: false),
                    Pressure_Min = table.Column<double>(type: "REAL", nullable: false),
                    Pressure_Max = table.Column<double>(type: "REAL", nullable: false),
                    Pressure_Unit = table.Column<string>(type: "TEXT", nullable: false),
                    Vibration_Min = table.Column<double>(type: "REAL", nullable: false),
                    Vibration_Max = table.Column<double>(type: "REAL", nullable: false),
                    Vibration_Unit = table.Column<string>(type: "TEXT", nullable: false),
                    Rpm_Min = table.Column<double>(type: "REAL", nullable: false),
                    Rpm_Max = table.Column<double>(type: "REAL", nullable: false),
                    Rpm_Unit = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Machines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "History",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    MachineId = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    OccurredOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Signature = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Fault = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Resolution = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_History", x => x.Id);
                    table.ForeignKey(
                        name: "FK_History_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Readings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 48, nullable: false),
                    MachineId = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CapturedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Temperature = table.Column<double>(type: "REAL", nullable: false),
                    Pressure = table.Column<double>(type: "REAL", nullable: false),
                    Vibration = table.Column<double>(type: "REAL", nullable: false),
                    Rpm = table.Column<double>(type: "REAL", nullable: false),
                    IsAnomaly = table.Column<bool>(type: "INTEGER", nullable: true),
                    Mode = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Readings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Readings_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Diagnoses",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 48, nullable: false),
                    ReadingId = table.Column<string>(type: "TEXT", maxLength: 48, nullable: false),
                    MachineId = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Fault = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: false),
                    Route = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    GateReason = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    DetectText = table.Column<string>(type: "TEXT", nullable: false),
                    MatchText = table.Column<string>(type: "TEXT", nullable: false),
                    DiagnoseText = table.Column<string>(type: "TEXT", nullable: false),
                    IncidentCites = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    TraceId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Diagnoses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Diagnoses_Readings_ReadingId",
                        column: x => x.ReadingId,
                        principalTable: "Readings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrders",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 48, nullable: false),
                    DiagnosisId = table.Column<string>(type: "TEXT", maxLength: 48, nullable: false),
                    MachineId = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Fault = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    ReadingId = table.Column<string>(type: "TEXT", maxLength: 48, nullable: false),
                    ActionText = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    IssuedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    RejectNote = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ClosedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrders_Diagnoses_DiagnosisId",
                        column: x => x.DiagnosisId,
                        principalTable: "Diagnoses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Diagnoses_ReadingId",
                table: "Diagnoses",
                column: "ReadingId");

            migrationBuilder.CreateIndex(
                name: "IX_Diagnoses_Status",
                table: "Diagnoses",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_History_MachineId_Signature",
                table: "History",
                columns: new[] { "MachineId", "Signature" });

            migrationBuilder.CreateIndex(
                name: "IX_Readings_MachineId_CapturedAt",
                table: "Readings",
                columns: new[] { "MachineId", "CapturedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_DiagnosisId",
                table: "WorkOrders",
                column: "DiagnosisId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_Status",
                table: "WorkOrders",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "History");

            migrationBuilder.DropTable(
                name: "WorkOrders");

            migrationBuilder.DropTable(
                name: "Diagnoses");

            migrationBuilder.DropTable(
                name: "Readings");

            migrationBuilder.DropTable(
                name: "Machines");
        }
    }
}
