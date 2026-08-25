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
    /// Unit tests for ExpensePaymentService: multiple payments per due, partial
    /// payment, overpayment, and validation failures.
    /// </summary>
    public class ExpensePaymentServiceTests : IDisposable
    {
        private readonly TestApplicationDbContext _context;
        private readonly Mock<IUserContextService> _mockUserContext;
        private readonly ExpensePaymentService _service;
        private readonly string _testUserId;
        private readonly Guid _testOrgId;
        private readonly Guid _testExpenseDueId;
        private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;

        public ExpensePaymentServiceTests()
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
            var propertyId = Guid.NewGuid();
            var expenseId = Guid.NewGuid();
            _testExpenseDueId = Guid.NewGuid();

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
                Id = propertyId,
                OrganizationId = _testOrgId,
                Address = "123 Main St",
                PropertyType = "House",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            });

            _context.PropertyExpenses.Add(new PropertyExpense
            {
                Id = expenseId,
                OrganizationId = _testOrgId,
                PropertyId = propertyId,
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

            _context.Set<ExpenseDue>().Add(new ExpenseDue
            {
                Id = _testExpenseDueId,
                OrganizationId = _testOrgId,
                PropertyExpenseId = expenseId,
                PropertyId = propertyId,
                DueDate = new DateTime(2026, 3, 1),
                AmountDue = 1000m,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            });

            _context.SaveChanges();

            var mockSettings = Options.Create(new ApplicationSettings { SoftDeleteEnabled = true });
            var mockLogger = new Mock<ILogger<ExpensePaymentService>>();

            _service = new ExpensePaymentService(_context, mockLogger.Object, _mockUserContext.Object, mockSettings);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _connection?.Dispose();
        }

        [Fact]
        public async Task RecordPaymentAsync_MultiplePayments_AllPersistedIndependently()
        {
            await _service.RecordPaymentAsync(_testExpenseDueId, 500m, new DateTime(2026, 3, 1), "First half");
            await _service.RecordPaymentAsync(_testExpenseDueId, 500m, new DateTime(2026, 3, 15), "Second half");

            var payments = await _service.GetPaymentsForExpenseDueAsync(_testExpenseDueId);
            Assert.Equal(2, payments.Count);
            Assert.Equal(1000m, payments.Sum(p => p.AmountPaid));
        }

        [Fact]
        public async Task RecordPaymentAsync_PartialPayment_ReflectedInDueBalance()
        {
            await _service.RecordPaymentAsync(_testExpenseDueId, 400m, DateTime.Today);

            var due = await _context.Set<ExpenseDue>()
                .Include(d => d.Payments)
                .SingleAsync(d => d.Id == _testExpenseDueId);

            Assert.Equal(400m, due.AmountPaid);
            Assert.Equal(600m, due.BalanceDue);
            Assert.Equal("PartiallyPaid", due.Status);
        }

        [Fact]
        public async Task RecordPaymentAsync_Overpayment_ReflectsOverpaidStatus()
        {
            await _service.RecordPaymentAsync(_testExpenseDueId, 1200m, DateTime.Today);

            var due = await _context.Set<ExpenseDue>()
                .Include(d => d.Payments)
                .SingleAsync(d => d.Id == _testExpenseDueId);

            Assert.Equal(200m, due.BalanceDue * -1);
            Assert.Equal("Overpaid", due.Status);
        }

        [Fact]
        public async Task RecordPaymentAsync_ZeroAmount_ThrowsValidationException()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.RecordPaymentAsync(_testExpenseDueId, 0m, DateTime.Today));
        }

        [Fact]
        public async Task RecordPaymentAsync_NegativeAmount_ThrowsValidationException()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.RecordPaymentAsync(_testExpenseDueId, -50m, DateTime.Today));
        }

        [Fact]
        public async Task RecordPaymentAsync_UnknownExpenseDue_ThrowsValidationException()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.RecordPaymentAsync(Guid.NewGuid(), 100m, DateTime.Today));
        }
    }
}
