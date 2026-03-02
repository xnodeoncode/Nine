using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Nine.Core.Validation;

namespace Nine.Core.Entities;

/// <summary>
/// Represents work performed on a property WITHOUT workflow/status tracking.
/// Repairs capture what work was done (or is being done) without the complexity
/// of MaintenanceRequest workflow (no Status, Priority, or Assignment).
/// Use CompletedOn to determine if work is finished (null = in progress, date = completed).
/// </summary>
public class Repair : BaseModel
{
    // Core Identity
    [RequiredGuid]
    public Guid PropertyId { get; set; }

    // Optional Relationships (Soft References)
    /// <summary>
    /// Optional: Links this repair to a MaintenanceRequest workflow (Professional product).
    /// Null for standalone repairs (Nine product).
    /// </summary>
    public Guid? MaintenanceRequestId { get; set; }

    /// <summary>
    /// Optional: Links this repair to a specific lease/tenant.
    /// </summary>
    public Guid? LeaseId { get; set; }

    // Repair Details
    [Required]
    [StringLength(200)]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    [StringLength(50)]
    [Display(Name = "Repair Type")]
    public string RepairType { get; set; } = string.Empty; // From ApplicationConstants.RepairTypes

    /// <summary>
    /// Optional: Date when work was actually completed.
    /// Null = work in progress or not yet completed.
    /// Date = work finished on this date.
    /// Since Repairs have no Status field, CompletedOn provides definitive completion date.
    /// </summary>
    [Display(Name = "Completed On")]
    public DateTime? CompletedOn { get; set; }

    // Cost & Duration
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Cost")]
    public decimal Cost { get; set; }

    [Display(Name = "Duration (Minutes)")]
    public int DurationMinutes { get; set; } // Time spent on repair

    // Who did the work
    [StringLength(100)]
    [Display(Name = "Contractor/Company Name")]
    public string ContractorName { get; set; } = string.Empty; // Company or person name (e.g., "ABC Plumbing & Heating" or "John Doe")

    [StringLength(100)]
    [Display(Name = "Contact Person")]
    public string ContactPerson { get; set; } = string.Empty; // Specific person at company (optional for companies)

    [StringLength(20)]
    [Display(Name = "Contractor Phone")]
    public string ContractorPhone { get; set; } = string.Empty; // Optional: "555-1234"

    /// <summary>
    /// Optional: Link to Contractor entity when contractor list is implemented.
    /// When null, contractor details are stored in text fields (ContractorName/ContactPerson/ContractorPhone).
    /// </summary>
    [Display(Name = "Contractor ID")]
    public Guid? ContractorId { get; set; }

    /// <summary>
    /// Future: Link to formal Contact entity when added to the system.
    /// </summary>
    public Guid? ContactId { get; set; }

    // Additional Details
    [StringLength(2000)]
    [Display(Name = "Notes")]
    public string Notes { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Parts Replaced")]
    public string PartsReplaced { get; set; } = string.Empty; // e.g., "Faucet cartridge, shower seal"

    [Display(Name = "Warranty Applies")]
    public bool WarrantyApplies { get; set; } = false;

    [Display(Name = "Warranty Expires On")]
    public DateTime? WarrantyExpiresOn { get; set; }

    // Navigation Properties
    public virtual Property? Property { get; set; }
    public virtual MaintenanceRequest? MaintenanceRequest { get; set; }
    public virtual Lease? Lease { get; set; }

    // Computed Properties
    [NotMapped]
    [Display(Name = "Duration")]
    public string DurationDisplay
    {
        get
        {
            if (DurationMinutes < 60)
                return $"{DurationMinutes} minutes";

            var hours = DurationMinutes / 60;
            var minutes = DurationMinutes % 60;
            return minutes > 0
                ? $"{hours}h {minutes}m"
                : $"{hours} hour{(hours > 1 ? "s" : "")}";
        }
    }

    [NotMapped]
    [Display(Name = "Under Warranty")]
    public bool IsUnderWarranty => WarrantyApplies &&
                                   WarrantyExpiresOn.HasValue &&
                                   WarrantyExpiresOn.Value > DateTime.Today;

    [NotMapped]
    [Display(Name = "Completed")]
    public bool IsCompleted => CompletedOn.HasValue; //Provides quick check for completion status. If no value, work is In Progess in the display.
}
