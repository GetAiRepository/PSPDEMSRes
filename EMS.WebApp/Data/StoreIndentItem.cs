using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.WebApp.Data
{
    [Table("store_indent_item")]
    public class StoreIndentItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("indent_item_id")]
        public int IndentItemId { get; set; }

        [Required(ErrorMessage = "Indent ID is required.")]
        [Column("indent_id")]
        public int IndentId { get; set; }

        [Required(ErrorMessage = "Medicine is required.")]
        [Column("med_item_id")]
        [Display(Name = "Medicine")]
        public int MedItemId { get; set; }

        [Required(ErrorMessage = "Vendor Code is required.")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Vendor Code must be between 2 and 50 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\-_]+$", ErrorMessage = "Vendor Code can only contain letters, numbers, hyphens, and underscores.")]
        [Column("vendor_code")]
        [Display(Name = "Vendor Code")]
        public string VendorCode { get; set; } = null!;

        [Required(ErrorMessage = "Raised Quantity is required.")]
        [Column("raised_quantity")]
        [Display(Name = "Raised Quantity")]
        [Range(1, int.MaxValue, ErrorMessage = "Raised Quantity must be greater than 0")]
        public int RaisedQuantity { get; set; }

        [Column("received_quantity")]
        [Display(Name = "Received Quantity")]
        [Range(0, int.MaxValue, ErrorMessage = "Received Quantity cannot be negative")]
        public int ReceivedQuantity { get; set; } = 0;

        // Calculated property - PendingQuantity = RaisedQuantity - ReceivedQuantity
        [NotMapped]
        [Display(Name = "Pending Quantity")]
        public int PendingQuantity => RaisedQuantity - ReceivedQuantity;

        [Column("unit_price")]
        [Display(Name = "Unit Price")]
        [Range(0, (double)decimal.MaxValue, ErrorMessage = "Unit Price cannot be negative")]
        public decimal? UnitPrice { get; set; }

        [Column("total_amount")]
        [Display(Name = "Total Amount")]
        [Range(0, (double)decimal.MaxValue, ErrorMessage = "Total Amount cannot be negative")]
        public decimal? TotalAmount { get; set; }

        // New fields for Store Inventory
        [StringLength(50, ErrorMessage = "Batch No cannot exceed 50 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\-_\.]*$", ErrorMessage = "Batch No can only contain letters, numbers, hyphens, underscores, and dots.")]
        [Column("batch_no")]
        [Display(Name = "Batch No")]
        public string? BatchNo { get; set; }

        [Column("expiry_date")]
        [Display(Name = "Expiry Date")]
        [DataType(DataType.Date)]
        public DateTime? ExpiryDate { get; set; }

        // Navigation properties
        [ForeignKey("IndentId")]
        public virtual StoreIndent StoreIndent { get; set; } = null!;

        [ForeignKey("MedItemId")]
        public virtual MedMaster MedMaster { get; set; } = null!;
    }
}