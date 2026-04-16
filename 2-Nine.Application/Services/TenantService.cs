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
                    .Where(t => !t.IsDeleted && t.OrganizationId == organizationId)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetTenantsWithRelations");
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
    }
}
