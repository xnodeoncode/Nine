using Nine.Core.Interfaces.Services;
using Nine.Core.Constants;
using Nine.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using Nine.Application.Services;

namespace Nine.Application.Services
{
    /// <summary>
    /// Service for managing Payment entities.
    /// Inherits common CRUD operations from BaseService and adds payment-specific business logic.
    /// </summary>
    public class PaymentService : BaseService<Payment>
    {
        private readonly NotificationService _notificationService;
        public PaymentService(
            ApplicationDbContext context,
            ILogger<PaymentService> logger,
            IUserContextService userContext,
            NotificationService notificationService,
            IOptions<ApplicationSettings> settings)
            : base(context, logger, userContext, settings)
        {
            _notificationService = notificationService;
        }

        /// <summary>
        /// Validates a payment before create/update operations.
        /// </summary>
        protected override async Task ValidateEntityAsync(Payment entity)
        {
            var errors = new List<string>();

            // Required fields
            if (entity.InvoiceId == Guid.Empty)
            {
                errors.Add("Invoice ID is required.");
            }

            if (entity.Amount <= 0)
            {
                errors.Add("Payment amount must be greater than zero.");
            }

            if (entity.PaidOn > DateTime.UtcNow.Date.AddDays(1))
            {
                errors.Add("Payment date cannot be in the future.");
            }

            // Validate invoice exists and belongs to organization
            if (entity.InvoiceId != Guid.Empty)
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();
                var invoice = await _context.Invoices
                    .Include(i => i.Lease)
                        .ThenInclude(l => l.Property)
                    .FirstOrDefaultAsync(i => i.Id == entity.InvoiceId && !i.IsDeleted);

                if (invoice == null)
                {
                    errors.Add($"Invoice with ID {entity.InvoiceId} does not exist.");
                }
                else if (invoice.Lease?.Property?.OrganizationId != organizationId)
                {
                    errors.Add("Invoice does not belong to the current organization.");
                }
                else
                {
                    // Validate payment doesn't exceed invoice balance
                    var existingPayments = await _context.Payments
                        .Where(p => p.InvoiceId == entity.InvoiceId 
                            && !p.IsDeleted 
                            && p.Id != entity.Id) // Exclude current payment for updates
                        .SumAsync(p => p.Amount);

                    var totalWithThisPayment = existingPayments + entity.Amount;
                    
                    // Include late fee in invoice total if it has been applied, since payments should cover the full amount due
                    var invoiceTotal = invoice.Amount + (invoice.LateFeeAmount ?? 0);

                    if (totalWithThisPayment > invoiceTotal)
                    {
                        errors.Add($"Payment amount would exceed invoice balance. Invoice total: {invoiceTotal:C}, Already paid: {existingPayments:C}, This payment: {entity.Amount:C}");
                    }
                }
            }

            // Validate payment method
            var validMethods = ApplicationConstants.PaymentMethods.AllPaymentMethods;

            if (!string.IsNullOrWhiteSpace(entity.PaymentMethod) && !validMethods.Contains(entity.PaymentMethod))
            {
                errors.Add($"Payment method must be one of: {string.Join(", ", validMethods)}");
            }

            if (errors.Any())
            {
                throw new ValidationException(string.Join(" ", errors));
            }
        }

        /// <summary>
        /// Creates a payment and automatically updates the associated invoice.
        /// </summary>
        public override async Task<Payment> CreateAsync(Payment entity)
        {
            // Auto-generate payment number if not provided
            if (string.IsNullOrWhiteSpace(entity.PaymentNumber))
            {
                entity.PaymentNumber = await GeneratePaymentNumberAsync();
            }

            var payment = await base.CreateAsync(entity);
            await UpdateInvoiceAfterPaymentChangeAsync(payment.InvoiceId);
            return payment;
        }

        /// <summary>
        /// Updates a payment and automatically updates the associated invoice.
        /// </summary>
        public override async Task<Payment> UpdateAsync(Payment entity)
        {
            var payment = await base.UpdateAsync(entity);
            await UpdateInvoiceAfterPaymentChangeAsync(payment.InvoiceId);
            return payment;
        }

        /// <summary>
        /// Deletes a payment and automatically updates the associated invoice.
        /// </summary>
        public override async Task<bool> DeleteAsync(Guid id)
        {
            var payment = await GetByIdAsync(id);
            if (payment != null)
            {
                var invoiceId = payment.InvoiceId;
                var result = await base.DeleteAsync(id);
                await UpdateInvoiceAfterPaymentChangeAsync(invoiceId);
                return result;
            }
            return false;
        }

        /// <summary>
        /// Gets all payments for a specific invoice.
        /// </summary>
        public async Task<List<Payment>> GetPaymentsByInvoiceIdAsync(Guid invoiceId)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Payments
                    .Include(p => p.Invoice)
                        .ThenInclude(i => i.Lease)
                            .ThenInclude(l => l.Property)
                    .Include(p => p.Invoice)
                        .ThenInclude(i => i.Lease)
                            .ThenInclude(l => l.Tenant)
                    .Where(p => p.InvoiceId == invoiceId
                        && !p.IsDeleted
                        && p.OrganizationId == organizationId)
                    .OrderByDescending(p => p.PaidOn)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetPaymentsByInvoiceId");
                throw;
            }
        }

        /// <summary>
        /// Gets payments by payment method.
        /// </summary>
        public async Task<List<Payment>> GetPaymentsByMethodAsync(string paymentMethod)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Payments
                    .Include(p => p.Invoice)
                        .ThenInclude(i => i.Lease)
                            .ThenInclude(l => l.Property)
                    .Include(p => p.Invoice)
                        .ThenInclude(i => i.Lease)
                            .ThenInclude(l => l.Tenant)
                    .Where(p => p.PaymentMethod == paymentMethod
                        && !p.IsDeleted
                        && p.OrganizationId == organizationId)
                    .OrderByDescending(p => p.PaidOn)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetPaymentsByMethod");
                throw;
            }
        }

        /// <summary>
        /// Gets payments within a specific date range.
        /// </summary>
        public async Task<List<Payment>> GetPaymentsByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Payments
                    .Include(p => p.Invoice)
                        .ThenInclude(i => i.Lease)
                            .ThenInclude(l => l.Property)
                    .Include(p => p.Invoice)
                        .ThenInclude(i => i.Lease)
                            .ThenInclude(l => l.Tenant)
                    .Where(p => p.PaidOn >= startDate
                        && p.PaidOn <= endDate
                        && !p.IsDeleted
                        && p.OrganizationId == organizationId)
                    .OrderByDescending(p => p.PaidOn)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetPaymentsByDateRange");
                throw;
            }
        }

        /// <summary>
        /// Gets a payment with all related entities loaded.
        /// </summary>
        public async Task<Payment?> GetPaymentWithRelationsAsync(Guid paymentId)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Payments
                    .Include(p => p.Invoice)
                        .ThenInclude(i => i.Lease)
                            .ThenInclude(l => l.Property)
                    .Include(p => p.Invoice)
                        .ThenInclude(i => i.Lease)
                            .ThenInclude(l => l.Tenant)
                    .Include(p => p.Document)
                    .FirstOrDefaultAsync(p => p.Id == paymentId
                        && !p.IsDeleted
                        && p.OrganizationId == organizationId);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetPaymentWithRelations");
                throw;
            }
        }

         /// <summary>
        /// Gets payments with all related entities loaded.
        /// </summary>
        public async Task<List<Payment>> GetPaymentsWithRelationsAsync()
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Payments
                    .Include(p => p.Invoice)
                        .ThenInclude(i => i.Lease)
                            .ThenInclude(l => l.Property)
                    .Include(p => p.Invoice)
                        .ThenInclude(i => i.Lease)
                            .ThenInclude(l => l.Tenant)
                    .Include(p => p.Document)
                    .Where(p => !p.IsDeleted
                        && p.OrganizationId == organizationId)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetPaymentsWithRelations");
                throw;
            }
        }

        /// <summary>
        /// Calculates the total payments received within a date range.
        /// </summary>
        public async Task<decimal> CalculateTotalPaymentsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                var query = _context.Payments
                    .Where(p => !p.IsDeleted && p.OrganizationId == organizationId);

                if (startDate.HasValue)
                {
                    query = query.Where(p => p.PaidOn >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(p => p.PaidOn <= endDate.Value);
                }

                return await query.SumAsync(p => p.Amount);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "CalculateTotalPayments");
                throw;
            }
        }

        /// <summary>
        /// Gets payment summary grouped by payment method.
        /// </summary>
        public async Task<Dictionary<string, decimal>> GetPaymentSummaryByMethodAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                var query = _context.Payments
                    .Where(p => !p.IsDeleted && p.OrganizationId == organizationId);

                if (startDate.HasValue)
                {
                    query = query.Where(p => p.PaidOn >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(p => p.PaidOn <= endDate.Value);
                }

                return await query
                    .GroupBy(p => p.PaymentMethod)
                    .Select(g => new { Method = g.Key, Total = g.Sum(p => p.Amount) })
                    .ToDictionaryAsync(x => x.Method, x => x.Total);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetPaymentSummaryByMethod");
                throw;
            }
        }

        /// <summary>
        /// Gets the total amount paid for a specific invoice.
        /// </summary>
        public async Task<decimal> GetTotalPaidForInvoiceAsync(Guid invoiceId)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Payments
                    .Where(p => p.InvoiceId == invoiceId
                        && !p.IsDeleted
                        && p.OrganizationId == organizationId)
                    .SumAsync(p => p.Amount);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetTotalPaidForInvoice");
                throw;
            }
        }

        /// <summary>
        /// Generates a unique payment number in the format PYMT-YYYYMMDD-####.
        /// </summary>
        /// <summary>
        /// Generates payment number in format: PYMT-{YYYYMM}-000n
        /// Numbers reset monthly and are scoped to organization.
        /// Uses MAX query to get last payment number for current month.
        /// Format matches invoice numbering for consistency.
        /// </summary>
        public async Task<string> GeneratePaymentNumberAsync()
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();
                var yearMonth = DateTime.UtcNow.ToString("yyyyMM");
                
                // Get the highest payment number for this month
                var lastPayment = await _context.Payments
                    .Where(p => p.OrganizationId == organizationId 
                        && p.PaymentNumber.StartsWith($"PYMT-{yearMonth}"))
                    .OrderByDescending(p => p.PaymentNumber)
                    .Select(p => p.PaymentNumber)
                    .FirstOrDefaultAsync();
                
                int nextNum = 1;
                if (lastPayment != null)
                {
                    // Extract sequence number from: PYMT-202602-0001
                    var parts = lastPayment.Split('-');
                    if (parts.Length == 3)
                    {
                        nextNum = int.Parse(parts[2]) + 1;
                    }
                }
                
                return $"PYMT-{yearMonth}-{nextNum:D4}";
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GeneratePaymentNumber");
                throw;
            }
        }

        /// <summary>
        /// Updates the invoice status and paid amount after a payment change.
        /// Also applies late fees if invoice becomes overdue and fees haven't been applied yet.
        /// </summary>
        private async Task UpdateInvoiceAfterPaymentChangeAsync(Guid invoiceId)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();
                var invoice = await _context.Invoices
                    .Include(i => i.Payments)
                    .FirstOrDefaultAsync(i => i.Id == invoiceId && i.OrganizationId == organizationId);

                if (invoice != null)
                {
                    var totalPaid = invoice.Payments
                        .Where(p => !p.IsDeleted)
                        .Sum(p => p.Amount);

                    invoice.AmountPaid = totalPaid;

                    // Total due is the invoice amount (which includes late fees if already applied)
                    var totalDue = invoice.Amount;

                    var previousStatus = invoice.Status;
                    var statusChangedToOverdue = false;

                    // Update invoice status based on payment
                    // Don't change status if invoice is Cancelled or Voided
                    if (invoice.Status != ApplicationConstants.InvoiceStatuses.Cancelled 
                        && invoice.Status != ApplicationConstants.InvoiceStatuses.Voided)
                    {
                        if (totalPaid >= totalDue)
                        {
                            invoice.Status = ApplicationConstants.InvoiceStatuses.Paid;
                            invoice.PaidOn = invoice.Payments
                                .Where(p => !p.IsDeleted)
                                .OrderByDescending(p => p.PaidOn)
                                .FirstOrDefault()?.PaidOn ?? DateTime.UtcNow;
                        }
                        else if (totalPaid > 0)
                        {
                            // Invoice is partially paid
                            if (invoice.DueOn < DateTime.Today)
                            {
                                invoice.Status = ApplicationConstants.InvoiceStatuses.Overdue;
                                statusChangedToOverdue = (previousStatus != ApplicationConstants.InvoiceStatuses.Overdue);
                            }
                            else
                            {
                                invoice.Status = ApplicationConstants.InvoiceStatuses.PaidPartial;
                            }
                        }
                        else
                        {
                            // No payments
                            if (invoice.DueOn < DateTime.Today)
                            {
                                invoice.Status = ApplicationConstants.InvoiceStatuses.Overdue;
                                statusChangedToOverdue = (previousStatus != ApplicationConstants.InvoiceStatuses.Overdue);
                            }
                            else
                            {
                                invoice.Status = ApplicationConstants.InvoiceStatuses.Pending;
                            }
                        }
                    }

                    // If invoice just became overdue, check if late fee should be applied
                    if (statusChangedToOverdue && 
                        (invoice.LateFeeApplied == null || !invoice.LateFeeApplied.Value))
                    {
                        await ApplyLateFeeIfEligibleAsync(invoice, organizationId);
                    }

                    var userId = await _userContext.GetUserIdAsync();
                    invoice.LastModifiedBy = userId ?? "system";
                    invoice.LastModifiedOn = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "UpdateInvoiceAfterPaymentChange");
                throw;
            }
        }

        /// <summary>
        /// Applies late fee to an invoice if eligible based on organization settings.
        /// Uses the same logic as the scheduled task to ensure consistency.
        /// </summary>
        private async Task ApplyLateFeeIfEligibleAsync(Invoice invoice, Guid? organizationId)
        {
            try
            {
                // Get organization settings
                var settings = await _context.OrganizationSettings
                    .FirstOrDefaultAsync(s => s.OrganizationId == organizationId);

                if (settings == null || !settings.LateFeeEnabled || !settings.LateFeeAutoApply)
                {
                    // Late fees not enabled or not set to auto-apply
                    return;
                }

                var today = DateTime.Today;
                var gracePeriodCutoff = today.AddDays(-settings.LateFeeGracePeriodDays);

                // Check if invoice is past grace period
                if (invoice.DueOn >= gracePeriodCutoff)
                {
                    // Still within grace period
                    return;
                }

                // Calculate and apply late fee
                var lateFee = Math.Min(invoice.Amount * settings.LateFeePercentage, settings.MaxLateFeeAmount);
                
                invoice.LateFeeAmount = lateFee;
                invoice.LateFeeApplied = true;
                invoice.LateFeeAppliedOn = DateTime.UtcNow;
                invoice.Amount += lateFee;
                invoice.Notes = string.IsNullOrEmpty(invoice.Notes)
                    ? $"Late fee of {lateFee:C} applied on {DateTime.Now:MMM dd, yyyy}"
                    : $"{invoice.Notes}\nLate fee of {lateFee:C} applied on {DateTime.Now:MMM dd, yyyy}";

                _logger.LogInformation(
                    "Late fee of {LateFee:C} auto-applied to invoice {InvoiceNumber} (ID: {InvoiceId}) during payment processing",
                    lateFee, invoice.InvoiceNumber, invoice.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying late fee to invoice {InvoiceId}", invoice.Id);
                // Don't throw - we don't want to fail the payment processing if late fee fails
            }
        }
    }
}
