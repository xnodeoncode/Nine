using Nine.Core.Interfaces.Services;
using Nine.Core.Constants;
using Nine.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using Nine.Application.Services;

namespace Nine.Application.Services
{
    /// <summary>
    /// Service for managing Lease entities.
    /// Inherits common CRUD operations from BaseService and adds lease-specific business logic.
    /// </summary>
    public class LeaseService : BaseService<Lease>
    {
        public LeaseService(
            ApplicationDbContext context,
            ILogger<LeaseService> logger,
            IUserContextService userContext,
            IOptions<ApplicationSettings> settings)
            : base(context, logger, userContext, settings)
        {
        }

        #region Overrides with Lease-Specific Logic

        /// <summary>
        /// Validates a lease entity before create/update operations.
        /// </summary>
        protected override async Task ValidateEntityAsync(Lease entity)
        {
            var errors = new List<string>();

            // Required field validation
            if (entity.PropertyId == Guid.Empty)
            {
                errors.Add("PropertyId is required");
            }

            if (entity.TenantId == Guid.Empty)
            {
                errors.Add("TenantId is required");
            }

            if (entity.StartDate == default)
            {
                errors.Add("StartDate is required");
            }

            if (entity.EndDate == default)
            {
                errors.Add("EndDate is required");
            }

            if (entity.MonthlyRent <= 0)
            {
                errors.Add("MonthlyRent must be greater than 0");
            }

            // Business rule validation
            if (entity.EndDate <= entity.StartDate)
            {
                errors.Add("EndDate must be after StartDate");
            }

            // Check for overlapping leases on the same property
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            var overlappingLease = await _context.Leases
                .Include(l => l.Property)
                .Where(l => l.PropertyId == entity.PropertyId
                    && l.Id != entity.Id
                    && !l.IsDeleted
                    && l.Property.OrganizationId == organizationId
                    && (l.Status == ApplicationConstants.LeaseStatuses.Active 
                        || l.Status == ApplicationConstants.LeaseStatuses.Pending))
                .Where(l =>
                    // New lease starts during existing lease
                    (entity.StartDate >= l.StartDate && entity.StartDate <= l.EndDate) ||
                    // New lease ends during existing lease
                    (entity.EndDate >= l.StartDate && entity.EndDate <= l.EndDate) ||
                    // New lease completely encompasses existing lease
                    (entity.StartDate <= l.StartDate && entity.EndDate >= l.EndDate))
                .FirstOrDefaultAsync();

            if (overlappingLease != null)
            {
                errors.Add($"A lease already exists for this property during the specified date range (Lease ID: {overlappingLease.Id})");
            }

            if (errors.Any())
            {
                throw new ValidationException(string.Join("; ", errors));
            }

            await base.ValidateEntityAsync(entity);
        }

        /// <summary>
        /// Creates a new lease and updates the property availability status.
        /// </summary>
        public override async Task<Lease> CreateAsync(Lease entity)
        {
            var lease = await base.CreateAsync(entity);

            // If lease is active, mark property as unavailable
            if (entity.Status == ApplicationConstants.LeaseStatuses.Active)
            {
                var property = await _context.Properties.FindAsync(entity.PropertyId);
                if (property != null)
                {
                    property.Status = ApplicationConstants.PropertyStatuses.Occupied;
                    property.LastModifiedOn = DateTime.UtcNow;
                    property.LastModifiedBy = await _userContext.GetUserIdAsync();
                    _context.Properties.Update(property);
                    await _context.SaveChangesAsync();
                }
            }

            return lease;
        }

        /// <summary>
        /// Updates a lease and manages property status based on lease status changes.
        /// </summary>
        public override async Task<Lease> UpdateAsync(Lease entity)
        {
            // Get the existing lease to check for status changes
            var existingLease = await _context.Leases.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == entity.Id);

            var lease = await base.UpdateAsync(entity);

            // Handle property status when lease becomes active
            if (existingLease != null && 
                existingLease.Status != ApplicationConstants.LeaseStatuses.Active &&
                entity.Status == ApplicationConstants.LeaseStatuses.Active)
            {
                var property = await _context.Properties.FindAsync(entity.PropertyId);
                if (property != null)
                {
                    property.Status = ApplicationConstants.PropertyStatuses.Occupied;
                    property.LastModifiedOn = DateTime.UtcNow;
                    property.LastModifiedBy = await _userContext.GetUserIdAsync();
                    _context.Properties.Update(property);
                    await _context.SaveChangesAsync();
                }
            }

            return lease;
        }

        /// <summary>
        /// Deletes (soft deletes) a lease and updates property availability if needed.
        /// </summary>
        public override async Task<bool> DeleteAsync(Guid id)
        {
            var lease = await GetByIdAsync(id);
            if (lease == null) return false;

            var result = await base.DeleteAsync(id);

            // If lease was active, check if property should be marked available
            if (result && lease.Status == ApplicationConstants.LeaseStatuses.Active)
            {
                var property = await _context.Properties.FindAsync(lease.PropertyId);
                if (property != null)
                {
                    // Check if there are any other active/pending leases for this property
                    var hasOtherActiveLeases = await _context.Leases
                        .AnyAsync(l => l.PropertyId == lease.PropertyId
                            && l.Id != lease.Id
                            && !l.IsDeleted
                            && (l.Status == ApplicationConstants.LeaseStatuses.Active 
                                || l.Status == ApplicationConstants.LeaseStatuses.Pending));

                    if (!hasOtherActiveLeases)
                    {
                        property.Status = ApplicationConstants.PropertyStatuses.Available;
                        property.LastModifiedOn = DateTime.UtcNow;
                        property.LastModifiedBy = await _userContext.GetUserIdAsync();
                        _context.Properties.Update(property);
                        await _context.SaveChangesAsync();
                    }
                }
            }

            return result;
        }

        #endregion

        #region Retrieval Methods

        /// <summary>
        /// Gets a lease with all related entities (Property, Tenant, Documents, Invoices).
        /// </summary>
        public async Task<Lease?> GetLeaseWithRelationsAsync(Guid leaseId)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                var lease = await _context.Leases
                    .Include(l => l.Property)
                    .Include(l => l.Tenant)
                    .Include(l => l.Document)
                    .Include(l => l.Documents)
                    .Include(l => l.Invoices)
                        .ThenInclude(i => i.Payments)
                    .Where(l => l.Id == leaseId
                        && !l.IsDeleted
                        && l.Property.OrganizationId == organizationId)
                    .FirstOrDefaultAsync();

                return lease;
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetLeaseWithRelations");
                throw;
            }
        }

        /// <summary>
        /// Gets all leases with Property and Tenant relations.
        /// </summary>
        public async Task<List<Lease>> GetLeasesWithRelationsAsync()
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Leases
                    .Include(l => l.Property)
                    .Include(l => l.Tenant)
                    .Where(l => !l.IsDeleted && l.Property.OrganizationId == organizationId)
                    .OrderByDescending(l => l.StartDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetLeasesWithRelations");
                throw;
            }
        }

        #endregion

        #region Business Logic Methods

        /// <summary>
        /// Gets all leases for a specific property.
        /// </summary>
        public async Task<List<Lease>> GetLeasesByPropertyIdAsync(Guid propertyId)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Leases
                    .Include(l => l.Property)
                    .Include(l => l.Tenant)
                    .Where(l => l.PropertyId == propertyId
                        && !l.IsDeleted
                        && l.Property.OrganizationId == organizationId)
                    .OrderByDescending(l => l.StartDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetLeasesByPropertyId");
                throw;
            }
        }

        /// <summary>
        /// Gets all leases for a specific tenant.
        /// </summary>
        public async Task<List<Lease>> GetLeasesByTenantIdAsync(Guid tenantId)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Leases
                    .Include(l => l.Property)
                    .Include(l => l.Tenant)
                    .Where(l => l.TenantId == tenantId
                        && !l.IsDeleted
                        && l.Property.OrganizationId == organizationId)
                    .OrderByDescending(l => l.StartDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetLeasesByTenantId");
                throw;
            }
        }

        /// <summary>
        /// Gets all active leases (current leases within their term).
        /// </summary>
        public async Task<List<Lease>> GetActiveLeasesAsync()
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();
                var today = DateTime.Today;

                return await _context.Leases
                    .Include(l => l.Property)
                    .Include(l => l.Tenant)
                    .Where(l => !l.IsDeleted
                        && l.Property.OrganizationId == organizationId
                        && l.Status == ApplicationConstants.LeaseStatuses.Active
                        && l.StartDate <= today
                        && l.EndDate >= today)
                    .OrderBy(l => l.Property.Address)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetActiveLeases");
                throw;
            }
        }

        /// <summary>
        /// Gets leases that are expiring within the specified number of days.
        /// </summary>
        public async Task<List<Lease>> GetLeasesExpiringSoonAsync(int daysThreshold = 90)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();
                var today = DateTime.Today;
                var expirationDate = today.AddDays(daysThreshold);

                return await _context.Leases
                    .Include(l => l.Property)
                    .Include(l => l.Tenant)
                    .Where(l => !l.IsDeleted
                        && l.Property.OrganizationId == organizationId
                        && l.Status == ApplicationConstants.LeaseStatuses.Active
                        && l.EndDate >= today
                        && l.EndDate <= expirationDate)
                    .OrderBy(l => l.EndDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetLeasesExpiringSoon");
                throw;
            }
        }

        /// <summary>
        /// Gets leases by status.
        /// </summary>
        public async Task<List<Lease>> GetLeasesByStatusAsync(string status)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Leases
                    .Include(l => l.Property)
                    .Include(l => l.Tenant)
                    .Where(l => !l.IsDeleted
                        && l.Property.OrganizationId == organizationId
                        && l.Status == status)
                    .OrderByDescending(l => l.StartDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetLeasesByStatus");
                throw;
            }
        }

        /// <summary>
        /// Gets current and upcoming leases for a property (Active or Pending status).
        /// </summary>
        public async Task<List<Lease>> GetCurrentAndUpcomingLeasesByPropertyIdAsync(Guid propertyId)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Leases
                    .Include(l => l.Property)
                    .Include(l => l.Tenant)
                    .Where(l => l.PropertyId == propertyId
                        && !l.IsDeleted
                        && l.Property.OrganizationId == organizationId
                        && (l.Status == ApplicationConstants.LeaseStatuses.Active
                            || l.Status == ApplicationConstants.LeaseStatuses.Pending))
                    .OrderBy(l => l.StartDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetCurrentAndUpcomingLeasesByPropertyId");
                throw;
            }
        }

        /// <summary>
        /// Gets active leases for a specific property.
        /// </summary>
        public async Task<List<Lease>> GetActiveLeasesByPropertyIdAsync(Guid propertyId)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();
                var today = DateTime.Today;

                return await _context.Leases
                    .Include(l => l.Property)
                    .Include(l => l.Tenant)
                    .Where(l => l.PropertyId == propertyId
                        && !l.IsDeleted
                        && l.Property.OrganizationId == organizationId
                        && l.Status == ApplicationConstants.LeaseStatuses.Active
                        && l.StartDate <= today
                        && l.EndDate >= today)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetActiveLeasesByPropertyId");
                throw;
            }
        }

        /// <summary>
        /// Calculates the total rent for a lease over its entire term.
        /// </summary>
        public async Task<decimal> CalculateTotalLeaseValueAsync(Guid leaseId)
        {
            try
            {
                var lease = await GetByIdAsync(leaseId);
                if (lease == null)
                {
                    throw new InvalidOperationException($"Lease not found: {leaseId}");
                }

                var months = ((lease.EndDate.Year - lease.StartDate.Year) * 12) 
                    + lease.EndDate.Month - lease.StartDate.Month;
                
                // Add 1 to include both start and end months
                return lease.MonthlyRent * (months + 1);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "CalculateTotalLeaseValue");
                throw;
            }
        }

        /// <summary>
        /// Updates the status of a lease.
        /// </summary>
        public async Task<Lease> UpdateLeaseStatusAsync(Guid leaseId, string newStatus)
        {
            try
            {
                var lease = await GetByIdAsync(leaseId);
                if (lease == null)
                {
                    throw new InvalidOperationException($"Lease not found: {leaseId}");
                }

                lease.Status = newStatus;

                // Update property availability based on status
                var property = await _context.Properties.FindAsync(lease.PropertyId);
                if (property != null)
                {
                    if (newStatus == ApplicationConstants.LeaseStatuses.Active)
                    {
                        // Property status is already set to Occupied by CreateAsync/UpdateAsync
                    }
                    else if (newStatus == ApplicationConstants.LeaseStatuses.Terminated 
                        || newStatus == ApplicationConstants.LeaseStatuses.Expired)
                    {
                        // Only mark available if no other active leases exist
                        var hasOtherActiveLeases = await _context.Leases
                            .AnyAsync(l => l.PropertyId == lease.PropertyId
                                && l.Id != lease.Id
                                && !l.IsDeleted
                                && (l.Status == ApplicationConstants.LeaseStatuses.Active 
                                    || l.Status == ApplicationConstants.LeaseStatuses.Pending));

                        if (!hasOtherActiveLeases)
                        {
                            property.Status = ApplicationConstants.PropertyStatuses.Available;
                        }
                    }

                    property.LastModifiedOn = DateTime.UtcNow;
                    property.LastModifiedBy = await _userContext.GetUserIdAsync();
                    _context.Properties.Update(property);
                }

                return await UpdateAsync(lease);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "UpdateLeaseStatus");
                throw;
            }
        }

        #endregion
    }
}
