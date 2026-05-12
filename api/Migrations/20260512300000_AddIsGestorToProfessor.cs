using Microsoft.EntityFrameworkCore.Migrations;
using AutoCo.Api.Data;

#nullable disable

namespace AutoCo.Api.Migrations
{
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(AppDbContext))]
    [Migration("20260512300000_AddIsGestorToProfessor")]
    public partial class AddIsGestorToProfessor : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsGestor",
                table: "Professors",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsGestor",
                table: "Professors");
        }
    }
}
