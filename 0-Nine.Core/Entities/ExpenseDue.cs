using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Nine.Core.Validation;

namespace Nine.Core.Entities;

/// <summary>
/// Represents one billable instance of a <see cref="PropertyExpense"/> — either
/// generated per cycle (for expenses whose RecurrencePattern.Frequency is
/// Monthly/Quarterly/Annual) or created once, directly, at entry time (for
/// OneTime expenses). This is the record of "what's actually owed and when",
/// distinct from the PropertyExpense rule that produced it.
/// </summary>
public class ExpenseDue : BaseModel
{
    [RequiredGuid]
    public Guid PropertyExpenseId { get; set; }

    /// <summary>
    /// Denormalized for query convenience, matching the pattern used elsewhere
    /// (e.g. PropertyExpense.PropertyId).
    /// </summary>
    [RequiredGuid]
    public Guid PropertyId { get; set; }

    /// <summary>
    /// Full date this instance is due. For recurring expenses, composed from
    /// RecurrencePattern.DueDayOfMonth/AnchorMonth and the generation cycle's
    /// year/month. For OneTime expenses, the exact user-selected date.
    /// </summary>
    [Required]
    [Display(Name = "Due Date")]
    public DateTime DueDate { get; set; }

    /// <summary>
    /// Snapshot of the owning expense's Amount at generation/entry time — protects
    /// history if the expense's Amount is edited later (e.g. a mid-year property
    /// tax change). Editing PropertyExpense.Amount in place never rewrites
    /// already-generated ExpenseDue rows.
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Amount Due")]
    public decimal AmountDue { get; set; }

    /// <summary>
    /// Set when a user explicitly waives/skips this instance. A user decision,
    /// not a payment-math outcome — never derived from AmountPaid.
    /// </summary>
    [Display(Name = "Waived On")]
    public DateTime? WaivedOn { get; set; }

    [StringLength(200)]
    [Display(Name = "Waived Reason")]
    public string? WaivedReason { get; set; }

    [StringLength(2000)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual PropertyExpense? PropertyExpense { get; set; }
    public virtual ICollection<ExpensePayment> Payments { get; set; } = new List<ExpensePayment>();

    // Computed Properties
    [NotMapped]
    [Display(Name = "Amount Paid")]
    public decimal AmountPaid => Payments.Where(p => !p.IsDeleted).Sum(p => p.AmountPaid);

    [NotMapped]
    [Display(Name = "Balance Due")]
    public decimal BalanceDue => AmountDue - AmountPaid;

    /// <summary>
    /// Unpaid / PartiallyPaid / Paid / Overpaid, derived from AmountPaid vs
    /// AmountDue — unless explicitly Waived, which always takes precedence.
    /// </summary>
    [NotMapped]
    [Display(Name = "Status")]
    public string Status
    {
        get
        {
            if (WaivedOn.HasValue)
                return "Waived";

            if (AmountPaid <= 0m)
                return "Unpaid";

            if (AmountPaid < AmountDue)
                return "PartiallyPaid";

            return AmountPaid > AmountDue ? "Overpaid" : "Paid";
        }
    }
}
