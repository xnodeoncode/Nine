using Nine.Core.Constants;
using Nine.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Nine.Application.Services;

/// <summary>
/// Generates ExpenseDue instances for recurring PropertyExpenses (those whose
/// RecurrencePattern.Frequency is Monthly, Quarterly, or Annual).
///
/// Automatic generation never reaches further back than January 1 of the
/// current calendar year, regardless of the expense's EffectiveDate/selected
/// due date or how long the application has been closed. This keeps every
/// auto-generated ExpenseDue within the still-open current tax year; any
/// cycle that would fall in a prior year must be entered manually so a user
/// never has historical financial data silently altered after the fact.
///
/// This same walk-forward algorithm is used for two distinct triggers:
///  - At app startup/periodically (via ScheduledTaskService), to catch up any
///    cycles that elapsed while the application was closed, across all
///    organizations, for all existing recurring expenses.
///  - At the moment a new recurring PropertyExpense is created, to backfill
///    any cycles between its selected due date (or Jan 1, whichever is
///    later) and today, so the user immediately sees due history rather than
///    waiting for the next scheduled run.
///
/// OneTime expenses are never touched by this service — their single
/// ExpenseDue is created directly at entry time by
/// PropertyExpenseService.CreateExpenseAsync.
/// </summary>
public class ExpenseGenerationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ExpenseGenerationService> _logger;

    public ExpenseGenerationService(ApplicationDbContext context, ILogger<ExpenseGenerationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Returns January 1 of <paramref name="asOfDate"/>'s year — the earliest
    /// date automatic ExpenseDue generation is ever allowed to reach.
    /// </summary>
    public static DateTime GetEarliestAutoGenerationDate(DateTime asOfDate) => new(asOfDate.Year, 1, 1);

    /// <summary>
    /// Generates any missing ExpenseDue instances, across all organizations,
    /// for every active recurring PropertyExpense, walking forward from
    /// January 1 of <paramref name="asOfDate"/>'s year through
    /// <paramref name="asOfDate"/>. Idempotent — safe to re-run (e.g. on every
    /// app startup) without creating duplicates, and naturally catches up any
    /// cycles missed while the application was closed, bounded to the current
    /// calendar year.
    /// </summary>
    /// <returns>The number of ExpenseDue rows generated.</returns>
    public async Task<int> GenerateMissingDueAsync(DateTime asOfDate)
    {
        var earliestAllowed = GetEarliestAutoGenerationDate(asOfDate);

        var candidates = await _context.PropertyExpenses
            .Where(e => !e.IsDeleted)
            .Where(e => !e.EndDate.HasValue || e.EndDate.Value >= earliestAllowed)
            .Where(e => e.RecurrencePattern.Frequency != ApplicationConstants.ExpenseFrequencies.OneTime)
            .ToListAsync();

        var generatedCount = 0;

        foreach (var expense in candidates)
        {
            var expenseRangeStart = expense.EffectiveDate.Date > earliestAllowed.Date
                ? expense.EffectiveDate.Date
                : earliestAllowed.Date;
            var expenseRangeEnd = expense.EndDate.HasValue && expense.EndDate.Value.Date < asOfDate.Date
                ? expense.EndDate.Value.Date
                : asOfDate.Date;

            generatedCount += await GenerateMissingDueForExpenseAsync(expense, expenseRangeStart, expenseRangeEnd);
        }

        if (generatedCount > 0)
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation(
                "Generated {Count} expense due record(s) through {AsOfDate}",
                generatedCount,
                asOfDate.ToString("yyyy-MM-dd"));
        }

        return generatedCount;
    }

    /// <summary>
    /// Backfills any missing ExpenseDue cycles for a single, just-created
    /// recurring PropertyExpense, from its selected due date through today —
    /// clamped so nothing is generated before January 1 of the current year.
    /// Call this once, immediately after creating a recurring PropertyExpense
    /// and saving its RecurrencePattern.
    /// </summary>
    /// <param name="expense">The newly created expense (already saved, with RecurrencePattern set).</param>
    /// <param name="selectedDueDate">The due date the user explicitly selected at entry time.</param>
    /// <param name="asOfDate">Typically DateTime.Today.</param>
    /// <returns>
    /// The number of ExpenseDue rows generated, and whether the backfill was
    /// clamped (i.e. the selected due date was before January 1 of the
    /// current year, so some cycles were intentionally skipped and must be
    /// entered manually).
    /// </returns>
    public async Task<(int GeneratedCount, bool WasClamped)> BackfillForNewExpenseAsync(
        PropertyExpense expense,
        DateTime selectedDueDate,
        DateTime asOfDate)
    {
        var earliestAllowed = GetEarliestAutoGenerationDate(asOfDate);
        var wasClamped = selectedDueDate < earliestAllowed;
        var rangeStart = wasClamped ? earliestAllowed : selectedDueDate;

        var generatedCount = await GenerateMissingDueForExpenseAsync(expense, rangeStart, asOfDate);

        if (generatedCount > 0)
        {
            await _context.SaveChangesAsync();
        }

        return (generatedCount, wasClamped);
    }

    /// <summary>
    /// Adds (but does not save) any ExpenseDue rows missing for the given
    /// expense between rangeStart and rangeEnd, inclusive. Idempotent — skips
    /// any cycle date that already has an ExpenseDue.
    /// </summary>
    private async Task<int> GenerateMissingDueForExpenseAsync(PropertyExpense expense, DateTime rangeStart, DateTime rangeEnd)
    {
        var cycleDates = ComputeCycleDatesInRange(expense.RecurrencePattern, rangeStart, rangeEnd);
        if (cycleDates.Count == 0)
        {
            return 0;
        }

        var existingDates = await _context.Set<ExpenseDue>()
            .Where(d => d.PropertyExpenseId == expense.Id)
            .Select(d => d.DueDate.Date)
            .ToListAsync();
        var existingDateSet = existingDates.ToHashSet();

        var generatedCount = 0;

        foreach (var dueDate in cycleDates)
        {
            if (existingDateSet.Contains(dueDate.Date))
            {
                continue;
            }

            _context.Set<ExpenseDue>().Add(new ExpenseDue
            {
                Id = Guid.NewGuid(),
                OrganizationId = expense.OrganizationId,
                PropertyExpenseId = expense.Id,
                PropertyId = expense.PropertyId,
                DueDate = dueDate,
                AmountDue = expense.Amount,
                CreatedBy = ApplicationConstants.SystemUser.Id,
                CreatedOn = DateTime.UtcNow
            });

            generatedCount++;
        }

        return generatedCount;
    }

    /// <summary>
    /// Computes every cycle due date for the given pattern that falls within
    /// [rangeStart, rangeEnd], inclusive, by walking month-by-month and
    /// testing each month against the pattern's cadence.
    /// </summary>
    private static List<DateTime> ComputeCycleDatesInRange(RecurrencePattern pattern, DateTime rangeStart, DateTime rangeEnd)
    {
        var dates = new List<DateTime>();
        if (rangeStart.Date > rangeEnd.Date)
        {
            return dates;
        }

        var dayOfMonth = pattern.DueDayOfMonth ?? 1;
        var anchorMonth = pattern.AnchorMonth ?? 1;

        var cursor = new DateTime(rangeStart.Year, rangeStart.Month, 1);
        var endCursor = new DateTime(rangeEnd.Year, rangeEnd.Month, 1);

        while (cursor <= endCursor)
        {
            if (IsCadenceMonth(pattern.Frequency, cursor.Month, anchorMonth))
            {
                var dueDate = BuildDueDate(cursor.Year, cursor.Month, dayOfMonth);
                if (dueDate.Date >= rangeStart.Date && dueDate.Date <= rangeEnd.Date)
                {
                    dates.Add(dueDate);
                }
            }

            cursor = cursor.AddMonths(1);
        }

        return dates;
    }

    /// <summary>
    /// True if the given month matches the pattern's recurrence cadence:
    /// every month for Monthly, every third month anchored to anchorMonth for
    /// Quarterly (e.g. anchor month 2 -> Feb/May/Aug/Nov), or only
    /// anchorMonth itself for Annual.
    /// </summary>
    private static bool IsCadenceMonth(string frequency, int month, int anchorMonth) => frequency switch
    {
        ApplicationConstants.ExpenseFrequencies.Monthly => true,
        ApplicationConstants.ExpenseFrequencies.Quarterly => ((month - anchorMonth) % 3 + 3) % 3 == 0,
        ApplicationConstants.ExpenseFrequencies.Annual => month == anchorMonth,
        _ => false
    };

    /// <summary>
    /// Builds a due date for the given year/month, clamping the day to the
    /// last valid day of that month (e.g. day 31 in April becomes April 30).
    /// </summary>
    private static DateTime BuildDueDate(int year, int month, int day)
    {
        var lastValidDay = DateTime.DaysInMonth(year, month);
        return new DateTime(year, month, Math.Min(day, lastValidDay));
    }
}
