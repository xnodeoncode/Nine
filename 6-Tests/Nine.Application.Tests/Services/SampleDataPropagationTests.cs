using Nine.Application.Services;
using Nine.Core.Constants;
using Nine.Core.Entities;
using Nine.Core.Interfaces;
using Nine.Core.Interfaces.Services;
using Nine.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Nine.Application.Tests.Services
{
    /// <summary>
    /// Tests for automatic IsSampleData flag propagation from parent entities to children.
    /// Verifies that when creating entities related to sample data, the child entity
    /// automatically inherits the IsSampleData flag.
    /// </summary>
    public class SampleDataPropagationTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<IUserContextService> _userContextMock;
        private readonly Mock<IOptions<ApplicationSettings>> _settingsMock;
        private readonly Guid _organizationId;
        private readonly string _userId;

        public SampleDataPropagationTests()
        {
            // Setup in-memory SQLite database (more accurate than InMemoryDatabase)
            var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
            connection.Open();
            
            // IMPORTANT: Disable foreign key constraints for test simplicity
            // In-memory database schema might not have all relationships configured
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA foreign_keys = OFF;";
                command.ExecuteNonQuery();
            }
            
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
            
            _context = new ApplicationDbContext(options);
            _context.Database.EnsureCreated();

            // Setup mocks
            _organizationId = Guid.NewGuid();
            _userId = Guid.NewGuid().ToString();

            _userContextMock = new Mock<IUserContextService>();
            _userContextMock.Setup(x => x.GetUserIdAsync()).ReturnsAsync(_userId);
            _userContextMock.Setup(x => x.GetActiveOrganizationIdAsync()).ReturnsAsync(_organizationId);

            _settingsMock = new Mock<IOptions<ApplicationSettings>>();
            _settingsMock.Setup(x => x.Value).Returns(new ApplicationSettings
            {
                SoftDeleteEnabled = true
            });
        }

        [Fact]
        public async Task CreatePayment_WithSampleInvoice_ShouldInheritSampleDataFlag()
        {
            // Arrange: Create full entity hierarchy (Property → Lease → Invoice)
            var sampleProperty = new Property
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organizationId,
                Address = "123 Test St",
                City = "Test City",
                State = "TS",
                ZipCode = "12345",
                PropertyType = ApplicationConstants.PropertyTypes.House,
                Status = ApplicationConstants.PropertyStatuses.Available,
                CreatedBy = _userId,
                CreatedOn = DateTime.UtcNow,
                IsSampleData = true
            };
            _context.Properties.Add(sampleProperty);

            var sampleLease = new Lease
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organizationId,
                PropertyId = sampleProperty.Id,
                TenantId = Guid.NewGuid(), // Required field
                StartDate = DateTime.UtcNow.Date,
                EndDate = DateTime.UtcNow.AddYears(1).Date,
                MonthlyRent = 1500m,
                SecurityDeposit = 1500m,
                Status = ApplicationConstants.LeaseStatuses.Accepted,
                CreatedBy = _userId,
                CreatedOn = DateTime.UtcNow,
                IsSampleData = true
            };
            _context.Leases.Add(sampleLease);

            var sampleInvoice = new Invoice
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organizationId,
                LeaseId = sampleLease.Id,
                InvoiceNumber = "INV-TEST-001",
                InvoicedOn = DateTime.UtcNow,
                DueOn = DateTime.UtcNow.AddDays(30),
                Amount = 1500m,
                Status = ApplicationConstants.InvoiceStatuses.Pending,
                Description = "Test Sample Invoice",
                CreatedBy = ApplicationConstants.SystemUser.Id,
                CreatedOn = DateTime.UtcNow,
                IsSampleData = true // SAMPLE DATA
            };
            _context.Invoices.Add(sampleInvoice);
            await _context.SaveChangesAsync();

            // Arrange: Create NotificationService (needed by PaymentService)
            var notificationLogger = new Mock<ILogger<NotificationService>>();
            var notificationService = new NotificationService(
                _context,
                _userContextMock.Object,
                Mock.Of<IEmailService>(),
                Mock.Of<ISMSService>(),
                _settingsMock.Object,
                Mock.Of<Microsoft.AspNetCore.SignalR.IHubContext<Nine.Infrastructure.Hubs.NotificationHub>>(),
                notificationLogger.Object);

            // Arrange: Create PaymentService
            var paymentLogger = new Mock<ILogger<PaymentService>>();
            var paymentService = new PaymentService(
                _context,
                paymentLogger.Object,
                _userContextMock.Object,
                notificationService,
                _settingsMock.Object);

            // Act: Create payment against sample invoice
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                InvoiceId = sampleInvoice.Id, // Link to sample invoice
                Amount = 1500m,
                PaymentMethod = ApplicationConstants.PaymentMethods.BankTransfer,
                PaidOn = DateTime.UtcNow,
                PaymentNumber = "PYMT-TEST-001"
                // IsSampleData NOT set - should be inherited
            };

            var createdPayment = await paymentService.CreateAsync(payment);

            // Assert: Payment should have IsSampleData = true
            Assert.True(createdPayment.IsSampleData, 
                "Payment should inherit IsSampleData=true from parent Invoice");
        }

        [Fact]
        public async Task CreatePayment_WithNormalInvoice_ShouldNotHaveSampleDataFlag()
        {
            // Arrange: Create full entity hierarchy (Property → Lease → Invoice) - all normal data
            var normalProperty = new Property
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organizationId,
                Address = "456 Normal St",
                City = "Normal City",
                State = "TS",
                ZipCode = "54321",
                PropertyType = ApplicationConstants.PropertyTypes.House,
                Status = ApplicationConstants.PropertyStatuses.Available,
                CreatedBy = _userId,
                CreatedOn = DateTime.UtcNow,
                IsSampleData = false
            };
            _context.Properties.Add(normalProperty);

            var normalLease = new Lease
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organizationId,
                PropertyId = normalProperty.Id,
                TenantId = Guid.NewGuid(), // Required field
                StartDate = DateTime.UtcNow.Date,
                EndDate = DateTime.UtcNow.AddYears(1).Date,
                MonthlyRent = 2000m,
                SecurityDeposit = 2000m,
                Status = ApplicationConstants.LeaseStatuses.Accepted,
                CreatedBy = _userId,
                CreatedOn = DateTime.UtcNow,
                IsSampleData = false
            };
            _context.Leases.Add(normalLease);

            var normalInvoice = new Invoice
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organizationId,
                LeaseId = normalLease.Id,
                InvoiceNumber = "INV-TEST-002",
                InvoicedOn = DateTime.UtcNow,
                DueOn = DateTime.UtcNow.AddDays(30),
                Amount = 2000m,
                Status = ApplicationConstants.InvoiceStatuses.Pending,
                Description = "Test Normal Invoice",
                CreatedBy = _userId,
                CreatedOn = DateTime.UtcNow,
                IsSampleData = false // NOT sample data
            };
            _context.Invoices.Add(normalInvoice);
            await _context.SaveChangesAsync();

            // Arrange: Create NotificationService (needed by PaymentService)
            var notificationLogger = new Mock<ILogger<NotificationService>>();
            var notificationService = new NotificationService(
                _context,
                _userContextMock.Object,
                Mock.Of<IEmailService>(),
                Mock.Of<ISMSService>(),
                _settingsMock.Object,
                Mock.Of<Microsoft.AspNetCore.SignalR.IHubContext<Nine.Infrastructure.Hubs.NotificationHub>>(),
                notificationLogger.Object);

            // Arrange: Create PaymentService
            var paymentLogger = new Mock<ILogger<PaymentService>>();
            var paymentService = new PaymentService(
                _context,
                paymentLogger.Object,
                _userContextMock.Object,
                notificationService,
                _settingsMock.Object);

            // Act: Create payment against normal invoice
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                InvoiceId = normalInvoice.Id,
                Amount = 2000m,
                PaymentMethod = ApplicationConstants.PaymentMethods.BankTransfer,
                PaidOn = DateTime.UtcNow,
                PaymentNumber = "PYMT-TEST-002"
            };

            var createdPayment = await paymentService.CreateAsync(payment);

            // Assert: Payment should have IsSampleData = false
            Assert.False(createdPayment.IsSampleData, 
                "Payment should NOT inherit IsSampleData flag from normal Invoice");
        }

        [Fact]
        public async Task CreateLease_WithSampleProperty_ShouldInheritSampleDataFlag()
        {
            // Arrange: Create sample property
            var sampleProperty = new Property
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organizationId,
                Address = "123 Sample St",
                City = "Austin",
                State = "TX",
                ZipCode = "78701",
                PropertyType = ApplicationConstants.PropertyTypes.House,
                MonthlyRent = 1800m,
                Status = ApplicationConstants.PropertyStatuses.Available,
                CreatedBy = ApplicationConstants.SystemUser.Id,
                CreatedOn = DateTime.UtcNow,
                IsSampleData = true // SAMPLE DATA
            };

            var sampleTenant = new Tenant
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organizationId,
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@example.com",
                PhoneNumber = "555-1234",
                CreatedBy = _userId,
                CreatedOn = DateTime.UtcNow,
                IsSampleData = false // Normal tenant
            };

            _context.Properties.Add(sampleProperty);
            _context.Tenants.Add(sampleTenant);
            await _context.SaveChangesAsync();

            // Arrange: Create LeaseService
            var logger = new Mock<ILogger<LeaseService>>();
            var leaseService = new LeaseService(
                _context,
                logger.Object,
                _userContextMock.Object,
                _settingsMock.Object);

            // Act: Create lease for sample property
            var lease = new Lease
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organizationId,
                PropertyId = sampleProperty.Id, // Link to sample property
                TenantId = sampleTenant.Id,
                StartDate = DateTime.UtcNow.Date,
                EndDate = DateTime.UtcNow.Date.AddMonths(12),
                MonthlyRent = 1800m,
                Status = ApplicationConstants.LeaseStatuses.Accepted
                // IsSampleData NOT set
            };

            var createdLease = await leaseService.CreateAsync(lease);

            // Assert: Lease should inherit IsSampleData=true from property
            Assert.True(createdLease.IsSampleData, 
                "Lease should inherit IsSampleData=true from parent Property");
        }

        [Fact]
        public async Task CreateInvoice_WithSampleLease_ShouldInheritSampleDataFlag()
        {
            // Arrange: Create sample lease
            var sampleProperty = new Property
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organizationId,
                Address = "456 Sample Ave",
                City = "Dallas",
                State = "TX",
                ZipCode = "75001",
                PropertyType = ApplicationConstants.PropertyTypes.Apartment,
                MonthlyRent = 1500m,
                Status = ApplicationConstants.PropertyStatuses.Occupied,
                CreatedBy = ApplicationConstants.SystemUser.Id,
                CreatedOn = DateTime.UtcNow,
                IsSampleData = true
            };

            var sampleTenant = new Tenant
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organizationId,
                FirstName = "Jane",
                LastName = "Smith",
                Email = "jane.smith@example.com",
                PhoneNumber = "555-5678",
                CreatedBy = ApplicationConstants.SystemUser.Id,
                CreatedOn = DateTime.UtcNow,
                IsSampleData = true
            };

            var sampleLease = new Lease
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organizationId,
                PropertyId = sampleProperty.Id,
                TenantId = sampleTenant.Id,
                StartDate = DateTime.UtcNow.Date.AddMonths(-3),
                EndDate = DateTime.UtcNow.Date.AddMonths(9),
                MonthlyRent = 1500m,
                Status = ApplicationConstants.LeaseStatuses.Accepted,
                CreatedBy = ApplicationConstants.SystemUser.Id,
                CreatedOn = DateTime.UtcNow,
                IsSampleData = true // SAMPLE DATA
            };

            _context.Properties.Add(sampleProperty);
            _context.Tenants.Add(sampleTenant);
            _context.Leases.Add(sampleLease);
            await _context.SaveChangesAsync();

            // Arrange: Create InvoiceService
            var logger = new Mock<ILogger<InvoiceService>>();
            var invoiceService = new InvoiceService(
                _context,
                logger.Object,
                _userContextMock.Object,
                _settingsMock.Object);

            // Act: Create invoice for sample lease
            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organizationId,
                LeaseId = sampleLease.Id, // Link to sample lease
                InvoiceNumber = "INV-TEST-003",
                InvoicedOn = DateTime.UtcNow,
                DueOn = DateTime.UtcNow.AddDays(30),
                Amount = 1500m,
                Status = ApplicationConstants.InvoiceStatuses.Pending,
                Description = "Rent for test month"
                // IsSampleData NOT set
            };

            var createdInvoice = await invoiceService.CreateAsync(invoice);

            // Assert: Invoice should inherit IsSampleData=true from lease
            Assert.True(createdInvoice.IsSampleData, 
                "Invoice should inherit IsSampleData=true from parent Lease");
        }

        [Fact]
        public async Task CreatePayment_AlreadyMarkedAsSampleData_ShouldNotCheckParents()
        {
            // Arrange: Create full entity hierarchy - all normal data
            var normalProperty = new Property
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organizationId,
                Address = "789 Regular Ave",
                City = "Regular City",
                State = "TS",
                ZipCode = "98765",
                PropertyType = ApplicationConstants.PropertyTypes.House,
                Status = ApplicationConstants.PropertyStatuses.Available,
                CreatedBy = _userId,
                CreatedOn = DateTime.UtcNow,
                IsSampleData = false
            };
            _context.Properties.Add(normalProperty);

            var normalLease = new Lease
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organizationId,
                PropertyId = normalProperty.Id,
                TenantId = Guid.NewGuid(), // Required field
                StartDate = DateTime.UtcNow.Date,
                EndDate = DateTime.UtcNow.AddYears(1).Date,
                MonthlyRent = 3000m,
                SecurityDeposit = 3000m,
                Status = ApplicationConstants.LeaseStatuses.Accepted,
                CreatedBy = _userId,
                CreatedOn = DateTime.UtcNow,
                IsSampleData = false
            };
            _context.Leases.Add(normalLease);

            var normalInvoice = new Invoice
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organizationId,
                LeaseId = normalLease.Id,
                InvoiceNumber = "INV-TEST-004",
                InvoicedOn = DateTime.UtcNow,
                DueOn = DateTime.UtcNow.AddDays(30),
                Amount = 3000m,
                Status = ApplicationConstants.InvoiceStatuses.Pending,
                Description = "Normal Invoice",
                CreatedBy = _userId,
                CreatedOn = DateTime.UtcNow,
                IsSampleData = false
            };
            _context.Invoices.Add(normalInvoice);
            await _context.SaveChangesAsync();

            // Arrange: Create NotificationService (needed by PaymentService)
            var notificationLogger = new Mock<ILogger<NotificationService>>();
            var notificationService = new NotificationService(
                _context,
                _userContextMock.Object,
                Mock.Of<IEmailService>(),
                Mock.Of<ISMSService>(),
                _settingsMock.Object,
                Mock.Of<Microsoft.AspNetCore.SignalR.IHubContext<Nine.Infrastructure.Hubs.NotificationHub>>(),
                notificationLogger.Object);

            // Arrange: Create PaymentService
            var paymentLogger = new Mock<ILogger<PaymentService>>();
            var paymentService = new PaymentService(
                _context,
                paymentLogger.Object,
                _userContextMock.Object,
                notificationService,
                _settingsMock.Object);

            // Act: Create payment EXPLICITLY marked as sample data
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                InvoiceId = normalInvoice.Id, // Normal invoice
                Amount = 3000m,
                PaymentMethod = ApplicationConstants.PaymentMethods.Cash,
                PaidOn = DateTime.UtcNow,
                PaymentNumber = "PYMT-TEST-003",
                IsSampleData = true // EXPLICITLY marked
            };

            var createdPayment = await paymentService.CreateAsync(payment);

            // Assert: Payment should remain IsSampleData=true despite normal invoice
            Assert.True(createdPayment.IsSampleData, 
                "Payment explicitly marked as sample data should remain sample data");
        }

        [Fact]
        public async Task CreateInspection_WithSampleProperty_ShouldInheritSampleDataFlag()
        {
            // Arrange: Create sample property
            var sampleProperty = new Property
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organizationId,
                Address = "456 Sample Ave",
                City = "Sample City",
                State = "TS",
                ZipCode = "12345",
                PropertyType = ApplicationConstants.PropertyTypes.House,
                Status = ApplicationConstants.PropertyStatuses.Available,
                CreatedBy = ApplicationConstants.SystemUser.Id,
                CreatedOn = DateTime.UtcNow,
                IsSampleData = true // SAMPLE DATA
            };
            _context.Properties.Add(sampleProperty);
            await _context.SaveChangesAsync();

            // Arrange: Create InspectionService
            var logger = new Mock<ILogger<InspectionService>>();
            var calendarEventService = new Mock<ICalendarEventService>();
            var inspectionService = new InspectionService(
                _context,
                logger.Object,
                _userContextMock.Object,
                _settingsMock.Object,
                calendarEventService.Object);

            // Act: Create inspection WITHOUT setting IsSampleData
            var inspection = new Inspection
            {
                Id = Guid.NewGuid(),
                PropertyId = sampleProperty.Id, // Link to sample property
                CompletedOn = DateTime.UtcNow,
                InspectionType = "Routine",
                InspectedBy = "Inspector Joe",
                OverallCondition = "Good"
                // IsSampleData NOT set - should be inherited from Property
            };

            var createdInspection = await inspectionService.CreateAsync(inspection);

            // Assert: Inspection should inherit IsSampleData = true from property
            Assert.True(createdInspection.IsSampleData,
                "Inspection should inherit IsSampleData=true from sample property");
        }

        [Fact]
        public async Task CreateDocument_WithSampleInspection_ShouldInheritSampleDataFlag()
        {
            // Arrange: Create sample property and inspection
            var sampleProperty = new Property
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organizationId,
                Address = "789 Sample Blvd",
                City = "Sample City",
                State = "TS",
                ZipCode = "12345",
                PropertyType = ApplicationConstants.PropertyTypes.House,
                Status = ApplicationConstants.PropertyStatuses.Available,
                CreatedBy = ApplicationConstants.SystemUser.Id,
                CreatedOn = DateTime.UtcNow,
                IsSampleData = true
            };
            _context.Properties.Add(sampleProperty);

            var sampleInspection = new Inspection
            {
                Id = Guid.NewGuid(),
                PropertyId = sampleProperty.Id,
                CompletedOn = DateTime.UtcNow,
                InspectionType = "Move-In",
                InspectedBy = "Inspector Jane",
                OverallCondition = "Excellent",
                IsSampleData = true // SAMPLE DATA
            };
            _context.Inspections.Add(sampleInspection);
            await _context.SaveChangesAsync();

            // Arrange: Create DocumentService
            var logger = new Mock<ILogger<DocumentService>>();
            var documentService = new DocumentService(
                _context,
                logger.Object,
                _userContextMock.Object,
                _settingsMock.Object);

            // Act: Create document WITHOUT setting IsSampleData
            var document = new Document
            {
                Id = Guid.NewGuid(),
                PropertyId = sampleProperty.Id, // Link to sample property (no InspectionId field)
                FileName = "inspection-report.pdf",
                FileExtension = ".pdf",
                FileSize = 1024,
                FileData = new byte[] { 0x25, 0x50, 0x44, 0x46 }, // PDF header
                DocumentType = "Inspection"
                // IsSampleData NOT set - should be inherited from Property
            };

            var createdDocument = await documentService.CreateAsync(document);

            // Assert: Document should inherit IsSampleData = true from property
            Assert.True(createdDocument.IsSampleData,
                "Document should inherit IsSampleData=true from sample property");
        }

        [Fact]
        public async Task CreateInspectionWithCalendarEvent_ShouldInheritSampleDataFlag()
        {
            // Arrange: Create sample property
            var sampleProperty = new Property
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organizationId,
                Address = "999 Sample Calendar St",
                City = "Sample City",
                State = "TS",
                ZipCode = "12345",
                PropertyType = ApplicationConstants.PropertyTypes.House,
                Status = ApplicationConstants.PropertyStatuses.Available,
                CreatedBy = ApplicationConstants.SystemUser.Id,
                CreatedOn = DateTime.UtcNow,
                IsSampleData = true // SAMPLE DATA
            };
            _context.Properties.Add(sampleProperty);

            // Arrange: Create CalendarSettings to enable auto-create for Inspection events
            var calendarSetting = new CalendarSettings
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organizationId,
                EntityType = CalendarEventTypes.Inspection,
                AutoCreateEvents = true,
                ShowOnCalendar = true,
                CreatedBy = _userId,
                CreatedOn = DateTime.UtcNow
            };
            _context.CalendarSettings.Add(calendarSetting);
            await _context.SaveChangesAsync();

            // Arrange: Create services with real CalendarSettingsService (not mocked)
            var calendarSettingsService = new CalendarSettingsService(_context, _userContextMock.Object);
            var calendarEventService = new CalendarEventService(
                _context,
                calendarSettingsService,
                _userContextMock.Object);

            var inspectionLogger = new Mock<ILogger<InspectionService>>();
            var inspectionService = new InspectionService(
                _context,
                inspectionLogger.Object,
                _userContextMock.Object,
                _settingsMock.Object,
                calendarEventService);

            // Act: Create inspection WITHOUT setting IsSampleData
            var inspection = new Inspection
            {
                Id = Guid.NewGuid(),
                PropertyId = sampleProperty.Id,
                CompletedOn = DateTime.UtcNow,
                InspectionType = "Routine",
                InspectedBy = "Inspector Joe",
                OverallCondition = "Good"
                // IsSampleData NOT set - should be inherited
            };

            var createdInspection = await inspectionService.CreateAsync(inspection);

            // Assert: Inspection should inherit IsSampleData
            Assert.True(createdInspection.IsSampleData,
                "Inspection should inherit IsSampleData=true from sample property");

            // Assert: CalendarEvent should be created and also have IsSampleData = true
            Assert.NotNull(createdInspection.CalendarEventId);
            var calendarEvent = await _context.CalendarEvents
                .FirstOrDefaultAsync(e => e.Id == createdInspection.CalendarEventId);
            
            Assert.NotNull(calendarEvent);
            Assert.True(calendarEvent.IsSampleData,
                "CalendarEvent should inherit IsSampleData=true from inspection");
            Assert.Equal(sampleProperty.Address, calendarEvent.Location);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}
