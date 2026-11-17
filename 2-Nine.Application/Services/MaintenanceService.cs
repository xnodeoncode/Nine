using Nine.Application.Models;
using Nine.Core.Interfaces.Services;
using Nine.Application.Services.Workflows;
using Nine.Core.Constants;
using Nine.Core.Entities;
using Nine.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using Nine.Application.Services;

namespace Nine.Application.Services
{
    /// <summary>
    /// Service for managing maintenance requests with business logic for status updates,
    /// assignment tracking, and overdue detection.
    /// </summary>
    public class MaintenanceService : BaseService<MaintenanceRequest>
    {
        private readonly ICalendarEventService _calendarEventService;

        public MaintenanceService(
            ApplicationDbContext context,
            ILogger<MaintenanceService> logger,
            IUserContextService userContext,
            IOptions<ApplicationSettings> settings,
            ICalendarEventService calendarEventService)
            : base(context, logger, userContext, settings)
        {
            _calendarEventService = calendarEventService;
        }

        /// <summary>
        /// Validates maintenance request business rules.
        /// </summary>
        protected override async Task ValidateEntityAsync(MaintenanceRequest entity)
        {
            var errors = new List<string>();

            // Required fields
            if (entity.PropertyId == Guid.Empty)
            {
                errors.Add("Property is required");
            }

            if (string.IsNullOrWhiteSpace(entity.Title))
            {
                errors.Add("Title is required");
            }

            if (string.IsNullOrWhiteSpace(entity.Description))
            {
                errors.Add("Description is required");
            }

            if (string.IsNullOrWhiteSpace(entity.RequestType))
            {
                errors.Add("Request type is required");
            }

            if (string.IsNullOrWhiteSpace(entity.Priority))
            {
                errors.Add("Priority is required");
            }

            if (string.IsNullOrWhiteSpace(entity.Status))
            {
                errors.Add("Status is required");
            }

            // Validate priority
            var validPriorities = new[] { "Low", "Medium", "High", "Urgent" };
            if (!validPriorities.Contains(entity.Priority))
            {
                errors.Add($"Priority must be one of: {string.Join(", ", validPriorities)}");
            }

            // Validate status
            var validStatuses = new[] { "Submitted", "In Progress", "Completed", "Cancelled" };
            if (!validStatuses.Contains(entity.Status))
            {
                errors.Add($"Status must be one of: {string.Join(", ", validStatuses)}");
            }

            // Validate dates
            if (entity.RequestedOn > DateTime.Today)
            {
                errors.Add("Requested date cannot be in the future");
            }

            if (entity.ScheduledOn.HasValue && entity.ScheduledOn.Value.Date < entity.RequestedOn.Date)
            {
                errors.Add("Scheduled date cannot be before requested date");
            }

            if (entity.CompletedOn.HasValue && entity.CompletedOn.Value.Date < entity.RequestedOn.Date)
            {
                errors.Add("Completed date cannot be before requested date");
            }

            // Validate costs
            if (entity.EstimatedCost < 0)
            {
                errors.Add("Estimated cost cannot be negative");
            }

            if (entity.ActualCost < 0)
            {
                errors.Add("Actual cost cannot be negative");
            }

            // Validate status-specific rules
            if (entity.Status == "Completed")
            {
                if (!entity.CompletedOn.HasValue)
                {
                    errors.Add("Completed date is required when status is Completed");
                }
            }

            // Verify property exists and belongs to organization
            if (entity.PropertyId != Guid.Empty)
            {
                var property = await _context.Properties
                    .FirstOrDefaultAsync(p => p.Id == entity.PropertyId && !p.IsDeleted);

                if (property == null)
                {
                    errors.Add($"Property with ID {entity.PropertyId} not found");
                }
                else if (property.OrganizationId != entity.OrganizationId)
                {
                    errors.Add("Property does not belong to the same organization");
                }
            }

            // If LeaseId is provided, verify it exists and belongs to the same property
            if (entity.LeaseId.HasValue && entity.LeaseId.Value != Guid.Empty)
            {
                var lease = await _context.Leases
                    .FirstOrDefaultAsync(l => l.Id == entity.LeaseId.Value && !l.IsDeleted);

                if (lease == null)
                {
                    errors.Add($"Lease with ID {entity.LeaseId.Value} not found");
                }
                else if (lease.PropertyId != entity.PropertyId)
                {
                    errors.Add("Lease does not belong to the specified property");
                }
                else if (lease.OrganizationId != entity.OrganizationId)
                {
                    errors.Add("Lease does not belong to the same organization");
                }
            }

            if (errors.Any())
            {
                throw new ValidationException(string.Join("; ", errors));
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Creates a maintenance request and automatically creates a calendar event.
        /// </summary>
        public override async Task<MaintenanceRequest> CreateAsync(MaintenanceRequest entity)
        {
            var maintenanceRequest = await base.CreateAsync(entity);

            // Create calendar event for the maintenance request
            await _calendarEventService.CreateOrUpdateEventAsync(maintenanceRequest);

            return maintenanceRequest;
        }

        /// <summary>
        /// Updates a maintenance request and synchronizes the calendar event.
        /// </summary>
        public override async Task<MaintenanceRequest> UpdateAsync(MaintenanceRequest entity)
        {
            var maintenanceRequest = await base.UpdateAsync(entity);

            // Update calendar event
            await _calendarEventService.CreateOrUpdateEventAsync(maintenanceRequest);

            return maintenanceRequest;
        }

        /// <summary>
        /// Deletes a maintenance request, nulling out repair associations and removing the calendar event.
        /// </summary>
        public override async Task<bool> DeleteAsync(Guid id)
        {
            var maintenanceRequest = await GetByIdAsync(id);

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Delete repairs associated with this maintenance request
                var repairs = await _context.Repairs.Where(r => r.MaintenanceRequestId == id).ToListAsync();
                _context.Repairs.RemoveRange(repairs);
                await _context.SaveChangesAsync();

                bool result = await base.DeleteAsync(id);

                if (result && maintenanceRequest != null)
                {
                    // Delete associated calendar event
                    await _calendarEventService.DeleteEventAsync(maintenanceRequest.CalendarEventId);
                }

                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Gets all maintenance requests for a specific property.
        /// </summary>
        public async Task<List<MaintenanceRequest>> GetMaintenanceRequestsByPropertyAsync(Guid propertyId)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            return await _context.MaintenanceRequests
                .Include(m => m.Property)
                .Include(m => m.Lease)
                .Where(m => m.PropertyId == propertyId && 
                           m.OrganizationId == organizationId && 
                           !m.IsDeleted)
                .OrderByDescending(m => m.RequestedOn)
                .ToListAsync();
        }

        /// <summary>
        /// Gets all maintenance requests for a specific lease.
        /// </summary>
        public async Task<List<MaintenanceRequest>> GetMaintenanceRequestsByLeaseAsync(Guid leaseId)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            return await _context.MaintenanceRequests
                .Include(m => m.Property)
                .Include(m => m.Lease)
                .Where(m => m.LeaseId == leaseId && 
                           m.OrganizationId == organizationId && 
                           !m.IsDeleted)
                .OrderByDescending(m => m.RequestedOn)
                .ToListAsync();
        }

        /// <summary>
        /// Gets maintenance requests by status.
        /// </summary>
        public async Task<List<MaintenanceRequest>> GetMaintenanceRequestsByStatusAsync(string status)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            return await _context.MaintenanceRequests
                .Include(m => m.Property)
                .Include(m => m.Lease)
                .Where(m => m.Status == status && 
                           m.OrganizationId == organizationId && 
                           !m.IsDeleted)
                .OrderByDescending(m => m.RequestedOn)
                .ToListAsync();
        }

        /// <summary>
        /// Gets maintenance requests by priority level.
        /// </summary>
        public async Task<List<MaintenanceRequest>> GetMaintenanceRequestsByPriorityAsync(string priority)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            return await _context.MaintenanceRequests
                .Include(m => m.Property)
                .Include(m => m.Lease)
                .Where(m => m.Priority == priority && 
                           m.OrganizationId == organizationId && 
                           !m.IsDeleted)
                .OrderByDescending(m => m.RequestedOn)
                .ToListAsync();
        }

        /// <summary>
        /// Gets overdue maintenance requests (scheduled date has passed but not completed).
        /// </summary>
        public async Task<List<MaintenanceRequest>> GetOverdueMaintenanceRequestsAsync()
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            var today = DateTime.Today;

            return await _context.MaintenanceRequests
                .Include(m => m.Property)
                .Include(m => m.Lease)
                .Where(m => m.OrganizationId == organizationId && 
                           !m.IsDeleted &&
                           m.Status != "Completed" &&
                           m.Status != "Cancelled" &&
                           m.ScheduledOn.HasValue &&
                           m.ScheduledOn.Value.Date < today)
                .OrderBy(m => m.ScheduledOn)
                .ToListAsync();
        }

        /// <summary>
        /// Gets the count of open (not completed/cancelled) maintenance requests.
        /// </summary>
        public async Task<int> GetOpenMaintenanceRequestCountAsync()
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            return await _context.MaintenanceRequests
                .Where(m => m.OrganizationId == organizationId && 
                           !m.IsDeleted &&
                           m.Status != "Completed" &&
                           m.Status != "Cancelled")
                .CountAsync();
        }

        /// <summary>
        /// Gets the count of urgent priority maintenance requests.
        /// </summary>
        public async Task<int> GetUrgentMaintenanceRequestCountAsync()
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            return await _context.MaintenanceRequests
                .Where(m => m.OrganizationId == organizationId && 
                           !m.IsDeleted &&
                           m.Priority == "Urgent" &&
                           m.Status != "Completed" &&
                           m.Status != "Cancelled")
                .CountAsync();
        }

        /// <summary>
        /// Gets a maintenance request with all related entities loaded.
        /// </summary>
        public async Task<MaintenanceRequest?> GetMaintenanceRequestWithRelationsAsync(Guid id)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            return await _context.MaintenanceRequests
                .Include(m => m.Property)
                .Include(m => m.Lease)
                    .ThenInclude(l => l!.Tenant)
                .FirstOrDefaultAsync(m => m.Id == id && 
                                        m.OrganizationId == organizationId && 
                                        !m.IsDeleted);
        }

        /// <summary>
        /// Updates the status of a maintenance request with automatic date tracking.
        /// </summary>
        public async Task<MaintenanceRequest> UpdateMaintenanceRequestStatusAsync(Guid id, string status)
        {
            var maintenanceRequest = await GetByIdAsync(id);
            
            if (maintenanceRequest == null)
            {
                throw new ValidationException($"Maintenance request {id} not found");
            }

            maintenanceRequest.Status = status;

            // Auto-set completed date when marked as completed
            if (status == "Completed" && !maintenanceRequest.CompletedOn.HasValue)
            {
                maintenanceRequest.CompletedOn = DateTime.Today;
            }

            return await UpdateAsync(maintenanceRequest);
        }

        /// <summary>
        /// Assigns a maintenance request to a contractor or maintenance person.
        /// </summary>
        public async Task<MaintenanceRequest> AssignMaintenanceRequestAsync(Guid id, string assignedTo, DateTime? scheduledOn = null)
        {
            var maintenanceRequest = await GetByIdAsync(id);
            
            if (maintenanceRequest == null)
            {
                throw new ValidationException($"Maintenance request {id} not found");
            }

            maintenanceRequest.AssignedTo = assignedTo;
            
            if (scheduledOn.HasValue)
            {
                maintenanceRequest.ScheduledOn = scheduledOn.Value;
            }

            // Auto-update status to In Progress if still Submitted
            if (maintenanceRequest.Status == "Submitted")
            {
                maintenanceRequest.Status = "In Progress";
            }

            return await UpdateAsync(maintenanceRequest);
        }

        /// <summary>
        /// Completes a maintenance request with actual cost and resolution notes.
        /// </summary>
        public async Task<MaintenanceRequest> CompleteMaintenanceRequestAsync(
            Guid id, 
            decimal actualCost, 
            string resolutionNotes)
        {
            var maintenanceRequest = await GetByIdAsync(id);
            
            if (maintenanceRequest == null)
            {
                throw new ValidationException($"Maintenance request {id} not found");
            }

            maintenanceRequest.Status = "Completed";
            maintenanceRequest.CompletedOn = DateTime.Today;
            maintenanceRequest.ActualCost = actualCost;
            maintenanceRequest.ResolutionNotes = resolutionNotes;

            return await UpdateAsync(maintenanceRequest);
        }

        /// <summary>
        /// Gets maintenance requests assigned to a specific person.
        /// </summary>
        public async Task<List<MaintenanceRequest>> GetMaintenanceRequestsByAssigneeAsync(string assignedTo)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            return await _context.MaintenanceRequests
                .Include(m => m.Property)
                .Include(m => m.Lease)
                .Where(m => m.AssignedTo == assignedTo && 
                           m.OrganizationId == organizationId && 
                           !m.IsDeleted &&
                           m.Status != "Completed" &&
                           m.Status != "Cancelled")
                .OrderByDescending(m => m.Priority == "Urgent")
                .ThenByDescending(m => m.Priority == "High")
                .ThenBy(m => m.ScheduledOn)
                .ToListAsync();
        }

        /// <summary>
        /// Calculates average days to complete maintenance requests.
        /// </summary>
        public async Task<double> CalculateAverageDaysToCompleteAsync()
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            var completedRequests = await _context.MaintenanceRequests
                .Where(m => m.OrganizationId == organizationId && 
                           !m.IsDeleted &&
                           m.Status == "Completed" &&
                           m.CompletedOn.HasValue)
                .Select(m => new { m.RequestedOn, m.CompletedOn })
                .ToListAsync();

            if (!completedRequests.Any())
            {
                return 0;
            }

            var totalDays = completedRequests.Sum(r => (r.CompletedOn!.Value.Date - r.RequestedOn.Date).Days);
            return (double)totalDays / completedRequests.Count;
        }

        /// <summary>
        /// Gets maintenance cost summary by property.
        /// </summary>
        public async Task<Dictionary<Guid, decimal>> GetMaintenanceCostsByPropertyAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            
            var query = _context.MaintenanceRequests
                .Where(m => m.OrganizationId == organizationId && 
                           !m.IsDeleted &&
                           m.Status == "Completed");

            if (startDate.HasValue)
            {
                query = query.Where(m => m.CompletedOn >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(m => m.CompletedOn <= endDate.Value);
            }

            return await query
                .GroupBy(m => m.PropertyId)
                .Select(g => new { PropertyId = g.Key, TotalCost = g.Sum(m => m.ActualCost) })
                .ToDictionaryAsync(x => x.PropertyId, x => x.TotalCost);
        }

        #region Cascade Summary

        /// <summary>
        /// Returns a count of related records that would be permanently deleted with this maintenance request.
        /// </summary>
        public override async Task<CascadeSummary> GetDeleteCascadeSummaryAsync(Guid id)
        {
            var counts = new Dictionary<string, int>
            {
                ["Repairs"] = await _context.Repairs.CountAsync(x => x.MaintenanceRequestId == id && !x.IsDeleted),
            };
            return new CascadeSummary { EntityName = "Maintenance Request", Counts = counts };
        }

        /// <summary>
        /// Returns a count of related records that would be archived with this maintenance request.
        /// </summary>
        public override async Task<CascadeSummary> GetArchiveCascadeSummaryAsync(Guid id)
        {
            var counts = new Dictionary<string, int>
            {
                ["Repairs"] = await _context.Repairs.CountAsync(x => x.MaintenanceRequestId == id && !x.IsDeleted && !x.IsArchived),
            };
            return new CascadeSummary { EntityName = "Maintenance Request", Counts = counts };
        }

        #endregion

        #region Archive / Restore

        /// <summary>
        /// Archives a maintenance request and cascades to its repairs.
        /// </summary>
        public override async Task<bool> ArchiveAsync(Guid id)
        {
            var userId = await _userContext.GetUserIdAsync();
            var now = DateTime.UtcNow;

            await _context.Repairs
                .Where(r => r.MaintenanceRequestId == id && !r.IsDeleted && !r.IsArchived)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.IsArchived, true)
                    .SetProperty(r => r.ArchivedOn, now)
                    .SetProperty(r => r.ArchivedBy, userId));

            int rows = await _context.MaintenanceRequests
                .Where(m => m.Id == id && !m.IsDeleted && !m.IsArchived)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.IsArchived, true)
                    .SetProperty(m => m.ArchivedOn, now)
                    .SetProperty(m => m.ArchivedBy, userId));

            return rows > 0;
        }

        /// <summary>
        /// Restores an archived maintenance request. Repairs are not automatically restored.
        /// </summary>
        public override async Task<bool> RestoreAsync(Guid id)
        {
            int rows = await _context.MaintenanceRequests
                .Where(m => m.Id == id && m.IsArchived)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsArchived, false));

            return rows > 0;
        }

        #endregion
    }
}
