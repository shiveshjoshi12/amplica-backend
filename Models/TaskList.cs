using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Models;


    [Table("task_lists")]
    public class TaskList
    {
        [Key]
        [Column("task_list_id")]
        public int TaskListId { get; set; }

        [Column("project_id")]
        [Required]
        public int ProjectId { get; set; }

        [Column("company_id")]
        [Required]
        public int CompanyId { get; set; }

        [Column("list_name")]
        [Required]
        [MaxLength(255)]
        public string ListName { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("list_order")]
        public int? ListOrder { get; set; }

        [Column("status")]
        [MaxLength(100)]
        public string Status { get; set; } = "active";

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("is_deleted")]
        public bool IsDeleted { get; set; } = false;

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Column("updated_by")]
        public int? UpdatedBy { get; set; }

    [Column("start_date")]
    public DateOnly? StartDate { get; set; } // Using DateOnly for date without time

    [Column("end_date")]
    public DateOnly? EndDate { get; set; }

    // Navigation Properties
    [ForeignKey("ProjectId")]
        public virtual Project? Project { get; set; }

        [ForeignKey("CompanyId")]
        public virtual Company? Company { get; set; }

        [ForeignKey("CreatedBy")]
        public virtual User? CreatedByUser { get; set; }

        [ForeignKey("UpdatedBy")]
        public virtual User? UpdatedByUser { get; set; }

        [InverseProperty("TaskList")]
        public virtual ICollection<Task> Tasks { get; set; } = new List<Task>();

    [InverseProperty("TaskList")] //
    public virtual ICollection<TaskTodo> TaskTodos { get; set; } = new List<TaskTodo>();
}
