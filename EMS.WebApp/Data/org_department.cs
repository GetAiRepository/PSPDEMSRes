﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace EMS.WebApp.Data;

[Table("org_department")]
public partial class org_department
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public short dept_id { get; set; }

    [Required(ErrorMessage = "Department Name is required.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "Department Name must be between 2 and 120 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\(\)\[\]]+$", ErrorMessage = "Department Name can only contain letters, numbers, spaces, hyphens, underscores, dots, parentheses, and brackets.")]
    [Display(Name = "Department Name")]
    [Column("dept_name")]
    public string dept_name { get; set; } = null!;

    [StringLength(250, ErrorMessage = "Description cannot exceed 250 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\,\;\:\!\?\(\)\[\]\{\}]*$", ErrorMessage = "Description contains invalid characters. Special characters like <, >, &, \", ', script tags are not allowed.")]
    [Display(Name = "Description")]
    [Column("dept_description")]
    public string? dept_description { get; set; }

    [StringLength(250, ErrorMessage = "Remarks cannot exceed 250 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\,\;\:\!\?\(\)\[\]\{\}]*$", ErrorMessage = "Remarks contains invalid characters. Special characters like <, >, &, \", ', script tags are not allowed.")]
    [Display(Name = "Remarks")]
    [Column("Remarks")]
    public string? Remarks { get; set; }

    [JsonIgnore]
    public ICollection<HrEmployee>? HrEmployees { get; set; }
}