using Nine.Core.Interfaces.Services;
using Nine.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nine.Core.Constants;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;

namespace Nine.Application.Services
{
    public class PropertyManagementService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ApplicationSettings _applicationSettings;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUserContextService _userContext;
        private readonly CalendarEventService _calendarEventService;
        private readonly ChecklistService _checklistService;

        public PropertyManagementService(
            ApplicationDbContext dbContext, 
            IOptions<ApplicationSettings> settings, 
            IHttpContextAccessor httpContextAccessor,
            IUserContextService userContext,
            CalendarEventService calendarEventService,
            ChecklistService checklistService)
        {
            _dbContext = dbContext;
            _applicationSettings = settings.Value;
            _httpContextAccessor = httpContextAccessor;
            _userContext = userContext;
            _calendarEventService = calendarEventService;
            _checklistService = checklistService;
        }

        #region Properties
        public async Task<List<Property>> GetPropertiesAsync()
        {
            var _userId = await _userContext.GetUserIdAsync();

            if (string.IsNullOrEmpty(_userId))
            {
                // Handle the case when the user is not authenticated
                throw new UnauthorizedAccessException("User is not authenticated.");
            }
            
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            
            return await _dbContext.Properties
                .Include(p => p.Leases)
                .Include(p => p.Documents)
                .Where(p => !p.IsDeleted && p.OrganizationId == organizationId)
                .ToListAsync();
        }

        public async Task<Property?> GetPropertyByIdAsync(Guid propertyId)
        {
            var _userId = await _userContext.GetUserIdAsync();

            if (string.IsNullOrEmpty(_userId))
            {
                // Handle the case when the user is not authenticated
                throw new UnauthorizedAccessException("User is not authenticated.");
            }
            
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            return await _dbContext.Properties
            .Include(p => p.Leases)
            .Include(p => p.Documents)
            .FirstOrDefaultAsync(p => p.Id == propertyId && p.OrganizationId == organizationId && !p.IsDeleted);
        }

        public async Task<List<Property>> SearchPropertiesByAddressAsync(string searchTerm)
        {
            var _userId = await _userContext.GetUserIdAsync();

            if (string.IsNullOrEmpty(_userId))
            {
                // Handle the case when the user is not authenticated
                throw new UnauthorizedAccessException("User is not authenticated.");
            }
            
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return await _dbContext.Properties
                    .Where(p => !p.IsDeleted && p.OrganizationId == organizationId)
                    .OrderBy(p => p.Address)
                    .Take(20)
                    .ToListAsync();
            }
            
            return await _dbContext.Properties
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

        public async Task AddPropertyAsync(Property property)
        {
            var _userId = await _userContext.GetUserIdAsync();

            if (string.IsNullOrEmpty(_userId))
            {
                // Handle the case when the user is not authenticated
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            // Set tracking fields automatically
            property.Id = Guid.NewGuid();
            property.OrganizationId = organizationId!.Value;
            property.CreatedBy = _userId;
            property.CreatedOn = DateTime.UtcNow;

            // Set initial routine inspection due date to 30 days from creation
            property.NextRoutineInspectionDueDate = DateTime.Today.AddDays(30);

            await _dbContext.Properties.AddAsync(property);
            await _dbContext.SaveChangesAsync();

            // Create calendar event for the first routine inspection
            await CreateRoutineInspectionCalendarEventAsync(property);
        }

        public async Task UpdatePropertyAsync(Property property)
        {
            var _userId = await _userContext.GetUserIdAsync();

            if (string.IsNullOrEmpty(_userId))
            {
                // Handle the case when the user is not authenticated
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            // Security: Verify property belongs to active organization
            var existing = await _dbContext.Properties
                .FirstOrDefaultAsync(p => p.Id == property.Id && p.OrganizationId == organizationId);

            if (existing == null)
            {
                throw new UnauthorizedAccessException($"Property {property.Id} not found in active organization.");
            }

            // Set tracking fields automatically
            property.LastModifiedBy = _userId;
            property.LastModifiedOn = DateTime.UtcNow;
            property.OrganizationId = organizationId!.Value; // Prevent org hijacking

            _dbContext.Entry(existing).CurrentValues.SetValues(property);
            await _dbContext.SaveChangesAsync();
        }

        public async Task DeletePropertyAsync(Guid propertyId)
        {
            var _userId = await _userContext.GetUserIdAsync();

            if (string.IsNullOrEmpty(_userId))
            {
                // Handle the case when the user is not authenticated
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            if (_applicationSettings.SoftDeleteEnabled)
            {
                await SoftDeletePropertyAsync(propertyId);
                return;
            }
            else
            {
                var property = await _dbContext.Properties
                    .FirstOrDefaultAsync(p => p.Id == propertyId &&
                        p.OrganizationId == organizationId);

                if (property != null)
                {
                    _dbContext.Properties.Remove(property);
                    await _dbContext.SaveChangesAsync();
                }
            }
        }

        private async Task SoftDeletePropertyAsync(Guid propertyId)
        {
            var _userId = await _userContext.GetUserIdAsync();

            if (string.IsNullOrEmpty(_userId))
            {
                // Handle the case when the user is not authenticated
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            var property = await _dbContext.Properties
                .FirstOrDefaultAsync(p => p.Id == propertyId && p.OrganizationId == organizationId);
                
            if (property != null && !property.IsDeleted)
            {
                property.IsDeleted = true;
                property.LastModifiedOn = DateTime.UtcNow;
                property.LastModifiedBy = _userId;
                _dbContext.Properties.Update(property);
                await _dbContext.SaveChangesAsync();

                var leases = await GetLeasesByPropertyIdAsync(propertyId);
                foreach (var lease in leases)
                {
                    lease.Status = ApplicationConstants.LeaseStatuses.Terminated;
                    lease.LastModifiedOn = DateTime.UtcNow;
                    lease.LastModifiedBy = _userId;
                    await UpdateLeaseAsync(lease);

                    var tenants = await GetTenantsByLeaseIdAsync(lease.Id);
                    foreach (var tenant in tenants)
                    {
                        var tenantLeases = await GetLeasesByTenantIdAsync(tenant.Id);
                        tenantLeases = tenantLeases.Where(l => l.PropertyId != propertyId && !l.IsDeleted).ToList();

                        if(tenantLeases.Count == 0) // Only this lease
                        {
                            tenant.IsActive = false;
                            tenant.LastModifiedBy = _userId;
                            tenant.LastModifiedOn = DateTime.UtcNow;
                            await UpdateTenantAsync(tenant);
                        }
                    }

                }

            }
        }
        #endregion

        #region Tenants

        public async Task<List<Tenant>> GetTenantsAsync()
        {
            var _userId = await _userContext.GetUserIdAsync();

            if (string.IsNullOrEmpty(_userId))
            {
                // Handle the case when the user is not authenticated
                throw new UnauthorizedAccessException("User is not authenticated.");
            }
            
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            
            return await _dbContext.Tenants
                .Include(t => t.Leases)
                .Where(t => !t.IsDeleted && t.OrganizationId == organizationId)
                .ToListAsync();
        }
        
        public async Task<List<Tenant>> GetTenantsByLeaseIdAsync(Guid leaseId)
        {
            var _userId = await _userContext.GetUserIdAsync();

            if (string.IsNullOrEmpty(_userId))
            {
                // Handle the case when the user is not authenticated
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            var leases = await _dbContext.Leases
                .Include(l => l.Tenant)
                .Where(l => l.Id == leaseId && l.Tenant!.OrganizationId == organizationId && !l.IsDeleted && !l.Tenant.IsDeleted)
                .ToListAsync();

            var tenantIds = leases.Select(l => l.TenantId).Distinct().ToList();
            
            return await _dbContext.Tenants
                .Where(t => tenantIds.Contains(t.Id) && t.OrganizationId == organizationId && !t.IsDeleted)
                .ToListAsync();
        }
        public async Task<List<Tenant>> GetTenantsByPropertyIdAsync(Guid propertyId)
        {
            var _userId = await _userContext.GetUserIdAsync();

            if (string.IsNullOrEmpty(_userId))
            {
                // Handle the case when the user is not authenticated
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            var leases = await _dbContext.Leases
                .Include(l => l.Tenant)
                .Where(l => l.PropertyId == propertyId && l.Tenant!.OrganizationId == organizationId && !l.IsDeleted && !l.Tenant.IsDeleted)
                .ToListAsync();

            var tenantIds = leases.Select(l => l.TenantId).Distinct().ToList();
            
            return await _dbContext.Tenants
                .Where(t => tenantIds.Contains(t.Id) && t.OrganizationId == organizationId && !t.IsDeleted)
                .ToListAsync();
        }

        public async Task<Tenant?> GetTenantByIdAsync(Guid tenantId)
        {
            var _userId = await _userContext.GetUserIdAsync();

            if (string.IsNullOrEmpty(_userId))
            {
                // Handle the case when the user is not authenticated
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            return await _dbContext.Tenants
                .Include(t => t.Leases)
                .FirstOrDefaultAsync(t => t.Id == tenantId && t.OrganizationId == organizationId && !t.IsDeleted);
        }

        public async Task<Tenant?> GetTenantByIdentificationNumberAsync(string identificationNumber)
        {
            var _userId = await _userContext.GetUserIdAsync();

            if (string.IsNullOrEmpty(_userId))
            {
                // Handle the case when the user is not authenticated
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            return await _dbContext.Tenants
                .Include(t => t.Leases)
                .FirstOrDefaultAsync(t => t.IdentificationNumber == identificationNumber && t.OrganizationId == organizationId && !t.IsDeleted);
        }

        public async Task<Tenant> AddTenantAsync(Tenant tenant)
        {
            var _userId = await _userContext.GetUserIdAsync();

            if (string.IsNullOrEmpty(_userId))
            {
                // Handle the case when the user is not authenticated
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            // Set tracking fields automatically
            tenant.Id = Guid.NewGuid();
            tenant.OrganizationId = organizationId!.Value;
            tenant.CreatedBy = _userId;
            tenant.CreatedOn = DateTime.UtcNow;

            await _dbContext.Tenants.AddAsync(tenant);
            await _dbContext.SaveChangesAsync();
            
            return tenant;
        }

        public async Task UpdateTenantAsync(Tenant tenant)
        {
            var _userId = await _userContext.GetUserIdAsync();

            if (string.IsNullOrEmpty(_userId))
            {
                // Handle the case when the user is not authenticated
                throw new UnauthorizedAccessException("User is not authenticated.");
            }
            
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            // Security: Verify tenant belongs to active organization
            var existing = await _dbContext.Tenants
                .FirstOrDefaultAsync(t => t.Id == tenant.Id && t.OrganizationId == organizationId);

            if (existing == null)
            {
                throw new UnauthorizedAccessException($"Tenant {tenant.Id} not found in active organization.");
            }

            // Set tracking fields automatically
            tenant.LastModifiedOn = DateTime.UtcNow;
            tenant.LastModifiedBy = _userId;
            tenant.OrganizationId = organizationId!.Value; // Prevent org hijacking

            _dbContext.Entry(existing).CurrentValues.SetValues(tenant);
            await _dbContext.SaveChangesAsync();
        }

        public async Task DeleteTenantAsync(Tenant tenant)
        {
            var userId = await _userContext.GetUserIdAsync();

            if (string.IsNullOrEmpty(userId))
            {
                // Handle the case when the user is not authenticated
                throw new UnauthorizedAccessException("User is not authenticated.");
            }
            if (_applicationSettings.SoftDeleteEnabled)
            {
                await SoftDeleteTenantAsync(tenant);
                return;
            }
            else
            {
                if (tenant != null)
                {
                    _dbContext.Tenants.Remove(tenant);
                    await _dbContext.SaveChangesAsync();
                }
            }
        }

        private async Task SoftDeleteTenantAsync(Tenant tenant)
        {
            var userId = await _userContext.GetUserIdAsync();

            if (tenant != null && !tenant.IsDeleted && !string.IsNullOrEmpty(userId))
            {
                tenant.IsDeleted = true;
                tenant.LastModifiedOn = DateTime.UtcNow;
                tenant.LastModifiedBy = userId;
                _dbContext.Tenants.Update(tenant);
                await _dbContext.SaveChangesAsync();
            }
        }

        #endregion

        #region Leases

        public async Task<List<Lease>> GetLeasesAsync()
        {
            var _userId = await _userContext.GetUserIdAsync();

            if (string.IsNullOrEmpty(_userId))
            {
                // Handle the case when the user is not authenticated
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            return await _dbContext.Leases
                .Include(l => l.Property)
                .Include(l => l.Tenant)
                .Where(l => !l.IsDeleted && !l.Tenant!.IsDeleted && !l.Property.IsDeleted && l.Property.OrganizationId == organizationId)
                .ToListAsync();
        }
        public async Task<Lease?> GetLeaseByIdAsync(Guid leaseId)
        {
            var _userId = await _userContext.GetUserIdAsync();

            if (string.IsNullOrEmpty(_userId))
            {
                // Handle the case when the user is not authenticated
                throw new UnauthorizedAccessException("User is not authenticated.");
            }
            
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            return await _dbContext.Leases
                .Include(l => l.Property)
                .Include(l => l.Tenant)
                .FirstOrDefaultAsync(l => l.Id == leaseId && !l.IsDeleted && (l.Tenant == null || !l.Tenant.IsDeleted) && !l.Property.IsDeleted && l.Property.OrganizationId == organizationId);
        }

        public async Task<List<Lease>> GetLeasesByPropertyIdAsync(Guid propertyId)
        {
            var _userId = await _userContext.GetUserIdAsync();

            if (string.IsNullOrEmpty(_userId))
            {
                // Handle the case when the user is not authenticated
                throw new UnauthorizedAccessException("User is not authenticated.");
            }
            
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            var leases = await _dbContext.Leases
            .Include(l => l.Property)
            .Include(l => l.Tenant)
            .Where(l => l.PropertyId == propertyId && !l.IsDeleted && l.Property.OrganizationId == organizationId)
            .ToListAsync();
            
            return leases;
        }

        public async Task<List<Lease>> GetCurrentAndUpcomingLeasesByPropertyIdAsync(Guid propertyId)
        {
            var _userId = await _userContext.GetUserIdAsync();

            if (string.IsNullOrEmpty(_userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }
            
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            return await _dbContext.Leases
                .Include(l => l.Property)
                .Include(l => l.Tenant)
                .Where(l => l.PropertyId == propertyId 
                    && !l.IsDeleted 
                    && l.Property.OrganizationId == organizationId
                    && (l.Status == ApplicationConstants.LeaseStatuses.Pending
                        || l.Status == ApplicationConstants.LeaseStatuses.Active))
                .ToListAsync();
        }

        public async Task<List<Lease>> GetActiveLeasesByPropertyIdAsync(Guid propertyId)
        {
            var _userId = await _userContext.GetUserIdAsync();

            if (string.IsNullOrEmpty(_userId))
            {
                // Handle the case when the user is not authenticated
                throw new UnauthorizedAccessException("User is not authenticated.");
            }
            
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            var leases = await _dbContext.Leases
            .Include(l => l.Property)
            .Include(l => l.Tenant)
            .Where(l => l.PropertyId == propertyId && !l.IsDeleted && !l.Tenant!.IsDeleted && !l.Property.IsDeleted && l.Property.OrganizationId == organizationId)
            .ToListAsync();
            
            return leases
                .Where(l => l.IsActive)
                .ToList();
        }

        
        public async Task<List<Lease>> GetLeasesByTenantIdAsync(Guid tenantId)
        {
            var _userId = await _userContext.GetUserIdAsync();

            if (string.IsNullOrEmpty(_userId))
            {
                // Handle the case when the user is not authenticated
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            return await _dbContext.Leases
                .Include(l => l.Property)
                .Include(l => l.Tenant)
                .Where(l => l.TenantId == tenantId && !l.Tenant!.IsDeleted && !l.IsDeleted && l.Property.OrganizationId == organizationId)
                .ToListAsync();
        }

        public async Task<Lease?> AddLeaseAsync(Lease lease)
        {
            var _userId = await _userContext.GetUserIdAsync();

            if (string.IsNullOrEmpty(_userId))
            {
                // Handle the case when the user is not authenticated
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            var property = await GetPropertyByIdAsync(lease.PropertyId);
            if(property is null || property.OrganizationId != organizationId)
                return lease;

            // Set tracking fields automatically
            lease.Id = Guid.NewGuid();
            lease.OrganizationId = organizationId!.Value;
            lease.CreatedBy = _userId;
            lease.CreatedOn = DateTime.UtcNow;

            await _dbContext.Leases.AddAsync(lease);

            property.IsActive = false;
            property.LastModifiedOn = DateTime.UtcNow;
            property.LastModifiedBy = _userId;

            _dbContext.Properties.Update(property);

            await _dbContext.SaveChangesAsync();

            return lease;
        }

        public async Task UpdateLeaseAsync(Lease lease)
        {
            var _userId = await _userContext.GetUserIdAsync();
            
            if (string.IsNullOrEmpty(_userId))
            {
                // Handle the case when the user is not authenticated
                throw new UnauthorizedAccessException("User is not authenticated.");
            }
            
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            
            // Security: Verify lease belongs to active organization
            var existing = await _dbContext.Leases
                .Include(l => l.Property)
                .FirstOrDefaultAsync(l => l.Id == lease.Id && l.Property.OrganizationId == organizationId);
            
            if (existing == null)
            {
                throw new UnauthorizedAccessException($"Lease {lease.Id} not found in active organization.");
            }
            
            // Set tracking fields automatically
            lease.LastModifiedOn = DateTime.UtcNow;
            lease.LastModifiedBy = _userId;

            _dbContext.Entry(existing).CurrentValues.SetValues(lease);
            await _dbContext.SaveChangesAsync();
        }

        public async Task DeleteLeaseAsync(Guid leaseId)
        {
            var _userId = await _userContext.GetUserIdAsync();

            if (string.IsNullOrEmpty(_userId))
            {
                // Handle the case when the user is not authenticated
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            if( !await _dbContext.Leases.AnyAsync(l => l.Id == leaseId && l.Property.OrganizationId == organizationId))
            {
                throw new UnauthorizedAccessException("User does not have access to this lease.");
            }

            if (_applicationSettings.SoftDeleteEnabled)
            {
                await SoftDeleteLeaseAsync(leaseId);
                return;
            }
            else
            {
                var lease = await _dbContext.Leases.FirstOrDefaultAsync(l => l.Id == leaseId);
                if (lease != null)
                {
                    _dbContext.Leases.Remove(lease);
                    await _dbContext.SaveChangesAsync();
                }
            }
        }

        private async Task SoftDeleteLeaseAsync(Guid leaseId)
        {
            var userId = await _userContext.GetUserIdAsync();
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            if (string.IsNullOrEmpty(userId))
            {
                // Handle the case when the user is not authenticated
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

       
            var lease = await _dbContext.Leases.FirstOrDefaultAsync(l => l.Id == leaseId && l.Property.OrganizationId == organizationId);
            if (lease != null && !lease.IsDeleted && !string.IsNullOrEmpty(userId))
            {
                lease.IsDeleted = true;
                lease.LastModifiedOn = DateTime.UtcNow;
                lease.LastModifiedBy = userId;
                _dbContext.Leases.Update(lease);
                await _dbContext.SaveChangesAsync();
            }
        }

    #endregion

        #region Invoices
        
        public async Task<List<Invoice>> GetInvoicesAsync()
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            
            return await _dbContext.Invoices
                .Include(i => i.Lease)
                    .ThenInclude(l => l.Property)
                .Include(i => i.Lease)
                    .ThenInclude(l => l.Tenant)
                .Include(i => i.Payments)
                .Where(i => !i.IsDeleted && i.Lease.Property.OrganizationId == organizationId)
                .OrderByDescending(i => i.DueOn)
                .ToListAsync();
        }

        public async Task<Invoice?> GetInvoiceByIdAsync(Guid invoiceId)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();


            return await _dbContext.Invoices
                .Include(i => i.Lease)
                    .ThenInclude(l => l.Property)
                .Include(i => i.Lease)
                    .ThenInclude(l => l.Tenant)
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.Id == invoiceId
                    && !i.IsDeleted 
                    && i.Lease.Property.OrganizationId == organizationId);
        }

        public async Task<List<Invoice>> GetInvoicesByLeaseIdAsync(Guid leaseId)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            return await _dbContext.Invoices
                .Include(i => i.Lease)
                    .ThenInclude(l => l.Property)
                .Include(i => i.Lease)
                    .ThenInclude(l => l.Tenant)
                .Include(i => i.Payments)
                .Where(i => i.LeaseId == leaseId
                    && !i.IsDeleted
                    && i.Lease.Property.OrganizationId == organizationId)
                .OrderByDescending(i => i.DueOn)
                .ToListAsync();
        }

        public async Task AddInvoiceAsync(Invoice invoice)
        {
            var _userId = await _userContext.GetUserIdAsync();

            if (string.IsNullOrEmpty(_userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            var lease = await _dbContext.Leases
                .Include(l => l.Property)
                .FirstOrDefaultAsync(l => l.Id == invoice.LeaseId && !l.IsDeleted);

            if (lease == null || lease.Property.OrganizationId != organizationId)
            {
                throw new UnauthorizedAccessException("User does not have access to this lease.");
            }

            // Set tracking fields automatically
            invoice.Id = Guid.NewGuid();
            invoice.OrganizationId = organizationId!.Value;
            invoice.CreatedBy = _userId;
            invoice.CreatedOn = DateTime.UtcNow;

            await _dbContext.Invoices.AddAsync(invoice);
            await _dbContext.SaveChangesAsync();
        }

        public async Task UpdateInvoiceAsync(Invoice invoice)
        {
            var userId = await _userContext.GetUserIdAsync();
            if (userId == null)
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            
            // Security: Verify invoice belongs to active organization
            var existing = await _dbContext.Invoices
                .Include(i => i.Lease)
                    .ThenInclude(l => l.Property)
                .FirstOrDefaultAsync(i => i.Id == invoice.Id && i.OrganizationId == organizationId);

            if (existing == null)
            {
                throw new UnauthorizedAccessException($"Invoice {invoice.Id} not found in active organization.");
            }

            // Set tracking fields automatically
            invoice.LastModifiedOn = DateTime.UtcNow;
            invoice.LastModifiedBy = userId;
            invoice.OrganizationId = organizationId!.Value; // Prevent org hijacking

            _dbContext.Entry(existing).CurrentValues.SetValues(invoice);
            await _dbContext.SaveChangesAsync();
        }

        public async Task DeleteInvoiceAsync(Invoice invoice)
        {
            var userId = await _userContext.GetUserIdAsync();
            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            if (_applicationSettings.SoftDeleteEnabled)
            {
                invoice.IsDeleted = true;
                invoice.LastModifiedOn = DateTime.UtcNow;
                invoice.LastModifiedBy = userId;
                _dbContext.Invoices.Update(invoice);
            }
            else
            {
                _dbContext.Invoices.Remove(invoice);
            }
            await _dbContext.SaveChangesAsync();
        }

        public async Task<string> GenerateInvoiceNumberAsync()
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();    
            var invoiceCount = await _dbContext.Invoices
                .Where(i => i.OrganizationId == organizationId)
                .CountAsync();
            
            var nextNumber = invoiceCount + 1;
            return $"INV-{DateTime.Now:yyyyMM}-{nextNumber:D5}";
        }

        #endregion

        #region Payments
        
        public async Task<List<Payment>> GetPaymentsAsync()
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            
            return await _dbContext.Payments
                .Include(p => p.Invoice)
                    .ThenInclude(i => i!.Lease)
                        .ThenInclude(l => l!.Property)
                .Include(p => p.Invoice)
                    .ThenInclude(i => i!.Lease)
                        .ThenInclude(l => l!.Tenant)
                .Where(p => !p.IsDeleted && p.Invoice.Lease.Property.OrganizationId == organizationId)
                .OrderByDescending(p => p.PaidOn)
                .ToListAsync();
        }


        public async Task<Payment?> GetPaymentByIdAsync(Guid paymentId)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            return await _dbContext.Payments
                .Include(p => p.Invoice)
                    .ThenInclude(i => i!.Lease)
                        .ThenInclude(l => l!.Property)
                .Include(p => p.Invoice)
                    .ThenInclude(i => i!.Lease)
                        .ThenInclude(l => l!.Tenant)
                .FirstOrDefaultAsync(p => p.Id == paymentId && !p.IsDeleted && p.Invoice.Lease.Property.OrganizationId == organizationId);
        }

        public async Task<List<Payment>> GetPaymentsByInvoiceIdAsync(Guid invoiceId)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            return await _dbContext.Payments
                .Include(p => p.Invoice)
                .Where(p => p.InvoiceId == invoiceId && !p.IsDeleted && p.Invoice.Lease.Property.OrganizationId == organizationId)
                .OrderByDescending(p => p.PaidOn)
                .ToListAsync();
        }

        public async Task AddPaymentAsync(Payment payment)
        {
            var _userId = await _userContext.GetUserIdAsync();

            if (string.IsNullOrEmpty(_userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            
            // Set tracking fields automatically
            payment.Id = Guid.NewGuid();
            payment.OrganizationId = organizationId!.Value;
            payment.CreatedBy = _userId;
            payment.CreatedOn = DateTime.UtcNow;

            await _dbContext.Payments.AddAsync(payment);
            await _dbContext.SaveChangesAsync();
            
            // Update invoice paid amount
            await UpdateInvoicePaidAmountAsync(payment.InvoiceId);
        }

        public async Task UpdatePaymentAsync(Payment payment)
        {
            var _userId = await _userContext.GetUserIdAsync();

            if (string.IsNullOrEmpty(_userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            
            // Security: Verify payment belongs to active organization
            var existing = await _dbContext.Payments
                .FirstOrDefaultAsync(p => p.Id == payment.Id && p.OrganizationId == organizationId);

            if (existing == null)
            {
                throw new UnauthorizedAccessException($"Payment {payment.Id} not found in active organization.");
            }
            
            // Set tracking fields automatically
            payment.OrganizationId = organizationId!.Value;
            payment.LastModifiedOn = DateTime.UtcNow;
            payment.LastModifiedBy = _userId;

            _dbContext.Entry(existing).CurrentValues.SetValues(payment);
            await _dbContext.SaveChangesAsync();
            
            // Update invoice paid amount
            await UpdateInvoicePaidAmountAsync(payment.InvoiceId);
        }

        public async Task DeletePaymentAsync(Payment payment)
        {
            var userId = await _userContext.GetUserIdAsync();
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            if (string.IsNullOrEmpty(userId) || payment.OrganizationId != organizationId)
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            var invoiceId = payment.InvoiceId;

            if (_applicationSettings.SoftDeleteEnabled)
            {
                payment.IsDeleted = true;
                payment.LastModifiedOn = DateTime.UtcNow;
                payment.LastModifiedBy = userId;
                _dbContext.Payments.Update(payment);
            }
            else
            {
                _dbContext.Payments.Remove(payment);
            }
            await _dbContext.SaveChangesAsync();
            
            // Update invoice paid amount
            await UpdateInvoicePaidAmountAsync(invoiceId);
        }

        private async Task UpdateInvoicePaidAmountAsync(Guid invoiceId)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            var invoice = await _dbContext.Invoices.Where(i => i.Id == invoiceId && i.OrganizationId == organizationId).FirstOrDefaultAsync();
            if (invoice != null)
            {
                var totalPaid = await _dbContext.Payments
                    .Where(p => p.InvoiceId == invoiceId && !p.IsDeleted && p.OrganizationId == organizationId)
                    .SumAsync(p => p.Amount);
                
                invoice.AmountPaid = totalPaid;
                
                // Update invoice status based on payment
                if (totalPaid >= invoice.Amount)
                {
                    invoice.Status = "Paid";
                    invoice.PaidOn = DateTime.UtcNow;
                }
                else if (totalPaid > 0)
                {
                    invoice.Status = "Partial";
                }
                
                await _dbContext.SaveChangesAsync();
            }
        }

        #endregion

        #region Documents
        
        public async Task<List<Document>> GetDocumentsAsync()
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            
            return await _dbContext.Documents
                .Include(d => d.Property)
                .Include(d => d.Tenant)
                .Include(d => d.Lease)
                    .ThenInclude(l => l!.Property)
                .Include(d => d.Lease)
                    .ThenInclude(l => l!.Tenant)
                .Include(d => d.Invoice)
                .Include(d => d.Payment)
                .Where(d => !d.IsDeleted && d.OrganizationId == organizationId && d.Property != null && !d.Property.IsDeleted)
                .OrderByDescending(d => d.CreatedOn)
                .ToListAsync();
        }

        public async Task<Document?> GetDocumentByIdAsync(Guid documentId)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();


            return await _dbContext.Documents
                .Include(d => d.Property)
                .Include(d => d.Tenant)
                .Include(d => d.Lease)
                    .ThenInclude(l => l!.Property)
                .Include(d => d.Lease)
                    .ThenInclude(l => l!.Tenant)
                .Include(d => d.Invoice)
                .Include(d => d.Payment)
                .FirstOrDefaultAsync(d => d.Id == documentId && !d.IsDeleted && d.OrganizationId == organizationId);
        }

        public async Task<List<Document>> GetDocumentsByLeaseIdAsync(Guid leaseId)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            return await _dbContext.Documents
                .Include(d => d.Lease)
                    .ThenInclude(l => l!.Property)
                .Include(d => d.Lease)
                    .ThenInclude(l => l!.Tenant)
                .Where(d => d.LeaseId == leaseId && !d.IsDeleted && d.OrganizationId == organizationId)
                .OrderByDescending(d => d.CreatedOn)
                .ToListAsync();
        }
        
        public async Task<List<Document>> GetDocumentsByPropertyIdAsync(Guid propertyId)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            return await _dbContext.Documents
                .Include(d => d.Property)
                .Include(d => d.Tenant)
                .Include(d => d.Lease)
                .Where(d => d.PropertyId == propertyId && !d.IsDeleted && d.OrganizationId == organizationId)
                .OrderByDescending(d => d.CreatedOn)
                .ToListAsync();
        }

        public async Task<List<Document>> GetDocumentsByTenantIdAsync(Guid tenantId)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            return await _dbContext.Documents
                .Include(d => d.Property)
                .Include(d => d.Tenant)
                .Include(d => d.Lease)
                .Where(d => d.TenantId == tenantId && !d.IsDeleted && d.OrganizationId == organizationId)
                .OrderByDescending(d => d.CreatedOn)
                .ToListAsync();
        }

        public async Task<Document> AddDocumentAsync(Document document)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            var _userId = await _userContext.GetUserIdAsync();
            if (string.IsNullOrEmpty(_userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            document.Id = Guid.NewGuid();
            document.OrganizationId = organizationId!.Value;
            document.CreatedBy = _userId;
            document.CreatedOn = DateTime.UtcNow;
            _dbContext.Documents.Add(document);
            await _dbContext.SaveChangesAsync();
            return document;
        }

        public async Task UpdateDocumentAsync(Document document)
        {
            var _userId = await _userContext.GetUserIdAsync();
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            if (string.IsNullOrEmpty(_userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            // Security: Verify document belongs to active organization
            var existing = await _dbContext.Documents
                .FirstOrDefaultAsync(d => d.Id == document.Id && d.OrganizationId == organizationId);

            if (existing == null)
            {
                throw new UnauthorizedAccessException($"Document {document.Id} not found in active organization.");
            }

            // Set tracking fields automatically
            document.LastModifiedBy = _userId;
            document.LastModifiedOn = DateTime.UtcNow;
            document.OrganizationId = organizationId!.Value; // Prevent org hijacking

            _dbContext.Entry(existing).CurrentValues.SetValues(document);
            await _dbContext.SaveChangesAsync();
        }

        public async Task DeleteDocumentAsync(Document document)
        {

            var _userId = await _userContext.GetUserIdAsync();
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            if (string.IsNullOrEmpty(_userId) || document.OrganizationId != organizationId)
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }
            
            if (!_applicationSettings.SoftDeleteEnabled)
            {
                _dbContext.Documents.Remove(document);
            }
            else
            {
                document.IsDeleted = true;
                document.LastModifiedBy = _userId;
                document.LastModifiedOn = DateTime.UtcNow;
                _dbContext.Documents.Update(document);

                // Clear reverse foreign keys in related entities
                // Since soft delete doesn't trigger DB cascade, we need to manually clear DocumentId
                
                // Clear Inspection.DocumentId if any inspection links to this document
                var inspection = await _dbContext.Inspections
                    .FirstOrDefaultAsync(i => i.DocumentId == document.Id);
                if (inspection != null)
                {
                    inspection.DocumentId = null;
                    inspection.LastModifiedBy = _userId;
                    inspection.LastModifiedOn = DateTime.UtcNow;
                    _dbContext.Inspections.Update(inspection);
                }

                // Clear Lease.DocumentId if any lease links to this document
                var lease = await _dbContext.Leases
                    .FirstOrDefaultAsync(l => l.DocumentId == document.Id);
                if (lease != null)
                {
                    lease.DocumentId = null;
                    lease.LastModifiedBy = _userId;
                    lease.LastModifiedOn = DateTime.UtcNow;
                    _dbContext.Leases.Update(lease);
                }

                // Clear Invoice.DocumentId if any invoice links to this document
                if (document.InvoiceId != null)
                {
                    var invoice = await _dbContext.Invoices
                        .FirstOrDefaultAsync(i => i.Id == document.InvoiceId.Value && i.DocumentId == document.Id);
                    if (invoice != null)
                    {
                        invoice.DocumentId = null;
                        invoice.LastModifiedBy = _userId;
                        invoice.LastModifiedOn = DateTime.UtcNow;
                        _dbContext.Invoices.Update(invoice);
                    }
                }

                // Clear Payment.DocumentId if any payment links to this document
                if (document.PaymentId != null)
                {
                    var payment = await _dbContext.Payments
                        .FirstOrDefaultAsync(p => p.Id == document.PaymentId.Value && p.DocumentId == document.Id);
                    if (payment != null)
                    {
                        payment.DocumentId = null;
                        payment.LastModifiedBy = _userId;
                        payment.LastModifiedOn = DateTime.UtcNow;
                        _dbContext.Payments.Update(payment);
                    }
                }
            }
            await _dbContext.SaveChangesAsync();
        }

        #endregion

        #region Inspections
        
        public async Task<List<Inspection>> GetInspectionsAsync()
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            
            return await _dbContext.Inspections
                .Include(i => i.Property)
                .Include(i => i.Lease)
                    .ThenInclude(l => l!.Tenant)
                .Where(i => !i.IsDeleted && i.OrganizationId == organizationId)
                .OrderByDescending(i => i.CompletedOn)
                .ToListAsync();
        }

        public async Task<List<Inspection>> GetInspectionsByPropertyIdAsync(Guid propertyId)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            
            return await _dbContext.Inspections
                .Include(i => i.Property)
                .Include(i => i.Lease)
                    .ThenInclude(l => l!.Tenant)
                .Where(i => i.PropertyId == propertyId && !i.IsDeleted && i.OrganizationId == organizationId)
                .OrderByDescending(i => i.CompletedOn)
                .ToListAsync();
        }

        public async Task<Inspection?> GetInspectionByIdAsync(Guid inspectionId)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            
            return await _dbContext.Inspections
                .Include(i => i.Property)
                .Include(i => i.Lease)
                    .ThenInclude(l => l!.Tenant)
                .FirstOrDefaultAsync(i => i.Id == inspectionId && !i.IsDeleted && i.OrganizationId == organizationId);
        }

        public async Task AddInspectionAsync(Inspection inspection)
        {

            var _userId = await _userContext.GetUserIdAsync();
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            if (string.IsNullOrEmpty(_userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            inspection.Id = Guid.NewGuid();
            inspection.OrganizationId = organizationId!.Value;
            inspection.CreatedBy = _userId;
            inspection.CreatedOn = DateTime.UtcNow;
            await _dbContext.Inspections.AddAsync(inspection);
            await _dbContext.SaveChangesAsync();

            // Create calendar event for the inspection
            await _calendarEventService.CreateOrUpdateEventAsync(inspection);

            // Update property inspection tracking if this is a routine inspection
            if (inspection.InspectionType == "Routine")
            {
                // Find and update/delete the original property-based routine inspection calendar event
                var propertyBasedEvent = await _dbContext.CalendarEvents
                    .FirstOrDefaultAsync(e => 
                        e.PropertyId == inspection.PropertyId &&
                        e.SourceEntityType == "Property" &&
                        e.EventType == CalendarEventTypes.Inspection &&
                        !e.IsDeleted);

                if (propertyBasedEvent != null)
                {
                    // Remove the old property-based event since we now have an actual inspection record
                    _dbContext.CalendarEvents.Remove(propertyBasedEvent);
                    await _dbContext.SaveChangesAsync();
                }

                await UpdatePropertyInspectionTrackingAsync(
                    inspection.PropertyId, 
                    inspection.CompletedOn);
            }
        }

        public async Task UpdateInspectionAsync(Inspection inspection)
        {
            var _userId = await _userContext.GetUserIdAsync();
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            
            if (string.IsNullOrEmpty(_userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            // Security: Verify inspection belongs to active organization
            var existing = await _dbContext.Inspections
                .FirstOrDefaultAsync(i => i.Id == inspection.Id && i.OrganizationId == organizationId);

            if (existing == null)
            {
                throw new UnauthorizedAccessException($"Inspection {inspection.Id} not found in active organization.");
            }

            // Set tracking fields automatically
            inspection.LastModifiedBy = _userId;
            inspection.LastModifiedOn = DateTime.UtcNow;
            inspection.OrganizationId = organizationId!.Value; // Prevent org hijacking

            _dbContext.Entry(existing).CurrentValues.SetValues(inspection);
            await _dbContext.SaveChangesAsync();

            // Update calendar event
            await _calendarEventService.CreateOrUpdateEventAsync(inspection);
        }

        public async Task DeleteInspectionAsync(Guid inspectionId)
        {
            var userId = await _userContext.GetUserIdAsync();
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            if (string.IsNullOrEmpty(userId) || !organizationId.HasValue || organizationId == Guid.Empty)
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            var inspection = await _dbContext.Inspections.FindAsync(inspectionId);
            if (inspection != null && !inspection.IsDeleted)
            {
                if (_applicationSettings.SoftDeleteEnabled)
                {
                    inspection.IsDeleted = true;
                    inspection.LastModifiedOn = DateTime.UtcNow;
                    inspection.LastModifiedBy = userId;
                    _dbContext.Inspections.Update(inspection);
                }
                else
                {
                    _dbContext.Inspections.Remove(inspection);
                }
                await _dbContext.SaveChangesAsync();

                // Delete associated calendar event
                await _calendarEventService.DeleteEventAsync(inspection.CalendarEventId);
            }
        }

        #endregion

        #region Inspection Tracking

        /// <summary>
        /// Updates property inspection tracking after a routine inspection is completed
        /// </summary>
        public async Task UpdatePropertyInspectionTrackingAsync(Guid propertyId, DateTime inspectionDate, int intervalMonths = 12)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            var property = await _dbContext.Properties.FindAsync(propertyId);
            if (property == null || property.IsDeleted || property.OrganizationId != organizationId)
            {
                throw new InvalidOperationException("Property not found.");
            }

            property.LastRoutineInspectionDate = inspectionDate;
            property.NextRoutineInspectionDueDate = inspectionDate.AddMonths(intervalMonths);
            property.RoutineInspectionIntervalMonths = intervalMonths;
            property.LastModifiedOn = DateTime.UtcNow;
            
            var userId = await _userContext.GetUserIdAsync();
            property.LastModifiedBy = string.IsNullOrEmpty(userId) ? string.Empty : userId;

            _dbContext.Properties.Update(property);
            await _dbContext.SaveChangesAsync();
        }

        /// <summary>
        /// Gets properties with overdue routine inspections
        /// </summary>
        public async Task<List<Property>> GetPropertiesWithOverdueInspectionsAsync()
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            
            return await _dbContext.Properties
                .Where(p => p.OrganizationId == organizationId && 
                           !p.IsDeleted &&
                           p.NextRoutineInspectionDueDate.HasValue &&
                           p.NextRoutineInspectionDueDate.Value < DateTime.Today)
                .OrderBy(p => p.NextRoutineInspectionDueDate)
                .ToListAsync();
        }

        /// <summary>
        /// Gets properties with inspections due within specified days
        /// </summary>
        public async Task<List<Property>> GetPropertiesWithInspectionsDueSoonAsync(int daysAhead = 30)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            var dueDate = DateTime.Today.AddDays(daysAhead);
            
            return await _dbContext.Properties
                .Where(p => p.OrganizationId == organizationId && 
                           !p.IsDeleted &&
                           p.NextRoutineInspectionDueDate.HasValue &&
                           p.NextRoutineInspectionDueDate.Value >= DateTime.Today &&
                           p.NextRoutineInspectionDueDate.Value <= dueDate)
                .OrderBy(p => p.NextRoutineInspectionDueDate)
                .ToListAsync();
        }

        /// <summary>
        /// Gets count of properties with overdue inspections
        /// </summary>
        public async Task<int> GetOverdueInspectionCountAsync()
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            
            return await _dbContext.Properties
                .CountAsync(p => p.OrganizationId == organizationId && 
                                !p.IsDeleted &&
                                p.NextRoutineInspectionDueDate.HasValue &&
                                p.NextRoutineInspectionDueDate.Value < DateTime.Today);
        }

        /// <summary>
        /// Initializes inspection tracking for a property (sets first inspection due date)
        /// </summary>
        public async Task InitializePropertyInspectionTrackingAsync(Guid propertyId, int intervalMonths = 12)
        {
            var property = await _dbContext.Properties.FindAsync(propertyId);
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            if (property == null || property.IsDeleted || property.OrganizationId != organizationId)
            {
                throw new InvalidOperationException("Property not found.");
            }

            if (!property.NextRoutineInspectionDueDate.HasValue)
            {
                property.NextRoutineInspectionDueDate = DateTime.Today.AddMonths(intervalMonths);
                property.RoutineInspectionIntervalMonths = intervalMonths;
                property.LastModifiedOn = DateTime.UtcNow;
                
                var userId = await _userContext.GetUserIdAsync();
                property.LastModifiedBy = string.IsNullOrEmpty(userId) ? string.Empty : userId;

                _dbContext.Properties.Update(property);
                await _dbContext.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Creates a calendar event for a routine property inspection
        /// </summary>
        private async Task CreateRoutineInspectionCalendarEventAsync(Property property)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            if (property == null || property.IsDeleted || property.OrganizationId != organizationId)
            {
                throw new InvalidOperationException("Property not found.");
            }

            if (!property.NextRoutineInspectionDueDate.HasValue)
            {
                return;
            }

    
            var userId = await _userContext.GetUserIdAsync();

            var calendarEvent = new CalendarEvent
            {
                Id = Guid.NewGuid(),
                Title = $"Routine Inspection - {property.Address}",
                Description = $"Routine inspection due for property at {property.Address}, {property.City}, {property.State}",
                StartOn = property.NextRoutineInspectionDueDate.Value,
                DurationMinutes = 60, // Default 1 hour for inspection
                EventType = CalendarEventTypes.Inspection,
                Status = "Scheduled",
                PropertyId = property.Id,
                Location = $"{property.Address}, {property.City}, {property.State} {property.ZipCode}",
                Color = CalendarEventTypes.GetColor(CalendarEventTypes.Inspection),
                Icon = CalendarEventTypes.GetIcon(CalendarEventTypes.Inspection),
                OrganizationId = property.OrganizationId,
                CreatedBy = string.IsNullOrEmpty(userId) ? string.Empty : userId,
                CreatedOn = DateTime.UtcNow,
                SourceEntityType = "Property",
                SourceEntityId = property.Id
            };

            _dbContext.CalendarEvents.Add(calendarEvent);
            await _dbContext.SaveChangesAsync();
        }

        #endregion

        #region Maintenance Requests

        public async Task<List<MaintenanceRequest>> GetMaintenanceRequestsAsync()
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            return await _dbContext.MaintenanceRequests
                .Include(m => m.Property)
                .Include(m => m.Lease)
                .Where(m => m.OrganizationId == organizationId && !m.IsDeleted)
                .OrderByDescending(m => m.RequestedOn)
                .ToListAsync();
        }

        public async Task<List<MaintenanceRequest>> GetMaintenanceRequestsByPropertyAsync(Guid propertyId)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            return await _dbContext.MaintenanceRequests
                .Include(m => m.Property)
                .Include(m => m.Lease)
                .Where(m => m.PropertyId == propertyId && m.OrganizationId == organizationId && !m.IsDeleted)
                .OrderByDescending(m => m.RequestedOn)
                .ToListAsync();
        }

        public async Task<List<MaintenanceRequest>> GetMaintenanceRequestsByLeaseAsync(Guid leaseId)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            return await _dbContext.MaintenanceRequests
                .Include(m => m.Property)
                .Include(m => m.Lease)
                .Where(m => m.LeaseId == leaseId && m.OrganizationId == organizationId && !m.IsDeleted)
                .OrderByDescending(m => m.RequestedOn)
                .ToListAsync();
        }

        public async Task<List<MaintenanceRequest>> GetMaintenanceRequestsByStatusAsync(string status)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            return await _dbContext.MaintenanceRequests
                .Include(m => m.Property)
                .Include(m => m.Lease)
                .Where(m => m.Status == status && m.OrganizationId == organizationId && !m.IsDeleted)
                .OrderByDescending(m => m.RequestedOn)
                .ToListAsync();
        }

        public async Task<List<MaintenanceRequest>> GetMaintenanceRequestsByPriorityAsync(string priority)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            return await _dbContext.MaintenanceRequests
                .Include(m => m.Property)
                .Include(m => m.Lease)
                .Where(m => m.Priority == priority && m.OrganizationId == organizationId && !m.IsDeleted)
                .OrderByDescending(m => m.RequestedOn)
                .ToListAsync();
        }

        public async Task<List<MaintenanceRequest>> GetOverdueMaintenanceRequestsAsync()
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            var today = DateTime.Today;

            return await _dbContext.MaintenanceRequests
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

        public async Task<int> GetOpenMaintenanceRequestCountAsync()
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            return await _dbContext.MaintenanceRequests
                .Where(m => m.OrganizationId == organizationId && 
                           !m.IsDeleted &&
                           m.Status != "Completed" &&
                           m.Status != "Cancelled")
                .CountAsync();
        }

        public async Task<int> GetUrgentMaintenanceRequestCountAsync()
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            return await _dbContext.MaintenanceRequests
                .Where(m => m.OrganizationId == organizationId && 
                           !m.IsDeleted &&
                           m.Priority == "Urgent" &&
                           m.Status != "Completed" &&
                           m.Status != "Cancelled")
                .CountAsync();
        }

        public async Task<MaintenanceRequest?> GetMaintenanceRequestByIdAsync(Guid id)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            return await _dbContext.MaintenanceRequests
                .Include(m => m.Property)
                .Include(m => m.Lease)
                .FirstOrDefaultAsync(m => m.Id == id && m.OrganizationId == organizationId && !m.IsDeleted);
        }

        public async Task AddMaintenanceRequestAsync(MaintenanceRequest maintenanceRequest)
        {
            var _userId = await _userContext.GetUserIdAsync();

            if (string.IsNullOrEmpty(_userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            // Set tracking fields automatically
            maintenanceRequest.Id = Guid.NewGuid();
            maintenanceRequest.OrganizationId = organizationId!.Value;
            maintenanceRequest.CreatedBy = _userId;
            maintenanceRequest.CreatedOn = DateTime.UtcNow;

            await _dbContext.MaintenanceRequests.AddAsync(maintenanceRequest);
            await _dbContext.SaveChangesAsync();

            // Create calendar event for the maintenance request
            await _calendarEventService.CreateOrUpdateEventAsync(maintenanceRequest);
        }

        public async Task UpdateMaintenanceRequestAsync(MaintenanceRequest maintenanceRequest)
        {
            var _userId = await _userContext.GetUserIdAsync();

            if (string.IsNullOrEmpty(_userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            // Security: Verify maintenance request belongs to active organization
            var existing = await _dbContext.MaintenanceRequests
                .FirstOrDefaultAsync(m => m.Id == maintenanceRequest.Id && m.OrganizationId == organizationId);

            if (existing == null)
            {
                throw new UnauthorizedAccessException($"Maintenance request {maintenanceRequest.Id} not found in active organization.");
            }

            // Set tracking fields automatically
            maintenanceRequest.LastModifiedBy = _userId;
            maintenanceRequest.LastModifiedOn = DateTime.UtcNow;
            maintenanceRequest.OrganizationId = organizationId!.Value; // Prevent org hijacking

            _dbContext.Entry(existing).CurrentValues.SetValues(maintenanceRequest);
            await _dbContext.SaveChangesAsync();

            // Update calendar event
            await _calendarEventService.CreateOrUpdateEventAsync(maintenanceRequest);
        }

        public async Task DeleteMaintenanceRequestAsync(Guid id)
        {
            var _userId = await _userContext.GetUserIdAsync();

            if (string.IsNullOrEmpty(_userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            var maintenanceRequest = await _dbContext.MaintenanceRequests
                .FirstOrDefaultAsync(m => m.Id == id && m.OrganizationId == organizationId);

            if (maintenanceRequest != null)
            {
                maintenanceRequest.IsDeleted = true;
                maintenanceRequest.LastModifiedOn = DateTime.Now;
                maintenanceRequest.LastModifiedBy = _userId;

                _dbContext.MaintenanceRequests.Update(maintenanceRequest);
                await _dbContext.SaveChangesAsync();

                // Delete associated calendar event
                await _calendarEventService.DeleteEventAsync(maintenanceRequest.CalendarEventId);
            }
        }

        public async Task UpdateMaintenanceRequestStatusAsync(Guid id, string status)
        {
            var _userId = await _userContext.GetUserIdAsync();

            if (string.IsNullOrEmpty(_userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            var maintenanceRequest = await _dbContext.MaintenanceRequests
                .FirstOrDefaultAsync(m => m.Id == id && m.OrganizationId == organizationId && !m.IsDeleted);

            if (maintenanceRequest != null)
            {
                maintenanceRequest.Status = status;
                maintenanceRequest.LastModifiedOn = DateTime.Now;
                maintenanceRequest.LastModifiedBy = _userId;

                if (status == "Completed")
                {
                    maintenanceRequest.CompletedOn = DateTime.Today;
                }

                _dbContext.MaintenanceRequests.Update(maintenanceRequest);
                await _dbContext.SaveChangesAsync();
            }
        }

        #endregion

        #region Organization Settings

        /// <summary>
        /// Gets the organization settings for the current user's organization.
        /// If no settings exist, creates default settings. 
        /// </summary>
        public async Task<OrganizationSettings?> GetOrganizationSettingsAsync()
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            if (!organizationId.HasValue || organizationId == Guid.Empty)
            {
                throw new InvalidOperationException("Organization ID not found for current user");
            }

            var settings = await _dbContext.OrganizationSettings
                .Where(s => !s.IsDeleted && s.OrganizationId == organizationId)
                .FirstOrDefaultAsync();

            // Create default settings if they don't exist
            if (settings == null)
            {
                var userId = await _userContext.GetUserIdAsync();
                settings = new OrganizationSettings
                {
                    OrganizationId = organizationId.Value, // This should be set to the actual organization ID
                    LateFeeEnabled = true,
                    LateFeeAutoApply = true,
                    LateFeeGracePeriodDays = 3,
                    LateFeePercentage = 0.05m,
                    MaxLateFeeAmount = 50.00m,
                    PaymentReminderEnabled = true,
                    PaymentReminderDaysBefore = 3,
                    CreatedOn = DateTime.UtcNow,
                    CreatedBy = string.IsNullOrEmpty(userId) ? string.Empty : userId
                };
                
                await _dbContext.OrganizationSettings.AddAsync(settings);
                await _dbContext.SaveChangesAsync();
            }

            return settings;
        }

        public async Task<OrganizationSettings?> GetOrganizationSettingsByOrgIdAsync(Guid organizationId)
        {
            var settings = await _dbContext.OrganizationSettings
                .Where(s => !s.IsDeleted && s.OrganizationId == organizationId)
                .FirstOrDefaultAsync();

            return settings;
        }

        /// <summary>
        /// Updates the organization settings for the current user's organization.
        /// </summary>
        public async Task UpdateOrganizationSettingsAsync(OrganizationSettings settings)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            if (!organizationId.HasValue || organizationId == Guid.Empty)
            {
                throw new InvalidOperationException("Organization ID not found for current user");
            }
            if (settings.OrganizationId != organizationId.Value)
            {
                throw new InvalidOperationException("Cannot update settings for a different organization");
            }
            var userId = await _userContext.GetUserIdAsync();
            
            settings.LastModifiedOn = DateTime.UtcNow;
            settings.LastModifiedBy = string.IsNullOrEmpty(userId) ? string.Empty : userId;
            _dbContext.OrganizationSettings.Update(settings);
            await _dbContext.SaveChangesAsync();
        }

        #endregion

        #region PreLeaseOperations

        #region ProspectiveTenant CRUD

        public async Task<List<ProspectiveTenant>> GetAllProspectiveTenantsAsync()
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            return await _dbContext.ProspectiveTenants
                .Where(pt => pt.OrganizationId == organizationId && !pt.IsDeleted)
                .Include(pt => pt.InterestedProperty)
                .Include(pt => pt.Tours)
                .Include(pt => pt.Applications)
                .OrderByDescending(pt => pt.CreatedOn)
                .ToListAsync();
        }

        public async Task<ProspectiveTenant?> GetProspectiveTenantByIdAsync(Guid id)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            return await _dbContext.ProspectiveTenants
                .Where(pt => pt.Id == id && pt.OrganizationId == organizationId && !pt.IsDeleted)
                .Include(pt => pt.InterestedProperty)
                .Include(pt => pt.Tours)
                .Include(pt => pt.Applications)
                .FirstOrDefaultAsync();
        }

        public async Task<ProspectiveTenant> CreateProspectiveTenantAsync(ProspectiveTenant prospectiveTenant)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            var userId = await _userContext.GetUserIdAsync();

            prospectiveTenant.Id = Guid.NewGuid();
            prospectiveTenant.OrganizationId = organizationId!.Value;
            prospectiveTenant.CreatedBy = string.IsNullOrEmpty(userId) ? string.Empty : userId;
            prospectiveTenant.CreatedOn = DateTime.UtcNow;
            prospectiveTenant.Status = ApplicationConstants.ProspectiveStatuses.Lead;
            prospectiveTenant.FirstContactedOn = DateTime.UtcNow;

            _dbContext.ProspectiveTenants.Add(prospectiveTenant);
            await _dbContext.SaveChangesAsync();
            return prospectiveTenant;
        }

        public async Task<ProspectiveTenant> UpdateProspectiveTenantAsync(ProspectiveTenant prospectiveTenant)
        {
            var userId = await _userContext.GetUserIdAsync();
            
            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            
            // Security: Verify prospective tenant belongs to active organization
            var existing = await _dbContext.ProspectiveTenants
                .FirstOrDefaultAsync(p => p.Id == prospectiveTenant.Id && p.OrganizationId == organizationId);

            if (existing == null)
            {
                throw new UnauthorizedAccessException($"Prospective tenant {prospectiveTenant.Id} not found in active organization.");
            }

            // Set tracking fields automatically
            prospectiveTenant.LastModifiedOn = DateTime.UtcNow;
            prospectiveTenant.LastModifiedBy = userId;
            prospectiveTenant.OrganizationId = organizationId!.Value; // Prevent org hijacking

            _dbContext.Entry(existing).CurrentValues.SetValues(prospectiveTenant);
            await _dbContext.SaveChangesAsync();
            return prospectiveTenant;
        }

        public async Task DeleteProspectiveTenantAsync(Guid id)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            var userId = await _userContext.GetUserIdAsync();
            var prospectiveTenant = await GetProspectiveTenantByIdAsync(id);
            
            if(prospectiveTenant == null)
            {
                throw new InvalidOperationException("Prospective tenant not found.");
            }

            if (prospectiveTenant.OrganizationId != organizationId)
            {
                throw new UnauthorizedAccessException("User is not authorized to delete this prospective tenant.");
            }
                prospectiveTenant.IsDeleted = true;
                prospectiveTenant.LastModifiedOn = DateTime.UtcNow;
                prospectiveTenant.LastModifiedBy = string.IsNullOrEmpty(userId) ? string.Empty : userId;
                await _dbContext.SaveChangesAsync();
        }

        #endregion

        #region Tour CRUD

        public async Task<List<Tour>> GetAllToursAsync()
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            return await _dbContext.Tours
                .Where(s => s.OrganizationId == organizationId && !s.IsDeleted)
                .Include(s => s.ProspectiveTenant)
                .Include(s => s.Property)
                .Include(s => s.Checklist)
            .OrderBy(s => s.ScheduledOn)
            .ToListAsync();
        }

        public async Task<List<Tour>> GetToursByProspectiveIdAsync(Guid prospectiveTenantId)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            return await _dbContext.Tours
                .Where(s => s.ProspectiveTenantId == prospectiveTenantId && s.OrganizationId == organizationId && !s.IsDeleted)
                .Include(s => s.ProspectiveTenant)
                .Include(s => s.Property)
                .Include(s => s.Checklist)
                .OrderBy(s => s.ScheduledOn)
                .ToListAsync();
        }

        public async Task<Tour?> GetTourByIdAsync(Guid id)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            return await _dbContext.Tours
                .Where(s => s.Id == id && s.OrganizationId == organizationId && !s.IsDeleted)
                .Include(s => s.ProspectiveTenant)
                .Include(s => s.Property)
                .Include(s => s.Checklist)
                .FirstOrDefaultAsync();
        }

        public async Task<Tour> CreateTourAsync(Tour tour, Guid? templateId = null)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            var userId = await _userContext.GetUserIdAsync();

            tour.Id = Guid.NewGuid();
            tour.OrganizationId = organizationId!.Value;
            tour.CreatedBy = string.IsNullOrEmpty(userId) ? string.Empty : userId;
            tour.CreatedOn = DateTime.UtcNow;
            tour.Status = ApplicationConstants.TourStatuses.Scheduled;

            // Get prospect information for checklist
            var prospective = await _dbContext.ProspectiveTenants
                .Include(p => p.InterestedProperty)
                .FirstOrDefaultAsync(p => p.Id == tour.ProspectiveTenantId);

            // Find the specified template, or fall back to default "Property Tour" template
            ChecklistTemplate? tourTemplate = null;
            
            if (templateId.HasValue)
            {
                // Use the specified template
                tourTemplate = await _dbContext.ChecklistTemplates
                    .FirstOrDefaultAsync(t => t.Id == templateId.Value && 
                        (t.OrganizationId == tour.OrganizationId || t.IsSystemTemplate) && 
                        !t.IsDeleted);
            }
            
            // Fall back to default "Property Tour" template if not specified or not found
            if (tourTemplate == null)
            {
                tourTemplate = await _dbContext.ChecklistTemplates
                    .FirstOrDefaultAsync(t => t.Name == "Property Tour" && 
                        (t.OrganizationId == tour.OrganizationId || t.IsSystemTemplate) && 
                        !t.IsDeleted);
            }

            if (tourTemplate != null && prospective != null)
            {
                // Create checklist from template
                var checklist = await _checklistService.CreateChecklistFromTemplateAsync(tourTemplate.Id);
                
                // Customize checklist with prospect information
                checklist.Name = $"Property Tour - {prospective.FullName}";
                checklist.PropertyId = tour.PropertyId;
                checklist.GeneralNotes = $"Prospect: {prospective.FullName}\n" +
                                        $"Email: {prospective.Email}\n" +
                                        $"Phone: {prospective.Phone}\n" +
                                    $"Scheduled: {tour.ScheduledOn:MMM dd, yyyy h:mm tt}";
                
                // Link tour to checklist
                tour.ChecklistId = checklist.Id;
            }

            _dbContext.Tours.Add(tour);
            await _dbContext.SaveChangesAsync();

            // Create calendar event for the tour
            await _calendarEventService.CreateOrUpdateEventAsync(tour);

            // Update ProspectiveTenant status
            if (prospective != null && prospective.Status == ApplicationConstants.ProspectiveStatuses.Lead)
            {
                prospective.Status = ApplicationConstants.ProspectiveStatuses.TourScheduled;
                prospective.LastModifiedOn = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
            }

            return tour;
        }

        public async Task<Tour> UpdateTourAsync(Tour tour)
        {
            var userId = await _userContext.GetUserIdAsync();
            
            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            
            // Security: Verify tour belongs to active organization
            var existing = await _dbContext.Tours
                .FirstOrDefaultAsync(t => t.Id == tour.Id && t.OrganizationId == organizationId);

            if (existing == null)
            {
                throw new UnauthorizedAccessException($"Tour {tour.Id} not found in active organization.");
            }

            // Set tracking fields automatically
            tour.LastModifiedBy = userId;
            tour.LastModifiedOn = DateTime.UtcNow;
            tour.OrganizationId = organizationId!.Value; // Prevent org hijacking

            _dbContext.Entry(existing).CurrentValues.SetValues(tour);
            await _dbContext.SaveChangesAsync();

            // Update calendar event
            await _calendarEventService.CreateOrUpdateEventAsync(tour);

            return tour;
        }

        public async Task DeleteTourAsync(Guid id)
        {
            var userId = await _userContext.GetUserIdAsync();
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            if(string.IsNullOrEmpty(userId) || !organizationId.HasValue || organizationId == Guid.Empty)
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }
            

            var tour = await GetTourByIdAsync(id);

            if(tour == null)
            {
                throw new InvalidOperationException("Tour not found.");
            }
            
            if (tour.OrganizationId != organizationId)
            {
                throw new UnauthorizedAccessException("User is not authorized to delete this tour.");
            }

                tour.IsDeleted = true;
                tour.LastModifiedOn = DateTime.UtcNow;
                tour.LastModifiedBy = string.IsNullOrEmpty(userId) ? string.Empty : userId;
                await _dbContext.SaveChangesAsync();

                // Delete associated calendar event
                await _calendarEventService.DeleteEventAsync(tour.CalendarEventId);
        }

        public async Task<bool> CancelTourAsync(Guid tourId)
        {
            var userId = await _userContext.GetUserIdAsync();
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            var tour = await GetTourByIdAsync(tourId);

            if(tour == null)
            {
                throw new InvalidOperationException("Tour not found.");
            }

            if(string.IsNullOrEmpty(userId) || !organizationId.HasValue || tour.OrganizationId != organizationId.Value)
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            // Update tour status to cancelled
            tour.Status = ApplicationConstants.TourStatuses.Cancelled;
            tour.LastModifiedOn = DateTime.UtcNow;
            tour.LastModifiedBy = string.IsNullOrEmpty(userId) ? string.Empty : userId;
            await _dbContext.SaveChangesAsync();

            // Update calendar event status
            await _calendarEventService.CreateOrUpdateEventAsync(tour);

            // Check if prospect has any other scheduled tours
            var prospective = await _dbContext.ProspectiveTenants.FindAsync(tour.ProspectiveTenantId);
            if (prospective != null && prospective.Status == ApplicationConstants.ProspectiveStatuses.TourScheduled)
            {
                var hasOtherScheduledTours = await _dbContext.Tours
                    .AnyAsync(s => s.ProspectiveTenantId == tour.ProspectiveTenantId
                        && s.Id != tourId
                        && !s.IsDeleted
                        && s.Status == ApplicationConstants.TourStatuses.Scheduled);

                // If no other scheduled tours, revert prospect status to Lead
                if (!hasOtherScheduledTours)
                {
                    prospective.Status = ApplicationConstants.ProspectiveStatuses.Lead;
                    prospective.LastModifiedOn = DateTime.UtcNow;
                    prospective.LastModifiedBy = string.IsNullOrEmpty(userId) ? string.Empty : userId;
                    await _dbContext.SaveChangesAsync();
                }
            }

            return true;
        }

        public async Task<bool> CompleteTourAsync(Guid tourId, string? feedback = null, string? interestLevel = null)
        {
            var userId = await _userContext.GetUserIdAsync();
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            var tour = await GetTourByIdAsync(tourId);
            if (tour == null) return false;

            if(string.IsNullOrEmpty(userId) || !organizationId.HasValue || tour.OrganizationId != organizationId.Value)
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            // Update tour status and feedback
            tour.Status = ApplicationConstants.TourStatuses.Completed;
            tour.Feedback = feedback;
            tour.InterestLevel = interestLevel;
            tour.ConductedBy = string.IsNullOrEmpty(userId) ? string.Empty : userId;
            tour.LastModifiedOn = DateTime.UtcNow;
            tour.LastModifiedBy = string.IsNullOrEmpty(userId) ? string.Empty : userId;

            // Update calendar event status
            if (tour.CalendarEventId.HasValue)
            {
                var calendarEvent = await _dbContext.CalendarEvents
                    .FirstOrDefaultAsync(e => e.Id == tour.CalendarEventId.Value);
                if (calendarEvent != null)
                {
                    calendarEvent.Status = ApplicationConstants.TourStatuses.Completed;
                    calendarEvent.LastModifiedBy = string.IsNullOrEmpty(userId) ? string.Empty : userId;
                    calendarEvent.LastModifiedOn = DateTime.UtcNow;
                }
            }

            await _dbContext.SaveChangesAsync();

            return true;
        }

        public async Task<bool> MarkTourAsNoShowAsync(Guid tourId)
        {
            var userId = await _userContext.GetUserIdAsync();
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            
            var tour = await GetTourByIdAsync(tourId);
            if (tour == null) return false;

            if(string.IsNullOrEmpty(userId) || !organizationId.HasValue || tour.OrganizationId != organizationId.Value)
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            // Update tour status to NoShow
            tour.Status = ApplicationConstants.TourStatuses.NoShow;
            tour.LastModifiedOn = DateTime.UtcNow;
            tour.LastModifiedBy = string.IsNullOrEmpty(userId) ? string.Empty : userId;
            // Update calendar event status
            if (tour.CalendarEventId.HasValue)
            {
                var calendarEvent = await _dbContext.CalendarEvents
                    .FirstOrDefaultAsync(e => e.Id == tour.CalendarEventId.Value);
                if (calendarEvent != null)
                {
                    calendarEvent.Status = ApplicationConstants.TourStatuses.NoShow;
                    calendarEvent.LastModifiedBy = string.IsNullOrEmpty(userId) ? string.Empty : userId;
                    calendarEvent.LastModifiedOn = DateTime.UtcNow;
                }
            }

            await _dbContext.SaveChangesAsync();
            return true;
        }

        #endregion

        #region RentalApplication CRUD

        public async Task<List<RentalApplication>> GetAllRentalApplicationsAsync()
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            return await _dbContext.RentalApplications
                .Where(ra => ra.OrganizationId == organizationId && !ra.IsDeleted)
                .Include(ra => ra.ProspectiveTenant)
                .Include(ra => ra.Property)
                .Include(ra => ra.Screening)
                .OrderByDescending(ra => ra.AppliedOn)
                .ToListAsync();
        }

        public async Task<RentalApplication?> GetRentalApplicationByIdAsync(Guid id)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            return await _dbContext.RentalApplications
                .Where(ra => ra.Id == id && ra.OrganizationId == organizationId && !ra.IsDeleted)
                .Include(ra => ra.ProspectiveTenant)
                .Include(ra => ra.Property)
                .Include(ra => ra.Screening)
                .FirstOrDefaultAsync();
        }

        public async Task<RentalApplication?> GetApplicationByProspectiveIdAsync(Guid prospectiveTenantId)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            return await _dbContext.RentalApplications
                .Where(ra => ra.ProspectiveTenantId == prospectiveTenantId && ra.OrganizationId == organizationId && !ra.IsDeleted)
                .Include(ra => ra.Property)
                .Include(ra => ra.Screening)
                .FirstOrDefaultAsync();
        }

        public async Task<RentalApplication> CreateRentalApplicationAsync(RentalApplication application)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            var userId = await _userContext.GetUserIdAsync();

            application.Id = Guid.NewGuid();
            application.OrganizationId = organizationId!.Value;
            application.CreatedBy = string.IsNullOrEmpty(userId) ? string.Empty : userId;
            application.CreatedOn = DateTime.UtcNow;
            application.AppliedOn = DateTime.UtcNow;
            application.Status = ApplicationConstants.ApplicationStatuses.Submitted;

            // Get organization settings for fee and expiration defaults
            var orgSettings = await _dbContext.OrganizationSettings
                .FirstOrDefaultAsync(s => s.OrganizationId == application.OrganizationId && !s.IsDeleted);

            if (orgSettings != null)
            {
                // Set application fee if not already set and fees are enabled
                if (orgSettings.ApplicationFeeEnabled && application.ApplicationFee == 0)
                {
                    application.ApplicationFee = orgSettings.DefaultApplicationFee;
                }

                // Set expiration date if not already set
                if (application.ExpiresOn == null)
                {
                    application.ExpiresOn = application.AppliedOn.AddDays(orgSettings.ApplicationExpirationDays);
                }
            }
            else
            {
                // Fallback defaults if no settings found
                if (application.ApplicationFee == 0)
                {
                    application.ApplicationFee = 50.00m; // Default fee
                }
                if (application.ExpiresOn == null)
                {
                    application.ExpiresOn = application.AppliedOn.AddDays(30); // Default 30 days
                }
            }

            _dbContext.RentalApplications.Add(application);
            await _dbContext.SaveChangesAsync();

            // Update property status to ApplicationPending
            var property = await _dbContext.Properties.FindAsync(application.PropertyId);
            if (property != null && property.Status == ApplicationConstants.PropertyStatuses.Available)
            {
                property.Status = ApplicationConstants.PropertyStatuses.ApplicationPending;
                property.LastModifiedOn = DateTime.UtcNow;
                property.LastModifiedBy = application.CreatedBy;
                await _dbContext.SaveChangesAsync();
            }

            // Update ProspectiveTenant status
            var prospective = await _dbContext.ProspectiveTenants.FindAsync(application.ProspectiveTenantId);
            if (prospective != null)
            {
                prospective.Status = ApplicationConstants.ProspectiveStatuses.Applied;
                prospective.LastModifiedOn = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
            }

            return application;
        }

        public async Task<RentalApplication> UpdateRentalApplicationAsync(RentalApplication application)
        {
            var userId = await _userContext.GetUserIdAsync();
            
            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            // Security: Verify rental application belongs to active organization
            var existing = await _dbContext.RentalApplications
                .FirstOrDefaultAsync(r => r.Id == application.Id && r.OrganizationId == organizationId);

            if (existing == null)
            {
                throw new UnauthorizedAccessException($"Rental application {application.Id} not found in active organization.");
            }

            // Set tracking fields automatically
            application.LastModifiedBy = userId;
            application.LastModifiedOn = DateTime.UtcNow;
            application.OrganizationId = organizationId!.Value; // Prevent org hijacking

            _dbContext.Entry(existing).CurrentValues.SetValues(application);
            await _dbContext.SaveChangesAsync();
            return application;
        }

        public async Task DeleteRentalApplicationAsync(Guid id)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            var userId = await _userContext.GetUserIdAsync();

            var application = await GetRentalApplicationByIdAsync(id);

            if(application == null)
            {
                throw new InvalidOperationException("Rental application not found.");
            }

            if (application.OrganizationId != organizationId)
            {
                throw new UnauthorizedAccessException("User is not authorized to delete this rental application.");
            }
                application.IsDeleted = true;
                application.LastModifiedOn = DateTime.UtcNow;
                application.LastModifiedBy = string.IsNullOrEmpty(userId) ? string.Empty : userId;
                await _dbContext.SaveChangesAsync();
        }

        #endregion

        #region ApplicationScreening CRUD

        public async Task<ApplicationScreening?> GetScreeningByApplicationIdAsync(Guid rentalApplicationId)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            return await _dbContext.ApplicationScreenings
                .Where(asc => asc.RentalApplicationId == rentalApplicationId && asc.OrganizationId == organizationId && !asc.IsDeleted)
                .Include(asc => asc.RentalApplication)
                .FirstOrDefaultAsync();
        }

        public async Task<ApplicationScreening> CreateScreeningAsync(ApplicationScreening screening)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            var userId = await _userContext.GetUserIdAsync();
            
            screening.Id = Guid.NewGuid();
            screening.OrganizationId = organizationId!.Value;
            screening.CreatedBy = string.IsNullOrEmpty(userId) ? string.Empty : userId;
            screening.CreatedOn = DateTime.UtcNow;
            screening.OverallResult = ApplicationConstants.ScreeningResults.Pending;

            _dbContext.ApplicationScreenings.Add(screening);
            await _dbContext.SaveChangesAsync();

            // Update application and prospective tenant status
            var application = await _dbContext.RentalApplications.FindAsync(screening.RentalApplicationId);
            if (application != null)
            {
                application.Status = ApplicationConstants.ApplicationStatuses.Screening;
                application.LastModifiedOn = DateTime.UtcNow;

                var prospective = await _dbContext.ProspectiveTenants.FindAsync(application.ProspectiveTenantId);
                if (prospective != null)
                {
                    prospective.Status = ApplicationConstants.ProspectiveStatuses.Screening;
                    prospective.LastModifiedOn = DateTime.UtcNow;
                }

                await _dbContext.SaveChangesAsync();
            }

            return screening;
        }

        public async Task<ApplicationScreening> UpdateScreeningAsync(ApplicationScreening screening)
        {
            var userId = await _userContext.GetUserIdAsync();
            
            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            // Security: Verify screening belongs to active organization
            var existing = await _dbContext.ApplicationScreenings
                .FirstOrDefaultAsync(s => s.Id == screening.Id && s.OrganizationId == organizationId);

            if (existing == null)
            {
                throw new UnauthorizedAccessException($"Application screening {screening.Id} not found in active organization.");
            }

            // Set tracking fields automatically
            screening.LastModifiedOn = DateTime.UtcNow;
            screening.LastModifiedBy = userId;
            screening.OrganizationId = organizationId!.Value; // Prevent org hijacking

            _dbContext.Entry(existing).CurrentValues.SetValues(screening);
            await _dbContext.SaveChangesAsync();
            return screening;
        }

        #endregion

        #region Business Logic

        public async Task<bool> ApproveApplicationAsync(Guid applicationId)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            var userId = await _userContext.GetUserIdAsync() ?? string.Empty;
            
            var application = await GetRentalApplicationByIdAsync(applicationId);
            if (application == null) return false;

            if (application.OrganizationId != organizationId)
            {
                throw new UnauthorizedAccessException("User is not authorized to approve this rental application.");
            }

            application.Status = ApplicationConstants.ApplicationStatuses.Approved;
            application.DecidedOn = DateTime.UtcNow;
            application.DecisionBy = userId;
            application.LastModifiedOn = DateTime.UtcNow;
            application.LastModifiedBy = userId;

            _dbContext.RentalApplications.Update(application);

            var prospective = await _dbContext.ProspectiveTenants.FindAsync(application.ProspectiveTenantId);
            if (prospective != null)
            {
                prospective.Status = ApplicationConstants.ProspectiveStatuses.Approved;
                prospective.LastModifiedOn = DateTime.UtcNow;
                prospective.LastModifiedBy = userId;
                _dbContext.ProspectiveTenants.Update(prospective);
            }

            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DenyApplicationAsync(Guid applicationId, string reason)
        {
            var userId = await _userContext.GetUserIdAsync() ?? string.Empty;
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            var application = await GetRentalApplicationByIdAsync(applicationId);
            if (application == null) return false;
            if (application.OrganizationId != organizationId)
            {
                throw new UnauthorizedAccessException("User is not authorized to deny this rental application.");
            }
            application.Status = ApplicationConstants.ApplicationStatuses.Denied;
            application.DecidedOn = DateTime.UtcNow;
            application.DecisionBy = userId;
            application.DenialReason = reason;
            application.LastModifiedOn = DateTime.UtcNow;
            application.LastModifiedBy = userId;

            var prospective = await _dbContext.ProspectiveTenants.FindAsync(application.ProspectiveTenantId);
            if (prospective != null)
            {
                prospective.Status = ApplicationConstants.ProspectiveStatuses.Denied;
                prospective.LastModifiedOn = DateTime.UtcNow;
                prospective.LastModifiedBy = userId;
            }

            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> WithdrawApplicationAsync(Guid applicationId, string? reason = null)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            var userId = await _userContext.GetUserIdAsync() ?? string.Empty;

            var application = await GetRentalApplicationByIdAsync(applicationId);
            if (application == null) return false;

            if (application.OrganizationId != organizationId)
            {
                throw new UnauthorizedAccessException("User is not authorized to withdraw this rental application.");
            }

            application.Status = ApplicationConstants.ApplicationStatuses.Withdrawn;
            application.DecidedOn = DateTime.UtcNow;
            application.DecisionBy = userId;
            application.DenialReason = reason; // Reusing this field for withdrawal reason
            application.LastModifiedOn = DateTime.UtcNow;
            application.LastModifiedBy = userId;

            var prospective = await _dbContext.ProspectiveTenants.FindAsync(application.ProspectiveTenantId);


            if (prospective != null)
            {
                prospective.Status = ApplicationConstants.ProspectiveStatuses.Withdrawn;
                prospective.LastModifiedOn = DateTime.UtcNow;
                prospective.LastModifiedBy = userId;
            }

            // If there's a lease offer, mark it as withdrawn too
            var leaseOffer = await GetLeaseOfferByApplicationIdAsync(applicationId);
            if (leaseOffer != null)
            {
                leaseOffer.Status = "Withdrawn";
                leaseOffer.RespondedOn = DateTime.UtcNow;
                leaseOffer.ResponseNotes = reason ?? "Application withdrawn";
                leaseOffer.LastModifiedOn = DateTime.UtcNow;
                leaseOffer.LastModifiedBy = userId;
            }

            // Update property status back to available if it was in lease pending
            var property = await _dbContext.Properties.FindAsync(application.PropertyId);
            if (property != null && property.Status == ApplicationConstants.PropertyStatuses.LeasePending)
            {
                property.Status = ApplicationConstants.PropertyStatuses.Available;
                property.LastModifiedOn = DateTime.UtcNow;
                property.LastModifiedBy = userId;
            }

            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<List<ProspectiveTenant>> GetProspectivesByStatusAsync(string status)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            return await _dbContext.ProspectiveTenants
                .Where(pt => pt.Status == status && pt.OrganizationId == organizationId && !pt.IsDeleted)
                .Include(pt => pt.InterestedProperty)
                .OrderByDescending(pt => pt.CreatedOn)
                .ToListAsync();
        }

        public async Task<List<Tour>> GetUpcomingToursAsync(int days = 7)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            var startDate = DateTime.UtcNow;
            var endDate = startDate.AddDays(days);

            return await _dbContext.Tours
                .Where(s => s.OrganizationId == organizationId 
                    && !s.IsDeleted 
                    && s.Status == ApplicationConstants.TourStatuses.Scheduled
                && s.ScheduledOn >= startDate 
                && s.ScheduledOn <= endDate)
            .Include(s => s.ProspectiveTenant)
            .Include(s => s.Property)
            .Include(s => s.Checklist)
            .OrderBy(s => s.ScheduledOn)
            .ToListAsync();
        }

        public async Task<List<RentalApplication>> GetPendingApplicationsAsync()
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            return await _dbContext.RentalApplications
                .Where(ra => ra.OrganizationId == organizationId 
                    && !ra.IsDeleted 
                    && (ra.Status == ApplicationConstants.ApplicationStatuses.Submitted 
                        || ra.Status == ApplicationConstants.ApplicationStatuses.UnderReview
                        || ra.Status == ApplicationConstants.ApplicationStatuses.Screening))
                .Include(ra => ra.ProspectiveTenant)
                .Include(ra => ra.Property)
                .Include(ra => ra.Screening)
                .OrderBy(ra => ra.AppliedOn)
                .ToListAsync();
        }

        #endregion

        #region Lease Offers

        public async Task<LeaseOffer?> CreateLeaseOfferAsync(LeaseOffer leaseOffer)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            var userId = await _userContext.GetUserIdAsync();

            leaseOffer.Id = Guid.NewGuid();
            leaseOffer.OrganizationId = organizationId!.Value;
            leaseOffer.CreatedBy = string.IsNullOrEmpty(userId) ? string.Empty : userId;
            leaseOffer.CreatedOn = DateTime.UtcNow;
            _dbContext.LeaseOffers.Add(leaseOffer);
            await _dbContext.SaveChangesAsync();
            return leaseOffer;
        }

        public async Task<LeaseOffer?> GetLeaseOfferByIdAsync(Guid leaseOfferId)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            return await _dbContext.LeaseOffers
                .Include(lo => lo.RentalApplication)
                .Include(lo => lo.Property)
                .Include(lo => lo.ProspectiveTenant)
                .FirstOrDefaultAsync(lo => lo.Id == leaseOfferId && lo.OrganizationId == organizationId && !lo.IsDeleted);
        }

        public async Task<LeaseOffer?> GetLeaseOfferByApplicationIdAsync(Guid applicationId)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            return await _dbContext.LeaseOffers
                .Include(lo => lo.RentalApplication)
                .Include(lo => lo.Property)
                .Include(lo => lo.ProspectiveTenant)
                .FirstOrDefaultAsync(lo => lo.RentalApplicationId == applicationId && lo.OrganizationId == organizationId && !lo.IsDeleted);
        }

        public async Task<List<LeaseOffer>> GetLeaseOffersByPropertyIdAsync(Guid propertyId)
        {
            var organizationId = await _userContext.GetActiveOrganizationIdAsync();
            return await _dbContext.LeaseOffers
                .Include(lo => lo.RentalApplication)
                .Include(lo => lo.Property)
                .Include(lo => lo.ProspectiveTenant)
                .Where(lo => lo.PropertyId == propertyId && lo.OrganizationId == organizationId && !lo.IsDeleted)
                .OrderByDescending(lo => lo.OfferedOn)
                .ToListAsync();
        }

        public async Task<LeaseOffer> UpdateLeaseOfferAsync(LeaseOffer leaseOffer)
        {
            var userId = await _userContext.GetUserIdAsync();
            
            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            var organizationId = await _userContext.GetActiveOrganizationIdAsync();

            // Security: Verify lease offer belongs to active organization
            var existing = await _dbContext.LeaseOffers
                .FirstOrDefaultAsync(l => l.Id == leaseOffer.Id && l.OrganizationId == organizationId);

            if (existing == null)
            {
                throw new UnauthorizedAccessException($"Lease offer {leaseOffer.Id} not found in active organization.");
            }

            // Set tracking fields automatically
            leaseOffer.LastModifiedBy = userId;
            leaseOffer.LastModifiedOn = DateTime.UtcNow;
            leaseOffer.OrganizationId = organizationId!.Value; // Prevent org hijacking

            _dbContext.Entry(existing).CurrentValues.SetValues(leaseOffer);
            await _dbContext.SaveChangesAsync();
            return leaseOffer;
        }

        #endregion

        #endregion
   }
}