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
    /// Raw sums of actual ExpensePayment amounts by PropertyExpense.ExpenseType for
    /// the given period. This app only captures and sums what was recorded — it
    /// does not compute tax-deductible portions (e.g. mortgage interest vs.
    /// principal); export these sums to a proper financial/tax tool for that.
    /// </summary>
    private static async Task<Dictionary<string, decimal>> GetActualFixedExpensesByCategoryAsync(
        ApplicationDbContext context, Guid organizationId, Guid? propertyId, DateTime startDate, DateTime endDate)
    {
        var query = context.Set<ExpensePayment>()
            .Include(p => p.ExpenseDue)
            .ThenInclude(d => d!.PropertyExpense)
            .Where(p => !p.IsDeleted &&
                       p.OrganizationId == organizationId &&
                       p.PaidOn >= startDate &&
                       p.PaidOn <= endDate);

        if (propertyId.HasValue)
        {
            query = query.Where(p => p.ExpenseDue!.PropertyId == propertyId.Value);
        }

        var payments = await query.ToListAsync();

        return payments
            .GroupBy(p => p.ExpenseDue!.PropertyExpense!.ExpenseType)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.AmountPaid));
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

        var fixedExpenses = await GetActualFixedExpensesByCategoryAsync(context, organizationId, propertyId, startDate, endDate);
        decimal GetCategory(string type) => fixedExpenses.TryGetValue(type, out var amt) ? amt : 0m;

        // Maintenance & Repairs combines ad hoc Repairs with recurring PropertyExpense
        // "Maintenance" entries (e.g. a lawn/pest contract) — both are genuine upkeep
        // costs, not a double count of the same expense.
        statement.MaintenanceExpenses = repairs.Sum(r => r.Cost) + GetCategory(ApplicationConstants.PropertyExpenseTypes.Maintenance);
        statement.UtilityExpenses = GetCategory(ApplicationConstants.PropertyExpenseTypes.Utilities);
        statement.InsuranceExpenses = GetCategory(ApplicationConstants.PropertyExpenseTypes.Insurance);
        statement.TaxExpenses = GetCategory(ApplicationConstants.PropertyExpenseTypes.PropertyTax);
        statement.ManagementFees = 0; // No PropertyExpense category maps to this yet
        statement.MortgageExpenses = GetCategory(ApplicationConstants.PropertyExpenseTypes.Mortgage);
        statement.OtherExpenses = GetCategory(ApplicationConstants.PropertyExpenseTypes.HOA) + GetCategory(ApplicationConstants.PropertyExpenseTypes.Other);

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

            // Calculate expenses from repairs (Nine approach) plus actual fixed expenses
            var repairCost = await context.Repairs
                .Where(r => !r.IsDeleted &&
                           r.PropertyId == property.Id &&
                           r.OrganizationId == organizationId &&
                           r.CompletedOn.HasValue &&
                           r.CompletedOn.Value >= startDate &&
                           r.CompletedOn.Value <= endDate &&
                           r.Cost > 0)
                .SumAsync(r => r.Cost);

            var fixedExpenses = await GetActualFixedExpensesByCategoryAsync(context, organizationId, property.Id, startDate, endDate);
            var expenses = repairCost + fixedExpenses.Values.Sum();

            // Calculate occupancy days
            // Include active leases (IsActive covers Accepted/MonthToMonth/NoticeGiven/Pending/Offered),
            // renewed (historical), and terminated (to count actual days occupied before move-out)
            var leases = await context.Leases
                .Where(l => l.PropertyId == property.Id &&
                           !l.IsDeleted &&
                           (l.IsActive ||
                            l.Status == ApplicationConstants.LeaseStatuses.Terminated ||
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

            var fixedExpenses = await GetActualFixedExpensesByCategoryAsync(context, organizationId, property.Id, startDate, endDate);
            decimal GetCategory(string type) => fixedExpenses.TryGetValue(type, out var amt) ? amt : 0m;

            var insurance = GetCategory(ApplicationConstants.PropertyExpenseTypes.Insurance);
            var taxes = GetCategory(ApplicationConstants.PropertyExpenseTypes.PropertyTax);
            var utilities = GetCategory(ApplicationConstants.PropertyExpenseTypes.Utilities);
            // Raw captured Mortgage amount (principal + interest) — use Form 1098, Box 1
            // for the actual tax-deductible interest portion; this app does not compute amortization.
            var mortgage = GetCategory(ApplicationConstants.PropertyExpenseTypes.Mortgage);
            var repairs = totalRepairCost + GetCategory(ApplicationConstants.PropertyExpenseTypes.Maintenance);
            var other = GetCategory(ApplicationConstants.PropertyExpenseTypes.HOA) + GetCategory(ApplicationConstants.PropertyExpenseTypes.Other);

            var taxReport = new TaxReportData
            {
                Year = year,
                PropertyId = property.Id,
                PropertyName = property.Address,
                TotalRentIncome = rentIncome,
                DepreciationAmount = depreciationAmount,

                // Advertising, Cleaning, Legal, Management, and Supplies have no dedicated
                // PropertyExpenseType by design — Cleaning rolls into Repairs (Maintenance type)
                // and the rest roll into Other if entered; Auto/travel is a business-level
                // expense, out of scope for per-property tracking, so it stays $0.
                Advertising = 0,
                Cleaning = 0,
                Insurance = insurance,
                Legal = 0,
                Management = 0,
                MortgageExpense = mortgage,
                Repairs = repairs,
                Supplies = 0,
                Taxes = taxes,
                Utilities = utilities,
                Other = other
            };

            taxReport.TotalExpenses = insurance + taxes + utilities + mortgage + repairs + other;

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
