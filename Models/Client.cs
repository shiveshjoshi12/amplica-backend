using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Models;

[Table("client")]
public partial class Client
{
    [Key]
    [Column("client_id")]
    public int ClientId { get; set; }

    [Column("company_id")]
    public int CompanyId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("is_multiple")]
    public bool? IsMultiple { get; set; }

    [Column("client_name")]
    [StringLength(255)]
    public string? ClientName { get; set; }

    [Column("client_email")]
    [StringLength(255)]
    public string? ClientEmail { get; set; }

    [Column("client_address")]
    public string? ClientAddress { get; set; }

    [Column("status")]
    [StringLength(50)]
    public string? Status { get; set; }

    [Column("createdat", TypeName = "timestamp without time zone")]
    public DateTime? Createdat { get; set; }

    [Column("client_phone")]
    [StringLength(20)]
    public string? ClientPhone { get; set; }

    [ForeignKey("CompanyId")]
    [InverseProperty("Clients")]
    public virtual Company Company { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("Clients")]
    public virtual User User { get; set; } = null!;
}
