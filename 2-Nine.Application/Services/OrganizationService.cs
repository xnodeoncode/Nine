using Nine.Core.Entities;
using Nine.Core.Constants;
using Nine.Core.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Nine.Application.Services
{
    public class OrganizationService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IUserContextService _userContext;
        private readonly ApplicationSettings _settings;

        public OrganizationService(
            ApplicationDbContext dbContext, 
            IUserContextService userContextService,
            IOptions<ApplicationSettings> settings)
        {
            _dbContext = dbContext;
            _userContext = userContextService;
            _settings = settings.Value;
        }

        #region CRUD Operations

        /// <summary>
        /// Create a new organization
        /// </summary>
        public async Task<Organization> CreateOrganizationAsync(string ownerId, string name, string? displayName = null, string? state = null)
        {
            var organization = new Organization
            {
                Id = Guid.NewGuid(),
                OwnerId = ownerId,
                Name = name,
                DisplayName = displayName ?? name,
                State = state,
                IsActive = true,
                CreatedOn = DateTime.UtcNow,
                CreatedBy = ownerId
            };

            _dbContext.Organizations.Add(organization);

            // Create Owner entry in OrganizationUsers
            var OrganizationUser = new OrganizationUser
            {
                Id = Guid.NewGuid(),
                UserId = ownerId,
                OrganizationId = organization.Id,
                Role = ApplicationConstants.OrganizationRoles.Owner,
                GrantedBy = ownerId,
                GrantedOn = DateTime.UtcNow,
                IsActive = true,
                CreatedOn = DateTime.UtcNow,
                CreatedBy = ownerId
            };

            _dbContext.OrganizationUsers.Add(OrganizationUser);

            // add organization settings record with defaults
            var settings = new OrganizationSettings
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organization.Id,
                    Name = organization.Name,
                    LateFeeEnabled = true,
                    LateFeeAutoApply = true,
                    LateFeeGracePeriodDays = 3,
                    LateFeePercentage = 0.05m,
                    MaxLateFeeAmount = 50.00m,
                    PaymentReminderEnabled = true,
                    PaymentReminderDaysBefore = 3,
                    CreatedOn = DateTime.UtcNow,
                    CreatedBy = ownerId
                };
                
                await _dbContext.OrganizationSettings.AddAsync(settings);
                await _dbContext.SaveChangesAsync();

            return organization;
        }

        /// <summary>
        /// Create a new organization
        /// </summary>
        public async Task<Organization> CreateOrganizationAsync(Organization organization)
        {

            var userId = await _userContext.GetUserIdAsync();

            if(string.IsNullOrEmpty(userId))
                throw new InvalidOperationException("Cannot create organization: User ID is not available in context.");


            organization.Id = Guid.NewGuid();
            organization.OwnerId = userId;
            organization.IsActive = true;
            organization.CreatedOn = DateTime.UtcNow;
            organization.CreatedBy = userId;

            _dbContext.Organizations.Add(organization);

            // Create Owner entry in OrganizationUsers
            var OrganizationUser = new OrganizationUser
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OrganizationId = organization.Id,
                Role = ApplicationConstants.OrganizationRoles.Owner,
                GrantedBy = userId,
                GrantedOn = DateTime.UtcNow,
                IsActive = true,
                CreatedOn = DateTime.UtcNow,
                CreatedBy = userId
            };

            _dbContext.OrganizationUsers.Add(OrganizationUser);
            await _dbContext.SaveChangesAsync();

            // add organization settings record with defaults
            var settings = new OrganizationSettings
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organization.Id,
                    Name = organization.Name,
                    LateFeeEnabled = true,
                    LateFeeAutoApply = true,
                    LateFeeGracePeriodDays = 3,
                    LateFeePercentage = 0.05m,
                    MaxLateFeeAmount = 50.00m,
                    PaymentReminderEnabled = true,
                    PaymentReminderDaysBefore = 3,
                    CreatedOn = DateTime.UtcNow,
                    CreatedBy = userId
                };
                
                await _dbContext.OrganizationSettings.AddAsync(settings);
                await _dbContext.SaveChangesAsync();

            return organization;
        }


        /// <summary>
        /// Get organization by ID
        /// </summary>
        public async Task<Organization?> GetOrganizationByIdAsync(Guid organizationId)
        {
            return await _dbContext.Organizations
                .Include(o => o.OrganizationUsers)
                .FirstOrDefaultAsync(o => o.Id == organizationId && !o.IsDeleted);
        }

        /// <summary>
        /// Get all organizations owned by a user
        /// </summary>
        public async Task<List<Organization>> GetOwnedOrganizationsAsync(string userId)
        {
            return await _dbContext.Organizations
                .Where(o => o.OwnerId == userId && !o.IsDeleted)
                .OrderBy(o => o.Name)
                .ToListAsync();
        }

        /// <summary>
        /// Get all organizations a user has access to (via OrganizationUsers)
        /// </summary>
        public async Task<List<OrganizationUser>> GetOrganizationUsersAsync(string userId)
        {
            return await _dbContext.OrganizationUsers
                .Include(uo => uo.Organization)
                .Where(uo => uo.UserId == userId && uo.IsActive && !uo.IsDeleted)
                .Where(uo => !uo.Organization.IsDeleted)
                .OrderBy(uo => uo.Organization.Name)
                .ToListAsync();
        }

        /// <summary>
        /// Update organization details
        /// </summary>
        public async Task<bool> UpdateOrganizationAsync(Organization organization)
        {
            var existing = await _dbContext.Organizations.FindAsync(organization.Id);
            if (existing == null || existing.IsDeleted)
                return false;

            existing.Name = organization.Name;
            existing.DisplayName = organization.DisplayName;
            existing.State = organization.State;
            existing.IsActive = organization.IsActive;
            existing.LastModifiedOn = DateTime.UtcNow;
            existing.LastModifiedBy = organization.LastModifiedBy;

            await _dbContext.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Delete organization (soft delete)
        /// </summary>
        public async Task<bool> DeleteOrganizationAsync(Guid organizationId, string deletedBy)
        {
            var organization = await _dbContext.Organizations.FindAsync(organizationId);
            if (organization == null || organization.IsDeleted)
                return false;

            organization.IsDeleted = true;
            organization.IsActive = false;
            organization.LastModifiedOn = DateTime.UtcNow;
            organization.LastModifiedBy = deletedBy;

            // Soft delete all OrganizationUsers entries
            var userOrgs = await _dbContext.OrganizationUsers
                .Where(uo => uo.OrganizationId == organizationId)
                .ToListAsync();

            foreach (var uo in userOrgs)
            {
                uo.IsDeleted = true;
                uo.IsActive = false;
                uo.RevokedOn = DateTime.UtcNow;
                uo.LastModifiedOn = DateTime.UtcNow;
                uo.LastModifiedBy = deletedBy;
            }

            await _dbContext.SaveChangesAsync();
            return true;
        }

        #endregion

        #region Permission & Role Management

        /// <summary>
        /// Check if user is the owner of an organization
        /// </summary>
        public async Task<bool> IsOwnerAsync(string userId, Guid organizationId)
        {
            var organization = await _dbContext.Organizations.FindAsync(organizationId);
            return organization != null && organization.OwnerId == userId && !organization.IsDeleted;
        }

        /// <summary>
        /// Check if user has administrator role in an organization
        /// </summary>
        public async Task<bool> IsAdministratorAsync(string userId, Guid organizationId)
        {
            var role = await GetUserRoleForOrganizationAsync(userId, organizationId);
            return role == ApplicationConstants.OrganizationRoles.Administrator;
        }

        /// <summary>
        /// Check if user can access an organization (has any active role)
        /// </summary>
        public async Task<bool> CanAccessOrganizationAsync(string userId, Guid organizationId)
        {
            return await _dbContext.OrganizationUsers
                .AnyAsync(uo => uo.UserId == userId 
                    && uo.OrganizationId == organizationId 
                    && uo.IsActive 
                    && !uo.IsDeleted);
        }

        /// <summary>
        /// Get user's role for a specific organization
        /// </summary>
        public async Task<string?> GetUserRoleForOrganizationAsync(string userId, Guid organizationId)
        {
            var userOrg = await _dbContext.OrganizationUsers
                .FirstOrDefaultAsync(uo => uo.UserId == userId 
                    && uo.OrganizationId == organizationId 
                    && uo.IsActive 
                    && !uo.IsDeleted);

            return userOrg?.Role;
        }

        #endregion

        #region User-Organization Assignment

        /// <summary>
        /// Grant a user access to an organization with a specific role
        /// </summary>
        public async Task<bool> GrantOrganizationAccessAsync(string userId, Guid organizationId, string role, string grantedBy)
        {
            // Validate role
            if (!ApplicationConstants.OrganizationRoles.IsValid(role))
                throw new ArgumentException($"Invalid role: {role}");

            // Check if organization exists
            var organization = await _dbContext.Organizations.FindAsync(organizationId);
            if (organization == null || organization.IsDeleted)
                return false;

            // Check user limit for Nine (MaxOrganizationUsers > 0)
            if (_settings.MaxOrganizationUsers > 0)
            {
                var currentUserCount = await _dbContext.OrganizationUsers
                    .Where(ou => ou.OrganizationId == organizationId
                              && ou.UserId != ApplicationConstants.SystemUser.Id
                              && ou.IsActive
                              && !ou.IsDeleted)
                    .CountAsync();

                if (currentUserCount >= _settings.MaxOrganizationUsers)
                {
                    throw new InvalidOperationException(
                        $"User limit reached. Nine allows maximum {_settings.MaxOrganizationUsers} user accounts (including system account). " +
                        "Upgrade to Nine Professional for unlimited users.");
                }
            }

            // Check if user already has access
            var existing = await _dbContext.OrganizationUsers
                .FirstOrDefaultAsync(uo => uo.UserId == userId && uo.OrganizationId == organizationId);

            if (existing != null)
            {
                // Reactivate if previously revoked
                if (!existing.IsActive || existing.IsDeleted)
                {
                    existing.IsActive = true;
                    existing.IsDeleted = false;
                    existing.Role = role;
                    existing.RevokedOn = null;
                    existing.LastModifiedOn = DateTime.UtcNow;
                    existing.LastModifiedBy = grantedBy;
                }
                else
                {
                    // Already has active access
                    return false;
                }
            }
            else
            {
                // Create new access
                var OrganizationUser = new OrganizationUser
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    OrganizationId = organizationId,
                    Role = role,
                    GrantedBy = grantedBy,
                    GrantedOn = DateTime.UtcNow,
                    IsActive = true,
                    CreatedOn = DateTime.UtcNow,
                    CreatedBy = grantedBy
                };

                _dbContext.OrganizationUsers.Add(OrganizationUser);
            }

            await _dbContext.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Revoke a user's access to an organization
        /// </summary>
        public async Task<bool> RevokeOrganizationAccessAsync(string userId, Guid organizationId, string revokedBy)
        {
            var userOrg = await _dbContext.OrganizationUsers
                .FirstOrDefaultAsync(uo => uo.UserId == userId 
                    && uo.OrganizationId == organizationId 
                    && uo.IsActive);

            if (userOrg == null)
                return false;

            // Don't allow revoking owner access
            if (userOrg.Role == ApplicationConstants.OrganizationRoles.Owner)
            {
                var organization = await _dbContext.Organizations.FindAsync(organizationId);
                if (organization?.OwnerId == userId)
                    throw new InvalidOperationException("Cannot revoke owner's access to their own organization");
            }

            userOrg.IsActive = false;
            userOrg.RevokedOn = DateTime.UtcNow;
            userOrg.LastModifiedOn = DateTime.UtcNow;
            userOrg.LastModifiedBy = revokedBy;

            await _dbContext.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Update a user's role in an organization
        /// </summary>
        public async Task<bool> UpdateUserRoleAsync(string userId, Guid organizationId, string newRole, string modifiedBy)
        {
            // Validate role
            if (!ApplicationConstants.OrganizationRoles.IsValid(newRole))
                throw new ArgumentException($"Invalid role: {newRole}");

            var userOrg = await _dbContext.OrganizationUsers
                .FirstOrDefaultAsync(uo => uo.UserId == userId 
                    && uo.OrganizationId == organizationId 
                    && uo.IsActive 
                    && !uo.IsDeleted);

            if (userOrg == null)
                return false;

            // Don't allow changing owner role
            if (userOrg.Role == ApplicationConstants.OrganizationRoles.Owner)
            {
                var organization = await _dbContext.Organizations.FindAsync(organizationId);
                if (organization?.OwnerId == userId)
                    throw new InvalidOperationException("Cannot change the role of the organization owner");
            }

            userOrg.Role = newRole;
            userOrg.LastModifiedOn = DateTime.UtcNow;
            userOrg.LastModifiedBy = modifiedBy;

            await _dbContext.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Get all users with access to an organization
        /// </summary>
        public async Task<List<OrganizationUser>> GetOrganizationUsersAsync(Guid organizationId)
        {
            return await _dbContext.OrganizationUsers
                .Where(uo => uo.OrganizationId == organizationId && uo.IsActive && !uo.IsDeleted && uo.UserId != ApplicationConstants.SystemUser.Id)
                .OrderBy(uo => uo.Role)
                .ThenBy(uo => uo.UserId)
                .ToListAsync();
        }

        /// <summary>
        /// Get all organization assignments for a user (including revoked)
        /// </summary>
        public async Task<List<OrganizationUser>> GetUserAssignmentsAsync()
        {
            var userId = await _userContext.GetUserIdAsync();
            if (string.IsNullOrEmpty(userId))
                throw new InvalidOperationException("Cannot get user assignments: User ID is not available in context.");

            return await _dbContext.OrganizationUsers
                .Include(uo => uo.Organization)
                .Where(uo => uo.UserId == userId && !uo.IsDeleted && uo.UserId != ApplicationConstants.SystemUser.Id)
                .OrderByDescending(uo => uo.IsActive)
                .ThenBy(uo => uo.Organization.Name)
                .ToListAsync();
        }

        /// <summary>
        /// Get all organization assignments for a user (including revoked)
        /// </summary>
        public async Task<List<OrganizationUser>> GetActiveUserAssignmentsAsync()
        {
            var userId = await _userContext.GetUserIdAsync();
            if (string.IsNullOrEmpty(userId))
                throw new InvalidOperationException("Cannot get user assignments: User ID is not available in context.");

            return await _dbContext.OrganizationUsers
                .Include(uo => uo.Organization)
                .Where(uo => uo.UserId == userId && !uo.IsDeleted && uo.IsActive && uo.UserId != ApplicationConstants.SystemUser.Id)
                .OrderByDescending(uo => uo.IsActive)
                .ThenBy(uo => uo.Organization.Name)
                .ToListAsync();
        }

        /// <summary>
        /// Gets organization settings by organization ID (for scheduled tasks).
        /// </summary>
        public async Task<OrganizationSettings?> GetOrganizationSettingsByOrgIdAsync(Guid organizationId)
        {
            return await _dbContext.OrganizationSettings
                .Where(s => !s.IsDeleted && s.OrganizationId == organizationId)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Gets the organization settings for the current user's active organization.
        /// If no settings exist, creates default settings.
        /// </summary>
        public async Task<OrganizationSettings?> GetOrganizationSettingsAsync()
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            if (!organizationId.HasValue || organizationId == Guid.Empty)
                throw new InvalidOperationException("Organization ID not found for current user");

            return await GetOrganizationSettingsByOrgIdAsync(organizationId.Value);
        }

        /// <summary>
        /// Updates the organization settings for the current user's organization.
        /// </summary>
        public async Task<bool> UpdateOrganizationSettingsAsync(OrganizationSettings settings)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            if (!organizationId.HasValue || organizationId == Guid.Empty)
                throw new InvalidOperationException("Organization ID not found for current user");

            if (settings.OrganizationId != organizationId.Value)
                throw new InvalidOperationException("Cannot update settings for a different organization");

            var userId = await _userContext.GetUserIdAsync();
            settings.LastModifiedOn = DateTime.UtcNow;
            settings.LastModifiedBy = string.IsNullOrEmpty(userId) ? string.Empty : userId;

            _dbContext.OrganizationSettings.Update(settings);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        #endregion
    }
}
