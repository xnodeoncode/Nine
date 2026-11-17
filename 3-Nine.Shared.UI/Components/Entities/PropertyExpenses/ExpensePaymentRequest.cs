namespace Nine.Shared.UI.Components.Entities.PropertyExpenses;

/// <summary>
/// Payload for the PropertyExpenseEditModal.OnMarkPaid callback: which
/// ExpenseDue the payment applies to, and the payment details entered by the
/// user.
/// </summary>
public record ExpensePaymentRequest(Guid ExpenseDueId, decimal AmountPaid, DateTime PaidOn, string? Notes);
