using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nine.Core.Entities
{
    public class Invoice : BaseModel
    {
        [Required]
        public Guid LeaseId { get; set; }

        [Required]
        [StringLength(50)]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        public DateTime InvoicedOn { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime DueOn { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(100)]
        public string Description { get; set; } = string.Empty;

        [StringLength(50)]
        public string Status { get; set; } = "Pending"; // Pending, Paid, Overdue, Cancelled

        public DateTime? PaidOn { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountPaid { get; set; }

        [StringLength(500)]
        public string Notes { get; set; } = string.Empty;

        // Late Fee Properties
        [Column(TypeName = "decimal(18,2)")]
        public decimal? LateFeeAmount { get; set; }

        public bool? LateFeeApplied { get; set; }

        public DateTime? LateFeeAppliedOn { get; set; }

        // Reminder Properties
        public bool? ReminderSent { get; set; }

        public DateTime? ReminderSentOn { get; set; }

    // Document Tracking
    public Guid? DocumentId { get; set; }

        // Navigation properties
    [ForeignKey("LeaseId")]
    public virtual Lease Lease { get; set; } = null!;

    [ForeignKey("DocumentId")]
    public virtual Document? Document { get; set; }

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

        // Computed properties
        public decimal BalanceDue => Amount + (LateFeeAmount ?? 0) - AmountPaid;
        public bool IsOverdue => Status != "Paid" && DueOn < DateTime.Now;
        public int DaysOverdue => IsOverdue ? (DateTime.Now - DueOn).Days : 0;
    }
}