using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoCo.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProfessorLogins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProfessorLogins",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfessorId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfessorLogins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfessorLogins_Professors_ProfessorId",
                        column: x => x.ProfessorId,
                        principalTable: "Professors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProfessorLogins_CreatedAt",
                table: "ProfessorLogins",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProfessorLogins_ProfessorId",
                table: "ProfessorLogins",
                column: "ProfessorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ProfessorLogins");
        }
    }
}
