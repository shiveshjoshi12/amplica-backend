using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Models;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext()
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<CompanyUser> CompanyUsers { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseNpgsql("Host=dpg-damd1up42hec738k47j0-a.singapore-postgres.render.com;Database=\"amplica\";Username=root;Password=LHlIG9rQzSKuYsPPy1B1Ll2WqjNm9KPa;Port=5432");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("uuid-ossp");

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

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
