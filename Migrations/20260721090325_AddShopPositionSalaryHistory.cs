using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FanShop.Migrations
{
    /// <inheritdoc />
    public partial class AddShopPositionSalaryHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PositionID",
                table: "WorkDayEmployee",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Shops",
                columns: table => new
                {
                    ShopID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ShopName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    OpenDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CloseDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shops", x => x.ShopID);
                });

            migrationBuilder.CreateTable(
                name: "Positions",
                columns: table => new
                {
                    PositionID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ShopID = table.Column<int>(type: "INTEGER", nullable: false),
                    PositionName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ShopID1 = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Positions", x => x.PositionID);
                    table.ForeignKey(
                        name: "FK_Positions_Shops_ShopID",
                        column: x => x.ShopID,
                        principalTable: "Shops",
                        principalColumn: "ShopID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Positions_Shops_ShopID1",
                        column: x => x.ShopID1,
                        principalTable: "Shops",
                        principalColumn: "ShopID");
                });

            migrationBuilder.CreateTable(
                name: "SalaryHistories",
                columns: table => new
                {
                    SalaryHistoryID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PositionID = table.Column<int>(type: "INTEGER", nullable: false),
                    Salary = table.Column<decimal>(type: "TEXT", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PositionID1 = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryHistories", x => x.SalaryHistoryID);
                    table.ForeignKey(
                        name: "FK_SalaryHistories_Positions_PositionID",
                        column: x => x.PositionID,
                        principalTable: "Positions",
                        principalColumn: "PositionID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SalaryHistories_Positions_PositionID1",
                        column: x => x.PositionID1,
                        principalTable: "Positions",
                        principalColumn: "PositionID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkDayEmployee_PositionID",
                table: "WorkDayEmployee",
                column: "PositionID");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_ShopID",
                table: "Positions",
                column: "ShopID");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_ShopID1",
                table: "Positions",
                column: "ShopID1");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryHistories_PositionID",
                table: "SalaryHistories",
                column: "PositionID");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryHistories_PositionID1",
                table: "SalaryHistories",
                column: "PositionID1");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkDayEmployee_Positions_PositionID",
                table: "WorkDayEmployee",
                column: "PositionID",
                principalTable: "Positions",
                principalColumn: "PositionID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkDayEmployee_Positions_PositionID",
                table: "WorkDayEmployee");

            migrationBuilder.DropTable(
                name: "SalaryHistories");

            migrationBuilder.DropTable(
                name: "Positions");

            migrationBuilder.DropTable(
                name: "Shops");

            migrationBuilder.DropIndex(
                name: "IX_WorkDayEmployee_PositionID",
                table: "WorkDayEmployee");

            migrationBuilder.DropColumn(
                name: "PositionID",
                table: "WorkDayEmployee");
        }
    }
}
