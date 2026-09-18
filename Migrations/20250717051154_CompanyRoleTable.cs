using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizfreeApp.Migrations
{
    /// <inheritdoc />
    public partial class CompanyRoleTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_roles_companies_company_id",
                table: "roles");

            migrationBuilder.AlterColumn<int>(
                name: "company_id",
                table: "roles",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_roles_companies_company_id",
                table: "roles",
                column: "company_id",
                principalTable: "companies",
                principalColumn: "company_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_roles_companies_company_id",
                table: "roles");

            migrationBuilder.AlterColumn<int>(
                name: "company_id",
                table: "roles",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_roles_companies_company_id",
                table: "roles",
                column: "company_id",
                principalTable: "companies",
                principalColumn: "company_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
