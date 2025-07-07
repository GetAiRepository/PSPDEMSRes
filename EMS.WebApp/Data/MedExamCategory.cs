using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.WebApp.Data
{
    [Table("med_exam_category")]
    public class MedExamCategory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("cat_id")]
        public int CatId { get; set; }

        [Required(ErrorMessage = "Category Name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Category Name must be between 2 and 100 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\(\)\[\]]+$", ErrorMessage = "Category Name can only contain letters, numbers, spaces, hyphens, underscores, dots, parentheses, and brackets.")]
        [Column("cat_name")]
        [Display(Name = "Category Name")]
        public string CatName { get; set; } = null!;

        [Required(ErrorMessage = "Years Frequency is required.")]
        [Range(1, 10, ErrorMessage = "Years Frequency must be between 1 and 10.")]
        [Column("years_freq")]
        [Display(Name = "Years Frequency")]
        public byte YearsFreq { get; set; }

        [Required(ErrorMessage = "Annually Rule is required.")]
        [StringLength(10, MinimumLength = 1, ErrorMessage = "Annually Rule must be between 1 and 10 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\-_]+$", ErrorMessage = "Annually Rule can only contain letters, numbers, hyphens, and underscores.")]
        [Column("annually_rule")]
        [Display(Name = "Annually Rule")]
        public string AnnuallyRule { get; set; } = null!;

        [Required(ErrorMessage = "Months Schedule is required.")]
        [StringLength(50, MinimumLength = 1, ErrorMessage = "Months Schedule must be between 1 and 50 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\,]+$", ErrorMessage = "Months Schedule can only contain letters, numbers, spaces, hyphens, underscores, dots, and commas.")]
        [Column("months_sched")]
        [Display(Name = "Months Schedule")]
        public string MonthsSched { get; set; } = null!;

        [StringLength(200, ErrorMessage = "Remarks cannot exceed 200 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\,\;\:\!\?\(\)\[\]\{\}]*$", ErrorMessage = "Remarks contains invalid characters. Special characters like <, >, &, \", ', script tags are not allowed.")]
        [Column("remarks")]
        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }
    }
}