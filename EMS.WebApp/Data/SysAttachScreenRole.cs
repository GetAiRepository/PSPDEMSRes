using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace EMS.WebApp.Data;

[Table("sys_attach_screen_role")]
public partial class SysAttachScreenRole
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("uid")]
    public int uid { get; set; }

    [Display(Name = "Role Uid")]
    [Column("role_uid")]
    public int role_uid { get; set; }

    [Display(Name = "Screen Uid")]
    [Column("screen_uid")]
    public string screen_uid { get; set; } = ""; // stores "1,2,5"

    [NotMapped]
    public List<int> screen_uids
    {
        get => string.IsNullOrEmpty(screen_uid)
            ? new List<int>()
            : screen_uid.Split(',').Select(int.Parse).ToList();

        set => screen_uid = string.Join(",", value);
    }

    [ForeignKey("role_uid")]
    public SysRole? SysRole { get; set; }

    // Remove the foreign key relationship since screen_uid is a string containing multiple IDs
    // [ForeignKey("screen_uid")]
    // public sys_screen_name? sys_screen_name { get; set; }

    // Instead, use a NotMapped property to get related screens when needed
    [NotMapped]
    public List<sys_screen_name>? RelatedScreens { get; set; }
}
