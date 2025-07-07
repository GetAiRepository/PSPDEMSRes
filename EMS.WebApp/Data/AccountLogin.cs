using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EMS.WebApp.Data;

[Table("account_login")]
public partial class AccountLogin
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("login_id")]
    public int login_id { get; set; }

    [Column("user_name")]
    public string user_name { get; set; } = null!;

    [Column("password")]
    public string? password { get; set; }
    [Column("is_active")]
    public bool is_active { get; set; }

    //this is for blocking multiple tab
    public string? SessionToken { get; set; }
    public DateTime? TokenIssuedAt { get; set; }
}
