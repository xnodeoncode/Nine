using Nine.Application.Services;
using Nine.Core.Constants;
using Nine.Core.Entities;
using Nine.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Nine.Application.Services;

/// <summary>
/// Nine financial report service that uses Repairs for expenses
/// instead of MaintenanceRequests (which are used in Professional edition).
/// </summary>
public class NineFinancialReportService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public NineFinancialReportService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Generate income statement for a specific period and optional property.
    /// Uses Repairs for expense tracking (Nine approach).
    /// </summary>
    public async Task<IncomeStatement> GenerateIncomeStatementAsync(
        Guid organizationId,
        DateTime startDate,
        DateTime endDate,
        Guid? propertyId = null)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        var statement = new IncomeStatement
        {
            StartDate = startDate,
            EndDate = endDate,
            PropertyId = propertyId
        };

        // Get property name if filtering by property
        if (propertyId.HasValue)
        {
            var property = await context.Properties
                .Where(p => p.Id == propertyId.Value && p.OrganizationId == organizationId)
                .FirstOrDefaultAsync();
            statement.PropertyName = property?.Address;
        }

        // Calculate total rent income from payments (all payments are rent payments)
        var paymentsQuery = context.Payments
            .Include(p => p.Invoice)
            .ThenInclude(i => i.Lease)
            .Where(p => p.Invoice.Lease.Property.OrganizationId == organizationId &&
                       p.PaidOn >= startDate &&
                       p.PaidOn <= endDate);

        if (propertyId.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(p => p.Invoice.Lease.PropertyId == propertyId.Value);
        }

        var totalPayments = await paymentsQuery.SumAsync(p => p.Amount);
        statement.TotalRentIncome = totalPayments;
        statement.TotalOtherIncome = 0; // No other income tracked currently

        // Get repair expenses (Nine uses Repairs instead of MaintenanceRequests)
        var repairsQuery = context.Repairs
            .Where(r => !r.IsDeleted &&
                       r.OrganizationId == organizationId &&
                       r.CompletedOn.HasValue &&
                       r.CompletedOn.Value >= startDate &&
                       r.CompletedOn.Value <= endDate &&
                       r.Cost > 0);

        if (propertyId.HasValue)
        {
            repairsQuery = repairsQuery.Where(r => r.PropertyId == propertyId.Value);
        }

        var repairs = await repairsQuery.ToListAsync();

        // All repair costs go to MaintenanceExpenses
        statement.MaintenanceExpenses = repairs.Sum(r => r.Cost);

        // Other expense categories are currently zero (no data tracked for these yet)
        statement.UtilityExpenses = 0;
        statement.InsuranceExpenses = 0;
        statement.TaxExpenses = 0;
        statement.ManagementFees = 0;
        statement.OtherExpenses = 0;

        return statement;
    }

    /// <summary>
    /// Generate property performance comparison report.
    /// Uses Repairs for expense tracking (Nine approach).
    /// </summary>
    public async Task<List<PropertyPerformance>> GeneratePropertyPerformanceAsync(
        Guid organizationId,
        DateTime startDate,
        DateTime endDate)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        var properties = await context.Properties
            .Where(p => p.OrganizationId == organizationId)
            .ToListAsync();

        var performance = new List<PropertyPerformance>();
        var totalDays = (endDate - startDate).Days + 1;

        foreach (var property in properties)
        {
            // Calculate income from rent payments
            var income = await context.Payments
                .Include(p => p.Invoice)
                .ThenInclude(i => i.Lease)
                .Where(p => p.Invoice.Lease.PropertyId == property.Id &&
                           p.PaidOn >= startDate &&
                           p.PaidOn <= endDate)
                .SumAsync(p => p.Amount);

            // Calculate expenses from repairs (Nine approach)
            var expenses = await context.Repairs
                .Where(r => !r.IsDeleted &&
                           r.PropertyId == property.Id &&
                           r.OrganizationId == organizationId &&
                           r.CompletedOn.HasValue &&
                           r.CompletedOn.Value >= startDate &&
                           r.CompletedOn.Value <= endDate &&
                           r.Cost > 0)
                .SumAsync(r => r.Cost);

            // Calculate occupancy days
            // Include active leases (IsActive covers Accepted/MonthToMonth/NoticeGiven/Pending/Offered),
            // renewed (historical), and terminated (to count actual days occupied before move-out)
            var leases = await context.Leases
                .Where(l => l.PropertyId == property.Id &&
                           !l.IsDeleted &&
                           (l.IsActive ||
                            l.Status == ApplicationConstants.LeaseStatuses.Renewed ||
                            l.Status == ApplicationConstants.LeaseStatuses.Terminated) &&
                           l.StartDate <= endDate)
                .ToListAsync();

            var occupancyDays = 0;
            foreach (var lease in leases)
            {
                // For terminated leases, use ActualMoveOutDate; otherwise use EndDate
                var effectiveEndDate = lease.Status == ApplicationConstants.LeaseStatuses.Terminated && lease.ActualMoveOutDate.HasValue
                    ? lease.ActualMoveOutDate.Value
                    : lease.EndDate;

                // Only count if lease overlaps with report period
                if (effectiveEndDate >= startDate)
                {
                    var leaseStart = lease.StartDate > startDate ? lease.StartDate : startDate;
                    var leaseEnd = effectiveEndDate < endDate ? effectiveEndDate : endDate;
                    
                    if (leaseEnd >= leaseStart)
                    {
                        occupancyDays += (leaseEnd - leaseStart).Days + 1;
                    }
                }
            }

            // Calculate ROI (simplified - based on profit margin since we don't track purchase price)
            var roi = income > 0
                ? ((income - expenses) / income) * 100
                : 0;

            performance.Add(new PropertyPerformance
            {
                PropertyId = property.Id,
                PropertyName = property.Address,
                PropertyAddress = property.Address,
                TotalIncome = income,
                TotalExpenses = expenses,
                ROI = roi,
                OccupancyDays = occupancyDays,
                TotalDays = totalDays
            });
        }

        return performance.OrderByDescending(p => p.NetIncome).ToList();
    }

    /// <summary>
    /// Generate tax report data for Schedule E.
    /// Uses Repairs for expense tracking (Nine approach).
    /// </summary>
    public async Task<List<TaxReportData>> GenerateTaxReportAsync(Guid organizationId, int year, Guid? propertyId = null)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var startDate = new DateTime(year, 1, 1);
        var endDate = new DateTime(year, 12, 31);

        var propertiesQuery = context.Properties.Where(p => p.OrganizationId == organizationId);
        if (propertyId.HasValue)
        {
            propertiesQuery = propertiesQuery.Where(p => p.Id == propertyId.Value);
        }

        var properties = await propertiesQuery.ToListAsync();
        var taxReports = new List<TaxReportData>();

        foreach (var property in properties)
        {
            // Calculate rent income from payments
            var rentIncome = await context.Payments
                .Include(p => p.Invoice)
                .ThenInclude(i => i.Lease)
                .Where(p => p.Invoice.Lease.PropertyId == property.Id &&
                           p.PaidOn >= startDate &&
                           p.PaidOn <= endDate)
                .SumAsync(p => p.Amount);

            // Get repair expenses (Nine approach)
            var repairExpenses = await context.Repairs
                .Where(r => !r.IsDeleted &&
                           r.PropertyId == property.Id &&
                           r.OrganizationId == organizationId &&
                           r.CompletedOn.HasValue &&
                           r.CompletedOn.Value >= startDate &&
                           r.CompletedOn.Value <= endDate &&
                           r.Cost > 0)
                .ToListAsync();

            // Calculate depreciation (simplified - 27.5 years for residential rental)
            // Note: Since we don't track purchase price, this should be manually entered
            var depreciationAmount = 0m;

            var totalRepairCost = repairExpenses.Sum(r => r.Cost);

            var taxReport = new TaxReportData
            {
                Year = year,
                PropertyId = property.Id,
                PropertyName = property.Address,
                TotalRentIncome = rentIncome,
                DepreciationAmount = depreciationAmount,

                // Currently only repairs are tracked
                Advertising = 0,
                Cleaning = 0,
                Insurance = 0,
                Legal = 0,
                Management = 0,
                MortgageInterest = 0,
                Repairs = totalRepairCost, // All repair costs
                Supplies = 0,
                Taxes = 0,
                Utilities = 0,
                Other = 0
            };

            taxReport.TotalExpenses = totalRepairCost;

            taxReports.Add(taxReport);
        }

        return taxReports;
    }

    /// <summary>
    /// Generate rent roll report showing all properties and tenants.
    /// This method is the same as base FinancialReportService since it doesn't involve expenses.
    /// </summary>
    public async Task<List<RentRollItem>> GenerateRentRollAsync(Guid organizationId, DateTime asOfDate)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        var rentRoll = await context.Leases
            .Include(l => l.Property)
            .Include(l => l.Tenant)
            .Include(l => l.Invoices)
            .ThenInclude(i => i.Payments)
            .Where(l => l.Property.OrganizationId == organizationId &&
                       l.Tenant != null &&
                       l.StartDate <= asOfDate &&
                       l.EndDate >= asOfDate)
            .OrderBy(l => l.Property.Address)
            .ThenBy(l => l.Tenant!.LastName)
            .Select(l => new RentRollItem
            {
                PropertyId = l.PropertyId,
                PropertyName = l.Property.Address,
                PropertyAddress = l.Property.Address,
                TenantId = l.TenantId,
                TenantName = $"{l.Tenant!.FirstName} {l.Tenant!.LastName}",
                LeaseStatus = l.Status,
                LeaseStartDate = l.StartDate,
                LeaseEndDate = l.EndDate,
                MonthlyRent = l.MonthlyRent,
                SecurityDeposit = l.SecurityDeposit,
                TotalPaid = l.Invoices.SelectMany(i => i.Payments).Sum(p => p.Amount),
                TotalDue = l.Invoices.Where(i => i.Status != "Cancelled").Sum(i => i.Amount)
            })
            .ToListAsync();

        return rentRoll;
    }
}
