using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FanShop.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaskCategories",
                columns: table => new
                {
                    TaskCategoryID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Color = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskCategories", x => x.TaskCategoryID);
                });

            migrationBuilder.CreateTable(
                name: "DayTask",
                columns: table => new
                {
                    DayTaskID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Comment = table.Column<string>(type: "TEXT", nullable: true),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    TaskCategoryID = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DayTask", x => x.DayTaskID);
                    table.ForeignKey(
                        name: "FK_DayTask_TaskCategories_TaskCategoryID",
                        column: x => x.TaskCategoryID,
                        principalTable: "TaskCategories",
                        principalColumn: "TaskCategoryID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DayTask_TaskCategoryID",
                table: "DayTask",
                column: "TaskCategoryID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DayTask");

            migrationBuilder.DropTable(
                name: "TaskCategories");
        }
    }
}
