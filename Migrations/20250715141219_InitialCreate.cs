using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BizfreeApp.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,");

            migrationBuilder.CreateTable(
                name: "modules",
                columns: table => new
                {
                    module_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    module_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("modules_pkey", x => x.module_id);
                });

            migrationBuilder.CreateTable(
                name: "taskpriorities",
                columns: table => new
                {
                    priority_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    priority_color = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("taskpriorities_pkey", x => x.priority_id);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    permission_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    permission_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    module_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("permissions_pkey", x => x.permission_id);
                    table.ForeignKey(
                        name: "fk_module",
                        column: x => x.module_id,
                        principalTable: "modules",
                        principalColumn: "module_id");
                });

            migrationBuilder.CreateTable(
                name: "client",
                columns: table => new
                {
                    client_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    is_multiple = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    client_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    client_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    client_address = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    createdat = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    client_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("client_pkey", x => x.client_id);
                });

            migrationBuilder.CreateTable(
                name: "companies",
                columns: table => new
                {
                    company_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    company_address = table.Column<string>(type: "text", nullable: true),
                    company_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    company_phone = table.Column<long>(type: "bigint", nullable: true),
                    company_url = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    company_logo_url = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    admin_user_id = table.Column<int>(type: "integer", nullable: true),
                    package_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("companies_pkey", x => x.company_id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    role_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    check_admin = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("roles_pkey", x => x.role_id);
                    table.UniqueConstraint("AK_roles_check_admin", x => x.check_admin);
                    table.ForeignKey(
                        name: "FK_roles_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rolespermissions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    permission_id = table.Column<int>(type: "integer", nullable: false),
                    company_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("rolespermissions_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_company",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "company_id");
                    table.ForeignKey(
                        name: "fk_permission",
                        column: x => x.permission_id,
                        principalTable: "permissions",
                        principalColumn: "permission_id");
                    table.ForeignKey(
                        name: "fk_role",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "role_id");
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    updated_by = table.Column<int>(type: "integer", nullable: true),
                    role_id = table.Column<int>(type: "integer", nullable: true),
                    refresh_token_expiry_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    refresh_token = table.Column<string>(type: "text", nullable: true),
                    company_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("users_pkey", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_role",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "role_id");
                    table.ForeignKey(
                        name: "fk_user_company",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "company_modules",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    module_id = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    enabled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_by = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("company_modules_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_company",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "company_id");
                    table.ForeignKey(
                        name: "fk_module",
                        column: x => x.module_id,
                        principalTable: "modules",
                        principalColumn: "module_id");
                    table.ForeignKey(
                        name: "fk_updated_by",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "department",
                columns: table => new
                {
                    dept_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    department_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    created_by = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_by = table.Column<int>(type: "integer", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("department_pkey", x => x.dept_id);
                    table.ForeignKey(
                        name: "fk_company",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "company_id");
                    table.ForeignKey(
                        name: "fk_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "fk_updated_by",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "packages",
                columns: table => new
                {
                    package_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    package_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    price_monthly = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    price_yearly = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    trial_days = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    created_by = table.Column<int>(type: "integer", nullable: true),
                    updated_by = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("packages_pkey", x => x.package_id);
                    table.ForeignKey(
                        name: "fk_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "fk_updated_by",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "taskstatus",
                columns: table => new
                {
                    status_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status_color = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_by = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_by = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("taskstatus_pkey", x => x.status_id);
                    table.ForeignKey(
                        name: "fk_user_taskstatus_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_user_taskstatus_updated_by",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "company_users",
                columns: table => new
                {
                    employee_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    department_id = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    employment_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    address_line1 = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    state = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    gender = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    blood_group = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    phone_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                    marital_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    joining_date = table.Column<DateOnly>(type: "date", nullable: true),
                    employee_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    profile_photo_url = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    emergency_contact_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    emergency_contact_relation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    emergency_contact_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    created_by = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_by = table.Column<int>(type: "integer", nullable: true),
                    check_admin = table.Column<int>(type: "integer", nullable: true),
                    first_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    last_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    anniversary_date = table.Column<DateOnly>(type: "date", nullable: true),
                    report_to_user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("company_users_pkey", x => x.employee_id);
                    table.ForeignKey(
                        name: "FK_company_users_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_company_users_department_department_id",
                        column: x => x.department_id,
                        principalTable: "department",
                        principalColumn: "dept_id");
                    table.ForeignKey(
                        name: "FK_company_users_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "FK_company_users_users_updated_by",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "FK_company_users_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_check_admin",
                        column: x => x.check_admin,
                        principalTable: "roles",
                        principalColumn: "check_admin");
                    table.ForeignKey(
                        name: "fk_report_to_user",
                        column: x => x.report_to_user_id,
                        principalTable: "company_users",
                        principalColumn: "employee_id");
                    table.ForeignKey(
                        name: "fk_role",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "role_id");
                });

            migrationBuilder.CreateTable(
                name: "packagemodule",
                columns: table => new
                {
                    package_id = table.Column<int>(type: "integer", nullable: false),
                    module_id = table.Column<int>(type: "integer", nullable: false),
                    package_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("packagemodule_pkey", x => new { x.package_id, x.module_id });
                    table.ForeignKey(
                        name: "fk_module",
                        column: x => x.module_id,
                        principalTable: "modules",
                        principalColumn: "module_id");
                    table.ForeignKey(
                        name: "fk_package",
                        column: x => x.package_id,
                        principalTable: "packages",
                        principalColumn: "package_id");
                });

            migrationBuilder.CreateTable(
                name: "company_task_status",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    default_task_status_id = table.Column<int>(type: "integer", nullable: true),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    status_color = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_custom = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_editable_name = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_deletable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_by = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_by = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("company_task_status_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_company_task_status_company",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_company_task_status_default_status",
                        column: x => x.default_task_status_id,
                        principalTable: "taskstatus",
                        principalColumn: "status_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_user_company_task_status_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_user_company_task_status_updated_by",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    project_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    created_by = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_by = table.Column<int>(type: "integer", nullable: true),
                    status_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("projects_pkey", x => x.project_id);
                    table.ForeignKey(
                        name: "fk_company",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "company_id");
                    table.ForeignKey(
                        name: "fk_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "fk_project_company_task_status_id",
                        column: x => x.status_id,
                        principalTable: "company_task_status",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_updated_by",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "project_documents",
                columns: table => new
                {
                    document_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    document_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    document_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    file_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    uploaded_by = table.Column<int>(type: "integer", nullable: true),
                    version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_documents", x => x.document_id);
                    table.ForeignKey(
                        name: "FK_project_documents_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_documents_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "project_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_documents_users_uploaded_by",
                        column: x => x.uploaded_by,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "project_members",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    added_by = table.Column<int>(type: "integer", nullable: true),
                    joined_at = table.Column<DateOnly>(type: "date", nullable: true, defaultValueSql: "CURRENT_DATE"),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("project_members_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_added_by",
                        column: x => x.added_by,
                        principalTable: "users",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "fk_project",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "project_id");
                    table.ForeignKey(
                        name: "fk_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "task_lists",
                columns: table => new
                {
                    task_list_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    list_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    list_order = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<int>(type: "integer", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_lists", x => x.task_list_id);
                    table.ForeignKey(
                        name: "FK_task_lists_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_task_lists_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "project_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_task_lists_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "FK_task_lists_users_updated_by",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "task_todos",
                columns: table => new
                {
                    task_todo_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<int>(type: "integer", nullable: true),
                    assigned_to = table.Column<int>(type: "integer", nullable: true),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    priority_id = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: true),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    daily_log = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    company_id = table.Column<int>(type: "integer", nullable: true),
                    task_list_id = table.Column<int>(type: "integer", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    estimated_hours = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    actual_hours = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    task_order = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<int>(type: "integer", nullable: true),
                    parent_task_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_todos", x => x.task_todo_id);
                    table.ForeignKey(
                        name: "FK_task_todos_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "company_id");
                    table.ForeignKey(
                        name: "FK_task_todos_company_task_status_status",
                        column: x => x.status,
                        principalTable: "company_task_status",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_task_todos_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "project_id");
                    table.ForeignKey(
                        name: "FK_task_todos_task_lists_task_list_id",
                        column: x => x.task_list_id,
                        principalTable: "task_lists",
                        principalColumn: "task_list_id");
                    table.ForeignKey(
                        name: "FK_task_todos_task_todos_parent_task_id",
                        column: x => x.parent_task_id,
                        principalTable: "task_todos",
                        principalColumn: "task_todo_id");
                    table.ForeignKey(
                        name: "FK_task_todos_taskpriorities_priority_id",
                        column: x => x.priority_id,
                        principalTable: "taskpriorities",
                        principalColumn: "priority_id");
                    table.ForeignKey(
                        name: "FK_task_todos_users_assigned_to",
                        column: x => x.assigned_to,
                        principalTable: "users",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "FK_task_todos_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "FK_task_todos_users_updated_by",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "tasks",
                columns: table => new
                {
                    task_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<int>(type: "integer", nullable: true),
                    assigned_to = table.Column<int>(type: "integer", nullable: true),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    priority_id = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: true),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    daily_log = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    company_id = table.Column<int>(type: "integer", nullable: true),
                    task_list_id = table.Column<int>(type: "integer", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    estimated_hours = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    actual_hours = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    task_order = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<int>(type: "integer", nullable: true),
                    parent_task_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tasks_pkey", x => x.task_id);
                    table.ForeignKey(
                        name: "FK_tasks_task_lists_task_list_id",
                        column: x => x.task_list_id,
                        principalTable: "task_lists",
                        principalColumn: "task_list_id");
                    table.ForeignKey(
                        name: "FK_tasks_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "FK_tasks_users_updated_by",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "fk_task_company_task_status",
                        column: x => x.status,
                        principalTable: "company_task_status",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_task_parent_task_id",
                        column: x => x.parent_task_id,
                        principalTable: "tasks",
                        principalColumn: "task_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "tasks_assigned_to_fkey",
                        column: x => x.assigned_to,
                        principalTable: "users",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "tasks_company_id_fkey",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "tasks_priority_id_fkey",
                        column: x => x.priority_id,
                        principalTable: "taskpriorities",
                        principalColumn: "priority_id");
                    table.ForeignKey(
                        name: "tasks_project_id_fkey",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "project_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_documents",
                columns: table => new
                {
                    document_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    task_id = table.Column<int>(type: "integer", nullable: false),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    document_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    document_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    file_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    uploaded_by = table.Column<int>(type: "integer", nullable: true),
                    version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_documents", x => x.document_id);
                    table.ForeignKey(
                        name: "FK_task_documents_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_task_documents_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "task_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_task_documents_users_uploaded_by",
                        column: x => x.uploaded_by,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "task_timelogs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    task_id = table.Column<int>(type: "integer", nullable: true),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    logged_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    duration = table.Column<string>(type: "varchar(5)", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("task_timelogs_pkey", x => x.id);
                    table.ForeignKey(
                        name: "task_timelogs_task_id_fkey",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "task_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "task_timelogs_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "taskattachment",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    file_url = table.Column<string>(type: "text", nullable: true),
                    task_id = table.Column<int>(type: "integer", nullable: true),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    uploaded_by = table.Column<int>(type: "integer", nullable: true),
                    uploaded_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("taskattachment_pkey", x => x.id);
                    table.ForeignKey(
                        name: "taskattachment_task_id_fkey",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "task_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "taskattachment_uploaded_by_fkey",
                        column: x => x.uploaded_by,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "taskcomment",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    task_id = table.Column<int>(type: "integer", nullable: true),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    parent_comment_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("taskcomment_pkey", x => x.id);
                    table.ForeignKey(
                        name: "taskcomment_parent_comment_id_fkey",
                        column: x => x.parent_comment_id,
                        principalTable: "taskcomment",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "taskcomment_task_id_fkey",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "task_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "taskcomment_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_client_company_id",
                table: "client",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_client_user_id",
                table: "client",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "companies_company_email_key",
                table: "companies",
                column: "company_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_companies_package_id",
                table: "companies",
                column: "package_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_modules_company_id",
                table: "company_modules",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_modules_module_id",
                table: "company_modules",
                column: "module_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_modules_updated_by",
                table: "company_modules",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "IX_company_task_status_company_id",
                table: "company_task_status",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_task_status_created_by",
                table: "company_task_status",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_company_task_status_default_task_status_id",
                table: "company_task_status",
                column: "default_task_status_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_task_status_updated_by",
                table: "company_task_status",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "company_users_employee_code_key",
                table: "company_users",
                column: "employee_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_company_users_check_admin",
                table: "company_users",
                column: "check_admin");

            migrationBuilder.CreateIndex(
                name: "IX_company_users_company_id",
                table: "company_users",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_users_created_by",
                table: "company_users",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_company_users_department_id",
                table: "company_users",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_users_report_to_user_id",
                table: "company_users",
                column: "report_to_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_users_role_id",
                table: "company_users",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_users_updated_by",
                table: "company_users",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "IX_company_users_user_id",
                table: "company_users",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_department_company_id",
                table: "department",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_department_created_by",
                table: "department",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_department_updated_by",
                table: "department",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "IX_packagemodule_module_id",
                table: "packagemodule",
                column: "module_id");

            migrationBuilder.CreateIndex(
                name: "IX_packages_created_by",
                table: "packages",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_packages_updated_by",
                table: "packages",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "IX_permissions_module_id",
                table: "permissions",
                column: "module_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_documents_company_id",
                table: "project_documents",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_documents_project_id",
                table: "project_documents",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_documents_uploaded_by",
                table: "project_documents",
                column: "uploaded_by");

            migrationBuilder.CreateIndex(
                name: "IX_project_members_added_by",
                table: "project_members",
                column: "added_by");

            migrationBuilder.CreateIndex(
                name: "IX_project_members_project_id",
                table: "project_members",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_members_user_id",
                table: "project_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_projects_company_id",
                table: "projects",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_projects_created_by",
                table: "projects",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_projects_status_id",
                table: "projects",
                column: "status_id");

            migrationBuilder.CreateIndex(
                name: "IX_projects_updated_by",
                table: "projects",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "IX_roles_company_id",
                table: "roles",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "uq_roles_check_admin",
                table: "roles",
                column: "check_admin",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rolespermissions_company_id",
                table: "rolespermissions",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_rolespermissions_permission_id",
                table: "rolespermissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "IX_rolespermissions_role_id",
                table: "rolespermissions",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_documents_company_id",
                table: "task_documents",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_documents_task_id",
                table: "task_documents",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_documents_uploaded_by",
                table: "task_documents",
                column: "uploaded_by");

            migrationBuilder.CreateIndex(
                name: "IX_task_lists_company_id",
                table: "task_lists",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_lists_created_by",
                table: "task_lists",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_task_lists_project_id",
                table: "task_lists",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_lists_updated_by",
                table: "task_lists",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "IX_task_timelogs_task_id",
                table: "task_timelogs",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_timelogs_user_id",
                table: "task_timelogs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_todos_assigned_to",
                table: "task_todos",
                column: "assigned_to");

            migrationBuilder.CreateIndex(
                name: "IX_task_todos_company_id",
                table: "task_todos",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_todos_created_by",
                table: "task_todos",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_task_todos_parent_task_id",
                table: "task_todos",
                column: "parent_task_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_todos_priority_id",
                table: "task_todos",
                column: "priority_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_todos_project_id",
                table: "task_todos",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_todos_status",
                table: "task_todos",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_task_todos_task_list_id",
                table: "task_todos",
                column: "task_list_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_todos_updated_by",
                table: "task_todos",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "IX_taskattachment_task_id",
                table: "taskattachment",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "IX_taskattachment_uploaded_by",
                table: "taskattachment",
                column: "uploaded_by");

            migrationBuilder.CreateIndex(
                name: "IX_taskcomment_parent_comment_id",
                table: "taskcomment",
                column: "parent_comment_id");

            migrationBuilder.CreateIndex(
                name: "IX_taskcomment_task_id",
                table: "taskcomment",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "IX_taskcomment_user_id",
                table: "taskcomment",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_assigned_to",
                table: "tasks",
                column: "assigned_to");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_company_id",
                table: "tasks",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_created_by",
                table: "tasks",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_parent_task_id",
                table: "tasks",
                column: "parent_task_id");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_priority_id",
                table: "tasks",
                column: "priority_id");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_project_id",
                table: "tasks",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_status",
                table: "tasks",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_task_list_id",
                table: "tasks",
                column: "task_list_id");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_updated_by",
                table: "tasks",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "IX_taskstatus_created_by",
                table: "taskstatus",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_taskstatus_updated_by",
                table: "taskstatus",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "IX_users_company_id",
                table: "users",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_role_id",
                table: "users",
                column: "role_id");

            migrationBuilder.AddForeignKey(
                name: "fk_company",
                table: "client",
                column: "company_id",
                principalTable: "companies",
                principalColumn: "company_id");

            migrationBuilder.AddForeignKey(
                name: "fk_user",
                table: "client",
                column: "user_id",
                principalTable: "users",
                principalColumn: "user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_package",
                table: "companies",
                column: "package_id",
                principalTable: "packages",
                principalColumn: "package_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_roles_companies_company_id",
                table: "roles");

            migrationBuilder.DropForeignKey(
                name: "fk_user_company",
                table: "users");

            migrationBuilder.DropTable(
                name: "client");

            migrationBuilder.DropTable(
                name: "company_modules");

            migrationBuilder.DropTable(
                name: "company_users");

            migrationBuilder.DropTable(
                name: "packagemodule");

            migrationBuilder.DropTable(
                name: "project_documents");

            migrationBuilder.DropTable(
                name: "project_members");

            migrationBuilder.DropTable(
                name: "rolespermissions");

            migrationBuilder.DropTable(
                name: "task_documents");

            migrationBuilder.DropTable(
                name: "task_timelogs");

            migrationBuilder.DropTable(
                name: "task_todos");

            migrationBuilder.DropTable(
                name: "taskattachment");

            migrationBuilder.DropTable(
                name: "taskcomment");

            migrationBuilder.DropTable(
                name: "department");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "tasks");

            migrationBuilder.DropTable(
                name: "modules");

            migrationBuilder.DropTable(
                name: "task_lists");

            migrationBuilder.DropTable(
                name: "taskpriorities");

            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropTable(
                name: "company_task_status");

            migrationBuilder.DropTable(
                name: "taskstatus");

            migrationBuilder.DropTable(
                name: "companies");

            migrationBuilder.DropTable(
                name: "packages");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "roles");
        }
    }
}
