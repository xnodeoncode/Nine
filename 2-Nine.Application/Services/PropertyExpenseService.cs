using Nine.Core.Constants;
using Nine.Core.Entities;
using Nine.Core.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nine.Application.Services;

/// <summary>
/// Service for managing fixed/recurring property expenses (mortgage, insurance,
/// property tax, HOA, maintenance, utilities, and other custom costs).
/// </summary>
public class PropertyExpenseService : BaseService<PropertyExpense>
{
    private readonly ExpenseGenerationService _expenseGenerationService;

    public PropertyExpenseService(
        ApplicationDbContext context,
        ILogger<PropertyExpenseService> logger,
        IUserContextService userContext,
        IOptions<ApplicationSettings> settings,
        ExpenseGenerationService expenseGenerationService)
        : base(context, logger, userContext, settings)
    {
        _expenseGenerationService = expenseGenerationService;
    }

    /// <summary>
    /// Archives a PropertyExpense along with its ExpenseDue and ExpensePayment
    /// history, mirroring LeaseService's Lease -> Invoices -> Payments archive
    /// cascade. Used when an expense is archived independently of its Property
    /// (Property-level archive already cascades to PropertyExpense via
    /// PropertyService.ArchiveAsync; this handles the reverse direction so the
    /// full expense chain goes offline together either way).
    /// </summary>
    public override async Task<bool> ArchiveAsync(Guid id)
    {
        var userId = await _userContext.GetUserIdAsync();
        var now = DateTime.UtcNow;

        var result = await base.ArchiveAsync(id);

        if (result)
        {
            await _context.Set<ExpenseDue>()
                .Where(d => d.PropertyExpenseId == id && !d.IsDeleted && !d.IsArchived)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.IsArchived, true)
                    .SetProperty(d => d.ArchivedOn, now)
                    .SetProperty(d => d.ArchivedBy, userId));

            await _context.Set<ExpensePayment>()
                .Where(p => !p.IsDeleted && !p.IsArchived && p.ExpenseDue!.PropertyExpenseId == id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.IsArchived, true)
                    .SetProperty(p => p.ArchivedOn, now)
                    .SetProperty(p => p.ArchivedBy, userId));
        }

        return result;
    }

    /// <summary>
    /// Restores an archived PropertyExpense. ExpenseDue/ExpensePayment history is
    /// not automatically restored, matching InvoiceService's Restore behavior.
    /// </summary>
    public override async Task<bool> RestoreAsync(Guid id)
    {
        return await base.RestoreAsync(id);
    }

    /// <summary>
    /// Validates property expense business rules before create/update.
    /// </summary>
    protected override async Task ValidateEntityAsync(PropertyExpense entity)
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();

        // Validate PropertyId exists and belongs to active organization
        var propertyExists = await _context.Properties
            .AnyAsync(p => p.Id == entity.PropertyId && p.OrganizationId == orgId && !p.IsDeleted);

        if (!propertyExists)
        {
            throw new InvalidOperationException("Property not found or does not belong to your organization.");
        }

        // Validate ExpenseType is a known value
        if (!ApplicationConstants.PropertyExpenseTypes.AllPropertyExpenseTypes.Contains(entity.ExpenseType))
        {
            throw new InvalidOperationException($"'{entity.ExpenseType}' is not a valid expense type.");
        }

        // Validate the required RecurrencePattern
        if (entity.RecurrencePattern == null)
        {
            throw new InvalidOperationException("A recurrence pattern (including one-time) is required.");
        }

        if (!ApplicationConstants.ExpenseFrequencies.AllExpenseFrequencies.Contains(entity.RecurrencePattern.Frequency))
        {
            throw new InvalidOperationException($"'{entity.RecurrencePattern.Frequency}' is not a valid expense frequency.");
        }

        if (entity.RecurrencePattern.Frequency != ApplicationConstants.ExpenseFrequencies.OneTime)
        {
            if (!entity.RecurrencePattern.DueDayOfMonth.HasValue ||
                entity.RecurrencePattern.DueDayOfMonth < 1 ||
                entity.RecurrencePattern.DueDayOfMonth > 31)
            {
                throw new InvalidOperationException("A due day of month (1-31) is required for recurring expenses.");
            }

            if (entity.RecurrencePattern.Frequency != ApplicationConstants.ExpenseFrequencies.Monthly &&
                (!entity.RecurrencePattern.AnchorMonth.HasValue ||
                 entity.RecurrencePattern.AnchorMonth < 1 ||
                 entity.RecurrencePattern.AnchorMonth > 12))
            {
                throw new InvalidOperationException("An anchor month (1-12) is required for quarterly and annual expenses.");
            }
        }

        // Validate Amount is non-negative
        if (entity.Amount < 0)
        {
            throw new InvalidOperationException("Amount cannot be negative.");
        }

        // Validate EndDate is after EffectiveDate
        if (entity.EndDate.HasValue && entity.EndDate.Value < entity.EffectiveDate)
        {
            throw new InvalidOperationException("End date must be on or after the effective date.");
        }

        await base.ValidateEntityAsync(entity);
    }

    /// <summary>
    /// Creates a PropertyExpense along with its RecurrencePattern (derived from the
    /// user-selected due date) and backfills any elapsed ExpenseDue cycles. This is
    /// the primary entry point for adding an expense from the UI.
    ///
    /// For a OneTime expense, exactly one ExpenseDue is created at the exact
    /// selected date, past or present. For a recurring expense (Monthly/
    /// Quarterly/Annual), every cycle between the selected due date and today
    /// is backfilled immediately — clamped to no earlier than January 1 of the
    /// current year, so a user is never surprised by an auto-generated
    /// ExpenseDue landing in a prior (potentially already-reported) tax year.
    /// Any cycles before that boundary must be entered manually; the caller
    /// should surface <see cref="CreateExpenseResult.WasBackfillClamped"/> to
    /// the user when true.
    /// </summary>
    /// <param name="expense">
    /// The expense to create. <see cref="PropertyExpense.RecurrencePattern"/>.Frequency
    /// must already be set; DueDayOfMonth/AnchorMonth are derived here from
    /// <paramref name="dueDate"/> and do not need to be pre-populated by the caller.
    /// </param>
    /// <param name="dueDate">
    /// The due date explicitly selected by the user at entry time. Never inferred
    /// from EffectiveDate.
    /// </param>
    public async Task<CreateExpenseResult> CreateExpenseAsync(PropertyExpense expense, DateTime dueDate)
    {
        if (expense.EffectiveDate.Date == DateTime.Today && dueDate.Date != DateTime.Today)
        {
            expense.EffectiveDate = dueDate.Date;
        }

        expense.RecurrencePattern ??= new RecurrencePattern();
        var frequency = expense.RecurrencePattern.Frequency;

        if (frequency != ApplicationConstants.ExpenseFrequencies.OneTime)
        {
            expense.RecurrencePattern.DueDayOfMonth = dueDate.Day;
            expense.RecurrencePattern.AnchorMonth = frequency == ApplicationConstants.ExpenseFrequencies.Monthly
                ? null
                : dueDate.Month;
        }
        else
        {
            expense.RecurrencePattern.DueDayOfMonth = null;
            expense.RecurrencePattern.AnchorMonth = null;
        }

        var created = await CreateAsync(expense);

        if (frequency == ApplicationConstants.ExpenseFrequencies.OneTime)
        {
            var userId = await _userContext.GetUserIdAsync();
            var due = new ExpenseDue
            {
                Id = Guid.NewGuid(),
                OrganizationId = created.OrganizationId,
                PropertyExpenseId = created.Id,
                PropertyId = created.PropertyId,
                DueDate = dueDate,
                AmountDue = created.Amount,
                CreatedBy = string.IsNullOrEmpty(userId) ? ApplicationConstants.SystemUser.Id : userId,
                CreatedOn = DateTime.UtcNow
            };

            _context.Set<ExpenseDue>().Add(due);
            await _context.SaveChangesAsync();

            return new CreateExpenseResult(created, GeneratedDueCount: 1, WasBackfillClamped: false);
        }

        var (generatedCount, wasClamped) = await _expenseGenerationService.BackfillForNewExpenseAsync(
            created, dueDate, DateTime.Today);

        return new CreateExpenseResult(created, generatedCount, wasClamped);
    }

    /// <summary>
    /// Gets all property expenses for the active organization with Property navigation included.
    /// Overrides base method to include navigation properties.
    /// </summary>
    public override async Task<List<PropertyExpense>> GetAllAsync()
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();

        return await _dbSet
            .Where(e => !e.IsDeleted && !e.IsArchived && e.OrganizationId == orgId)
            .Include(e => e.Property)
            .OrderBy(e => e.Property!.Address)
            .ThenBy(e => e.ExpenseType)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all archived property expenses for the active organization with Property navigation included.
    /// </summary>
    public override async Task<List<PropertyExpense>> GetArchivedAsync()
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();

        return await _dbSet
            .Where(e => !e.IsDeleted && e.IsArchived && e.OrganizationId == orgId)
            .Include(e => e.Property)
            .OrderBy(e => e.Property!.Address)
            .ThenBy(e => e.ExpenseType)
            .ToListAsync();
    }

    /// <summary>
    /// Gets currently active expenses for a specific property (no EndDate set).
    /// </summary>
    public async Task<List<PropertyExpense>> GetExpensesByPropertyAsync(Guid propertyId)
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();

        return await _dbSet
            .Where(e => e.PropertyId == propertyId && !e.IsDeleted && !e.IsArchived && !e.EndDate.HasValue)
            .Include(e => e.Property)
            .Where(e => e.Property!.OrganizationId == orgId)
            .OrderBy(e => e.ExpenseType)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all expenses (including cancelled) for a property within a date range.
    /// Used for lease-period reporting: returns any expense whose effective span
    /// overlaps the given period.
    /// </summary>
    public async Task<List<PropertyExpense>> GetExpensesForLeasePeriodAsync(Guid propertyId, DateTime leaseStart, DateTime leaseEnd)
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();

        return await _dbSet
            .Where(e => e.PropertyId == propertyId && !e.IsDeleted)
            .Include(e => e.Property)
            .Where(e => e.Property!.OrganizationId == orgId)
            // Expense was active at some point during the lease period
            .Where(e => e.EffectiveDate <= leaseEnd &&
                        (!e.EndDate.HasValue || e.EndDate.Value >= leaseStart))
            .OrderBy(e => e.EffectiveDate)
            .ThenBy(e => e.ExpenseType)
            .ToListAsync();
    }

    /// <summary>
    /// Gets the total normalized monthly expense amount for a specific property,
    /// summed across all active (non-ended) expenses.
    /// </summary>
    public async Task<decimal> GetTotalMonthlyExpensesByPropertyAsync(Guid propertyId)
    {
        var expenses = await GetExpensesByPropertyAsync(propertyId);
        var today = DateTime.Today;

        return expenses
            .Where(e => !e.EndDate.HasValue || e.EndDate.Value >= today)
            .Sum(e => e.MonthlyAmount);
    }

    /// <summary>
    /// Gets total normalized monthly expenses grouped by property, for all properties
    /// in the active organization.
    /// </summary>
    public async Task<Dictionary<Guid, decimal>> GetTotalMonthlyExpensesByAllPropertiesAsync()
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();
        var today = DateTime.Today;

        var expenses = await _dbSet
            .Where(e => !e.IsDeleted && !e.IsArchived && e.OrganizationId == orgId)
            .Where(e => !e.EndDate.HasValue || e.EndDate.Value >= today)
            .ToListAsync();

        return expenses
            .GroupBy(e => e.PropertyId)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.MonthlyAmount));
    }

    /// <summary>
    /// Cancels an expense by setting EndDate to today and optionally recording a reason.
    /// The record is permanently retained for audit and lease-period reporting.
    /// </summary>
    public async Task CancelExpenseAsync(Guid id, string? reason = null)
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();
        var userId = await _userContext.GetUserIdAsync();

        var expense = await _dbSet
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted && e.OrganizationId == orgId);

        if (expense == null)
            throw new InvalidOperationException("Expense not found or does not belong to your organization.");

        expense.EndDate = DateTime.Today;
        expense.EndReason = string.IsNullOrWhiteSpace(reason)
            ? $"Cancelled by {userId}"
            : reason.Trim();
        expense.LastModifiedOn = DateTime.UtcNow;
        expense.LastModifiedBy = userId;

        _context.Update(expense);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Voids an expense entered in error. Deletes the record according to
    /// ApplicationSettings.SoftDeleteEnabled (hard delete when disabled, soft delete when enabled).
    /// </summary>
    public async Task VoidExpenseAsync(Guid id)
    {
        await DeleteAsync(id);
    }
}

/// <summary>
/// Result of PropertyExpenseService.CreateExpenseAsync — the created expense,
/// how many ExpenseDue cycles were backfilled, and whether the backfill was
/// clamped to January 1 of the current year (meaning the user selected a due
/// date before that boundary, and any cycles prior to it must be entered
/// manually). Callers should surface WasBackfillClamped to the user.
/// </summary>
public record CreateExpenseResult(PropertyExpense Expense, int GeneratedDueCount, bool WasBackfillClamped);
