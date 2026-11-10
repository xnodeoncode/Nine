using Nine.Application.Models;
using Nine.Core.Interfaces.Services;
using Nine.Core.Constants;
using Nine.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using Nine.Application.Services;
using Nine.Application.Services.PdfGenerators;

namespace Nine.Application.Services
{
    /// <summary>
    /// Service for managing Document entities.
    /// Inherits common CRUD operations from BaseService and adds document-specific business logic.
    /// </summary>
    public class DocumentService : BaseService<Document>
    {
        public DocumentService(
            ApplicationDbContext context,
            ILogger<DocumentService> logger,
            IUserContextService userContext,
            IOptions<ApplicationSettings> settings)
            : base(context, logger, userContext, settings)
        {
        }

        #region Overrides with Document-Specific Logic

        /// <summary>
        /// Validates a document entity before create/update operations.
        /// </summary>
        protected override async Task ValidateEntityAsync(Document entity)
        {
            var errors = new List<string>();

            // Required field validation
            if (string.IsNullOrWhiteSpace(entity.FileName))
            {
                errors.Add("FileName is required");
            }

            if (string.IsNullOrWhiteSpace(entity.FileExtension))
            {
                errors.Add("FileExtension is required");
            }

            if (string.IsNullOrWhiteSpace(entity.DocumentType))
            {
                errors.Add("DocumentType is required");
            }

            if (entity.FileData == null || entity.FileData.Length == 0)
            {
                errors.Add("FileData is required");
            }

            // Business rule: At least one foreign key must be set
            if (!entity.PropertyId.HasValue 
                && !entity.TenantId.HasValue 
                && !entity.LeaseId.HasValue 
                && !entity.InvoiceId.HasValue 
                && !entity.PaymentId.HasValue
                && !entity.RepairId.HasValue)
            {
                errors.Add("Document must be associated with at least one entity (Property, Tenant, Lease, Invoice, Payment, or Repair)");
            }

            // Validate file size (e.g., max 10MB)
            const long maxFileSizeBytes = 10 * 1024 * 1024; // 10MB
            if (entity.FileSize > maxFileSizeBytes)
            {
                errors.Add($"File size exceeds maximum allowed size of {maxFileSizeBytes / (1024 * 1024)}MB");
            }

            if (errors.Any())
            {
                throw new ValidationException(string.Join("; ", errors));
            }

            await base.ValidateEntityAsync(entity);
        }

        #endregion

        #region Retrieval Methods

        /// <summary>
        /// Gets a document with all related entities.
        /// </summary>
        public async Task<Document?> GetDocumentWithRelationsAsync(Guid documentId)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                var document = await _context.Documents
                    .Include(d => d.Property)
                    .Include(d => d.Tenant)
                    .Include(d => d.Lease)
                        .ThenInclude(l => l!.Property)
                    .Include(d => d.Lease)
                        .ThenInclude(l => l!.Tenant)
                    .Include(d => d.Invoice)
                    .Include(d => d.Payment)
                    .Where(d => d.Id == documentId
                        && !d.IsDeleted
                        && d.OrganizationId == organizationId)
                    .FirstOrDefaultAsync();

                return document;
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetDocumentWithRelations");
                throw;
            }
        }

        /// <summary>
        /// Gets all documents with related entities.
        /// </summary>
        public async Task<List<Document>> GetDocumentsWithRelationsAsync(bool includeArchived = false)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Documents
                    .AsNoTracking()
                    .Include(d => d.Property)
                    .Include(d => d.Tenant)
                    .Include(d => d.Lease)
                        .ThenInclude(l => l!.Property)
                    .Include(d => d.Lease)
                        .ThenInclude(l => l!.Tenant)
                    .Include(d => d.Invoice)
                    .Include(d => d.Payment)
                    .Where(d => !d.IsDeleted && (includeArchived || !d.IsArchived) && d.OrganizationId == organizationId)
                    .OrderByDescending(d => d.CreatedOn)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetDocumentsWithRelations");
                throw;
            }
        }

        #endregion

        #region Business Logic Methods

        /// <summary>
        /// Gets all documents for a specific property.
        /// </summary>
        public async Task<List<Document>> GetDocumentsByPropertyIdAsync(Guid propertyId)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Documents
                    .Include(d => d.Property)
                    .Include(d => d.Tenant)
                    .Include(d => d.Lease)
                    .Where(d => d.PropertyId == propertyId
                        && !d.IsDeleted
                        && !d.IsArchived
                        && d.OrganizationId == organizationId)
                    .OrderByDescending(d => d.CreatedOn)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetDocumentsByPropertyId");
                throw;
            }
        }

        /// <summary>
        /// Gets all documents for a specific tenant.
        /// </summary>
        public async Task<List<Document>> GetDocumentsByTenantIdAsync(Guid tenantId)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Documents
                    .Include(d => d.Property)
                    .Include(d => d.Tenant)
                    .Include(d => d.Lease)
                    .Where(d => d.TenantId == tenantId
                        && !d.IsDeleted
                        && !d.IsArchived
                        && d.OrganizationId == organizationId)
                    .OrderByDescending(d => d.CreatedOn)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetDocumentsByTenantId");
                throw;
            }
        }

        /// <summary>
        /// Gets all documents for a specific lease.
        /// </summary>
        public async Task<List<Document>> GetDocumentsByLeaseIdAsync(Guid leaseId)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Documents
                    .Include(d => d.Lease)
                        .ThenInclude(l => l!.Property)
                    .Include(d => d.Lease)
                        .ThenInclude(l => l!.Tenant)
                    .Where(d => d.LeaseId == leaseId
                        && !d.IsDeleted
                        && !d.IsArchived
                        && d.OrganizationId == organizationId)
                    .OrderByDescending(d => d.CreatedOn)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetDocumentsByLeaseId");
                throw;
            }
        }

        /// <summary>
        /// Gets all documents for a specific invoice.
        /// </summary>
        public async Task<List<Document>> GetDocumentsByInvoiceIdAsync(Guid invoiceId)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Documents
                    .Include(d => d.Invoice)
                    .Where(d => d.InvoiceId == invoiceId
                        && !d.IsDeleted
                        && !d.IsArchived
                        && d.OrganizationId == organizationId)
                    .OrderByDescending(d => d.CreatedOn)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetDocumentsByInvoiceId");
                throw;
            }
        }

        /// <summary>
        /// Gets all documents for a specific payment.
        /// </summary>
        public async Task<List<Document>> GetDocumentsByPaymentIdAsync(Guid paymentId)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Documents
                    .Include(d => d.Payment)
                    .Where(d => d.PaymentId == paymentId
                        && !d.IsDeleted
                        && d.OrganizationId == organizationId)
                    .OrderByDescending(d => d.CreatedOn)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetDocumentsByPaymentId");
                throw;
            }
        }

        /// <summary>
        /// Gets all documents (e.g., receipts) for a specific repair.
        /// </summary>
        public async Task<List<Document>> GetDocumentsByRepairIdAsync(Guid repairId)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Documents
                    .Include(d => d.Repair)
                    .Where(d => d.RepairId == repairId
                        && !d.IsDeleted
                        && !d.IsArchived
                        && d.OrganizationId == organizationId)
                    .OrderByDescending(d => d.CreatedOn)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetDocumentsByRepairId");
                throw;
            }
        }

        /// <summary>
        /// Gets documents by document type (e.g., "Lease Agreement", "Invoice", "Receipt").
        /// </summary>
        public async Task<List<Document>> GetDocumentsByTypeAsync(string documentType)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Documents
                    .Include(d => d.Property)
                    .Include(d => d.Tenant)
                    .Include(d => d.Lease)
                    .Where(d => d.DocumentType == documentType
                        && !d.IsDeleted
                        && d.OrganizationId == organizationId)
                    .OrderByDescending(d => d.CreatedOn)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetDocumentsByType");
                throw;
            }
        }

        /// <summary>
        /// Searches documents by filename.
        /// </summary>
        public async Task<List<Document>> SearchDocumentsByFilenameAsync(string searchTerm, int maxResults = 20)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    // Return recent documents if no search term
                    return await _context.Documents
                        .Include(d => d.Property)
                        .Include(d => d.Tenant)
                        .Include(d => d.Lease)
                        .Where(d => !d.IsDeleted && d.OrganizationId == organizationId)
                        .OrderByDescending(d => d.CreatedOn)
                        .Take(maxResults)
                        .ToListAsync();
                }

                var searchLower = searchTerm.ToLower();

                return await _context.Documents
                    .Include(d => d.Property)
                    .Include(d => d.Tenant)
                    .Include(d => d.Lease)
                    .Where(d => !d.IsDeleted
                        && d.OrganizationId == organizationId
                        && (d.FileName.ToLower().Contains(searchLower)
                            || d.Description.ToLower().Contains(searchLower)))
                    .OrderByDescending(d => d.CreatedOn)
                    .Take(maxResults)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "SearchDocumentsByFilename");
                throw;
            }
        }

        /// <summary>
        /// Calculates total storage used by all documents in the organization (in bytes).
        /// </summary>
        public async Task<long> CalculateTotalStorageUsedAsync()
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Documents
                    .Where(d => !d.IsDeleted && d.OrganizationId == organizationId)
                    .SumAsync(d => d.FileSize);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "CalculateTotalStorageUsed");
                throw;
            }
        }

        /// <summary>
        /// Gets documents uploaded within a specific date range.
        /// </summary>
        public async Task<List<Document>> GetDocumentsByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Documents
                    .Include(d => d.Property)
                    .Include(d => d.Tenant)
                    .Include(d => d.Lease)
                    .Where(d => !d.IsDeleted
                        && d.OrganizationId == organizationId
                        && d.CreatedOn >= startDate
                        && d.CreatedOn <= endDate)
                    .OrderByDescending(d => d.CreatedOn)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetDocumentsByDateRange");
                throw;
            }
        }

        /// <summary>
        /// Gets document count by document type for reporting.
        /// </summary>
        public async Task<Dictionary<string, int>> GetDocumentCountByTypeAsync()
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Documents
                    .Where(d => !d.IsDeleted && d.OrganizationId == organizationId)
                    .GroupBy(d => d.DocumentType)
                    .Select(g => new { Type = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Type, x => x.Count);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetDocumentCountByType");
                throw;
            }
        }

        #endregion

        #region Cascade Summary / Archive / Restore

        /// <summary>
        /// Returns a count of related records that would be permanently deleted with this document.
        /// </summary>
        public override Task<CascadeSummary> GetDeleteCascadeSummaryAsync(Guid id)
            => Task.FromResult(new CascadeSummary { EntityName = "Document", Counts = new Dictionary<string, int>() });

        /// <summary>
        /// Returns a count of related records that would be archived with this document.
        /// </summary>
        public override Task<CascadeSummary> GetArchiveCascadeSummaryAsync(Guid id)
            => Task.FromResult(new CascadeSummary { EntityName = "Document", Counts = new Dictionary<string, int>() });

        /// <summary>
        /// Archives a document using a direct SQL update to avoid EF change tracking issues.
        /// </summary>
        public override async Task<bool> ArchiveAsync(Guid id)
        {
            var userId = await _userContext.GetUserIdAsync();
            var now = DateTime.UtcNow;

            int rows = await _context.Documents
                .Where(d => d.Id == id && !d.IsDeleted && !d.IsArchived)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.IsArchived, true)
                    .SetProperty(d => d.ArchivedOn, now)
                    .SetProperty(d => d.ArchivedBy, userId));

            return rows > 0;
        }

        /// <summary>
        /// Restores an archived document using a direct SQL update to avoid EF change tracking issues.
        /// </summary>
        public override async Task<bool> RestoreAsync(Guid id)
        {
            int rows = await _context.Documents
                .Where(d => d.Id == id && d.IsArchived)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.IsArchived, false));

            return rows > 0;
        }

        #endregion

        #region PDF Generation Methods

        /// <summary>
        /// Generates a lease document PDF.
        /// </summary>
        public async Task<byte[]> GenerateLeaseDocumentAsync(Lease lease)
        {
            return await LeasePdfGenerator.GenerateLeasePdf(lease);
        }

        #endregion
    }
}
