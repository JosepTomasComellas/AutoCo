using Microsoft.EntityFrameworkCore.Migrations;
using AutoCo.Api.Data;

#nullable disable

namespace AutoCo.Api.Migrations
{
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(AppDbContext))]
    [Migration("20260512100000_AddDefaultCriteria")]
    public partial class AddDefaultCriteria : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DefaultCriteria",
                columns: t => new
                {
                    Id         = t.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    Key        = t.Column<string>(maxLength: 50, nullable: false),
                    Label      = t.Column<string>(maxLength: 200, nullable: false),
                    Weight     = t.Column<int>(nullable: false, defaultValue: 1),
                    OrderIndex = t.Column<int>(nullable: false)
                },
                constraints: t => t.PrimaryKey("PK_DefaultCriteria", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_DefaultCriteria_Key",
                table: "DefaultCriteria",
                column: "Key",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "DefaultCriteria");
        }
    }
}
