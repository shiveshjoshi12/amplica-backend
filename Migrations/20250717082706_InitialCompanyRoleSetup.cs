using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizfreeApp.Migrations
{
    /// <inheritdoc />
    public partial class InitialCompanyRoleSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_company_roles_companies_company_id",
                table: "company_roles");

            migrationBuilder.DropForeignKey(
                name: "FK_company_roles_roles_role_id",
                table: "company_roles");

            migrationBuilder.DropForeignKey(
                name: "FK_company_roles_users_created_by",
                table: "company_roles");

            migrationBuilder.DropForeignKey(
                name: "FK_company_roles_users_updated_by",
                table: "company_roles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_company_roles",
                table: "company_roles");

            migrationBuilder.DropIndex(
                name: "IX_company_roles_role_id",
                table: "company_roles");

            migrationBuilder.DropIndex(
                name: "uq_company_roles_company_id_role_id",
                table: "company_roles");

            migrationBuilder.DropColumn(
                name: "role_id",
                table: "company_roles");

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                table: "company_roles",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "default_role",
                table: "company_roles",
                type: "text",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "default_permission",
                table: "company_roles",
                type: "text",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "company_roles",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "company_roles_pkey",
                table: "company_roles",
                column: "company_role_id");

            migrationBuilder.CreateIndex(
                name: "uq_company_roles_company_id_role_name",
                table: "company_roles",
                columns: new[] { "company_id", "role_name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_company_role_company",
                table: "company_roles",
                column: "company_id",
                principalTable: "companies",
                principalColumn: "company_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_company_role_created_by",
                table: "company_roles",
                column: "created_by",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_company_role_updated_by",
                table: "company_roles",
                column: "updated_by",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_company_role_company",
                table: "company_roles");

            migrationBuilder.DropForeignKey(
                name: "fk_company_role_created_by",
                table: "company_roles");

            migrationBuilder.DropForeignKey(
                name: "fk_company_role_updated_by",
                table: "company_roles");

            migrationBuilder.DropPrimaryKey(
                name: "company_roles_pkey",
                table: "company_roles");

            migrationBuilder.DropIndex(
                name: "uq_company_roles_company_id_role_name",
                table: "company_roles");

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                table: "company_roles",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<bool>(
                name: "default_role",
                table: "company_roles",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "default_permission",
                table: "company_roles",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "company_roles",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<int>(
                name: "role_id",
                table: "company_roles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_company_roles",
                table: "company_roles",
                column: "company_role_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_roles_role_id",
                table: "company_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "uq_company_roles_company_id_role_id",
                table: "company_roles",
                columns: new[] { "company_id", "role_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_company_roles_companies_company_id",
                table: "company_roles",
                column: "company_id",
                principalTable: "companies",
                principalColumn: "company_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_company_roles_roles_role_id",
                table: "company_roles",
                column: "role_id",
                principalTable: "roles",
                principalColumn: "role_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_company_roles_users_created_by",
                table: "company_roles",
                column: "created_by",
                principalTable: "users",
                principalColumn: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_company_roles_users_updated_by",
                table: "company_roles",
                column: "updated_by",
                principalTable: "users",
                principalColumn: "user_id");
        }
    }
}
