using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryApp.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddAliasNameOnProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProductAlias",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProductAlias",
                table: "Products");
        }
    }
}
