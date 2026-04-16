using System.ComponentModel.DataAnnotations;
using Nine.Application.Services;
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

namespace Nine.Application.Tests
{
    /// <summary>
    /// Comprehensive unit tests for TenantService.
    /// Tests business logic, validation, search, and relationships.
    /// </summary>
    public class TenantServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly TestApplicationDbContext _context;
        private readonly Mock<IUserContextService> _mockUserContext;
        private readonly Mock<ILogger<TenantService>> _mockLogger;
        private readonly IOptions<ApplicationSettings> _mockSettings;
        private readonly TenantService _service;
        private readonly Guid _testOrgId = Guid.NewGuid();
        private readonly string _testUserId = Guid.NewGuid().ToString();

        public TenantServiceTests()
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

            // Create test user (required for Organization.OwnerId foreign key)
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
            _context.SaveChanges();

            // Setup logger and settings
            _mockLogger = new Mock<ILogger<TenantService>>();

            _mockSettings = Options.Create(new ApplicationSettings
            {
                SoftDeleteEnabled = true
            });

            // Create service instance
            _service = new TenantService(
                _context,
                _mockLogger.Object,
                _mockUserContext.Object,
                _mockSettings);
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Close();
            _connection.Dispose();
        }

        #region Validation Tests

        [Fact]
        public async Task CreateAsync_ValidTenant_CreatesSuccessfully()
        {
            // Arrange
            var tenant = new Tenant
            {
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@example.com",
                IdentificationNumber = "SSN123456789",
                PhoneNumber = "555-1234"
            };

            // Act
            var result = await _service.CreateAsync(tenant);

            // Assert
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.Id);
            Assert.Equal(_testOrgId, result.OrganizationId);
            Assert.Equal(_testUserId, result.CreatedBy);
            Assert.Equal("John", result.FirstName);
            Assert.Equal("Doe", result.LastName);
            Assert.Equal("john.doe@example.com", result.Email);
            Assert.Equal("SSN123456789", result.IdentificationNumber);
        }

        [Fact]
        public async Task CreateAsync_MissingEmail_ThrowsException()
        {
            // Arrange
            var tenant = new Tenant
            {
                FirstName = "John",
                LastName = "Doe",
                Email = "", // Missing email
                IdentificationNumber = "SSN123456789"
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(tenant));
        }

        [Fact]
        public async Task CreateAsync_MissingIdentificationNumber_ThrowsException()
        {
            // Arrange
            var tenant = new Tenant
            {
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@example.com",
                IdentificationNumber = "" // Missing ID number
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(tenant));
        }

        [Fact]
        public async Task CreateAsync_DuplicateEmail_ThrowsException()
        {
            // Arrange
            var tenant1 = new Tenant
            {
                FirstName = "John",
                LastName = "Doe",
                Email = "duplicate@example.com",
                IdentificationNumber = "SSN111111111"
            };
            await _service.CreateAsync(tenant1);

            var tenant2 = new Tenant
            {
                FirstName = "Jane",
                LastName = "Smith",
                Email = "duplicate@example.com", // Duplicate email
                IdentificationNumber = "SSN222222222"
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(tenant2));
        }

        [Fact]
        public async Task CreateAsync_DuplicateIdentificationNumber_ThrowsException()
        {
            // Arrange
            var tenant1 = new Tenant
            {
                FirstName = "John",
                LastName = "Doe",
                Email = "john@example.com",
                IdentificationNumber = "SSN999999999"
            };
            await _service.CreateAsync(tenant1);

            var tenant2 = new Tenant
            {
                FirstName = "Jane",
                LastName = "Smith",
                Email = "jane@example.com",
                IdentificationNumber = "SSN999999999" // Duplicate ID number
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(tenant2));
        }

        #endregion

        #region Search Tests

        [Fact]
        public async Task SearchTenantsAsync_ByFirstName_ReturnsTenants()
        {
            // Arrange
            await _service.CreateAsync(new Tenant
            {
                FirstName = "Alice",
                LastName = "Johnson",
                Email = "alice@example.com",
                IdentificationNumber = "SSN001"
            });
            await _service.CreateAsync(new Tenant
            {
                FirstName = "Bob",
                LastName = "Smith",
                Email = "bob@example.com",
                IdentificationNumber = "SSN002"
            });

            // Act
            var result = await _service.SearchTenantsAsync("Alice");

            // Assert
            Assert.Single(result);
            Assert.Equal("Alice", result[0].FirstName);
        }

        [Fact]
        public async Task SearchTenantsAsync_ByLastName_ReturnsTenants()
        {
            // Arrange
            await _service.CreateAsync(new Tenant
            {
                FirstName = "John",
                LastName = "Williams",
                Email = "john.w@example.com",
                IdentificationNumber = "SSN003"
            });
            await _service.CreateAsync(new Tenant
            {
                FirstName = "Jane",
                LastName = "Williams",
                Email = "jane.w@example.com",
                IdentificationNumber = "SSN004"
            });

            // Act
            var result = await _service.SearchTenantsAsync("Williams");

            // Assert
            Assert.Equal(2, result.Count);
            Assert.All(result, t => Assert.Equal("Williams", t.LastName));
        }

        [Fact]
        public async Task SearchTenantsAsync_ByEmail_ReturnsTenant()
        {
            // Arrange
            await _service.CreateAsync(new Tenant
            {
                FirstName = "Charlie",
                LastName = "Brown",
                Email = "charlie.unique@example.com",
                IdentificationNumber = "SSN005"
            });

            // Act
            var result = await _service.SearchTenantsAsync("charlie.unique");

            // Assert
            Assert.Single(result);
            Assert.Equal("charlie.unique@example.com", result[0].Email);
        }

        [Fact]
        public async Task SearchTenantsAsync_ByIdentificationNumber_ReturnsTenant()
        {
            // Arrange
            await _service.CreateAsync(new Tenant
            {
                FirstName = "David",
                LastName = "Miller",
                Email = "david@example.com",
                IdentificationNumber = "SSN777777777"
            });

            // Act
            var result = await _service.SearchTenantsAsync("SSN777777777");

            // Assert
            Assert.Single(result);
            Assert.Equal("SSN777777777", result[0].IdentificationNumber);
        }

        [Fact]
        public async Task SearchTenantsAsync_EmptySearchTerm_ReturnsFirst20()
        {
            // Arrange - Create 25 tenants
            for (int i = 1; i <= 25; i++)
            {
                await _service.CreateAsync(new Tenant
                {
                    FirstName = $"Tenant{i}",
                    LastName = $"Test{i}",
                    Email = $"tenant{i}@example.com",
                    IdentificationNumber = $"SSN{i:D9}"
                });
            }

            // Act
            var result = await _service.SearchTenantsAsync("");

            // Assert
            Assert.Equal(20, result.Count); // Should limit to 20
        }

        [Fact]
        public async Task SearchTenantsAsync_NoMatch_ReturnsEmpty()
        {
            // Arrange
            await _service.CreateAsync(new Tenant
            {
                FirstName = "Eve",
                LastName = "Anderson",
                Email = "eve@example.com",
                IdentificationNumber = "SSN006"
            });

            // Act
            var result = await _service.SearchTenantsAsync("NonExistentName");

            // Assert
            Assert.Empty(result);
        }

        #endregion

        #region Lookup Tests

        [Fact]
        public async Task GetTenantByEmailAsync_ExistingEmail_ReturnsTenant()
        {
            // Arrange
            var tenant = await _service.CreateAsync(new Tenant
            {
                FirstName = "Frank",
                LastName = "Garcia",
                Email = "frank.garcia@example.com",
                IdentificationNumber = "SSN007"
            });

            // Act
            var result = await _service.GetTenantByEmailAsync("frank.garcia@example.com");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(tenant.Id, result.Id);
            Assert.Equal("frank.garcia@example.com", result.Email);
        }

        [Fact]
        public async Task GetTenantByEmailAsync_NonExistentEmail_ReturnsNull()
        {
            // Act
            var result = await _service.GetTenantByEmailAsync("nonexistent@example.com");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetTenantByIdentificationNumberAsync_ExistingNumber_ReturnsTenant()
        {
            // Arrange
            var tenant = await _service.CreateAsync(new Tenant
            {
                FirstName = "Grace",
                LastName = "Martinez",
                Email = "grace@example.com",
                IdentificationNumber = "SSN888888888"
            });

            // Act
            var result = await _service.GetTenantByIdentificationNumberAsync("SSN888888888");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(tenant.Id, result.Id);
            Assert.Equal("SSN888888888", result.IdentificationNumber);
        }

        [Fact]
        public async Task GetTenantByIdentificationNumberAsync_NonExistentNumber_ReturnsNull()
        {
            // Act
            var result = await _service.GetTenantByIdentificationNumberAsync("NONEXISTENT");

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region Active Tenant Tests

        [Fact]
        public async Task GetActiveTenantsAsync_ReturnsOnlyActiveTenants()
        {
            // Arrange
            var activeTenant = await _service.CreateAsync(new Tenant
            {
                FirstName = "Active",
                LastName = "Tenant",
                Email = "active@example.com",
                IdentificationNumber = "SSN010",
                IsActive = true
            });

            var inactiveTenant = await _service.CreateAsync(new Tenant
            {
                FirstName = "Inactive",
                LastName = "Tenant",
                Email = "inactive@example.com",
                IdentificationNumber = "SSN011",
                IsActive = false
            });

            // Act
            var result = await _service.GetActiveTenantsAsync();

            // Assert
            Assert.Single(result);
            Assert.Equal(activeTenant.Id, result[0].Id);
            Assert.True(result[0].IsActive);
        }

        #endregion

        #region Tenant with Active Leases Tests

        [Fact]
        public async Task GetTenantsWithActiveLeasesAsync_ReturnsOnlyTenantsWithActiveLeases()
        {
            // Arrange
            var property = new Property
            {
                Id = Guid.NewGuid(),
                OrganizationId = _testOrgId,
                Address = "123 Main St",
                City = "Test City",
                State = "TS",
                ZipCode = "12345",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _context.Properties.AddAsync(property);

            var tenantWithActiveLease = await _service.CreateAsync(new Tenant
            {
                FirstName = "With",
                LastName = "ActiveLease",
                Email = "withlease@example.com",
                IdentificationNumber = "SSN012"
            });

            var tenantWithoutLease = await _service.CreateAsync(new Tenant
            {
                FirstName = "Without",
                LastName = "Lease",
                Email = "withoutlease@example.com",
                IdentificationNumber = "SSN013"
            });

            var activeLease = new Lease
            {
                Id = Guid.NewGuid(),
                OrganizationId = _testOrgId,
                PropertyId = property.Id,
                TenantId = tenantWithActiveLease.Id,
                Status = ApplicationConstants.LeaseStatuses.Accepted,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(12),
                MonthlyRent = 1000,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _context.Leases.AddAsync(activeLease);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetTenantsWithActiveLeasesAsync();

            // Assert
            Assert.Single(result);
            Assert.Equal(tenantWithActiveLease.Id, result[0].Id);
        }

        #endregion

        #region Relationship Tests

        [Fact]
        public async Task GetTenantWithRelationsAsync_LoadsLeases()
        {
            // Arrange
            var property = new Property
            {
                Id = Guid.NewGuid(),
                OrganizationId = _testOrgId,
                Address = "456 Oak Ave",
                City = "Test City",
                State = "TS",
                ZipCode = "12345",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _context.Properties.AddAsync(property);

            var tenant = await _service.CreateAsync(new Tenant
            {
                FirstName = "Henry",
                LastName = "Wilson",
                Email = "henry@example.com",
                IdentificationNumber = "SSN014"
            });

            var lease = new Lease
            {
                Id = Guid.NewGuid(),
                OrganizationId = _testOrgId,
                PropertyId = property.Id,
                TenantId = tenant.Id,
                Status = ApplicationConstants.LeaseStatuses.Accepted,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(6),
                MonthlyRent = 1200,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _context.Leases.AddAsync(lease);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetTenantWithRelationsAsync(tenant.Id);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Leases);
            Assert.Single(result.Leases);
            Assert.Equal(lease.Id, result.Leases.First().Id);
        }

        [Fact]
        public async Task GetTenantsByPropertyIdAsync_ReturnsTenantsForProperty()
        {
            // Arrange
            var property = new Property
            {
                Id = Guid.NewGuid(),
                OrganizationId = _testOrgId,
                Address = "789 Pine Rd",
                City = "Test City",
                State = "TS",
                ZipCode = "12345",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _context.Properties.AddAsync(property);

            var tenant1 = await _service.CreateAsync(new Tenant
            {
                FirstName = "Tenant",
                LastName = "One",
                Email = "tenant1@example.com",
                IdentificationNumber = "SSN015"
            });

            var tenant2 = await _service.CreateAsync(new Tenant
            {
                FirstName = "Tenant",
                LastName = "Two",
                Email = "tenant2@example.com",
                IdentificationNumber = "SSN016"
            });

            var lease1 = new Lease
            {
                Id = Guid.NewGuid(),
                OrganizationId = _testOrgId,
                PropertyId = property.Id,
                TenantId = tenant1.Id,
                Status = ApplicationConstants.LeaseStatuses.Accepted,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(12),
                MonthlyRent = 1000,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _context.Leases.AddAsync(lease1);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetTenantsByPropertyIdAsync(property.Id);

            // Assert
            Assert.Single(result);
            Assert.Equal(tenant1.Id, result[0].Id);
        }

        #endregion

        #region Balance Calculation Tests

        [Fact]
        public async Task CalculateTenantBalanceAsync_CalculatesCorrectBalance()
        {
            // Arrange
            var property = new Property
            {
                Id = Guid.NewGuid(),
                OrganizationId = _testOrgId,
                Address = "111 Balance St",
                City = "Test City",
                State = "TS",
                ZipCode = "12345",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _context.Properties.AddAsync(property);

            var tenant = await _service.CreateAsync(new Tenant
            {
                FirstName = "Balance",
                LastName = "Test",
                Email = "balance@example.com",
                IdentificationNumber = "SSN017"
            });

            var lease = new Lease
            {
                Id = Guid.NewGuid(),
                OrganizationId = _testOrgId,
                PropertyId = property.Id,
                TenantId = tenant.Id,
                Status = ApplicationConstants.LeaseStatuses.Accepted,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(12),
                MonthlyRent = 1000,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _context.Leases.AddAsync(lease);

            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                OrganizationId = _testOrgId,
                LeaseId = lease.Id,
                Amount = 1000,
                AmountPaid = 0,
                DueOn = DateTime.UtcNow,
                Status = "Outstanding",
                InvoiceNumber = "INV-001",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _context.Invoices.AddAsync(invoice);

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                OrganizationId = _testOrgId,
                InvoiceId = invoice.Id,
                Amount = 600,
                PaidOn = DateTime.UtcNow,
                PaymentMethod = "Check",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _context.Payments.AddAsync(payment);
            await _context.SaveChangesAsync();

            // Act
            var balance = await _service.CalculateTenantBalanceAsync(tenant.Id);

            // Assert
            Assert.Equal(400, balance); // 1000 - 600 = 400
        }

        [Fact]
        public async Task CalculateTenantBalanceAsync_NoInvoices_ReturnsZero()
        {
            // Arrange
            var tenant = await _service.CreateAsync(new Tenant
            {
                FirstName = "Zero",
                LastName = "Balance",
                Email = "zero@example.com",
                IdentificationNumber = "SSN018"
            });

            // Act
            var balance = await _service.CalculateTenantBalanceAsync(tenant.Id);

            // Assert
            Assert.Equal(0, balance);
        }

        [Fact]
        public async Task CalculateTenantBalanceAsync_NonExistentTenant_ThrowsException()
        {
            // Arrange
            var nonExistentTenantId = Guid.NewGuid();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CalculateTenantBalanceAsync(nonExistentTenantId));
        }

        #endregion

        #region Organization Isolation Tests

        [Fact]
        public async Task GetByIdAsync_DifferentOrganization_ReturnsNull()
        {
            // Arrange - Create different organization with different owner
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

            var otherOrgTenant = new Tenant
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrg.Id,
                FirstName = "Other",
                LastName = "Org",
                Email = "other@example.com",
                IdentificationNumber = "SSN999",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _context.Tenants.AddAsync(otherOrgTenant);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetByIdAsync(otherOrgTenant.Id);

            // Assert
            Assert.Null(result); // Should not access tenant from different org
        }

        [Fact]
        public async Task GetAllAsync_ReturnsOnlyCurrentOrganizationTenants()
        {
            // Arrange - Create different organization with different owner
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

            // Create tenant in test org
            await _service.CreateAsync(new Tenant
            {
                FirstName = "Test",
                LastName = "Org",
                Email = "testorg@example.com",
                IdentificationNumber = "SSN020"
            });

            // Create tenant in other org
            var otherOrgTenant = new Tenant
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrg.Id,
                FirstName = "Other",
                LastName = "Org",
                Email = "otherorg@example.com",
                IdentificationNumber = "SSN021",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _context.Tenants.AddAsync(otherOrgTenant);
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
