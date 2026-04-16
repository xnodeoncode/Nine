using Nine.Core.Entities;
using Nine.Core.Constants;
using Nine.Core.Interfaces.Services;
using Nine.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;

// Alias for the workflow enum to avoid ambiguity with Core.Constants.LeaseStatus
using WorkflowLeaseStatus = Nine.Application.Services.Workflows.LeaseStatus;
using Nine.Infrastructure.Data;
using Nine.Entities;
using Nine.Application.Services.Workflows;
using Nine.Application.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nine.Application.Tests;

/// <summary>
/// Comprehensive tests for LeaseWorkflowService covering:
/// - Lease activation flow
/// - Termination notice recording
/// - Month-to-month conversion
/// - Lease renewal workflow
/// - Move-out completion
/// - Early termination scenarios
/// - Security deposit settlement
/// - State machine validation
/// </summary>
public class LeaseWorkflowServiceTests
{
    #region Test Infrastructure

    private static async Task<TestContext> CreateTestContextAsync()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var testUserId = "test-user-id";
        var orgId = Guid.NewGuid();

        // Mock IUserContextService
        var mockUserContext = new Mock<IUserContextService>();
        mockUserContext.Setup(x => x.GetUserIdAsync())
            .ReturnsAsync(testUserId);
        mockUserContext.Setup(x => x.GetActiveOrganizationIdAsync())
            .ReturnsAsync(orgId);
        mockUserContext.Setup(x => x.GetUserNameAsync())
            .ReturnsAsync("testuser");
        mockUserContext.Setup(x => x.GetUserEmailAsync())
            .ReturnsAsync("t@t.com");
        mockUserContext.Setup(x => x.GetOrganizationIdAsync())
            .ReturnsAsync(orgId);

        TestApplicationDbContext context = new TestApplicationDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var appUserEntity = new ApplicationUser { Id = testUserId, UserName = "testuser", Email = "t@t.com", ActiveOrganizationId = orgId };
        context.Users.Add(appUserEntity);

        var org = new Organization { Id = orgId, Name = "TestOrg", OwnerId = testUserId, CreatedBy = testUserId, CreatedOn = DateTime.UtcNow };
        context.Organizations.Add(org);
        await context.SaveChangesAsync();

        var noteService = new Application.Services.NoteService(context, mockUserContext.Object);
        // In CreateTestContextAsync()
        var mockEmailService = new Mock<IEmailService>();
        var mockSmsService = new Mock<ISMSService>();

        var notificationService = new NotificationService(
            context,
            mockUserContext.Object,
            mockEmailService.Object,
            mockSmsService.Object,
            Options.Create(new ApplicationSettings { SoftDeleteEnabled = true }),
            Mock.Of<IHubContext<NotificationHub>>(),
            Mock.Of<ILogger<NotificationService>>()
        );

        var workflowService = new LeaseWorkflowService(
            context, 
            mockUserContext.Object, 
            noteService,
            notificationService
        );
        // var workflowService = new LeaseWorkflowService(context, mockUserContext.Object, noteService);

        return new TestContext
        {
            Connection = connection,
            Context = context,
            WorkflowService = workflowService,
            UserId = testUserId,
            OrgId = orgId
        };
    }

    private static async Task<(Tenant Tenant, Property Property, Lease Lease)> CreateTenantPropertyAndLeaseAsync(
        TestContext ctx,
        string leaseStatus = "Pending",
        DateTime? startDate = null,
        DateTime? endDate = null,
        decimal monthlyRent = 1500m,
        decimal securityDeposit = 1500m)
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            OrganizationId = ctx.OrgId,
            FirstName = "Test",
            LastName = "Tenant",
            Email = "tenant@test.com",
            PhoneNumber = "555-0100",
            IdentificationNumber = Guid.NewGuid().ToString("N")[..10], // Unique ID
            IsActive = leaseStatus == ApplicationConstants.LeaseStatuses.Accepted,
            CreatedBy = ctx.UserId,
            CreatedOn = DateTime.UtcNow
        };

        var property = new Property
        {
            Id = Guid.NewGuid(),
            OrganizationId = ctx.OrgId,
            Address = "123 Test St",
            City = "TestCity",
            State = "TS",
            ZipCode = "12345",
            Status = leaseStatus == ApplicationConstants.LeaseStatuses.Accepted 
                ? ApplicationConstants.PropertyStatuses.Occupied 
                : ApplicationConstants.PropertyStatuses.LeasePending,
            MonthlyRent = monthlyRent,
            CreatedBy = ctx.UserId,
            CreatedOn = DateTime.UtcNow
        };

        var lease = new Lease
        {
            Id = Guid.NewGuid(),
            OrganizationId = ctx.OrgId,
            TenantId = tenant.Id,
            PropertyId = property.Id,
            StartDate = startDate ?? DateTime.Today,
            EndDate = endDate ?? DateTime.Today.AddYears(1),
            MonthlyRent = monthlyRent,
            SecurityDeposit = securityDeposit,
            Status = leaseStatus,
            Terms = "Standard lease terms",
            CreatedBy = ctx.UserId,
            CreatedOn = DateTime.UtcNow
        };

        ctx.Context.Tenants.Add(tenant);
        ctx.Context.Properties.Add(property);
        ctx.Context.Leases.Add(lease);
        await ctx.Context.SaveChangesAsync();

        return (tenant, property, lease);
    }

    private static async Task<SecurityDeposit> CreateSecurityDepositAsync(
        TestContext ctx,
        Guid leaseId,
        Guid tenantId,
        decimal amount = 1500m,
        string status = "Held")
    {
        var deposit = new SecurityDeposit
        {
            Id = Guid.NewGuid(),
            OrganizationId = ctx.OrgId,
            LeaseId = leaseId,
            TenantId = tenantId,
            Amount = amount,
            DateReceived = DateTime.Today,
            PaymentMethod = "Check",
            Status = status,
            CreatedBy = ctx.UserId,
            CreatedOn = DateTime.UtcNow
        };

        ctx.Context.SecurityDeposits.Add(deposit);
        await ctx.Context.SaveChangesAsync();

        return deposit;
    }

    private class TestContext : IAsyncDisposable
    {
        public required Microsoft.Data.Sqlite.SqliteConnection Connection { get; init; }
        public required ApplicationDbContext Context { get; init; }
        public required LeaseWorkflowService WorkflowService { get; init; }
        public required string UserId { get; init; }
        public required Guid OrgId { get; init; }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    #endregion

    #region Lease Activation Tests

    [Fact]
    public async Task ActivateLease_Success_UpdatesLeaseAndPropertyStatus()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (tenant, property, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.Pending,
            startDate: DateTime.Today);

        // Act
        var result = await ctx.WorkflowService.ActivateLeaseAsync(lease.Id);

        // Assert
        Assert.True(result.Success, string.Join(";", result.Errors));

        var dbLease = await ctx.Context.Leases.FirstAsync(l => l.Id == lease.Id);
        Assert.Equal(ApplicationConstants.LeaseStatuses.Accepted, dbLease.Status);
        Assert.NotNull(dbLease.SignedOn);

        var dbProperty = await ctx.Context.Properties.FirstAsync(p => p.Id == property.Id);
        Assert.Equal(ApplicationConstants.PropertyStatuses.Occupied, dbProperty.Status);

        var dbTenant = await ctx.Context.Tenants.FirstAsync(t => t.Id == tenant.Id);
        Assert.True(dbTenant.IsActive);
    }

    [Fact]
    public async Task ActivateLease_FailsIfNotPending()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (_, _, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.Accepted);

        // Act
        var result = await ctx.WorkflowService.ActivateLeaseAsync(lease.Id);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Pending", result.Errors.First());
    }

    [Fact]
    public async Task ActivateLease_FailsIfStartDateTooFarInFuture()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (_, _, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.Pending,
            startDate: DateTime.Today.AddDays(60)); // More than 30 days in future

        // Act
        var result = await ctx.WorkflowService.ActivateLeaseAsync(lease.Id);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("30 days", result.Errors.First());
    }

    [Fact]
    public async Task ActivateLease_FailsIfLeaseNotFound()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();

        // Act
        var result = await ctx.WorkflowService.ActivateLeaseAsync(Guid.NewGuid());

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Errors.First().ToLower());
    }

    #endregion

    #region Termination Notice Tests

    [Fact]
    public async Task RecordTerminationNotice_Success_UpdatesLeaseStatus()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (_, _, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.Accepted);

        var noticeDate = DateTime.Today;
        var moveOutDate = DateTime.Today.AddDays(30);

        // Act
        var result = await ctx.WorkflowService.RecordTerminationNoticeAsync(
            lease.Id, noticeDate, moveOutDate, "Tenant", "Relocating for work");

        // Assert
        Assert.True(result.Success, string.Join(";", result.Errors));
        Assert.Contains("Move-out date:", result.Message);

        var dbLease = await ctx.Context.Leases.FirstAsync(l => l.Id == lease.Id);
        Assert.Equal(ApplicationConstants.LeaseStatuses.NoticeGiven, dbLease.Status);
        Assert.Equal(noticeDate, dbLease.TerminationNoticedOn);
        Assert.Equal(moveOutDate, dbLease.ExpectedMoveOutDate);
        Assert.Contains("[Tenant]", dbLease.TerminationReason!);
    }

    [Fact]
    public async Task RecordTerminationNotice_FailsIfNotActive()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (_, _, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.Pending);

        // Act
        var result = await ctx.WorkflowService.RecordTerminationNoticeAsync(
            lease.Id, DateTime.Today, DateTime.Today.AddDays(30), "Tenant", "Reason");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("active leases", result.Errors.First().ToLower());
    }

    [Fact]
    public async Task RecordTerminationNotice_FailsIfMoveOutDateInPast()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (_, _, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.Accepted);

        // Act
        var result = await ctx.WorkflowService.RecordTerminationNoticeAsync(
            lease.Id, DateTime.Today, DateTime.Today.AddDays(-1), "Tenant", "Reason");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("future", result.Errors.First().ToLower());
    }

    [Fact]
    public async Task RecordTerminationNotice_FailsWithoutReason()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (_, _, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.Accepted);

        // Act
        var result = await ctx.WorkflowService.RecordTerminationNoticeAsync(
            lease.Id, DateTime.Today, DateTime.Today.AddDays(30), "Tenant", "");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("reason is required", result.Errors.First().ToLower());
    }

    [Fact]
    public async Task RecordTerminationNotice_WorksForMonthToMonth()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (_, _, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.MonthToMonth);

        // Act
        var result = await ctx.WorkflowService.RecordTerminationNoticeAsync(
            lease.Id, DateTime.Today, DateTime.Today.AddDays(30), "Landlord", "Property being sold");

        // Assert
        Assert.True(result.Success, string.Join(";", result.Errors));
    }

    #endregion

    #region Month-to-Month Conversion Tests

    [Fact]
    public async Task ConvertToMonthToMonth_Success_FromActive()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (_, _, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.Accepted);

        // Act
        var result = await ctx.WorkflowService.ConvertToMonthToMonthAsync(lease.Id);

        // Assert
        Assert.True(result.Success, string.Join(";", result.Errors));

        var dbLease = await ctx.Context.Leases.FirstAsync(l => l.Id == lease.Id);
        Assert.Equal(ApplicationConstants.LeaseStatuses.MonthToMonth, dbLease.Status);
    }

    [Fact]
    public async Task ConvertToMonthToMonth_Success_FromExpired()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (_, _, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.Expired);

        // Act
        var result = await ctx.WorkflowService.ConvertToMonthToMonthAsync(lease.Id);

        // Assert
        Assert.True(result.Success, string.Join(";", result.Errors));
    }

    [Fact]
    public async Task ConvertToMonthToMonth_CanUpdateRent()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (_, _, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.Accepted,
            monthlyRent: 1500m);

        // Act
        var result = await ctx.WorkflowService.ConvertToMonthToMonthAsync(lease.Id, newMonthlyRent: 1600m);

        // Assert
        Assert.True(result.Success);

        var dbLease = await ctx.Context.Leases.FirstAsync(l => l.Id == lease.Id);
        Assert.Equal(1600m, dbLease.MonthlyRent);
    }

    [Fact]
    public async Task ConvertToMonthToMonth_FailsIfPending()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (_, _, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.Pending);

        // Act
        var result = await ctx.WorkflowService.ConvertToMonthToMonthAsync(lease.Id);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Active or Expired", result.Errors.First());
    }

    #endregion

    #region Lease Renewal Tests

    [Fact]
    public async Task RenewLease_Success_CreatesNewLeaseAndUpdatesOld()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (tenant, property, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.Accepted,
            endDate: DateTime.Today.AddMonths(1));

        var renewalModel = new LeaseRenewalModel
        {
            NewEndDate = DateTime.Today.AddYears(1).AddMonths(1),
            NewMonthlyRent = 1600m,
            NewTerms = "Updated terms for renewal"
        };

        // Act
        var result = await ctx.WorkflowService.RenewLeaseAsync(lease.Id, renewalModel);

        // Assert
        Assert.True(result.Success, string.Join(";", result.Errors));
        Assert.NotNull(result.Data);

        // Verify new lease created
        var newLease = result.Data;
        Assert.NotEqual(lease.Id, newLease.Id);
        Assert.Equal(lease.Id, newLease.PreviousLeaseId);
        Assert.Equal(ApplicationConstants.LeaseStatuses.Accepted, newLease.Status);
        Assert.Equal(1600m, newLease.MonthlyRent);
        Assert.Equal(1, newLease.RenewalNumber);
        Assert.Equal(renewalModel.NewEndDate, newLease.EndDate);

        // Verify old lease updated
        var dbOldLease = await ctx.Context.Leases.FirstAsync(l => l.Id == lease.Id);
        Assert.Equal(ApplicationConstants.LeaseStatuses.Renewed, dbOldLease.Status);
    }

    [Fact]
    public async Task RenewLease_FailsIfEndDateNotAfterCurrent()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (_, _, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.Accepted,
            endDate: DateTime.Today.AddYears(1));

        var renewalModel = new LeaseRenewalModel
        {
            NewEndDate = DateTime.Today.AddMonths(6), // Before current end date
            NewMonthlyRent = 1600m
        };

        // Act
        var result = await ctx.WorkflowService.RenewLeaseAsync(lease.Id, renewalModel);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("after current end date", result.Errors.First().ToLower());
    }

    [Fact]
    public async Task RenewLease_FailsIfRentIsZero()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (_, _, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.Accepted);

        var renewalModel = new LeaseRenewalModel
        {
            NewEndDate = DateTime.Today.AddYears(2),
            NewMonthlyRent = 0m
        };

        // Act
        var result = await ctx.WorkflowService.RenewLeaseAsync(lease.Id, renewalModel);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("rent", result.Errors.First().ToLower());
    }

    [Fact]
    public async Task RenewLease_CanRenewFromMonthToMonth()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (_, _, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.MonthToMonth);

        var renewalModel = new LeaseRenewalModel
        {
            NewEndDate = DateTime.Today.AddYears(2), // Must be after current end date (1 year)
            NewMonthlyRent = 1550m
        };

        // Act
        var result = await ctx.WorkflowService.RenewLeaseAsync(lease.Id, renewalModel);

        // Assert
        Assert.True(result.Success, string.Join(";", result.Errors));
    }

    [Fact]
    public async Task RenewLease_CanCancelNoticeWithRenewal()
    {
        // Arrange - lease with notice given
        await using var ctx = await CreateTestContextAsync();
        var (_, _, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.NoticeGiven);

        var renewalModel = new LeaseRenewalModel
        {
            NewEndDate = DateTime.Today.AddYears(2), // Must be after current end date (1 year)
            NewMonthlyRent = 1500m
        };

        // Act - renewal should cancel the notice
        var result = await ctx.WorkflowService.RenewLeaseAsync(lease.Id, renewalModel);

        // Assert
        Assert.True(result.Success, string.Join(";", result.Errors));
    }

    #endregion

    #region Move-Out Completion Tests

    [Fact]
    public async Task CompleteMoveOut_Success_UpdatesLeaseAndProperty()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (tenant, property, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.NoticeGiven);

        var moveOutDate = DateTime.Today;
        var moveOutModel = new MoveOutModel
        {
            FinalInspectionCompleted = true,
            KeysReturned = true,
            Notes = "Unit in good condition"
        };

        // Act
        var result = await ctx.WorkflowService.CompleteMoveOutAsync(lease.Id, moveOutDate, moveOutModel);

        // Assert
        Assert.True(result.Success, string.Join(";", result.Errors));

        var dbLease = await ctx.Context.Leases.FirstAsync(l => l.Id == lease.Id);
        Assert.Equal(ApplicationConstants.LeaseStatuses.Terminated, dbLease.Status);
        Assert.Equal(moveOutDate, dbLease.ActualMoveOutDate);

        var dbProperty = await ctx.Context.Properties.FirstAsync(p => p.Id == property.Id);
        Assert.Equal(ApplicationConstants.PropertyStatuses.Available, dbProperty.Status);

        var dbTenant = await ctx.Context.Tenants.FirstAsync(t => t.Id == tenant.Id);
        Assert.False(dbTenant.IsActive);
    }

    [Fact]
    public async Task CompleteMoveOut_KeepsTenantActive_IfOtherActiveLeasesExist()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (tenant, property1, lease1) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.NoticeGiven);

        // Ensure tenant is marked as active (they have an active lease elsewhere)
        tenant.IsActive = true;
        await ctx.Context.SaveChangesAsync();

        // Create a second active lease for the same tenant
        var property2 = new Property
        {
            Id = Guid.NewGuid(),
            OrganizationId = ctx.OrgId,
            Address = "456 Second St",
            City = "TestCity",
            State = "TS",
            ZipCode = "12345",
            Status = ApplicationConstants.PropertyStatuses.Occupied,
            MonthlyRent = 1600m,
            CreatedBy = ctx.UserId,
            CreatedOn = DateTime.UtcNow
        };

        var lease2 = new Lease
        {
            Id = Guid.NewGuid(),
            OrganizationId = ctx.OrgId,
            TenantId = tenant.Id,
            PropertyId = property2.Id,
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddYears(1),
            MonthlyRent = 1600m,
            Status = ApplicationConstants.LeaseStatuses.Accepted,
            CreatedBy = ctx.UserId,
            CreatedOn = DateTime.UtcNow
        };

        ctx.Context.Properties.Add(property2);
        ctx.Context.Leases.Add(lease2);
        await ctx.Context.SaveChangesAsync();

        // Act
        var result = await ctx.WorkflowService.CompleteMoveOutAsync(lease1.Id, DateTime.Today);

        // Assert
        Assert.True(result.Success);

        var dbTenant = await ctx.Context.Tenants.FirstAsync(t => t.Id == tenant.Id);
        Assert.True(dbTenant.IsActive); // Should still be active due to second lease
    }

    [Fact]
    public async Task CompleteMoveOut_CanMoveOutFromExpired()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (_, _, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.Expired);

        // Act
        var result = await ctx.WorkflowService.CompleteMoveOutAsync(lease.Id, DateTime.Today);

        // Assert
        Assert.True(result.Success, string.Join(";", result.Errors));
    }

    [Fact]
    public async Task CompleteMoveOut_CanEmergencyMoveOutFromActive()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (_, _, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.Accepted);

        // Act - emergency move-out directly from Active (no notice given)
        var result = await ctx.WorkflowService.CompleteMoveOutAsync(lease.Id, DateTime.Today, 
            new MoveOutModel { Notes = "Emergency move-out" });

        // Assert
        Assert.True(result.Success, string.Join(";", result.Errors));
    }

    [Fact]
    public async Task CompleteMoveOut_FailsIfTerminated()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (_, _, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.Terminated);

        // Act
        var result = await ctx.WorkflowService.CompleteMoveOutAsync(lease.Id, DateTime.Today);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Terminated", result.Errors.First());
    }

    #endregion

    #region Early Termination Tests

    [Fact]
    public async Task EarlyTerminate_Success_WithEviction()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (tenant, property, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.Accepted);

        // Act
        var result = await ctx.WorkflowService.EarlyTerminateAsync(
            lease.Id, "Eviction", "Non-payment of rent for 3 months", DateTime.Today);

        // Assert
        Assert.True(result.Success, string.Join(";", result.Errors));
        Assert.Contains("Eviction", result.Message);

        var dbLease = await ctx.Context.Leases.FirstAsync(l => l.Id == lease.Id);
        Assert.Equal(ApplicationConstants.LeaseStatuses.Terminated, dbLease.Status);
        Assert.Contains("[Eviction]", dbLease.TerminationReason!);

        var dbProperty = await ctx.Context.Properties.FirstAsync(p => p.Id == property.Id);
        Assert.Equal(ApplicationConstants.PropertyStatuses.Available, dbProperty.Status);
    }

    [Fact]
    public async Task EarlyTerminate_Success_MutualAgreement()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (_, _, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.Accepted);

        // Act
        var result = await ctx.WorkflowService.EarlyTerminateAsync(
            lease.Id, "Mutual", "Both parties agreed to end lease early", DateTime.Today);

        // Assert
        Assert.True(result.Success);
        
        var dbLease = await ctx.Context.Leases.FirstAsync(l => l.Id == lease.Id);
        Assert.Contains("[Mutual]", dbLease.TerminationReason!);
    }

    [Fact]
    public async Task EarlyTerminate_FailsWithoutReason()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (_, _, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.Accepted);

        // Act
        var result = await ctx.WorkflowService.EarlyTerminateAsync(
            lease.Id, "Breach", "", DateTime.Today);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("reason is required", result.Errors.First().ToLower());
    }

    [Fact]
    public async Task EarlyTerminate_CanCancelPendingLease()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (_, _, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.Pending);

        // Act
        var result = await ctx.WorkflowService.EarlyTerminateAsync(
            lease.Id, "Mutual", "Tenant changed their mind before move-in", DateTime.Today);

        // Assert
        Assert.True(result.Success, string.Join(";", result.Errors));
    }

    [Fact]
    public async Task EarlyTerminate_FutureEffectiveDate_DoesNotUpdatePropertyYet()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (_, property, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.Accepted);

        var futureDate = DateTime.Today.AddDays(30);

        // Act
        var result = await ctx.WorkflowService.EarlyTerminateAsync(
            lease.Id, "Mutual", "Agreed termination", futureDate);

        // Assert
        Assert.True(result.Success);

        // Property should still be occupied since effective date is in the future
        var dbProperty = await ctx.Context.Properties.FirstAsync(p => p.Id == property.Id);
        Assert.Equal(ApplicationConstants.PropertyStatuses.Occupied, dbProperty.Status);
    }

    #endregion

    #region Lease Expiration Tests

    [Fact]
    public async Task ExpireOverdueLeases_ExpiresLeasesPassedEndDate()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();

        // Create an active lease that ended yesterday
        var (tenant, property, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.Accepted,
            startDate: DateTime.Today.AddYears(-1),
            endDate: DateTime.Today.AddDays(-1)); // Ended yesterday

        // Act
        var result = await ctx.WorkflowService.ExpireOverdueLeaseAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.Data); // One lease expired

        var dbLease = await ctx.Context.Leases.FirstAsync(l => l.Id == lease.Id);
        Assert.Equal(ApplicationConstants.LeaseStatuses.Expired, dbLease.Status);
    }

    [Fact]
    public async Task ExpireOverdueLeases_DoesNotExpireActiveFutureLeases()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();

        // Create an active lease that ends in the future
        var (_, _, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.Accepted,
            endDate: DateTime.Today.AddMonths(6)); // 6 months remaining

        // Act
        var result = await ctx.WorkflowService.ExpireOverdueLeaseAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.Data); // No leases expired

        var dbLease = await ctx.Context.Leases.FirstAsync(l => l.Id == lease.Id);
        Assert.Equal(ApplicationConstants.LeaseStatuses.Accepted, dbLease.Status);
    }

    #endregion

    #region Security Deposit Settlement Tests

    [Fact]
    public async Task InitiateDepositSettlement_Success_CalculatesRefund()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (tenant, property, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.NoticeGiven);
        await CreateSecurityDepositAsync(ctx, lease.Id, tenant.Id, amount: 1500m);

        var deductions = new List<DepositDeductionModel>
        {
            new() { Description = "Cleaning fee", Amount = 200m, Category = "Cleaning" },
            new() { Description = "Wall repair", Amount = 150m, Category = "Repair" }
        };

        // Act
        var result = await ctx.WorkflowService.InitiateDepositSettlementAsync(lease.Id, deductions);

        // Assert
        Assert.True(result.Success, string.Join(";", result.Errors));
        Assert.NotNull(result.Data);

        var settlement = result.Data;
        Assert.Equal(1500m, settlement.OriginalAmount);
        Assert.Equal(350m, settlement.TotalDeductions);
        Assert.Equal(1150m, settlement.RefundAmount);
        Assert.Equal(0m, settlement.AmountOwed);
        Assert.Equal(2, settlement.Deductions.Count);
    }

    [Fact]
    public async Task InitiateDepositSettlement_CalculatesAmountOwed_WhenDeductionsExceedDeposit()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (tenant, property, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.Terminated);
        await CreateSecurityDepositAsync(ctx, lease.Id, tenant.Id, amount: 1000m);

        var deductions = new List<DepositDeductionModel>
        {
            new() { Description = "Major damage repair", Amount = 1200m, Category = "Repair" },
            new() { Description = "Unpaid rent", Amount = 500m, Category = "UnpaidRent" }
        };

        // Act
        var result = await ctx.WorkflowService.InitiateDepositSettlementAsync(lease.Id, deductions);

        // Assert
        Assert.True(result.Success);

        var settlement = result.Data!;
        Assert.Equal(1000m, settlement.OriginalAmount);
        Assert.Equal(1700m, settlement.TotalDeductions);
        Assert.Equal(0m, settlement.RefundAmount); // No refund
        Assert.Equal(700m, settlement.AmountOwed); // Tenant owes money
    }

    [Fact]
    public async Task InitiateDepositSettlement_FailsIfLeaseNotInTerminationStatus()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (tenant, property, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.Accepted);
        await CreateSecurityDepositAsync(ctx, lease.Id, tenant.Id);

        // Act
        var result = await ctx.WorkflowService.InitiateDepositSettlementAsync(lease.Id, new List<DepositDeductionModel>());

        // Assert
        Assert.False(result.Success);
        Assert.Contains("termination status", result.Errors.First().ToLower());
    }

    [Fact]
    public async Task InitiateDepositSettlement_FailsIfNoDepositRecord()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (tenant, property, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.NoticeGiven);
        // No security deposit created

        // Act
        var result = await ctx.WorkflowService.InitiateDepositSettlementAsync(lease.Id, new List<DepositDeductionModel>());

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Errors.First().ToLower());
    }

    [Fact]
    public async Task RecordDepositRefund_Success()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (tenant, property, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.Terminated);
        var deposit = await CreateSecurityDepositAsync(ctx, lease.Id, tenant.Id, amount: 1500m, status: "Pending Return");

        // Act
        var result = await ctx.WorkflowService.RecordDepositRefundAsync(
            lease.Id, 1200m, "Check", "CHK-12345");

        // Assert
        Assert.True(result.Success, string.Join(";", result.Errors));

        var dbDeposit = await ctx.Context.SecurityDeposits.FirstAsync(sd => sd.Id == deposit.Id);
        Assert.Equal("Refunded", dbDeposit.Status);
        Assert.Equal(1200m, dbDeposit.RefundAmount);
        Assert.Equal("Check", dbDeposit.RefundMethod);
        Assert.Equal("CHK-12345", dbDeposit.RefundReference);
        Assert.NotNull(dbDeposit.RefundProcessedDate);
    }

    [Fact]
    public async Task RecordDepositRefund_FailsIfAlreadyReturned()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (tenant, property, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.Terminated);
        await CreateSecurityDepositAsync(ctx, lease.Id, tenant.Id, status: "Returned");

        // Act
        var result = await ctx.WorkflowService.RecordDepositRefundAsync(
            lease.Id, 1000m, "Check", "CHK-99999");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("already been returned", result.Errors.First().ToLower());
    }

    #endregion

    #region State Machine Tests

    [Theory]
    [InlineData(WorkflowLeaseStatus.Pending, WorkflowLeaseStatus.Active, true)]
    [InlineData(WorkflowLeaseStatus.Pending, WorkflowLeaseStatus.Terminated, true)]
    [InlineData(WorkflowLeaseStatus.Pending, WorkflowLeaseStatus.Expired, false)]
    [InlineData(WorkflowLeaseStatus.Active, WorkflowLeaseStatus.Renewed, true)]
    [InlineData(WorkflowLeaseStatus.Active, WorkflowLeaseStatus.MonthToMonth, true)]
    [InlineData(WorkflowLeaseStatus.Active, WorkflowLeaseStatus.NoticeGiven, true)]
    [InlineData(WorkflowLeaseStatus.Active, WorkflowLeaseStatus.Pending, false)]
    [InlineData(WorkflowLeaseStatus.MonthToMonth, WorkflowLeaseStatus.NoticeGiven, true)]
    [InlineData(WorkflowLeaseStatus.MonthToMonth, WorkflowLeaseStatus.Renewed, true)]
    [InlineData(WorkflowLeaseStatus.NoticeGiven, WorkflowLeaseStatus.Expired, true)]
    [InlineData(WorkflowLeaseStatus.NoticeGiven, WorkflowLeaseStatus.Terminated, true)]
    [InlineData(WorkflowLeaseStatus.NoticeGiven, WorkflowLeaseStatus.Active, false)]
    [InlineData(WorkflowLeaseStatus.Terminated, WorkflowLeaseStatus.Active, false)]
    [InlineData(WorkflowLeaseStatus.Expired, WorkflowLeaseStatus.Active, false)]
    public void IsValidTransition_ReturnsExpectedResult(WorkflowLeaseStatus from, WorkflowLeaseStatus to, bool expected)
    {
        // Arrange
        var service = CreateWorkflowServiceForStateMachineTests();

        // Act
        var result = service.IsValidTransition(from, to);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetValidNextStates_Active_ReturnsAllValidTransitions()
    {
        // Arrange
        var service = CreateWorkflowServiceForStateMachineTests();

        // Act
        var validStates = service.GetValidNextStates(WorkflowLeaseStatus.Active);

        // Assert
        Assert.Contains(WorkflowLeaseStatus.Renewed, validStates);
        Assert.Contains(WorkflowLeaseStatus.MonthToMonth, validStates);
        Assert.Contains(WorkflowLeaseStatus.NoticeGiven, validStates);
        Assert.Contains(WorkflowLeaseStatus.Expired, validStates);
        Assert.Contains(WorkflowLeaseStatus.Terminated, validStates);
        Assert.DoesNotContain(WorkflowLeaseStatus.Pending, validStates);
    }

    [Fact]
    public void GetValidNextStates_Terminated_ReturnsEmpty()
    {
        // Arrange
        var service = CreateWorkflowServiceForStateMachineTests();

        // Act
        var validStates = service.GetValidNextStates(WorkflowLeaseStatus.Terminated);

        // Assert
        Assert.Empty(validStates);
    }

    [Fact]
    public void GetInvalidTransitionReason_ReturnsHelpfulMessage()
    {
        // Arrange
        var service = CreateWorkflowServiceForStateMachineTests();

        // Act
        var reason = service.GetInvalidTransitionReason(WorkflowLeaseStatus.Pending, WorkflowLeaseStatus.Expired);

        // Assert
        Assert.Contains("Cannot transition", reason);
        Assert.Contains("Active", reason); // Should list valid options
        Assert.Contains("Terminated", reason);
    }

    private static LeaseWorkflowService CreateWorkflowServiceForStateMachineTests()
    {
        // Create minimal dependencies for state machine tests (doesn't need DB)
        return new LeaseWorkflowService(null!, null!, null!, null!);
    }

    #endregion

    #region Query Method Tests

    [Fact]
    public async Task GetLeaseWorkflowState_ReturnsComprehensiveState()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (tenant, property, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.Accepted,
            endDate: DateTime.Today.AddDays(45)); // Expiring soon
        await CreateSecurityDepositAsync(ctx, lease.Id, tenant.Id);

        // Act
        var state = await ctx.WorkflowService.GetLeaseWorkflowStateAsync(lease.Id);

        // Assert
        Assert.NotNull(state.Lease);
        Assert.NotNull(state.Tenant);
        Assert.NotNull(state.Property);
        Assert.NotNull(state.SecurityDeposit);
        Assert.Equal(45, state.DaysUntilExpiration);
        Assert.True(state.IsExpiring); // Within 60 days
        Assert.True(state.CanRenew);
        Assert.True(state.CanTerminate);
    }

    [Fact]
    public async Task GetLeaseWorkflowState_ReturnsEmptyForNonexistentLease()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();

        // Act
        var state = await ctx.WorkflowService.GetLeaseWorkflowStateAsync(Guid.NewGuid());

        // Assert
        Assert.Null(state.Lease);
        Assert.Empty(state.AuditHistory);
    }

    [Fact]
    public async Task GetExpiringLeases_ReturnsLeasesWithinWindow()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();

        // Create lease expiring in 30 days
        var (_, _, lease1) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.Accepted,
            endDate: DateTime.Today.AddDays(30));

        // Create lease expiring in 90 days (outside default 60-day window)
        var tenant2 = new Tenant
        {
            Id = Guid.NewGuid(),
            OrganizationId = ctx.OrgId,
            FirstName = "Another",
            LastName = "Tenant",
            Email = "another@test.com",
            PhoneNumber = "555-0200",
            IdentificationNumber = Guid.NewGuid().ToString("N")[..10], // Unique ID
            CreatedBy = ctx.UserId,
            CreatedOn = DateTime.UtcNow
        };
        var property2 = new Property
        {
            Id = Guid.NewGuid(),
            OrganizationId = ctx.OrgId,
            Address = "456 Other St",
            City = "TestCity",
            State = "TS",
            ZipCode = "12345",
            Status = ApplicationConstants.PropertyStatuses.Occupied,
            MonthlyRent = 1600m,
            CreatedBy = ctx.UserId,
            CreatedOn = DateTime.UtcNow
        };
        var lease2 = new Lease
        {
            Id = Guid.NewGuid(),
            OrganizationId = ctx.OrgId,
            TenantId = tenant2.Id,
            PropertyId = property2.Id,
            StartDate = DateTime.Today.AddYears(-1),
            EndDate = DateTime.Today.AddDays(90), // Outside 60-day window
            MonthlyRent = 1600m,
            Status = ApplicationConstants.LeaseStatuses.Accepted,
            CreatedBy = ctx.UserId,
            CreatedOn = DateTime.UtcNow
        };
        ctx.Context.Tenants.Add(tenant2);
        ctx.Context.Properties.Add(property2);
        ctx.Context.Leases.Add(lease2);
        await ctx.Context.SaveChangesAsync();

        // Act
        var expiring = await ctx.WorkflowService.GetExpiringLeasesAsync(60);

        // Assert
        Assert.Single(expiring);
        Assert.Equal(lease1.Id, expiring.First().Id);
    }

    [Fact]
    public async Task GetLeasesWithNotice_ReturnsCorrectLeases()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();

        // Create lease with notice
        var (_, _, lease1) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.NoticeGiven);

        // Create active lease (no notice)
        var tenant2 = new Tenant
        {
            Id = Guid.NewGuid(),
            OrganizationId = ctx.OrgId,
            FirstName = "Active",
            LastName = "Tenant",
            Email = "active@test.com",
            PhoneNumber = "555-0300",
            IdentificationNumber = Guid.NewGuid().ToString("N")[..10], // Unique ID
            CreatedBy = ctx.UserId,
            CreatedOn = DateTime.UtcNow
        };
        var property2 = new Property
        {
            Id = Guid.NewGuid(),
            OrganizationId = ctx.OrgId,
            Address = "789 Active St",
            City = "TestCity",
            State = "TS",
            ZipCode = "12345",
            Status = ApplicationConstants.PropertyStatuses.Occupied,
            MonthlyRent = 1700m,
            CreatedBy = ctx.UserId,
            CreatedOn = DateTime.UtcNow
        };
        var lease2 = new Lease
        {
            Id = Guid.NewGuid(),
            OrganizationId = ctx.OrgId,
            TenantId = tenant2.Id,
            PropertyId = property2.Id,
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddYears(1),
            MonthlyRent = 1700m,
            Status = ApplicationConstants.LeaseStatuses.Accepted,
            CreatedBy = ctx.UserId,
            CreatedOn = DateTime.UtcNow
        };
        ctx.Context.Tenants.Add(tenant2);
        ctx.Context.Properties.Add(property2);
        ctx.Context.Leases.Add(lease2);
        await ctx.Context.SaveChangesAsync();

        // Act
        var withNotice = await ctx.WorkflowService.GetLeasesWithNoticeAsync();

        // Assert
        Assert.Single(withNotice);
        Assert.Equal(lease1.Id, withNotice.First().Id);
    }

    #endregion

    #region Audit Trail Tests

    [Fact]
    public async Task LeaseOperations_CreateAuditTrail()
    {
        // Arrange
        await using var ctx = await CreateTestContextAsync();
        var (_, _, lease) = await CreateTenantPropertyAndLeaseAsync(ctx, 
            leaseStatus: ApplicationConstants.LeaseStatuses.Pending,
            startDate: DateTime.Today);

        // Act - perform multiple operations
        await ctx.WorkflowService.ActivateLeaseAsync(lease.Id);
        await ctx.WorkflowService.RecordTerminationNoticeAsync(
            lease.Id, DateTime.Today, DateTime.Today.AddDays(30), "Tenant", "Moving out");

        // Assert
        var auditLogs = await ctx.Context.WorkflowAuditLogs
            .Where(w => w.EntityType == "Lease" && w.EntityId == lease.Id)
            .OrderBy(w => w.PerformedOn)
            .ToListAsync();

        Assert.Equal(2, auditLogs.Count);

        // Verify activation log
        var activateLog = auditLogs.First(l => l.Action == "ActivateLease");
        Assert.Equal(ApplicationConstants.LeaseStatuses.Pending, activateLog.FromStatus);
        Assert.Equal(ApplicationConstants.LeaseStatuses.Accepted, activateLog.ToStatus);

        // Verify notice log
        var noticeLog = auditLogs.First(l => l.Action == "RecordTerminationNotice");
        Assert.Equal(ApplicationConstants.LeaseStatuses.Accepted, noticeLog.FromStatus);
        Assert.Equal(ApplicationConstants.LeaseStatuses.NoticeGiven, noticeLog.ToStatus);
    }

    #endregion
}
