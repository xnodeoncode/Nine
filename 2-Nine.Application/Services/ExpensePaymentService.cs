using Nine.Core.Constants;
using Nine.Core.Entities;
using Nine.Core.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nine.Application.Services;

/// <summary>
/// Service for recording actual payments against an ExpenseDue. Multiple
/// payments per ExpenseDue are expected and supported (e.g. a mortgage split
/// into two payments, or a duplicate/extra payment) — "Mark Paid" always
/// inserts a new row and never edits or overwrites a prior payment.
/// </summary>
public class ExpensePaymentService : BaseService<ExpensePayment>
{
    public ExpensePaymentService(
        ApplicationDbContext context,
        ILogger<ExpensePaymentService> logger,
        IUserContextService userContext,
        IOptions<ApplicationSettings> settings)
        : base(context, logger, userContext, settings)
    {
    }

    protected override async Task ValidateEntityAsync(ExpensePayment entity)
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();

        var dueExists = await _context.Set<ExpenseDue>()
            .AnyAsync(d => d.Id == entity.ExpenseDueId && d.OrganizationId == orgId && !d.IsDeleted);

        if (!dueExists)
        {
            throw new InvalidOperationException("Expense due not found or does not belong to your organization.");
        }

        if (entity.AmountPaid <= 0)
        {
            throw new InvalidOperationException("Amount paid must be greater than zero.");
        }

        await base.ValidateEntityAsync(entity);
    }

    /// <summary>
    /// Records a payment against an ExpenseDue. This is the "Mark Paid" action —
    /// always available regardless of the due's current status, and always
    /// inserts a new ExpensePayment row rather than modifying an existing one.
    /// </summary>
    public async Task<ExpensePayment> RecordPaymentAsync(
        Guid expenseDueId,
        decimal amountPaid,
        DateTime paidOn,
        string? notes = null)
    {
        var payment = new ExpensePayment
        {
            ExpenseDueId = expenseDueId,
            AmountPaid = amountPaid,
            PaidOn = paidOn,
            Notes = notes
        };

        return await CreateAsync(payment);
    }

    /// <summary>
    /// Gets all payments recorded against a specific ExpenseDue, oldest first.
    /// </summary>
    public async Task<List<ExpensePayment>> GetPaymentsForExpenseDueAsync(Guid expenseDueId)
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();

        return await _dbSet
            .Where(p => p.ExpenseDueId == expenseDueId && !p.IsDeleted && p.OrganizationId == orgId)
            .OrderBy(p => p.PaidOn)
            .ToListAsync();
    }
}
