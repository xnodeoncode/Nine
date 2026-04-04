using Nine.Application.Services;
using Nine.Core.Constants;
using Nine.Core.Entities;
using Nine.Core.Interfaces;
using Nine.Core.Interfaces.Services;
using Nine.Entities;
using Nine.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.ComponentModel.DataAnnotations;

namespace Nine.Application.Tests
{
    public class MaintenanceServiceTests : IDisposable
    {
        private readonly TestApplicationDbContext _context;
        private readonly MaintenanceService _service;
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<ILogger<MaintenanceService>> _mockLogger;
        private readonly Mock<ICalendarEventService> _mockCalendarEventService;
        private readonly Mock<IUserContextService> _mockUserContext;
        private readonly IOptions<ApplicationSettings> _mockSettings;
        private readonly SqliteConnection _connection;
        private readonly ApplicationUser _testUser;
        private readonly Organization _testOrg;

        private readonly Guid _testOrgId = Guid.NewGuid();

        private readonly string _testUserId = Guid.NewGuid().ToString();

        private readonly Guid _testPropertyId = Guid.NewGuid();

        private readonly Guid _testLeaseId = Guid.NewGuid();

        private readonly Guid _testTenantId = Guid.NewGuid();

        private readonly Property _testProperty;
        private readonly Lease _testLease;

        private readonly Tenant _testTenant;

        public MaintenanceServiceTests()
        {
            // Create in-memory SQLite database
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new TestApplicationDbContext(options);
            _context.Database.EnsureCreated();

            // Setup test data
            _testUser = new ApplicationUser
            {
                Id = _testUserId,
                UserName = "testuser@test.com",
                Email = "testuser@test.com"
            };

            _testOrg = new Organization
            {
                Id = _testOrgId,
                Name = "Test Organization",
                OwnerId = _testUserId,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };

            _testProperty = new Property
            {
                Id = _testPropertyId,
                OrganizationId = _testOrg.Id,
                Address = "123 Test St",
                City = "Test City",
                State = "TS",
                ZipCode = "12345",
                PropertyType = "Single Family",
                Bedrooms = 3,
                Bathrooms = 2,
                SquareFeet = 1500,
                IsActive = true,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };


            _testTenant = new Tenant
            {
                Id = _testTenantId,
                OrganizationId = _testOrg.Id,
                FirstName = "Test",
                LastName = "Tenant",
                Email = "testtenant@test.com",
                PhoneNumber = "123-456-7890",
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };

            _testLease = new Lease
            {
                Id = _testLeaseId,
                PropertyId = _testProperty.Id,
                OrganizationId = _testOrg.Id,
                TenantId = _testTenant.Id,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddMonths(12),
                MonthlyRent = 1500,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };


            _context.Users.Add(_testUser);
            _context.SaveChanges(); // Save user first so OwnerId foreign key can be satisfied

            _context.Organizations.Add(_testOrg);
            _context.Properties.Add(_testProperty);
            _context.Tenants.Add(_testTenant);
            _context.Leases.Add(_testLease);
            _context.SaveChanges();

            // Setup mocks
            var userStore = new Mock<IUserStore<ApplicationUser>>();
            _mockUserManager = new Mock<UserManager<ApplicationUser>>(
                userStore.Object, null, null, null, null, null, null, null, null);

            _testUser.ActiveOrganizationId = _testOrg.Id;
            _mockUserManager.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
                .ReturnsAsync(_testUser);

            // Mock IUserContextService
            _mockUserContext = new Mock<IUserContextService>();
            _mockUserContext.Setup(x => x.GetUserIdAsync())
                .ReturnsAsync(_testUserId);
            _mockUserContext.Setup(x => x.GetActiveOrganizationIdAsync())
                .ReturnsAsync(_testOrgId);
            _mockUserContext.Setup(x => x.GetUserNameAsync())
                .ReturnsAsync("testuser@test.com");
            _mockUserContext.Setup(x => x.GetUserEmailAsync())
                .ReturnsAsync("testuser@test.com");
            _mockUserContext.Setup(x => x.GetOrganizationIdAsync())
                .ReturnsAsync(_testOrgId);

            _mockLogger = new Mock<ILogger<MaintenanceService>>();

            _mockSettings = Options.Create(new ApplicationSettings
            {
                SoftDeleteEnabled = true
            });
            
            _mockCalendarEventService = new Mock<ICalendarEventService>();

            _service = new MaintenanceService(
                _context,
                _mockLogger.Object,
                _mockUserContext.Object,
                _mockSettings,
                _mockCalendarEventService.Object);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _connection.Close();
            _connection.Dispose();
        }

        #region Validation Tests

        [Fact]
        public async Task CreateAsync_MissingPropertyId_ThrowsException()
        {
            // Arrange
            var maintenanceRequest = new MaintenanceRequest
            {
                PropertyId = Guid.Empty,
                Title = "Test Request",
                Description = "Test Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Plumbing,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.Medium,
                Status = ApplicationConstants.MaintenanceRequestStatuses.Submitted,
                RequestedOn = DateTime.Today,
                OrganizationId = _testOrg.Id
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(maintenanceRequest));
        }

        [Fact]
        public async Task CreateAsync_MissingTitle_ThrowsException()
        {
            // Arrange
            var maintenanceRequest = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "",
                Description = "Test Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Plumbing,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.Medium,
                Status = ApplicationConstants.MaintenanceRequestStatuses.Submitted,
                RequestedOn = DateTime.Today,
                OrganizationId = _testOrg.Id
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(maintenanceRequest));
        }

        [Fact]
        public async Task CreateAsync_MissingDescription_ThrowsException()
        {
            // Arrange
            var maintenanceRequest = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "Test Request",
                Description = "",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Plumbing,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.Medium,
                Status = ApplicationConstants.MaintenanceRequestStatuses.Submitted,
                RequestedOn = DateTime.Today,
                OrganizationId = _testOrg.Id
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(maintenanceRequest));
        }

        [Fact]
        public async Task CreateAsync_InvalidPriority_ThrowsException()
        {
            // Arrange
            var maintenanceRequest = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "Test Request",
                Description = "Test Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Plumbing,
                Priority = "InvalidPriority",
                Status = ApplicationConstants.MaintenanceRequestStatuses.Submitted,
                RequestedOn = DateTime.Today,
                OrganizationId = _testOrg.Id
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(maintenanceRequest));
        }

        [Fact]
        public async Task CreateAsync_InvalidStatus_ThrowsException()
        {
            // Arrange
            var maintenanceRequest = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "Test Request",
                Description = "Test Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Plumbing,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.Medium,
                Status = "InvalidStatus",
                RequestedOn = DateTime.Today,
                OrganizationId = _testOrg.Id
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(maintenanceRequest));
        }

        [Fact]
        public async Task CreateAsync_FutureRequestedDate_ThrowsException()
        {
            // Arrange
            var maintenanceRequest = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "Test Request",
                Description = "Test Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Plumbing,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.Medium,
                Status = ApplicationConstants.MaintenanceRequestStatuses.Submitted,
                RequestedOn = DateTime.Today.AddDays(1),
                OrganizationId = _testOrg.Id
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(maintenanceRequest));
        }

        [Fact]
        public async Task CreateAsync_ScheduledBeforeRequested_ThrowsException()
        {
            // Arrange
            var maintenanceRequest = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "Test Request",
                Description = "Test Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Plumbing,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.Medium,
                Status = ApplicationConstants.MaintenanceRequestStatuses.Submitted,
                RequestedOn = DateTime.Today,
                ScheduledOn = DateTime.Today.AddDays(-1),
                OrganizationId = _testOrg.Id
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(maintenanceRequest));
        }

        [Fact]
        public async Task CreateAsync_NegativeEstimatedCost_ThrowsException()
        {
            // Arrange
            var maintenanceRequest = new MaintenanceRequest
            {
                PropertyId = _testPropertyId,
                LeaseId = _testLeaseId,
                Title = "Test Request",
                Description = "Test Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Plumbing,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.Medium,
                Status = ApplicationConstants.MaintenanceRequestStatuses.Submitted,
                RequestedOn = DateTime.Today,
                EstimatedCost = -100,
                OrganizationId = _testOrg.Id
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(maintenanceRequest));
        }

        [Fact]
        public async Task CreateAsync_CompletedWithoutDate_ThrowsException()
        {
            // Arrange
            var maintenanceRequest = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "Test Request",
                Description = "Test Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Plumbing,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.Medium,
                Status = ApplicationConstants.MaintenanceRequestStatuses.Completed,
                RequestedOn = DateTime.Today,
                CompletedOn = null,
                OrganizationId = _testOrg.Id
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(maintenanceRequest));
        }

        [Fact]
        public async Task CreateAsync_InvalidPropertyOrganization_ThrowsException()
        {
            // Arrange
            var otherOrg = new Organization
            {
                Id = Guid.NewGuid(),
                Name = "Other Org",
                OwnerId = _testUserId,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };
            _context.Organizations.Add(otherOrg);
            
            var otherProperty = new Property
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrg.Id,
                Address = "999 Other St",
                City = "Other City",
                State = "OT",
                ZipCode = "99999",
                PropertyType = "Apartment",
                Bedrooms = 2,
                Bathrooms = 1,
                SquareFeet = 900,
                IsActive = true,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };
            _context.Properties.Add(otherProperty);
            await _context.SaveChangesAsync();

            var maintenanceRequest = new MaintenanceRequest
            {
                PropertyId = otherProperty.Id, // Property from different org
                Title = "Test Request",
                Description = "Test Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Plumbing,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.Medium,
                Status = ApplicationConstants.MaintenanceRequestStatuses.Submitted,
                RequestedOn = DateTime.Today
                // OrganizationId will be auto-set from user context, which won't match property's org
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(maintenanceRequest));
        }

        [Fact]
        public async Task CreateAsync_ValidMaintenanceRequest_CreatesSuccessfully()
        {
            // Arrange
            var maintenanceRequest = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "Leaky Faucet",
                Description = "Kitchen faucet is dripping",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Plumbing,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.Medium,
                Status = ApplicationConstants.MaintenanceRequestStatuses.Submitted,
                RequestedOn = DateTime.Today,
                EstimatedCost = 150,
                OrganizationId = _testOrg.Id
            };

            // Act
            var result = await _service.CreateAsync(maintenanceRequest);

            // Assert
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.Id);
            Assert.Equal("Leaky Faucet", result.Title);
            Assert.Equal(_testOrg.Id, result.OrganizationId);
            
            // Verify calendar event was created
            _mockCalendarEventService.Verify(
                x => x.CreateOrUpdateEventAsync(It.IsAny<MaintenanceRequest>()), 
                Times.Once);
        }

        #endregion

        #region Retrieval Tests

        [Fact]
        public async Task GetMaintenanceRequestsByPropertyAsync_ReturnsPropertyRequests()
        {
            // Arrange
            var request1 = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "Request 1",
                Description = "Description 1",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Plumbing,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.Medium,
                Status = ApplicationConstants.MaintenanceRequestStatuses.Submitted,
                RequestedOn = DateTime.Today,
                OrganizationId = _testOrg.Id,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };

            var request2 = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "Request 2",
                Description = "Description 2",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Electrical,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.High,
                Status = ApplicationConstants.MaintenanceRequestStatuses.InProgress,
                RequestedOn = DateTime.Today.AddDays(-1),
                OrganizationId = _testOrg.Id,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };

            await _service.CreateAsync(request1);
            await _service.CreateAsync(request2);

            // Act
            var results = await _service.GetMaintenanceRequestsByPropertyAsync(_testProperty.Id);

            // Assert
            Assert.Equal(2, results.Count);
            Assert.All(results, r => Assert.Equal(_testProperty.Id, r.PropertyId));
        }

        [Fact]
        public async Task GetMaintenanceRequestsByStatusAsync_ReturnsMatchingRequests()
        {
            // Arrange
            var submitted = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "Submitted Request",
                Description = "Description",
                RequestType = "Plumbing",
                Priority = "Medium",
                Status = "Submitted",
                RequestedOn = DateTime.Today,
                OrganizationId = _testOrg.Id,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };

            var inProgress = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "In Progress Request",
                Description = "Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Electrical,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.High,
                Status = ApplicationConstants.MaintenanceRequestStatuses.InProgress,
                RequestedOn = DateTime.Today,
                OrganizationId = _testOrg.Id,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };

            await _service.CreateAsync(submitted);
            await _service.CreateAsync(inProgress);

            // Act
            var results = await _service.GetMaintenanceRequestsByStatusAsync("Submitted");

            // Assert
            Assert.Single(results);
            Assert.Equal("Submitted", results[0].Status);
        }

        [Fact]
        public async Task GetMaintenanceRequestsByPriorityAsync_ReturnsMatchingRequests()
        {
            // Arrange
            var urgent = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "Urgent Request",
                Description = "Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Plumbing,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.Urgent,
                Status = ApplicationConstants.MaintenanceRequestStatuses.Submitted,
                RequestedOn = DateTime.Today,
                OrganizationId = _testOrg.Id,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };

            var low = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "Low Request",
                Description = "Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Other,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.Low,
                Status = ApplicationConstants.MaintenanceRequestStatuses.Submitted,
                RequestedOn = DateTime.Today,
                OrganizationId = _testOrg.Id,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };

            await _service.CreateAsync(urgent);
            await _service.CreateAsync(low);

            // Act
            var results = await _service.GetMaintenanceRequestsByPriorityAsync("Urgent");

            // Assert
            Assert.Single(results);
            Assert.Equal("Urgent", results[0].Priority);
        }

        [Fact]
        public async Task GetOverdueMaintenanceRequestsAsync_ReturnsOverdueRequests()
        {
            // Arrange
            var overdue = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "Overdue Request",
                Description = "Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Plumbing,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.High,
                Status = ApplicationConstants.MaintenanceRequestStatuses.InProgress,
                RequestedOn = DateTime.Today.AddDays(-5),
                ScheduledOn = DateTime.Today.AddDays(-2),
                OrganizationId = _testOrg.Id,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };

            var notOverdue = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "Not Overdue Request",
                Description = "Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Electrical,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.Medium,
                Status = ApplicationConstants.MaintenanceRequestStatuses.Submitted,
                RequestedOn = DateTime.Today,
                ScheduledOn = DateTime.Today.AddDays(2),
                OrganizationId = _testOrg.Id,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };

            await _service.CreateAsync(overdue);
            await _service.CreateAsync(notOverdue);

            // Act
            var results = await _service.GetOverdueMaintenanceRequestsAsync();

            // Assert
            Assert.Single(results);
            Assert.Equal("Overdue Request", results[0].Title);
        }

        [Fact]
        public async Task GetMaintenanceRequestWithRelationsAsync_LoadsAllRelations()
        {
            // Arrange
            // Tenant and Lease already exist from constructor, no need to re-add them

            var request = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                LeaseId = _testLease.Id,
                Title = "Test Request",
                Description = "Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Plumbing,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.Medium,
                Status = ApplicationConstants.MaintenanceRequestStatuses.Submitted,
                RequestedOn = DateTime.Today,
                OrganizationId = _testOrg.Id,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(request);

            // Act
            var result = await _service.GetMaintenanceRequestWithRelationsAsync(request.Id);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Property);
            Assert.NotNull(result.Lease);
            Assert.NotNull(result.Lease.Tenant);
            Assert.Equal("Test", result.Lease.Tenant.FirstName);
        }

        #endregion

        #region Business Logic Tests

        [Fact]
        public async Task UpdateMaintenanceRequestStatusAsync_UpdatesStatusAndSetsCompletedDate()
        {
            // Arrange
            var request = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "Test Request",
                Description = "Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Plumbing,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.Medium,
                Status = ApplicationConstants.MaintenanceRequestStatuses.Submitted,
                RequestedOn = DateTime.Today,
                OrganizationId = _testOrg.Id,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(request);

            // Act
            var result = await _service.UpdateMaintenanceRequestStatusAsync(request.Id, "Completed");

            // Assert
            Assert.Equal("Completed", result.Status);
            Assert.NotNull(result.CompletedOn);
            Assert.Equal(DateTime.Today, result.CompletedOn.Value.Date);
        }

        [Fact]
        public async Task AssignMaintenanceRequestAsync_UpdatesAssignmentAndStatus()
        {
            // Arrange
            var request = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "Test Request",
                Description = "Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Plumbing,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.Medium,
                Status = ApplicationConstants.MaintenanceRequestStatuses.Submitted,
                RequestedOn = DateTime.Today,
                OrganizationId = _testOrg.Id,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(request);

            // Act
            var scheduledDate = DateTime.Today.AddDays(2);
            var result = await _service.AssignMaintenanceRequestAsync(
                request.Id, 
                "John Smith", 
                scheduledDate);

            // Assert
            Assert.Equal("John Smith", result.AssignedTo);
            Assert.Equal(scheduledDate, result.ScheduledOn);
            Assert.Equal("In Progress", result.Status);
        }

        [Fact]
        public async Task CompleteMaintenanceRequestAsync_UpdatesAllCompletionFields()
        {
            // Arrange
            var request = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "Test Request",
                Description = "Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Plumbing,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.Medium,
                Status = ApplicationConstants.MaintenanceRequestStatuses.InProgress,
                RequestedOn = DateTime.Today.AddDays(-3),
                EstimatedCost = 200,
                OrganizationId = _testOrg.Id,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(request);

            // Act
            var result = await _service.CompleteMaintenanceRequestAsync(
                request.Id,
                175.50m,
                "Fixed the leak and replaced washers");

            // Assert
            Assert.Equal("Completed", result.Status);
            Assert.Equal(175.50m, result.ActualCost);
            Assert.Equal("Fixed the leak and replaced washers", result.ResolutionNotes);
            Assert.NotNull(result.CompletedOn);
        }

        [Fact]
        public async Task GetOpenMaintenanceRequestCountAsync_ReturnsCorrectCount()
        {
            // Arrange
            var submitted = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "Submitted",
                Description = "Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Plumbing,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.Medium,
                Status = ApplicationConstants.MaintenanceRequestStatuses.Submitted,
                RequestedOn = DateTime.Today,
                OrganizationId = _testOrg.Id,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };

            var inProgress = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "In Progress",
                Description = "Description",
                RequestType = "Electrical",
                Priority = "High",
                Status = "In Progress",
                RequestedOn = DateTime.Today,
                OrganizationId = _testOrg.Id,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };

            var completed = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "Completed",
                Description = "Description",
                RequestType = "Other",
                Priority = "Low",
                Status = "Completed",
                RequestedOn = DateTime.Today.AddDays(-5),
                CompletedOn = DateTime.Today,
                OrganizationId = _testOrg.Id,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };

            await _service.CreateAsync(submitted);
            await _service.CreateAsync(inProgress);
            await _service.CreateAsync(completed);

            // Act
            var count = await _service.GetOpenMaintenanceRequestCountAsync();

            // Assert
            Assert.Equal(2, count); // Only submitted and in progress
        }

        [Fact]
        public async Task GetUrgentMaintenanceRequestCountAsync_ReturnsCorrectCount()
        {
            // Arrange
            var urgent1 = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "Urgent 1",
                Description = "Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Plumbing,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.Urgent,
                Status = ApplicationConstants.MaintenanceRequestStatuses.Submitted,
                RequestedOn = DateTime.Today,
                OrganizationId = _testOrg.Id,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };

            var urgent2 = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "Urgent 2",
                Description = "Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Electrical,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.Urgent,
                Status = ApplicationConstants.MaintenanceRequestStatuses.InProgress,
                RequestedOn = DateTime.Today,
                OrganizationId = _testOrg.Id,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };

            var high = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "High Priority",
                Description = "Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Other,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.High,
                Status = ApplicationConstants.MaintenanceRequestStatuses.Submitted,
                RequestedOn = DateTime.Today,
                OrganizationId = _testOrg.Id,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };

            await _service.CreateAsync(urgent1);
            await _service.CreateAsync(urgent2);
            await _service.CreateAsync(high);

            // Act
            var count = await _service.GetUrgentMaintenanceRequestCountAsync();

            // Assert
            Assert.Equal(2, count);
        }

        [Fact]
        public async Task GetMaintenanceRequestsByAssigneeAsync_ReturnsSortedByPriority()
        {
            // Arrange
            var low = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "Low Priority",
                Description = "Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Other,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.Low,
                Status = ApplicationConstants.MaintenanceRequestStatuses.InProgress,
                AssignedTo = "John Smith",
                RequestedOn = DateTime.Today,
                OrganizationId = _testOrg.Id,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };

            var urgent = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "Urgent Request",
                Description = "Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Plumbing,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.Urgent,
                Status = ApplicationConstants.MaintenanceRequestStatuses.Submitted,
                AssignedTo = "John Smith",
                RequestedOn = DateTime.Today,
                OrganizationId = _testOrg.Id,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };

            var high = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "High Priority",
                Description = "Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Electrical,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.High,
                Status = ApplicationConstants.MaintenanceRequestStatuses.InProgress,
                AssignedTo = "John Smith",
                RequestedOn = DateTime.Today,
                OrganizationId = _testOrg.Id,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };

            await _service.CreateAsync(low);
            await _service.CreateAsync(urgent);
            await _service.CreateAsync(high);

            // Act
            var results = await _service.GetMaintenanceRequestsByAssigneeAsync("John Smith");

            // Assert
            Assert.Equal(3, results.Count);
            Assert.Equal("Urgent", results[0].Priority); // Urgent first
            Assert.Equal("High", results[1].Priority);   // High second
            Assert.Equal("Low", results[2].Priority);    // Low last
        }

        [Fact]
        public async Task CalculateAverageDaysToCompleteAsync_ReturnsCorrectAverage()
        {
            // Arrange
            var completed1 = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "Request 1",
                Description = "Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Plumbing,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.Medium,
                Status = ApplicationConstants.MaintenanceRequestStatuses.Completed,
                RequestedOn = DateTime.Today.AddDays(-10),
                CompletedOn = DateTime.Today.AddDays(-5), // 5 days
                OrganizationId = _testOrg.Id,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };

            var completed2 = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "Request 2",
                Description = "Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Electrical,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.High,
                Status = ApplicationConstants.MaintenanceRequestStatuses.Completed,
                RequestedOn = DateTime.Today.AddDays(-7),
                CompletedOn = DateTime.Today, // 7 days
                OrganizationId = _testOrg.Id,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };

            await _service.CreateAsync(completed1);
            await _service.CreateAsync(completed2);

            // Act
            var average = await _service.CalculateAverageDaysToCompleteAsync();

            // Assert
            Assert.Equal(6.0, average); // (5 + 7) / 2 = 6
        }

        [Fact]
        public async Task GetMaintenanceCostsByPropertyAsync_ReturnsCorrectTotals()
        {
            // Arrange
            var property2 = new Property
            {
                Id = Guid.NewGuid(),
                OrganizationId = _testOrg.Id,
                Address = "456 Other St",
                City = "Test City",
                State = "TS",
                ZipCode = "12345",
                PropertyType = "Condo",
                Bedrooms = 2,
                Bathrooms = 1,
                SquareFeet = 1000,
                IsActive = true,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };
            _context.Properties.Add(property2);
            await _context.SaveChangesAsync();

            var request1 = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "Request 1",
                Description = "Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Plumbing,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.Medium,
                Status = ApplicationConstants.MaintenanceRequestStatuses.Completed,
                RequestedOn = DateTime.Today.AddDays(-10),
                CompletedOn = DateTime.Today,
                ActualCost = 150,
                OrganizationId = _testOrg.Id,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };

            var request2 = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "Request 2",
                Description = "Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Electrical,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.High,
                Status = ApplicationConstants.MaintenanceRequestStatuses.Completed,
                RequestedOn = DateTime.Today.AddDays(-5),
                CompletedOn = DateTime.Today,
                ActualCost = 200,
                OrganizationId = _testOrg.Id,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };

            var request3 = new MaintenanceRequest
            {
                PropertyId = property2.Id,
                Title = "Request 3",
                Description = "Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Other,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.Low,
                Status = ApplicationConstants.MaintenanceRequestStatuses.Completed,
                RequestedOn = DateTime.Today.AddDays(-3),
                CompletedOn = DateTime.Today,
                ActualCost = 75,
                OrganizationId = _testOrg.Id,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };

            await _service.CreateAsync(request1);
            await _service.CreateAsync(request2);
            await _service.CreateAsync(request3);

            // Act
            var costs = await _service.GetMaintenanceCostsByPropertyAsync();

            // Assert
            Assert.Equal(2, costs.Count);
            Assert.Equal(350m, costs[_testProperty.Id]); // 150 + 200
            Assert.Equal(75m, costs[property2.Id]);
        }

        [Fact]
        public async Task DeleteAsync_RemovesCalendarEvent()
        {
            // Arrange
            var request = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "Test Request",
                Description = "Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Plumbing,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.Medium,
                Status = ApplicationConstants.MaintenanceRequestStatuses.Submitted,
                RequestedOn = DateTime.Today,
                OrganizationId = _testOrg.Id,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(request);

            // Act
            await _service.DeleteAsync(request.Id);

            // Assert
            _mockCalendarEventService.Verify(
                x => x.DeleteEventAsync(It.IsAny<Guid?>()), 
                Times.Once);
        }

        #endregion

        #region Organization Isolation Tests

        [Fact]
        public async Task GetByIdAsync_DifferentOrganization_ReturnsNull()
        {
            // Arrange
            var otherOrg = new Organization
            {
                Id = Guid.NewGuid(),
                Name = "Other Organization",
                OwnerId = _testUserId,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };
            _context.Organizations.Add(otherOrg);
            await _context.SaveChangesAsync();

            var otherProperty = new Property
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrg.Id,
                Address = "999 Other St",
                City = "Other City",
                State = "OT",
                ZipCode = "99999",
                PropertyType = "Condo",
                Bedrooms = 2,
                Bathrooms = 1,
                SquareFeet = 900,
                IsActive = true,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };
            _context.Properties.Add(otherProperty);
            await _context.SaveChangesAsync();

            var otherRequest = new MaintenanceRequest
            {
                PropertyId = otherProperty.Id,
                Title = "Other Request",
                Description = "Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Plumbing,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.Medium,
                Status = ApplicationConstants.MaintenanceRequestStatuses.Submitted,
                RequestedOn = DateTime.Today,
                OrganizationId = otherOrg.Id,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };
            _context.MaintenanceRequests.Add(otherRequest);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetByIdAsync(otherRequest.Id);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsOnlyCurrentOrganizationRequests()
        {
            // Arrange
            var myRequest = new MaintenanceRequest
            {
                PropertyId = _testProperty.Id,
                Title = "My Request",
                Description = "Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Plumbing,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.Medium,
                Status = ApplicationConstants.MaintenanceRequestStatuses.Submitted,
                RequestedOn = DateTime.Today,
                OrganizationId = _testOrg.Id,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(myRequest);

            var otherOrg = new Organization
            {
                Id = Guid.NewGuid(),
                Name = "Other Organization",
                OwnerId = _testUserId,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };
            _context.Organizations.Add(otherOrg);
            await _context.SaveChangesAsync();

            var otherProperty = new Property
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrg.Id,
                Address = "999 Other St",
                City = "Other City",
                State = "OT",
                ZipCode = "99999",
                PropertyType = "Condo",
                Bedrooms = 2,
                Bathrooms = 1,
                SquareFeet = 900,
                IsActive = true,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };
            _context.Properties.Add(otherProperty);

            var otherRequest = new MaintenanceRequest
            {
                PropertyId = otherProperty.Id,
                Title = "Other Request",
                Description = "Description",
                RequestType = ApplicationConstants.MaintenanceRequestTypes.Electrical,
                Priority = ApplicationConstants.MaintenanceRequestPriorities.High,
                Status = ApplicationConstants.MaintenanceRequestStatuses.InProgress,
                RequestedOn = DateTime.Today,
                OrganizationId = otherOrg.Id,
                CreatedBy = _testUser.Id,
                CreatedOn = DateTime.UtcNow
            };
            _context.MaintenanceRequests.Add(otherRequest);
            await _context.SaveChangesAsync();

            // Act
            var results = await _service.GetAllAsync();

            // Assert
            Assert.Single(results);
            Assert.Equal(_testOrg.Id, results[0].OrganizationId);
            Assert.Equal("My Request", results[0].Title);
        }

        #endregion
    }
}
