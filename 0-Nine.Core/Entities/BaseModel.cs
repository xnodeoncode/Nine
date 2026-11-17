using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Nine.Core.Interfaces;
using Nine.Core.Validation;

namespace Nine.Core.Entities
{
    public class BaseModel : IAuditable
    {
        [Key]
        [JsonInclude]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public Guid Id { get; set; }

        /// <summary>
        /// Organization partition key - all entities are scoped to an organization for multi-tenancy.
        /// This is the fundamental isolation boundary in the system.
        /// </summary>
        [RequiredGuid]
        [JsonInclude]
        [Display(Name = "Organization ID")]
        public Guid OrganizationId { get; set; } = Guid.Empty;

        [Required]
        [JsonInclude]
        [DataType(DataType.DateTime)]
        [Display(Name = "Created On")]
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        [Required]
        [JsonInclude]
        [StringLength(100)]
        [DataType(DataType.Text)]
        [Display(Name = "Created By")]
        public string CreatedBy { get; set; } = string.Empty;

        [JsonInclude]
        [DataType(DataType.DateTime)]
        [Display(Name = "Last Modified On")]
        public DateTime? LastModifiedOn { get; set; }

        [JsonInclude]
        [StringLength(100)]
        [DataType(DataType.Text)]
        [Display(Name = "Last Modified By")]
        public string? LastModifiedBy { get; set; }

        [JsonInclude]
        [Display(Name = "Is Deleted?")]
        public bool IsDeleted { get; set; } = false;

        [JsonInclude]
        [Display(Name = "Is Sample Data?")]
        public bool IsSampleData { get; set; } = false;

        [JsonInclude]
        [Display(Name = "Is Archived?")]
        public bool IsArchived { get; set; } = false;

        [JsonInclude]
        [DataType(DataType.DateTime)]
        [Display(Name = "Archived On")]
        public DateTime? ArchivedOn { get; set; }

        [JsonInclude]
        [StringLength(100)]
        [Display(Name = "Archived By")]
        public string? ArchivedBy { get; set; }
    }
}