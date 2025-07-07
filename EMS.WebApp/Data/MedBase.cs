using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace EMS.WebApp.Data
{
    [Table("med_base")]
    public class MedBase
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("base_id")]
        [Display(Name = "Base ID")]
        public int BaseId { get; set; }

        [Required(ErrorMessage = "Base Name is required.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Base Name must be between 2 and 120 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\(\)\[\]]+$", ErrorMessage = "Base Name can only contain letters, numbers, spaces, hyphens, underscores, dots, parentheses, and brackets.")]
        [Column("base_name")]
        [Display(Name = "Base Name")]
        public string BaseName { get; set; } = null!;

        [StringLength(250, ErrorMessage = "Base Description cannot exceed 250 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\,\;\:\!\?\(\)\[\]\{\}]*$", ErrorMessage = "Base Description contains invalid characters. Special characters like <, >, &, \", ', script tags are not allowed.")]
        [Column("base_desc")]
        [Display(Name = "Base Description")]
        public string? BaseDesc { get; set; }

        [JsonIgnore]
        public ICollection<MedMaster>? MedMasters { get; set; }
    }
}