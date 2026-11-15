using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nine.Core.Entities
{
    /// <summary>
    /// Organization-specific settings for late fees, payment reminders, and other configurable features.
    /// Each organization can have different policies for their property management operations.
    /// </summary>
    public class OrganizationSettings : BaseModel
    {
        [MaxLength(200)]
        public string? Name { get; set; }

        #region Late Fee Settings

        [Display(Name = "Enable Late Fees")]
        public bool LateFeeEnabled { get; set; } = true;

        [Display(Name = "Auto-Apply Late Fees")]
        public bool LateFeeAutoApply { get; set; } = true;

        [Required]
        [Range(0, 30)]
        [Display(Name = "Grace Period (Days)")]
        public int LateFeeGracePeriodDays { get; set; } = 5;

        [Required]
        [Range(0, 1)]
        [Display(Name = "Late Fee Percentage")]
        public decimal LateFeePercentage { get; set; } = 0.05m;

        [Required]
        [Range(0, 10000)]
        [Display(Name = "Maximum Late Fee Amount")]
        public decimal MaxLateFeeAmount { get; set; } = 50.00m;

        #endregion

        #region Payment Reminder Settings

        [Display(Name = "Enable Payment Reminders")]
        public bool PaymentReminderEnabled { get; set; } = true;

        [Required]
        [Range(1, 30)]
        [Display(Name = "Send Reminder (Days Before Due)")]
        public int PaymentReminderDaysBefore { get; set; } = 10;

        #endregion

        #region Tour Settings

        [Required]
        [Range(1, 168)]
        [Display(Name = "Tour No-Show Grace Period (Hours)")]
        public int TourNoShowGracePeriodHours { get; set; } = 24;

        #endregion

        #region Application Fee Settings

        [Display(Name = "Enable Application Fees")]
        public bool ApplicationFeeEnabled { get; set; } = true;

        [Required]
        [Range(0, 1000)]
        [Display(Name = "Default Application Fee")]
        public decimal DefaultApplicationFee { get; set; } = 50.00m;

        [Required]
        [Range(1, 90)]
        [Display(Name = "Application Expiration (Days)")]
        public int ApplicationExpirationDays { get; set; } = 30;

        #endregion

        #region Security Deposit Settings

        [Display(Name = "Enable Security Deposit Investment Pool")]
        public bool SecurityDepositInvestmentEnabled { get; set; } = true;

        [Required]
        [Range(0, 1)]
        [Display(Name = "Organization Share Percentage")]
        [Column(TypeName = "decimal(18,6)")]
        public decimal OrganizationSharePercentage { get; set; } = 0.20m; // Default 20%

        [Display(Name = "Auto-Calculate Security Deposit from Rent")]
        public bool AutoCalculateSecurityDeposit { get; set; } = true;

        [Required]
        [Range(0.5, 3.0)]
        [Display(Name = "Security Deposit Multiplier")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal SecurityDepositMultiplier { get; set; } = 1.0m; // Default 1x monthly rent

        [Required]
        [Range(1, 12)]
        [Display(Name = "Refund Processing Days")]
        public int RefundProcessingDays { get; set; } = 30; // Days after move-out to process refund

        [Required]
        [Range(1, 12)]
        [Display(Name = "Dividend Distribution Month")]
        public int DividendDistributionMonth { get; set; } = 1; // January = 1

        [Display(Name = "Allow Tenant Choice for Dividend Payment")]
        public bool AllowTenantDividendChoice { get; set; } = true;

        [Display(Name = "Default Dividend Payment Method")]
        [StringLength(50)]
        public string DefaultDividendPaymentMethod { get; set; } = "LeaseCredit"; // LeaseCredit or Check

        #endregion

        // Future settings can be added here as new regions:
        // - Default lease terms
        // - Routine inspection intervals
        // - Document retention policies
        // - etc.
    }
}
