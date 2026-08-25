using Nine.Application.Models;
using Nine.Core.Interfaces.Services;
using System.ComponentModel.DataAnnotations;
using Nine.Core.Constants;
using Nine.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Nine.Application.Services
{
    /// <summary>
    /// Service for managing Property entities.
    /// Inherits common CRUD operations from BaseService and adds property-specific business logic.
    /// </summary>
    public class PropertyService : BaseService<Property>
    {
        private readonly CalendarEventService _calendarEventService;
        private readonly ApplicationSettings _appSettings;

        private readonly NotificationService _notificationService;

        public PropertyService(
            ApplicationDbContext context,
            ILogger<PropertyService> logger,
            IUserContextService userContext,
            IOptions<ApplicationSettings> settings,
            CalendarEventService calendarEventService, NotificationService notificationService)
            : base(context, logger, userContext, settings)
        {
            _calendarEventService = calendarEventService;
            _notificationService = notificationService;
            _appSettings = settings.Value;
        }

        #region Overrides with Property-Specific Logic

        /// <summary>
        /// Creates a new property with initial routine inspection scheduling.
        /// </summary>
        public override async Task<Property> CreateAsync(Property property)
        {
            // Set initial routine inspection due date to 30 days from creation
            property.NextRoutineInspectionDueDate = DateTime.Today.AddDays(30);

            // Call base create (handles audit fields, org assignment, validation)
            var createdProperty = await base.CreateAsync(property);

            // Create calendar event for the first routine inspection
            await CreateRoutineInspectionCalendarEventAsync(createdProperty);

            return createdProperty;
        }

        /// <summary>
        /// Retrieves a property by ID with related entities (Leases, Documents).
        /// </summary>
        public async Task<Property?> GetPropertyWithRelationsAsync(Guid propertyId)
        {
            try
            {
                var userId = await _userContext.GetUserIdAsync();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User is not authenticated.");
                }

                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Properties
                    .Include(p => p.Leases)
                    .Include(p => p.Documents)
                    .Include(p => p.Repairs)
                    .Include(p => p.MaintenanceRequests)
                    .FirstOrDefaultAsync(p => p.Id == propertyId && 
                                            p.OrganizationId == organizationId && 
                                            !p.IsDeleted);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetPropertyWithRelations");
                throw;
            }
        }

        /// <summary>
        /// Retrieves all properties with related entities.
        /// </summary>
        public async Task<List<Property>> GetPropertiesWithRelationsAsync()
        {
            try
            {
                var userId = await _userContext.GetUserIdAsync();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User is not authenticated.");
                }

                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Properties
                    .Include(p => p.Leases)
                    .Include(p => p.Documents)
                    .Include(p => p.Repairs)
                    .Include(p => p.MaintenanceRequests)
                    .Where(p => !p.IsDeleted && p.OrganizationId == organizationId)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetPropertiesWithRelations");
                throw;
            }
        }

        /// <summary>
        /// Validates property data before create/update operations.
        /// </summary>
        protected override async Task ValidateEntityAsync(Property property)
        {
            // Validate required address
            if (string.IsNullOrWhiteSpace(property.Address))
            {
                throw new ValidationException("Property address is required.");
            }

            // Check for duplicate address in same organization
            var userId = await _userContext.GetUserIdAsync();
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            var exists = await _context.Properties
                .AnyAsync(p => p.Address == property.Address && 
                             p.City == property.City &&
                             p.State == property.State &&
                             p.ZipCode == property.ZipCode &&
                             p.Id != property.Id && 
                             p.OrganizationId == organizationId &&
                             !p.IsDeleted);

            if (exists)
            {
                throw new ValidationException($"A property with address '{property.Address}' already exists in this location.");
            }

            // Inactive properties must have an inactive status (Off Market or Under Renovation)
            if (!property.IsActive && !ApplicationConstants.PropertyStatuses.InactiveStatuses.Contains(property.Status))
            {
                throw new ValidationException(
                    $"An inactive property must have a status of '{ApplicationConstants.PropertyStatuses.OffMarket}' or '{ApplicationConstants.PropertyStatuses.UnderRenovation}'.");
            }

            // Properties with an inactive status must be marked inactive
            if (property.IsActive && ApplicationConstants.PropertyStatuses.InactiveStatuses.Contains(property.Status))
            {
                throw new ValidationException(
                    $"A property with status '{property.Status}' must be marked as inactive.");
            }

            await base.ValidateEntityAsync(property);
        }

        #endregion

        #region Business Logic Methods

        /// <summary>
        /// Searches properties by address, city, state, or zip code.
        /// </summary>
        public async Task<List<Property>> SearchPropertiesByAddressAsync(string searchTerm)
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
                    return await _context.Properties
                        .Where(p => !p.IsDeleted && p.OrganizationId == organizationId)
                        .OrderBy(p => p.Address)
                        .Take(20)
                        .ToListAsync();
                }

                return await _context.Properties
                    .Where(p => !p.IsDeleted &&
                               p.OrganizationId == organizationId &&
                               (p.Address.Contains(searchTerm) ||
                                p.City.Contains(searchTerm) ||
                                p.State.Contains(searchTerm) ||
                                p.ZipCode.Contains(searchTerm)))
                    .OrderBy(p => p.Address)
                    .Take(20)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "SearchPropertiesByAddress");
                throw;
            }
        }

        /// <summary>
        /// Retrieves all vacant properties (no active leases).
        /// </summary>
        public async Task<List<Property>> GetVacantPropertiesAsync()
        {
            try
            {
                var userId = await _userContext.GetUserIdAsync();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User is not authenticated.");
                }

                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Properties
                    .Where(p => !p.IsDeleted && 
                               p.IsActive && 
                               p.OrganizationId == organizationId)
                    .Where(p => !_context.Leases.Any(l =>
                        l.PropertyId == p.Id &&
                        l.IsActive &&
                        !l.IsDeleted))
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetVacantProperties");
                throw;
            }
        }

        /// <summary>
        /// Calculates the overall occupancy rate for the organization.
        /// </summary>
        public async Task<decimal> CalculateOccupancyRateAsync()
        {
            try
            {
                var userId = await _userContext.GetUserIdAsync();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User is not authenticated.");
                }

                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                var totalProperties = await _context.Properties
                    .CountAsync(p => !p.IsDeleted && p.OrganizationId == organizationId);

                if (totalProperties == 0)
                {
                    return 0;
                }

                var occupiedProperties = await _context.Properties
                    .CountAsync(p => !p.IsDeleted && 
                                    p.OrganizationId == organizationId &&
                                    _context.Leases.Any(l =>
                                        l.PropertyId == p.Id &&
                                        l.IsActive &&
                                        !l.IsDeleted));

                return (decimal)occupiedProperties / totalProperties * 100;
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "CalculateOccupancyRate");
                throw;
            }
        }

        /// <summary>
        /// Calculates the annual occupancy rate for a single property based on days occupied.
        /// Default period starts April 1 of current year (fiscal year).
        /// </summary>
        /// <param name="propertyId">Property ID to calculate occupancy for</param>
        /// <param name="periodStart">Start date of annual period (defaults to April 1 of current year)</param>
        /// <returns>Occupancy rate as percentage (0-100)</returns>
        public async Task<decimal> CalculatePropertyOccupancyRateAsync(Guid propertyId, DateTime? periodStart = null)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                // Default to April 1 of current year if not specified
                var startDate = periodStart ?? new DateTime(DateTime.Today.Year, 4, 1);
                var endDate = startDate.AddYears(1).AddDays(-1);

                // Get all leases for this property that overlap with the period
                // Include active leases, renewed (historical), and terminated (for actual move-out)
                var leases = await _context.Leases
                    .Where(l => l.PropertyId == propertyId &&
                               l.OrganizationId == organizationId &&
                               !l.IsDeleted &&
                               (l.IsActive ||
                                l.Status == ApplicationConstants.LeaseStatuses.Terminated ||
                                l.Status == ApplicationConstants.LeaseStatuses.Terminated) &&
                               l.StartDate <= endDate)
                    .ToListAsync();

                // Calculate days occupied within the period
                var daysOccupied = 0;
                foreach (var lease in leases)
                {
                    // For terminated leases, use ActualMoveOutDate; otherwise use EndDate
                    var effectiveEndDate = lease.Status == ApplicationConstants.LeaseStatuses.Terminated && lease.ActualMoveOutDate.HasValue
                        ? lease.ActualMoveOutDate.Value
                        : lease.EndDate;
                    
                    // Only count if lease overlaps with report period
                    if (effectiveEndDate >= startDate)
                    {
                        var leaseStart = lease.StartDate < startDate ? startDate : lease.StartDate;
                        var leaseEnd = effectiveEndDate > endDate ? endDate : effectiveEndDate;
                    
                        if (leaseEnd >= leaseStart)
                        {
                            daysOccupied += (leaseEnd - leaseStart).Days + 1; // +1 to include both start and end dates
                        }
                    }
                }

                // Calculate total days in period
                var totalDays = (endDate - startDate).Days + 1;

                // Return occupancy rate as percentage
                return totalDays > 0 ? (decimal)daysOccupied / totalDays * 100 : 0;
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "CalculatePropertyOccupancyRate");
                throw;
            }
        }

        /// <summary>
        /// Calculates the annual occupancy rate for the entire portfolio.
        /// Average of all properties' occupancy rates weighted by number of properties.
        /// Default period starts April 1 of current year (fiscal year).
        /// </summary>
        /// <param name="periodStart">Start date of annual period (defaults to April 1 of current year)</param>
        /// <returns>Portfolio occupancy rate as percentage (0-100)</returns>
        public async Task<decimal> CalculatePortfolioOccupancyRateAsync(DateTime? periodStart = null)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                // Get all properties for the organization
                var properties = await _context.Properties
                    .Where(p => !p.IsDeleted && p.OrganizationId == organizationId)
                    .Select(p => p.Id)
                    .ToListAsync();

                if (properties.Count == 0)
                {
                    return 0;
                }

                // Calculate total occupied days and total available days across all properties
                // Default to current fiscal year (April 1 - March 31)
                // If today is Jan-Mar, use April 1 of previous year
                // If today is Apr-Dec, use April 1 of current year
                DateTime startDate;
                if (periodStart.HasValue)
                {
                    startDate = periodStart.Value;
                }
                else
                {
                    var today = DateTime.Today;
                    startDate = today.Month < 4 
                        ? new DateTime(today.Year - 1, 4, 1) 
                        : new DateTime(today.Year, 4, 1);
                }
                var endDate = startDate.AddYears(1).AddDays(-1);
                var totalDays = (endDate - startDate).Days + 1;

                var totalDaysOccupied = 0;
                var totalDaysAvailable = properties.Count * totalDays;

                // For each property, calculate days occupied
                foreach (var propertyId in properties)
                {
                    var leases = await _context.Leases
                        .Where(l => l.PropertyId == propertyId &&
                                   l.OrganizationId == organizationId &&
                                   !l.IsDeleted &&
                                   (l.IsActive ||
                                    l.Status == ApplicationConstants.LeaseStatuses.Terminated ||
                                    l.Status == ApplicationConstants.LeaseStatuses.Terminated) &&
                                   l.StartDate <= endDate)
                        .ToListAsync();

                    foreach (var lease in leases)
                    {
                        // For terminated leases, use ActualMoveOutDate; otherwise use EndDate
                        var effectiveEndDate = lease.Status == ApplicationConstants.LeaseStatuses.Terminated && lease.ActualMoveOutDate.HasValue
                            ? lease.ActualMoveOutDate.Value
                            : lease.EndDate;
                        
                        // Only count if lease overlaps with report period
                        if (effectiveEndDate >= startDate)
                        {
                            var leaseStart = lease.StartDate < startDate ? startDate : lease.StartDate;
                            var leaseEnd = effectiveEndDate > endDate ? endDate : effectiveEndDate;
                        
                            if (leaseEnd >= leaseStart)
                            {
                                totalDaysOccupied += (leaseEnd - leaseStart).Days + 1;
                            }
                        }
                    }
                }

                // Return portfolio occupancy rate
                return totalDaysAvailable > 0 ? (decimal)totalDaysOccupied / totalDaysAvailable * 100 : 0;
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "CalculatePortfolioOccupancyRate");
                throw;
            }
        }

        /// <summary>
        /// Retrieves properties that need routine inspection.
        /// </summary>
        public async Task<List<Property>> GetPropertiesDueForInspectionAsync(int daysAhead = 7)
        {
            try
            {
                var userId = await _userContext.GetUserIdAsync();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User is not authenticated.");
                }

                var organizationId = await _userContext.GetActiveOrganizationIdAsync();
                var cutoffDate = DateTime.Today.AddDays(daysAhead);

                return await _context.Properties
                    .Where(p => !p.IsDeleted && 
                               p.OrganizationId == organizationId &&
                               p.NextRoutineInspectionDueDate.HasValue &&
                               p.NextRoutineInspectionDueDate.Value <= cutoffDate)
                    .OrderBy(p => p.NextRoutineInspectionDueDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetPropertiesDueForInspection");
                throw;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates a calendar event for routine property inspection.
        /// </summary>
        private async Task CreateRoutineInspectionCalendarEventAsync(Property property)
        {
            if (!property.NextRoutineInspectionDueDate.HasValue)
            {
                return;
            }

            var userId = await _userContext.GetUserIdAsync();
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            var calendarEvent = new CalendarEvent
            {
                Id = Guid.NewGuid(),
                Title = $"Routine Inspection - {property.Address}",
                Description = $"Scheduled routine inspection for property at {property.Address}",
                StartOn = property.NextRoutineInspectionDueDate.Value,
                EndOn = property.NextRoutineInspectionDueDate.Value.AddHours(1),
                DurationMinutes = 60,
                Location = property.Address,
                SourceEntityType = nameof(Property),
                SourceEntityId = property.Id,
                PropertyId = property.Id,
                OrganizationId = organizationId!.Value,
                CreatedBy = userId!,
                CreatedOn = DateTime.UtcNow,
                EventType = "Inspection",
                Status = "Scheduled"
            };

            await _notificationService.CreateAsync(new Notification
            {
                Id = Guid.NewGuid(),
                Type = NotificationConstants.Types.Info,
                Category = NotificationConstants.Categories.CalendarEvent,
                Title = "Routine Inspection Scheduled",
                Message = $"A routine inspection has been scheduled for the property at {property.Address} on {calendarEvent.StartOn:d}.",
                RecipientUserId = userId!,
                RelatedEntityId = calendarEvent.PropertyId,
                RelatedEntityType = nameof(Property),
                SentOn = DateTime.UtcNow,
                OrganizationId = organizationId!.Value,
                CreatedBy = userId!,
                CreatedOn = DateTime.UtcNow
            });

            await _calendarEventService.CreateCustomEventAsync(calendarEvent);
        }

        /// <summary>
        /// Gets properties with overdue routine inspections.
        /// </summary>
        public async Task<List<Property>> GetPropertiesWithOverdueInspectionsAsync()
        {
            try
            {
                var organizationId = await _userContext.GetOrganizationIdAsync();
                
                return await _context.Properties
                    .Where(p => p.OrganizationId == organizationId && 
                               !p.IsDeleted &&
                               p.NextRoutineInspectionDueDate.HasValue &&
                               p.NextRoutineInspectionDueDate.Value < DateTime.Today)
                    .OrderBy(p => p.NextRoutineInspectionDueDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetPropertiesWithOverdueInspections");
                throw;
            }
        }

        /// <summary>
        /// Gets properties with inspections due within specified days.
        /// </summary>
        public async Task<List<Property>> GetPropertiesWithInspectionsDueSoonAsync(int daysAhead = 30)
        {
            try
            {
                var organizationId = await _userContext.GetOrganizationIdAsync();
                var dueDate = DateTime.Today.AddDays(daysAhead);
                
                return await _context.Properties
                    .Where(p => p.OrganizationId == organizationId && 
                               !p.IsDeleted &&
                               p.NextRoutineInspectionDueDate.HasValue &&
                               p.NextRoutineInspectionDueDate.Value >= DateTime.Today &&
                               p.NextRoutineInspectionDueDate.Value <= dueDate)
                    .OrderBy(p => p.NextRoutineInspectionDueDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetPropertiesWithInspectionsDueSoon");
                throw;
            }
        }



        #endregion

        #region Archive / Unarchive Overrides

        /// <summary>
        /// Archives the property, sets it inactive with Off Market status, and cascade-archives
        /// all active related records (leases, inspections, maintenance requests, documents).
        /// </summary>
        public override async Task<bool> ArchiveAsync(Guid id)
        {
            var result = await base.ArchiveAsync(id);

            if (result)
            {
                var property = await _dbSet.FirstOrDefaultAsync(p => p.Id == id);
                if (property != null)
                {
                    property.IsActive = false;
                    property.Status = ApplicationConstants.PropertyStatuses.OffMarket;
                    await _context.SaveChangesAsync();
                }

                // Cascade archive all active related records
                var userId = await _userContext.GetUserIdAsync();
                var now = DateTime.UtcNow;

                await _context.Leases
                    .Where(x => x.PropertyId == id && !x.IsDeleted && !x.IsArchived)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.IsArchived, true)
                        .SetProperty(x => x.ArchivedOn, now)
                        .SetProperty(x => x.ArchivedBy, userId));

                await _context.Inspections
                    .Where(x => x.PropertyId == id && !x.IsDeleted && !x.IsArchived)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.IsArchived, true)
                        .SetProperty(x => x.ArchivedOn, now)
                        .SetProperty(x => x.ArchivedBy, userId));

                await _context.MaintenanceRequests
                    .Where(x => x.PropertyId == id && !x.IsDeleted && !x.IsArchived)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.IsArchived, true)
                        .SetProperty(x => x.ArchivedOn, now)
                        .SetProperty(x => x.ArchivedBy, userId));

                await _context.Documents
                    .Where(x => x.PropertyId == id && !x.IsDeleted && !x.IsArchived)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.IsArchived, true)
                        .SetProperty(x => x.ArchivedOn, now)
                        .SetProperty(x => x.ArchivedBy, userId));

                await _context.Repairs
                    .Where(x => x.PropertyId == id && !x.IsDeleted && !x.IsArchived)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.IsArchived, true)
                        .SetProperty(x => x.ArchivedOn, now)
                        .SetProperty(x => x.ArchivedBy, userId));

                await _context.PropertyExpenses
                    .Where(x => x.PropertyId == id && !x.IsDeleted && !x.IsArchived)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.IsArchived, true)
                        .SetProperty(x => x.ArchivedOn, now)
                        .SetProperty(x => x.ArchivedBy, userId));

                // ExpenseDue/ExpensePayment are PropertyExpense's own children.
                // Reached directly here (rather than relying on
                // PropertyExpenseService.ArchiveAsync's cascade) so the full
                // expense chain goes offline together when the property itself
                // is archived, per "Archive Property -> everything follows".
                await _context.Set<ExpenseDue>()
                    .Where(x => x.PropertyId == id && !x.IsDeleted && !x.IsArchived)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.IsArchived, true)
                        .SetProperty(x => x.ArchivedOn, now)
                        .SetProperty(x => x.ArchivedBy, userId));

                await _context.Set<ExpensePayment>()
                    .Where(x => !x.IsDeleted && !x.IsArchived && x.ExpenseDue!.PropertyId == id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.IsArchived, true)
                        .SetProperty(x => x.ArchivedOn, now)
                        .SetProperty(x => x.ArchivedBy, userId));
            }

            return result;
        }

        /// <summary>
        /// Restores the property to view (removes archive flag only).
        /// IsActive and Status are NOT changed — the property remains inactive and off market.
        /// The user must explicitly reactivate a property to avoid breaching the active property limit.
        /// </summary>
        public override async Task<bool> RestoreAsync(Guid id)
        {
            return await base.RestoreAsync(id);
        }

        /// <summary>
        /// Unarchives the property. When <paramref name="restoreRelated"/> is true, also unarchives
        /// all archived related records (leases, inspections, maintenance requests, documents).
        /// IsActive and Status are NOT changed — the user must explicitly reactivate the property.
        /// </summary>
        public async Task<bool> RestorePropertyAsync(Guid id, bool restoreRelated)
        {
            var result = await base.RestoreAsync(id);

            if (result && restoreRelated)
            {
                await _context.Leases
                    .Where(x => x.PropertyId == id && !x.IsDeleted && x.IsArchived)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.IsArchived, false)
                        .SetProperty(x => x.ArchivedOn, x => (DateTime?)null)
                        .SetProperty(x => x.ArchivedBy, x => (string?)null));

                await _context.Inspections
                    .Where(x => x.PropertyId == id && !x.IsDeleted && x.IsArchived)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.IsArchived, false)
                        .SetProperty(x => x.ArchivedOn, x => (DateTime?)null)
                        .SetProperty(x => x.ArchivedBy, x => (string?)null));

                await _context.MaintenanceRequests
                    .Where(x => x.PropertyId == id && !x.IsDeleted && x.IsArchived)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.IsArchived, false)
                        .SetProperty(x => x.ArchivedOn, x => (DateTime?)null)
                        .SetProperty(x => x.ArchivedBy, x => (string?)null));

                await _context.Documents
                    .Where(x => x.PropertyId == id && !x.IsDeleted && x.IsArchived)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.IsArchived, false)
                        .SetProperty(x => x.ArchivedOn, x => (DateTime?)null)
                        .SetProperty(x => x.ArchivedBy, x => (string?)null));

                await _context.Repairs
                    .Where(x => x.PropertyId == id && !x.IsDeleted && x.IsArchived)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.IsArchived, false)
                        .SetProperty(x => x.ArchivedOn, x => (DateTime?)null)
                        .SetProperty(x => x.ArchivedBy, x => (string?)null));

                await _context.PropertyExpenses
                    .Where(x => x.PropertyId == id && !x.IsDeleted && x.IsArchived)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.IsArchived, false)
                        .SetProperty(x => x.ArchivedOn, x => (DateTime?)null)
                        .SetProperty(x => x.ArchivedBy, x => (string?)null));

                await _context.Set<ExpenseDue>()
                    .Where(x => x.PropertyId == id && !x.IsDeleted && x.IsArchived)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.IsArchived, false)
                        .SetProperty(x => x.ArchivedOn, x => (DateTime?)null)
                        .SetProperty(x => x.ArchivedBy, x => (string?)null));

                await _context.Set<ExpensePayment>()
                    .Where(x => !x.IsDeleted && x.IsArchived && x.ExpenseDue!.PropertyId == id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.IsArchived, false)
                        .SetProperty(x => x.ArchivedOn, x => (DateTime?)null)
                        .SetProperty(x => x.ArchivedBy, x => (string?)null));
            }

            return result;
        }

        /// <summary>
        /// Returns a count of currently-archived related records for this property.
        /// Used to populate the unarchive confirmation prompt.
        /// </summary>
        public async Task<CascadeSummary> GetRestoreCascadeSummaryAsync(Guid id)
        {
            var counts = new Dictionary<string, int>
            {
                ["Leases"] = await _context.Leases.CountAsync(x => x.PropertyId == id && !x.IsDeleted && x.IsArchived),
                ["Inspections"] = await _context.Inspections.CountAsync(x => x.PropertyId == id && !x.IsDeleted && x.IsArchived),
                ["Maintenance Requests"] = await _context.MaintenanceRequests.CountAsync(x => x.PropertyId == id && !x.IsDeleted && x.IsArchived),
                ["Documents"] = await _context.Documents.CountAsync(x => x.PropertyId == id && !x.IsDeleted && x.IsArchived),
                ["Repairs"] = await _context.Repairs.CountAsync(x => x.PropertyId == id && !x.IsDeleted && x.IsArchived),
                ["Property Expenses"] = await _context.PropertyExpenses.CountAsync(x => x.PropertyId == id && !x.IsDeleted && x.IsArchived),
            };
            return new CascadeSummary { EntityName = "Property", Counts = counts };
        }

        #endregion

        #region Delete Override

        /// <summary>
        /// Deletes a property and all related records (cascade delete).
        /// Removes leases (with invoices, payments, security deposits), maintenance requests
        /// (with repairs), inspections, documents, calendar events, tours, and rental applications.
        /// </summary>
        public override async Task<bool> DeleteAsync(Guid id)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
            // Delete invoice payments and invoices for all leases on this property
            var leaseIds = await _context.Leases
                .Where(l => l.PropertyId == id)
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
                    var invoicePayments = await _context.Payments
                        .Where(p => p.InvoiceId == invoiceId)
                        .ToListAsync();
                    _context.Payments.RemoveRange(invoicePayments);
                }

                var invoices = await _context.Invoices.Where(i => i.LeaseId == leaseId).ToListAsync();
                _context.Invoices.RemoveRange(invoices);

                var securityDeposits = await _context.SecurityDeposits.Where(s => s.LeaseId == leaseId).ToListAsync();
                _context.SecurityDeposits.RemoveRange(securityDeposits);
            }

            var leases = await _context.Leases.Where(l => l.PropertyId == id).ToListAsync();

            _context.Leases.RemoveRange(leases);

            // Null out Repair.MaintenanceRequestId before deleting maintenance requests
            var maintenanceIds = await _context.MaintenanceRequests
                .Where(m => m.PropertyId == id)
                .Select(m => m.Id)
                .ToListAsync();

            var repairs = await _context.Repairs
                .Where(r => r.PropertyId == id)
                .ToListAsync();
            _context.Repairs.RemoveRange(repairs);

            var expenses = await _context.PropertyExpenses
                .Where(e => e.PropertyId == id)
                .ToListAsync();
            _context.PropertyExpenses.RemoveRange(expenses);

            var maintenanceRequests = await _context.MaintenanceRequests.Where(m => m.PropertyId == id).ToListAsync();
            _context.MaintenanceRequests.RemoveRange(maintenanceRequests);

            // Delete calendar events linked to inspections, then delete inspections
            var inspectionIds = await _context.Inspections
                .Where(i => i.PropertyId == id)
                .Select(i => new { i.Id, i.CalendarEventId })
                .ToListAsync();

            foreach (var inspection in inspectionIds.Where(i => i.CalendarEventId.HasValue))
            {
                var inspectionEvent = await _context.CalendarEvents.FindAsync(inspection.CalendarEventId!.Value);
                if (inspectionEvent != null) _context.CalendarEvents.Remove(inspectionEvent);
            }

            var inspections = await _context.Inspections.Where(i => i.PropertyId == id).ToListAsync();
            _context.Inspections.RemoveRange(inspections);

            // Delete documents, calendar events, tours, rental applications
            var documents = await _context.Documents.Where(d => d.PropertyId == id).ToListAsync();
            _context.Documents.RemoveRange(documents);

            var calendarEvents = await _context.CalendarEvents.Where(c => c.PropertyId == id).ToListAsync();
            _context.CalendarEvents.RemoveRange(calendarEvents);

            var tours = await _context.Tours.Where(t => t.PropertyId == id).ToListAsync();
            _context.Tours.RemoveRange(tours);

            var applicationIds = await _context.RentalApplications
                .Where(a => a.PropertyId == id)
                .Select(a => a.Id)
                .ToListAsync();

            var screenings = await _context.ApplicationScreenings
                .Where(s => applicationIds.Contains(s.RentalApplicationId))
                .ToListAsync();
            _context.ApplicationScreenings.RemoveRange(screenings);

            var applications = await _context.RentalApplications.Where(a => a.PropertyId == id).ToListAsync();
            _context.RentalApplications.RemoveRange(applications);

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
        /// Returns a count of related records that would be permanently deleted with this property.
        /// </summary>
        public override async Task<CascadeSummary> GetDeleteCascadeSummaryAsync(Guid id)
        {
            var counts = new Dictionary<string, int>
            {
                ["Leases"] = await _context.Leases.CountAsync(x => x.PropertyId == id && !x.IsDeleted),
                ["Inspections"] = await _context.Inspections.CountAsync(x => x.PropertyId == id && !x.IsDeleted),
                ["Maintenance Requests"] = await _context.MaintenanceRequests.CountAsync(x => x.PropertyId == id && !x.IsDeleted),
                ["Documents"] = await _context.Documents.CountAsync(x => x.PropertyId == id && !x.IsDeleted),
                ["Calendar Events"] = await _context.CalendarEvents.CountAsync(x => x.PropertyId == id && !x.IsDeleted),
                ["Tours"] = await _context.Tours.CountAsync(x => x.PropertyId == id && !x.IsDeleted),
                ["Rental Applications"] = await _context.RentalApplications.CountAsync(x => x.PropertyId == id && !x.IsDeleted),
                ["Property Expenses"] = await _context.PropertyExpenses.CountAsync(x => x.PropertyId == id && !x.IsDeleted),
            };
            return new CascadeSummary { EntityName = "Property", Counts = counts };
        }

        /// <summary>
        /// Returns a count of related records that would be archived with this property.
        /// </summary>
        public override async Task<CascadeSummary> GetArchiveCascadeSummaryAsync(Guid id)
        {
            var counts = new Dictionary<string, int>
            {
                ["Leases"] = await _context.Leases.CountAsync(x => x.PropertyId == id && !x.IsDeleted && !x.IsArchived),
                ["Inspections"] = await _context.Inspections.CountAsync(x => x.PropertyId == id && !x.IsDeleted && !x.IsArchived),
                ["Maintenance Requests"] = await _context.MaintenanceRequests.CountAsync(x => x.PropertyId == id && !x.IsDeleted && !x.IsArchived),
                ["Documents"] = await _context.Documents.CountAsync(x => x.PropertyId == id && !x.IsDeleted && !x.IsArchived),
                ["Repairs"] = await _context.Repairs.CountAsync(x => x.PropertyId == id && !x.IsDeleted && !x.IsArchived),
                ["Property Expenses"] = await _context.PropertyExpenses.CountAsync(x => x.PropertyId == id && !x.IsDeleted && !x.IsArchived),
            };
            return new CascadeSummary { EntityName = "Property", Counts = counts };
        }

        #endregion
    }
}
