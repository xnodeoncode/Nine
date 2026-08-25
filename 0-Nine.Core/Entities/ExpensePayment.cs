using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Nine.Core.Validation;

namespace Nine.Core.Entities;

/// <summary>
/// Represents one actual payment event against an <see cref="ExpenseDue"/>.
/// Multiple rows per ExpenseDue are expected and supported — e.g. a mortgage
/// split into two payments, or a duplicate/extra payment. "Mark Paid" always
/// inserts a new row; it never edits or overwrites a prior payment.
/// </summary>
public class ExpensePayment : BaseModel
{
    [RequiredGuid]
    public Guid ExpenseDueId { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Amount Paid")]
    public decimal AmountPaid { get; set; }

    [Required]
    [Display(Name = "Paid On")]
    public DateTime PaidOn { get; set; } = DateTime.Today;

    [StringLength(500)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; } // e.g., "second half of mortgage payment"

    // Navigation Properties
    public virtual ExpenseDue? ExpenseDue { get; set; }
}
