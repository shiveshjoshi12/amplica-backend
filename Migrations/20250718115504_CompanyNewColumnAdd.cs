using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizfreeApp.Migrations
{
    /// <inheritdoc />
    public partial class CompanyNewColumnAdd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "about_company",
                table: "companies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "company_size",
                table: "companies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "created_by",
                table: "companies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "industry",
                table: "companies",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_approved",
                table: "companies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "updated_by",
                table: "companies",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_companies_created_by",
                table: "companies",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_companies_updated_by",
                table: "companies",
                column: "updated_by");

            migrationBuilder.AddForeignKey(
                name: "FK_companies_users_created_by",
                table: "companies",
                column: "created_by",
                principalTable: "users",
                principalColumn: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_companies_users_updated_by",
                table: "companies",
                column: "updated_by",
                principalTable: "users",
                principalColumn: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_companies_users_created_by",
                table: "companies");

            migrationBuilder.DropForeignKey(
                name: "FK_companies_users_updated_by",
                table: "companies");

            migrationBuilder.DropIndex(
                name: "IX_companies_created_by",
                table: "companies");

            migrationBuilder.DropIndex(
                name: "IX_companies_updated_by",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "about_company",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "company_size",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "industry",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "is_approved",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "companies");
        }
    }
}
