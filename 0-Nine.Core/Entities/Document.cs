using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nine.Core.Entities {

    public class Document:BaseModel
    {
        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [StringLength(10)]
        public string FileExtension { get; set; } = string.Empty; // .pdf, .jpg, .docx, etc.

        [Required]
        public byte[] FileData { get; set; } = Array.Empty<byte>();

        [StringLength(255)]
        public string FilePath { get; set; } = string.Empty;

        [StringLength(500)]
        public string ContentType { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string FileType { get; set; } = string.Empty; // PDF, Image, etc.

        public long FileSize { get; set; }

        [Required]
        [StringLength(100)]
        public string DocumentType { get; set; } = string.Empty; // Lease Agreement, Invoice, Receipt, Photo, etc.

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        // Foreign keys - at least one must be set
        public Guid? PropertyId { get; set; }
        public Guid? TenantId { get; set; }
        public Guid? LeaseId { get; set; }
        public Guid? InvoiceId { get; set; }
        public Guid? PaymentId { get; set; }
        public Guid? RepairId { get; set; }

        // Navigation properties
        [ForeignKey("PropertyId")]
        public virtual Property? Property { get; set; }

        [ForeignKey("TenantId")]
        public virtual Tenant? Tenant { get; set; }

        [ForeignKey("LeaseId")]
        public virtual Lease? Lease { get; set; }

        [ForeignKey("InvoiceId")]
        public virtual Invoice? Invoice { get; set; }

        [ForeignKey("PaymentId")]
        public virtual Payment? Payment { get; set; }

        [ForeignKey("RepairId")]
        public virtual Repair? Repair { get; set; }

        // Computed property
        public string FileSizeFormatted
        {
            get
            {
                string[] sizes = { "B", "KB", "MB", "GB" };
                double len = FileSize;
                int order = 0;
                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len = len / 1024;
                }
                return $"{len:0.##} {sizes[order]}";
            }
        }
    }
}