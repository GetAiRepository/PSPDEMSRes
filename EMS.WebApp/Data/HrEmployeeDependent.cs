using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace EMS.WebApp.Data;

[Table("hr_employee_dependent")]
public partial class HrEmployeeDependent
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("emp_dep_id")]
    public int emp_dep_id { get; set; }

    [Required(ErrorMessage = "Employee is required.")]
    [Display(Name = "Employee")]
    [Column("emp_uid")]
    public int emp_uid { get; set; }

    [Required(ErrorMessage = "Dependent Name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Dependent Name must be between 2 and 100 characters.")]
    [RegularExpression(@"^[a-zA-Z\s\-\.]+$", ErrorMessage = "Dependent Name can only contain letters, spaces, hyphens, and dots.")]
    [Display(Name = "Dependent Name")]
    [Column("dep_name")]
    public string dep_name { get; set; } = null!;

    [Display(Name = "Date of Birth")]
    [Column("dep_dob")]
    public DateOnly? dep_dob { get; set; }

    [Required(ErrorMessage = "Relation is required.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Relation must be between 2 and 50 characters.")]
    [RegularExpression(@"^[a-zA-Z\s\-]+$", ErrorMessage = "Relation can only contain letters, spaces, and hyphens.")]
    [Display(Name = "Relation")]
    [Column("relation")]
    public string relation { get; set; } = null!;

    [Required(ErrorMessage = "Gender is required.")]
    [RegularExpression(@"^[MFO]$", ErrorMessage = "Gender must be M (Male), F (Female), or O (Other).")]
    [Display(Name = "Gender")]
    [Column("gender")]
    public string gender { get; set; } = null!;

    [Display(Name = "Status")]
    [Column("is_active")]
    public bool is_active { get; set; }

    [Display(Name = "Marital Status")]
    [Column("marital_status")]
    public bool? marital_status { get; set; }

    [ForeignKey("emp_uid")]
    public HrEmployee? HrEmployee { get; set; }
}