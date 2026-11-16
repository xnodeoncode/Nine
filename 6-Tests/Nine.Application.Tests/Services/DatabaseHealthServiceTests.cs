using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Nine.Core.Entities;
using Nine.Infrastructure.Data;
using Nine.Shared.Services;

namespace Nine.Application.Tests
{
    public class DatabaseHealthServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly TestApplicationDbContext _context;
        private readonly Guid _orgId = Guid.NewGuid();
        private readonly Guid _otherOrgId = Guid.NewGuid();
        private const string UserId = "test-user";

        public DatabaseHealthServiceTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
            _context = new TestApplicationDbContext(options);
            _context.Database.EnsureCreated();

            _context.Users.Add(new ApplicationUser { Id = UserId, UserName = "testuser", Email = "testuser@example.com" });
            foreach (var orgId in new[] { _orgId, _otherOrgId })
            {
                _context.Organizations.Add(new Organization
                {
                    Id = orgId, Name = $"Org {orgId}", OwnerId = UserId, CreatedBy = UserId, CreatedOn = DateTime.UtcNow
                });
            }
            _context.SaveChanges();
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose();
        }

        [Fact]
        public async Task CountActiveRecordsAsync_ExcludesDeletedArchivedAndOtherOrganizations_IgnoringIsActive()
        {
            var activeProperty = NewProperty("1 Active St", _orgId);
            var inactiveProperty = NewProperty("2 Inactive St", _orgId, isActive: false);
            var deletedProperty = NewProperty("3 Deleted St", _orgId, isDeleted: true);
            var archivedProperty = NewProperty("5 Archived St", _orgId, isArchived: true);
            var otherOrgProperty = NewProperty("4 Other St", _otherOrgId);
            _context.Properties.AddRange(activeProperty, inactiveProperty, deletedProperty, archivedProperty, otherOrgProperty);

            var activeTenant = NewTenant("a", _orgId);
            var inactiveTenant = NewTenant("b", _orgId, isActive: false);
            var deletedTenant = NewTenant("c", _orgId, isDeleted: true);
            _context.Tenants.AddRange(activeTenant, inactiveTenant, deletedTenant);

            var activeLease = NewLease(activeProperty.Id, activeTenant.Id, isActive: true);
            var inactiveLease = NewLease(activeProperty.Id, activeTenant.Id, isActive: false);
            var deletedLease = NewLease(activeProperty.Id, activeTenant.Id, isActive: true, isDeleted: true);
            var archivedLease = NewLease(activeProperty.Id, activeTenant.Id, isActive: true, isArchived: true);
            _context.Leases.AddRange(activeLease, inactiveLease, deletedLease, archivedLease);

            var invoice1 = NewInvoice(activeLease.Id, "INV-1");
            var invoice2 = NewInvoice(activeLease.Id, "INV-2");
            var archivedInvoice = NewInvoice(activeLease.Id, "INV-3", isArchived: true);
            var deletedInvoice = NewInvoice(activeLease.Id, "INV-4", isDeleted: true);
            var otherOrgInvoice = NewInvoice(activeLease.Id, "INV-5", orgId: _otherOrgId);
            _context.Invoices.AddRange(invoice1, invoice2, archivedInvoice, deletedInvoice, otherOrgInvoice);
            await _context.SaveChangesAsync();

            _context.Payments.AddRange(
                NewPayment(invoice1.Id),
                NewPayment(invoice1.Id, isArchived: true),
                NewPayment(invoice1.Id, isDeleted: true),
                NewPayment(invoice1.Id, orgId: _otherOrgId));
            await _context.SaveChangesAsync();

            var counts = await DatabaseHealthService.CountActiveRecordsAsync(_context, _orgId);

            Assert.Equal(2, counts.Properties);
            Assert.Equal(2, counts.Tenants);
            Assert.Equal(2, counts.Leases);
            Assert.Equal(2, counts.Invoices);
            Assert.Equal(1, counts.Payments);
        }

        [Fact]
        public void ApplyIndicators_HealthyRecentBackup_HasNoWarningsOrErrors()
        {
            var now = new DateTime(2026, 2, 19, 12, 0, 0);
            var report = HealthyReport(lastBackup: now.AddDays(-1));

            DatabaseHealthService.ApplyIndicators(report, now);

            Assert.True(report.IsHealthy);
            Assert.Empty(report.Warnings);
        }

        [Fact]
        public void ApplyIndicators_StaleOrMissingBackup_WarnsButStaysHealthy()
        {
            var now = new DateTime(2026, 2, 19, 12, 0, 0);

            var stale = HealthyReport(lastBackup: now.AddDays(-8));
            DatabaseHealthService.ApplyIndicators(stale, now);
            Assert.True(stale.IsHealthy);
            Assert.Single(stale.Warnings);

            var none = HealthyReport(lastBackup: null);
            DatabaseHealthService.ApplyIndicators(none, now);
            Assert.True(none.IsHealthy);
            Assert.Single(none.Warnings);
        }

        [Fact]
        public void ApplyIndicators_LargeDatabase_Warns()
        {
            var now = DateTime.Now;
            var report = HealthyReport(lastBackup: now);
            report.FileSizeBytes = DatabaseHealthService.LargeDatabaseBytes + 1;

            DatabaseHealthService.ApplyIndicators(report, now);

            Assert.True(report.IsHealthy);
            Assert.Single(report.Warnings);
        }

        [Fact]
        public void ApplyIndicators_ConnectionIntegrityAndSchemaProblems_AreErrors()
        {
            var now = DateTime.Now;

            var disconnected = HealthyReport(lastBackup: now);
            disconnected.IsConnected = false;
            disconnected.ConnectionMessage = "database is locked";
            DatabaseHealthService.ApplyIndicators(disconnected, now);
            Assert.False(disconnected.IsHealthy);
            Assert.Contains(disconnected.Errors, e => e.Contains("database is locked"));

            var corrupt = HealthyReport(lastBackup: now);
            corrupt.IntegrityPassed = false;
            corrupt.IntegrityMessage = "page 3 corrupt";
            DatabaseHealthService.ApplyIndicators(corrupt, now);
            Assert.False(corrupt.IsHealthy);

            var mismatch = HealthyReport(lastBackup: now);
            mismatch.SchemaMatches = false;
            mismatch.SchemaMessage = "Schema version mismatch!";
            DatabaseHealthService.ApplyIndicators(mismatch, now);
            Assert.False(mismatch.IsHealthy);
            Assert.Contains("Schema version mismatch!", mismatch.Errors);
        }

        [Fact]
        public void ResolveBackupDate_ParsesTimestampFromFileName()
        {
            var date = DatabaseBackupService.ResolveBackupDate("/tmp/Nine_Backup_Manual_20260218_143022.db");

            Assert.Equal(new DateTime(2026, 2, 18, 14, 30, 22), date);
        }

        [Fact]
        public void ResolveBackupDate_FallsBackToLastWriteTime_WhenNameHasNoTimestamp()
        {
            var path = Path.Combine(Path.GetTempPath(), $"custom-{Guid.NewGuid():N}.db");
            File.WriteAllText(path, "x");
            try
            {
                var expected = new DateTime(2025, 1, 2, 3, 4, 5);
                File.SetLastWriteTime(path, expected);

                Assert.Equal(expected, DatabaseBackupService.ResolveBackupDate(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static DatabaseHealthReport HealthyReport(DateTime? lastBackup) => new()
        {
            IsConnected = true,
            IntegrityPassed = true,
            IntegrityMessage = "ok",
            SchemaMatches = true,
            FileSizeBytes = 1024,
            LastBackupDate = lastBackup,
            BackupCount = lastBackup == null ? 0 : 1
        };

        private static Property NewProperty(string address, Guid orgId, bool isActive = true, bool isDeleted = false, bool isArchived = false) => new()
        {
            Id = Guid.NewGuid(), OrganizationId = orgId, Address = address, City = "City", State = "ST",
            ZipCode = "12345", IsActive = isActive, IsDeleted = isDeleted, IsArchived = isArchived, CreatedBy = UserId, CreatedOn = DateTime.UtcNow
        };

        private Tenant NewTenant(string key, Guid orgId, bool isActive = true, bool isDeleted = false) => new()
        {
            Id = Guid.NewGuid(), OrganizationId = orgId, FirstName = "T", LastName = key, Email = $"{key}@test.com",
            IdentificationNumber = $"ID-{key}", IsActive = isActive, IsDeleted = isDeleted, CreatedBy = UserId, CreatedOn = DateTime.UtcNow
        };

        private Lease NewLease(Guid propertyId, Guid tenantId, bool isActive, bool isDeleted = false, bool isArchived = false) => new()
        {
            Id = Guid.NewGuid(), OrganizationId = _orgId, PropertyId = propertyId, TenantId = tenantId,
            StartDate = DateTime.Today, EndDate = DateTime.Today.AddYears(1), MonthlyRent = 1500, SecurityDeposit = 1500,
            Status = "Active", IsActive = isActive, IsDeleted = isDeleted, IsArchived = isArchived, CreatedBy = UserId, CreatedOn = DateTime.UtcNow
        };

        private Invoice NewInvoice(Guid leaseId, string number, Guid? orgId = null, bool isArchived = false, bool isDeleted = false) => new()
        {
            Id = Guid.NewGuid(), OrganizationId = orgId ?? _orgId, LeaseId = leaseId, InvoiceNumber = number,
            InvoicedOn = DateTime.Today, DueOn = DateTime.Today.AddDays(30), Amount = 1500, Description = "Test",
            Status = "Pending", AmountPaid = 0, IsArchived = isArchived, IsDeleted = isDeleted,
            CreatedBy = UserId, CreatedOn = DateTime.UtcNow
        };

        private Payment NewPayment(Guid invoiceId, Guid? orgId = null, bool isArchived = false, bool isDeleted = false) => new()
        {
            Id = Guid.NewGuid(), OrganizationId = orgId ?? _orgId, InvoiceId = invoiceId, Amount = 100,
            PaymentNumber = $"PAY-{Guid.NewGuid():N}",
            PaidOn = DateTime.Today, PaymentMethod = "Check", IsArchived = isArchived, IsDeleted = isDeleted,
            CreatedBy = UserId, CreatedOn = DateTime.UtcNow
        };
    }
}
