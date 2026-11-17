using Nine.Application.Models;
using Nine.Core.Interfaces.Services;
using System.ComponentModel.DataAnnotations;
using Nine.Core.Constants;
using Nine.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nine.Application.Services;
using Microsoft.Extensions.Logging;

namespace Nine.Application.Services
{
    /// <summary>
    /// Service for managing Tenant entities.
    /// Inherits common CRUD operations from BaseService and adds tenant-specific business logic.
    /// </summary>
    public class TenantService : BaseService<Tenant>
    {
        public TenantService(
            ApplicationDbContext context,
            ILogger<TenantService> logger,
            IUserContextService userContext,
            IOptions<ApplicationSettings> settings)
            : base(context, logger, userContext, settings)
        {
        }

        #region Overrides with Tenant-Specific Logic

        /// <summary>
        /// Retrieves a tenant by ID with related entities (Leases).
        /// </summary>
        public async Task<Tenant?> GetTenantWithRelationsAsync(Guid tenantId)
        {
            try
            {
                var userId = await _userContext.GetUserIdAsync();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User is not authenticated.");
                }

                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Tenants
                    .Include(t => t.Leases)
                    .FirstOrDefaultAsync(t => t.Id == tenantId && 
                                            t.OrganizationId == organizationId && 
                                            !t.IsDeleted);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetTenantWithRelations");
                throw;
            }
        }

        /// <summary>
        /// Retrieves all tenants with related entities.
        /// </summary>
        public async Task<List<Tenant>> GetTenantsWithRelationsAsync()
        {
            try
            {
                var userId = await _userContext.GetUserIdAsync();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User is not authenticated.");
                }

                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Tenants
                    .Include(t => t.Leases)
                        .ThenInclude(l => l.Property)
                    .Where(t => !t.IsDeleted && !t.IsArchived && t.OrganizationId == organizationId)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetTenantsWithRelations");
                throw;
            }
        }

        /// <summary>
        /// Retrieves all archived tenants with related entities (Leases and their Properties).
        /// </summary>
        public async Task<List<Tenant>> GetArchivedTenantsWithRelationsAsync()
        {
            try
            {
                var userId = await _userContext.GetUserIdAsync();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User is not authenticated.");
                }

                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Tenants
                    .Include(t => t.Leases)
                        .ThenInclude(l => l.Property)
                    .Where(t => !t.IsDeleted && t.IsArchived && t.OrganizationId == organizationId)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetArchivedTenantsWithRelations");
                throw;
            }
        }

        /// <summary>
        /// Validates tenant data before create/update operations.
        /// </summary>
        protected override async Task ValidateEntityAsync(Tenant tenant)
        {
            // Validate required email
            if (string.IsNullOrWhiteSpace(tenant.Email))
            {
                throw new ValidationException("Tenant email is required.");
            }

            // Validate required identification number
            if (string.IsNullOrWhiteSpace(tenant.IdentificationNumber))
            {
                throw new ValidationException("Tenant identification number is required.");
            }

            // Check for duplicate email in same organization
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            var emailExists = await _context.Tenants
                .AnyAsync(t => t.Email == tenant.Email && 
                             t.Id != tenant.Id && 
                             t.OrganizationId == organizationId &&
                             !t.IsDeleted);

            if (emailExists)
            {
                throw new ValidationException($"A tenant with email '{tenant.Email}' already exists.");
            }

            // Check for duplicate identification number in same organization
            var idNumberExists = await _context.Tenants
                .AnyAsync(t => t.IdentificationNumber == tenant.IdentificationNumber && 
                             t.Id != tenant.Id && 
                             t.OrganizationId == organizationId &&
                             !t.IsDeleted);

            if (idNumberExists)
            {
                throw new ValidationException($"A tenant with identification number '{tenant.IdentificationNumber}' already exists.");
            }

            await base.ValidateEntityAsync(tenant);
        }

        #endregion

        #region Business Logic Methods

        /// <summary>
        /// Retrieves a tenant by identification number.
        /// </summary>
        public async Task<Tenant?> GetTenantByIdentificationNumberAsync(string identificationNumber)
        {
            try
            {
                var userId = await _userContext.GetUserIdAsync();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User is not authenticated.");
                }

                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Tenants
                    .Include(t => t.Leases)
                    .FirstOrDefaultAsync(t => t.IdentificationNumber == identificationNumber && 
                                            t.OrganizationId == organizationId && 
                                            !t.IsDeleted);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetTenantByIdentificationNumber");
                throw;
            }
        }

        /// <summary>
        /// Retrieves a tenant by email address.
        /// </summary>
        public async Task<Tenant?> GetTenantByEmailAsync(string email)
        {
            try
            {
                var userId = await _userContext.GetUserIdAsync();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User is not authenticated.");
                }

                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Tenants
                    .Include(t => t.Leases)
                    .FirstOrDefaultAsync(t => t.Email == email && 
                                            t.OrganizationId == organizationId && 
                                            !t.IsDeleted);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetTenantByEmail");
                throw;
            }
        }

        /// <summary>
        /// Retrieves all active tenants (IsActive = true).
        /// </summary>
        public async Task<List<Tenant>> GetActiveTenantsAsync()
        {
            try
            {
                var userId = await _userContext.GetUserIdAsync();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User is not authenticated.");
                }

                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Tenants
                    .Where(t => !t.IsDeleted && 
                               t.IsActive && 
                               t.OrganizationId == organizationId)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetActiveTenants");
                throw;
            }
        }

        /// <summary>
        /// Retrieves all tenants with active leases.
        /// </summary>
        public async Task<List<Tenant>> GetTenantsWithActiveLeasesAsync()
        {
            try
            {
                var userId = await _userContext.GetUserIdAsync();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User is not authenticated.");
                }

                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Tenants
                    .Where(t => !t.IsDeleted && 
                               t.OrganizationId == organizationId)
                    .Where(t => _context.Leases.Any(l =>
                        l.TenantId == t.Id &&
                        l.IsActive &&
                        !l.IsDeleted))
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetTenantsWithActiveLeases");
                throw;
            }
        }

        /// <summary>
        /// Retrieves tenants by property ID (via their leases).
        /// </summary>
        public async Task<List<Tenant>> GetTenantsByPropertyIdAsync(Guid propertyId)
        {
            try
            {
                var userId = await _userContext.GetUserIdAsync();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User is not authenticated.");
                }

                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                var leases = await _context.Leases
                    .Include(l => l.Tenant)
                    .Where(l => l.PropertyId == propertyId && 
                               l.Tenant!.OrganizationId == organizationId && 
                               !l.IsDeleted && 
                               !l.Tenant.IsDeleted)
                    .ToListAsync();

                var tenantIds = leases.Select(l => l.TenantId).Distinct().ToList();

                return await _context.Tenants
                    .Where(t => tenantIds.Contains(t.Id) && 
                               t.OrganizationId == organizationId && 
                               !t.IsDeleted)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetTenantsByPropertyId");
                throw;
            }
        }

        /// <summary>
        /// Retrieves tenants by lease ID.
        /// </summary>
        public async Task<List<Tenant>> GetTenantsByLeaseIdAsync(Guid leaseId)
        {
            try
            {
                var userId = await _userContext.GetUserIdAsync();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User is not authenticated.");
                }

                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                var leases = await _context.Leases
                    .Include(l => l.Tenant)
                    .Where(l => l.Id == leaseId && 
                               l.Tenant!.OrganizationId == organizationId && 
                               !l.IsDeleted && 
                               !l.Tenant.IsDeleted)
                    .ToListAsync();

                var tenantIds = leases.Select(l => l.TenantId).Distinct().ToList();

                return await _context.Tenants
                    .Where(t => tenantIds.Contains(t.Id) && 
                               t.OrganizationId == organizationId && 
                               !t.IsDeleted)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetTenantsByLeaseId");
                throw;
            }
        }

        /// <summary>
        /// Searches tenants by name, email, or identification number.
        /// </summary>
        public async Task<List<Tenant>> SearchTenantsAsync(string searchTerm)
        {
            try
            {
                var userId = await _userContext.GetUserIdAsync();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User is not authenticated.");
                }

                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    return await _context.Tenants
                        .Where(t => !t.IsDeleted && t.OrganizationId == organizationId)
                        .OrderBy(t => t.LastName)
                        .ThenBy(t => t.FirstName)
                        .Take(20)
                        .ToListAsync();
                }

                return await _context.Tenants
                    .Where(t => !t.IsDeleted &&
                               t.OrganizationId == organizationId &&
                               (t.FirstName.Contains(searchTerm) ||
                                t.LastName.Contains(searchTerm) ||
                                t.Email.Contains(searchTerm) ||
                                t.IdentificationNumber.Contains(searchTerm)))
                    .OrderBy(t => t.LastName)
                    .ThenBy(t => t.FirstName)
                    .Take(20)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "SearchTenants");
                throw;
            }
        }

        /// <summary>
        /// Calculates the total outstanding balance for a tenant across all their leases.
        /// </summary>
        public async Task<decimal> CalculateTenantBalanceAsync(Guid tenantId)
        {
            try
            {
                var userId = await _userContext.GetUserIdAsync();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User is not authenticated.");
                }

                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                // Verify tenant exists and belongs to organization
                var tenant = await GetByIdAsync(tenantId);
                if (tenant == null)
                {
                    throw new InvalidOperationException($"Tenant not found: {tenantId}");
                }

                // Calculate total invoiced amount
                var totalInvoiced = await _context.Invoices
                    .Where(i => i.Lease.TenantId == tenantId &&
                               i.Lease.Property.OrganizationId == organizationId &&
                               !i.IsDeleted &&
                               !i.Lease.IsDeleted)
                    .SumAsync(i => i.Amount);

                // Calculate total paid amount
                var totalPaid = await _context.Payments
                    .Where(p => p.Invoice.Lease.TenantId == tenantId &&
                               p.Invoice.Lease.Property.OrganizationId == organizationId &&
                               !p.IsDeleted &&
                               !p.Invoice.IsDeleted)
                    .SumAsync(p => p.Amount);

                return totalInvoiced - totalPaid;
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "CalculateTenantBalance");
                throw;
            }
        }

        #endregion

        #region Delete Override

        /// <summary>
        /// Deletes a tenant and all related records (cascade delete).
        /// Removes leases (with invoices, payments, security deposits) and documents.
        /// </summary>
        public override async Task<bool> DeleteAsync(Guid id)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
            // Get all lease IDs for this tenant
            var leaseIds = await _context.Leases
                .Where(l => l.TenantId == id)
                .Select(l => l.Id)
                .ToListAsync();

            foreach (var leaseId in leaseIds)
            {
                var invoiceIds = await _context.Invoices
                    .Where(i => i.LeaseId == leaseId)
                    .Select(i => i.Id)
                    .ToListAsync();

                foreach (var invoiceId in invoiceIds)
                {
                    var payments = await _context.Payments.Where(p => p.InvoiceId == invoiceId).ToListAsync();
                    _context.Payments.RemoveRange(payments);
                }

                var invoices = await _context.Invoices.Where(i => i.LeaseId == leaseId).ToListAsync();
                _context.Invoices.RemoveRange(invoices);
            }

            var securityDeposits = await _context.SecurityDeposits.Where(s => s.TenantId == id).ToListAsync();
            _context.SecurityDeposits.RemoveRange(securityDeposits);

            var leases = await _context.Leases.Where(l => l.TenantId == id).ToListAsync();
            _context.Leases.RemoveRange(leases);

            var documents = await _context.Documents.Where(d => d.TenantId == id).ToListAsync();
            _context.Documents.RemoveRange(documents);

            await _context.SaveChangesAsync();

            bool result = await base.DeleteAsync(id);
            await transaction.CommitAsync();
            return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        #endregion

        #region Cascade Summary

        /// <summary>
        /// Returns a count of related records that would be permanently deleted with this tenant.
        /// </summary>
        public override async Task<CascadeSummary> GetDeleteCascadeSummaryAsync(Guid id)
        {
            var counts = new Dictionary<string, int>
            {
                ["Leases"] = await _context.Leases.CountAsync(x => x.TenantId == id && !x.IsDeleted),
                ["Documents"] = await _context.Documents.CountAsync(x => x.TenantId == id && !x.IsDeleted),
                ["Security Deposits"] = await _context.SecurityDeposits.CountAsync(x => x.TenantId == id && !x.IsDeleted),
            };
            return new CascadeSummary { EntityName = "Tenant", Counts = counts };
        }

        /// <summary>
        /// Returns a count of related records that would be archived with this tenant.
        /// </summary>
        public override async Task<CascadeSummary> GetArchiveCascadeSummaryAsync(Guid id)
        {
            var counts = new Dictionary<string, int>
            {
                ["Leases"] = await _context.Leases.CountAsync(x => x.TenantId == id && !x.IsDeleted && !x.IsArchived),
                ["Documents"] = await _context.Documents.CountAsync(x => x.TenantId == id && !x.IsDeleted && !x.IsArchived),
            };
            return new CascadeSummary { EntityName = "Tenant", Counts = counts };
        }

        /// <summary>
        /// Returns true if the tenant has any active (non-deleted) leases.
        /// Used to guard the archive action in the UI before showing the archive modal.
        /// </summary>
        public async Task<bool> HasActiveLeasesAsync(Guid id)
        {
            return await _context.Leases
                .AnyAsync(l => l.TenantId == id && l.IsActive && !l.IsDeleted);
        }

        /// <summary>
        /// Archives a tenant using a direct SQL update to avoid EF change tracking issues.
        /// The tenant must have no active leases before archiving.
        /// Only the tenant record is archived; leases remain unchanged.
        /// </summary>
        public override async Task<bool> ArchiveAsync(Guid id)
        {
            bool hasActiveLeases = await _context.Leases
                .AnyAsync(l => l.TenantId == id && l.IsActive && !l.IsDeleted);

            if (hasActiveLeases)
                throw new InvalidOperationException("Cannot archive a tenant with active leases.");

            var userId = await _userContext.GetUserIdAsync();
            var now = DateTime.UtcNow;

            int rows = await _context.Tenants
                .Where(t => t.Id == id && !t.IsDeleted && !t.IsArchived)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.IsArchived, true)
                    .SetProperty(t => t.ArchivedOn, now)
                    .SetProperty(t => t.ArchivedBy, userId));

            return rows > 0;
        }

        /// <summary>
        /// Archives a tenant and all of their inactive leases, cascading to invoices, payments, and documents.
        /// The tenant must have no active leases before archiving.
        /// </summary>
        public async Task<bool> ArchiveWithLeasesAsync(Guid id)
        {
            bool hasActiveLeases = await _context.Leases
                .AnyAsync(l => l.TenantId == id && l.IsActive && !l.IsDeleted);

            if (hasActiveLeases)
                throw new InvalidOperationException("Cannot archive a tenant with active leases.");

            var userId = await _userContext.GetUserIdAsync();
            var now = DateTime.UtcNow;

            List<Guid> leaseIds = await _context.Leases
                .Where(l => l.TenantId == id && !l.IsDeleted && !l.IsArchived)
                .Select(l => l.Id)
                .ToListAsync();

            if (leaseIds.Count > 0)
            {
                List<Guid> invoiceIds = await _context.Invoices
                    .Where(x => leaseIds.Contains(x.LeaseId) && !x.IsDeleted && !x.IsArchived)
                    .Select(x => x.Id)
                    .ToListAsync();

                if (invoiceIds.Count > 0)
                {
                    await _context.Payments
                        .Where(p => invoiceIds.Contains(p.InvoiceId) && !p.IsDeleted && !p.IsArchived)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(p => p.IsArchived, true)
                            .SetProperty(p => p.ArchivedOn, now)
                            .SetProperty(p => p.ArchivedBy, userId));
                }

                await _context.Invoices
                    .Where(x => leaseIds.Contains(x.LeaseId) && !x.IsDeleted && !x.IsArchived)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.IsArchived, true)
                        .SetProperty(x => x.ArchivedOn, now)
                        .SetProperty(x => x.ArchivedBy, userId));

                await _context.Documents
                    .Where(x => x.LeaseId != null && leaseIds.Contains(x.LeaseId.Value) && !x.IsDeleted && !x.IsArchived)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.IsArchived, true)
                        .SetProperty(x => x.ArchivedOn, now)
                        .SetProperty(x => x.ArchivedBy, userId));

                await _context.Leases
                    .Where(l => leaseIds.Contains(l.Id))
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(l => l.IsArchived, true)
                        .SetProperty(l => l.ArchivedOn, now)
                        .SetProperty(l => l.ArchivedBy, userId));
            }

            await _context.Documents
                .Where(x => x.TenantId == id && !x.IsDeleted && !x.IsArchived)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.IsArchived, true)
                    .SetProperty(x => x.ArchivedOn, now)
                    .SetProperty(x => x.ArchivedBy, userId));

            int rows = await _context.Tenants
                .Where(t => t.Id == id && !t.IsDeleted && !t.IsArchived)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.IsArchived, true)
                    .SetProperty(t => t.ArchivedOn, now)
                    .SetProperty(t => t.ArchivedBy, userId));

            return rows > 0;
        }

        /// <summary>
        /// Restores an archived tenant using a direct SQL update to avoid EF change tracking issues.
        /// Leases are not automatically restored and must be restored separately if needed.
        /// </summary>
        public override async Task<bool> RestoreAsync(Guid id)
        {
            int rows = await _context.Tenants
                .Where(t => t.Id == id && t.IsArchived)
                .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.IsArchived, false));

            return rows > 0;
        }

        #endregion
    }
}
