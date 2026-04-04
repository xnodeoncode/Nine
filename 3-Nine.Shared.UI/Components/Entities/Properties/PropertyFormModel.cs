using System.ComponentModel.DataAnnotations;
using Nine.Core.Constants;

namespace Nine.Shared.UI.Components.Entities.Properties;

/// <summary>
/// Form model for property create/edit operations
/// </summary>
public class PropertyFormModel
{
    [Required(ErrorMessage = "Address is required")]
    [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters")]
    public string Address { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "Unit number cannot exceed 50 characters")]
    public string? UnitNumber { get; set; }

    [StringLength(100, ErrorMessage = "City cannot exceed 100 characters")]
    public string City { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "State cannot exceed 50 characters")]
    public string State { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "Zip Code cannot exceed 20 characters")]
    [DataType(DataType.PostalCode)]
    [RegularExpression(@"^\d{5}(-\d{4})?$", ErrorMessage = "Invalid Zip Code format.")]
    public string ZipCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Property type is required")]
    [StringLength(50, ErrorMessage = "Property type cannot exceed 50 characters")]
    public string PropertyType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Monthly rent is required")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Monthly rent must be greater than 0")]
    public decimal MonthlyRent { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Bedrooms cannot be negative")]
    public int Bedrooms { get; set; }

    [Range(0.0, double.MaxValue, ErrorMessage = "Bathrooms cannot be negative")]
    public decimal Bathrooms { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Square feet cannot be negative")]
    public int SquareFeet { get; set; }

    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Status is required")]
    [StringLength(50, ErrorMessage = "Status cannot exceed 50 characters")]
    public string Status { get; set; } = ApplicationConstants.PropertyStatuses.Available;

    public bool IsActive { get; set; } = true;

    public bool IsSampleData { get; set; } = false;
}
