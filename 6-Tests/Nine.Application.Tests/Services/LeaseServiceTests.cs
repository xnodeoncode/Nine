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
    /// Comprehensive unit tests for LeaseService.
    /// Tests CRUD operations, business logic, validation, and organization isolation.
    /// </summary>
    public class LeaseServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly TestApplicationDbContext _context;
        private readonly Mock<IUserContextService> _mockUserContext;
        private readonly Mock<ILogger<LeaseService>> _mockLogger;
        private readonly IOptions<ApplicationSettings> _mockSettings;
        private readonly LeaseService _service;
        private readonly Guid _testOrgId = Guid.NewGuid();
        private readonly string _testUserId = "test-user-123";
        private readonly Guid _testPropertyId = Guid.NewGuid();
        private readonly Guid _testTenantId = Guid.NewGuid();

        public LeaseServiceTests()
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
                IsActive = true,
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

            _context.SaveChanges();

            // Setup logger and settings
            _mockLogger = new Mock<ILogger<LeaseService>>();

            _mockSettings = Options.Create(new ApplicationSettings
            {
                SoftDeleteEnabled = true
            });

            // Create service instance
            _service = new LeaseService(
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
        public async Task CreateAsync_ValidLease_CreatesSuccessfully()
        {
            // Arrange
            var lease = new Lease
            {
                OrganizationId = _testOrgId,
                PropertyId = _testPropertyId,
                TenantId = _testTenantId,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddYears(1),
                MonthlyRent = 1500,
                SecurityDeposit = 1500,
                Status = ApplicationConstants.LeaseStatuses.Pending,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };

            // Act
            var result = await _service.CreateAsync(lease);

            // Assert
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.Id);
            Assert.Equal(_testPropertyId, result.PropertyId);
            Assert.Equal(_testTenantId, result.TenantId);
        }

        [Fact]
        public async Task CreateAsync_MissingPropertyId_ThrowsException()
        {
            // Arrange
            var lease = new Lease
            {
                OrganizationId = _testOrgId,
                PropertyId = Guid.Empty, // Missing
                TenantId = _testTenantId,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddYears(1),
                MonthlyRent = 1500,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(lease));
        }

        [Fact]
        public async Task CreateAsync_MissingTenantId_ThrowsException()
        {
            // Arrange
            var lease = new Lease
            {
                OrganizationId = _testOrgId,
                PropertyId = _testPropertyId,
                TenantId = Guid.Empty, // Missing
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddYears(1),
                MonthlyRent = 1500,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(lease));
        }

        [Fact]
        public async Task CreateAsync_EndDateBeforeStartDate_ThrowsException()
        {
            // Arrange
            var lease = new Lease
            {
                OrganizationId = _testOrgId,
                PropertyId = _testPropertyId,
                TenantId = _testTenantId,
                StartDate = DateTime.Today.AddYears(1),
                EndDate = DateTime.Today, // Before start date
                MonthlyRent = 1500,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(lease));
        }

        [Fact]
        public async Task CreateAsync_ZeroMonthlyRent_ThrowsException()
        {
            // Arrange
            var lease = new Lease
            {
                OrganizationId = _testOrgId,
                PropertyId = _testPropertyId,
                TenantId = _testTenantId,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddYears(1),
                MonthlyRent = 0, // Invalid
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(lease));
        }

        [Fact]
        public async Task CreateAsync_OverlappingLease_ThrowsException()
        {
            // Arrange - Create first lease
            var firstLease = new Lease
            {
                OrganizationId = _testOrgId,
                PropertyId = _testPropertyId,
                TenantId = _testTenantId,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddYears(1),
                MonthlyRent = 1500,
                Status = ApplicationConstants.LeaseStatuses.Active,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(firstLease);

            // Try to create overlapping lease
            var overlappingLease = new Lease
            {
                OrganizationId = _testOrgId,
                PropertyId = _testPropertyId,
                TenantId = _testTenantId,
                StartDate = DateTime.Today.AddMonths(6),
                EndDate = DateTime.Today.AddMonths(18),
                MonthlyRent = 1500,
                Status = ApplicationConstants.LeaseStatuses.Pending,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(overlappingLease));
        }

        #endregion

        #region Property Availability Tests

        [Fact]
        public async Task CreateAsync_ActiveLease_MarksPropertyUnavailable()
        {
            // Arrange
            var lease = new Lease
            {
                OrganizationId = _testOrgId,
                PropertyId = _testPropertyId,
                TenantId = _testTenantId,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddYears(1),
                MonthlyRent = 1500,
                Status = ApplicationConstants.LeaseStatuses.Active,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };

            // Act
            await _service.CreateAsync(lease);

            // Assert
            var property = await _context.Properties.FindAsync(_testPropertyId);
            Assert.False(property!.IsActive);
        }

        [Fact]
        public async Task CreateAsync_PendingLease_DoesNotMarkPropertyUnavailable()
        {
            // Arrange
            var lease = new Lease
            {
                OrganizationId = _testOrgId,
                PropertyId = _testPropertyId,
                TenantId = _testTenantId,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddYears(1),
                MonthlyRent = 1500,
                Status = ApplicationConstants.LeaseStatuses.Pending,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };

            // Act
            await _service.CreateAsync(lease);

            // Assert
            var property = await _context.Properties.FindAsync(_testPropertyId);
            Assert.True(property!.IsActive);
        }

        [Fact]
        public async Task DeleteAsync_ActiveLease_MarksPropertyAvailable()
        {
            // Arrange - Create active lease
            var lease = new Lease
            {
                OrganizationId = _testOrgId,
                PropertyId = _testPropertyId,
                TenantId = _testTenantId,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddYears(1),
                MonthlyRent = 1500,
                Status = ApplicationConstants.LeaseStatuses.Active,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            var created = await _service.CreateAsync(lease);

            // Act
            await _service.DeleteAsync(created.Id);

            // Assert
            var property = await _context.Properties.FindAsync(_testPropertyId);
            Assert.True(property!.IsActive);
        }

        #endregion

        #region Retrieval Tests

        [Fact]
        public async Task GetLeasesByPropertyIdAsync_ReturnsPropertyLeases()
        {
            // Arrange - Create multiple leases
            var lease1 = new Lease
            {
                OrganizationId = _testOrgId,
                PropertyId = _testPropertyId,
                TenantId = _testTenantId,
                StartDate = DateTime.Today.AddYears(-1),
                EndDate = DateTime.Today.AddMonths(-1),
                MonthlyRent = 1200,
                Status = ApplicationConstants.LeaseStatuses.Expired,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(lease1);

            var lease2 = new Lease
            {
                OrganizationId = _testOrgId,
                PropertyId = _testPropertyId,
                TenantId = _testTenantId,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddYears(1),
                MonthlyRent = 1500,
                Status = ApplicationConstants.LeaseStatuses.Active,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(lease2);

            // Act
            var result = await _service.GetLeasesByPropertyIdAsync(_testPropertyId);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.All(result, l => Assert.Equal(_testPropertyId, l.PropertyId));
        }

        [Fact]
        public async Task GetLeasesByTenantIdAsync_ReturnsTenantLeases()
        {
            // Arrange - Create lease
            var lease = new Lease
            {
                OrganizationId = _testOrgId,
                PropertyId = _testPropertyId,
                TenantId = _testTenantId,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddYears(1),
                MonthlyRent = 1500,
                Status = ApplicationConstants.LeaseStatuses.Active,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(lease);

            // Act
            var result = await _service.GetLeasesByTenantIdAsync(_testTenantId);

            // Assert
            Assert.Single(result);
            Assert.Equal(_testTenantId, result[0].TenantId);
        }

        [Fact]
        public async Task GetActiveLeasesAsync_ReturnsOnlyActiveLeases()
        {
            // Arrange - Create leases with different statuses
            var pendingLease = new Lease
            {
                OrganizationId = _testOrgId,
                PropertyId = _testPropertyId,
                TenantId = _testTenantId,
                StartDate = DateTime.Today.AddMonths(1),
                EndDate = DateTime.Today.AddMonths(13),
                MonthlyRent = 1400,
                Status = ApplicationConstants.LeaseStatuses.Pending,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(pendingLease);

            // Create a second property and tenant for active lease
            var property2 = new Property
            {
                Id = Guid.NewGuid(),
                OrganizationId = _testOrgId,
                Address = "456 Test Ave",
                City = "Test City",
                State = "TS",
                ZipCode = "12345",
                IsActive = true,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            _context.Properties.Add(property2);

            var tenant2 = new Tenant
            {
                Id = Guid.NewGuid(),
                OrganizationId = _testOrgId,
                FirstName = "Active",
                LastName = "Tenant",
                Email = "active@test.com",
                IdentificationNumber = "SSN789012",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            _context.Tenants.Add(tenant2);
            await _context.SaveChangesAsync();

            var activeLease = new Lease
            {
                OrganizationId = _testOrgId,
                PropertyId = property2.Id,
                TenantId = tenant2.Id,
                StartDate = DateTime.Today.AddMonths(-1),
                EndDate = DateTime.Today.AddMonths(11),
                MonthlyRent = 1500,
                Status = ApplicationConstants.LeaseStatuses.Active,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(activeLease);

            // Act
            var result = await _service.GetActiveLeasesAsync();

            // Assert
            Assert.Single(result);
            Assert.Equal(ApplicationConstants.LeaseStatuses.Active, result[0].Status);
            Assert.True(result[0].StartDate <= DateTime.Today);
            Assert.True(result[0].EndDate >= DateTime.Today);
        }

        [Fact]
        public async Task GetLeasesExpiringSoonAsync_ReturnsLeasesWithinThreshold()
        {
            // Arrange - Create leases with different expiration dates
            // Create additional property
            var property2 = new Property
            {
                Id = Guid.NewGuid(),
                OrganizationId = _testOrgId,
                Address = "789 Test Blvd",
                City = "Test City",
                State = "TS",
                ZipCode = "12345",
                IsActive = true,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            _context.Properties.Add(property2);

            var tenant2 = new Tenant
            {
                Id = Guid.NewGuid(),
                OrganizationId = _testOrgId,
                FirstName = "Expiring",
                LastName = "Tenant",
                Email = "expiring@test.com",
                IdentificationNumber = "SSN345678",
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            _context.Tenants.Add(tenant2);
            await _context.SaveChangesAsync();

            // Lease expiring in 30 days (within 90-day threshold)
            var expiringSoonLease = new Lease
            {
                OrganizationId = _testOrgId,
                PropertyId = _testPropertyId,
                TenantId = _testTenantId,
                StartDate = DateTime.Today.AddYears(-1),
                EndDate = DateTime.Today.AddDays(30),
                MonthlyRent = 1400,
                Status = ApplicationConstants.LeaseStatuses.Active,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(expiringSoonLease);

            // Lease expiring in 6 months (outside 90-day threshold)
            var farOutLease = new Lease
            {
                OrganizationId = _testOrgId,
                PropertyId = property2.Id,
                TenantId = tenant2.Id,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddMonths(6),
                MonthlyRent = 1500,
                Status = ApplicationConstants.LeaseStatuses.Active,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(farOutLease);

            // Act
            var result = await _service.GetLeasesExpiringSoonAsync(90);

            // Assert
            Assert.Single(result);
            Assert.Equal(expiringSoonLease.Id, result[0].Id);
        }

        [Fact]
        public async Task GetLeasesByStatusAsync_ReturnsLeasesWithStatus()
        {
            // Arrange - Create leases with different statuses
            var activeLease = new Lease
            {
                OrganizationId = _testOrgId,
                PropertyId = _testPropertyId,
                TenantId = _testTenantId,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddYears(1),
                MonthlyRent = 1500,
                Status = ApplicationConstants.LeaseStatuses.Active,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(activeLease);

            // Create second property for pending lease
            var property2 = new Property
            {
                Id = Guid.NewGuid(),
                OrganizationId = _testOrgId,
                Address = "321 Pending Rd",
                City = "Test City",
                State = "TS",
                ZipCode = "12345",
                IsActive = true,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            _context.Properties.Add(property2);
            await _context.SaveChangesAsync();

            var pendingLease = new Lease
            {
                OrganizationId = _testOrgId,
                PropertyId = property2.Id,
                TenantId = _testTenantId,
                StartDate = DateTime.Today.AddMonths(1),
                EndDate = DateTime.Today.AddMonths(13),
                MonthlyRent = 1400,
                Status = ApplicationConstants.LeaseStatuses.Pending,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(pendingLease);

            // Act
            var activeResults = await _service.GetLeasesByStatusAsync(ApplicationConstants.LeaseStatuses.Active);
            var pendingResults = await _service.GetLeasesByStatusAsync(ApplicationConstants.LeaseStatuses.Pending);

            // Assert
            Assert.Single(activeResults);
            Assert.Single(pendingResults);
            Assert.Equal(ApplicationConstants.LeaseStatuses.Active, activeResults[0].Status);
            Assert.Equal(ApplicationConstants.LeaseStatuses.Pending, pendingResults[0].Status);
        }

        [Fact]
        public async Task GetLeaseWithRelationsAsync_LoadsAllRelations()
        {
            // Arrange - Create lease
            var lease = new Lease
            {
                OrganizationId = _testOrgId,
                PropertyId = _testPropertyId,
                TenantId = _testTenantId,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddYears(1),
                MonthlyRent = 1500,
                Status = ApplicationConstants.LeaseStatuses.Active,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            var created = await _service.CreateAsync(lease);

            // Act
            var result = await _service.GetLeaseWithRelationsAsync(created.Id);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Property);
            Assert.NotNull(result.Tenant);
            Assert.Equal(_testPropertyId, result.Property.Id);
            Assert.Equal(_testTenantId, result.Tenant!.Id);
        }

        #endregion

        #region Business Logic Tests

        [Fact]
        public async Task CalculateTotalLeaseValueAsync_CalculatesCorrectly()
        {
            // Arrange - Create 12-month lease
            var lease = new Lease
            {
                OrganizationId = _testOrgId,
                PropertyId = _testPropertyId,
                TenantId = _testTenantId,
                StartDate = new DateTime(2025, 1, 1),
                EndDate = new DateTime(2025, 12, 31),
                MonthlyRent = 1500,
                Status = ApplicationConstants.LeaseStatuses.Active,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            var created = await _service.CreateAsync(lease);

            // Act
            var totalValue = await _service.CalculateTotalLeaseValueAsync(created.Id);

            // Assert
            // 12 months * $1500 = $18,000
            Assert.Equal(18000, totalValue);
        }

        [Fact]
        public async Task UpdateLeaseStatusAsync_UpdatesStatusAndPropertyAvailability()
        {
            // Arrange - Create pending lease
            var lease = new Lease
            {
                OrganizationId = _testOrgId,
                PropertyId = _testPropertyId,
                TenantId = _testTenantId,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddYears(1),
                MonthlyRent = 1500,
                Status = ApplicationConstants.LeaseStatuses.Pending,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            var created = await _service.CreateAsync(lease);

            // Act - Change to Active
            var updated = await _service.UpdateLeaseStatusAsync(created.Id, ApplicationConstants.LeaseStatuses.Active);

            // Assert
            Assert.Equal(ApplicationConstants.LeaseStatuses.Active, updated.Status);
            var property = await _context.Properties.FindAsync(_testPropertyId);
            Assert.False(property!.IsActive);
        }

        [Fact]
        public async Task UpdateLeaseStatusAsync_ToTerminated_MarksPropertyAvailable()
        {
            // Arrange - Create active lease
            var lease = new Lease
            {
                OrganizationId = _testOrgId,
                PropertyId = _testPropertyId,
                TenantId = _testTenantId,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddYears(1),
                MonthlyRent = 1500,
                Status = ApplicationConstants.LeaseStatuses.Active,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            var created = await _service.CreateAsync(lease);

            // Act - Change to Terminated
            await _service.UpdateLeaseStatusAsync(created.Id, ApplicationConstants.LeaseStatuses.Terminated);

            // Assert
            var property = await _context.Properties.FindAsync(_testPropertyId);
            Assert.True(property!.IsActive);
        }

        #endregion

        #region Organization Isolation Tests

        [Fact]
        public async Task GetByIdAsync_DifferentOrganization_ReturnsNull()
        {
            // Arrange - Create different organization and lease
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
                Email = "other@example.com",
                IdentificationNumber = "SSN999999",
                CreatedBy = otherUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _context.Tenants.AddAsync(otherTenant);

            var otherOrgLease = new Lease
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrg.Id,
                PropertyId = otherProperty.Id,
                TenantId = otherTenant.Id,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddYears(1),
                MonthlyRent = 2000,
                Status = ApplicationConstants.LeaseStatuses.Active,
                CreatedBy = otherUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _context.Leases.AddAsync(otherOrgLease);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetByIdAsync(otherOrgLease.Id);

            // Assert
            Assert.Null(result); // Should not access lease from different org
        }

        [Fact]
        public async Task GetAllAsync_ReturnsOnlyCurrentOrganizationLeases()
        {
            // Arrange - Create lease in test org
            var testOrgLease = new Lease
            {
                OrganizationId = _testOrgId,
                PropertyId = _testPropertyId,
                TenantId = _testTenantId,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddYears(1),
                MonthlyRent = 1500,
                Status = ApplicationConstants.LeaseStatuses.Active,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(testOrgLease);

            // Create lease in different org
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
                LastName = "Person",
                Email = "otherperson@example.com",
                IdentificationNumber = "SSN888888",
                CreatedBy = otherUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _context.Tenants.AddAsync(otherTenant);

            var otherOrgLease = new Lease
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrg.Id,
                PropertyId = otherProperty.Id,
                TenantId = otherTenant.Id,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddYears(1),
                MonthlyRent = 2000,
                Status = ApplicationConstants.LeaseStatuses.Active,
                CreatedBy = otherUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _context.Leases.AddAsync(otherOrgLease);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetAllAsync();

            // Assert
            Assert.Single(result);
            Assert.Equal(_testOrgId, result[0].Property.OrganizationId);
        }

        #endregion
    }
}
