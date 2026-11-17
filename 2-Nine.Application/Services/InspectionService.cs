using Nine.Application.Models;
using Nine.Core.Interfaces.Services;
using Nine.Core.Constants;
using Nine.Core.Entities;
using Nine.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nine.Application.Services
{
    /// <summary>
    /// Service for managing property inspections with business logic for scheduling,
    /// tracking, and integration with calendar events.
    /// </summary>
    public class InspectionService : BaseService<Inspection>
    {
        private readonly ICalendarEventService _calendarEventService;

        public InspectionService(
            ApplicationDbContext context,
            ILogger<InspectionService> logger,
            IUserContextService userContext,
            IOptions<ApplicationSettings> settings,
            ICalendarEventService calendarEventService)
            : base(context, logger, userContext, settings)
        {
            _calendarEventService = calendarEventService;
        }

        #region Helper Methods

        protected async Task<string> GetUserIdAsync()
        {
            var userId = await _userContext.GetUserIdAsync();
            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }
            return userId;
        }

        protected async Task<Guid> GetActiveOrganizationIdAsync()
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            if (!organizationId.HasValue)
            {
                throw new UnauthorizedAccessException("No active organization.");
            }
            return organizationId.Value;
        }

        #endregion

        /// <summary>
        /// Validates inspection business rules.
        /// </summary>
        protected override async Task ValidateEntityAsync(Inspection entity)
        {
            var errors = new List<string>();

            // Required fields
            if (entity.PropertyId == Guid.Empty)
            {
                errors.Add("Property is required");
            }

            if (string.IsNullOrWhiteSpace(entity.InspectionType))
            {
                errors.Add("Inspection type is required");
            }

            if (entity.CompletedOn == default)
            {
                errors.Add("Completion date is required");
            }

            if (errors.Any())
            {
                throw new InvalidOperationException(string.Join("; ", errors));
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Gets all inspections for the active organization.
        /// </summary>
        public override async Task<List<Inspection>> GetAllAsync()
        {
            var organizationId = await GetActiveOrganizationIdAsync();

            return await _context.Inspections
                .Include(i => i.Property)
                .Include(i => i.Lease)
                    .ThenInclude(l => l!.Tenant)
                .Where(i => !i.IsDeleted && i.OrganizationId == organizationId)
                .OrderByDescending(i => i.CompletedOn)
                .ToListAsync();
        }

        /// <summary>
        /// Gets inspections by property ID.
        /// </summary>
        public async Task<List<Inspection>> GetByPropertyIdAsync(Guid propertyId)
        {
            var organizationId = await GetActiveOrganizationIdAsync();

            return await _context.Inspections
                .Include(i => i.Property)
                .Include(i => i.Lease)
                    .ThenInclude(l => l!.Tenant)
                .Where(i => i.PropertyId == propertyId && !i.IsDeleted && i.OrganizationId == organizationId)
                .OrderByDescending(i => i.CompletedOn)
                .ToListAsync();
        }

        /// <summary>
        /// Gets the most recent routine inspection for a property.
        /// </summary>
        public async Task<Inspection?> GetLastRoutineInspectionAsync(Guid propertyId)
        {
            var organizationId = await GetActiveOrganizationIdAsync();

            return await _context.Inspections
                .Include(i => i.Property)
                .Include(i => i.Lease)
                    .ThenInclude(l => l!.Tenant)
                .Where(i => i.PropertyId == propertyId 
                    && i.InspectionType == ApplicationConstants.InspectionTypes.Routine
                    && !i.IsDeleted 
                    && i.OrganizationId == organizationId)
                .OrderByDescending(i => i.CompletedOn)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Gets a single inspection by ID with related data.
        /// </summary>
        public override async Task<Inspection?> GetByIdAsync(Guid id)
        {
            var organizationId = await GetActiveOrganizationIdAsync();

            return await _context.Inspections
                .Include(i => i.Property)
                .Include(i => i.Lease)
                    .ThenInclude(l => l!.Tenant)
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted && i.OrganizationId == organizationId);
        }

        /// <summary>
        /// Creates a new inspection with calendar event integration.
        /// </summary>
        public override async Task<Inspection> CreateAsync(Inspection inspection)
        {
            // Call base.CreateAsync to handle:
            // - Sample data propagation from Property
            // - Organization context setup
            // - Audit tracking fields
            // - Validation
            var createdInspection = await base.CreateAsync(inspection);

            // Custom logic: Create calendar event for the inspection
            await _calendarEventService.CreateOrUpdateEventAsync(createdInspection);

            // Custom logic: Update property inspection tracking if this is a routine inspection
            if (createdInspection.InspectionType == ApplicationConstants.InspectionTypes.Routine)
            {
                await HandleRoutineInspectionCompletionAsync(createdInspection);
            }

            _logger.LogInformation("Created inspection {InspectionId} for property {PropertyId}", 
                createdInspection.Id, createdInspection.PropertyId);

            return createdInspection;
        }

        /// <summary>
        /// Updates an existing inspection.
        /// </summary>
        public override async Task<Inspection> UpdateAsync(Inspection inspection)
        {
            await ValidateEntityAsync(inspection);

            var userId = await GetUserIdAsync();
            var organizationId = await GetActiveOrganizationIdAsync();

            // Security: Verify inspection belongs to active organization
            var existing = await _context.Inspections
                .FirstOrDefaultAsync(i => i.Id == inspection.Id && i.OrganizationId == organizationId);

            if (existing == null)
            {
                throw new UnauthorizedAccessException($"Inspection {inspection.Id} not found in active organization.");
            }

            // Set tracking fields
            inspection.LastModifiedBy = userId;
            inspection.LastModifiedOn = DateTime.UtcNow;
            inspection.OrganizationId = organizationId; // Prevent org hijacking

            _context.Entry(existing).CurrentValues.SetValues(inspection);
            await _context.SaveChangesAsync();

            // Update calendar event
            await _calendarEventService.CreateOrUpdateEventAsync(inspection);

            // Update property inspection tracking if routine inspection date changed
            if (inspection.InspectionType == ApplicationConstants.InspectionTypes.Routine)
            {
                await HandleRoutineInspectionCompletionAsync(inspection);
            }

            _logger.LogInformation("Updated inspection {InspectionId}", inspection.Id);

            return inspection;
        }

        /// <summary>
        /// Deletes an inspection and its associated calendar event.
        /// </summary>
        public override async Task<bool> DeleteAsync(Guid id)
        {
            var organizationId = await GetActiveOrganizationIdAsync();

            var inspection = await _context.Inspections
                .FirstOrDefaultAsync(i => i.Id == id && i.OrganizationId == organizationId);

            if (inspection == null)
            {
                throw new KeyNotFoundException($"Inspection {id} not found.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Delete associated calendar event before deleting the inspection
                if (inspection.CalendarEventId.HasValue)
                {
                    var calendarEvent = await _context.CalendarEvents.FindAsync(inspection.CalendarEventId.Value);
                    if (calendarEvent != null) _context.CalendarEvents.Remove(calendarEvent);
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation("Deleted inspection {InspectionId}", id);

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

        /// <summary>
        /// Handles routine inspection completion by updating property tracking and removing old calendar events.
        /// </summary>
        private async Task HandleRoutineInspectionCompletionAsync(Inspection inspection)
        {
            // Find and update/delete the original property-based routine inspection calendar event
            var propertyBasedEvent = await _context.CalendarEvents
                .FirstOrDefaultAsync(e =>
                    e.PropertyId == inspection.PropertyId &&
                    e.SourceEntityType == "Property" &&
                    e.EventType == CalendarEventTypes.Inspection &&
                    !e.IsDeleted);

            if (propertyBasedEvent != null)
            {
                // Remove the old property-based event since we now have an actual inspection record
                _context.CalendarEvents.Remove(propertyBasedEvent);
            }

            // Update property's routine inspection tracking
            var property = await _context.Properties
                .FirstOrDefaultAsync(p => p.Id == inspection.PropertyId);

            if (property != null)
            {
                property.LastRoutineInspectionDate = inspection.CompletedOn;
                
                // Calculate next routine inspection date based on interval
                if (property.RoutineInspectionIntervalMonths > 0)
                {
                    property.NextRoutineInspectionDueDate = inspection.CompletedOn
                        .AddMonths(property.RoutineInspectionIntervalMonths);
                }

                await _context.SaveChangesAsync();
            }
        }

        #region Cascade Summary / Archive / Restore

        /// <summary>
        /// Returns a count of related records that would be permanently deleted with this inspection.
        /// </summary>
        public override Task<CascadeSummary> GetDeleteCascadeSummaryAsync(Guid id)
            => Task.FromResult(new CascadeSummary { EntityName = "Inspection", Counts = new Dictionary<string, int>() });

        /// <summary>
        /// Returns a count of related records that would be archived with this inspection.
        /// </summary>
        public override Task<CascadeSummary> GetArchiveCascadeSummaryAsync(Guid id)
            => Task.FromResult(new CascadeSummary { EntityName = "Inspection", Counts = new Dictionary<string, int>() });

        /// <summary>
        /// Archives an inspection using a direct SQL update to avoid EF change tracking issues.
        /// </summary>
        public override async Task<bool> ArchiveAsync(Guid id)
        {
            var userId = await _userContext.GetUserIdAsync();
            var now = DateTime.UtcNow;

            int rows = await _context.Inspections
                .Where(i => i.Id == id && !i.IsDeleted && !i.IsArchived)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(i => i.IsArchived, true)
                    .SetProperty(i => i.ArchivedOn, now)
                    .SetProperty(i => i.ArchivedBy, userId));

            return rows > 0;
        }

        /// <summary>
        /// Restores an archived inspection using a direct SQL update to avoid EF change tracking issues.
        /// </summary>
        public override async Task<bool> RestoreAsync(Guid id)
        {
            int rows = await _context.Inspections
                .Where(i => i.Id == id && i.IsArchived)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.IsArchived, false));

            return rows > 0;
        }

        #endregion
    }
}
