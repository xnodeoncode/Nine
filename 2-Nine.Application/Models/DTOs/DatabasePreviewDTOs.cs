namespace Nine.Application.Models.DTOs;

/// <summary>
/// DTO for database preview summary data
/// </summary>
public class DatabasePreviewData
{
    public int PropertyCount { get; set; }
    public int TenantCount { get; set; }
    public int LeaseCount { get; set; }
    public int InvoiceCount { get; set; }
    public int PaymentCount { get; set; }
    public int MaintenanceCount { get; set; }
    public int RepairCount { get; set; }
    public int DocumentCount { get; set; }

    public List<PropertyPreview> Properties { get; set; } = new();
    public List<TenantPreview> Tenants { get; set; } = new();
    public List<LeasePreview> Leases { get; set; } = new();
    public List<InvoicePreview> Invoices { get; set; } = new();
    public List<PaymentPreview> Payments { get; set; } = new();
    public List<MaintenancePreview> MaintenanceRequests { get; set; } = new();
    public List<RepairPreview> Repairs { get; set; } = new();
}

/// <summary>
/// DTO for property preview in read-only database view
/// </summary>
public class PropertyPreview
{
    public Guid Id { get; set; }
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string PropertyType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? Units { get; set; }
    public decimal? MonthlyRent { get; set; }
}

/// <summary>
/// DTO for tenant preview in read-only database view
/// </summary>
public class TenantPreview
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
}

/// <summary>
/// DTO for lease preview in read-only database view
/// </summary>
public class LeasePreview
{
    public Guid Id { get; set; }
    public string PropertyAddress { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal MonthlyRent { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// DTO for invoice preview in read-only database view
/// </summary>
public class InvoicePreview
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string PropertyAddress { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public DateTime DueOn { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// DTO for payment preview in read-only database view
/// </summary>
public class PaymentPreview
{
    public Guid Id { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime PaidOn { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
}

/// <summary>
/// DTO for maintenance request preview in read-only database view
/// </summary>
public class MaintenancePreview
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string PropertyAddress { get; set; } = string.Empty;
    public string RequestType { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedOn { get; set; }
}

/// <summary>
/// DTO for repair preview in read-only database view
/// </summary>
public class RepairPreview
{
    public Guid Id { get; set; }
    public string PropertyAddress { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RepairType { get; set; } = string.Empty;
    public DateTime? CompletedOn { get; set; }
    public decimal Cost { get; set; }
}

/// <summary>
/// Represents a non-backup database file found in the data directory (e.g. an older versioned DB)
/// </summary>
public class OtherDatabaseFile
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string FileSizeFormatted => FormatBytes(FileSizeBytes);
    public DateTime LastModified { get; set; }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1_048_576) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / 1_048_576.0:F1} MB";
    }
}

/// <summary>
/// Result of a data import operation from a preview database
/// </summary>
public class ImportResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int PropertiesImported { get; set; }
    public int TenantsImported { get; set; }
    public int LeasesImported { get; set; }
    public int InvoicesImported { get; set; }
    public int PaymentsImported { get; set; }
    public int MaintenanceRequestsImported { get; set; }
    public int RepairsImported { get; set; }
    public int DocumentsImported { get; set; }
    public List<string> Errors { get; set; } = new();

    public int TotalImported => PropertiesImported + TenantsImported + LeasesImported
        + InvoicesImported + PaymentsImported + MaintenanceRequestsImported + RepairsImported + DocumentsImported;
}

/// <summary>
/// Result object for database operations
/// </summary>
public class DatabaseOperationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;

    public static DatabaseOperationResult SuccessResult(string message = "Operation successful")
        => new() { Success = true, Message = message };

    public static DatabaseOperationResult FailureResult(string message)
        => new() { Success = false, Message = message };
}
