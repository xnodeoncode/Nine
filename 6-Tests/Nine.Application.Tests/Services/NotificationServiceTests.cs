using Nine.Application.Services;
using Nine.Core.Constants;
using Nine.Core.Entities;
using Nine.Core.Interfaces.Services;
using Nine.Entities;
using Nine.Infrastructure.Data;
using Nine.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Nine.Application.Tests
{
    public class NotificationServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly TestApplicationDbContext _context;
        private readonly NotificationService _service;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly Mock<ISMSService> _mockSMSService;
        private readonly Mock<IUserContextService> _mockUserContext;
        private readonly Guid _testOrgId;
        private readonly string _testUserId;

        public NotificationServiceTests()
        {
            // Setup SQLite in-memory database
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new TestApplicationDbContext(options);
            _context.Database.EnsureCreated();

            // Setup test organization and user
            _testUserId = Guid.NewGuid().ToString();
            _testOrgId = Guid.NewGuid();

            var testUser = new ApplicationUser
            {
                Id = _testUserId,
                UserName = "testuser",
                Email = "test@example.com",
                ActiveOrganizationId = _testOrgId
            };

            var testOrg = new Organization
            {
                Id = _testOrgId,
                Name = "Test Organization",
                OwnerId = _testUserId,
                IsActive = true,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };

            _context.Users.Add(testUser);
            _context.Organizations.Add(testOrg);
            _context.SaveChanges();

            // Mock IUserContextService
            _mockUserContext = new Mock<IUserContextService>();
            _mockUserContext.Setup(x => x.GetUserIdAsync())
                .ReturnsAsync(_testUserId);
            _mockUserContext.Setup(x => x.GetActiveOrganizationIdAsync())
                .ReturnsAsync(_testOrgId);
            _mockUserContext.Setup(x => x.GetUserNameAsync())
                .ReturnsAsync("testuser");
            _mockUserContext.Setup(x => x.GetUserEmailAsync())
                .ReturnsAsync("test@example.com");
            _mockUserContext.Setup(x => x.GetOrganizationIdAsync())
                .ReturnsAsync(_testOrgId);

            // Setup mock email and SMS services
            _mockEmailService = new Mock<IEmailService>();
            _mockSMSService = new Mock<ISMSService>();

            // Setup ApplicationSettings
            var settings = Options.Create(new ApplicationSettings
            {
                AppName = "Nine",
                Version = "0.3.0"
            });

            // Create NotificationService
            _service = new NotificationService(
                _context,
                _mockUserContext.Object,
                _mockEmailService.Object,
                _mockSMSService.Object,
                settings,
                Mock.Of<IHubContext<NotificationHub>>(),
                new NullLogger<NotificationService>());
        }

        [Fact]
        public async Task SendNotificationAsync_CreatesInAppNotification()
        {
            // Arrange
            var title = "Test Notification";
            var message = "Test message content";

            // Act
            var notification = await _service.SendNotificationAsync(
                _testUserId,
                title,
                message,
                NotificationConstants.Types.Info,
                NotificationConstants.Categories.System);

            // Assert
            Assert.NotNull(notification);
            Assert.Equal(title, notification.Title);
            Assert.Equal(message, notification.Message);
            Assert.Equal(NotificationConstants.Types.Info, notification.Type);
            Assert.Equal(NotificationConstants.Categories.System, notification.Category);
            Assert.False(notification.IsRead);
            Assert.Null(notification.ReadOn);
            Assert.True(notification.SendInApp);
            Assert.Equal(_testOrgId, notification.OrganizationId);
            Assert.Equal(_testUserId, notification.RecipientUserId);
        }

        [Fact]
        public async Task SendNotificationAsync_WithRelatedEntity_SavesReference()
        {
            // Arrange
            var relatedEntityId = Guid.NewGuid();
            var relatedEntityType = "Lease";

            // Act
            var notification = await _service.SendNotificationAsync(
                _testUserId,
                "Entity Notification",
                "Related to lease",
                NotificationConstants.Types.Info,
                NotificationConstants.Categories.Lease,
                relatedEntityId,
                relatedEntityType);

            // Assert
            Assert.NotNull(notification);
            Assert.Equal(relatedEntityId, notification.RelatedEntityId);
            Assert.Equal(relatedEntityType, notification.RelatedEntityType);
        }

        [Fact]
        public async Task SendNotificationAsync_SendsEmailWhenEnabled()
        {
            // Arrange
            var prefs = new NotificationPreferences
            {
                Id = Guid.NewGuid(),
                OrganizationId = _testOrgId,
                UserId = _testUserId,
                EnableEmailNotifications = true,
                EmailAddress = "test@example.com",
                EmailPaymentDue = true,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            _context.NotificationPreferences.Add(prefs);
            await _context.SaveChangesAsync();

            _mockEmailService.Setup(e => e.SendEmailAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var notification = await _service.SendNotificationAsync(
                _testUserId,
                "Payment Due",
                "Your rent is due",
                NotificationConstants.Types.Warning,
                NotificationConstants.Categories.Payment);

            // Assert
            Assert.True(notification.SendEmail);
            Assert.True(notification.EmailSent);
            Assert.NotNull(notification.EmailSentOn);
            Assert.Null(notification.EmailError);
            _mockEmailService.Verify(e => e.SendEmailAsync(
                "test@example.com",
                "Payment Due",
                "Your rent is due"), Times.Once);
        }

        [Fact]
        public async Task SendNotificationAsync_DoesNotSendEmailWhenDisabled()
        {
            // Arrange
            var prefs = new NotificationPreferences
            {
                Id = Guid.NewGuid(),
                OrganizationId = _testOrgId,
                UserId = _testUserId,
                EnableEmailNotifications = false,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            _context.NotificationPreferences.Add(prefs);
            await _context.SaveChangesAsync();

            // Act
            var notification = await _service.SendNotificationAsync(
                _testUserId,
                "Test",
                "Message",
                NotificationConstants.Types.Info,
                NotificationConstants.Categories.System);

            // Assert
            Assert.False(notification.SendEmail);
            Assert.False(notification.EmailSent);
            _mockEmailService.Verify(e => e.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SendNotificationAsync_RespectsEmailCategoryPreferences()
        {
            // Arrange
            var prefs = new NotificationPreferences
            {
                Id = Guid.NewGuid(),
                OrganizationId = _testOrgId,
                UserId = _testUserId,
                EnableEmailNotifications = true,
                EmailAddress = "test@example.com",
                EmailPaymentDue = false,  // Disabled for payment category
                EmailLeaseExpiring = true,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            _context.NotificationPreferences.Add(prefs);
            await _context.SaveChangesAsync();

            // Act
            var paymentNotification = await _service.SendNotificationAsync(
                _testUserId,
                "Payment Due",
                "Rent due",
                NotificationConstants.Types.Warning,
                NotificationConstants.Categories.Payment);

            var leaseNotification = await _service.SendNotificationAsync(
                _testUserId,
                "Lease Expiring",
                "Lease ends soon",
                NotificationConstants.Types.Warning,
                NotificationConstants.Categories.Lease);

            // Assert
            Assert.False(paymentNotification.SendEmail);
            Assert.True(leaseNotification.SendEmail);
        }

        [Fact]
        public async Task SendNotificationAsync_SendsSMSWhenEnabled()
        {
            // Arrange
            var prefs = new NotificationPreferences
            {
                Id = Guid.NewGuid(),
                OrganizationId = _testOrgId,
                UserId = _testUserId,
                EnableSMSNotifications = true,
                PhoneNumber = "+15555551234",
                SMSPaymentDue = true,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            _context.NotificationPreferences.Add(prefs);
            await _context.SaveChangesAsync();

            _mockSMSService.Setup(s => s.SendSMSAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var notification = await _service.SendNotificationAsync(
                _testUserId,
                "Payment Due",
                "Your rent is due",
                NotificationConstants.Types.Warning,
                NotificationConstants.Categories.Payment);

            // Assert
            Assert.True(notification.SendSMS);
            Assert.True(notification.SMSSent);
            Assert.NotNull(notification.SMSSentOn);
            Assert.Null(notification.SMSError);
            _mockSMSService.Verify(s => s.SendSMSAsync(
                "+15555551234",
                "Payment Due: Your rent is due"), Times.Once);
        }

        [Fact]
        public async Task SendNotificationAsync_HandlesEmailFailureGracefully()
        {
            // Arrange
            var prefs = new NotificationPreferences
            {
                Id = Guid.NewGuid(),
                OrganizationId = _testOrgId,
                UserId = _testUserId,
                EnableEmailNotifications = true,
                EmailAddress = "test@example.com",
                EmailPaymentDue = true,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            _context.NotificationPreferences.Add(prefs);
            await _context.SaveChangesAsync();

            _mockEmailService.Setup(e => e.SendEmailAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .ThrowsAsync(new Exception("Email service unavailable"));

            // Act
            var notification = await _service.SendNotificationAsync(
                _testUserId,
                "Test",
                "Message",
                NotificationConstants.Types.Info,
                NotificationConstants.Categories.Payment);

            // Assert
            Assert.NotNull(notification);
            Assert.True(notification.SendEmail);
            Assert.False(notification.EmailSent);
            Assert.Null(notification.EmailSentOn);
            Assert.NotNull(notification.EmailError);
            Assert.Contains("Email service unavailable", notification.EmailError);
        }

        [Fact]
        public async Task SendNotificationAsync_HandlesSMSFailureGracefully()
        {
            // Arrange
            var prefs = new NotificationPreferences
            {
                Id = Guid.NewGuid(),
                OrganizationId = _testOrgId,
                UserId = _testUserId,
                EnableSMSNotifications = true,
                PhoneNumber = "+15555551234",
                SMSPaymentDue = true,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            _context.NotificationPreferences.Add(prefs);
            await _context.SaveChangesAsync();

            _mockSMSService.Setup(s => s.SendSMSAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .ThrowsAsync(new Exception("SMS service unavailable"));

            // Act
            var notification = await _service.SendNotificationAsync(
                _testUserId,
                "Test",
                "Message",
                NotificationConstants.Types.Info,
                NotificationConstants.Categories.Payment);

            // Assert
            Assert.NotNull(notification);
            Assert.True(notification.SendSMS);
            Assert.False(notification.SMSSent);
            Assert.Null(notification.SMSSentOn);
            Assert.NotNull(notification.SMSError);
            Assert.Contains("SMS service unavailable", notification.SMSError);
        }

        [Fact]
        public async Task MarkAsReadAsync_UpdatesNotificationStatus()
        {
            // Arrange
            var notification = await _service.SendNotificationAsync(
                _testUserId,
                "Test",
                "Message",
                NotificationConstants.Types.Info,
                NotificationConstants.Categories.System);

            Assert.False(notification.IsRead);
            Assert.Null(notification.ReadOn);

            // Act
            await _service.MarkAsReadAsync(notification.Id);

            // Assert
            var updated = await _context.Notifications.FindAsync(notification.Id);
            Assert.NotNull(updated);
            Assert.True(updated.IsRead);
            Assert.NotNull(updated.ReadOn);
        }

        [Fact]
        public async Task MarkAsReadAsync_HandlesNonExistentNotification()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid();

            // Act & Assert - should not throw
            await _service.MarkAsReadAsync(nonExistentId);
        }

        [Fact]
        public async Task GetUnreadNotificationsAsync_ReturnsOnlyUnreadForCurrentUser()
        {
            // Arrange
            await _service.SendNotificationAsync(
                _testUserId,
                "Unread 1",
                "Message 1",
                NotificationConstants.Types.Info,
                NotificationConstants.Categories.System);

            var notification2 = await _service.SendNotificationAsync(
                _testUserId,
                "Unread 2",
                "Message 2",
                NotificationConstants.Types.Info,
                NotificationConstants.Categories.System);

            await _service.MarkAsReadAsync(notification2.Id);

            // Act
            var unread = await _service.GetUnreadNotificationsAsync();

            // Assert
            Assert.Single(unread);
            Assert.Equal("Unread 1", unread[0].Title);
            Assert.False(unread[0].IsRead);
        }

        [Fact]
        public async Task GetNotificationHistoryAsync_ReturnsAllNotificationsForCurrentUser()
        {
            // Arrange
            var notification1 = await _service.SendNotificationAsync(
                _testUserId,
                "Notification 1",
                "Message 1",
                NotificationConstants.Types.Info,
                NotificationConstants.Categories.System);

            var notification2 = await _service.SendNotificationAsync(
                _testUserId,
                "Notification 2",
                "Message 2",
                NotificationConstants.Types.Warning,
                NotificationConstants.Categories.Payment);

            await _service.MarkAsReadAsync(notification1.Id);

            // Act
            var history = await _service.GetNotificationHistoryAsync(100);

            // Assert
            Assert.Equal(2, history.Count);
            Assert.Contains(history, n => n.Title == "Notification 1" && n.IsRead);
            Assert.Contains(history, n => n.Title == "Notification 2" && !n.IsRead);
        }

        [Fact]
        public async Task GetNotificationHistoryAsync_RespectsCountLimit()
        {
            // Arrange
            for (int i = 0; i < 5; i++)
            {
                await _service.SendNotificationAsync(
                    _testUserId,
                    $"Notification {i}",
                    $"Message {i}",
                    NotificationConstants.Types.Info,
                    NotificationConstants.Categories.System);
            }

            // Act
            var history = await _service.GetNotificationHistoryAsync(3);

            // Assert
            Assert.Equal(3, history.Count);
        }

        [Fact]
        public async Task Notifications_AreIsolatedByOrganization()
        {
            // Arrange
            var otherOrgId = Guid.NewGuid();
            var otherUserId = "other-user-456";

            var otherUser = new ApplicationUser
            {
                Id = otherUserId,
                UserName = "otheruser",
                Email = "other@example.com",
                ActiveOrganizationId = otherOrgId
            };

            var otherOrg = new Organization
            {
                Id = otherOrgId,
                Name = "Other Organization",
                OwnerId = otherUserId,
                IsActive = true,
                CreatedBy = otherUserId,
                CreatedOn = DateTime.UtcNow
            };

            _context.Users.Add(otherUser);
            _context.Organizations.Add(otherOrg);
            await _context.SaveChangesAsync();

            // Create notification for test user
            await _service.SendNotificationAsync(
                _testUserId,
                "Test User Notification",
                "Message for test user",
                NotificationConstants.Types.Info,
                NotificationConstants.Categories.System);

            // Create notification for other user's org directly
            var otherNotification = new Notification
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrgId,
                RecipientUserId = otherUserId,
                Title = "Other User Notification",
                Message = "Message for other user",
                Type = NotificationConstants.Types.Info,
                Category = NotificationConstants.Categories.System,
                SentOn = DateTime.UtcNow,
                CreatedBy = otherUserId,
                CreatedOn = DateTime.UtcNow
            };
            _context.Notifications.Add(otherNotification);
            await _context.SaveChangesAsync();

            // Act
            var unread = await _service.GetUnreadNotificationsAsync();

            // Assert
            Assert.Single(unread);
            Assert.Equal("Test User Notification", unread[0].Title);
            Assert.Equal(_testOrgId, unread[0].OrganizationId);
        }

        [Fact]
        public async Task SendNotificationAsync_CreatesDefaultPreferencesForNewUser()
        {
            // Arrange
            var newUserId = "new-user-789";
            var newUser = new ApplicationUser
            {
                Id = newUserId,
                UserName = "newuser",
                Email = "new@example.com",
                ActiveOrganizationId = _testOrgId
            };
            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // Act
            var notification = await _service.SendNotificationAsync(
                newUserId,
                "First Notification",
                "Welcome",
                NotificationConstants.Types.Info,
                NotificationConstants.Categories.System);

            // Assert
            var prefs = await _context.NotificationPreferences
                .FirstOrDefaultAsync(p => p.UserId == newUserId && p.OrganizationId == _testOrgId);

            Assert.NotNull(prefs);
            Assert.True(prefs.EnableInAppNotifications);
            Assert.True(prefs.EnableEmailNotifications);
            Assert.False(prefs.EnableSMSNotifications);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _connection?.Dispose();
        }
    }
}