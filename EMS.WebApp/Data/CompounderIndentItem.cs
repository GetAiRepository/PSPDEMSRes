using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.WebApp.Data
{
    [Table("compounder_indent_item")]
    public class CompounderIndentItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("indent_item_id")]
        public int IndentItemId { get; set; }

        [Required]
        [Column("indent_id")]
        public int IndentId { get; set; }

        [Required]
        [Column("med_item_id")]
        [Display(Name = "Medicine")]
        public int MedItemId { get; set; }

        [Required]
        [Column("vendor_code")]
        [MaxLength(50)]
        [Display(Name = "Vendor Code")]
        public string VendorCode { get; set; } = null!;

        [Required]
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
        public decimal? UnitPrice { get; set; }

        [Column("total_amount")]
        [Display(Name = "Total Amount")]
        public decimal? TotalAmount { get; set; }

        // New fields for Compounder Inventory - Batch, Expiry, and Available Stock tracking
        [Column("batch_no")]
        [MaxLength(50)]
        [Display(Name = "Batch No")]
        public string? BatchNo { get; set; }

        [Column("expiry_date")]
        [Display(Name = "Expiry Date")]
        [DataType(DataType.Date)]
        public DateTime? ExpiryDate { get; set; }

        [Column("available_stock")]
        [Display(Name = "Available Stock")]
        [Range(0, int.MaxValue, ErrorMessage = "Available Stock cannot be negative")]
        public int? AvailableStock { get; set; }

        // Navigation properties
        [ForeignKey("IndentId")]
        public virtual CompounderIndent CompounderIndent { get; set; } = null!;

        [ForeignKey("MedItemId")]
        public virtual MedMaster MedMaster { get; set; } = null!;
    }
}