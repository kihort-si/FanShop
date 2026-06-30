using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FanShop.Migrations
{
    /// <inheritdoc />
    public partial class AddTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DayTask_TaskCategories_TaskCategoryID",
                table: "DayTask");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DayTask",
                table: "DayTask");

            migrationBuilder.RenameTable(
                name: "DayTask",
                newName: "DayTasks");

            migrationBuilder.RenameIndex(
                name: "IX_DayTask_TaskCategoryID",
                table: "DayTasks",
                newName: "IX_DayTasks_TaskCategoryID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DayTasks",
                table: "DayTasks",
                column: "DayTaskID");

            migrationBuilder.AddForeignKey(
                name: "FK_DayTasks_TaskCategories_TaskCategoryID",
                table: "DayTasks",
                column: "TaskCategoryID",
                principalTable: "TaskCategories",
                principalColumn: "TaskCategoryID",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DayTasks_TaskCategories_TaskCategoryID",
                table: "DayTasks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DayTasks",
                table: "DayTasks");

            migrationBuilder.RenameTable(
                name: "DayTasks",
                newName: "DayTask");

            migrationBuilder.RenameIndex(
                name: "IX_DayTasks_TaskCategoryID",
                table: "DayTask",
                newName: "IX_DayTask_TaskCategoryID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DayTask",
                table: "DayTask",
                column: "DayTaskID");

            migrationBuilder.AddForeignKey(
                name: "FK_DayTask_TaskCategories_TaskCategoryID",
                table: "DayTask",
                column: "TaskCategoryID",
                principalTable: "TaskCategories",
                principalColumn: "TaskCategoryID",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
