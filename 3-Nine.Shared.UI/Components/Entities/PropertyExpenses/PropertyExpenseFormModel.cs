using System.ComponentModel.DataAnnotations;
using Nine.Core.Constants;

namespace Nine.Shared.UI.Components.Entities.PropertyExpenses;

/// <summary>
/// Form model for adding an expense to a property via the Add Property Expense
/// modal. Frequency and DueDate are always required and explicitly chosen by the
/// user — DueDate is never inferred from EffectiveDate.
/// </summary>
public class PropertyExpenseFormModel
{
    [Required(ErrorMessage = "Expense type is required.")]
    [StringLength(50)]
    public string ExpenseType { get; set; } = ApplicationConstants.PropertyExpenseTypes.Mortgage;

    [StringLength(200)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Amount is required.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Frequency is required.")]
    [StringLength(20)]
    public string Frequency { get; set; } = ApplicationConstants.ExpenseFrequencies.Monthly;

    /// <summary>
    /// The date this expense is due, explicitly selected by the user. For
    /// recurring expenses, only the day (and month, for Quarterly/Annual) are
    /// retained as the recurring cadence; for One-Time expenses, the exact date
    /// is used as-is.
    /// </summary>
    [Required(ErrorMessage = "Due date is required.")]
    public DateTime DueDate { get; set; } = DateTime.Today;
}
