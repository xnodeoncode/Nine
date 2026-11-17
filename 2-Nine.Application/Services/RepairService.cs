using Nine.Core.Constants;
using Nine.Core.Entities;
using Nine.Core.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nine.Application.Services;

/// <summary>
/// Service for managing repairs (work performed on properties WITHOUT workflow/status tracking).
/// Repairs can be standalone or part of a MaintenanceRequest composition.
/// </summary>
public class RepairService : BaseService<Repair>
{
    public RepairService(
        ApplicationDbContext context,
        ILogger<RepairService> logger,
        IUserContextService userContext,
        IOptions<ApplicationSettings> settings)
        : base(context, logger, userContext, settings)
    {
    }

    /// <summary>
    /// Gets a repair by id with its Property eagerly loaded. Overridden because
    /// BaseService.GetByIdAsync does not include any navigation properties by
    /// default; without this, Repair.Property is only populated when another
    /// already-Included query (e.g., the Repairs list) happens to have loaded
    /// the same Property into this request's change tracker first.
    /// </summary>
    public override async Task<Repair?> GetByIdAsync(Guid id)
    {
        var organizationId = await _userContext.GetActiveOrganizationIdAsync();

        return await _context.Repairs
            .Include(r => r.Property)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted && r.OrganizationId == organizationId);
    }

    /// <summary>
    /// Validates repair business rules before create/update.
    /// </summary>
    protected override async Task ValidateEntityAsync(Repair entity)
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();

        // Validate PropertyId exists and belongs to active organization
        var propertyExists = await _context.Properties
            .AnyAsync(p => p.Id == entity.PropertyId && p.OrganizationId == orgId && !p.IsDeleted);
        
        if (!propertyExists)
        {
            throw new InvalidOperationException("Property not found or does not belong to your organization.");
        }

        // Validate MaintenanceRequestId if provided
        if (entity.MaintenanceRequestId.HasValue)
        {
            var maintenanceRequest = await _context.MaintenanceRequests
                .Include(mr => mr.Property)
                .FirstOrDefaultAsync(mr => mr.Id == entity.MaintenanceRequestId.Value && !mr.IsDeleted);

            if (maintenanceRequest == null)
            {
                throw new InvalidOperationException("Maintenance request not found.");
            }

            if (maintenanceRequest.Property?.OrganizationId != orgId)
            {
                throw new InvalidOperationException("Maintenance request does not belong to your organization.");
            }

            // Ensure repair belongs to same property as maintenance request
            if (maintenanceRequest.PropertyId != entity.PropertyId)
            {
                throw new InvalidOperationException("Repair must belong to the same property as the maintenance request.");
            }
        }

        // Validate LeaseId if provided
        if (entity.LeaseId.HasValue)
        {
            var lease = await _context.Leases
                .Include(l => l.Property)
                .FirstOrDefaultAsync(l => l.Id == entity.LeaseId.Value && !l.IsDeleted);

            if (lease == null)
            {
                throw new InvalidOperationException("Lease not found.");
            }

            if (lease.Property?.OrganizationId != orgId)
            {
                throw new InvalidOperationException("Lease does not belong to your organization.");
            }

            // Ensure repair belongs to same property as lease
            if (lease.PropertyId != entity.PropertyId)
            {
                throw new InvalidOperationException("Repair must belong to the same property as the lease.");
            }
        }

        // Validate CompletedOn not in future
        if (entity.CompletedOn.HasValue && entity.CompletedOn.Value > DateTime.UtcNow)
        {
            throw new InvalidOperationException("Completion date cannot be in the future.");
        }

        // Validate Cost is non-negative
        if (entity.Cost < 0)
        {
            throw new InvalidOperationException("Cost cannot be negative.");
        }

        // Validate DurationMinutes is non-negative
        if (entity.DurationMinutes < 0)
        {
            throw new InvalidOperationException("Duration cannot be negative.");
        }

        // Validate warranty dates
        if (entity.WarrantyApplies && entity.WarrantyExpiresOn.HasValue)
        {
            if (!entity.CompletedOn.HasValue)
            {
                throw new InvalidOperationException("Warranty expiration date requires a completion date.");
            }

            if (entity.WarrantyExpiresOn.Value <= entity.CompletedOn.Value)
            {
                throw new InvalidOperationException("Warranty expiration date must be after completion date.");
            }
        }

        await base.ValidateEntityAsync(entity);
    }

    /// <summary>
    /// Gets all repairs for the active organization with Property navigation included.
    /// Overrides base method to include navigation properties.
    /// </summary>
    public override async Task<List<Repair>> GetAllAsync()
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();

        return await _dbSet
            .Where(r => !r.IsDeleted && !r.IsArchived && r.OrganizationId == orgId)
            .Include(r => r.Property)
            .OrderByDescending(r => r.CompletedOn ?? r.CreatedOn)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all archived repairs for the active organization with Property navigation included.
    /// </summary>
    public override async Task<List<Repair>> GetArchivedAsync()
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();

        return await _dbSet
            .Where(r => !r.IsDeleted && r.IsArchived && r.OrganizationId == orgId)
            .Include(r => r.Property)
            .OrderByDescending(r => r.CompletedOn ?? r.CreatedOn)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all repairs for a specific property.
    /// </summary>
    public async Task<List<Repair>> GetRepairsByPropertyAsync(Guid propertyId)
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();

        return await _dbSet
            .Where(r => r.PropertyId == propertyId && !r.IsDeleted)
            .Include(r => r.Property)
            .Include(r => r.MaintenanceRequest)
            .Include(r => r.Lease)
            .Where(r => r.Property!.OrganizationId == orgId)
            .OrderByDescending(r => r.CompletedOn ?? r.CreatedOn)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all repairs that are part of a specific maintenance request.
    /// </summary>
    public async Task<List<Repair>> GetRepairsByMaintenanceRequestAsync(Guid maintenanceRequestId)
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();

        return await _dbSet
            .Where(r => r.MaintenanceRequestId == maintenanceRequestId && !r.IsDeleted)
            .Include(r => r.Property)
            .Include(r => r.MaintenanceRequest)
            .Include(r => r.Lease)
            .Where(r => r.Property!.OrganizationId == orgId)
            .OrderByDescending(r => r.CompletedOn ?? r.CreatedOn)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all standalone repairs (not associated with any maintenance request).
    /// These are repairs logged directly without workflow.
    /// </summary>
    public async Task<List<Repair>> GetStandaloneRepairsAsync()
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();

        return await _dbSet
            .Where(r => r.MaintenanceRequestId == null && !r.IsDeleted)
            .Include(r => r.Property)
            .Include(r => r.Lease)
            .Where(r => r.Property!.OrganizationId == orgId)
            .OrderByDescending(r => r.CompletedOn ?? r.CreatedOn)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all repairs completed within a date range.
    /// </summary>
    public async Task<List<Repair>> GetRepairsByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();

        return await _dbSet
            .Where(r => r.CompletedOn.HasValue && 
                       r.CompletedOn.Value >= startDate && 
                       r.CompletedOn.Value <= endDate && 
                       !r.IsDeleted)
            .Include(r => r.Property)
            .Include(r => r.MaintenanceRequest)
            .Include(r => r.Lease)
            .Where(r => r.Property!.OrganizationId == orgId)
            .OrderByDescending(r => r.CompletedOn)
            .ToListAsync();
    }

    /// <summary>
    /// Gets repairs in progress (CompletedOn is null).
    /// </summary>
    public async Task<List<Repair>> GetRepairsInProgressAsync()
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();

        return await _dbSet
            .Where(r => !r.CompletedOn.HasValue && !r.IsDeleted)
            .Include(r => r.Property)
            .Include(r => r.MaintenanceRequest)
            .Include(r => r.Lease)
            .Where(r => r.Property!.OrganizationId == orgId)
            .OrderByDescending(r => r.CreatedOn)
            .ToListAsync();
    }

    /// <summary>
    /// Gets repair cost summary by property, optionally filtered by date range.
    /// </summary>
    public async Task<Dictionary<Guid, decimal>> GetRepairCostsByPropertyAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();

        var query = _dbSet
            .Where(r => !r.IsDeleted)
            .Include(r => r.Property)
            .Where(r => r.Property!.OrganizationId == orgId);

        if (startDate.HasValue)
        {
            query = query.Where(r => r.CompletedOn.HasValue && r.CompletedOn.Value >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(r => r.CompletedOn.HasValue && r.CompletedOn.Value <= endDate.Value);
        }

        return await query
            .GroupBy(r => r.PropertyId)
            .Select(g => new { PropertyId = g.Key, TotalCost = g.Sum(r => r.Cost) })
            .ToDictionaryAsync(x => x.PropertyId, x => x.TotalCost);
    }

    /// <summary>
    /// Gets all repairs currently under warranty.
    /// </summary>
    public async Task<List<Repair>> GetRepairsUnderWarrantyAsync()
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();
        var today = DateTime.UtcNow;

        return await _dbSet
            .Where(r => r.WarrantyApplies && 
                       r.WarrantyExpiresOn.HasValue && 
                       r.WarrantyExpiresOn.Value >= today && 
                       !r.IsDeleted)
            .Include(r => r.Property)
            .Include(r => r.MaintenanceRequest)
            .Include(r => r.Lease)
            .Where(r => r.Property!.OrganizationId == orgId)
            .OrderBy(r => r.WarrantyExpiresOn)
            .ToListAsync();
    }

    /// <summary>
    /// Gets repairs by type for reporting/analytics.
    /// </summary>
    public async Task<Dictionary<string, int>> GetRepairCountsByTypeAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();

        var query = _dbSet
            .Where(r => !r.IsDeleted)
            .Include(r => r.Property)
            .Where(r => r.Property!.OrganizationId == orgId);

        if (startDate.HasValue)
        {
            query = query.Where(r => r.CompletedOn.HasValue && r.CompletedOn.Value >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(r => r.CompletedOn.HasValue && r.CompletedOn.Value <= endDate.Value);
        }

        return await query
            .GroupBy(r => r.RepairType)
            .Select(g => new { RepairType = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RepairType, x => x.Count);
    }

    /// <summary>
    /// Gets total repair costs for the organization within a date range.
    /// </summary>
    public async Task<decimal> GetTotalRepairCostsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();

        var query = _dbSet
            .Where(r => !r.IsDeleted)
            .Include(r => r.Property)
            .Where(r => r.Property!.OrganizationId == orgId);

        if (startDate.HasValue)
        {
            query = query.Where(r => r.CompletedOn.HasValue && r.CompletedOn.Value >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(r => r.CompletedOn.HasValue && r.CompletedOn.Value <= endDate.Value);
        }

        return await query.SumAsync(r => r.Cost);
    }

    /// <summary>
    /// Marks a repair as completed (sets CompletedOn to current timestamp).
    /// </summary>
    public async Task<Repair?> CompleteRepairAsync(Guid repairId)
    {
        var repair = await GetByIdAsync(repairId);
        if (repair == null) return null;

        if (repair.CompletedOn.HasValue)
        {
            throw new InvalidOperationException("Repair is already marked as completed.");
        }

        repair.CompletedOn = DateTime.UtcNow;
        await UpdateAsync(repair);
        return repair;
    }
}
