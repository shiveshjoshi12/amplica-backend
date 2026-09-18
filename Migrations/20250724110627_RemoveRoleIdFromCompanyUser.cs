using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizfreeApp.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRoleIdFromCompanyUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use IF EXISTS to safely drop constraints that may or may not exist
            migrationBuilder.Sql("ALTER TABLE company_users DROP CONSTRAINT IF EXISTS \"FK_company_users_companies_company_id\";");
            migrationBuilder.Sql("ALTER TABLE company_users DROP CONSTRAINT IF EXISTS \"FK_company_users_company_roles_company_role_id\";");
            migrationBuilder.Sql("ALTER TABLE company_users DROP CONSTRAINT IF EXISTS \"FK_company_users_department_department_id\";");
            migrationBuilder.Sql("ALTER TABLE company_users DROP CONSTRAINT IF EXISTS \"FK_company_users_users_created_by\";");
            migrationBuilder.Sql("ALTER TABLE company_users DROP CONSTRAINT IF EXISTS \"FK_company_users_users_updated_by\";");
            migrationBuilder.Sql("ALTER TABLE company_users DROP CONSTRAINT IF EXISTS \"FK_company_users_users_user_id\";");
            migrationBuilder.Sql("ALTER TABLE company_users DROP CONSTRAINT IF EXISTS \"fk_role\";");

            // Also try common variations of constraint names
            migrationBuilder.Sql("ALTER TABLE company_users DROP CONSTRAINT IF EXISTS \"fk_company_users_roles\";");
            migrationBuilder.Sql("ALTER TABLE company_users DROP CONSTRAINT IF EXISTS \"company_users_role_id_fkey\";");

            // Drop index if it exists
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_company_users_company_role_id\";");

            // Drop column if it exists
            migrationBuilder.Sql("ALTER TABLE company_users DROP COLUMN IF EXISTS company_role_id;");

            // CRITICAL: Update existing role_id values to match company_roles
            migrationBuilder.Sql(@"
                UPDATE company_users 
                SET role_id = (
                    SELECT cr.company_role_id 
                    FROM company_roles cr 
                    JOIN roles r ON LOWER(cr.role_name) = LOWER(r.role_name)
                    WHERE r.role_id = company_users.role_id
                    AND cr.company_id = company_users.company_id
                    LIMIT 1
                )
                WHERE EXISTS (
                    SELECT 1 
                    FROM company_roles cr 
                    JOIN roles r ON LOWER(cr.role_name) = LOWER(r.role_name)
                    WHERE r.role_id = company_users.role_id
                    AND cr.company_id = company_users.company_id
                );
            ");

            // Handle cases where no matching company role exists - assign to default employee role
            migrationBuilder.Sql(@"
                UPDATE company_users 
                SET role_id = (
                    SELECT cr.company_role_id 
                    FROM company_roles cr 
                    WHERE LOWER(cr.role_name) = 'employee'
                    AND cr.company_id = company_users.company_id
                    AND cr.is_active = true
                    AND cr.is_deleted = false
                    LIMIT 1
                )
                WHERE role_id NOT IN (
                    SELECT company_role_id FROM company_roles WHERE company_id = company_users.company_id
                );
            ");

            // Add new foreign key constraints with proper names
            migrationBuilder.AddForeignKey(
                name: "fk_company",
                table: "company_users",
                column: "company_id",
                principalTable: "companies",
                principalColumn: "company_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_company_role",
                table: "company_users",
                column: "role_id",
                principalTable: "company_roles",
                principalColumn: "company_role_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_created_by",
                table: "company_users",
                column: "created_by",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_department",
                table: "company_users",
                column: "department_id",
                principalTable: "department",
                principalColumn: "dept_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_updated_by",
                table: "company_users",
                column: "updated_by",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_user",
                table: "company_users",
                column: "user_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the new constraints
            migrationBuilder.Sql("ALTER TABLE company_users DROP CONSTRAINT IF EXISTS \"fk_company\";");
            migrationBuilder.Sql("ALTER TABLE company_users DROP CONSTRAINT IF EXISTS \"fk_company_role\";");
            migrationBuilder.Sql("ALTER TABLE company_users DROP CONSTRAINT IF EXISTS \"fk_created_by\";");
            migrationBuilder.Sql("ALTER TABLE company_users DROP CONSTRAINT IF EXISTS \"fk_department\";");
            migrationBuilder.Sql("ALTER TABLE company_users DROP CONSTRAINT IF EXISTS \"fk_updated_by\";");
            migrationBuilder.Sql("ALTER TABLE company_users DROP CONSTRAINT IF EXISTS \"fk_user\";");

            // Revert role_id values back to global role IDs
            migrationBuilder.Sql(@"
                UPDATE company_users 
                SET role_id = (
                    SELECT r.role_id 
                    FROM roles r 
                    JOIN company_roles cr ON LOWER(r.role_name) = LOWER(cr.role_name)
                    WHERE cr.company_role_id = company_users.role_id
                    LIMIT 1
                )
                WHERE EXISTS (
                    SELECT 1 
                    FROM company_roles cr 
                    JOIN roles r ON LOWER(cr.role_name) = LOWER(r.role_name)
                    WHERE cr.company_role_id = company_users.role_id
                );
            ");

            // Add back the company_role_id column
            migrationBuilder.AddColumn<int>(
                name: "company_role_id",
                table: "company_users",
                type: "integer",
                nullable: true);

            // Create index
            migrationBuilder.CreateIndex(
                name: "IX_company_users_company_role_id",
                table: "company_users",
                column: "company_role_id");

            // Restore old foreign key constraints
            migrationBuilder.AddForeignKey(
                name: "FK_company_users_companies_company_id",
                table: "company_users",
                column: "company_id",
                principalTable: "companies",
                principalColumn: "company_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_role",
                table: "company_users",
                column: "role_id",
                principalTable: "roles",
                principalColumn: "role_id");
        }
    }
}
