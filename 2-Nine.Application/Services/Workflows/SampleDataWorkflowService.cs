using Nine.Application.Services.Workflows;
using Nine.Core.Constants;
using Nine.Core.Entities;
using Nine.Core.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Nine.Application.Services.Workflows
{
    /// <summary>
    /// Workflow service for generating sample test data.
    /// Creates properties, tenants, leases, invoices, and payments for testing and demos.
    /// </summary>
    public class SampleDataWorkflowService : BaseWorkflowService
    {
        private readonly ILogger<SampleDataWorkflowService> _logger;
        private readonly InvoiceService _invoiceService;
        private readonly PaymentService _paymentService;
        private readonly NotificationService _notificationService;
        private readonly Random _random;

        public SampleDataWorkflowService(
            ApplicationDbContext context,
            IUserContextService userContext,
            InvoiceService invoiceService,
            PaymentService paymentService,
            NotificationService notificationService,
            ILogger<SampleDataWorkflowService> logger) : base(context, userContext)
        {
            _logger = logger;
            _invoiceService = invoiceService;
            _paymentService = paymentService;
            _notificationService = notificationService;
            _random = new Random(DateTime.Now.Millisecond); // Seed for varied data
        }

        /// <summary>
        /// Main orchestration method - generates complete sample dataset.
        /// </summary>
        public async Task<WorkflowResult> GenerateSampleDataAsync()
        {
            return await ExecuteWorkflowAsync(async () =>
            {
                _logger.LogInformation("Starting sample data generation...");

                try
                {
                    // Get context
                    var orgId = await GetActiveOrganizationIdAsync();

                    if (orgId == Guid.Empty)
                    {
                        return WorkflowResult.Fail("Organization context not available. Please ensure you are logged in.");
                    }

                    // Use SystemUser.Id for test data identification
                    var systemUserId = ApplicationConstants.SystemUser.Id;
                    _logger.LogInformation($"Generating sample data for Organization: {orgId}, CreatedBy: {systemUserId}");

                    // Generate entities in proper order (respecting dependencies)
                    var properties = await GeneratePropertiesAsync(orgId, systemUserId);
                    _logger.LogInformation($"Created {properties.Count} properties");

                    var calendarEvents = await GenerateCalendarEventsForPropertiesAsync(properties);
                    _logger.LogInformation($"Created {calendarEvents.Count} calendar events");

                    var notifications = await GenerateNotificationsForRoutineInspections(properties);
                    _logger.LogInformation($"Created {notifications.Count} notifications");

                    var tenants = await GenerateTenantsAsync(orgId, systemUserId);
                    _logger.LogInformation($"Created {tenants.Count} tenants");

                    var leases = await GenerateLeasesAsync(properties, tenants, orgId, systemUserId);
                    _logger.LogInformation($"Created {leases.Count} leases");

                    var invoices = await GenerateInvoicesAsync(leases, orgId, systemUserId);
                    _logger.LogInformation($"Created {invoices.Count} invoices");

                    var payments = await GeneratePaymentsAsync(invoices, orgId, systemUserId);
                    _logger.LogInformation($"Created {payments.Count} payments");

                    // Log workflow completion
                    await LogTransitionAsync(
                        entityType: "SampleData",
                        entityId: orgId,
                        fromStatus: null,
                        toStatus: "Generated",
                        action: "GenerateSampleData",
                        reason: $"Created {properties.Count} properties, {tenants.Count} tenants, {leases.Count} leases, {invoices.Count} invoices, {payments.Count} payments"
                    );

                    return WorkflowResult.Ok(
                        $"Successfully generated sample data: {properties.Count} properties, {tenants.Count} tenants, " +
                        $"{leases.Count} leases, {invoices.Count} invoices, {payments.Count} payments");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating sample data");
                    return WorkflowResult.Fail($"Error generating sample data: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Removes all sample data created with SystemUser.Id.
        /// Deletes properties, tenants, leases, invoices, and payments in proper order.
        /// </summary>
        public async Task<WorkflowResult> RemoveSampleDataAsync()
        {
            return await ExecuteWorkflowAsync(async () =>
            {
                _logger.LogInformation("Starting sample data removal...");

                try
                {
                    var orgId = await GetActiveOrganizationIdAsync();

                    if (orgId == Guid.Empty)
                    {
                        return WorkflowResult.Fail("Organization context not available.");
                    }

                    _logger.LogInformation($"Removing sample data for Organization: {orgId}");

                    // Delete in reverse hierarchical order (leaf nodes first, parents last)
                    // This prevents foreign key constraint violations
                    
                    var notificationsDeleted = await RemoveNotificationsAsync(orgId);
                    _logger.LogInformation($"Deleted {notificationsDeleted} notifications");
                    
                    var notesDeleted = await RemoveNotesAsync(orgId);
                    _logger.LogInformation($"Deleted {notesDeleted} notes");
                    
                    var checklistItemsDeleted = await RemoveChecklistItemsAsync(orgId);
                    _logger.LogInformation($"Deleted {checklistItemsDeleted} checklist items");
                    
                    var checklistsDeleted = await RemoveChecklistsAsync(orgId);
                    _logger.LogInformation($"Deleted {checklistsDeleted} checklists");
                    
                    var checklistTemplateItemsDeleted = await RemoveChecklistTemplateItemsAsync(orgId);
                    _logger.LogInformation($"Deleted {checklistTemplateItemsDeleted} checklist template items");
                    
                    var checklistTemplatesDeleted = await RemoveChecklistTemplatesAsync(orgId);
                    _logger.LogInformation($"Deleted {checklistTemplatesDeleted} checklist templates");
                    
                    var calendarEventsDeleted = await RemoveCalendarEventsAsync(orgId);
                    _logger.LogInformation($"Deleted {calendarEventsDeleted} calendar events");
                    
                    var documentsDeleted = await RemoveDocumentsAsync(orgId);
                    _logger.LogInformation($"Deleted {documentsDeleted} documents");
                  
                    
                    var paymentsDeleted = await RemovePaymentsAsync(orgId);
                    _logger.LogInformation($"Deleted {paymentsDeleted} payments");
                    
                    var invoicesDeleted = await RemoveInvoicesAsync(orgId);
                    _logger.LogInformation($"Deleted {invoicesDeleted} invoices");
                    
                    var repairsDeleted = await RemoveRepairsAsync(orgId);
                    _logger.LogInformation($"Deleted {repairsDeleted} repairs");
                    
                    var maintenanceRequestsDeleted = await RemoveMaintenanceRequestsAsync(orgId);
                    _logger.LogInformation($"Deleted {maintenanceRequestsDeleted} maintenance requests");
                    
                    var inspectionsDeleted = await RemoveInspectionsAsync(orgId);
                    _logger.LogInformation($"Deleted {inspectionsDeleted} inspections");
                    
                    var toursDeleted = await RemoveToursAsync(orgId);
                    _logger.LogInformation($"Deleted {toursDeleted} tours");
                    
                    var applicationScreeningsDeleted = await RemoveApplicationScreeningsAsync(orgId);
                    _logger.LogInformation($"Deleted {applicationScreeningsDeleted} application screenings");
                    
                    var rentalApplicationsDeleted = await RemoveRentalApplicationsAsync(orgId);
                    _logger.LogInformation($"Deleted {rentalApplicationsDeleted} rental applications");
                    
                    var prospectiveTenantsDeleted = await RemoveProspectiveTenantsAsync(orgId);
                    _logger.LogInformation($"Deleted {prospectiveTenantsDeleted} prospective tenants");
                    
                    var leaseOffersDeleted = await RemoveLeaseOffersAsync(orgId);
                    _logger.LogInformation($"Deleted {leaseOffersDeleted} lease offers");
                    
                    var securityDepositsDeleted = await RemoveSecurityDepositsAsync(orgId);
                    _logger.LogInformation($"Deleted {securityDepositsDeleted} security deposits");
                    
                    var securityDepositDividendsDeleted = await RemoveSecurityDepositDividendsAsync(orgId);
                    _logger.LogInformation($"Deleted {securityDepositDividendsDeleted} security deposit dividends");
                    
                    var securityDepositInvestmentPoolsDeleted = await RemoveSecurityDepositInvestmentPoolsAsync(orgId);
                    _logger.LogInformation($"Deleted {securityDepositInvestmentPoolsDeleted} security deposit investment pools");
                    
                    var leasesDeleted = await RemoveLeasesAsync(orgId);
                    _logger.LogInformation($"Deleted {leasesDeleted} leases");
                    
                    var tenantsDeleted = await RemoveTenantsAsync(orgId);
                    _logger.LogInformation($"Deleted {tenantsDeleted} tenants");
                    
                    var propertiesDeleted = await RemovePropertiesAsync(orgId);
                    _logger.LogInformation($"Deleted {propertiesDeleted} properties");

                    // Calculate total records deleted
                    var totalDeleted = propertiesDeleted + tenantsDeleted + leasesDeleted + invoicesDeleted + paymentsDeleted +
                                     documentsDeleted + calendarEventsDeleted + notificationsDeleted + notesDeleted +
                                     checklistItemsDeleted + checklistsDeleted + checklistTemplateItemsDeleted + checklistTemplatesDeleted +
                                     repairsDeleted + maintenanceRequestsDeleted + inspectionsDeleted + toursDeleted +
                                     applicationScreeningsDeleted + rentalApplicationsDeleted + prospectiveTenantsDeleted +
                                     leaseOffersDeleted + securityDepositsDeleted + securityDepositDividendsDeleted +
                                     securityDepositInvestmentPoolsDeleted;

                    // Log workflow completion
                    await LogTransitionAsync(
                        entityType: "SampleData",
                        entityId: orgId,
                        fromStatus: "Generated",
                        toStatus: "Removed",
                        action: "RemoveSampleData",
                        reason: $"Deleted {totalDeleted} total records across all entity types"
                    );

                    return WorkflowResult.Ok(
                        $"Successfully removed {totalDeleted} sample data records: " +
                        $"{propertiesDeleted} properties, {tenantsDeleted} tenants, {leasesDeleted} leases, " +
                        $"{invoicesDeleted} invoices, {paymentsDeleted} payments, {documentsDeleted} documents, " +
                        $"{maintenanceRequestsDeleted} maintenance requests, {inspectionsDeleted} inspections, " +
                        $"{rentalApplicationsDeleted} applications, {prospectiveTenantsDeleted} prospects");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error removing sample data");
                    return WorkflowResult.Fail($"Error removing sample data: {ex.Message}");
                }
            });
        }

        #region Property Generation

        private async Task<List<Property>> GeneratePropertiesAsync(Guid organizationId, string userId)
        {
            var properties = new List<Property>();
            var now = DateTime.UtcNow;

            // Define 9 properties in Texas with varied characteristics
            var propertyData = new[]
            {
                new { Address = "1234 Riverside Dr", City = "Austin", State = "TX", Zip = "78701", Type = ApplicationConstants.PropertyTypes.House, Beds = 3, Baths = 2.0m, SqFt = 1850, Rent = 1850m, Status = ApplicationConstants.PropertyStatuses.Occupied },
                new { Address = "5678 Oak Street", City = "Houston", State = "TX", Zip = "77002", Type = ApplicationConstants.PropertyTypes.Apartment, Beds = 2, Baths = 2.0m, SqFt = 1200, Rent = 1450m, Status = ApplicationConstants.PropertyStatuses.Occupied },
                new { Address = "910 Maple Ave", City = "Dallas", State = "TX", Zip = "75201", Type = ApplicationConstants.PropertyTypes.House, Beds = 4, Baths = 3.0m, SqFt = 2500, Rent = 2200m, Status = ApplicationConstants.PropertyStatuses.Occupied },
                new { Address = "1122 Pine Ln", City = "San Antonio", State = "TX", Zip = "78205", Type = ApplicationConstants.PropertyTypes.Condo, Beds = 2, Baths = 1.0m, SqFt = 1100, Rent = 1200m, Status = ApplicationConstants.PropertyStatuses.Available },
                new { Address = "3344 Elm Ct", City = "Fort Worth", State = "TX", Zip = "76102", Type = ApplicationConstants.PropertyTypes.House, Beds = 3, Baths = 2.0m, SqFt = 1750, Rent = 1750m, Status = ApplicationConstants.PropertyStatuses.Available },
                new { Address = "5566 Cedar Rd", City = "El Paso", State = "TX", Zip = "79901", Type = ApplicationConstants.PropertyTypes.Apartment, Beds = 1, Baths = 1.0m, SqFt = 850, Rent = 1100m, Status = ApplicationConstants.PropertyStatuses.Available },
                new { Address = "7789 Bluebonnet Blvd", City = "Austin", State = "TX", Zip = "78758", Type = ApplicationConstants.PropertyTypes.House, Beds = 3, Baths = 2.5m, SqFt = 2000, Rent = 1950m, Status = ApplicationConstants.PropertyStatuses.Occupied },
                new { Address = "9012 Ranch Road", City = "Plano", State = "TX", Zip = "75024", Type = ApplicationConstants.PropertyTypes.Townhouse, Beds = 3, Baths = 2.5m, SqFt = 1650, Rent = 1800m, Status = ApplicationConstants.PropertyStatuses.Occupied },
                new { Address = "4455 Lonestar Dr", City = "Corpus Christi", State = "TX", Zip = "78401", Type = ApplicationConstants.PropertyTypes.Condo, Beds = 2, Baths = 2.0m, SqFt = 1300, Rent = 1350m, Status = ApplicationConstants.PropertyStatuses.Available }
            };

            for (int i = 0; i < propertyData.Length; i++)
            {
                var data = propertyData[i];
                var createdDate = GetRandomDate(new DateTime(2025, 4, 1), new DateTime(2025, 6, 30));

                var property = new Property
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    Address = data.Address,
                    City = data.City,
                    State = data.State,
                    ZipCode = data.Zip,
                    PropertyType = data.Type,
                    Bedrooms = data.Beds,
                    Bathrooms = data.Baths,
                    SquareFeet = data.SqFt,
                    MonthlyRent = data.Rent,
                    Status = data.Status,
                    IsActive = data.Status == ApplicationConstants.PropertyStatuses.Available,
                    Description = $"Beautiful {data.Beds} bedroom, {data.Baths} bath {data.Type.ToLower()} in {data.City}. " +
                                 $"{data.SqFt} square feet with modern amenities and convenient location.",
                    RoutineInspectionIntervalMonths = 12,
                    NextRoutineInspectionDueDate = createdDate.AddDays(30),
                    CreatedBy = userId,
                    CreatedOn = createdDate,
                    IsDeleted = false,
                    IsSampleData = true
                };

                _context.Properties.Add(property);
                properties.Add(property);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Generated {properties.Count} properties");

            return properties;
        }

        #endregion

        #region Tenant Generation

        private async Task<List<Tenant>> GenerateTenantsAsync(Guid organizationId, string userId)
        {
            var tenants = new List<Tenant>();

            // Define 5 tenants with realistic data
            var tenantData = new[]
            {
                new { FirstName = "Sarah", LastName = "Johnson", DOB = new DateTime(1988, 5, 15), EmergencyName = "John Johnson", EmergencyPhone = "555-987-6543", Relationship = "Spouse" },
                new { FirstName = "Michael", LastName = "Chen", DOB = new DateTime(1992, 8, 22), EmergencyName = "Lisa Chen", EmergencyPhone = "555-876-5432", Relationship = "Sister" },
                new { FirstName = "Emily", LastName = "Rodriguez", DOB = new DateTime(1990, 3, 10), EmergencyName = "Carlos Rodriguez", EmergencyPhone = "555-765-4321", Relationship = "Father" },
                new { FirstName = "James", LastName = "Martinez", DOB = new DateTime(1985, 11, 8), EmergencyName = "Maria Martinez", EmergencyPhone = "555-654-3210", Relationship = "Mother" },
                new { FirstName = "Amanda", LastName = "Williams", DOB = new DateTime(1993, 7, 25), EmergencyName = "Robert Williams", EmergencyPhone = "555-543-2109", Relationship = "Brother" }
            };

            for (int i = 0; i < tenantData.Length; i++)
            {
                var data = tenantData[i];
                var createdDate = GetRandomDate(new DateTime(2025, 5, 1), new DateTime(2025, 7, 31));

                var tenant = new Tenant
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    FirstName = data.FirstName,
                    LastName = data.LastName,
                    Email = $"{data.FirstName.ToLower()}.{data.LastName.ToLower()}@example.com",
                    PhoneNumber = $"555-{_random.Next(100, 999)}-{_random.Next(1000, 9999)}",
                    DateOfBirth = data.DOB,
                    IdentificationNumber = $"ID-{_random.Next(10000000, 99999999)}",
                    IsActive = true,
                    EmergencyContactName = data.EmergencyName,
                    EmergencyContactPhone = data.EmergencyPhone,
                    Notes = $"Emergency contact relationship: {data.Relationship}",
                    CreatedBy = userId,
                    CreatedOn = createdDate,
                    IsDeleted = false,
                    IsSampleData = true
                };

                _context.Tenants.Add(tenant);
                tenants.Add(tenant);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Generated {tenants.Count} tenants");

            return tenants;
        }

        #endregion

        #region Lease Generation

        private async Task<List<Lease>> GenerateLeasesAsync(
            List<Property> properties,
            List<Tenant> tenants,
            Guid organizationId,
            string userId)
        {
            var leases = new List<Lease>();
            var currentDate = DateTime.UtcNow.Date;

            // Create 3 standard leases for first 3 properties (original logic)
            var leaseStartMonths = new[] { 5, 6, 7 }; // May, June, July 2025

            for (int i = 0; i < 3; i++)
            {
                var property = properties[i];
                var tenant = tenants[i];
                var startMonth = leaseStartMonths[i];

                // Random start day (1-5 of month)
                var startDay = _random.Next(1, 6);
                var startDate = new DateTime(2025, startMonth, startDay);
                var endDate = startDate.AddYears(1); // 12-month lease

                var lease = new Lease
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    PropertyId = property.Id,
                    TenantId = tenant.Id,
                    StartDate = startDate,
                    EndDate = endDate,
                    MonthlyRent = property.MonthlyRent,
                    SecurityDeposit = property.MonthlyRent, // 1x rent
                    Status = ApplicationConstants.LeaseStatuses.Active,
                    Terms = $"12-month {ApplicationConstants.LeaseTypes.FixedTerm} lease. Rent: ${property.MonthlyRent}/month. " +
                           $"Security Deposit: ${property.MonthlyRent}. Payment due on the 5th of each month.",
                    SignedOn = startDate.AddDays(-10), // Signed 10 days before start
                    OfferedOn = startDate.AddDays(-20), // Offered 20 days before start
                    CreatedBy = userId,
                    CreatedOn = startDate.AddDays(-25),
                    IsDeleted = false,
                    IsSampleData = true
                };

                _context.Leases.Add(lease);
                leases.Add(lease);

                // Update property status to Occupied
                property.Status = ApplicationConstants.PropertyStatuses.Occupied;
                property.IsActive = false;
            }

            // Create 2 new leases expiring soon (properties 6 and 7, tenants 3 and 4)
            // Lease 1: Expires in 30 days from current date
            var endDate30 = currentDate.AddDays(30);
            var startDate30 = endDate30.AddMonths(-11); // 12-month lease
            var lease30Days = new Lease
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                PropertyId = properties[6].Id, // 7789 Bluebonnet Blvd
                TenantId = tenants[3].Id, // James Martinez
                StartDate = startDate30,
                EndDate = endDate30,
                MonthlyRent = properties[6].MonthlyRent,
                SecurityDeposit = properties[6].MonthlyRent,
                Status = ApplicationConstants.LeaseStatuses.Active,
                Terms = $"12-month {ApplicationConstants.LeaseTypes.FixedTerm} lease. Rent: ${properties[6].MonthlyRent}/month. " +
                       $"Security Deposit: ${properties[6].MonthlyRent}. Payment due on the 5th of each month. EXPIRING SOON.",
                SignedOn = startDate30.AddDays(-10), // Signed 10 days before start
                OfferedOn = startDate30.AddDays(-20), // Offered 20 days before start
                CreatedBy = userId,
                CreatedOn = startDate30.AddDays(-25),
                IsDeleted = false,
                IsSampleData = true
            };
            _context.Leases.Add(lease30Days);
            leases.Add(lease30Days);
            properties[6].Status = ApplicationConstants.PropertyStatuses.Occupied;
            properties[6].IsActive = false;

            // Lease 2: Expires in 60 days from current date
            var endDate60 = currentDate.AddDays(60);
            var startDate60 = endDate60.AddMonths(-10); // 12-month lease
            var lease60Days = new Lease
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                PropertyId = properties[7].Id, // 9012 Ranch Road
                TenantId = tenants[4].Id, // Amanda Williams
                StartDate = startDate60,
                EndDate = endDate60,
                MonthlyRent = properties[7].MonthlyRent,
                SecurityDeposit = properties[7].MonthlyRent,
                Status = ApplicationConstants.LeaseStatuses.Active,
                Terms = $"12-month {ApplicationConstants.LeaseTypes.FixedTerm} lease. Rent: ${properties[7].MonthlyRent}/month. " +
                       $"Security Deposit: ${properties[7].MonthlyRent}. Payment due on the 5th of each month. EXPIRING SOON.",
                SignedOn = startDate60.AddDays(-10), // Signed 10 days before start
                OfferedOn = startDate60.AddDays(-20), // Offered 20 days before start
                CreatedBy = userId,
                CreatedOn = startDate60.AddDays(-25),
                IsDeleted = false,
                IsSampleData = true
            };
            _context.Leases.Add(lease60Days);
            leases.Add(lease60Days);
            properties[7].Status = ApplicationConstants.PropertyStatuses.Occupied;
            properties[7].IsActive = false;

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Generated {leases.Count} leases");

            return leases;
        }

        #endregion

        #region Invoice Generation

        private async Task<List<Invoice>> GenerateInvoicesAsync(
            List<Lease> leases,
            Guid organizationId,
            string userId)
        {
            var invoices = new List<Invoice>();
            var currentDate = DateTime.UtcNow.Date;

            foreach (var lease in leases)
            {
                var invoiceDate = new DateTime(lease.StartDate.Year, lease.StartDate.Month, 20); // 20th of start month
                var currentMonth = new DateTime(currentDate.Year, currentDate.Month, 1);

                // Generate invoices from lease start to current month
                while (invoiceDate.Month <= currentDate.Month || invoiceDate.Year < currentDate.Year)
                {
                    // Skip if invoice date is in the future
                    if (invoiceDate > currentDate)
                        break;

                    var dueDate = invoiceDate.AddMonths(1);
                    dueDate = new DateTime(dueDate.Year, dueDate.Month, 5); // Due on 5th of following month

                    // Generate proper invoice number using service
                    var invoiceNumber = await _invoiceService.GenerateInvoiceNumberAsync();

                    var invoice = new Invoice
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = organizationId,
                        LeaseId = lease.Id,
                        InvoiceNumber = invoiceNumber,
                        InvoicedOn = invoiceDate,
                        DueOn = dueDate,
                        Amount = lease.MonthlyRent,
                        Status = ApplicationConstants.InvoiceStatuses.Pending,
                        Description = $"Monthly Rent - {invoiceDate:MMMM yyyy}",
                        CreatedBy = userId,
                        CreatedOn = invoiceDate,
                        IsDeleted = false,
                        IsSampleData = true
                    };

                    _context.Invoices.Add(invoice);
                    
                    // CRITICAL FIX: Save immediately after generating number to prevent collisions
                    // The MAX query in GenerateInvoiceNumberAsync needs to see this invoice
                    // before generating the next number
                    await _context.SaveChangesAsync();
                    
                    invoices.Add(invoice);

                    // Move to next month
                    invoiceDate = invoiceDate.AddMonths(1);
                }
            }

            _logger.LogInformation($"Generated {invoices.Count} invoices");

            return invoices;
        }

        #endregion

        #region Payment Generation

        private async Task<List<Payment>> GeneratePaymentsAsync(
            List<Invoice> invoices,
            Guid organizationId,
            string userId)
        {
            var payments = new List<Payment>();
            var currentDate = DateTime.UtcNow.Date;

            // Group invoices by lease
            var invoicesByLease = invoices.GroupBy(i => i.LeaseId).ToList();

            foreach (var leaseGroup in invoicesByLease)
            {
                var leaseInvoices = leaseGroup.OrderBy(i => i.InvoicedOn).ToList();
                var lease = await _context.Leases.FindAsync(leaseGroup.Key);
                
                if (lease == null) continue;

                List<Invoice> invoicesToPay;

                // Check if this is one of the two new expiring leases by checking if lease has "EXPIRING SOON" in terms
                if (lease.Terms != null && lease.Terms.Contains("EXPIRING SOON"))
                {
                    // New expiring leases: Pay ALL past-due invoices
                    invoicesToPay = leaseInvoices
                        .Where(i => i.DueOn < currentDate)
                        .ToList();
                }
                else
                {
                    // Original 3 leases: Pay only last 3 invoices
                    var monthsRemaining = ((lease.EndDate.Year - currentDate.Year) * 12) + 
                                         lease.EndDate.Month - currentDate.Month;

                    if (monthsRemaining > 3)
                    {
                        invoicesToPay = leaseInvoices
                            .Where(i => i.DueOn < currentDate)
                            .OrderByDescending(i => i.InvoicedOn)
                            .Take(3)
                            .ToList();
                    }
                    else
                    {
                        invoicesToPay = new List<Invoice>();
                    }
                }

                foreach (var invoice in invoicesToPay)
                {
                    // Random payment date logic:
                    // 70% chance: Pay before due date (between invoice date and due date)
                    // 30% chance: Pay late (1-10 days after due date)
                    DateTime paymentDate;
                    decimal lateFee = 0m;
                    
                    if (_random.Next(100) < 70)
                    {
                        // Pay on time: Random date between invoice date and due date
                        var daysInPeriod = (invoice.DueOn - invoice.InvoicedOn).Days;
                        var randomDays = _random.Next(daysInPeriod + 1);
                        paymentDate = invoice.InvoicedOn.AddDays(randomDays);
                    }
                    else
                    {
                        // Pay late: 1-10 days after due date
                        var lateDays = _random.Next(1, 11);
                        paymentDate = invoice.DueOn.AddDays(lateDays);
                        lateFee = 50m; // $50 late fee
                    }

                    // Ensure payment date doesn't exceed current date
                    if (paymentDate > currentDate)
                        paymentDate = currentDate;

                    // Calculate total payment amount (invoice + late fee if applicable)
                    var totalAmount = invoice.Amount + lateFee;

                    // Generate proper payment number using service
                    var paymentNumber = await _paymentService.GeneratePaymentNumberAsync();

                    var payment = new Payment
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = organizationId,
                        InvoiceId = invoice.Id,
                        Amount = totalAmount,
                        PaymentNumber = paymentNumber,
                        PaidOn = paymentDate,
                        PaymentMethod = GetRandomPaymentMethod(),
                        Notes = lateFee > 0 
                            ? $"Payment for {invoice.Description} (includes $50 late fee)"
                            : $"Payment for {invoice.Description}",
                        CreatedBy = userId,
                        CreatedOn = paymentDate,
                        IsDeleted = false,
                        IsSampleData = true
                    };

                    _context.Payments.Add(payment);
                    
                    // CRITICAL FIX: Save immediately after generating number to prevent collisions
                    await _context.SaveChangesAsync();
                    
                    payments.Add(payment);

                    // Update invoice status to Paid
                    invoice.Status = ApplicationConstants.InvoiceStatuses.Paid;
                    invoice.AmountPaid = totalAmount;
                    invoice.PaidOn = paymentDate;
                    invoice.LastModifiedBy = userId;
                    invoice.LastModifiedOn = paymentDate;
                    
                    // Save invoice status update immediately
                    await _context.SaveChangesAsync();
                }
            }

            _logger.LogInformation($"Generated {payments.Count} payments");

            return payments;
        }

        #endregion

        #region Calendar Event Generation

        private async Task<List<CalendarEvent>> GenerateCalendarEventsForPropertiesAsync(List<Property> properties)
        {
            var calendarEvents = new List<CalendarEvent>();

            foreach (var property in properties)
            {
                if (!property.NextRoutineInspectionDueDate.HasValue)
                {
                    continue;
                }

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
                    OrganizationId = property.OrganizationId,
                    CreatedBy = property.CreatedBy,
                    CreatedOn = DateTime.UtcNow,
                    EventType = "Inspection",
                    Status = "Scheduled",
                    IsSampleData = true
                };

                calendarEvents.Add(calendarEvent);
            }
            _context.CalendarEvents.AddRange(calendarEvents);
            await _context.SaveChangesAsync();
            return calendarEvents;
        }

        #endregion

        #region Notification Generation
        private async Task<List<Notification>> GenerateNotificationsForRoutineInspections(List<Property> properties)
        {
            if (properties == null || properties.Count == 0)
                return new List<Notification>();

            var notifications = new List<Notification>();
            var users = await _context.OrganizationUsers
                .Where(o => o.OrganizationId == properties.First().OrganizationId && !o.IsDeleted && o.IsActive).ToListAsync();
            
            foreach(var user in users)
            {
                foreach(var property in properties)
                {
                    // Use NotificationService to send notifications with SignalR broadcasts
                    var notification = await _notificationService.SendNotificationAsync(
                        user.UserId,
                        "Routine Inspection Scheduled",
                        $"A routine inspection has been scheduled for the property at {property.Address} on {property.NextRoutineInspectionDueDate!.Value:d}.",
                        NotificationConstants.Types.Info,
                        NotificationConstants.Categories.Inspection,
                        property.Id,
                        nameof(Property)
                    );
                    
                    // Mark as sample data
                    notification.IsSampleData = true;
                    await _context.SaveChangesAsync();
                    
                    notifications.Add(notification);
                }
            }

            return notifications;
        }
        #endregion

        #region Helper Methods

        /// <summary>
        /// Gets a random date within the specified range.
        /// </summary>
        private DateTime GetRandomDate(DateTime start, DateTime end)
        {
            var range = (end - start).Days;
            return start.AddDays(_random.Next(range + 1));
        }

        /// <summary>
        /// Gets a random payment method from available options.
        /// </summary>
        private string GetRandomPaymentMethod()
        {
            var methods = new[]
            {
                ApplicationConstants.PaymentMethods.CreditCard,
                ApplicationConstants.PaymentMethods.Check,
                ApplicationConstants.PaymentMethods.BankTransfer,
                ApplicationConstants.PaymentMethods.Cash
            };

            return methods[_random.Next(methods.Length)];
        }

        #endregion

        #region Sample Data Removal

        private async Task<int> RemovePropertiesAsync(Guid organizationId)
        {
            var properties = await _context.Properties
                .Where(p => p.OrganizationId == organizationId && p.IsSampleData)
                .ToListAsync();

            _context.Properties.RemoveRange(properties);
            await _context.SaveChangesAsync();

            return properties.Count;
        }

        private async Task<int> RemoveTenantsAsync(Guid organizationId)
        {
            var tenants = await _context.Tenants
                .Where(t => t.OrganizationId == organizationId && t.IsSampleData)
                .ToListAsync();

            _context.Tenants.RemoveRange(tenants);
            await _context.SaveChangesAsync();

            return tenants.Count;
        }

        private async Task<int> RemoveLeasesAsync(Guid organizationId)
        {
            var leases = await _context.Leases
                .Where(l => l.OrganizationId == organizationId && l.IsSampleData)
                .ToListAsync();

            _context.Leases.RemoveRange(leases);
            await _context.SaveChangesAsync();

            return leases.Count;
        }

        private async Task<int> RemoveInvoicesAsync(Guid organizationId)
        {
            var invoices = await _context.Invoices
                .Where(i => i.OrganizationId == organizationId && i.IsSampleData)
                .ToListAsync();

            _context.Invoices.RemoveRange(invoices);
            await _context.SaveChangesAsync();

            return invoices.Count;
        }

        private async Task<int> RemovePaymentsAsync(Guid organizationId)
        {
            var payments = await _context.Payments
                .Where(p => p.OrganizationId == organizationId && p.IsSampleData)
                .ToListAsync();

            _context.Payments.RemoveRange(payments);
            await _context.SaveChangesAsync();

            return payments.Count;
        }

        private async Task<int> RemoveDocumentsAsync(Guid organizationId)
        {
            var documents = await _context.Documents
                .Where(d => d.OrganizationId == organizationId && d.IsSampleData)
                .ToListAsync();

            _context.Documents.RemoveRange(documents);
            await _context.SaveChangesAsync();

            return documents.Count;
        }

        private async Task<int> RemoveNotesAsync(Guid organizationId)
        {
            var notes = await _context.Notes
                .Where(n => n.OrganizationId == organizationId && n.IsSampleData)
                .ToListAsync();

            _context.Notes.RemoveRange(notes);
            await _context.SaveChangesAsync();

            return notes.Count;
        }

        private async Task<int> RemoveChecklistItemsAsync(Guid organizationId)
        {
            var checklistItems = await _context.ChecklistItems
                .Where(ci => ci.OrganizationId == organizationId && ci.IsSampleData)
                .ToListAsync();

            _context.ChecklistItems.RemoveRange(checklistItems);
            await _context.SaveChangesAsync();

            return checklistItems.Count;
        }

        private async Task<int> RemoveChecklistsAsync(Guid organizationId)
        {
            var checklists = await _context.Checklists
                .Where(c => c.OrganizationId == organizationId && c.IsSampleData)
                .ToListAsync();

            _context.Checklists.RemoveRange(checklists);
            await _context.SaveChangesAsync();

            return checklists.Count;
        }

        private async Task<int> RemoveChecklistTemplateItemsAsync(Guid organizationId)
        {
            var checklistTemplateItems = await _context.ChecklistTemplateItems
                .Where(cti => cti.OrganizationId == organizationId && cti.IsSampleData)
                .ToListAsync();

            _context.ChecklistTemplateItems.RemoveRange(checklistTemplateItems);
            await _context.SaveChangesAsync();

            return checklistTemplateItems.Count;
        }

        private async Task<int> RemoveChecklistTemplatesAsync(Guid organizationId)
        {
            var checklistTemplates = await _context.ChecklistTemplates
                .Where(ct => ct.OrganizationId == organizationId && ct.IsSampleData)
                .ToListAsync();

            _context.ChecklistTemplates.RemoveRange(checklistTemplates);
            await _context.SaveChangesAsync();

            return checklistTemplates.Count;
        }

        private async Task<int> RemoveRepairsAsync(Guid organizationId)
        {
            var repairs = await _context.Repairs
                .Where(r => r.OrganizationId == organizationId && r.IsSampleData)
                .ToListAsync();

            _context.Repairs.RemoveRange(repairs);
            await _context.SaveChangesAsync();

            return repairs.Count;
        }

        private async Task<int> RemoveMaintenanceRequestsAsync(Guid organizationId)
        {
            var maintenanceRequests = await _context.MaintenanceRequests
                .Where(mr => mr.OrganizationId == organizationId && mr.IsSampleData)
                .ToListAsync();

            _context.MaintenanceRequests.RemoveRange(maintenanceRequests);
            await _context.SaveChangesAsync();

            return maintenanceRequests.Count;
        }

        private async Task<int> RemoveInspectionsAsync(Guid organizationId)
        {
            var inspections = await _context.Inspections
                .Where(i => i.OrganizationId == organizationId && i.IsSampleData)
                .ToListAsync();

            _context.Inspections.RemoveRange(inspections);
            await _context.SaveChangesAsync();

            return inspections.Count;
        }

        private async Task<int> RemoveToursAsync(Guid organizationId)
        {
            var tours = await _context.Tours
                .Where(t => t.OrganizationId == organizationId && t.IsSampleData)
                .ToListAsync();

            _context.Tours.RemoveRange(tours);
            await _context.SaveChangesAsync();

            return tours.Count;
        }

        private async Task<int> RemoveApplicationScreeningsAsync(Guid organizationId)
        {
            var applicationScreenings = await _context.ApplicationScreenings
                .Where(a => a.OrganizationId == organizationId && a.IsSampleData)
                .ToListAsync();

            _context.ApplicationScreenings.RemoveRange(applicationScreenings);
            await _context.SaveChangesAsync();

            return applicationScreenings.Count;
        }

        private async Task<int> RemoveRentalApplicationsAsync(Guid organizationId)
        {
            var rentalApplications = await _context.RentalApplications
                .Where(ra => ra.OrganizationId == organizationId && ra.IsSampleData)
                .ToListAsync();

            _context.RentalApplications.RemoveRange(rentalApplications);
            await _context.SaveChangesAsync();

            return rentalApplications.Count;
        }

        private async Task<int> RemoveProspectiveTenantsAsync(Guid organizationId)
        {
            var prospectiveTenants = await _context.ProspectiveTenants
                .Where(pt => pt.OrganizationId == organizationId && pt.IsSampleData)
                .ToListAsync();

            _context.ProspectiveTenants.RemoveRange(prospectiveTenants);
            await _context.SaveChangesAsync();

            return prospectiveTenants.Count;
        }

        private async Task<int> RemoveLeaseOffersAsync(Guid organizationId)
        {
            var leaseOffers = await _context.LeaseOffers
                .Where(lo => lo.OrganizationId == organizationId && lo.IsSampleData)
                .ToListAsync();

            _context.LeaseOffers.RemoveRange(leaseOffers);
            await _context.SaveChangesAsync();

            return leaseOffers.Count;
        }

        private async Task<int> RemoveSecurityDepositsAsync(Guid organizationId)
        {
            var securityDeposits = await _context.SecurityDeposits
                .Where(sd => sd.OrganizationId == organizationId && sd.IsSampleData)
                .ToListAsync();

            _context.SecurityDeposits.RemoveRange(securityDeposits);
            await _context.SaveChangesAsync();

            return securityDeposits.Count;
        }

        private async Task<int> RemoveSecurityDepositDividendsAsync(Guid organizationId)
        {
            var securityDepositDividends = await _context.SecurityDepositDividends
                .Where(sdd => sdd.OrganizationId == organizationId && sdd.IsSampleData)
                .ToListAsync();

            _context.SecurityDepositDividends.RemoveRange(securityDepositDividends);
            await _context.SaveChangesAsync();

            return securityDepositDividends.Count;
        }

        private async Task<int> RemoveSecurityDepositInvestmentPoolsAsync(Guid organizationId)
        {
            var securityDepositInvestmentPools = await _context.SecurityDepositInvestmentPools
                .Where(sdip => sdip.OrganizationId == organizationId && sdip.IsSampleData)
                .ToListAsync();

            _context.SecurityDepositInvestmentPools.RemoveRange(securityDepositInvestmentPools);
            await _context.SaveChangesAsync();

            return securityDepositInvestmentPools.Count;
        }

        private async Task<int> RemoveCalendarEventsAsync(Guid organizationId)
        {
            var calendarEvents = await _context.CalendarEvents
                .Where(ce => ce.OrganizationId == organizationId && ce.IsSampleData)
                .ToListAsync();

            _context.CalendarEvents.RemoveRange(calendarEvents);
            await _context.SaveChangesAsync();

            return calendarEvents.Count;
        }

        private async Task<int> RemoveNotificationsAsync(Guid organizationId)
        {
            var notifications = await _context.Notifications
                .Where(n => n.OrganizationId == organizationId && n.IsSampleData)
                .ToListAsync();

            _context.Notifications.RemoveRange(notifications);
            await _context.SaveChangesAsync();

            return notifications.Count;
        }

        //
        // Potential future method to remove all sample data in one go (if needed) - currently we remove by entity type to ensure proper order and avoid FK issues
        private async Task RemoveAllSampleDataAsync(Guid orgId)
        {
            var allSampleEntities = new List<object>();

            foreach (var dbSet in _context.GetType().GetProperties().Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>)))
            {
                var entity = dbSet.GetValue(_context);
                if (entity is IQueryable<BaseModel> baseModelQueryable)
                {
                    var sampleData = baseModelQueryable
                        .Where(e => e.OrganizationId == orgId && e.IsSampleData)
                        .ToList();
                    allSampleEntities.AddRange(sampleData);
                }
            }

            _context.RemoveRange(allSampleEntities); // EF Core figures out order
            await _context.SaveChangesAsync();
        }

        #endregion

        #region Sample Data Detection

        /// <summary>
        /// Checks if sample data exists for the active organization.
        /// Used to conditionally show Add/Remove Sample Data buttons.
        /// </summary>
        public async Task<bool> HasSampleDataAsync()
        {
            var orgId = await GetActiveOrganizationIdAsync();
            
            // Check any entity type - if any sample data exists, return true
            var hasSampleData = await _context.Properties
                .AnyAsync(p => p.OrganizationId == orgId && p.IsSampleData && !p.IsDeleted);
            
            if (!hasSampleData)
            {
                hasSampleData = await _context.Tenants
                    .AnyAsync(t => t.OrganizationId == orgId && t.IsSampleData && !t.IsDeleted);
            }
            
            if (!hasSampleData)
            {
                hasSampleData = await _context.Leases
                    .AnyAsync(l => l.OrganizationId == orgId && l.IsSampleData && !l.IsDeleted);
            }
            
            if (!hasSampleData)
            {
                hasSampleData = await _context.CalendarEvents
                    .AnyAsync(ce => ce.OrganizationId == orgId && ce.IsSampleData && !ce.IsDeleted);
            }
            
            return hasSampleData;
        }

        /// <summary>
        /// Gets count of sample data records by entity type.
        /// Used for UI display (e.g., "3 sample properties, 2 sample tenants").
        /// </summary>
        public async Task<SampleDataSummary> GetSampleDataSummaryAsync()
        {
            var orgId = await GetActiveOrganizationIdAsync();
            
            return new SampleDataSummary
            {
                PropertyCount = await _context.Properties.CountAsync(p => p.OrganizationId == orgId && p.IsSampleData && !p.IsDeleted),
                TenantCount = await _context.Tenants.CountAsync(t => t.OrganizationId == orgId && t.IsSampleData && !t.IsDeleted),
                LeaseCount = await _context.Leases.CountAsync(l => l.OrganizationId == orgId && l.IsSampleData && !l.IsDeleted),
                InvoiceCount = await _context.Invoices.CountAsync(i => i.OrganizationId == orgId && i.IsSampleData && !i.IsDeleted),
                PaymentCount = await _context.Payments.CountAsync(p => p.OrganizationId == orgId && p.IsSampleData && !p.IsDeleted),
                DocumentCount = await _context.Documents.CountAsync(d => d.OrganizationId == orgId && d.IsSampleData && !d.IsDeleted),
                CalendarEventCount = await _context.CalendarEvents.CountAsync(ce => ce.OrganizationId == orgId && ce.IsSampleData && !ce.IsDeleted)
            };
        }

        #endregion
    }

    /// <summary>
    /// Summary of sample data counts by entity type.
    /// </summary>
    public class SampleDataSummary
    {
        public int PropertyCount { get; set; }
        public int TenantCount { get; set; }
        public int LeaseCount { get; set; }
        public int InvoiceCount { get; set; }
        public int PaymentCount { get; set; }
        public int DocumentCount { get; set; }
        public int CalendarEventCount { get; set; }
        public int NotificationCount { get; set; }
        
        public int TotalCount => PropertyCount + TenantCount + LeaseCount + InvoiceCount + PaymentCount + DocumentCount + CalendarEventCount + NotificationCount;
        public bool HasData => TotalCount > 0;
    }
}
