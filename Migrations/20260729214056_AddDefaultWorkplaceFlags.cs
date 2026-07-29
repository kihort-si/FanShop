using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FanShop.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultWorkplaceFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "Shops",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "Positions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "Shops");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "Positions");
        }
    }
}
