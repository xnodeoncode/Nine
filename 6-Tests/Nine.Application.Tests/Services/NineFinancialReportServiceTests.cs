using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Nine.Application.Services;
using Nine.Core.Constants;
using Nine.Core.Entities;
using Nine.Infrastructure.Data;

namespace Nine.Application.Tests.Services
{
    /// <summary>
    /// Unit tests for NineFinancialReportService covering actual PropertyExpense
    /// category sums flowing into IncomeStatement, PropertyPerformance, and
    /// TaxReportData (the Property Expense Reporting Part 2 feature).
    /// </summary>
    public class NineFinancialReportServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<ApplicationDbContext> _options;
        private readonly NineFinancialReportService _service;
        private readonly string _testUserId = "test-user-123";
        private readonly Guid _testOrgId = Guid.NewGuid();
        private readonly Guid _testPropertyId = Guid.NewGuid();
        private readonly DateTime _periodStart = new(2026, 1, 1);
        private readonly DateTime _periodEnd = new(2026, 1, 31);

        /// <summary>
        /// Returns a fresh context per call against the same in-memory SQLite
        /// connection, matching how NineFinancialReportService consumes
        /// IDbContextFactory in production.
        /// </summary>
        private class TestDbContextFactory : IDbContextFactory<ApplicationDbContext>
        {
            private readonly DbContextOptions<ApplicationDbContext> _options;
            public TestDbContextFactory(DbContextOptions<ApplicationDbContext> options) => _options = options;
            public ApplicationDbContext CreateDbContext() => new TestApplicationDbContext(_options);
            public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult<ApplicationDbContext>(new TestApplicationDbContext(_options));
        }

        public NineFinancialReportServiceTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var seedContext = new TestApplicationDbContext(_options);
            seedContext.Database.EnsureCreated();
            SeedData(seedContext);

            _service = new NineFinancialReportService(new TestDbContextFactory(_options));
        }

        public void Dispose() => _connection.Dispose();

        private void SeedData(TestApplicationDbContext context)
        {
            context.Organizations.Add(new Organization
            {
                Id = _testOrgId,
                Name = "Test Org",
                OwnerId = _testUserId,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            });

            context.Properties.Add(new Property
            {
                Id = _testPropertyId,
                OrganizationId = _testOrgId,
                Address = "123 Main St",
                PropertyType = "House",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            });

            var tenantId = Guid.NewGuid();
            context.Tenants.Add(new Tenant
            {
                Id = tenantId,
                OrganizationId = _testOrgId,
                FirstName = "Test",
                LastName = "Tenant",
                Email = "tenant@test.com",
                IdentificationNumber = "SSN123456",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            });

            var leaseId = Guid.NewGuid();
            context.Leases.Add(new Lease
            {
                Id = leaseId,
                OrganizationId = _testOrgId,
                PropertyId = _testPropertyId,
                TenantId = tenantId,
                StartDate = new DateTime(2025, 1, 1),
                EndDate = new DateTime(2026, 12, 31),
                MonthlyRent = 1500,
                SecurityDeposit = 1500,
                Status = "Active",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            });

            var invoiceId = Guid.NewGuid();
            context.Invoices.Add(new Invoice
            {
                Id = invoiceId,
                OrganizationId = _testOrgId,
                LeaseId = leaseId,
                InvoiceNumber = "INV-TEST-001",
                InvoicedOn = _periodStart,
                DueOn = _periodStart.AddDays(5),
                Amount = 1500,
                Description = "January Rent",
                Status = "Paid",
                AmountPaid = 1500,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            });

            context.Payments.Add(new Payment
            {
                OrganizationId = _testOrgId,
                InvoiceId = invoiceId,
                Amount = 1500,
                PaidOn = new DateTime(2026, 1, 1),
                PaymentMethod = "Check",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            });

            // Ad hoc repair - separate bucket from recurring "Maintenance" PropertyExpense
            context.Repairs.Add(new Repair
            {
                Id = Guid.NewGuid(),
                OrganizationId = _testOrgId,
                PropertyId = _testPropertyId,
                Description = "Fix leaky faucet",
                RepairType = "Plumbing",
                CompletedOn = new DateTime(2026, 1, 10),
                Cost = 200m,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            });

            AddFixedExpense(context, ApplicationConstants.PropertyExpenseTypes.Mortgage, 1000m);
            AddFixedExpense(context, ApplicationConstants.PropertyExpenseTypes.Insurance, 100m);
            AddFixedExpense(context, ApplicationConstants.PropertyExpenseTypes.PropertyTax, 150m);
            AddFixedExpense(context, ApplicationConstants.PropertyExpenseTypes.Utilities, 75m);
            AddFixedExpense(context, ApplicationConstants.PropertyExpenseTypes.HOA, 50m);
            AddFixedExpense(context, ApplicationConstants.PropertyExpenseTypes.Other, 25m);
            AddFixedExpense(context, ApplicationConstants.PropertyExpenseTypes.Maintenance, 60m);

            context.SaveChanges();
        }

        /// <summary>
        /// Seeds a PropertyExpense + ExpenseDue + ExpensePayment chain (the actual
        /// cash-out path the reports read from, not the budgeted Amount).
        /// </summary>
        private void AddFixedExpense(TestApplicationDbContext context, string expenseType, decimal amountPaid)
        {
            var expenseId = Guid.NewGuid();
            context.PropertyExpenses.Add(new PropertyExpense
            {
                Id = expenseId,
                OrganizationId = _testOrgId,
                PropertyId = _testPropertyId,
                ExpenseType = expenseType,
                Amount = amountPaid,
                RecurrencePattern = new RecurrencePattern
                {
                    Frequency = ApplicationConstants.ExpenseFrequencies.Monthly,
                    DueDayOfMonth = 1
                },
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            });

            var dueId = Guid.NewGuid();
            context.Set<ExpenseDue>().Add(new ExpenseDue
            {
                Id = dueId,
                OrganizationId = _testOrgId,
                PropertyExpenseId = expenseId,
                PropertyId = _testPropertyId,
                DueDate = new DateTime(2026, 1, 1),
                AmountDue = amountPaid,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            });

            context.Set<ExpensePayment>().Add(new ExpensePayment
            {
                Id = Guid.NewGuid(),
                OrganizationId = _testOrgId,
                ExpenseDueId = dueId,
                AmountPaid = amountPaid,
                PaidOn = new DateTime(2026, 1, 5),
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            });
        }

        [Fact]
        public async Task GenerateIncomeStatementAsync_PopulatesAllExpenseCategoriesFromActualData()
        {
            var statement = await _service.GenerateIncomeStatementAsync(_testOrgId, _periodStart, _periodEnd);

            Assert.Equal(1500m, statement.TotalRentIncome);
            Assert.Equal(1000m, statement.MortgageExpenses);
            Assert.Equal(100m, statement.InsuranceExpenses);
            Assert.Equal(150m, statement.TaxExpenses);
            Assert.Equal(75m, statement.UtilityExpenses);
            Assert.Equal(75m, statement.OtherExpenses); // HOA (50) + Other (25)
            Assert.Equal(260m, statement.MaintenanceExpenses); // Repairs (200) + Maintenance PropertyExpense (60)
            Assert.Equal(0m, statement.ManagementFees); // No PropertyExpense category maps to this yet
            Assert.Equal(1660m, statement.TotalExpenses);
            Assert.Equal(-160m, statement.NetIncome);
        }

        [Fact]
        public async Task GeneratePropertyPerformanceAsync_TotalExpensesIncludesFixedExpensesAndRepairs()
        {
            var performance = await _service.GeneratePropertyPerformanceAsync(_testOrgId, _periodStart, _periodEnd);

            var result = Assert.Single(performance);
            Assert.Equal(1500m, result.TotalIncome);
            Assert.Equal(1660m, result.TotalExpenses); // 1000+100+150+75+50+25+60 fixed + 200 repairs
            Assert.Equal(-160m, result.NetIncome);
        }

        [Fact]
        public async Task GenerateTaxReportAsync_MapsCategoriesAndDoesNotLabelMortgageAsInterest()
        {
            var reports = await _service.GenerateTaxReportAsync(_testOrgId, 2026);

            var report = Assert.Single(reports);
            Assert.Equal(1500m, report.TotalRentIncome);
            Assert.Equal(1000m, report.MortgageExpense); // Raw captured payment, not an interest-only figure
            Assert.Equal(100m, report.Insurance);
            Assert.Equal(150m, report.Taxes);
            Assert.Equal(75m, report.Utilities);
            Assert.Equal(75m, report.Other); // HOA (50) + Other (25)
            Assert.Equal(260m, report.Repairs); // Repairs (200) + Maintenance PropertyExpense (60)
            Assert.Equal(1660m, report.TotalExpenses);
        }

        [Fact]
        public async Task GenerateIncomeStatementAsync_ExcludesOtherOrganizationsExpenses()
        {
            using (var context = new TestApplicationDbContext(_options))
            {
                var otherOrgId = Guid.NewGuid();
                context.Organizations.Add(new Organization
                {
                    Id = otherOrgId,
                    Name = "Other Org",
                    OwnerId = "other-user",
                    CreatedBy = "other-user",
                    CreatedOn = DateTime.UtcNow
                });

                var otherPropertyId = Guid.NewGuid();
                context.Properties.Add(new Property
                {
                    Id = otherPropertyId,
                    OrganizationId = otherOrgId,
                    Address = "999 Other St",
                    PropertyType = "House",
                    CreatedBy = "other-user",
                    CreatedOn = DateTime.UtcNow
                });

                var otherExpenseId = Guid.NewGuid();
                context.PropertyExpenses.Add(new PropertyExpense
                {
                    Id = otherExpenseId,
                    OrganizationId = otherOrgId,
                    PropertyId = otherPropertyId,
                    ExpenseType = ApplicationConstants.PropertyExpenseTypes.Mortgage,
                    Amount = 5000m,
                    RecurrencePattern = new RecurrencePattern
                    {
                        Frequency = ApplicationConstants.ExpenseFrequencies.Monthly,
                        DueDayOfMonth = 1
                    },
                    CreatedBy = "other-user",
                    CreatedOn = DateTime.UtcNow
                });

                var otherDueId = Guid.NewGuid();
                context.Set<ExpenseDue>().Add(new ExpenseDue
                {
                    Id = otherDueId,
                    OrganizationId = otherOrgId,
                    PropertyExpenseId = otherExpenseId,
                    PropertyId = otherPropertyId,
                    DueDate = new DateTime(2026, 1, 1),
                    AmountDue = 5000m,
                    CreatedBy = "other-user",
                    CreatedOn = DateTime.UtcNow
                });

                context.Set<ExpensePayment>().Add(new ExpensePayment
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = otherOrgId,
                    ExpenseDueId = otherDueId,
                    AmountPaid = 5000m,
                    PaidOn = new DateTime(2026, 1, 5),
                    CreatedBy = "other-user",
                    CreatedOn = DateTime.UtcNow
                });

                context.SaveChanges();
            }

            var statement = await _service.GenerateIncomeStatementAsync(_testOrgId, _periodStart, _periodEnd);

            Assert.Equal(1000m, statement.MortgageExpenses); // Only this org's mortgage payment, not the other org's 5000
        }
    }
}
