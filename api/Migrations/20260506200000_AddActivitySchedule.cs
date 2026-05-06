using Microsoft.EntityFrameworkCore.Migrations;
using AutoCo.Api.Data;

#nullable disable

namespace AutoCo.Api.Migrations
{
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(AppDbContext))]
    [Migration("20260506200000_AddActivitySchedule")]
    public partial class AddActivitySchedule : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OpenAt",
                table: "Activities",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CloseAt",
                table: "Activities",
                type: "datetime2",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "OpenAt",  table: "Activities");
            migrationBuilder.DropColumn(name: "CloseAt", table: "Activities");
        }
    }
}
