using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.WebApp.Data;

[Table("sys_screen_name")]
public partial class sys_screen_name
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("screen_uid")]
    public int screen_uid { get; set; }

    [Required(ErrorMessage = "Screen Name is required.")]
    [StringLength(40, MinimumLength = 2, ErrorMessage = "Screen Name must be between 2 and 40 characters.")]
    [RegularExpression(@"^[a-zA-Z][a-zA-Z0-9]*$", ErrorMessage = "Screen Name must start with a letter and contain only letters and numbers.")]
    [Display(Name = "Screen Name")]
    [Column("screen_name")]
    public string screen_name { get; set; } = null!;

    [StringLength(250, ErrorMessage = "Description cannot exceed 250 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\,\;\:\!\?\(\)\[\]\{\}]*$", ErrorMessage = "Description contains invalid characters. Special characters like <, >, &, \", ', script tags are not allowed.")]
    [Display(Name = "Description")]
    [Column("screen_description")]
    public string? screen_description { get; set; }

    // Remove the collection navigation property since we can't have a direct relationship
    // public ICollection<SysAttachScreenRole>? SysAttachScreenRole { get; set; }
}