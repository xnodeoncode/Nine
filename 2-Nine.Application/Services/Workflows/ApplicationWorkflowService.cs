using Nine.Core.Interfaces.Services;
using Nine.Core.Constants;
using Nine.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Nine.Application.Services.Workflows
{
    /// <summary>
    /// Application status enumeration for state machine validation.
    /// </summary>
    public enum ApplicationStatus
    {
        Submitted,
        UnderReview,
        Screening,
        Approved,
        Denied,
        LeaseOffered,
        LeaseAccepted,
        LeaseDeclined,
        Expired,
        Withdrawn
    }

    /// <summary>
    /// Workflow service for rental application lifecycle management.
    /// Centralizes all state transitions from prospect inquiry through lease offer generation.
    /// </summary>
    public class ApplicationWorkflowService : BaseWorkflowService, IWorkflowState<ApplicationStatus>
    {
        private readonly NoteService _noteService;
        private readonly NotificationService _notificationService;

        public ApplicationWorkflowService(
            ApplicationDbContext context,
            IUserContextService userContext,
            NoteService noteService,
            NotificationService notificationService)
            : base(context, userContext)
        {
            _noteService = noteService;
            _notificationService = notificationService;
        }

        #region State Machine Implementation

        public bool IsValidTransition(ApplicationStatus fromStatus, ApplicationStatus toStatus)
        {
            var validTransitions = GetValidNextStates(fromStatus);
            return validTransitions.Contains(toStatus);
        }

        public List<ApplicationStatus> GetValidNextStates(ApplicationStatus currentStatus)
        {
            return currentStatus switch
            {
                ApplicationStatus.Submitted => new()
                {
                    ApplicationStatus.UnderReview,
                    ApplicationStatus.Denied,
                    ApplicationStatus.Withdrawn,
                    ApplicationStatus.Expired
                },
                ApplicationStatus.UnderReview => new()
                {
                    ApplicationStatus.Screening,
                    ApplicationStatus.Denied,
                    ApplicationStatus.Withdrawn,
                    ApplicationStatus.Expired
                },
                ApplicationStatus.Screening => new()
                {
                    ApplicationStatus.Approved,
                    ApplicationStatus.Denied,
                    ApplicationStatus.Withdrawn
                },
                ApplicationStatus.Approved => new()
                {
                    ApplicationStatus.LeaseOffered,
                    ApplicationStatus.Denied  // Can deny after approval if issues found
                },
                ApplicationStatus.LeaseOffered => new()
                {
                    ApplicationStatus.LeaseAccepted,
                    ApplicationStatus.LeaseDeclined,
                    ApplicationStatus.Expired
                },
                _ => new List<ApplicationStatus>() // Terminal states have no valid transitions
            };
        }

        public string GetInvalidTransitionReason(ApplicationStatus fromStatus, ApplicationStatus toStatus)
        {
            var validStates = GetValidNextStates(fromStatus);
            return $"Cannot transition from {fromStatus} to {toStatus}. Valid next states: {string.Join(", ", validStates)}";
        }

        #endregion

        #region Core Workflow Methods

        /// <summary>
        /// Submits a new rental application for a prospect and property.
        /// Creates application, updates property status if first app, and updates prospect status.
        /// </summary>
        public async Task<WorkflowResult<RentalApplication>> SubmitApplicationAsync(
            Guid prospectId,
            Guid propertyId,
            ApplicationSubmissionModel model)
        {
            return await ExecuteWorkflowAsync<RentalApplication>(async () =>
            {
                // Validation
                var validation = await ValidateApplicationSubmissionAsync(prospectId, propertyId);
                if (!validation.Success)
                    return WorkflowResult<RentalApplication>.Fail(validation.Errors);

                var userId = await GetCurrentUserIdAsync();
                var orgId = await GetActiveOrganizationIdAsync();

                // Get organization settings for expiration days
                var settings = await _context.OrganizationSettings
                    .FirstOrDefaultAsync(s => s.OrganizationId == orgId);
                
                var expirationDays = settings?.ApplicationExpirationDays ?? 30;

                // Create application
                var application = new RentalApplication
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = orgId,
                    ProspectiveTenantId = prospectId,
                    PropertyId = propertyId,
                    Status = ApplicationConstants.ApplicationStatuses.Submitted,
                    AppliedOn = DateTime.UtcNow,
                    ExpiresOn = DateTime.UtcNow.AddDays(expirationDays),
                    ApplicationFee = model.ApplicationFee,
                    ApplicationFeePaid = model.ApplicationFeePaid,
                    ApplicationFeePaidOn = model.ApplicationFeePaid ? DateTime.UtcNow : null,
                    ApplicationFeePaymentMethod = model.ApplicationFeePaymentMethod,
                    CurrentAddress = model.CurrentAddress,
                    CurrentCity = model.CurrentCity,
                    CurrentState = model.CurrentState,
                    CurrentZipCode = model.CurrentZipCode,
                    CurrentRent = model.CurrentRent,
                    LandlordName = model.LandlordName,
                    LandlordPhone = model.LandlordPhone,
                    EmployerName = model.EmployerName,
                    JobTitle = model.JobTitle,
                    MonthlyIncome = model.MonthlyIncome,
                    EmploymentLengthMonths = model.EmploymentLengthMonths,
                    Reference1Name = model.Reference1Name,
                    Reference1Phone = model.Reference1Phone,
                    Reference1Relationship = model.Reference1Relationship,
                    Reference2Name = model.Reference2Name,
                    Reference2Phone = model.Reference2Phone,
                    Reference2Relationship = model.Reference2Relationship,
                    CreatedBy = userId,
                    CreatedOn = DateTime.UtcNow
                };

                _context.RentalApplications.Add(application);
                // Note: EF Core will assign ID when transaction commits

                // Update property status if this is first application
                var property = await _context.Properties
                    .FirstOrDefaultAsync(p => p.Id == propertyId && p.OrganizationId == orgId);
                
                if (property != null && property.Status == ApplicationConstants.PropertyStatuses.Available)
                {
                    property.Status = ApplicationConstants.PropertyStatuses.ApplicationPending;
                    property.LastModifiedBy = userId;
                    property.LastModifiedOn = DateTime.UtcNow;
                }

                // Update prospect status
                var prospect = await _context.ProspectiveTenants
                    .FirstOrDefaultAsync(p => p.Id == prospectId && p.OrganizationId == orgId);
                
                if (prospect != null)
                {
                    var oldStatus = prospect.Status;
                    prospect.Status = ApplicationConstants.ProspectiveStatuses.Applied;
                    prospect.LastModifiedBy = userId;
                    prospect.LastModifiedOn = DateTime.UtcNow;

                    // Log prospect transition
                    await LogTransitionAsync(
                        "ProspectiveTenant",
                        prospectId,
                        oldStatus,
                        prospect.Status,
                        "SubmitApplication");
                }

                // Log application creation
                await LogTransitionAsync(
                    "RentalApplication",
                    application.Id,
                    null,
                    ApplicationConstants.ApplicationStatuses.Submitted,
                    "SubmitApplication");

                // send notification to leasing agents
                await _notificationService.NotifyAllUsersAsync(
                    orgId,
                    $"New Rental Application Submitted for {property?.Address}",
                    $"{prospect!.FullName} has submitted a new rental application for property ID {property!.Address}.",
                    NotificationConstants.Types.Info,
                    NotificationConstants.Categories.Application,
                    application!.Id,
                    ApplicationConstants.EntityTypes.Application
                );

                return WorkflowResult<RentalApplication>.Ok(
                    application,
                    "Application submitted successfully");

            });
        }

        /// <summary>
        /// Marks an application as under manual review.
        /// </summary>
        public async Task<WorkflowResult> MarkApplicationUnderReviewAsync(Guid applicationId)
        {
            return await ExecuteWorkflowAsync(async () =>
            {
                var application = await GetApplicationAsync(applicationId);
                if (application == null)
                    return WorkflowResult.Fail("Application not found");

                // Validate state transition
                if (!IsValidTransition(
                    Enum.Parse<ApplicationStatus>(application.Status),
                    ApplicationStatus.UnderReview))
                {
                    return WorkflowResult.Fail(GetInvalidTransitionReason(
                        Enum.Parse<ApplicationStatus>(application.Status),
                        ApplicationStatus.UnderReview));
                }

                var userId = await GetCurrentUserIdAsync();
                var oldStatus = application.Status;

                application.Status = ApplicationConstants.ApplicationStatuses.UnderReview;
                application.DecisionBy = userId;
                application.LastModifiedBy = userId;
                application.LastModifiedOn = DateTime.UtcNow;

                await LogTransitionAsync(
                    "RentalApplication",
                    applicationId,
                    oldStatus,
                    application.Status,
                    "MarkUnderReview");

                // send notification to leasing agents
                await _notificationService.NotifyAllUsersAsync(
                    application.OrganizationId,
                    $"Application Marked as Under Review for {application.Property?.Address}",
                    $"Application ID {application.Id} has been marked as under review.",
                    NotificationConstants.Types.Info,
                    NotificationConstants.Categories.Application,
                    application.Id,
                    ApplicationConstants.EntityTypes.Application
                );

                return WorkflowResult.Ok("Application marked as under review");

            });
        }

        /// <summary>
        /// Initiates background and/or credit screening for an application.
        /// Requires application fee to be paid.
        /// </summary>
        public async Task<WorkflowResult<ApplicationScreening>> InitiateScreeningAsync(
            Guid applicationId,
            bool requestBackgroundCheck,
            bool requestCreditCheck)
        {
            return await ExecuteWorkflowAsync<ApplicationScreening>(async () =>
            {
                var application = await GetApplicationAsync(applicationId);
                if (application == null)
                    return WorkflowResult<ApplicationScreening>.Fail("Application not found");

                var userId = await GetCurrentUserIdAsync();
                var orgId = await GetActiveOrganizationIdAsync();

                // Auto-transition from Submitted to UnderReview if needed
                if (application.Status == ApplicationConstants.ApplicationStatuses.Submitted)
                {
                    application.Status = ApplicationConstants.ApplicationStatuses.UnderReview;
                    application.DecisionBy = userId;
                    application.LastModifiedBy = userId;
                    application.LastModifiedOn = DateTime.UtcNow;

                    await LogTransitionAsync(
                        "RentalApplication",
                        applicationId,
                        ApplicationConstants.ApplicationStatuses.Submitted,
                        ApplicationConstants.ApplicationStatuses.UnderReview,
                        "AutoTransition-InitiateScreening");
                }

                // Validate state
                if (application.Status != ApplicationConstants.ApplicationStatuses.UnderReview)
                    return WorkflowResult<ApplicationScreening>.Fail(
                        $"Application must be Submitted or Under Review to initiate screening. Current status: {application.Status}");

                // Validate application fee paid
                if (!application.ApplicationFeePaid)
                    return WorkflowResult<ApplicationScreening>.Fail(
                        "Application fee must be paid before initiating screening");

                // Check for existing screening
                var existingScreening = await _context.ApplicationScreenings
                    .FirstOrDefaultAsync(s => s.RentalApplicationId == applicationId);
                
                if (existingScreening != null)
                    return WorkflowResult<ApplicationScreening>.Fail(
                        "Screening already exists for this application");

                // Create screening record
                var screening = new ApplicationScreening
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = orgId,
                    RentalApplicationId = applicationId,
                    BackgroundCheckRequested = requestBackgroundCheck,
                    BackgroundCheckRequestedOn = requestBackgroundCheck ? DateTime.UtcNow : null,
                    CreditCheckRequested = requestCreditCheck,
                    CreditCheckRequestedOn = requestCreditCheck ? DateTime.UtcNow : null,
                    OverallResult = "Pending",
                    CreatedBy = userId,
                    CreatedOn = DateTime.UtcNow
                };

                _context.ApplicationScreenings.Add(screening);

                // Update application status
                var oldStatus = application.Status;
                application.Status = ApplicationConstants.ApplicationStatuses.Screening;
                application.LastModifiedBy = userId;
                application.LastModifiedOn = DateTime.UtcNow;

                // Update prospect status
                if (application.ProspectiveTenant != null)
                {
                    application.ProspectiveTenant.Status = ApplicationConstants.ProspectiveStatuses.Screening;
                    application.ProspectiveTenant.LastModifiedBy = userId;
                    application.ProspectiveTenant.LastModifiedOn = DateTime.UtcNow;
                }

                await LogTransitionAsync(
                    "RentalApplication",
                    applicationId,
                    oldStatus,
                    application.Status,
                    "InitiateScreening");

                // notify leasing agents
                await _notificationService.NotifyAllUsersAsync(
                    application.OrganizationId,
                    $"Screening Initiated for Application ID {application.Id}",
                    $"Background and/or credit screening has been initiated for application ID {application.Id}.",
                    NotificationConstants.Types.Info,
                    NotificationConstants.Categories.Application,
                    application.Id,
                    ApplicationConstants.EntityTypes.Application
                );

                return WorkflowResult<ApplicationScreening>.Ok(
                    screening,
                    "Screening initiated successfully");

            });
        }

        /// <summary>
        /// Approves an application after screening review.
        /// Requires screening to be completed with passing result.
        /// </summary>
        public async Task<WorkflowResult> ApproveApplicationAsync(Guid applicationId)
        {
            return await ExecuteWorkflowAsync(async () =>
            {
                var application = await GetApplicationAsync(applicationId);
                if (application == null)
                    return WorkflowResult.Fail("Application not found");

                // Validate state
                if (application.Status != ApplicationConstants.ApplicationStatuses.Screening)
                    return WorkflowResult.Fail(
                        $"Application must be in Screening status to approve. Current status: {application.Status}");

                // Validate screening completed
                if (application.Screening == null)
                    return WorkflowResult.Fail("Screening record not found");

                if (application.Screening.OverallResult != "Passed" && 
                    application.Screening.OverallResult != "ConditionalPass")
                    return WorkflowResult.Fail(
                        $"Cannot approve application with screening result: {application.Screening.OverallResult}");

                var userId = await GetCurrentUserIdAsync();
                var oldStatus = application.Status;

                // Update application
                application.Status = ApplicationConstants.ApplicationStatuses.Approved;
                application.DecidedOn = DateTime.UtcNow;
                application.DecisionBy = userId;
                application.LastModifiedBy = userId;
                application.LastModifiedOn = DateTime.UtcNow;

                // Update prospect
                if (application.ProspectiveTenant != null)
                {
                    application.ProspectiveTenant.Status = ApplicationConstants.ProspectiveStatuses.Approved;
                    application.ProspectiveTenant.LastModifiedBy = userId;
                    application.ProspectiveTenant.LastModifiedOn = DateTime.UtcNow;
                }

                await LogTransitionAsync(
                    "RentalApplication",
                    applicationId,
                    oldStatus,
                    application.Status,
                    "ApproveApplication");


                // send notification to leasing agents
                await _notificationService.NotifyAllUsersAsync(
                    application.OrganizationId,
                    $"Application Approved for {application.Property?.Address}",
                    $"Application ID {application.Id} has been approved.",
                    NotificationConstants.Types.Info,
                    NotificationConstants.Categories.Application,
                    application.Id,
                    ApplicationConstants.EntityTypes.Application
                );

                return WorkflowResult.Ok("Application approved successfully");

            });
        }

        /// <summary>
        /// Denies an application with a required reason.
        /// Rolls back property status if no other pending applications exist.
        /// </summary>
        public async Task<WorkflowResult> DenyApplicationAsync(Guid applicationId, string denialReason)
        {
            return await ExecuteWorkflowAsync(async () =>
            {
                if (string.IsNullOrWhiteSpace(denialReason))
                    return WorkflowResult.Fail("Denial reason is required");

                var application = await GetApplicationAsync(applicationId);
                if (application == null)
                    return WorkflowResult.Fail("Application not found");

                // Validate not already in terminal state
                var terminalStates = new[] {
                    ApplicationConstants.ApplicationStatuses.Denied,
                    ApplicationConstants.ApplicationStatuses.LeaseAccepted,
                    ApplicationConstants.ApplicationStatuses.Withdrawn
                };

                if (terminalStates.Contains(application.Status))
                    return WorkflowResult.Fail(
                        $"Cannot deny application in {application.Status} status");

                var userId = await GetCurrentUserIdAsync();
                var oldStatus = application.Status;

                // Update application
                application.Status = ApplicationConstants.ApplicationStatuses.Denied;
                application.DenialReason = denialReason;
                application.DecidedOn = DateTime.UtcNow;
                application.DecisionBy = userId;
                application.LastModifiedBy = userId;
                application.LastModifiedOn = DateTime.UtcNow;

                // Update prospect
                if (application.ProspectiveTenant != null)
                {
                    application.ProspectiveTenant.Status = ApplicationConstants.ProspectiveStatuses.Denied;
                    application.ProspectiveTenant.LastModifiedBy = userId;
                    application.ProspectiveTenant.LastModifiedOn = DateTime.UtcNow;
                }

                // Check if property status should roll back (exclude this application which is being denied)
                await RollbackPropertyStatusIfNeededAsync(application.PropertyId, excludeApplicationId: applicationId);

                await LogTransitionAsync(
                    "RentalApplication",
                    applicationId,
                    oldStatus,
                    application.Status,
                    "DenyApplication",
                    denialReason);

                // send notification to leasing agents
                await _notificationService.NotifyAllUsersAsync(
                    application.OrganizationId,
                    $"Application Denied for {application.Property?.Address}",
                    $"Application ID {application.Id} has been denied. Reason: {denialReason}",
                    NotificationConstants.Types.Warning,
                    NotificationConstants.Categories.Application,
                    application.Id,
                    ApplicationConstants.EntityTypes.Application
                );

                return WorkflowResult.Ok("Application denied");

            });
        }

        /// <summary>
        /// Withdraws an application (initiated by prospect).
        /// Rolls back property status if no other pending applications exist.
        /// </summary>
        public async Task<WorkflowResult> WithdrawApplicationAsync(Guid applicationId, string withdrawalReason)
        {
            return await ExecuteWorkflowAsync(async () =>
            {
                if (string.IsNullOrWhiteSpace(withdrawalReason))
                    return WorkflowResult.Fail("Withdrawal reason is required");

                var application = await GetApplicationAsync(applicationId);
                if (application == null)
                    return WorkflowResult.Fail("Application not found");

                // Validate in active state
                var activeStates = new[] {
                    ApplicationConstants.ApplicationStatuses.Submitted,
                    ApplicationConstants.ApplicationStatuses.UnderReview,
                    ApplicationConstants.ApplicationStatuses.Screening,
                    ApplicationConstants.ApplicationStatuses.Approved,
                    ApplicationConstants.ApplicationStatuses.LeaseOffered
                };

                if (!activeStates.Contains(application.Status))
                    return WorkflowResult.Fail(
                        $"Cannot withdraw application in {application.Status} status");

                var userId = await GetCurrentUserIdAsync();
                var oldStatus = application.Status;

                // Update application
                application.Status = ApplicationConstants.ApplicationStatuses.Withdrawn;
                application.DenialReason = withdrawalReason; // Reuse field
                application.DecidedOn = DateTime.UtcNow;
                application.DecisionBy = userId;
                application.LastModifiedBy = userId;
                application.LastModifiedOn = DateTime.UtcNow;

                // Update prospect
                if (application.ProspectiveTenant != null)
                {
                    application.ProspectiveTenant.Status = ApplicationConstants.ProspectiveStatuses.Withdrawn;
                    application.ProspectiveTenant.LastModifiedBy = userId;
                    application.ProspectiveTenant.LastModifiedOn = DateTime.UtcNow;
                }

                // Check if property status should roll back (exclude this application which is being withdrawn)
                await RollbackPropertyStatusIfNeededAsync(application.PropertyId, excludeApplicationId: applicationId);

                await LogTransitionAsync(
                    "RentalApplication",
                    applicationId,
                    oldStatus,
                    application.Status,
                    "WithdrawApplication",
                    withdrawalReason);

                // send notification to leasing agents
                await _notificationService.NotifyAllUsersAsync(
                    application.OrganizationId,
                    $"Application Withdrawn for {application.Property?.Address}",
                    $"Application ID {application.Id} has been withdrawn by the prospect. Reason: {withdrawalReason}",
                    NotificationConstants.Types.Warning,
                    NotificationConstants.Categories.Application,
                    application.Id,
                    ApplicationConstants.EntityTypes.Application
                );

                return WorkflowResult.Ok("Application withdrawn");

            });
        }

        /// <summary>
        /// Updates screening results after background/credit checks are completed.
        /// Does not automatically approve - requires manual ApproveApplicationAsync call.
        /// </summary>
        public async Task<WorkflowResult> CompleteScreeningAsync(
            Guid applicationId,
            ScreeningResultModel results)
        {
            return await ExecuteWorkflowAsync(async () =>
            {
                var application = await GetApplicationAsync(applicationId);
                if (application == null)
                    return WorkflowResult.Fail("Application not found");

                if (application.Status != ApplicationConstants.ApplicationStatuses.Screening)
                    return WorkflowResult.Fail(
                        $"Application must be in Screening status. Current status: {application.Status}");

                if (application.Screening == null)
                    return WorkflowResult.Fail("Screening record not found");

                var userId = await GetCurrentUserIdAsync();

                // Update screening results
                var screening = application.Screening;

                if (results.BackgroundCheckPassed.HasValue)
                {
                    screening.BackgroundCheckPassed = results.BackgroundCheckPassed;
                    screening.BackgroundCheckCompletedOn = DateTime.UtcNow;
                    screening.BackgroundCheckNotes = results.BackgroundCheckNotes;
                }

                if (results.CreditCheckPassed.HasValue)
                {
                    screening.CreditCheckPassed = results.CreditCheckPassed;
                    screening.CreditScore = results.CreditScore;
                    screening.CreditCheckCompletedOn = DateTime.UtcNow;
                    screening.CreditCheckNotes = results.CreditCheckNotes;
                }

                screening.OverallResult = results.OverallResult;
                screening.ResultNotes = results.ResultNotes;
                screening.LastModifiedBy = userId;
                screening.LastModifiedOn = DateTime.UtcNow;

                await LogTransitionAsync(
                    "ApplicationScreening",
                    screening.Id,
                    "Pending",
                    screening.OverallResult,
                    "CompleteScreening",
                    results.ResultNotes);

                // notify leasing agents
                await _notificationService.NotifyAllUsersAsync(
                    application.OrganizationId,
                    $"Screening Results Updated for Application ID {application.Id}",
                    $"Screening results have been updated for application ID {application.Id}. Overall Result: {screening.OverallResult}",
                    NotificationConstants.Types.Info,
                    NotificationConstants.Categories.Application,
                    application.Id,
                    ApplicationConstants.EntityTypes.Application
                );

                return WorkflowResult.Ok("Screening results updated successfully");

            });
        }

        /// <summary>
        /// Generates a lease offer for an approved application.
        /// Creates LeaseOffer entity, updates property to LeasePending, and denies competing applications.
        /// </summary>
        public async Task<WorkflowResult<LeaseOffer>> GenerateLeaseOfferAsync(
            Guid applicationId,
            LeaseOfferModel model)
        {
            return await ExecuteWorkflowAsync<LeaseOffer>(async () =>
            {
                var application = await GetApplicationAsync(applicationId);
                if (application == null)
                    return WorkflowResult<LeaseOffer>.Fail("Application not found");

                // Validate application approved
                if (application.Status != ApplicationConstants.ApplicationStatuses.Approved)
                    return WorkflowResult<LeaseOffer>.Fail(
                        $"Application must be Approved to generate lease offer. Current status: {application.Status}");

                // Validate property not already leased
                var property = application.Property;
                if (property == null)
                    return WorkflowResult<LeaseOffer>.Fail("Property not found");

                if (property.Status == ApplicationConstants.PropertyStatuses.Occupied)
                    return WorkflowResult<LeaseOffer>.Fail("Property is already occupied");

                // Validate lease dates
                if (model.StartDate >= model.EndDate)
                    return WorkflowResult<LeaseOffer>.Fail("End date must be after start date");

                if (model.StartDate < DateTime.Today)
                    return WorkflowResult<LeaseOffer>.Fail("Start date cannot be in the past");

                if (model.MonthlyRent <= 0 || model.SecurityDeposit < 0)
                    return WorkflowResult<LeaseOffer>.Fail("Invalid rent or deposit amount");

                var userId = await GetCurrentUserIdAsync();
                var orgId = await GetActiveOrganizationIdAsync();

                // Create lease offer
                var leaseOffer = new LeaseOffer
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = orgId,
                    RentalApplicationId = applicationId,
                    PropertyId = property.Id,
                    ProspectiveTenantId = application.ProspectiveTenantId,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate,
                    MonthlyRent = model.MonthlyRent,
                    SecurityDeposit = model.SecurityDeposit,
                    Terms = model.Terms,
                    Notes = model.Notes ?? string.Empty,
                    OfferedOn = DateTime.UtcNow,
                    ExpiresOn = DateTime.UtcNow.AddDays(30),
                    Status = "Pending",
                    CreatedBy = userId,
                    CreatedOn = DateTime.UtcNow
                };

                _context.LeaseOffers.Add(leaseOffer);
                // Note: EF Core will assign ID when transaction commits

                // Update application
                var oldAppStatus = application.Status;
                application.Status = ApplicationConstants.ApplicationStatuses.LeaseOffered;
                application.LastModifiedBy = userId;
                application.LastModifiedOn = DateTime.UtcNow;

                // Update prospect
                if (application.ProspectiveTenant != null)
                {
                    application.ProspectiveTenant.Status = ApplicationConstants.ProspectiveStatuses.LeaseOffered;
                    application.ProspectiveTenant.LastModifiedBy = userId;
                    application.ProspectiveTenant.LastModifiedOn = DateTime.UtcNow;
                }

                // Update property to LeasePending
                property.Status = ApplicationConstants.PropertyStatuses.LeasePending;
                property.LastModifiedBy = userId;
                property.LastModifiedOn = DateTime.UtcNow;

                // Deny all competing applications
                var competingApps = await _context.RentalApplications
                    .Where(a => a.PropertyId == property.Id &&
                               a.Id != applicationId &&
                               a.OrganizationId == orgId &&
                               (a.Status == ApplicationConstants.ApplicationStatuses.Submitted ||
                                a.Status == ApplicationConstants.ApplicationStatuses.UnderReview ||
                                a.Status == ApplicationConstants.ApplicationStatuses.Screening ||
                                a.Status == ApplicationConstants.ApplicationStatuses.Approved) &&
                               !a.IsDeleted)
                    .Include(a => a.ProspectiveTenant)
                    .ToListAsync();

                foreach (var competingApp in competingApps)
                {
                    competingApp.Status = ApplicationConstants.ApplicationStatuses.Denied;
                    competingApp.DenialReason = "Property leased to another applicant";
                    competingApp.DecidedOn = DateTime.UtcNow;
                    competingApp.DecisionBy = userId;
                    competingApp.LastModifiedBy = userId;
                    competingApp.LastModifiedOn = DateTime.UtcNow;

                    if (competingApp.ProspectiveTenant != null)
                    {
                        competingApp.ProspectiveTenant.Status = ApplicationConstants.ProspectiveStatuses.Denied;
                        competingApp.ProspectiveTenant.LastModifiedBy = userId;
                        competingApp.ProspectiveTenant.LastModifiedOn = DateTime.UtcNow;
                    }

                    await LogTransitionAsync(
                        "RentalApplication",
                        competingApp.Id,
                        competingApp.Status,
                        ApplicationConstants.ApplicationStatuses.Denied,
                        "DenyCompetingApplication",
                        "Property leased to another applicant");
                }

                await LogTransitionAsync(
                    "RentalApplication",
                    applicationId,
                    oldAppStatus,
                    application.Status,
                    "GenerateLeaseOffer");

                await LogTransitionAsync(
                    "LeaseOffer",
                    leaseOffer.Id,
                    null,
                    "Pending",
                    "GenerateLeaseOffer");

                return WorkflowResult<LeaseOffer>.Ok(
                    leaseOffer,
                    $"Lease offer generated successfully. {competingApps.Count} competing application(s) denied.");

            });
        }

        /// <summary>
        /// Accepts a lease offer and converts prospect to tenant.
        /// Creates Tenant and Lease entities, updates property to Occupied.
        /// Records security deposit payment.
        /// </summary>
        public async Task<WorkflowResult<Lease>> AcceptLeaseOfferAsync(
            Guid leaseOfferId,
            string depositPaymentMethod,
            DateTime depositPaymentDate,
            string? depositReferenceNumber = null,
            string? depositNotes = null)
        {
            return await ExecuteWorkflowAsync<Lease>(async () =>
            {
                var orgId = await GetActiveOrganizationIdAsync();
                var userId = await GetCurrentUserIdAsync();

                var leaseOffer = await _context.LeaseOffers
                    .Include(lo => lo.RentalApplication)
                        .ThenInclude(a => a.ProspectiveTenant)
                    .Include(lo => lo.Property)
                    .FirstOrDefaultAsync(lo => lo.Id == leaseOfferId &&
                                              lo.OrganizationId == orgId &&
                                              !lo.IsDeleted);

                if (leaseOffer == null)
                    return WorkflowResult<Lease>.Fail("Lease offer not found");

                if (leaseOffer.Status != "Pending")
                    return WorkflowResult<Lease>.Fail($"Lease offer status is {leaseOffer.Status}, not Pending");

                if (leaseOffer.ExpiresOn < DateTime.UtcNow)
                    return WorkflowResult<Lease>.Fail("Lease offer has expired");

                var prospect = leaseOffer.RentalApplication?.ProspectiveTenant;
                if (prospect == null)
                    return WorkflowResult<Lease>.Fail("Prospective tenant not found");

                // Convert prospect to tenant
                var tenant = new Tenant
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = orgId,
                    FirstName = prospect.FirstName,
                    LastName = prospect.LastName,
                    Email = prospect.Email,
                    PhoneNumber = prospect.Phone,
                    DateOfBirth = prospect.DateOfBirth,
                    IdentificationNumber = prospect.IdentificationNumber ?? $"ID-{Guid.NewGuid().ToString("N")[..8]}",
                    ProspectiveTenantId = prospect.Id,
                    IsActive = true,
                    CreatedBy = userId,
                    CreatedOn = DateTime.UtcNow
                };

                _context.Tenants.Add(tenant);
                // Note: EF Core will assign ID when transaction commits

                // Create lease
                var lease = new Lease
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = orgId,
                    PropertyId = leaseOffer.PropertyId,
                    Tenant = tenant, // Use navigation property instead of TenantId
                    LeaseOfferId = leaseOffer.Id,
                    StartDate = leaseOffer.StartDate,
                    EndDate = leaseOffer.EndDate,
                    MonthlyRent = leaseOffer.MonthlyRent,
                    SecurityDeposit = leaseOffer.SecurityDeposit,
                    Terms = leaseOffer.Terms,
                    Status = ApplicationConstants.LeaseStatuses.Accepted,
                    IsActive = true,
                    SignedOn = DateTime.UtcNow,
                    CreatedBy = userId,
                    CreatedOn = DateTime.UtcNow
                };

                _context.Leases.Add(lease);
                // Note: EF Core will assign ID when transaction commits

                // Create security deposit record
                var securityDeposit = new SecurityDeposit
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = orgId,
                    Lease = lease, // Use navigation property
                    Tenant = tenant, // Use navigation property
                    Amount = leaseOffer.SecurityDeposit,
                    DateReceived = depositPaymentDate,
                    PaymentMethod = depositPaymentMethod,
                    TransactionReference = depositReferenceNumber,
                    Status = "Held",
                    InInvestmentPool = true,
                    PoolEntryDate = leaseOffer.StartDate,
                    Notes = depositNotes,
                    CreatedBy = userId,
                    CreatedOn = DateTime.UtcNow
                };

                _context.SecurityDeposits.Add(securityDeposit);

                // Update lease offer
                leaseOffer.Status = "Accepted";
                leaseOffer.RespondedOn = DateTime.UtcNow;
                leaseOffer.ConvertedLeaseId = lease.Id;
                leaseOffer.LastModifiedBy = userId;
                leaseOffer.LastModifiedOn = DateTime.UtcNow;

                // Update application
                var application = leaseOffer.RentalApplication;
                if (application != null)
                {
                    application.Status = ApplicationConstants.ApplicationStatuses.LeaseAccepted;
                    application.LastModifiedBy = userId;
                    application.LastModifiedOn = DateTime.UtcNow;
                }

                // Update prospect
                prospect.Status = ApplicationConstants.ProspectiveStatuses.ConvertedToTenant;
                prospect.LastModifiedBy = userId;
                prospect.LastModifiedOn = DateTime.UtcNow;

                // Update property
                var property = leaseOffer.Property;
                if (property != null)
                {
                    property.Status = ApplicationConstants.PropertyStatuses.Occupied;
                    property.LastModifiedBy = userId;
                    property.LastModifiedOn = DateTime.UtcNow;
                }

                await LogTransitionAsync(
                    "LeaseOffer",
                    leaseOfferId,
                    "Pending",
                    "Accepted",
                    "AcceptLeaseOffer");

                await LogTransitionAsync(
                    "ProspectiveTenant",
                    prospect.Id,
                    ApplicationConstants.ProspectiveStatuses.LeaseOffered,
                    ApplicationConstants.ProspectiveStatuses.ConvertedToTenant,
                    "AcceptLeaseOffer");

                // Add note if lease start date is in the future
                if (leaseOffer.StartDate > DateTime.Today)
                {
                    var noteContent = $"Lease accepted on {DateTime.Today:MMM dd, yyyy}. Lease start date: {leaseOffer.StartDate:MMM dd, yyyy}.";
                    await _noteService.AddNoteAsync(ApplicationConstants.EntityTypes.Lease, lease.Id, noteContent);
                }

                // send notification to leasing agents
                await _notificationService.NotifyAllUsersAsync(
                    orgId,
                    $"Lease Offer Accepted for {leaseOffer.Property?.Address}",
                    $"Lease offer ID {leaseOffer.Id} has been accepted and tenant {tenant.FirstName} {tenant.LastName} has been created.",
                    NotificationConstants.Types.Success,
                    NotificationConstants.Categories.Lease,
                    lease.Id,
                    ApplicationConstants.EntityTypes.Lease
                );

                return WorkflowResult<Lease>.Ok(lease, "Lease offer accepted and tenant created successfully");

            });
        }

        /// <summary>
        /// Declines a lease offer.
        /// Rolls back property status and marks prospect as lease declined.
        /// </summary>
        public async Task<WorkflowResult> DeclineLeaseOfferAsync(Guid leaseOfferId, string declineReason)
        {
            return await ExecuteWorkflowAsync(async () =>
            {
                if (string.IsNullOrWhiteSpace(declineReason))
                    return WorkflowResult.Fail("Decline reason is required");

                var orgId = await GetActiveOrganizationIdAsync();
                var userId = await GetCurrentUserIdAsync();

                var leaseOffer = await _context.LeaseOffers
                    .Include(lo => lo.RentalApplication)
                        .ThenInclude(a => a.ProspectiveTenant)
                    .Include(lo => lo.Property)
                    .FirstOrDefaultAsync(lo => lo.Id == leaseOfferId &&
                                              lo.OrganizationId == orgId &&
                                              !lo.IsDeleted);

                if (leaseOffer == null)
                    return WorkflowResult.Fail("Lease offer not found");

                if (leaseOffer.Status != "Pending")
                    return WorkflowResult.Fail($"Lease offer status is {leaseOffer.Status}, not Pending");

                // Update lease offer
                leaseOffer.Status = "Declined";
                leaseOffer.RespondedOn = DateTime.UtcNow;
                leaseOffer.ResponseNotes = declineReason;
                leaseOffer.LastModifiedBy = userId;
                leaseOffer.LastModifiedOn = DateTime.UtcNow;

                // Update application
                var application = leaseOffer.RentalApplication;
                if (application != null)
                {
                    application.Status = ApplicationConstants.ApplicationStatuses.LeaseDeclined;
                    application.LastModifiedBy = userId;
                    application.LastModifiedOn = DateTime.UtcNow;

                    // Update prospect
                    if (application.ProspectiveTenant != null)
                    {
                        application.ProspectiveTenant.Status = ApplicationConstants.ProspectiveStatuses.LeaseDeclined;
                        application.ProspectiveTenant.LastModifiedBy = userId;
                        application.ProspectiveTenant.LastModifiedOn = DateTime.UtcNow;
                    }
                }

                // Rollback property status (exclude this lease offer which is being declined and the application being updated)
                await RollbackPropertyStatusIfNeededAsync(
                    leaseOffer.PropertyId, 
                    excludeApplicationId: application?.Id,
                    excludeLeaseOfferId: leaseOfferId);

                await LogTransitionAsync(
                    "LeaseOffer",
                    leaseOfferId,
                    "Pending",
                    "Declined",
                    "DeclineLeaseOffer",
                    declineReason);

                // send notification to leasing agents
                await _notificationService.NotifyAllUsersAsync(
                    orgId,
                    $"Lease Offer Declined for {leaseOffer.Property?.Address}",
                    $"Lease offer ID {leaseOffer.Id} has been declined. Reason: {declineReason}",
                    NotificationConstants.Types.Warning,
                    NotificationConstants.Categories.Lease,
                    leaseOffer.Id,
                    ApplicationConstants.EntityTypes.Lease
                );

                return WorkflowResult.Ok("Lease offer declined");

            });
        }

        /// <summary>
        /// Expires a lease offer (called by scheduled task).
        /// Similar to decline but automated.
        /// </summary>
        public async Task<WorkflowResult> ExpireLeaseOfferAsync(Guid leaseOfferId, Guid? organizationId = null)
        {
            return await ExecuteWorkflowAsync(async () =>
            {
                var orgId = organizationId ?? await GetActiveOrganizationIdAsync();
                var userId = await GetCurrentUserIdAsync();
                if (string.IsNullOrEmpty(userId)) userId = "System";

                var leaseOffer = await _context.LeaseOffers
                    .Include(lo => lo.RentalApplication)
                        .ThenInclude(a => a.ProspectiveTenant)
                    .Include(lo => lo.Property)
                    .FirstOrDefaultAsync(lo => lo.Id == leaseOfferId &&
                                              lo.OrganizationId == orgId &&
                                              !lo.IsDeleted);

                if (leaseOffer == null)
                    return WorkflowResult.Fail("Lease offer not found");

                if (leaseOffer.Status != "Pending")
                    return WorkflowResult.Fail($"Lease offer status is {leaseOffer.Status}, not Pending");

                if (leaseOffer.ExpiresOn >= DateTime.UtcNow)
                    return WorkflowResult.Fail("Lease offer has not expired yet");

                // Update lease offer
                leaseOffer.Status = "Expired";
                leaseOffer.RespondedOn = DateTime.UtcNow;
                leaseOffer.LastModifiedBy = userId;
                leaseOffer.LastModifiedOn = DateTime.UtcNow;

                // Update application
                var application = leaseOffer.RentalApplication;
                if (application != null)
                {
                    application.Status = ApplicationConstants.ApplicationStatuses.Expired;
                    application.LastModifiedBy = userId;
                    application.LastModifiedOn = DateTime.UtcNow;

                    // Update prospect
                    if (application.ProspectiveTenant != null)
                    {
                        application.ProspectiveTenant.Status = ApplicationConstants.ProspectiveStatuses.LeaseDeclined;
                        application.ProspectiveTenant.LastModifiedBy = userId;
                        application.ProspectiveTenant.LastModifiedOn = DateTime.UtcNow;
                    }
                }

                // Rollback property status (exclude this lease offer which is expiring and the application being updated)
                await RollbackPropertyStatusIfNeededAsync(
                    leaseOffer.PropertyId, 
                    excludeApplicationId: application?.Id,
                    excludeLeaseOfferId: leaseOfferId);

                await LogTransitionAsync(
                    "LeaseOffer",
                    leaseOfferId,
                    "Pending",
                    "Expired",
                    "ExpireLeaseOffer",
                    "Offer expired after 30 days",
                    organizationId: orgId);

                // send notification to leasing agents
                await _notificationService.SendNotificationAsync(
                    userId,
                    $"Lease Offer Expired for {leaseOffer.Property?.Address}",
                    $"Lease offer ID {leaseOffer.Id} has expired after 30 days.",
                    NotificationConstants.Types.Info,
                    NotificationConstants.Categories.Lease,
                    leaseOffer.Id,
                    ApplicationConstants.EntityTypes.Lease
                );

                return WorkflowResult.Ok("Lease offer expired");

            });
        }

        #endregion

        #region Helper Methods

        private async Task<RentalApplication?> GetApplicationAsync(Guid applicationId)
        {
            var orgId = await GetActiveOrganizationIdAsync();
            return await _context.RentalApplications
                .Include(a => a.ProspectiveTenant)
                .Include(a => a.Property)
                .Include(a => a.Screening)
                .FirstOrDefaultAsync(a =>
                    a.Id == applicationId &&
                    a.OrganizationId == orgId &&
                    !a.IsDeleted);
        }

        private async Task<WorkflowResult> ValidateApplicationSubmissionAsync(
            Guid prospectId,
            Guid propertyId)
        {
            var errors = new List<string>();
            var orgId = await GetActiveOrganizationIdAsync();

            // Validate prospect exists
            var prospect = await _context.ProspectiveTenants
                .FirstOrDefaultAsync(p => p.Id == prospectId && p.OrganizationId == orgId && !p.IsDeleted);
            
            if (prospect == null)
                errors.Add("Prospect not found");
            else if (prospect.Status == ApplicationConstants.ProspectiveStatuses.ConvertedToTenant)
                errors.Add("Prospect has already been converted to a tenant");

            // Validate property exists and is available
            var property = await _context.Properties
                .FirstOrDefaultAsync(p => p.Id == propertyId && p.OrganizationId == orgId && !p.IsDeleted);
            
            if (property == null)
                errors.Add("Property not found");
            else if (property.Status == ApplicationConstants.PropertyStatuses.Occupied)
                errors.Add("Property is currently occupied");

            // Check for existing active application by identification number and state
            // A prospect can have multiple applications over time, but only one "active" (non-disposed) application
            if (prospect != null && !string.IsNullOrEmpty(prospect.IdentificationNumber) && !string.IsNullOrEmpty(prospect.IdentificationState))
            {
                // Terminal/disposed statuses - application is no longer active
                var disposedStatuses = new[] {
                    ApplicationConstants.ApplicationStatuses.Approved,
                    ApplicationConstants.ApplicationStatuses.Denied,
                    ApplicationConstants.ApplicationStatuses.Withdrawn,
                    ApplicationConstants.ApplicationStatuses.Expired,
                    ApplicationConstants.ApplicationStatuses.LeaseDeclined,
                    ApplicationConstants.ApplicationStatuses.LeaseAccepted
                };

                var existingActiveApp = await _context.RentalApplications
                    .Include(a => a.ProspectiveTenant)
                    .AnyAsync(a =>
                        a.ProspectiveTenant != null &&
                        a.ProspectiveTenant.IdentificationNumber == prospect.IdentificationNumber &&
                        a.ProspectiveTenant.IdentificationState == prospect.IdentificationState &&
                        a.OrganizationId == orgId &&
                        !disposedStatuses.Contains(a.Status) &&
                        !a.IsDeleted);

                if (existingActiveApp)
                    errors.Add("An active application already exists for this identification");
            }

            return errors.Any()
                ? WorkflowResult.Fail(errors)
                : WorkflowResult.Ok();
        }

        /// <summary>
        /// Checks if property status should roll back when an application is denied/withdrawn.
        /// Rolls back to Available if no active applications or pending lease offers remain.
        /// </summary>
        /// <param name="propertyId">The property to check</param>
        /// <param name="excludeApplicationId">Optional application ID to exclude from the active apps check (for the app being denied/withdrawn)</param>
        /// <param name="excludeLeaseOfferId">Optional lease offer ID to exclude from the pending offers check (for the offer being declined)</param>
        private async Task RollbackPropertyStatusIfNeededAsync(
            Guid propertyId, 
            Guid? excludeApplicationId = null,
            Guid? excludeLeaseOfferId = null)
        {
            var orgId = await GetActiveOrganizationIdAsync();
            var userId = await GetCurrentUserIdAsync();

            // Get all active applications for this property
            var activeStates = new[] {
                ApplicationConstants.ApplicationStatuses.Submitted,
                ApplicationConstants.ApplicationStatuses.UnderReview,
                ApplicationConstants.ApplicationStatuses.Screening,
                ApplicationConstants.ApplicationStatuses.Approved,
                ApplicationConstants.ApplicationStatuses.LeaseOffered
            };

            var hasActiveApplications = await _context.RentalApplications
                .AnyAsync(a =>
                    a.PropertyId == propertyId &&
                    a.OrganizationId == orgId &&
                    activeStates.Contains(a.Status) &&
                    (excludeApplicationId == null || a.Id != excludeApplicationId) &&
                    !a.IsDeleted);

            // Also check for pending lease offers
            var hasPendingLeaseOffers = await _context.LeaseOffers
                .AnyAsync(lo =>
                    lo.PropertyId == propertyId &&
                    lo.OrganizationId == orgId &&
                    lo.Status == "Pending" &&
                    (excludeLeaseOfferId == null || lo.Id != excludeLeaseOfferId) &&
                    !lo.IsDeleted);

            // If no active applications or pending lease offers remain, roll back property to Available
            if (!hasActiveApplications && !hasPendingLeaseOffers)
            {
                var property = await _context.Properties
                    .FirstOrDefaultAsync(p => p.Id == propertyId && p.OrganizationId == orgId);

                if (property != null && 
                    (property.Status == ApplicationConstants.PropertyStatuses.ApplicationPending ||
                     property.Status == ApplicationConstants.PropertyStatuses.LeasePending))
                {
                    property.Status = ApplicationConstants.PropertyStatuses.Available;
                    property.LastModifiedBy = userId;
                    property.LastModifiedOn = DateTime.UtcNow;
                }
            }
        }

        #endregion

        /// <summary>
        /// Returns a comprehensive view of the application's workflow state,
        /// including related prospect, property, screening, lease offers, and audit history.
        /// </summary>
        public async Task<ApplicationWorkflowState> GetApplicationWorkflowStateAsync(Guid applicationId)
        {
            var orgId = await GetActiveOrganizationIdAsync();

            var application = await _context.RentalApplications
                .Include(a => a.ProspectiveTenant)
                .Include(a => a.Property)
                .Include(a => a.Screening)
                .FirstOrDefaultAsync(a => a.Id == applicationId && a.OrganizationId == orgId && !a.IsDeleted);

            if (application == null)
                return new ApplicationWorkflowState
                {
                    Application = null,
                    AuditHistory = new List<WorkflowAuditLog>(),
                    LeaseOffers = new List<LeaseOffer>()
                };

            var leaseOffers = await _context.LeaseOffers
                .Where(lo => lo.RentalApplicationId == applicationId && lo.OrganizationId == orgId && !lo.IsDeleted)
                .OrderByDescending(lo => lo.OfferedOn)
                .ToListAsync();

            var auditHistory = await _context.WorkflowAuditLogs
                .Where(w => w.EntityType == "RentalApplication" && w.EntityId == applicationId && w.OrganizationId == orgId)
                .OrderByDescending(w => w.PerformedOn)
                .ToListAsync();

            return new ApplicationWorkflowState
            {
                Application = application,
                Prospect = application.ProspectiveTenant,
                Property = application.Property,
                Screening = application.Screening,
                LeaseOffers = leaseOffers,
                AuditHistory = auditHistory
            };
        }
    }

    /// <summary>
    /// Model for application submission data.
    /// </summary>
    public class ApplicationSubmissionModel
    {
        public decimal ApplicationFee { get; set; }
        public bool ApplicationFeePaid { get; set; }
        public string? ApplicationFeePaymentMethod { get; set; }

        public string CurrentAddress { get; set; } = string.Empty;
        public string CurrentCity { get; set; } = string.Empty;
        public string CurrentState { get; set; } = string.Empty;
        public string CurrentZipCode { get; set; } = string.Empty;
        public decimal CurrentRent { get; set; }
        public string LandlordName { get; set; } = string.Empty;
        public string LandlordPhone { get; set; } = string.Empty;

        public string EmployerName { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public decimal MonthlyIncome { get; set; }
        public int EmploymentLengthMonths { get; set; }

        public string Reference1Name { get; set; } = string.Empty;
        public string Reference1Phone { get; set; } = string.Empty;
        public string Reference1Relationship { get; set; } = string.Empty;
        public string? Reference2Name { get; set; }
        public string? Reference2Phone { get; set; }
        public string? Reference2Relationship { get; set; }
    }

    /// <summary>
    /// Model for screening results update.
    /// </summary>
    public class ScreeningResultModel
    {
        public bool? BackgroundCheckPassed { get; set; }
        public string? BackgroundCheckNotes { get; set; }

        public bool? CreditCheckPassed { get; set; }
        public int? CreditScore { get; set; }
        public string? CreditCheckNotes { get; set; }

        public string OverallResult { get; set; } = "Pending"; // Pending, Passed, Failed, ConditionalPass
        public string? ResultNotes { get; set; }
    }

    /// <summary>
    /// Model for lease offer generation.
    /// </summary>
    public class LeaseOfferModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal MonthlyRent { get; set; }
        public decimal SecurityDeposit { get; set; }
        public string Terms { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Aggregated workflow state returned by GetApplicationWorkflowStateAsync.
    /// </summary>
    public class ApplicationWorkflowState
    {
        public RentalApplication? Application { get; set; }
        public ProspectiveTenant? Prospect { get; set; }
        public Property? Property { get; set; }
        public ApplicationScreening? Screening { get; set; }
        public List<LeaseOffer> LeaseOffers { get; set; } = new();
        public List<WorkflowAuditLog> AuditHistory { get; set; } = new();
    }
}
