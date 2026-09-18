using System;
using System.Collections.Generic;
using BizfreeApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Data;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext()
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
    public DbSet<AttendanceRecord> AttendanceRecords { get; set; }
    public virtual DbSet<Client> Clients { get; set; }
    public virtual DbSet<Company> Companies { get; set; }
    public DbSet<CompanyDocument> CompanyDocuments { get; set; }
    public virtual DbSet<CompanyModule> CompanyModules { get; set; }
    public virtual DbSet<CompanyUser> CompanyUsers { get; set; }
    public virtual DbSet<Department> Departments { get; set; }
    public DbSet<DepartmentMaster> DepartmentMasters { get; set; }
    public virtual DbSet<Module> Modules { get; set; }
    public virtual DbSet<Package> Packages { get; set; }
    public virtual DbSet<Packagemodule> Packagemodules { get; set; }
    public virtual DbSet<Permission> Permissions { get; set; }
    public virtual DbSet<Project> Projects { get; set; }
    public virtual DbSet<ProjectDocument> ProjectDocuments { get; set; }
    public virtual DbSet<ProjectMember> ProjectMembers { get; set; }
    public virtual DbSet<Role> Roles { get; set; }
    public virtual DbSet<Rolespermission> Rolespermissions { get; set; }
    public virtual DbSet<BizfreeApp.Models.Task> Tasks { get; set; }
    public virtual DbSet<TaskTimelog> TaskTimelogs { get; set; }
    public virtual DbSet<Taskattachment> Taskattachments { get; set; }
    public virtual DbSet<Taskcomment> Taskcomments { get; set; }
    public virtual DbSet<TaskDocument> TaskDocuments { get; set; }
    public virtual DbSet<TaskList> TaskLists { get; set; }
    public virtual DbSet<Taskpriority> Taskpriorities { get; set; }
    public DbSet<Models.TaskTodo> TaskTodos { get; set; } // Add this line

    // DbSets for the two-table status design
    public virtual DbSet<Taskstatus> Taskstatuses { get; set; } // Global Master Statuses
    public virtual DbSet<CompanyTaskStatus> CompanyTaskStatuses { get; set; } // Company-Specific Statuses
    public virtual DbSet<CompanyRole> CompanyRoles { get; set; } //

    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseNpgsql("Host=dpg-damd1up42hec738k47j0-a.singapore-postgres.render.com;Database=\"amplica\";Username=root;Password=LHlIG9rQzSKuYsPPy1B1Ll2WqjNm9KPa;Port=5432");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        modelBuilder.HasPostgresExtension("uuid-ossp");

        modelBuilder.Entity<AttendanceRecord>(entity =>
        {
            entity.HasOne(a => a.Employee)
                  .WithMany(e => e.AttendanceRecords)
                  .HasForeignKey(a => a.EmployeeId)
                  .IsRequired()
                  .OnDelete(DeleteBehavior.Cascade);

            entity.Property(a => a.PunchType)
                  .HasMaxLength(3)
                  .IsRequired();

            entity.Property(a => a.DeviceId)
                  .HasMaxLength(100);

            entity.Property(a => a.Source)
                  .HasMaxLength(50);

            entity.Property(a => a.IpAddress)
                  .HasMaxLength(45);

            entity.Property(a => a.LocationLat)
                  .HasColumnType("decimal(10,7)");

            entity.Property(a => a.LocationLng)
                  .HasColumnType("decimal(10,7)");

            // Add indexes for query optimization
            entity.HasIndex(a => a.EmployeeId);
            entity.HasIndex(a => a.Timestamp);
        });

        modelBuilder.Entity<Client>(entity =>
        {
            entity.HasKey(e => e.ClientId).HasName("client_pkey");
            entity.Property(e => e.Createdat).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.IsMultiple).HasDefaultValue(false);
            entity.HasOne(d => d.Company).WithMany(p => p.Clients)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_company");
            entity.HasOne(d => d.User).WithMany(p => p.Clients)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_user");
        });
        modelBuilder.Entity<Company>(entity =>
        {
            entity.HasKey(e => e.CompanyId).HasName("companies_pkey");
            entity.ToTable("companies");

            // Unique Index
            entity.HasIndex(e => e.CompanyEmail, "companies_company_email_key").IsUnique();

            // Column configurations
            entity.Property(e => e.CompanyId).HasColumnName("company_id");
            entity.Property(e => e.CompanyName).HasColumnName("company_name").HasMaxLength(255);
            entity.Property(e => e.CompanyAddress).HasColumnName("company_address");
            entity.Property(e => e.CompanyEmail).HasColumnName("company_email").HasMaxLength(255);
            entity.Property(e => e.CompanyPhone).HasColumnName("company_phone");
            entity.Property(e => e.CompanyUrl).HasColumnName("company_url").HasMaxLength(255);
            entity.Property(e => e.CompanyLogoUrl).HasColumnName("company_logo_url");

            // --- NEWLY ADDED COLUMNS ---
            entity.Property(e => e.Vision).HasColumnName("vision");
            entity.Property(e => e.Mission).HasColumnName("mission");
            entity.Property(e => e.Goal).HasColumnName("goal");
            entity.Property(e => e.IsDeleted)
                .HasColumnName("is_deleted")
                .IsRequired()
                .HasDefaultValue(false);
            // --- END OF NEW COLUMNS ---

            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.AdminUserId).HasColumnName("admin_user_id");
            entity.Property(e => e.PackageId).HasColumnName("package_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Relationships
            entity.HasOne(d => d.Package)
                .WithMany(p => p.Companies)
                .HasForeignKey(d => d.PackageId)
                .HasConstraintName("fk_package");

            // Relationship for Company to CompanyTaskStatus
            entity.HasMany(d => d.CompanyTaskStatuses)
                .WithOne(p => p.Company)
                .HasForeignKey(p => p.CompanyId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_company_company_task_statuses");
        });

        modelBuilder.Entity<CompanyDocument>(entity =>
        {
            entity.HasKey(cd => cd.Id);

            entity.HasOne(cd => cd.Company)
                  .WithMany(c => c.Documents)
                  .HasForeignKey(cd => cd.CompanyId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(cd => cd.CreatedByUser)
                  .WithMany()
                  .HasForeignKey(cd => cd.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(cd => cd.UpdatedByUser)
                  .WithMany()
                  .HasForeignKey(cd => cd.UpdatedBy)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(cd => new { cd.CompanyId, cd.DocumentName })
                  .HasDatabaseName("IX_CompanyDocuments_CompanyId_DocumentName");

            entity.HasIndex(cd => cd.Category)
                  .HasDatabaseName("IX_CompanyDocuments_Category");
        });

        modelBuilder.Entity<CompanyModule>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("company_modules_pkey");
            entity.Property(e => e.Enabled).HasDefaultValue(true);
            entity.Property(e => e.EnabledAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(d => d.Company).WithMany(p => p.CompanyModules)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_company");
            entity.HasOne(d => d.Module).WithMany(p => p.CompanyModules)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_module");
            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.CompanyModules).HasConstraintName("fk_updated_by");
        });

        modelBuilder.Entity<CompanyRole>(entity =>
        {
            entity.HasKey(e => e.CompanyRoleId).HasName("company_roles_pkey");
            entity.ToTable("company_roles");

            // Unique index to ensure a company has unique role names
            entity.HasIndex(e => new { e.CompanyId, e.RoleName }, "uq_company_roles_company_id_role_name").IsUnique();

            // Foreign key to Company
            entity.HasOne(d => d.Company)
                .WithMany(p => p.CompanyRoles)
                .HasForeignKey(d => d.CompanyId)
                .OnDelete(DeleteBehavior.Cascade) // If company is deleted, its specific roles are deleted
                .HasConstraintName("fk_company_role_company");
            entity.Property(e => e.IsDeleted)
       .HasColumnName("is_deleted")
       .IsRequired()
       .HasDefaultValue(false);

            // Removed foreign key to global Role table as per new requirement
            // entity.HasOne(d => d.Role).WithMany(p => p.CompanyRoles)
            //     .HasForeignKey(d => d.RoleId)
            //     .OnDelete(DeleteBehavior.Cascade)
            //     .HasConstraintName("FK_company_roles_roles_role_id");

            // Foreign key to User for CreatedBy
            entity.HasOne(d => d.CreatedByNavigation)
                .WithMany(p => p.CompanyRoleCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_company_role_created_by");

            // Foreign key to User for UpdatedBy
            entity.HasOne(d => d.UpdatedByNavigation)
                .WithMany(p => p.CompanyRoleUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_company_role_updated_by");

            // Column properties and default values
            entity.Property(e => e.CompanyRoleId).HasColumnName("company_role_id").ValueGeneratedOnAdd();
            entity.Property(e => e.CompanyId).HasColumnName("company_id");
            entity.Property(e => e.RoleName).HasColumnName("role_name").IsRequired().HasMaxLength(100);
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.DefaultRole).HasColumnName("default_role");
            entity.Property(e => e.DefaultPermission).HasColumnName("default_permission");
        });

        modelBuilder.Entity<CompanyUser>(entity =>
        {
            entity.HasKey(e => e.EmployeeId).HasName("company_users_pkey");
            entity.ToTable("company_users");
            entity.HasIndex(e => e.EmployeeCode, "company_users_employee_code_key").IsUnique();
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.Address).HasColumnName("address");
            entity.Property(e => e.AnniversaryDate).HasColumnName("anniversary_date");
            entity.Property(e => e.CheckAdmin).HasColumnName("check_admin");
            entity.Property(e => e.CompanyId).HasColumnName("company_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DateOfBirth).HasColumnName("date_of_birth");
            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.EmployeeCode)
                .HasMaxLength(100)
                .HasColumnName("employee_code");
            entity.Property(e => e.EmploymentType)
                .HasMaxLength(100)
                .HasColumnName("employment_type");
            entity.Property(e => e.FirstName)
                .HasMaxLength(255)
                .HasColumnName("first_name");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.IsDeleted)
                .HasDefaultValue(false)
                .HasColumnName("is_deleted");
            entity.Property(e => e.JoiningDate).HasColumnName("joining_date");
            entity.Property(e => e.LastName)
                .HasMaxLength(255)
                .HasColumnName("last_name");
            entity.Property(e => e.MaritalStatus)
                .HasMaxLength(50)
                .HasColumnName("marital_status");
            entity.Property(e => e.ProfilePhotoUrl).HasColumnName("profile_photo_url");
            entity.Property(e => e.ReportToUserId).HasColumnName("report_to_user_id");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            // Keep existing relationships that are still valid
            entity.HasOne(d => d.CheckAdminNavigation).WithMany(p => p.CompanyUserCheckAdminNavigations)
                .HasPrincipalKey(p => p.CheckAdmin)
                .HasForeignKey(d => d.CheckAdmin)
                .HasConstraintName("fk_check_admin");

            entity.HasOne(d => d.ReportToUser).WithMany(p => p.InverseReportToUser)
                .HasForeignKey(d => d.ReportToUserId)
                .HasConstraintName("fk_report_to_user");

            // UPDATED: Replace the old Role relationship with CompanyRole relationship
            entity.HasOne(d => d.CompanyRole).WithMany(p => p.CompanyUsers)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.Restrict) // Changed from ClientSetNull to Restrict
                .HasConstraintName("fk_company_role"); // Updated constraint name

            // Add other navigation relationships
            entity.HasOne(d => d.Company).WithMany(p => p.CompanyUsers)
                .HasForeignKey(d => d.CompanyId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_company");

            entity.HasOne(d => d.Department).WithMany(p => p.CompanyUsers)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_department");

            entity.HasOne(d => d.User).WithMany(p => p.CompanyUserUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_user");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.CompanyUserCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.CompanyUserUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_updated_by");
        });


        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(e => e.DeptId).HasName("department_pkey");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(d => d.Company).WithMany(p => p.Departments)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_company");
            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.DepartmentCreatedByNavigations).HasConstraintName("fk_created_by");
            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.DepartmentUpdatedByNavigations).HasConstraintName("fk_updated_by");
        });

        modelBuilder.Entity<Module>(entity =>
        {
            entity.HasKey(e => e.ModuleId).HasName("modules_pkey");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<Package>(entity =>
        {
            entity.HasKey(e => e.PackageId).HasName("packages_pkey");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.TrialDays).HasDefaultValue(0);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PackageCreatedByNavigations).HasConstraintName("fk_created_by");
            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.PackageUpdatedByNavigations).HasConstraintName("fk_updated_by");
        });

        modelBuilder.Entity<Packagemodule>(entity =>
        {
            entity.HasKey(e => new { e.PackageId, e.ModuleId }).HasName("packagemodule_pkey");
            entity.HasOne(d => d.Module).WithMany(p => p.Packagemodules)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_module");
            entity.HasOne(d => d.Package).WithMany(p => p.Packagemodules)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_package");
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(e => e.PermissionId).HasName("permissions_pkey");
            entity.HasOne(d => d.Module).WithMany(p => p.Permissions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_module");
        });

        // --- START Project Model Configuration ---
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.ProjectId).HasName("projects_pkey");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(d => d.Company).WithMany(p => p.Projects)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_company");
            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ProjectCreatedByNavigations).HasConstraintName("fk_created_by");
            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ProjectUpdatedByNavigations).HasConstraintName("fk_updated_by");

            entity.HasOne(d => d.StatusNavigation) // A Project has one Status
                    .WithMany(p => p.Projects)      // A CompanyTaskStatus can apply to many Projects
                    .HasForeignKey(d => d.StatusId)   // The foreign key column is StatusId
                    .IsRequired(false)              // StatusId is nullable
                    .OnDelete(DeleteBehavior.NoAction) // Changed to NoAction to avoid cascade issues during migration
                    .HasConstraintName("fk_project_company_task_status_id"); // Renamed constraint for clarity
        });
        // --- END Project Model Configuration ---

        modelBuilder.Entity<ProjectMember>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("project_members_pkey");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.JoinedAt).HasDefaultValueSql("CURRENT_DATE");
            entity.HasOne(d => d.AddedByNavigation).WithMany(p => p.ProjectMemberAddedByNavigations).HasConstraintName("fk_added_by");
            entity.HasOne(d => d.Project).WithMany(p => p.ProjectMembers)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project");
            entity.HasOne(d => d.User).WithMany(p => p.ProjectMemberUsers)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_user");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("roles_pkey");
            entity.ToTable("roles");
            entity.HasIndex(e => e.CheckAdmin, "uq_roles_check_admin").IsUnique();
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.CheckAdmin)
                .IsRequired()
                .HasColumnName("check_admin");
            entity.Property(e => e.CompanyId).HasColumnName("company_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_at");
            entity.Property(e => e.RoleName)
                .HasMaxLength(100)
                .HasColumnName("role_name");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<Rolespermission>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("rolespermissions_pkey");
            entity.HasOne(d => d.Company).WithMany(p => p.Rolespermissions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_company");
            entity.HasOne(d => d.Permission).WithMany(p => p.Rolespermissions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_permission");
            entity.HasOne(d => d.Role).WithMany(p => p.Rolespermissions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_role");
        });

        // --- START Task Model Configuration ---
        modelBuilder.Entity<BizfreeApp.Models.Task>(entity =>
        {
            entity.HasKey(e => e.TaskId).HasName("tasks_pkey");
            entity.Property(e => e.TaskId).HasColumnName("task_id").ValueGeneratedOnAdd();

            entity.HasOne(d => d.AssignedToNavigation).WithMany(p => p.Tasks).HasConstraintName("tasks_assigned_to_fkey");
            entity.HasOne(d => d.Company).WithMany(p => p.Tasks)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("tasks_company_id_fkey");
            entity.HasOne(d => d.Priority).WithMany(p => p.Tasks).HasConstraintName("tasks_priority_id_fkey");
            entity.HasOne(d => d.Project).WithMany(p => p.Tasks)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("tasks_project_id_fkey");
            // StatusNavigation for Task, now linking to CompanyTaskStatus
            entity.HasOne(d => d.StatusNavigation)
                .WithMany(p => p.Tasks)
                .HasForeignKey(d => d.Status) // Foreign key is the 'Status' property in Task
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction) // Changed to NoAction to avoid cascade issues during migration
                .HasConstraintName("fk_task_company_task_status"); // Renamed constraint for clarity

            // Self-referencing relationship for ParentTask/SubTasks
            entity.HasOne(d => d.ParentTask)
                    .WithMany(p => p.SubTasks)
                    .HasForeignKey(d => d.ParentTaskId)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("fk_task_parent_task_id");
        });
        // --- END Task Model Configuration ---

        modelBuilder.Entity<TaskTimelog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("task_timelogs_pkey");
            entity.Property(e => e.LoggedAt).ValueGeneratedOnAdd();
            entity.HasOne(d => d.Task)
                    .WithMany(p => p.TaskTimelogs)
                    .HasForeignKey(d => d.TaskId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("task_timelogs_task_id_fkey");
            entity.HasOne(d => d.User)
                    .WithMany(p => p.TaskTimelogs)
                    .HasForeignKey(d => d.UserId)
                    .HasConstraintName("task_timelogs_user_id_fkey");
        });

        modelBuilder.Entity<Taskattachment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("taskattachment_pkey");
            entity.Property(e => e.UploadedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(d => d.Task).WithMany(p => p.Taskattachments)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("taskattachment_task_id_fkey");
            entity.HasOne(d => d.UploadedByNavigation).WithMany(p => p.Taskattachments).HasConstraintName("taskattachment_uploaded_by_fkey");
        });

        modelBuilder.Entity<Taskcomment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("taskcomment_pkey");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(d => d.ParentComment).WithMany(p => p.InverseParentComment).HasConstraintName("taskcomment_parent_comment_id_fkey");
            entity.HasOne(d => d.Task).WithMany(p => p.Taskcomments)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("taskcomment_task_id_fkey");
            entity.HasOne(d => d.User).WithMany(p => p.Taskcomments).HasConstraintName("taskcomment_user_id_fkey");
        });

        modelBuilder.Entity<TaskList>(entity =>
        {
            entity.ToTable("task_lists");
            entity.HasKey(e => e.TaskListId);
            entity.Property(e => e.TaskListId)
                    .HasColumnName("task_list_id")
                    .ValueGeneratedOnAdd();
            entity.Property(e => e.ListName)
                    .HasColumnName("list_name")
                    .IsRequired()
                    .HasMaxLength(255);
            entity.Property(e => e.Description)
                    .HasColumnName("description")
                    .HasMaxLength(1000);
            entity.Property(e => e.ListOrder)
                    .HasColumnName("list_order");
        });

        modelBuilder.Entity<Taskpriority>(entity =>
        {
            entity.HasKey(e => e.PriorityId).HasName("taskpriorities_pkey");
            // Existing scaffolded configurations (if any)
        });

        modelBuilder.Entity<TaskTodo>(entity =>
        {
            entity.HasOne(d => d.AssignedToNavigation).WithMany(p => p.TaskTodos).HasForeignKey(d => d.AssignedTo);
            entity.HasOne(d => d.Company).WithMany(p => p.TaskTodos).HasForeignKey(d => d.CompanyId);
            entity.HasOne(d => d.Priority).WithMany(p => p.TaskTodos).HasForeignKey(d => d.PriorityId);
            entity.HasOne(d => d.Project).WithMany(p => p.TaskTodos).HasForeignKey(d => d.ProjectId);
            entity.HasOne(d => d.StatusNavigation).WithMany(p => p.TaskTodos).HasForeignKey(d => d.Status);
            entity.HasOne(d => d.TaskList).WithMany(p => p.TaskTodos).HasForeignKey(d => d.TaskListId);
            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.TaskTodosCreated).HasForeignKey(d => d.CreatedBy);
            entity.HasOne(d => d.UpdatedByUser).WithMany(p => p.TaskTodosUpdated).HasForeignKey(d => d.UpdatedBy);

            // Configure self-referencing relationship for sub-todos
            entity.HasOne(d => d.TaskList)
         .WithMany(p => p.TaskTodos) // TaskList has many TaskTodos
         .HasForeignKey(d => d.TaskListId)
         .IsRequired(false); // TaskListId can be null for TaskTodo
        });

        // --- START Taskstatus Model Configuration (Global Master) ---
        modelBuilder.Entity<Taskstatus>(entity =>
        {
            entity.HasKey(e => e.StatusId).HasName("taskstatus_pkey"); // Primary key

            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.StatusColor).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Relationships for audit users
            entity.HasOne(d => d.CreatedByNavigation)
                .WithMany(p => p.TaskstatusCreatedByNavigations) // Assuming User has this collection
                .HasForeignKey(d => d.CreatedBy)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_taskstatus_created_by");

            entity.HasOne(d => d.UpdatedByNavigation)
                .WithMany(p => p.TaskstatusUpdatedByNavigations) // Assuming User has this collection
                .HasForeignKey(d => d.UpdatedBy)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_taskstatus_updated_by");

            // Relationship to CompanyTaskStatus (one-to-many: one master status can have many company-specific instances)
            entity.HasMany(d => d.CompanyTaskStatuses)
                .WithOne(p => p.DefaultTaskStatusNavigation)
                .HasForeignKey(p => p.DefaultTaskStatusId)
                .IsRequired(false) // DefaultTaskStatusId is nullable in CompanyTaskStatus
                .OnDelete(DeleteBehavior.SetNull) // If a master status is deleted, its link in company statuses becomes null
                .HasConstraintName("fk_taskstatus_company_task_statuses"); // Renamed constraint for clarity
        });
        // --- END Taskstatus Model Configuration ---

        // --- START CompanyTaskStatus Model Configuration (Company-Specific) ---
        modelBuilder.Entity<CompanyTaskStatus>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("company_task_status_pkey");

            // Foreign key to Company
            entity.HasOne(d => d.Company)
                .WithMany(p => p.CompanyTaskStatuses) // Assuming Company model has this collection
                .HasForeignKey(d => d.CompanyId)
                .OnDelete(DeleteBehavior.Cascade) // If company is deleted, its specific statuses are deleted
                .HasConstraintName("fk_company_task_status_company");

            // Foreign key to Taskstatus (the global master)
            entity.HasOne(d => d.DefaultTaskStatusNavigation)
                .WithMany(p => p.CompanyTaskStatuses) // This refers to the collection in Taskstatus (the global master)
                .HasForeignKey(d => d.DefaultTaskStatusId)
                .IsRequired(false) // It's nullable
                .OnDelete(DeleteBehavior.SetNull) // If the master status is deleted, set FK to null
                .HasConstraintName("fk_company_task_status_default_status");

            // Foreign key to User for CreatedBy
            entity.HasOne(d => d.CreatedByNavigation)
                .WithMany(p => p.CompanyTaskStatusCreatedByNavigations) // Assuming User has this collection
                .HasForeignKey(d => d.CreatedBy)
                .IsRequired(false) // CreatedBy is nullable
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_company_task_status_created_by");

            // Foreign key to User for UpdatedBy
            entity.HasOne(d => d.UpdatedByNavigation)
                .WithMany(p => p.CompanyTaskStatusUpdatedByNavigations) // Assuming User has this collection
                .HasForeignKey(d => d.UpdatedBy)
                .IsRequired(false) // UpdatedBy is nullable
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_company_task_status_updated_by");

            // Column properties and default values
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Slug).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Order).IsRequired();
            entity.Property(e => e.StatusColor).HasMaxLength(50);
            entity.Property(e => e.IsCustom).HasDefaultValue(false);
            entity.Property(e => e.IsEditableName).HasDefaultValue(true);
            entity.Property(e => e.IsDeletable).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        // --- END CompanyTaskStatus Model Configuration ---

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("users_pkey");
            entity.Property(e => e.UserId).ValueGeneratedOnAdd();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);

            entity.HasOne(d => d.Role)
                    .WithMany(p => p.Users)
                    .HasForeignKey(d => d.RoleId)
                    .HasConstraintName("fk_role");

            entity.HasOne(d => d.Company)
                    .WithMany(p => p.Users)
                    .HasForeignKey(d => d.CompanyId)
                    .HasConstraintName("fk_user_company")
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Restrict);

            // Relationships for audit trails in Taskstatus (global master)
            entity.HasMany(d => d.TaskstatusCreatedByNavigations)
                .WithOne(p => p.CreatedByNavigation)
                .HasForeignKey(p => p.CreatedBy)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_user_taskstatus_created_by");

            entity.HasMany(d => d.TaskstatusUpdatedByNavigations)
                .WithOne(p => p.UpdatedByNavigation)
                .HasForeignKey(p => p.UpdatedBy)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_user_taskstatus_updated_by");

            // Relationships for audit trails in CompanyTaskStatus (company-specific)
            entity.HasMany(d => d.CompanyTaskStatusCreatedByNavigations)
                .WithOne(p => p.CreatedByNavigation)
                .HasForeignKey(p => p.CreatedBy)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_user_company_task_status_created_by");

            entity.HasMany(d => d.CompanyTaskStatusUpdatedByNavigations)
                .WithOne(p => p.UpdatedByNavigation)
                .HasForeignKey(p => p.UpdatedBy)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_user_company_task_status_updated_by");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}