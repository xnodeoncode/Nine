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
using DocumentService = Nine.Application.Services.DocumentService;

namespace Nine.Application.Tests
{
    /// <summary>
    /// Comprehensive unit tests for DocumentService.
    /// Tests CRUD operations, validation, business logic, and organization isolation.
    /// </summary>
    public class DocumentServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly TestApplicationDbContext _context;
        private readonly Mock<IUserContextService> _mockUserContext;
        private readonly Mock<ILogger<DocumentService>> _mockLogger;
        private readonly IOptions<ApplicationSettings> _mockSettings;
        private readonly DocumentService _service;
        private readonly Guid _testOrgId = Guid.NewGuid();
        private readonly string _testUserId = "test-user-123";
        private readonly Guid _testPropertyId = Guid.NewGuid();
        private readonly Guid _testTenantId = Guid.NewGuid();
        private readonly Guid _testLeaseId = Guid.NewGuid();

        public DocumentServiceTests()
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
            _mockLogger = new Mock<ILogger<DocumentService>>();

            _mockSettings = Options.Create(new ApplicationSettings
            {
                SoftDeleteEnabled = true
            });

            // Create service instance
            _service = new DocumentService(
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
        public async Task CreateAsync_ValidDocument_CreatesSuccessfully()
        {
            // Arrange
            var document = new Document
            {
                OrganizationId = _testOrgId,
                FileName = "TestDocument.pdf",
                FileExtension = ".pdf",
                FileData = new byte[] { 1, 2, 3, 4, 5 },
                ContentType = "application/pdf",
                FileType = "PDF",
                FileSize = 5,
                DocumentType = "Lease Agreement",
                PropertyId = _testPropertyId,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };

            // Act
            var result = await _service.CreateAsync(document);

            // Assert
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.Id);
            Assert.Equal("TestDocument.pdf", result.FileName);
        }

        [Fact]
        public async Task CreateAsync_MissingFileName_ThrowsException()
        {
            // Arrange
            var document = new Document
            {
                OrganizationId = _testOrgId,
                FileName = "", // Missing
                FileExtension = ".pdf",
                FileData = new byte[] { 1, 2, 3 },
                DocumentType = "Invoice",
                PropertyId = _testPropertyId,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(document));
        }

        [Fact]
        public async Task CreateAsync_MissingFileExtension_ThrowsException()
        {
            // Arrange
            var document = new Document
            {
                OrganizationId = _testOrgId,
                FileName = "test.pdf",
                FileExtension = "", // Missing
                FileData = new byte[] { 1, 2, 3 },
                DocumentType = "Invoice",
                PropertyId = _testPropertyId,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(document));
        }

        [Fact]
        public async Task CreateAsync_MissingDocumentType_ThrowsException()
        {
            // Arrange
            var document = new Document
            {
                OrganizationId = _testOrgId,
                FileName = "test.pdf",
                FileExtension = ".pdf",
                FileData = new byte[] { 1, 2, 3 },
                DocumentType = "", // Missing
                PropertyId = _testPropertyId,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(document));
        }

        [Fact]
        public async Task CreateAsync_MissingFileData_ThrowsException()
        {
            // Arrange
            var document = new Document
            {
                OrganizationId = _testOrgId,
                FileName = "test.pdf",
                FileExtension = ".pdf",
                FileData = Array.Empty<byte>(), // Missing
                DocumentType = "Invoice",
                PropertyId = _testPropertyId,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(document));
        }

        [Fact]
        public async Task CreateAsync_NoForeignKeys_ThrowsException()
        {
            // Arrange
            var document = new Document
            {
                OrganizationId = _testOrgId,
                FileName = "test.pdf",
                FileExtension = ".pdf",
                FileData = new byte[] { 1, 2, 3 },
                DocumentType = "Invoice",
                // No foreign keys set
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(document));
        }

        [Fact]
        public async Task CreateAsync_FileSizeExceedsLimit_ThrowsException()
        {
            // Arrange - Create 11MB file (exceeds 10MB limit)
            var largeFile = new byte[11 * 1024 * 1024];
            var document = new Document
            {
                OrganizationId = _testOrgId,
                FileName = "large.pdf",
                FileExtension = ".pdf",
                FileData = largeFile,
                FileSize = largeFile.Length,
                DocumentType = "Invoice",
                PropertyId = _testPropertyId,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(document));
        }

        #endregion

        #region Retrieval Tests

        [Fact]
        public async Task GetDocumentsByPropertyIdAsync_ReturnsPropertyDocuments()
        {
            // Arrange - Create documents
            var doc1 = new Document
            {
                OrganizationId = _testOrgId,
                FileName = "property_doc1.pdf",
                FileExtension = ".pdf",
                FileData = new byte[] { 1, 2, 3 },
                FileSize = 3,
                DocumentType = "Photo",
                PropertyId = _testPropertyId,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(doc1);

            var doc2 = new Document
            {
                OrganizationId = _testOrgId,
                FileName = "property_doc2.jpg",
                FileExtension = ".jpg",
                FileData = new byte[] { 4, 5, 6 },
                FileSize = 3,
                DocumentType = "Photo",
                PropertyId = _testPropertyId,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(doc2);

            // Act
            var result = await _service.GetDocumentsByPropertyIdAsync(_testPropertyId);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.All(result, d => Assert.Equal(_testPropertyId, d.PropertyId));
        }

        [Fact]
        public async Task GetDocumentsByTenantIdAsync_ReturnsTenantDocuments()
        {
            // Arrange
            var document = new Document
            {
                OrganizationId = _testOrgId,
                FileName = "tenant_id.pdf",
                FileExtension = ".pdf",
                FileData = new byte[] { 1, 2, 3 },
                FileSize = 3,
                DocumentType = "Identification",
                TenantId = _testTenantId,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(document);

            // Act
            var result = await _service.GetDocumentsByTenantIdAsync(_testTenantId);

            // Assert
            Assert.Single(result);
            Assert.Equal(_testTenantId, result[0].TenantId);
        }

        [Fact]
        public async Task GetDocumentsByLeaseIdAsync_ReturnsLeaseDocuments()
        {
            // Arrange
            var document = new Document
            {
                OrganizationId = _testOrgId,
                FileName = "lease_agreement.pdf",
                FileExtension = ".pdf",
                FileData = new byte[] { 1, 2, 3 },
                FileSize = 3,
                DocumentType = "Lease Agreement",
                LeaseId = _testLeaseId,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(document);

            // Act
            var result = await _service.GetDocumentsByLeaseIdAsync(_testLeaseId);

            // Assert
            Assert.Single(result);
            Assert.Equal(_testLeaseId, result[0].LeaseId);
        }

        [Fact]
        public async Task GetDocumentsByTypeAsync_ReturnsDocumentsOfType()
        {
            // Arrange - Create documents of different types
            var leaseDoc = new Document
            {
                OrganizationId = _testOrgId,
                FileName = "lease1.pdf",
                FileExtension = ".pdf",
                FileData = new byte[] { 1, 2, 3 },
                FileSize = 3,
                DocumentType = "Lease Agreement",
                LeaseId = _testLeaseId,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(leaseDoc);

            var photo = new Document
            {
                OrganizationId = _testOrgId,
                FileName = "photo.jpg",
                FileExtension = ".jpg",
                FileData = new byte[] { 4, 5, 6 },
                FileSize = 3,
                DocumentType = "Photo",
                PropertyId = _testPropertyId,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(photo);

            // Act
            var leaseResults = await _service.GetDocumentsByTypeAsync("Lease Agreement");
            var photoResults = await _service.GetDocumentsByTypeAsync("Photo");

            // Assert
            Assert.Single(leaseResults);
            Assert.Single(photoResults);
            Assert.Equal("Lease Agreement", leaseResults[0].DocumentType);
            Assert.Equal("Photo", photoResults[0].DocumentType);
        }

        [Fact]
        public async Task SearchDocumentsByFilenameAsync_FindsMatchingDocuments()
        {
            // Arrange
            var doc1 = new Document
            {
                OrganizationId = _testOrgId,
                FileName = "lease_agreement_2025.pdf",
                FileExtension = ".pdf",
                FileData = new byte[] { 1, 2, 3 },
                FileSize = 3,
                DocumentType = "Lease Agreement",
                LeaseId = _testLeaseId,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(doc1);

            var doc2 = new Document
            {
                OrganizationId = _testOrgId,
                FileName = "property_photo.jpg",
                FileExtension = ".jpg",
                FileData = new byte[] { 4, 5, 6 },
                FileSize = 3,
                DocumentType = "Photo",
                PropertyId = _testPropertyId,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(doc2);

            // Act
            var leaseResults = await _service.SearchDocumentsByFilenameAsync("lease");
            var photoResults = await _service.SearchDocumentsByFilenameAsync("photo");

            // Assert
            Assert.Single(leaseResults);
            Assert.Single(photoResults);
            Assert.Contains("lease", leaseResults[0].FileName.ToLower());
            Assert.Contains("photo", photoResults[0].FileName.ToLower());
        }

        [Fact]
        public async Task SearchDocumentsByFilenameAsync_EmptySearch_ReturnsRecentDocuments()
        {
            // Arrange - Create documents directly in database to control CreatedOn
            for (int i = 0; i < 3; i++)
            {
                var doc = new Document
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = _testOrgId,
                    FileName = $"doc{i}.pdf",
                    FileExtension = ".pdf",
                    FileData = new byte[] { 1, 2, 3 },
                    FileSize = 3,
                    DocumentType = "Test",
                    PropertyId = _testPropertyId,
                    CreatedBy = _testUserId,
                    CreatedOn = DateTime.UtcNow.AddMinutes(i), // doc2 is most recent, doc0 is oldest
                    IsDeleted = false
                };
                _context.Documents.Add(doc);
            }
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.SearchDocumentsByFilenameAsync("");

            // Assert
            Assert.Equal(3, result.Count);
            // Should be ordered by most recent first (descending CreatedOn)
            Assert.Equal("doc2.pdf", result[0].FileName); // Most recent
            Assert.Equal("doc1.pdf", result[1].FileName);
            Assert.Equal("doc0.pdf", result[2].FileName); // Oldest
        }

        [Fact]
        public async Task GetDocumentWithRelationsAsync_LoadsAllRelations()
        {
            // Arrange
            var document = new Document
            {
                OrganizationId = _testOrgId,
                FileName = "full_relations.pdf",
                FileExtension = ".pdf",
                FileData = new byte[] { 1, 2, 3 },
                FileSize = 3,
                DocumentType = "Lease Agreement",
                PropertyId = _testPropertyId,
                TenantId = _testTenantId,
                LeaseId = _testLeaseId,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            var created = await _service.CreateAsync(document);

            // Act
            var result = await _service.GetDocumentWithRelationsAsync(created.Id);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Property);
            Assert.NotNull(result.Tenant);
            Assert.NotNull(result.Lease);
            Assert.Equal(_testPropertyId, result.Property.Id);
            Assert.Equal(_testTenantId, result.Tenant!.Id);
            Assert.Equal(_testLeaseId, result.Lease!.Id);
        }

        #endregion

        #region Business Logic Tests

        [Fact]
        public async Task CalculateTotalStorageUsedAsync_ReturnsCorrectTotal()
        {
            // Arrange - Create documents with known sizes
            var doc1 = new Document
            {
                OrganizationId = _testOrgId,
                FileName = "doc1.pdf",
                FileExtension = ".pdf",
                FileData = new byte[1000],
                FileSize = 1000,
                DocumentType = "Test",
                PropertyId = _testPropertyId,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(doc1);

            var doc2 = new Document
            {
                OrganizationId = _testOrgId,
                FileName = "doc2.jpg",
                FileExtension = ".jpg",
                FileData = new byte[2500],
                FileSize = 2500,
                DocumentType = "Photo",
                PropertyId = _testPropertyId,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(doc2);

            // Act
            var totalStorage = await _service.CalculateTotalStorageUsedAsync();

            // Assert
            Assert.Equal(3500, totalStorage);
        }

        [Fact]
        public async Task GetDocumentsByDateRangeAsync_ReturnsDocumentsInRange()
        {
            // Arrange - Create documents at different times
            var oldDoc = new Document
            {
                OrganizationId = _testOrgId,
                FileName = "old.pdf",
                FileExtension = ".pdf",
                FileData = new byte[] { 1, 2, 3 },
                FileSize = 3,
                DocumentType = "Test",
                PropertyId = _testPropertyId,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow.AddMonths(-2)
            };
            _context.Documents.Add(oldDoc);

            var recentDoc = new Document
            {
                OrganizationId = _testOrgId,
                FileName = "recent.pdf",
                FileExtension = ".pdf",
                FileData = new byte[] { 4, 5, 6 },
                FileSize = 3,
                DocumentType = "Test",
                PropertyId = _testPropertyId,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(recentDoc);

            // Act
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow.AddDays(1);
            var result = await _service.GetDocumentsByDateRangeAsync(startDate, endDate);

            // Assert
            Assert.Single(result);
            Assert.Equal("recent.pdf", result[0].FileName);
        }

        [Fact]
        public async Task GetDocumentCountByTypeAsync_ReturnsCorrectCounts()
        {
            // Arrange - Create documents of various types
            var types = new[] { "Lease Agreement", "Lease Agreement", "Photo", "Invoice" };
            foreach (var type in types)
            {
                var doc = new Document
                {
                    OrganizationId = _testOrgId,
                    FileName = $"{type}.pdf",
                    FileExtension = ".pdf",
                    FileData = new byte[] { 1, 2, 3 },
                    FileSize = 3,
                    DocumentType = type,
                    PropertyId = _testPropertyId,
                    CreatedBy = _testUserId,
                    CreatedOn = DateTime.UtcNow
                };
                await _service.CreateAsync(doc);
            }

            // Act
            var counts = await _service.GetDocumentCountByTypeAsync();

            // Assert
            Assert.Equal(2, counts["Lease Agreement"]);
            Assert.Equal(1, counts["Photo"]);
            Assert.Equal(1, counts["Invoice"]);
        }

        #endregion

        #region Organization Isolation Tests

        [Fact]
        public async Task GetByIdAsync_DifferentOrganization_ReturnsNull()
        {
            // Arrange - Create different organization and document
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

            var otherOrgDoc = new Document
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrg.Id,
                FileName = "other_doc.pdf",
                FileExtension = ".pdf",
                FileData = new byte[] { 1, 2, 3 },
                FileSize = 3,
                DocumentType = "Test",
                PropertyId = otherProperty.Id,
                CreatedBy = otherUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _context.Documents.AddAsync(otherOrgDoc);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetByIdAsync(otherOrgDoc.Id);

            // Assert
            Assert.Null(result); // Should not access document from different org
        }

        [Fact]
        public async Task GetAllAsync_ReturnsOnlyCurrentOrganizationDocuments()
        {
            // Arrange - Create document in test org
            var testOrgDoc = new Document
            {
                OrganizationId = _testOrgId,
                FileName = "test_org.pdf",
                FileExtension = ".pdf",
                FileData = new byte[] { 1, 2, 3 },
                FileSize = 3,
                DocumentType = "Test",
                PropertyId = _testPropertyId,
                CreatedBy = _testUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _service.CreateAsync(testOrgDoc);

            // Create document in different org
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

            var otherOrgDoc = new Document
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrg.Id,
                FileName = "other.pdf",
                FileExtension = ".pdf",
                FileData = new byte[] { 4, 5, 6 },
                FileSize = 3,
                DocumentType = "Test",
                PropertyId = otherProperty.Id,
                CreatedBy = otherUserId,
                CreatedOn = DateTime.UtcNow
            };
            await _context.Documents.AddAsync(otherOrgDoc);
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
