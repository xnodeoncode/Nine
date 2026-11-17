using Nine.Core.Constants;
using Nine.Core.Entities;
using Nine.Core.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nine.Application.Services;

/// <summary>
/// Service for managing billable instances (ExpenseDue) of a PropertyExpense —
/// the record of what's actually owed and when, as distinct from the recurring
/// rule that produced it.
/// </summary>
public class ExpenseDueService : BaseService<ExpenseDue>
{
    public ExpenseDueService(
        ApplicationDbContext context,
        ILogger<ExpenseDueService> logger,
        IUserContextService userContext,
        IOptions<ApplicationSettings> settings)
        : base(context, logger, userContext, settings)
    {
    }

    protected override async Task ValidateEntityAsync(ExpenseDue entity)
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();

        var expenseExists = await _context.PropertyExpenses
            .AnyAsync(e => e.Id == entity.PropertyExpenseId && e.OrganizationId == orgId && !e.IsDeleted);

        if (!expenseExists)
        {
            throw new InvalidOperationException("Property expense not found or does not belong to your organization.");
        }

        if (entity.AmountDue < 0)
        {
            throw new InvalidOperationException("Amount due cannot be negative.");
        }

        await base.ValidateEntityAsync(entity);
    }

    /// <summary>
    /// Gets all ExpenseDue instances for a property, most recent first.
    /// </summary>
    /// <param name="includeHistory">
    /// When false, only the single most recent ExpenseDue per PropertyExpense is
    /// returned (the "current cycle" view). When true, full history is returned.
    /// </param>
    public async Task<List<ExpenseDue>> GetDueForPropertyAsync(Guid propertyId, bool includeHistory = false)
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();

        var dues = await _dbSet
            .Where(d => d.PropertyId == propertyId && !d.IsDeleted && d.OrganizationId == orgId)
            .Include(d => d.Payments)
            .OrderByDescending(d => d.DueDate)
            .ToListAsync();

        if (includeHistory)
        {
            return dues;
        }

        return dues
            .GroupBy(d => d.PropertyExpenseId)
            .Select(g => g.OrderByDescending(d => d.DueDate).First())
            .ToList();
    }

    /// <summary>
    /// Gets the most recent (current cycle) ExpenseDue for a specific
    /// PropertyExpense, or null if none have been generated/entered yet.
    /// </summary>
    public async Task<ExpenseDue?> GetCurrentCycleForPropertyExpenseAsync(Guid propertyExpenseId)
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();

        return await _dbSet
            .Where(d => d.PropertyExpenseId == propertyExpenseId && !d.IsDeleted && d.OrganizationId == orgId)
            .Include(d => d.Payments)
            .OrderByDescending(d => d.DueDate)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Archives an ExpenseDue along with its ExpensePayment history, mirroring
    /// InvoiceService's Invoice -> Payments archive cascade.
    /// </summary>
    public override async Task<bool> ArchiveAsync(Guid id)
    {
        var userId = await _userContext.GetUserIdAsync();
        var now = DateTime.UtcNow;

        var result = await base.ArchiveAsync(id);

        if (result)
        {
            await _context.Set<ExpensePayment>()
                .Where(p => p.ExpenseDueId == id && !p.IsDeleted && !p.IsArchived)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.IsArchived, true)
                    .SetProperty(p => p.ArchivedOn, now)
                    .SetProperty(p => p.ArchivedBy, userId));
        }

        return result;
    }

    /// <summary>
    /// Restores an archived ExpenseDue. Payments are not automatically restored,
    /// matching InvoiceService's Restore behavior.
    /// </summary>
    public override async Task<bool> RestoreAsync(Guid id)
    {
        return await base.RestoreAsync(id);
    }

    /// <summary>
    /// Waives/skips an ExpenseDue — a user decision, not a payment-derived
    /// outcome. A reason is required so the audit trail explains why this cycle
    /// was not paid.
    /// </summary>
    public async Task WaiveExpenseDueAsync(Guid id, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("A reason is required to waive an expense due.");
        }

        var orgId = await _userContext.GetActiveOrganizationIdAsync();
        var userId = await _userContext.GetUserIdAsync();

        var due = await _dbSet
            .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted && d.OrganizationId == orgId);

        if (due == null)
        {
            throw new InvalidOperationException("Expense due not found or does not belong to your organization.");
        }

        due.WaivedOn = DateTime.Today;
        due.WaivedReason = reason.Trim();
        due.LastModifiedOn = DateTime.UtcNow;
        due.LastModifiedBy = userId;

        _context.Update(due);
        await _context.SaveChangesAsync();
    }
}
