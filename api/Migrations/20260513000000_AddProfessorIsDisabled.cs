using Microsoft.EntityFrameworkCore.Migrations;
using AutoCo.Api.Data;

#nullable disable

namespace AutoCo.Api.Migrations
{
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(AppDbContext))]
    [Migration("20260513000000_AddProfessorIsDisabled")]
    public partial class AddProfessorIsDisabled : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDisabled",
                table: "Professors",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDisabled",
                table: "Professors");
        }
    }
}
