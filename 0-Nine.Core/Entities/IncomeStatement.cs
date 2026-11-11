using System.ComponentModel.DataAnnotations;
using Nine.Core.Validation;

namespace Nine.Core.Entities;

/// <summary>
/// Income statement for a specific period
/// </summary>
public class IncomeStatement
{
    [RequiredGuid]
    [Display(Name = "Organization ID")]
    public Guid OrganizationId { get; set; } = Guid.Empty;
        
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public Guid? PropertyId { get; set; }
    public string? PropertyName { get; set; }

    // Income
    public decimal TotalRentIncome { get; set; }
    public decimal TotalOtherIncome { get; set; }
    public decimal TotalIncome => TotalRentIncome + TotalOtherIncome;

    // Expenses
    public decimal MaintenanceExpenses { get; set; }
    public decimal UtilityExpenses { get; set; }
    public decimal InsuranceExpenses { get; set; }
    public decimal TaxExpenses { get; set; }
    public decimal ManagementFees { get; set; }
    public decimal MortgageExpenses { get; set; }
    public decimal OtherExpenses { get; set; }
    public decimal TotalExpenses => MaintenanceExpenses + UtilityExpenses + InsuranceExpenses + 
                                     TaxExpenses + ManagementFees + MortgageExpenses + OtherExpenses;

    // Net Income
    public decimal NetIncome => TotalIncome - TotalExpenses;
    public decimal ProfitMargin => TotalIncome > 0 ? (NetIncome / TotalIncome) * 100 : 0;
}

/// <summary>
/// Rent roll item showing tenant and payment information
/// </summary>
public class RentRollItem
{
    [RequiredGuid]
    public Guid PropertyId { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string PropertyAddress { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public string? TenantName { get; set; }
    public string LeaseStatus { get; set; } = string.Empty;
    public DateTime? LeaseStartDate { get; set; }
    public DateTime? LeaseEndDate { get; set; }
    public decimal MonthlyRent { get; set; }
    public decimal SecurityDeposit { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalDue { get; set; }
    public decimal Balance => TotalDue - TotalPaid;
    public string PaymentStatus => Balance <= 0 ? "Current" : "Outstanding";
}

/// <summary>
/// Property performance summary
/// </summary>
public class PropertyPerformance
{
    [RequiredGuid]
    public Guid PropertyId { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string PropertyAddress { get; set; } = string.Empty;
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetIncome => TotalIncome - TotalExpenses;
    public decimal ROI { get; set; }
    public int OccupancyDays { get; set; }
    public int TotalDays { get; set; }
    public decimal OccupancyRate => TotalDays > 0 ? (decimal)OccupancyDays / TotalDays * 100 : 0;
}

/// <summary>
/// Tax report data
/// </summary>
public class TaxReportData
{
    public int Year { get; set; }
    public Guid? PropertyId { get; set; }
    public string? PropertyName { get; set; }
    public decimal TotalRentIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetRentalIncome => TotalRentIncome - TotalExpenses;
    public decimal DepreciationAmount { get; set; }
    public decimal TaxableIncome => NetRentalIncome - DepreciationAmount;
    
    // Expense breakdown for Schedule E
    public decimal Advertising { get; set; }
    public decimal Auto { get; set; }
    public decimal Cleaning { get; set; }
    public decimal Insurance { get; set; }
    public decimal Legal { get; set; }
    public decimal Management { get; set; }
    public decimal MortgageExpense { get; set; }
    public decimal Repairs { get; set; }
    public decimal Supplies { get; set; }
    public decimal Taxes { get; set; }
    public decimal Utilities { get; set; }
    public decimal Other { get; set; }
}
