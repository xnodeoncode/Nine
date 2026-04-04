using Nine.Core.Entities;
using Nine.Core.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Nine.Application.Services.Workflows
{
    /// <summary>
    /// Abstract base class for all workflow services.
    /// Provides transaction support, audit logging, and validation infrastructure.
    /// </summary>
    public abstract class BaseWorkflowService
    {
        protected readonly ApplicationDbContext _context;
        protected readonly IUserContextService _userContext;

        protected BaseWorkflowService(
            ApplicationDbContext context,
            IUserContextService userContext)
        {
            _context = context;
            _userContext = userContext;
        }

        /// <summary>
        /// Executes a workflow operation within a database transaction.
        /// Automatically commits on success or rolls back on failure.
        /// </summary>
        protected async Task<WorkflowResult<T>> ExecuteWorkflowAsync<T>(
            Func<Task<WorkflowResult<T>>> workflowOperation)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var result = await workflowOperation();

                if (result.Success)
                {
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                else
                {
                    await transaction.RollbackAsync();
                    // Clear the ChangeTracker to discard all tracked changes
                    _context.ChangeTracker.Clear();
                }

                return result;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                // Clear the ChangeTracker to discard all tracked changes
                _context.ChangeTracker.Clear();
                
                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage += $" | Inner: {ex.InnerException.Message}";
                    if (ex.InnerException.InnerException != null)
                    {
                        errorMessage += $" | Inner(2): {ex.InnerException.InnerException.Message}";
                    }
                }
                Console.WriteLine($"Workflow Error: {errorMessage}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return WorkflowResult<T>.Fail($"Workflow operation failed: {errorMessage}");
            }
        }

        /// <summary>
        /// Executes a workflow operation within a database transaction (non-generic version).
        /// </summary>
        protected async Task<WorkflowResult> ExecuteWorkflowAsync(
            Func<Task<WorkflowResult>> workflowOperation)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var result = await workflowOperation();

                if (result.Success)
                {
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                else
                {
                    await transaction.RollbackAsync();
                    // Clear the ChangeTracker to discard all tracked changes
                    _context.ChangeTracker.Clear();
                }

                return result;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                // Clear the ChangeTracker to discard all tracked changes
                _context.ChangeTracker.Clear();
                
                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage += $" | Inner: {ex.InnerException.Message}";
                    if (ex.InnerException.InnerException != null)
                    {
                        errorMessage += $" | Inner(2): {ex.InnerException.InnerException.Message}";
                    }
                }
                Console.WriteLine($"Workflow Error: {errorMessage}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return WorkflowResult.Fail($"Workflow operation failed: {errorMessage}");
            }
        }

        /// <summary>
        /// Logs a workflow state transition to the audit log.
        /// Pass <paramref name="organizationId"/> when calling from background services where the
        /// user context has no Blazor circuit (e.g., ScheduledTaskService).
        /// </summary>
        protected async Task LogTransitionAsync(
            string entityType,
            Guid entityId,
            string? fromStatus,
            string toStatus,
            string action,
            string? reason = null,
            Dictionary<string, object>? metadata = null,
            Guid? organizationId = null)
        {
            var userId = await _userContext.GetUserIdAsync() ?? string.Empty;
            var activeOrgId = organizationId ?? await _userContext.GetActiveOrganizationIdAsync();
            
            var auditLog = new WorkflowAuditLog
            {
                Id = Guid.NewGuid(),
                EntityType = entityType,
                EntityId = entityId,
                FromStatus = fromStatus,
                ToStatus = toStatus,
                Action = action,
                Reason = reason,
                PerformedBy = string.IsNullOrEmpty(userId) ? "System" : userId,
                PerformedOn = DateTime.UtcNow,
                OrganizationId = activeOrgId.HasValue ? activeOrgId.Value : Guid.Empty,
                Metadata = metadata != null ? JsonSerializer.Serialize(metadata) : null,
                CreatedOn = DateTime.UtcNow,
                CreatedBy = string.IsNullOrEmpty(userId) ? "System" : userId
            };

            _context.WorkflowAuditLogs.Add(auditLog);
            // Note: SaveChangesAsync is called by ExecuteWorkflowAsync
        }

        /// <summary>
        /// Gets the complete audit history for an entity.
        /// </summary>
        public async Task<List<WorkflowAuditLog>> GetAuditHistoryAsync(
            string entityType,
            Guid entityId)
        {
            var activeOrgId = await _userContext.GetActiveOrganizationIdAsync();
            
            return await _context.WorkflowAuditLogs
                .Where(w => w.EntityType == entityType && w.EntityId == entityId)
                .Where(w => w.OrganizationId == activeOrgId)
                .OrderBy(w => w.PerformedOn)
                .ToListAsync();
        }

        /// <summary>
        /// Validates that an entity belongs to the active organization.
        /// </summary>
        protected async Task<bool> ValidateOrganizationOwnershipAsync<TEntity>(
            IQueryable<TEntity> query,
            Guid entityId) where TEntity : class
        {
            var activeOrgId = await _userContext.GetActiveOrganizationIdAsync();
            
            // This assumes entities have OrganizationId property
            // Override in derived classes if different validation needed
            var entity = await query
                .Where(e => EF.Property<Guid>(e, "Id") == entityId)
                .Where(e => EF.Property<Guid>(e, "OrganizationId") == activeOrgId)
                .Where(e => EF.Property<bool>(e, "IsDeleted") == false)
                .FirstOrDefaultAsync();

            return entity != null;
        }

        /// <summary>
        /// Gets the current user ID from the user context.
        /// </summary>
        protected async Task<string> GetCurrentUserIdAsync()
        {
            return await _userContext.GetUserIdAsync() ?? string.Empty;
        }

        /// <summary>
        /// Gets the active organization ID from the user context.
        /// </summary>
        protected async Task<Guid> GetActiveOrganizationIdAsync()
        {
            return await _userContext.GetActiveOrganizationIdAsync() ?? Guid.Empty;
        }

    }
}
