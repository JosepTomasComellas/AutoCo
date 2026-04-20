using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoCo.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrderIndex",
                table: "Groups",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Inicialitza OrderIndex = Id per preservar l'ordre actual
            migrationBuilder.Sql("UPDATE Groups SET OrderIndex = Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "OrderIndex", table: "Groups");
        }
    }
}
