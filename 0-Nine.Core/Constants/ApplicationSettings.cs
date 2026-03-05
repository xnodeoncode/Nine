namespace Nine.Core.Constants
{
    public class ApplicationSettings
    {
        public string AppName { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public string Copyright { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Website { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Repository { get; set; } = string.Empty;
        public string DatabaseFileName { get; set; } = string.Empty;
        public string PreviousDatabaseFileName { get; set; } = string.Empty;
        public bool SoftDeleteEnabled { get; set; }
        public string SchemaVersion { get; set; } = string.Empty;
        public int MaxOrganizationUsers { get; set; } = 0; // 0 = unlimited (Professional), 3 = Nine limit
        public string License { get; set; } = string.Empty;
        public string LicenseUrl { get; set; } = string.Empty;
        public string HelpUrl { get; set; } = string.Empty;
    }

    // Property & Tenant Lifecycle Enums

    /// <summary>
    /// Property status in the rental lifecycle
    /// </summary>
    public enum PropertyStatus
    {
        Available,          // Ready to market and show
        ApplicationPending, // One or more applications under review
        LeasePending,       // Application approved, lease offered, awaiting signature
        Occupied,           // Active lease in place
        UnderRenovation,    // Not marketable, undergoing repairs/upgrades
        OffMarket          // Temporarily unavailable
    }

    /// <summary>
    /// Prospect status through the application journey
    /// </summary>
    public enum ProspectStatus
    {
        Inquiry,              // Initial contact/lead
        Contacted,            // Follow-up made
        TourScheduled,        // Tour appointment set
        Toured,               // Tour completed
        ApplicationSubmitted, // Application submitted, awaiting review
        UnderReview,          // Screening in progress
        ApplicationApproved,  // Approved, lease offer pending
        ApplicationDenied,    // Application rejected
        LeaseOffered,         // Lease document sent for signature
        LeaseSigned,          // Lease accepted and signed
        LeaseDeclined,        // Lease offer declined
        ConvertedToTenant,    // Successfully converted to tenant
        Inactive              // No longer pursuing or expired
    }

    /// <summary>
    /// Rental application status
    /// </summary>
    public enum ApplicationStatus
    {
        Pending,      // Application received, awaiting review
        UnderReview,  // Screening in progress
        Approved,     // Approved for lease
        Denied,       // Application rejected
        Expired,      // Not processed within 30 days
        Withdrawn     // Applicant withdrew
    }

    /// <summary>
    /// Lease status through its lifecycle
    /// </summary>
    public enum LeaseStatus
    {
        Offered,          // Lease generated, awaiting tenant signature
        Active,           // Signed and currently active
        Expired,          // Past end date, not renewed
        Terminated,       // Ended early or declined
        Renewed,          // Superseded by renewal lease
        MonthToMonth      // Converted to month-to-month
    }

    /// <summary>
    /// Security deposit disposition status
    /// </summary>
    public enum DepositDispositionStatus
    {
        Held,              // Currently escrowed
        PartiallyReturned, // Part returned, part withheld
        FullyReturned,     // Fully returned to tenant
        Withheld,          // Fully withheld for damages/unpaid rent
        PartiallyWithheld  // Same as PartiallyReturned (choose one)
    }

    /// <summary>
    /// Dividend payment method chosen by tenant
    /// </summary>
    public enum DividendPaymentMethod
    {
        TenantChoice,  // Not yet chosen
        LeaseCredit,   // Apply as credit to next invoice
        Check          // Send check to tenant
    }

    /// <summary>
    /// Dividend payment status
    /// </summary>
    public enum DividendPaymentStatus
    {
        Pending,          // Calculated but not yet distributed
        Applied,          // Applied as lease credit
        CheckIssued,      // Check sent to tenant
        Completed,        // Fully processed
        Forfeited         // Tenant did not claim (rare)
    }
}