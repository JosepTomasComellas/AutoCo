using AutoCo.Api.Data;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace AutoCo.Api.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260505000000_AddStudentEncryptedPassword")]
    public partial class AddStudentEncryptedPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlainPasswordEncrypted",
                table: "Students",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlainPasswordEncrypted",
                table: "Students");
        }
    }
}
