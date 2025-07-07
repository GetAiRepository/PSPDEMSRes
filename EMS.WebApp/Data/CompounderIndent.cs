using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.WebApp.Data
{
    [Table("compounder_indent")]
    public class CompounderIndent
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("indent_id")]
        public int IndentId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("indent_type")]
        [Display(Name = "Indent Type")]
        public string IndentType { get; set; } = null!;

        [Required]
        [Column("indent_date")]
        [Display(Name = "Indent Raised Date")]
        [DataType(DataType.Date)]
        public DateTime IndentDate { get; set; }

        [Column("created_date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [Column("created_by")]
        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        [Column("status")]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending";

        [Column("comments")]
        [MaxLength(500)]
        [Display(Name = "Comments")]
        public string? Comments { get; set; }

        [Column("approved_by")]
        [MaxLength(100)]
        public string? ApprovedBy { get; set; }

        [Column("approved_date")]
        public DateTime? ApprovedDate { get; set; }

        // Navigation property
        public virtual ICollection<CompounderIndentItem> CompounderIndentItems { get; set; } = new List<CompounderIndentItem>();
    }
}