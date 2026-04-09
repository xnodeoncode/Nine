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
                        l.Status == Core.Constants.ApplicationConstants.LeaseStatuses.Active &&
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
                                        l.Status == Core.Constants.ApplicationConstants.LeaseStatuses.Active &&
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
                // Include all occupied statuses: Active, Renewed, Month-to-Month, Notice Given, Terminated
                var leases = await _context.Leases
                    .Where(l => l.PropertyId == propertyId &&
                               l.OrganizationId == organizationId &&
                               !l.IsDeleted &&
                               (l.Status == ApplicationConstants.LeaseStatuses.Active || 
                                l.Status == ApplicationConstants.LeaseStatuses.Renewed ||
                                l.Status == ApplicationConstants.LeaseStatuses.MonthToMonth ||
                                l.Status == ApplicationConstants.LeaseStatuses.NoticeGiven ||
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
                                   (l.Status == ApplicationConstants.LeaseStatuses.Active || 
                                    l.Status == ApplicationConstants.LeaseStatuses.Renewed ||
                                    l.Status == ApplicationConstants.LeaseStatuses.MonthToMonth ||
                                    l.Status == ApplicationConstants.LeaseStatuses.NoticeGiven ||
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
    }
}
