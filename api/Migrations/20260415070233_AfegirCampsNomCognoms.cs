using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoCo.Api.Migrations
{
    /// <inheritdoc />
    public partial class AfegirCampsNomCognoms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "Professors");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Students",
                newName: "Cognoms");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Professors",
                newName: "Cognoms");

            migrationBuilder.AddColumn<string>(
                name: "CorreuElectronic",
                table: "Students",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nom",
                table: "Students",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "NumLlista",
                table: "Students",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CorreuElectronic",
                table: "Professors",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nom",
                table: "Professors",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CorreuElectronic",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "Nom",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "NumLlista",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "CorreuElectronic",
                table: "Professors");

            migrationBuilder.DropColumn(
                name: "Nom",
                table: "Professors");

            migrationBuilder.RenameColumn(
                name: "Cognoms",
                table: "Students",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "Cognoms",
                table: "Professors",
                newName: "Name");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Professors",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
