using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FanShop.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkDayEmployee_Employees_EmployeeID",
                table: "WorkDayEmployee");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkDayEmployee_WorkDays_WorkDayID",
                table: "WorkDayEmployee");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WorkDayEmployee",
                table: "WorkDayEmployee");

            migrationBuilder.RenameTable(
                name: "WorkDayEmployee",
                newName: "WorkDayEmployees");

            migrationBuilder.RenameIndex(
                name: "IX_WorkDayEmployee_WorkDayID",
                table: "WorkDayEmployees",
                newName: "IX_WorkDayEmployees_WorkDayID");

            migrationBuilder.RenameIndex(
                name: "IX_WorkDayEmployee_EmployeeID",
                table: "WorkDayEmployees",
                newName: "IX_WorkDayEmployees_EmployeeID");

            migrationBuilder.AddColumn<bool>(
                name: "IncludeInPass",
                table: "WorkDayEmployees",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeInSalary",
                table: "WorkDayEmployees",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_WorkDayEmployees",
                table: "WorkDayEmployees",
                column: "WorkDayEmployeeID");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkDayEmployees_Employees_EmployeeID",
                table: "WorkDayEmployees",
                column: "EmployeeID",
                principalTable: "Employees",
                principalColumn: "EmployeeID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkDayEmployees_WorkDays_WorkDayID",
                table: "WorkDayEmployees",
                column: "WorkDayID",
                principalTable: "WorkDays",
                principalColumn: "WorkDayID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkDayEmployees_Employees_EmployeeID",
                table: "WorkDayEmployees");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkDayEmployees_WorkDays_WorkDayID",
                table: "WorkDayEmployees");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WorkDayEmployees",
                table: "WorkDayEmployees");

            migrationBuilder.DropColumn(
                name: "IncludeInPass",
                table: "WorkDayEmployees");

            migrationBuilder.DropColumn(
                name: "IncludeInSalary",
                table: "WorkDayEmployees");

            migrationBuilder.RenameTable(
                name: "WorkDayEmployees",
                newName: "WorkDayEmployee");

            migrationBuilder.RenameIndex(
                name: "IX_WorkDayEmployees_WorkDayID",
                table: "WorkDayEmployee",
                newName: "IX_WorkDayEmployee_WorkDayID");

            migrationBuilder.RenameIndex(
                name: "IX_WorkDayEmployees_EmployeeID",
                table: "WorkDayEmployee",
                newName: "IX_WorkDayEmployee_EmployeeID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WorkDayEmployee",
                table: "WorkDayEmployee",
                column: "WorkDayEmployeeID");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkDayEmployee_Employees_EmployeeID",
                table: "WorkDayEmployee",
                column: "EmployeeID",
                principalTable: "Employees",
                principalColumn: "EmployeeID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkDayEmployee_WorkDays_WorkDayID",
                table: "WorkDayEmployee",
                column: "WorkDayID",
                principalTable: "WorkDays",
                principalColumn: "WorkDayID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
