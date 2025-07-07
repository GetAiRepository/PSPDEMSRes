﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace EMS.WebApp.Data;

[Table("med_disease")]
public partial class MedDisease
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("disease_id")]
    public int DiseaseId { get; set; }

    [Required(ErrorMessage = "Disease Name is required.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "Disease Name must be between 2 and 120 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\(\)\[\]]+$", ErrorMessage = "Disease Name can only contain letters, numbers, spaces, hyphens, underscores, dots, parentheses, and brackets.")]
    [Column("disease_name")]
    [Display(Name = "Disease Name")]
    public string DiseaseName { get; set; } = null!;

    [StringLength(250, ErrorMessage = "Description cannot exceed 250 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\,\;\:\!\?\(\)\[\]\{\}]*$", ErrorMessage = "Description contains invalid characters. Special characters like <, >, &, \", ', script tags are not allowed.")]
    [Column("disease_desc")]
    [Display(Name = "Description")]
    public string? DiseaseDesc { get; set; }
}