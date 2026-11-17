using Nine.Application.Services;
using Nine.Core.Constants;
using Nine.Core.Entities;
using Nine.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Nine.Application.Tests
{
    /// <summary>
    /// Unit tests for ExpenseGenerationService: idempotent current-tax-year
    /// catch-up, frequency cadence logic, day-of-month clamping for short
    /// months, and exclusion of OneTime / already-ended expenses.
    /// </summary>
    public class ExpenseGenerationServiceTests : IDisposable
    {
        private readonly TestApplicationDbContext _context;
        private readonly ExpenseGenerationService _service;
        private readonly Guid _testOrgId;
        private readonly Guid _testPropertyId;
        private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;

        public ExpenseGenerationServiceTests()
        {
            _connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new TestApplicationDbContext(options);
            _context.Database.EnsureCreated();

            _testOrgId = Guid.NewGuid();
            _testPropertyId = Guid.NewGuid();

            _context.Organizations.Add(new Organization
            {
                Id = _testOrgId,
                Name = "Test Org",
                OwnerId = "system",
                CreatedBy = "system",
                CreatedOn = DateTime.UtcNow
            });

            _context.Properties.Add(new Property
            {
                Id = _testPropertyId,
                OrganizationId = _testOrgId,
                Address = "123 Main St",
                PropertyType = "House",
                CreatedBy = "system",
                CreatedOn = DateTime.UtcNow
            });
            _context.SaveChanges();

            var mockLogger = new Mock<ILogger<ExpenseGenerationService>>();
            _service = new ExpenseGenerationService(_context, mockLogger.Object);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _connection?.Dispose();
        }

        private PropertyExpense AddExpense(
            string frequency,
            int? dueDayOfMonth = null,
            int? anchorMonth = null,
            DateTime? effectiveDate = null,
            DateTime? endDate = null,
            decimal amount = 100m,
            string expenseType = ApplicationConstants.PropertyExpenseTypes.Other)
        {
            var expense = new PropertyExpense
            {
                Id = Guid.NewGuid(),
                OrganizationId = _testOrgId,
                PropertyId = _testPropertyId,
                ExpenseType = expenseType,
                Amount = amount,
                EffectiveDate = effectiveDate ?? new DateTime(2025, 1, 1),
                EndDate = endDate,
                RecurrencePattern = new RecurrencePattern
                {
                    Frequency = frequency,
                    DueDayOfMonth = dueDayOfMonth,
                    AnchorMonth = anchorMonth
                },
                CreatedBy = "system",
                CreatedOn = DateTime.UtcNow
            };

            _context.PropertyExpenses.Add(expense);
            _context.SaveChanges();
            return expense;
        }

        [Fact]
        public async Task GenerateMissingDueAsync_Monthly_CatchesUpFromJanuaryThroughAsOfDate()
        {
            AddExpense(ApplicationConstants.ExpenseFrequencies.Monthly, dueDayOfMonth: 1);

            var count = await _service.GenerateMissingDueAsync(new DateTime(2026, 3, 15));

            Assert.Equal(3, count);
            var dues = await _context.Set<ExpenseDue>().OrderBy(d => d.DueDate).ToListAsync();
            Assert.Equal(
                new[] { new DateTime(2026, 1, 1), new DateTime(2026, 2, 1), new DateTime(2026, 3, 1) },
                dues.Select(d => d.DueDate).ToArray());
        }

        [Fact]
        public async Task GenerateMissingDueAsync_IsIdempotent_NoDuplicateOnReRun()
        {
            AddExpense(ApplicationConstants.ExpenseFrequencies.Monthly, dueDayOfMonth: 1);

            var firstRun = await _service.GenerateMissingDueAsync(new DateTime(2026, 3, 15));
            var secondRun = await _service.GenerateMissingDueAsync(new DateTime(2026, 3, 20));

            Assert.Equal(3, firstRun);
            Assert.Equal(0, secondRun);
            Assert.Equal(3, await _context.Set<ExpenseDue>().CountAsync());
        }

        [Fact]
        public async Task GenerateMissingDueAsync_MonthlyDayClamping_ShortMonthUsesLastValidDay()
        {
            AddExpense(ApplicationConstants.ExpenseFrequencies.Monthly, dueDayOfMonth: 31);

            await _service.GenerateMissingDueAsync(new DateTime(2026, 4, 30));

            var dueDates = await _context.Set<ExpenseDue>()
                .OrderBy(d => d.DueDate)
                .Select(d => d.DueDate)
                .ToListAsync();

            Assert.Contains(new DateTime(2026, 2, 28), dueDates);
            Assert.Contains(new DateTime(2026, 4, 30), dueDates);
        }

        [Fact]
        public async Task GenerateMissingDueAsync_Quarterly_CatchesUpElapsedCadenceMonths()
        {
            // Lawn service example: Jan 1 quarterly, checked in August -> Jan/Apr/Jul.
            AddExpense(ApplicationConstants.ExpenseFrequencies.Quarterly, dueDayOfMonth: 1, anchorMonth: 1);

            var count = await _service.GenerateMissingDueAsync(new DateTime(2026, 8, 25));

            Assert.Equal(3, count);
            var dues = await _context.Set<ExpenseDue>().OrderBy(d => d.DueDate).ToListAsync();
            Assert.Equal(
                new[] { new DateTime(2026, 1, 1), new DateTime(2026, 4, 1), new DateTime(2026, 7, 1) },
                dues.Select(d => d.DueDate).ToArray());
        }

        [Fact]
        public async Task BackfillForNewExpenseAsync_ClampsToJanuaryFirstWhenSelectedDueDateIsPriorTaxYear()
        {
            var expense = AddExpense(
                ApplicationConstants.ExpenseFrequencies.Monthly,
                dueDayOfMonth: 1,
                anchorMonth: null,
                effectiveDate: new DateTime(2023, 1, 1));

            var result = await _service.BackfillForNewExpenseAsync(
                expense,
                selectedDueDate: new DateTime(2023, 1, 1),
                asOfDate: new DateTime(2026, 3, 20));

            Assert.Equal(3, result.GeneratedCount);
            Assert.True(result.WasClamped);

            var dues = await _context.Set<ExpenseDue>().OrderBy(d => d.DueDate).ToListAsync();
            Assert.Equal(
                new[] { new DateTime(2026, 1, 1), new DateTime(2026, 2, 1), new DateTime(2026, 3, 1) },
                dues.Select(d => d.DueDate).ToArray());
        }

        [Fact]
        public async Task GenerateMissingDueAsync_Annual_OnlyGeneratesInAnchorMonth()
        {
            AddExpense(ApplicationConstants.ExpenseFrequencies.Annual, dueDayOfMonth: 1, anchorMonth: 6);

            var beforeAnchor = await _service.GenerateMissingDueAsync(new DateTime(2026, 3, 1));
            var afterAnchor = await _service.GenerateMissingDueAsync(new DateTime(2026, 6, 1));

            Assert.Equal(0, beforeAnchor);
            Assert.Equal(1, afterAnchor);
        }

        [Fact]
        public async Task GenerateMissingDueAsync_OneTime_NeverGeneratesAnything()
        {
            AddExpense(ApplicationConstants.ExpenseFrequencies.OneTime);

            var count = await _service.GenerateMissingDueAsync(new DateTime(2026, 3, 1));

            Assert.Equal(0, count);
            Assert.Empty(await _context.Set<ExpenseDue>().ToListAsync());
        }

        [Fact]
        public async Task GenerateMissingDueAsync_CancelledExpense_StopsGeneratingAfterEndDate()
        {
            AddExpense(
                ApplicationConstants.ExpenseFrequencies.Monthly,
                dueDayOfMonth: 1,
                endDate: new DateTime(2026, 2, 15));

            var count = await _service.GenerateMissingDueAsync(new DateTime(2026, 3, 1));

            Assert.Equal(2, count);
            var dues = await _context.Set<ExpenseDue>().OrderBy(d => d.DueDate).ToListAsync();
            Assert.Equal(
                new[] { new DateTime(2026, 1, 1), new DateTime(2026, 2, 1) },
                dues.Select(d => d.DueDate).ToArray());
        }
    }
}
