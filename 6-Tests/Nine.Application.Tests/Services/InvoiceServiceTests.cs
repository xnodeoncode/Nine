using System.ComponentModel.DataAnnotations;
using Nine.Core.Constants;
using Nine.Core.Entities;
using Nine.Core.Interfaces.Services;
using Nine.Entities;
using Nine.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using InvoiceService = Nine.Application.Services.InvoiceService;

namespace Nine.Application.Tests
{
    /// <summary>
    /// Comprehensive unit tests for InvoiceService.
    /// Tests CRUD operations, validation, business logic, and organization isolation.
    /// </summary>
    public class InvoiceServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly TestApplicationDbContext _context;
        private readonly Mock<IUserContextService> _mockUserContext;
        private readonly Mock<ILogger<InvoiceService>> _mockLogger;
        private readonly IOptions<ApplicationSettings> _mockSettings;
        private readonly InvoiceService _service;
        private readonly Guid _testOrgId = Guid.NewGuid();
        private readonly string _testUserId = "test-user-123";
        private readonly Guid _testPropertyId = Guid.NewGuid();
        private readonly Guid _testTenantId = Guid.NewGuid();
        private readonly Guid _testLeaseId = Guid.NewGuid();

        public InvoiceServiceTests()
        {
            // Setup SQLite in-memory database
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new TestApplicationDbContext(options);
            _context.Database.EnsureCreated();

            // Mock IUserContextService
            _mockUserContext = new Mock<IUserContextService>();
            _mockUserContext.Setup(x => x.GetUserIdAsync())
                .ReturnsAsync(_testUserId);
            _mockUserContext.Setup(x => x.GetActiveOrganizationIdAsync())
                .ReturnsAsync(_testOrgId);
            _mockUserContext.Setup(x => x.GetUserNameAsync())
                .ReturnsAsync("testuser");
            _mockUserContext.Setup(x => x.GetUserEmailAsync())
                .ReturnsAsync("testuser@example.com");
            _mockUserContext.Setup(x => x.GetOrganizationIdAsync())
                .ReturnsAsync(_testOrgId);

            // Create test user
            var user = new ApplicationUser
            {
                Id = _testUserId,
                UserName = "testuser",
                Email = "testuser@example.com",
                ActiveOrganizationId = _testOrgId
            };
            _context.Users.Add(user);

            // Create test organization
            var organization = new Organization
            {
                Id = _testOrgId,
                Name = "Test Organization",
                OwnerId = _testUserId,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            _context.Organizations.Add(organization);

            // Create test property
            var property = new Property
            {
                Id = _testPropertyId,
                OrganizationId = _testOrgId,
                Address = "123 Test St",
                City = "Test City",
                State = "TS",
                ZipCode = "12345",
                IsActive = false,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            _context.Properties.Add(property);

            // Create test tenant
            var tenant = new Tenant
            {
                Id = _testTenantId,
                OrganizationId = _testOrgId,
                FirstName = "Test",
                LastName = "Tenant",
                Email = "tenant@test.com",
                IdentificationNumber = "SSN123456",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            _context.Tenants.Add(tenant);

            // Create test lease
            var lease = new Lease
            {
                Id = _testLeaseId,
                OrganizationId = _testOrgId,
                PropertyId = _testPropertyId,
                TenantId = _testTenantId,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddYears(1),
                MonthlyRent = 1500,
                SecurityDeposit = 1500,
                Status = "Active",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            _context.Leases.Add(lease);

            _context.SaveChanges();

            // Setup logger and settings
            _mockLogger = new Mock<ILogger<InvoiceService>>();

            _mockSettings = Options.Create(new ApplicationSettings
            {
                SoftDeleteEnabled = true
            });

            // Create service instance
            _service = new InvoiceService(
                _context,
                _mockLogger.Object,
                _mockUserContext.Object,
                _mockSettings);
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose();
        }

        #region Validation Tests

        [Fact]
        public async Task CreateAsync_ValidInvoice_CreatesSuccessfully()
        {
            // Arrange
            var invoice = new Invoice
            {
                OrganizationId = _testOrgId,
                LeaseId = _testLeaseId,
                InvoiceNumber = "INV-202512-00001",
                InvoicedOn = DateTime.Today,
                DueOn = DateTime.Today.AddDays(30),
                Amount = 1500,
                Description = "Monthly Rent - December 2025",
                Status = "Pending",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };

            // Act
            var result = await _service.CreateAsync(invoice);

            // Assert
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.Id);
            Assert.Equal("INV-202512-00001", result.InvoiceNumber);
            Assert.Equal(1500, result.Amount);
        }

        [Fact]
        public async Task CreateAsync_MissingLeaseId_ThrowsException()
        {
            // Arrange
            var invoice = new Invoice
            {
                OrganizationId = _testOrgId,
                LeaseId = Guid.Empty, // Missing
                InvoiceNumber = "INV-001",
                InvoicedOn = DateTime.Today,
                DueOn = DateTime.Today.AddDays(30),
                Amount = 1500,
                Description = "Test",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(invoice));
        }

        [Fact]
        public async Task CreateAsync_MissingInvoiceNumber_ThrowsException()
        {
            // Arrange
            var invoice = new Invoice
            {
                OrganizationId = _testOrgId,
                LeaseId = _testLeaseId,
                InvoiceNumber = "", // Missing
                InvoicedOn = DateTime.Today,
                DueOn = DateTime.Today.AddDays(30),
                Amount = 1500,
                Description = "Test",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(invoice));
        }

        [Fact]
        public async Task CreateAsync_MissingDescription_ThrowsException()
        {
            // Arrange
            var invoice = new Invoice
            {
                OrganizationId = _testOrgId,
                LeaseId = _testLeaseId,
                InvoiceNumber = "INV-001",
                InvoicedOn = DateTime.Today,
                DueOn = DateTime.Today.AddDays(30),
                Amount = 1500,
                Description = "", // Missing
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(invoice));
        }

        [Fact]
        public async Task CreateAsync_ZeroAmount_ThrowsException()
        {
            // Arrange
            var invoice = new Invoice
            {
                OrganizationId = _testOrgId,
                LeaseId = _testLeaseId,
                InvoiceNumber = "INV-001",
                InvoicedOn = DateTime.Today,
                DueOn = DateTime.Today.AddDays(30),
                Amount = 0, // Invalid
                Description = "Test",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(invoice));
        }

        [Fact]
        public async Task CreateAsync_DueBeforeInvoiced_ThrowsException()
        {
            // Arrange
            var invoice = new Invoice
            {
                OrganizationId = _testOrgId,
                LeaseId = _testLeaseId,
                InvoiceNumber = "INV-001",
                InvoicedOn = DateTime.Today,
                DueOn = DateTime.Today.AddDays(-1), // Before invoice date
                Amount = 1500,
                Description = "Test",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(invoice));
        }

        [Fact]
        public async Task CreateAsync_DuplicateInvoiceNumber_ThrowsException()
        {
            // Arrange
            var invoice1 = new Invoice
            {
                OrganizationId = _testOrgId,
                LeaseId = _testLeaseId,
                InvoiceNumber = "INV-DUPLICATE",
                InvoicedOn = DateTime.Today,
                DueOn = DateTime.Today.AddDays(30),
                Amount = 1500,
                Description = "Test",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(invoice1);

            var invoice2 = new Invoice
            {
                OrganizationId = _testOrgId,
                LeaseId = _testLeaseId,
                InvoiceNumber = "INV-DUPLICATE", // Same number
                InvoicedOn = DateTime.Today,
                DueOn = DateTime.Today.AddDays(30),
                Amount = 1500,
                Description = "Test",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(invoice2));
        }

        [Fact]
        public async Task CreateAsync_InvalidStatus_ThrowsException()
        {
            // Arrange
            var invoice = new Invoice
            {
                OrganizationId = _testOrgId,
                LeaseId = _testLeaseId,
                InvoiceNumber = "INV-001",
                InvoicedOn = DateTime.Today,
                DueOn = DateTime.Today.AddDays(30),
                Amount = 1500,
                Description = "Test",
                Status = "InvalidStatus", // Invalid
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(invoice));
        }

        #endregion

        #region Retrieval Tests

        [Fact]
        public async Task GetInvoicesByLeaseIdAsync_ReturnsLeaseInvoices()
        {
            // Arrange - Create invoices
            var invoice1 = new Invoice
            {
                OrganizationId = _testOrgId,
                LeaseId = _testLeaseId,
                InvoiceNumber = "INV-001",
                InvoicedOn = DateTime.Today,
                DueOn = DateTime.Today.AddDays(30),
                Amount = 1500,
                Description = "Rent - Month 1",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(invoice1);

            var invoice2 = new Invoice
            {
                OrganizationId = _testOrgId,
                LeaseId = _testLeaseId,
                InvoiceNumber = "INV-002",
                InvoicedOn = DateTime.Today.AddMonths(1),
                DueOn = DateTime.Today.AddMonths(1).AddDays(30),
                Amount = 1500,
                Description = "Rent - Month 2",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(invoice2);

            // Act
            var result = await _service.GetInvoicesByLeaseIdAsync(_testLeaseId);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.All(result, i => Assert.Equal(_testLeaseId, i.LeaseId));
        }

        [Fact]
        public async Task GetInvoicesByStatusAsync_ReturnsMatchingInvoices()
        {
            // Arrange
            var pending = new Invoice
            {
                OrganizationId = _testOrgId,
                LeaseId = _testLeaseId,
                InvoiceNumber = "INV-PENDING",
                InvoicedOn = DateTime.Today,
                DueOn = DateTime.Today.AddDays(30),
                Amount = 1500,
                Description = "Pending Invoice",
                Status = "Pending",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(pending);

            var paid = new Invoice
            {
                OrganizationId = _testOrgId,
                LeaseId = _testLeaseId,
                InvoiceNumber = "INV-PAID",
                InvoicedOn = DateTime.Today,
                DueOn = DateTime.Today.AddDays(30),
                Amount = 1500,
                Description = "Paid Invoice",
                Status = "Paid",
                PaidOn = DateTime.Today,
                AmountPaid = 1500,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(paid);

            // Act
            var pendingResults = await _service.GetInvoicesByStatusAsync("Pending");
            var paidResults = await _service.GetInvoicesByStatusAsync("Paid");

            // Assert
            Assert.Single(pendingResults);
            Assert.Single(paidResults);
            Assert.Equal("Pending", pendingResults[0].Status);
            Assert.Equal("Paid", paidResults[0].Status);
        }

        [Fact]
        public async Task GetOverdueInvoicesAsync_ReturnsOnlyOverdueInvoices()
        {
            // Arrange - Create overdue invoice
            var overdue = new Invoice
            {
                OrganizationId = _testOrgId,
                LeaseId = _testLeaseId,
                InvoiceNumber = "INV-OVERDUE",
                InvoicedOn = DateTime.Today.AddDays(-60),
                DueOn = DateTime.Today.AddDays(-30), // 30 days overdue
                Amount = 1500,
                Description = "Overdue Invoice",
                Status = "Pending",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(overdue);

            // Create current invoice (not overdue)
            var current = new Invoice
            {
                OrganizationId = _testOrgId,
                LeaseId = _testLeaseId,
                InvoiceNumber = "INV-CURRENT",
                InvoicedOn = DateTime.Today,
                DueOn = DateTime.Today.AddDays(30),
                Amount = 1500,
                Description = "Current Invoice",
                Status = "Pending",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(current);

            // Act
            var result = await _service.GetOverdueInvoicesAsync();

            // Assert
            Assert.Single(result);
            Assert.Equal("INV-OVERDUE", result[0].InvoiceNumber);
            Assert.True(result[0].DueOn < DateTime.Today);
        }

        [Fact]
        public async Task GetInvoicesDueSoonAsync_ReturnsInvoicesDueWithinThreshold()
        {
            // Arrange - Create invoice due in 5 days
            var dueSoon = new Invoice
            {
                OrganizationId = _testOrgId,
                LeaseId = _testLeaseId,
                InvoiceNumber = "INV-DUE-SOON",
                InvoicedOn = DateTime.Today,
                DueOn = DateTime.Today.AddDays(5),
                Amount = 1500,
                Description = "Due Soon",
                Status = "Pending",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(dueSoon);

            // Create invoice due in 30 days (outside threshold)
            var dueLater = new Invoice
            {
                OrganizationId = _testOrgId,
                LeaseId = _testLeaseId,
                InvoiceNumber = "INV-DUE-LATER",
                InvoicedOn = DateTime.Today,
                DueOn = DateTime.Today.AddDays(30),
                Amount = 1500,
                Description = "Due Later",
                Status = "Pending",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(dueLater);

            // Act
            var result = await _service.GetInvoicesDueSoonAsync(7);

            // Assert
            Assert.Single(result);
            Assert.Equal("INV-DUE-SOON", result[0].InvoiceNumber);
        }

        [Fact]
        public async Task GetInvoiceWithRelationsAsync_LoadsAllRelations()
        {
            // Arrange
            var invoice = new Invoice
            {
                OrganizationId = _testOrgId,
                LeaseId = _testLeaseId,
                InvoiceNumber = "INV-RELATIONS",
                InvoicedOn = DateTime.Today,
                DueOn = DateTime.Today.AddDays(30),
                Amount = 1500,
                Description = "Test Relations",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            var created = await _service.CreateAsync(invoice);

            // Act
            var result = await _service.GetInvoiceWithRelationsAsync(created.Id);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Lease);
            Assert.NotNull(result.Lease.Property);
            Assert.NotNull(result.Lease.Tenant);
            Assert.Equal(_testLeaseId, result.Lease.Id);
        }

        #endregion

        #region Business Logic Tests

        [Fact]
        public async Task GenerateInvoiceNumberAsync_GeneratesUniqueNumber()
        {
            // Act - Generate first number and create invoice to persist it
            var invoiceNumber1 = await _service.GenerateInvoiceNumberAsync();
            var invoice1 = new Invoice
            {
                OrganizationId = _testOrgId,
                LeaseId = _testLeaseId,
                InvoiceNumber = invoiceNumber1,
                InvoicedOn = DateTime.Today,
                DueOn = DateTime.Today.AddDays(30),
                Amount = 1500,
                Description = "Test Invoice 1",
                Status = "Pending",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(invoice1);
            
            // Generate second number - should see the first invoice and increment
            var invoiceNumber2 = await _service.GenerateInvoiceNumberAsync();

            // Assert
            Assert.NotNull(invoiceNumber1);
            Assert.NotNull(invoiceNumber2);
            Assert.StartsWith("INV-", invoiceNumber1);
            Assert.StartsWith("INV-", invoiceNumber2);
            
            // Verify correct format: INV-{YYYYMM}-{0001}
            Assert.Matches(@"^INV-\d{6}-\d{4}$", invoiceNumber1);
            Assert.Matches(@"^INV-\d{6}-\d{4}$", invoiceNumber2);
            
            // Verify both numbers are unique
            Assert.NotEqual(invoiceNumber1, invoiceNumber2);
            
            // Verify sequential numbering (both must be in same month for this test)
            var parts1 = invoiceNumber1.Split('-');
            var parts2 = invoiceNumber2.Split('-');
            Assert.Equal(parts1[1], parts2[1]); // Same month (test runs fast enough)
            
            var seq1 = int.Parse(parts1[2]);
            var seq2 = int.Parse(parts2[2]);
            Assert.Equal(seq1 + 1, seq2); // Should increment: 0001 -> 0002
        }

        [Fact]
        public async Task ApplyLateFeeAsync_AddsLateFeeToInvoice()
        {
            // Arrange
            var invoice = new Invoice
            {
                OrganizationId = _testOrgId,
                LeaseId = _testLeaseId,
                InvoiceNumber = "INV-LATE-FEE",
                InvoicedOn = DateTime.Today.AddDays(-60),
                DueOn = DateTime.Today.AddDays(-30),
                Amount = 1500,
                Description = "Overdue Invoice",
                Status = "Pending",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            var created = await _service.CreateAsync(invoice);

            // Act
            var result = await _service.ApplyLateFeeAsync(created.Id, 50m);

            // Assert
            Assert.Equal(50m, result.LateFeeAmount);
            Assert.True(result.LateFeeApplied);
            Assert.NotNull(result.LateFeeAppliedOn);
            Assert.Equal("Overdue", result.Status);
        }

        [Fact]
        public async Task ApplyLateFeeAsync_AlreadyApplied_ThrowsException()
        {
            // Arrange
            var invoice = new Invoice
            {
                OrganizationId = _testOrgId,
                LeaseId = _testLeaseId,
                InvoiceNumber = "INV-TEST",
                InvoicedOn = DateTime.Today,
                DueOn = DateTime.Today.AddDays(30),
                Amount = 1500,
                Description = "Test",
                Status = "Pending",
                LateFeeApplied = true,
                LateFeeAmount = 50,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            var created = await _service.CreateAsync(invoice);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.ApplyLateFeeAsync(created.Id, 50m));
        }

        [Fact]
        public async Task MarkReminderSentAsync_UpdatesReminderFields()
        {
            // Arrange
            var invoice = new Invoice
            {
                OrganizationId = _testOrgId,
                LeaseId = _testLeaseId,
                InvoiceNumber = "INV-REMINDER",
                InvoicedOn = DateTime.Today,
                DueOn = DateTime.Today.AddDays(5),
                Amount = 1500,
                Description = "Test Reminder",
                Status = "Pending",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            var created = await _service.CreateAsync(invoice);

            // Act
            var result = await _service.MarkReminderSentAsync(created.Id);

            // Assert
            Assert.True(result.ReminderSent);
            Assert.NotNull(result.ReminderSentOn);
        }

        [Fact]
        public async Task UpdateInvoiceStatusAsync_FullyPaid_UpdatesStatusToPaid()
        {
            // Arrange
            var invoice = new Invoice
            {
                OrganizationId = _testOrgId,
                LeaseId = _testLeaseId,
                InvoiceNumber = "INV-STATUS-TEST",
                InvoicedOn = DateTime.Today,
                DueOn = DateTime.Today.AddDays(30),
                Amount = 1500,
                Description = "Test Status",
                Status = "Pending",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            var created = await _service.CreateAsync(invoice);

            // Create payment
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                OrganizationId = _testOrgId,
                InvoiceId = created.Id,
                Amount = 1500,
                PaymentMethod = "Check",
                PaidOn = DateTime.Today,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow,
                IsDeleted = false
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.UpdateInvoiceStatusAsync(created.Id);

            // Assert
            Assert.Equal("Paid", result.Status);
            Assert.Equal(1500, result.AmountPaid);
            Assert.NotNull(result.PaidOn);
        }

        [Fact]
        public async Task CalculateTotalOutstandingAsync_ReturnsCorrectTotal()
        {
            // Arrange - Create unpaid invoices
            var invoice1 = new Invoice
            {
                OrganizationId = _testOrgId,
                LeaseId = _testLeaseId,
                InvoiceNumber = "INV-OUT-1",
                InvoicedOn = DateTime.Today,
                DueOn = DateTime.Today.AddDays(30),
                Amount = 1500,
                Description = "Outstanding 1",
                Status = "Pending",
                AmountPaid = 0,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(invoice1);

            var invoice2 = new Invoice
            {
                OrganizationId = _testOrgId,
                LeaseId = _testLeaseId,
                InvoiceNumber = "INV-OUT-2",
                InvoicedOn = DateTime.Today,
                DueOn = DateTime.Today.AddDays(30),
                Amount = 2000,
                Description = "Outstanding 2",
                Status = "Pending",
                AmountPaid = 500, // Partially paid
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(invoice2);

            // Act
            var total = await _service.CalculateTotalOutstandingAsync();

            // Assert
            Assert.Equal(3000m, total); // 1500 + (2000 - 500)
        }

        [Fact]
        public async Task GetInvoicesByDateRangeAsync_ReturnsInvoicesInRange()
        {
            // Arrange
            var oldInvoice = new Invoice
            {
                OrganizationId = _testOrgId,
                LeaseId = _testLeaseId,
                InvoiceNumber = "INV-OLD",
                InvoicedOn = DateTime.Today.AddMonths(-2),
                DueOn = DateTime.Today.AddMonths(-1),
                Amount = 1500,
                Description = "Old Invoice",
                Status = "Paid",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(oldInvoice);

            var recentInvoice = new Invoice
            {
                OrganizationId = _testOrgId,
                LeaseId = _testLeaseId,
                InvoiceNumber = "INV-RECENT",
                InvoicedOn = DateTime.Today,
                DueOn = DateTime.Today.AddDays(30),
                Amount = 1500,
                Description = "Recent Invoice",
                Status = "Pending",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(recentInvoice);

            // Act
            var startDate = DateTime.Today.AddDays(-7);
            var endDate = DateTime.Today.AddDays(7);
            var result = await _service.GetInvoicesByDateRangeAsync(startDate, endDate);

            // Assert
            Assert.Single(result);
            Assert.Equal("INV-RECENT", result[0].InvoiceNumber);
        }

        #endregion

        #region Organization Isolation Tests

        [Fact]
        public async Task GetByIdAsync_DifferentOrganization_ReturnsNull()
        {
            // Arrange - Create different organization and invoice
            var otherUserId = "other-user-456";
            var otherUser = new ApplicationUser
            {
                Id = otherUserId,
                UserName = "otheruser",
                Email = "otheruser@example.com"
            };
            _context.Users.Add(otherUser);
            await _context.SaveChangesAsync();

            var otherOrg = new Organization
            {
                Id = Guid.NewGuid(),
                Name = "Other Organization",
                OwnerId = otherUserId,
                CreatedBy = otherUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _context.Organizations.AddAsync(otherOrg);

            var otherProperty = new Property
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrg.Id,
                Address = "999 Other St",
                City = "Other City",
                State = "OT",
                ZipCode = "99999",
                IsActive = true,
                CreatedBy = otherUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _context.Properties.AddAsync(otherProperty);

            var otherTenant = new Tenant
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrg.Id,
                FirstName = "Other",
                LastName = "Tenant",
                Email = "other@test.com",
                IdentificationNumber = "ID999",
                CreatedBy = otherUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _context.Tenants.AddAsync(otherTenant);

            var otherLease = new Lease
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrg.Id,
                PropertyId = otherProperty.Id,
                TenantId = otherTenant.Id,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddYears(1),
                MonthlyRent = 2000,
                SecurityDeposit = 2000,
                Status = "Active",
                CreatedBy = otherUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _context.Leases.AddAsync(otherLease);

            var otherOrgInvoice = new Invoice
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrg.Id,
                LeaseId = otherLease.Id,
                InvoiceNumber = "INV-OTHER",
                InvoicedOn = DateTime.Today,
                DueOn = DateTime.Today.AddDays(30),
                Amount = 2000,
                Description = "Other Org Invoice",
                Status = "Pending",
                CreatedBy = otherUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _context.Invoices.AddAsync(otherOrgInvoice);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetByIdAsync(otherOrgInvoice.Id);

            // Assert
            Assert.Null(result); // Should not access invoice from different org
        }

        [Fact]
        public async Task GetAllAsync_ReturnsOnlyCurrentOrganizationInvoices()
        {
            // Arrange - Create invoice in test org
            var testOrgInvoice = new Invoice
            {
                OrganizationId = _testOrgId,
                LeaseId = _testLeaseId,
                InvoiceNumber = "INV-TEST-ORG",
                InvoicedOn = DateTime.Today,
                DueOn = DateTime.Today.AddDays(30),
                Amount = 1500,
                Description = "Test Org Invoice",
                Status = "Pending",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(testOrgInvoice);

            // Create invoice in different org
            var otherUserId = "other-user-456";
            var otherUser = new ApplicationUser
            {
                Id = otherUserId,
                UserName = "otheruser",
                Email = "otheruser@example.com"
            };
            _context.Users.Add(otherUser);
            await _context.SaveChangesAsync();

            var otherOrg = new Organization
            {
                Id = Guid.NewGuid(),
                Name = "Other Organization",
                OwnerId = otherUserId,
                CreatedBy = otherUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _context.Organizations.AddAsync(otherOrg);

            var otherProperty = new Property
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrg.Id,
                Address = "888 Other Ave",
                City = "Other City",
                State = "OT",
                ZipCode = "88888",
                IsActive = true,
                CreatedBy = otherUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _context.Properties.AddAsync(otherProperty);

            var otherTenant = new Tenant
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrg.Id,
                FirstName = "Other",
                LastName = "Tenant",
                Email = "other@test.com",
                IdentificationNumber = "ID888",
                CreatedBy = otherUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _context.Tenants.AddAsync(otherTenant);

            var otherLease = new Lease
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrg.Id,
                PropertyId = otherProperty.Id,
                TenantId = otherTenant.Id,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddYears(1),
                MonthlyRent = 2500,
                SecurityDeposit = 2500,
                Status = "Active",
                CreatedBy = otherUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _context.Leases.AddAsync(otherLease);

            var otherOrgInvoice = new Invoice
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrg.Id,
                LeaseId = otherLease.Id,
                InvoiceNumber = "INV-OTHER-ORG",
                InvoicedOn = DateTime.Today,
                DueOn = DateTime.Today.AddDays(30),
                Amount = 2500,
                Description = "Other",
                Status = "Pending",
                CreatedBy = otherUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _context.Invoices.AddAsync(otherOrgInvoice);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetAllAsync();

            // Assert
            Assert.Single(result);
            Assert.Equal(_testOrgId, result[0].OrganizationId);
        }

        #endregion
    }
}
