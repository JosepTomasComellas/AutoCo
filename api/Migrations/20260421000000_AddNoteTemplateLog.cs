using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoCo.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteTemplateLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── ProfessorNotes ────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "ProfessorNotes",
                columns: table => new
                {
                    Id         = table.Column<int>(type: "int", nullable: false)
                                     .Annotation("SqlServer:Identity", "1, 1"),
                    ActivityId = table.Column<int>(type: "int", nullable: false),
                    StudentId  = table.Column<int>(type: "int", nullable: false),
                    Note       = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt  = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfessorNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfessorNotes_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProfessorNotes_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProfessorNotes_ActivityId_StudentId",
                table: "ProfessorNotes",
                columns: new[] { "ActivityId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfessorNotes_StudentId",
                table: "ProfessorNotes",
                column: "StudentId");

            // ── ActivityTemplates ─────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "ActivityTemplates",
                columns: table => new
                {
                    Id           = table.Column<int>(type: "int", nullable: false)
                                       .Annotation("SqlServer:Identity", "1, 1"),
                    ProfessorId  = table.Column<int>(type: "int", nullable: false),
                    Name         = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description  = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CriteriaJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt    = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityTemplates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityTemplates_ProfessorId",
                table: "ActivityTemplates",
                column: "ProfessorId");

            // ── ActivityLogs ──────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "ActivityLogs",
                columns: table => new
                {
                    Id           = table.Column<int>(type: "int", nullable: false)
                                       .Annotation("SqlServer:Identity", "1, 1"),
                    ActivityId   = table.Column<int>(type: "int", nullable: false),
                    ActivityName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ActorName    = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Action       = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Details      = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt    = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_ActivityId",
                table: "ActivityLogs",
                column: "ActivityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ProfessorNotes");
            migrationBuilder.DropTable(name: "ActivityTemplates");
            migrationBuilder.DropTable(name: "ActivityLogs");
        }
    }
}
