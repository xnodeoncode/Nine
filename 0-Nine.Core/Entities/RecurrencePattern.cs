using System.ComponentModel.DataAnnotations;
using Nine.Core.Constants;

namespace Nine.Core.Entities;

/// <summary>
/// Mandatory, one-to-one recurrence definition owned by a <see cref="PropertyExpense"/>.
/// Every PropertyExpense has exactly one — its presence is never optional and never
/// inferred from missing data. Whether an expense recurs (and how) is always an
/// explicit, visible value: <see cref="Frequency"/> is <c>OneTime</c> for a
/// one-off/ad-hoc expense (e.g. an inspection fee) or one of <c>Monthly</c>,
/// <c>Quarterly</c>, <c>Annual</c> for a recurring one.
/// </summary>
public class RecurrencePattern
{
    /// <summary>
    /// From <see cref="ApplicationConstants.ExpenseFrequencies"/>. Always set —
    /// <c>OneTime</c> is the explicit representation of "does not recur", not an
    /// absent/null pattern.
    /// </summary>
    [Required]
    [StringLength(20)]
    [Display(Name = "Frequency")]
    public string Frequency { get; set; } = ApplicationConstants.ExpenseFrequencies.Monthly;

    /// <summary>
    /// Day of month (1-31) the expense is due, explicitly selected by the user at
    /// entry time (never inferred from EffectiveDate). Set for
    /// Monthly/Quarterly/Annual; unused for OneTime, whose specific due date lives
    /// directly on its single <see cref="ExpenseDue"/>.
    /// </summary>
    [Range(1, 31)]
    [Display(Name = "Due Day of Month")]
    public int? DueDayOfMonth { get; set; }

    /// <summary>
    /// Month (1-12) the cadence is anchored to, derived from the user-selected due
    /// date. Set for Quarterly (determines the recurring 3-month cycle, e.g. anchor
    /// month 2 -> Feb/May/Aug/Nov) and Annual (the single month it recurs in);
    /// unused for Monthly/OneTime.
    /// </summary>
    [Range(1, 12)]
    [Display(Name = "Anchor Month")]
    public int? AnchorMonth { get; set; }
}
