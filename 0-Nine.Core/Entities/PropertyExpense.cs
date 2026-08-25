using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Nine.Core.Constants;
using Nine.Core.Validation;

namespace Nine.Core.Entities;

/// <summary>
/// Represents an expense associated with a property (e.g. mortgage, insurance,
/// property tax, HOA, maintenance, utilities, an inspection fee, or other custom
/// costs). An expense's recurrence (if any) is captured by its required
/// <see cref="RecurrencePattern"/>; whether it repeats and how is a separate
/// concern from what kind of expense it is (<see cref="ExpenseType"/>). Actual
/// billable instances and payments are tracked separately via
/// <see cref="ExpenseDue"/> and <see cref="ExpensePayment"/>.
/// </summary>
public class PropertyExpense : BaseModel
{
    // Core Identity
    [RequiredGuid]
    public Guid PropertyId { get; set; }

    [Required]
    [StringLength(50)]
    [Display(Name = "Expense Type")]
    public string ExpenseType { get; set; } = string.Empty; // From ApplicationConstants.PropertyExpenseTypes

    [StringLength(200)]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty; // e.g., "Primary mortgage - Chase", "Annual hazard insurance"

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Amount")]
    public decimal Amount { get; set; }

    /// <summary>
    /// Date the expense obligation actually begins in the real world. This is a
    /// business fact, distinct from <see cref="BaseModel.CreatedOn"/> (a system
    /// audit timestamp of when the row was inserted) — entry may be retroactive
    /// (EffectiveDate in the past) or advance (EffectiveDate in the future).
    /// Immutable after creation: routine edits to Amount, ExpenseType,
    /// Description, Notes, or RecurrencePattern never change this value. Only a
    /// brand-new PropertyExpense row gets a new EffectiveDate.
    /// </summary>
    [Required]
    [Display(Name = "Effective Date")]
    public DateTime EffectiveDate { get; set; } = DateTime.Today;

    /// <summary>
    /// Optional date the expense ends (e.g. mortgage payoff, policy cancellation).
    /// Null means the expense is ongoing/indefinite.
    /// </summary>
    [Display(Name = "End Date")]
    public DateTime? EndDate { get; set; }

    [StringLength(2000)]
    [Display(Name = "Notes")]
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Recorded when an expense is cancelled via the Edit → Cancel Expense flow.
    /// Null for active expenses and voided records.
    /// </summary>
    [StringLength(50)]
    [Display(Name = "Cancellation Reason")]
    public string? EndReason { get; set; }

    /// <summary>
    /// Mandatory recurrence definition for this expense. Every PropertyExpense has
    /// exactly one — its Frequency is never absent/inferred, so "does this recur?"
    /// is always an explicit, visible fact rather than ambiguous missing data.
    /// </summary>
    [Required]
    public RecurrencePattern RecurrencePattern { get; set; } = new();

    // Navigation Properties
    public virtual Property? Property { get; set; }
    public virtual ICollection<ExpenseDue> ExpenseDues { get; set; } = new List<ExpenseDue>();

    // Computed Properties
    [NotMapped]
    [Display(Name = "Monthly Amount")]
    public decimal MonthlyAmount => RecurrencePattern.Frequency switch
    {
        ApplicationConstants.ExpenseFrequencies.Monthly => Amount,
        ApplicationConstants.ExpenseFrequencies.Quarterly => Amount / 3m,
        ApplicationConstants.ExpenseFrequencies.Annual => Amount / 12m,
        ApplicationConstants.ExpenseFrequencies.OneTime => 0m, // One-time costs don't contribute to recurring monthly budget totals
        _ => Amount
    };

    [NotMapped]
    [Display(Name = "Is Active")]
    public bool IsActive => !EndDate.HasValue || EndDate.Value >= DateTime.Today;
}
