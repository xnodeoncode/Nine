using System.Linq.Expressions;
using Nine.Core.Constants;
using Nine.Core.Entities;
using Nine.Core.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nine.Application.Services
{
    /// <summary>
    /// Abstract base service providing common CRUD operations for entities.
    /// Implements organization-based multi-tenancy, soft delete support, 
    /// and automatic audit field management.
    /// </summary>
    /// <typeparam name="TEntity">Entity type that inherits from BaseModel</typeparam>
    public abstract class BaseService<TEntity> where TEntity : BaseModel
    {
        protected readonly ApplicationDbContext _context;
        protected readonly ILogger<BaseService<TEntity>> _logger;
        protected readonly IUserContextService _userContext;
        protected readonly ApplicationSettings _settings;
        protected readonly DbSet<TEntity> _dbSet;

        protected BaseService(
            ApplicationDbContext context,
            ILogger<BaseService<TEntity>> logger,
            IUserContextService userContext,
            IOptions<ApplicationSettings> settings)
        {
            _context = context;
            _logger = logger;
            _userContext = userContext;
            _settings = settings.Value;
            _dbSet = context.Set<TEntity>();
        }

        #region CRUD Operations

        /// <summary>
        /// Retrieves an entity by its ID with organization isolation.
        /// Returns null if entity not found or belongs to different organization.
        /// Automatically filters out soft-deleted entities.
        /// </summary>
        public virtual async Task<TEntity?> GetByIdAsync(Guid id)
        {
            try
            {
                var userId = await _userContext.GetUserIdAsync();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User is not authenticated.");
                }

                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                var entity = await _dbSet
                    .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);

                if (entity == null)
                {
                    _logger.LogWarning($"{typeof(TEntity).Name} not found: {id}");
                    return null;
                }

                // Verify organization access if entity has OrganizationId property
                if (HasOrganizationIdProperty(entity))
                {
                    var entityOrgId = GetOrganizationId(entity);
                    if (entityOrgId != organizationId)
                    {
                        _logger.LogWarning($"Unauthorized access to {typeof(TEntity).Name} {id} from organization {organizationId}");
                        return null;
                    }
                }

                return entity;
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, $"GetById{typeof(TEntity).Name}");
                throw;
            }
        }

        /// <summary>
        /// Retrieves all entities for the current organization.
        /// Automatically filters out soft-deleted entities and applies organization isolation.
        /// </summary>
        public virtual async Task<List<TEntity>> GetAllAsync()
        {
            try
            {
                var userId = await _userContext.GetUserIdAsync();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User is not authenticated.");
                }

                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                IQueryable<TEntity> query = _dbSet.Where(e => !e.IsDeleted);

                // Apply organization filter if entity has OrganizationId property
                if (typeof(TEntity).GetProperty("OrganizationId") != null)
                {
                    var parameter = Expression.Parameter(typeof(TEntity), "e");
                    var property = Expression.Property(parameter, "OrganizationId");
                    var constant = Expression.Constant(organizationId);
                    var condition = Expression.Equal(property, constant);
                    var lambda = Expression.Lambda<Func<TEntity, bool>>(condition, parameter);

                    query = query.Where(lambda);
                }

                return await query.ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, $"GetAll{typeof(TEntity).Name}");
                throw;
            }
        }

        /// <summary>
        /// Creates a new entity with automatic audit field and organization assignment.
        /// Validates entity before creation and sets CreatedBy, CreatedOn, and OrganizationId.
        /// </summary>
        public virtual async Task<TEntity> CreateAsync(TEntity entity)
        {
            try
            {
                var userId = await _userContext.GetUserIdAsync();
                if (string.IsNullOrEmpty(userId))
                {
                    if (entity.CreatedBy == ApplicationConstants.SystemUser.Id)
                        userId = entity.CreatedBy; // Allow system-created records to specify "System" as creator
                    else
                        throw new UnauthorizedAccessException("User is not authenticated.");
                }

                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                // Set organization ID BEFORE validation so validation rules can check it
                if (HasOrganizationIdProperty(entity) && organizationId.HasValue)
                {
                    SetOrganizationId(entity, organizationId.Value);
                }

                // Call hook to set default values
                entity = await SetCreateDefaultsAsync(entity);

                // Validate entity
                await ValidateEntityAsync(entity);

                // Ensure ID is set
                if (entity.Id == Guid.Empty)
                {
                    entity.Id = Guid.NewGuid();
                }

                // Set audit fields
                SetAuditFieldsForCreate(entity, userId);

                _dbSet.Add(entity);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"{typeof(TEntity).Name} created: {entity.Id} by user {userId}");

                // Call hook for post-create operations
                await AfterCreateAsync(entity);

                return entity;
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, $"Create{typeof(TEntity).Name}");
                throw;
            }
        }

        /// <summary>
        /// Updates an existing entity with automatic audit field management.
        /// Validates entity and organization ownership before update.
        /// Sets LastModifiedBy and LastModifiedOn automatically.
        /// </summary>
        public virtual async Task<TEntity> UpdateAsync(TEntity entity)
        {
            try
            {
                var userId = await _userContext.GetUserIdAsync();
                if (string.IsNullOrEmpty(userId))
                {
                    if (entity.LastModifiedBy == ApplicationConstants.SystemUser.Id)
                        userId = ApplicationConstants.SystemUser.Id;
                    else
                        throw new UnauthorizedAccessException("User is not authenticated.");
                }

                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                // Validate entity
                await ValidateEntityAsync(entity);

                // Verify entity exists and belongs to organization
                var existing = await _dbSet
                    .FirstOrDefaultAsync(e => e.Id == entity.Id && !e.IsDeleted);

                if (existing == null)
                {
                    throw new InvalidOperationException($"{typeof(TEntity).Name} not found: {entity.Id}");
                }

                // Verify organization access
                if (HasOrganizationIdProperty(existing) && organizationId.HasValue)
                {
                    var existingOrgId = GetOrganizationId(existing);
                    if (existingOrgId != organizationId)
                    {
                        throw new UnauthorizedAccessException(
                            $"Cannot update {typeof(TEntity).Name} {entity.Id} - belongs to different organization.");
                    }

                    // Prevent organization hijacking
                    SetOrganizationId(entity, organizationId.Value);
                }

                // Set audit fields
                SetAuditFieldsForUpdate(entity, userId);

                // Update entity
                _context.Entry(existing).CurrentValues.SetValues(entity);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"{typeof(TEntity).Name} updated: {entity.Id} by user {userId}");

                return entity;
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, $"Update{typeof(TEntity).Name}");
                throw;
            }
        }

        /// <summary>
        /// Deletes an entity (soft delete if enabled, hard delete otherwise).
        /// Verifies organization ownership before deletion.
        /// </summary>
        public virtual async Task<bool> DeleteAsync(Guid id)
        {
            try
            {
                var userId = await _userContext.GetUserIdAsync();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User is not authenticated.");
                }

                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                var entity = await _dbSet
                    .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);

                if (entity == null)
                {
                    _logger.LogWarning($"{typeof(TEntity).Name} not found for deletion: {id}");
                    return false;
                }

                // Verify organization access
                if (HasOrganizationIdProperty(entity) && organizationId.HasValue)
                {
                    var entityOrgId = GetOrganizationId(entity);
                    if (entityOrgId != organizationId)
                    {
                        throw new UnauthorizedAccessException(
                            $"Cannot delete {typeof(TEntity).Name} {id} - belongs to different organization.");
                    }
                }

                // Soft delete or hard delete based on settings
                if (_settings.SoftDeleteEnabled)
                {
                    entity.IsDeleted = true;
                    SetAuditFieldsForUpdate(entity, userId);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"{typeof(TEntity).Name} soft deleted: {id} by user {userId}");
                }
                else
                {
                    _dbSet.Remove(entity);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"{typeof(TEntity).Name} hard deleted: {id} by user {userId}");
                }

                return true;
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, $"Delete{typeof(TEntity).Name}");
                throw;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Virtual method for entity-specific validation.
        /// Override in derived classes to implement custom validation logic.
        /// </summary>
        protected virtual async Task ValidateEntityAsync(TEntity entity)
        {
            // Default: no validation
            // Override in derived classes for specific validation
            await Task.CompletedTask;
        }

        /// <summary>
        /// Virtual method for centralized exception handling.
        /// Override in derived classes for custom error handling logic.
        /// </summary>
        protected virtual async Task HandleExceptionAsync(Exception ex, string operation)
        {
            _logger.LogError(ex, $"Error in {operation} for {typeof(TEntity).Name}");
            await Task.CompletedTask;
        }

        /// <summary>
        /// Sets audit fields when creating a new entity.
        /// </summary>
        protected virtual void SetAuditFieldsForCreate(TEntity entity, string userId)
        {
            entity.CreatedBy = userId;
            entity.CreatedOn = DateTime.UtcNow;
        }

        /// <summary>
        /// Sets audit fields when updating an existing entity.
        /// </summary>
        protected virtual void SetAuditFieldsForUpdate(TEntity entity, string userId)
        {
            entity.LastModifiedBy = userId;
            entity.LastModifiedOn = DateTime.UtcNow;
        }

        /// <summary>
        /// Checks if entity has OrganizationId property via reflection.
        /// </summary>
        private bool HasOrganizationIdProperty(TEntity entity)
        {
            return typeof(TEntity).GetProperty("OrganizationId") != null;
        }

        /// <summary>
        /// Gets the OrganizationId value from entity via reflection.
        /// </summary>
        private Guid? GetOrganizationId(TEntity entity)
        {
            var property = typeof(TEntity).GetProperty("OrganizationId");
            if (property == null) return null;

            var value = property.GetValue(entity);
            return value is Guid guidValue ? guidValue : null;
        }

        /// <summary>
        /// Sets the OrganizationId value on entity via reflection.
        /// </summary>
        private void SetOrganizationId(TEntity entity, Guid organizationId)
        {
            var property = typeof(TEntity).GetProperty("OrganizationId");
            property?.SetValue(entity, organizationId);
        }

        /// <summary>
        /// Hook method called before creating entity to set default values.
        /// Override in derived services to customize default behavior.
        /// </summary>
        protected virtual async Task<TEntity> SetCreateDefaultsAsync(TEntity entity)
        {
            // Automatically propagate IsSampleData flag from parent entities
            await InheritSampleDataFlagFromParentsAsync(entity);
            
            await Task.CompletedTask;
            return entity;
        }

        /// <summary>
        /// Checks parent entities for IsSampleData flag and propagates to child entity.
        /// If any parent entity has IsSampleData = true, the child entity is marked as sample data.
        /// This ensures sample data "taints" all related records for proper cleanup.
        /// </summary>
        /// <param name="entity">The entity being created</param>
        /// <returns>Task</returns>
        protected virtual async Task InheritSampleDataFlagFromParentsAsync(TEntity entity)
        {
            try
            {
                // If already marked as sample data, no need to check parents
                if (entity.IsSampleData)
                {
                    return;
                }

                // Check for common parent relationship properties
                var entityType = typeof(TEntity);
                var properties = entityType.GetProperties();

                // Common parent ID properties to check
                var parentIdProperties = new[] 
                { 
                    "PropertyId", 
                    "LeaseId", 
                    "InvoiceId", 
                    "TenantId", 
                    "ProspectiveTenantId", 
                    "RentalApplicationId",
                    "RepairId",
                    "InspectionId",
                    "MaintenanceRequestId",
                    "OrganizationId" // Check organization itself for multi-tenant sample orgs
                };

                foreach (var parentPropName in parentIdProperties)
                {
                    var parentIdProperty = properties.FirstOrDefault(p => 
                        p.Name == parentPropName && 
                        (p.PropertyType == typeof(Guid) || p.PropertyType == typeof(Guid?)));

                    if (parentIdProperty == null) continue;

                    var parentId = parentIdProperty.GetValue(entity);
                    if (parentId == null || (parentId is Guid guidValue && guidValue == Guid.Empty)) continue;

                    // Determine parent entity type and check IsSampleData flag
                    bool parentIsSampleData = await CheckParentEntityIsSampleDataAsync(parentPropName, (Guid)parentId);
                    
                    if (parentIsSampleData)
                    {
                        entity.IsSampleData = true;
                        _logger.LogInformation(
                            $"{typeof(TEntity).Name} marked as sample data - inherited from {parentPropName} ({parentId})");
                        return; // Once marked as sample data, no need to check other parents
                    }
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail entity creation if sample data check fails
                _logger.LogWarning(ex, $"Error checking parent sample data flag for {typeof(TEntity).Name}");
            }
        }

        /// <summary>
        /// Checks if a parent entity has IsSampleData = true.
        /// Simple direct query approach for better reliability and maintainability.
        /// </summary>
        /// <param name="parentPropertyName">Parent property name (e.g., "LeaseId", "PropertyId")</param>
        /// <param name="parentId">Parent entity ID</param>
        /// <returns>True if parent is sample data, false otherwise</returns>
        private async Task<bool> CheckParentEntityIsSampleDataAsync(string parentPropertyName, Guid parentId)
        {
            try
            {
                // Direct queries for each entity type - simple and reliable
                switch (parentPropertyName)
                {
                    case "PropertyId":
                        var property = await _context.Properties
                            .Where(p => p.Id == parentId)
                            .Select(p => p.IsSampleData)
                            .FirstOrDefaultAsync();
                        return property;

                    case "LeaseId":
                        var lease = await _context.Leases
                            .Where(l => l.Id == parentId)
                            .Select(l => l.IsSampleData)
                            .FirstOrDefaultAsync();
                        return lease;

                    case "InvoiceId":
                        var invoice = await _context.Invoices
                            .Where(i => i.Id == parentId)
                            .Select(i => i.IsSampleData)
                            .FirstOrDefaultAsync();
                        return invoice;

                    case "TenantId":
                        var tenant = await _context.Tenants
                            .Where(t => t.Id == parentId)
                            .Select(t => t.IsSampleData)
                            .FirstOrDefaultAsync();
                        return tenant;

                    case "ProspectiveTenantId":
                        var prospect = await _context.ProspectiveTenants
                            .Where(pt => pt.Id == parentId)
                            .Select(pt => pt.IsSampleData)
                            .FirstOrDefaultAsync();
                        return prospect;

                    case "RentalApplicationId":
                        var application = await _context.RentalApplications
                            .Where(ra => ra.Id == parentId)
                            .Select(ra => ra.IsSampleData)
                            .FirstOrDefaultAsync();
                        return application;

                    case "RepairId":
                        var repair = await _context.Repairs
                            .Where(r => r.Id == parentId)
                            .Select(r => r.IsSampleData)
                            .FirstOrDefaultAsync();
                        return repair;

                    case "InspectionId":
                        var inspection = await _context.Inspections
                            .Where(i => i.Id == parentId)
                            .Select(i => i.IsSampleData)
                            .FirstOrDefaultAsync();
                        return inspection;

                    case "MaintenanceRequestId":
                        var maintenanceRequest = await _context.MaintenanceRequests
                            .Where(mr => mr.Id == parentId)
                            .Select(mr => mr.IsSampleData)
                            .FirstOrDefaultAsync();
                        return maintenanceRequest;

                    // OrganizationId is NOT checked - Organizations don't have IsSampleData flag
                    // Sample data is marked at the entity level within an organization

                    default:
                        _logger.LogDebug($"Unknown parent property: {parentPropertyName}");
                        return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, $"Could not check IsSampleData for {parentPropertyName}");
                return false;
            }
        }

        /// <summary>
        /// Hook method called after creating entity for post-creation operations.
        /// Override in derived services to handle side effects like updating related entities.
        /// </summary>
        protected virtual async Task AfterCreateAsync(TEntity entity)
        {
            await Task.CompletedTask;
        }

        #endregion
    }
}
