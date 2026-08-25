using Nine.Application.Services;
using Nine.Core.Constants;
using Nine.Core.Entities;
using Nine.Core.Interfaces.Services;
using Nine.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Nine.Application.Tests
{
    /// <summary>
    /// Unit tests for PropertyExpenseService covering the new
    /// Frequency/RecurrencePattern-aware create flow, including immediate
    /// ExpenseDue creation for both OneTime and recurring expenses.
    /// </summary>
    public class PropertyExpenseServiceTests : IDisposable
    {
        private readonly TestApplicationDbContext _context;
        private readonly Mock<IUserContextService> _mockUserContext;
        private readonly PropertyExpenseService _service;
        private readonly string _testUserId;
        private readonly Guid _testOrgId;
        private readonly Guid _testPropertyId;
        private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;

        public PropertyExpenseServiceTests()
        {
            _connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new TestApplicationDbContext(options);
            _context.Database.EnsureCreated();

            _testUserId = "test-user-123";
            _testOrgId = Guid.NewGuid();
            _testPropertyId = Guid.NewGuid();

            _mockUserContext = new Mock<IUserContextService>();
            _mockUserContext.Setup(x => x.GetUserIdAsync()).ReturnsAsync(_testUserId);
            _mockUserContext.Setup(x => x.GetActiveOrganizationIdAsync()).ReturnsAsync(_testOrgId);

            _context.Organizations.Add(new Organization
            {
                Id = _testOrgId,
                Name = "Test Org",
                OwnerId = _testUserId,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            });

            var property = new Property
            {
                Id = _testPropertyId,
                OrganizationId = _testOrgId,
                Address = "123 Main St",
                PropertyType = "House",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            _context.Properties.Add(property);
            _context.SaveChanges();

            var mockSettings = Options.Create(new ApplicationSettings { SoftDeleteEnabled = true });
            var mockLogger = new Mock<ILogger<PropertyExpenseService>>();
            var mockGenerationLogger = new Mock<ILogger<ExpenseGenerationService>>();
            var expenseGenerationService = new ExpenseGenerationService(_context, mockGenerationLogger.Object);

            _service = new PropertyExpenseService(
                _context,
                mockLogger.Object,
                _mockUserContext.Object,
                mockSettings,
                expenseGenerationService);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _connection?.Dispose();
        }

        [Fact]
        public async Task CreateExpenseAsync_OneTime_CreatesSingleExpenseDueImmediately()
        {
            var dueDate = new DateTime(2026, 3, 15);
            var expense = new PropertyExpense
            {
                PropertyId = _testPropertyId,
                ExpenseType = ApplicationConstants.PropertyExpenseTypes.Maintenance,
                Amount = 150m,
                RecurrencePattern = new RecurrencePattern { Frequency = ApplicationConstants.ExpenseFrequencies.OneTime }
            };

            var result = await _service.CreateExpenseAsync(expense, dueDate);
            var created = result.Expense;

            Assert.Equal(ApplicationConstants.ExpenseFrequencies.OneTime, created.RecurrencePattern.Frequency);
            Assert.Null(created.RecurrencePattern.DueDayOfMonth);
            Assert.Null(created.RecurrencePattern.AnchorMonth);

            var dues = await _context.Set<ExpenseDue>().Where(d => d.PropertyExpenseId == created.Id).ToListAsync();
            Assert.Single(dues);
            Assert.Equal(dueDate, dues[0].DueDate);
            Assert.Equal(150m, dues[0].AmountDue);
        }

        [Fact]
        public async Task CreateExpenseAsync_Monthly_DerivesDueDayOfMonthAndCreatesFirstCycle()
        {
            var dueDate = DateTime.Today;
            var expense = new PropertyExpense
            {
                PropertyId = _testPropertyId,
                ExpenseType = ApplicationConstants.PropertyExpenseTypes.Mortgage,
                Amount = 1200m,
                RecurrencePattern = new RecurrencePattern { Frequency = ApplicationConstants.ExpenseFrequencies.Monthly }
            };

            var result = await _service.CreateExpenseAsync(expense, dueDate);
            var created = result.Expense;

            Assert.Equal(dueDate.Day, created.RecurrencePattern.DueDayOfMonth);
            Assert.Null(created.RecurrencePattern.AnchorMonth);

            var dues = await _context.Set<ExpenseDue>().Where(d => d.PropertyExpenseId == created.Id).ToListAsync();
            Assert.Single(dues);
            Assert.Equal(dueDate, dues[0].DueDate);
        }

        [Fact]
        public async Task CreateExpenseAsync_Quarterly_DerivesDueDayAndAnchorMonth()
        {
            // Mirrors the "quarterly due on the 22nd" scenario discussed in planning.
            var dueDate = new DateTime(2026, 2, 22);
            var expense = new PropertyExpense
            {
                PropertyId = _testPropertyId,
                ExpenseType = ApplicationConstants.PropertyExpenseTypes.HOA,
                Amount = 300m,
                RecurrencePattern = new RecurrencePattern { Frequency = ApplicationConstants.ExpenseFrequencies.Quarterly }
            };

            var result = await _service.CreateExpenseAsync(expense, dueDate);
            var created = result.Expense;

            Assert.Equal(22, created.RecurrencePattern.DueDayOfMonth);
            Assert.Equal(2, created.RecurrencePattern.AnchorMonth);
        }

        [Fact]
        public async Task CreateExpenseAsync_InvalidExpenseType_ThrowsValidationException()
        {
            var expense = new PropertyExpense
            {
                PropertyId = _testPropertyId,
                ExpenseType = "NotARealType",
                Amount = 100m,
                RecurrencePattern = new RecurrencePattern { Frequency = ApplicationConstants.ExpenseFrequencies.Monthly }
            };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.CreateExpenseAsync(expense, DateTime.Today));
        }

        [Fact]
        public async Task CreateExpenseAsync_NegativeAmount_ThrowsValidationException()
        {
            var expense = new PropertyExpense
            {
                PropertyId = _testPropertyId,
                ExpenseType = ApplicationConstants.PropertyExpenseTypes.Other,
                Amount = -10m,
                RecurrencePattern = new RecurrencePattern { Frequency = ApplicationConstants.ExpenseFrequencies.OneTime }
            };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.CreateExpenseAsync(expense, DateTime.Today));
        }

        [Fact]
        public async Task VoidExpenseAsync_HardDeletesExpenseAndCascadesToExpenseDueAndExpensePayment()
        {
            // Regression test: PropertyExpense -> ExpenseDue was originally
            // configured with DeleteBehavior.Restrict, which threw
            // "the relationship is either marked as required or is implicitly
            // required... configure the relationship to use cascade deletes"
            // whenever VoidExpenseAsync (a genuine hard delete, for expenses
            // entered in error) was called on an expense that already had
            // ExpenseDue/ExpensePayment history. Fixed by making the FK cascade.
            var expense = new PropertyExpense
            {
                PropertyId = _testPropertyId,
                ExpenseType = ApplicationConstants.PropertyExpenseTypes.Mortgage,
                Amount = 500m,
                RecurrencePattern = new RecurrencePattern { Frequency = ApplicationConstants.ExpenseFrequencies.Monthly }
            };
            var result = await _service.CreateExpenseAsync(expense, new DateTime(2026, 3, 1));
            var created = result.Expense;

            var due = await _context.Set<ExpenseDue>().FirstAsync(d => d.PropertyExpenseId == created.Id);
            var payment = new ExpensePayment
            {
                Id = Guid.NewGuid(),
                OrganizationId = _testOrgId,
                ExpenseDueId = due.Id,
                AmountPaid = 500m,
                PaidOn = DateTime.Today,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            _context.Set<ExpensePayment>().Add(payment);
            await _context.SaveChangesAsync();

            var exception = await Record.ExceptionAsync(() => _service.VoidExpenseAsync(created.Id));

            Assert.Null(exception);
            Assert.Null(await _context.PropertyExpenses.FindAsync(created.Id));
            Assert.Empty(await _context.Set<ExpenseDue>().Where(d => d.PropertyExpenseId == created.Id).ToListAsync());
            Assert.Empty(await _context.Set<ExpensePayment>().Where(p => p.Id == payment.Id).ToListAsync());
        }

        [Fact]
        public async Task ArchiveAsync_CascadesToExpenseDueAndExpensePayment()
        {
            var expense = new PropertyExpense
            {
                PropertyId = _testPropertyId,
                ExpenseType = ApplicationConstants.PropertyExpenseTypes.Insurance,
                Amount = 200m,
                RecurrencePattern = new RecurrencePattern { Frequency = ApplicationConstants.ExpenseFrequencies.Annual }
            };
            var result = await _service.CreateExpenseAsync(expense, new DateTime(2026, 6, 1));
            var created = result.Expense;
            var due = await _context.Set<ExpenseDue>().SingleAsync(d => d.PropertyExpenseId == created.Id);

            _context.Set<ExpensePayment>().Add(new ExpensePayment
            {
                Id = Guid.NewGuid(),
                OrganizationId = _testOrgId,
                ExpenseDueId = due.Id,
                AmountPaid = 200m,
                PaidOn = DateTime.Today,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            var archiveResult = await _service.ArchiveAsync(created.Id);

            Assert.True(archiveResult);
            var archivedExpense = await _context.PropertyExpenses.FindAsync(created.Id);
            Assert.True(archivedExpense!.IsArchived);

            // AsNoTracking + ExecuteUpdateAsync bypasses the change tracker, so
            // force a fresh read rather than returning a stale tracked instance.
            var archivedDue = await _context.Set<ExpenseDue>().AsNoTracking().SingleAsync(d => d.Id == due.Id);
            Assert.True(archivedDue.IsArchived);

            var archivedPayment = await _context.Set<ExpensePayment>().AsNoTracking().SingleAsync(p => p.ExpenseDueId == due.Id);
            Assert.True(archivedPayment.IsArchived);
        }
    }
}
