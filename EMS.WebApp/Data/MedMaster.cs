﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.WebApp.Data
{
    [Table("med_master")]
    public class MedMaster
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("med_item_id")]
        public int MedItemId { get; set; }

        [Required(ErrorMessage = "Medicine Name is required.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Medicine Name must be between 2 and 120 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\(\)\[\]\/\+]+$", ErrorMessage = "Medicine Name can only contain letters, numbers, spaces, hyphens, underscores, dots, parentheses, brackets, forward slashes, and plus signs.")]
        [Column("med_item_name")]
        [Display(Name = "Medicine Name")]
        public string MedItemName { get; set; } = null!;

        [Required(ErrorMessage = "Base selection is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a valid medicine base.")]
        [Column("base_id")]
        [Display(Name = "Medicine Base")]
        public int? BaseId { get; set; }

        [StringLength(120, MinimumLength = 2, ErrorMessage = "Company Name must be between 2 and 120 characters when provided.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\(\)\[\]&,]+$", ErrorMessage = "Company Name can only contain letters, numbers, spaces, hyphens, underscores, dots, parentheses, brackets, ampersands, and commas.")]
        [Column("company_name")]
        [Display(Name = "Company Name")]
        public string? CompanyName { get; set; }

        [Required(ErrorMessage = "Reorder Limit is required.")]
        [Range(0, 999999, ErrorMessage = "Reorder Limit must be between 0 and 999999.")]
        [Column("reorder_limit")]
        [Display(Name = "Reorder Limit")]
        public int ReorderLimit { get; set; }

        [ForeignKey("BaseId")]
        public MedBase? MedBase { get; set; }
    }
}