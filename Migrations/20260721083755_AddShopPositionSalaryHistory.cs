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

            migrationBuilder.AddColumn<decimal>(
                name: "SalaryAtMoment",
                table: "WorkDayEmployee",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

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
                    PositionName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
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
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: true)
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
                });
            
            migrationBuilder.InsertData(
                table: "Shops",
                columns: new[] { "ShopID", "ShopName", "OpenDate" },
                values: new object[]
                {
                    1,
                    "Фаншоп",
                    new DateTime(2020, 1, 1)
                });

            migrationBuilder.InsertData(
                table: "Positions",
                columns: new[] { "PositionID", "ShopID", "PositionName" },
                values: new object[]
                {
                    1,
                    1,
                    "Помощник"
                });

            migrationBuilder.InsertData(
                table: "SalaryHistories",
                columns: new[]
                {
                    "SalaryHistoryID",
                    "PositionID",
                    "Salary",
                    "StartDate",
                    "EndDate"
                },
                values: new object[]
                {
                    1,
                    1,
                    2500m,
                    new DateTime(2000, 1, 1),
                    null
                });
            
            migrationBuilder.Sql("""
                                 UPDATE WorkDayEmployee
                                 SET PositionID = 1
                                 """);

            migrationBuilder.Sql("""
                                 UPDATE WorkDayEmployee
                                 SET SalaryAtMoment = 2500
                                 """);

            migrationBuilder.CreateIndex(
                name: "IX_WorkDayEmployee_PositionID",
                table: "WorkDayEmployee",
                column: "PositionID");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_ShopID",
                table: "Positions",
                column: "ShopID");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryHistories_PositionID",
                table: "SalaryHistories",
                column: "PositionID");

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

            migrationBuilder.DropColumn(
                name: "SalaryAtMoment",
                table: "WorkDayEmployee");
        }
    }
}
