using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace EMS.WebApp.Data
{
    [Table("med_diagnosis")]
    public class MedDiagnosis
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Display(Name = "Diagnosis ID")]
        [Column("diag_id")]
        public int diag_id { get; set; }

        [Required(ErrorMessage = "Diagnosis Name is required.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Diagnosis Name must be between 2 and 120 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\(\)\[\]]+$", ErrorMessage = "Diagnosis Name can only contain letters, numbers, spaces, hyphens, underscores, dots, parentheses, and brackets.")]
        [Display(Name = "Diagnosis Name")]
        [Column("diag_name")]
        public string diag_name { get; set; } = null!;

        [StringLength(250, ErrorMessage = "Description cannot exceed 250 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\,\;\:\!\?\(\)\[\]\{\}]*$", ErrorMessage = "Description contains invalid characters. Special characters like <, >, &, \", ', script tags are not allowed.")]
        [Display(Name = "Description")]
        [Column("diag_desc")]
        public string? diag_desc { get; set; }
    }
}