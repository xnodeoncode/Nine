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
    /// Unit tests for ExpenseDueService: current-cycle lookup and waive behavior.
    /// </summary>
    public class ExpenseDueServiceTests : IDisposable
    {
        private readonly TestApplicationDbContext _context;
        private readonly Mock<IUserContextService> _mockUserContext;
        private readonly ExpenseDueService _service;
        private readonly string _testUserId;
        private readonly Guid _testOrgId;
        private readonly Guid _testPropertyId;
        private readonly Guid _testExpenseId;
        private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;

        public ExpenseDueServiceTests()
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
            _testExpenseId = Guid.NewGuid();

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

            _context.Properties.Add(new Property
            {
                Id = _testPropertyId,
                OrganizationId = _testOrgId,
                Address = "123 Main St",
                PropertyType = "House",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            });

            _context.PropertyExpenses.Add(new PropertyExpense
            {
                Id = _testExpenseId,
                OrganizationId = _testOrgId,
                PropertyId = _testPropertyId,
                ExpenseType = ApplicationConstants.PropertyExpenseTypes.Mortgage,
                Amount = 1000m,
                RecurrencePattern = new RecurrencePattern
                {
                    Frequency = ApplicationConstants.ExpenseFrequencies.Monthly,
                    DueDayOfMonth = 1
                },
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            });

            // Two cycles: January (older) and February (most recent).
            _context.Set<ExpenseDue>().Add(new ExpenseDue
            {
                Id = Guid.NewGuid(),
                OrganizationId = _testOrgId,
                PropertyExpenseId = _testExpenseId,
                PropertyId = _testPropertyId,
                DueDate = new DateTime(2026, 1, 1),
                AmountDue = 1000m,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            });
            _context.Set<ExpenseDue>().Add(new ExpenseDue
            {
                Id = Guid.NewGuid(),
                OrganizationId = _testOrgId,
                PropertyExpenseId = _testExpenseId,
                PropertyId = _testPropertyId,
                DueDate = new DateTime(2026, 2, 1),
                AmountDue = 1000m,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            });

            _context.SaveChanges();

            var mockSettings = Options.Create(new ApplicationSettings { SoftDeleteEnabled = true });
            var mockLogger = new Mock<ILogger<ExpenseDueService>>();

            _service = new ExpenseDueService(_context, mockLogger.Object, _mockUserContext.Object, mockSettings);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _connection?.Dispose();
        }

        [Fact]
        public async Task GetCurrentCycleForPropertyExpenseAsync_ReturnsMostRecentDue()
        {
            var current = await _service.GetCurrentCycleForPropertyExpenseAsync(_testExpenseId);

            Assert.NotNull(current);
            Assert.Equal(new DateTime(2026, 2, 1), current!.DueDate);
        }

        [Fact]
        public async Task WaiveExpenseDueAsync_SetsWaivedFieldsAndStatus()
        {
            var current = await _service.GetCurrentCycleForPropertyExpenseAsync(_testExpenseId);

            await _service.WaiveExpenseDueAsync(current!.Id, "Landlord covered this cycle");

            var updated = await _context.Set<ExpenseDue>().SingleAsync(d => d.Id == current.Id);
            Assert.NotNull(updated.WaivedOn);
            Assert.Equal("Landlord covered this cycle", updated.WaivedReason);
            Assert.Equal("Waived", updated.Status);
        }

        [Fact]
        public async Task WaiveExpenseDueAsync_EmptyReason_ThrowsValidationException()
        {
            var current = await _service.GetCurrentCycleForPropertyExpenseAsync(_testExpenseId);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.WaiveExpenseDueAsync(current!.Id, ""));
        }

        [Fact]
        public async Task GetDueForPropertyAsync_WithoutHistory_ReturnsOnlyMostRecentPerExpense()
        {
            var dues = await _service.GetDueForPropertyAsync(_testPropertyId, includeHistory: false);

            Assert.Single(dues);
            Assert.Equal(new DateTime(2026, 2, 1), dues[0].DueDate);
        }

        [Fact]
        public async Task GetDueForPropertyAsync_WithHistory_ReturnsAllCycles()
        {
            var dues = await _service.GetDueForPropertyAsync(_testPropertyId, includeHistory: true);

            Assert.Equal(2, dues.Count);
        }
    }
}
