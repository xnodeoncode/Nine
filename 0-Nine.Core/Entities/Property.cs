using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Nine.Core.Constants;

namespace Nine.Core.Entities
{
    public class Property : BaseModel
    {
        [Required]
        [JsonInclude]
        [StringLength(200)]
        [DataType(DataType.Text)]
        [Display(Name = "Street Address", Description = "Street address of the property",
            Prompt = "e.g., 123 Main St", ShortName = "Address")]
        public string Address { get; set; } = string.Empty;

        [StringLength(50)]
        [JsonInclude]
        [DataType(DataType.Text)]
        [Display(Name = "Unit Number", Description = "Optional unit or apartment number",
            Prompt = "e.g., Apt 2B, Unit 101", ShortName = "Unit")]
        public string? UnitNumber { get; set; }

        [StringLength(100)]
        [JsonInclude]
        [DataType(DataType.Text)]
        [Display(Name = "City", Description = "City where the property is located",
            Prompt = "e.g., Los Angeles, New York, Chicago", ShortName = "City")]
        public string City { get; set; } = string.Empty;

        [StringLength(50)]
        [JsonInclude]
        [DataType(DataType.Text)]
        [Display(Name = "State", Description = "State or province where the property is located",
            Prompt = "e.g., CA, NY, TX", ShortName = "State")]
        public string State { get; set; } = string.Empty;

        [StringLength(10)]
        [JsonInclude]
        [RegularExpression(@"^\d{5}(-\d{4})?$", ErrorMessage = "Invalid Zip Code format.")]
        [DataType(DataType.PostalCode)]
        [Display(Name = "Postal Code", Description = "Postal code for the property",
            Prompt = "e.g., 12345 or 12345-6789", ShortName = "ZIP")]
        [MaxLength(10, ErrorMessage = "Zip Code cannot exceed 10 characters.")]
        public string ZipCode { get; set; } = string.Empty;

        [Required]
        [JsonInclude]
        [StringLength(50)]
        [DataType(DataType.Text)]
        [Display(Name = "Property Type", Description = "Type of the property",
            Prompt = "e.g., House, Apartment, Condo", ShortName = "Type")]
        public string PropertyType { get; set; } = string.Empty; // House, Apartment, Condo, etc.

        [JsonInclude]
        [Column(TypeName = "decimal(18,2)")]
        [DataType(DataType.Currency)]
        [Display(Name = "Monthly Rent", Description = "Monthly rental amount for the property",
            Prompt = "e.g., 1200.00", ShortName = "Rent")]
        public decimal MonthlyRent { get; set; }

        [JsonInclude]
        [Range(0, int.MaxValue, ErrorMessage = "Bedrooms must be a non-negative number.")]
        [DataType(DataType.Text)]
        [Display(Name = "Bedrooms", Description = "Number of Bedrooms",
            Prompt = "e.g., 3", ShortName = "Beds")]
        [MaxLength(3, ErrorMessage = "Bedrooms cannot exceed 3 digits.")]
        public int Bedrooms { get; set; }
        

        [JsonInclude]
        [Column(TypeName = "decimal(3,1)")]
        [DataType(DataType.Text)]
        [MaxLength(3, ErrorMessage = "Bathrooms cannot exceed 3 digits.")]
        [Display(Name = "Bathrooms", Description = "Number of Bathrooms",
            Prompt = "e.g., 1.5 for one and a half bathrooms", ShortName = "Baths")]
        public decimal Bathrooms { get; set; }


        [JsonInclude]
        [Range(0, int.MaxValue, ErrorMessage = "Square Feet must be a non-negative number.")]
        [DataType(DataType.Text)]
        [MaxLength(7, ErrorMessage = "Square Feet cannot exceed 7 digits.")]
        [Display(Name = "Square Feet", Description = "Total square footage of the property",
            Prompt = "e.g., 1500", ShortName = "Sq. Ft.")]
        public int SquareFeet { get; set; }


        [JsonInclude]
        [StringLength(1000)]
        [Display(Name = "Description", Description = "Detailed description of the property",
            Prompt = "Provide additional details about the property", ShortName = "Desc.")]
        [DataType(DataType.MultilineText)]
        [MaxLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
        public string Description { get; set; } = string.Empty;

        [JsonInclude]
        [Display(Name = "Is Active?", Description = "Indicates if the property is active in the system")]
        public bool IsActive { get; set; } = true;

        [JsonInclude]
        [StringLength(50)]
        [Display(Name = "Property Status", Description = "Current status in the rental lifecycle")]
        public string Status { get; set; } = ApplicationConstants.PropertyStatuses.Available;

        // Inspection tracking
    

        [JsonInclude]
        public DateTime? LastRoutineInspectionDate { get; set; }
        [JsonInclude]
        public DateTime? NextRoutineInspectionDueDate { get; set; }
        [JsonInclude]
        public int RoutineInspectionIntervalMonths { get; set; } = 12; // Default to annual inspections

        // Navigation properties
        public virtual ICollection<Lease> Leases { get; set; } = new List<Lease>();
        public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
        public virtual ICollection<Repair> Repairs { get; set; } = new List<Repair>();
        public virtual ICollection<MaintenanceRequest> MaintenanceRequests { get; set; } = new List<MaintenanceRequest>();

        // Computed property for pending application count
        [NotMapped]
        [JsonInclude]
        public int PendingApplicationCount => 0; // Will be populated when RentalApplications are added

        // Computed property for inspection status
        [NotMapped]
        public bool IsInspectionOverdue
        {
            get
            {
                if (!NextRoutineInspectionDueDate.HasValue)
                    return false;
                
                return DateTime.Today >= NextRoutineInspectionDueDate.Value.Date;
            }
        }

        [NotMapped]
        public int DaysUntilInspectionDue
        {
            get
            {
                if (!NextRoutineInspectionDueDate.HasValue)
                    return 0;
                
                return (NextRoutineInspectionDueDate.Value.Date - DateTime.Today).Days;
            }
        }

        [NotMapped]
        public int DaysOverdue
        {
            get
            {
                if (!IsInspectionOverdue)
                    return 0;
                
                return (DateTime.Today - NextRoutineInspectionDueDate!.Value.Date).Days;
            }
        }

        [NotMapped]
        public string InspectionStatus
        {
            get
            {
                if (!NextRoutineInspectionDueDate.HasValue)
                    return "Not Scheduled";
                
                if (IsInspectionOverdue)
                    return "Overdue";
                
                if (DaysUntilInspectionDue <= 30)
                    return "Due Soon";
                
                return "Scheduled";
            }
        }
    }
}