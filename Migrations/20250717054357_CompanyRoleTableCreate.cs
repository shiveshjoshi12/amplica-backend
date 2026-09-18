using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BizfreeApp.Migrations
{
    /// <inheritdoc />
    public partial class CompanyRoleTableCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "company_role_id",
                table: "rolespermissions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "company_role_id",
                table: "company_users",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "company_roles",
                columns: table => new
                {
                    company_role_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    role_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<int>(type: "integer", nullable: true),
                    default_role = table.Column<bool>(type: "boolean", nullable: true),
                    default_permission = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_roles", x => x.company_role_id);
                    table.ForeignKey(
                        name: "FK_company_roles_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_company_roles_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "role_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_company_roles_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "FK_company_roles_users_updated_by",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_rolespermissions_company_role_id",
                table: "rolespermissions",
                column: "company_role_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_users_company_role_id",
                table: "company_users",
                column: "company_role_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_roles_created_by",
                table: "company_roles",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_company_roles_role_id",
                table: "company_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_roles_updated_by",
                table: "company_roles",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "uq_company_roles_company_id_role_id",
                table: "company_roles",
                columns: new[] { "company_id", "role_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_company_users_company_roles_company_role_id",
                table: "company_users",
                column: "company_role_id",
                principalTable: "company_roles",
                principalColumn: "company_role_id");

            migrationBuilder.AddForeignKey(
                name: "FK_rolespermissions_company_roles_company_role_id",
                table: "rolespermissions",
                column: "company_role_id",
                principalTable: "company_roles",
                principalColumn: "company_role_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_company_users_company_roles_company_role_id",
                table: "company_users");

            migrationBuilder.DropForeignKey(
                name: "FK_rolespermissions_company_roles_company_role_id",
                table: "rolespermissions");

            migrationBuilder.DropTable(
                name: "company_roles");

            migrationBuilder.DropIndex(
                name: "IX_rolespermissions_company_role_id",
                table: "rolespermissions");

            migrationBuilder.DropIndex(
                name: "IX_company_users_company_role_id",
                table: "company_users");

            migrationBuilder.DropColumn(
                name: "company_role_id",
                table: "rolespermissions");

            migrationBuilder.DropColumn(
                name: "company_role_id",
                table: "company_users");
        }
    }
}
