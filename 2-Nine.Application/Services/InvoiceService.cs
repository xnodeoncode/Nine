using Nine.Application.Models;
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
    /// Service for managing Invoice entities.
    /// Inherits common CRUD operations from BaseService and adds invoice-specific business logic.
    /// </summary>
    public class InvoiceService : BaseService<Invoice>
    {
        public InvoiceService(
            ApplicationDbContext context,
            ILogger<InvoiceService> logger,
            IUserContextService userContext,
            IOptions<ApplicationSettings> settings)
            : base(context, logger, userContext, settings)
        {
        }

        /// <summary>
        /// Validates an invoice before create/update operations.
        /// </summary>
        protected override async Task ValidateEntityAsync(Invoice entity)
        {
            var errors = new List<string>();

            // Required fields
            if (entity.LeaseId == Guid.Empty)
            {
                errors.Add("Lease ID is required.");
            }

            if (string.IsNullOrWhiteSpace(entity.InvoiceNumber))
            {
                errors.Add("Invoice number is required.");
            }

            if (string.IsNullOrWhiteSpace(entity.Description))
            {
                errors.Add("Description is required.");
            }

            if (entity.Amount <= 0)
            {
                errors.Add("Amount must be greater than zero.");
            }

            if (entity.DueOn < entity.InvoicedOn)
            {
                errors.Add("Due date cannot be before invoice date.");
            }

            // Validate lease exists and belongs to organization
            if (entity.LeaseId != Guid.Empty)
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();
                var lease = await _context.Leases
                    .Include(l => l.Property)
                    .FirstOrDefaultAsync(l => l.Id == entity.LeaseId && !l.IsDeleted);

                if (lease == null)
                {
                    errors.Add($"Lease with ID {entity.LeaseId} does not exist.");
                }
                else if (lease.Property.OrganizationId != organizationId)
                {
                    errors.Add("Lease does not belong to the current organization.");
                }
            }

            // Check for duplicate invoice number in same organization
            if (!string.IsNullOrWhiteSpace(entity.InvoiceNumber))
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();
                var duplicate = await _context.Invoices
                    .AnyAsync(i => i.InvoiceNumber == entity.InvoiceNumber
                        && i.OrganizationId == organizationId
                        && i.Id != entity.Id
                        && !i.IsDeleted);

                if (duplicate)
                {
                    errors.Add($"Invoice number '{entity.InvoiceNumber}' already exists.");
                }
            }

            // Validate status
            var validStatuses = new[] { "Pending", "Paid", "Overdue", "Cancelled" };
            if (!string.IsNullOrWhiteSpace(entity.Status) && !validStatuses.Contains(entity.Status))
            {
                errors.Add($"Status must be one of: {string.Join(", ", validStatuses)}");
            }

            // Validate amount paid doesn't exceed amount
            if (entity.AmountPaid > entity.Amount + (entity.LateFeeAmount ?? 0))
            {
                errors.Add("Amount paid cannot exceed invoice amount plus late fees.");
            }

            if (errors.Any())
            {
                throw new ValidationException(string.Join(" ", errors));
            }
        }

        /// <summary>
        /// Gets all invoices for a specific lease.
        /// </summary>
        public async Task<List<Invoice>> GetInvoicesByLeaseIdAsync(Guid leaseId)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Invoices
                    .Include(i => i.Lease)
                        .ThenInclude(l => l.Property)
                    .Include(i => i.Lease)
                        .ThenInclude(l => l.Tenant)
                    .Include(i => i.Payments)
                    .Where(i => i.LeaseId == leaseId
                        && !i.IsDeleted
                        && i.OrganizationId == organizationId)
                    .OrderByDescending(i => i.DueOn)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetInvoicesByLeaseId");
                throw;
            }
        }

        /// <summary>
        /// Gets all invoices with a specific status.
        /// </summary>
        public async Task<List<Invoice>> GetInvoicesByStatusAsync(string status)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Invoices
                    .Include(i => i.Lease)
                        .ThenInclude(l => l.Property)
                    .Include(i => i.Lease)
                        .ThenInclude(l => l.Tenant)
                    .Include(i => i.Payments)
                    .Where(i => i.Status == status
                        && !i.IsDeleted
                        && i.OrganizationId == organizationId)
                    .OrderByDescending(i => i.DueOn)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetInvoicesByStatus");
                throw;
            }
        }

        /// <summary>
        /// Gets all overdue invoices (due date passed and not paid).
        /// </summary>
        public async Task<List<Invoice>> GetOverdueInvoicesAsync()
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();
                var today = DateTime.Today;

                return await _context.Invoices
                    .Include(i => i.Lease)
                        .ThenInclude(l => l.Property)
                    .Include(i => i.Lease)
                        .ThenInclude(l => l.Tenant)
                    .Include(i => i.Payments)
                    .Where(i => i.Status != "Paid"
                        && i.Status != "Cancelled"
                        && i.DueOn < today
                        && !i.IsDeleted
                        && i.OrganizationId == organizationId)
                    .OrderBy(i => i.DueOn)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetOverdueInvoices");
                throw;
            }
        }

        /// <summary>
        /// Gets invoices due within the specified number of days.
        /// </summary>
        public async Task<List<Invoice>> GetInvoicesDueSoonAsync(int daysThreshold = 7)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();
                var today = DateTime.Today;
                var thresholdDate = today.AddDays(daysThreshold);

                return await _context.Invoices
                    .Include(i => i.Lease)
                        .ThenInclude(l => l.Property)
                    .Include(i => i.Lease)
                        .ThenInclude(l => l.Tenant)
                    .Include(i => i.Payments)
                    .Where(i => i.Status == "Pending"
                        && i.DueOn >= today
                        && i.DueOn <= thresholdDate
                        && !i.IsDeleted
                        && i.OrganizationId == organizationId)
                    .OrderBy(i => i.DueOn)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetInvoicesDueSoon");
                throw;
            }
        }

        /// <summary>
        /// Gets an invoice with all related entities loaded.
        /// </summary>
        public async Task<Invoice?> GetInvoiceWithRelationsAsync(Guid invoiceId)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Invoices
                    .Include(i => i.Lease)
                        .ThenInclude(l => l.Property)
                    .Include(i => i.Lease)
                        .ThenInclude(l => l.Tenant)
                    .Include(i => i.Payments)
                    .Include(i => i.Document)
                    .FirstOrDefaultAsync(i => i.Id == invoiceId
                        && !i.IsDeleted
                        && i.OrganizationId == organizationId);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetInvoiceWithRelations");
                throw;
            }
        }

        /// <summary>
        /// Generates a unique invoice number for the organization.
        /// Format: INV-YYYYMM-00001
        /// </summary>
        /// <summary>
        /// Generates invoice number in format: INV-{YYYYMM}-000n
        /// Numbers reset monthly and are scoped to organization.
        /// Uses MAX query to get last invoice number for current month.
        /// </summary>
        public async Task<string> GenerateInvoiceNumberAsync()
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();
                var yearMonth = DateTime.UtcNow.ToString("yyyyMM");
                
                // Get the highest invoice number for this month
                var lastInvoice = await _context.Invoices
                    .Where(i => i.OrganizationId == organizationId 
                        && i.InvoiceNumber.StartsWith($"INV-{yearMonth}"))
                    .OrderByDescending(i => i.InvoiceNumber)
                    .Select(i => i.InvoiceNumber)
                    .FirstOrDefaultAsync();
                
                int nextNum = 1;
                if (lastInvoice != null)
                {
                    // Extract sequence number from: INV-202602-0001
                    var parts = lastInvoice.Split('-');
                    if (parts.Length == 3)
                    {
                        nextNum = int.Parse(parts[2]) + 1;
                    }
                }
                
                return $"INV-{yearMonth}-{nextNum:D4}";
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GenerateInvoiceNumber");
                throw;
            }
        }

        /// <summary>
        /// Applies a late fee to an overdue invoice.
        /// </summary>
        public async Task<Invoice> ApplyLateFeeAsync(Guid invoiceId, decimal lateFeeAmount)
        {
            try
            {
                var invoice = await GetByIdAsync(invoiceId);
                if (invoice == null)
                {
                    throw new InvalidOperationException($"Invoice {invoiceId} not found.");
                }

                if (invoice.Status == "Paid" || invoice.Status == "Cancelled")
                {
                    throw new InvalidOperationException("Cannot apply late fee to paid or cancelled invoice.");
                }

                if (invoice.LateFeeApplied == true)
                {
                    throw new InvalidOperationException("Late fee has already been applied to this invoice.");
                }

                if (lateFeeAmount <= 0)
                {
                    throw new ArgumentException("Late fee amount must be greater than zero.");
                }

                invoice.LateFeeAmount = lateFeeAmount;
                invoice.LateFeeApplied = true;
                invoice.LateFeeAppliedOn = DateTime.UtcNow;

                // Update status to overdue if not already
                if (invoice.Status == "Pending")
                {
                    invoice.Status = "Overdue";
                }

                await UpdateAsync(invoice);

                return invoice;
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "ApplyLateFee");
                throw;
            }
        }

        /// <summary>
        /// Marks a reminder as sent for an invoice.
        /// </summary>
        public async Task<Invoice> MarkReminderSentAsync(Guid invoiceId)
        {
            try
            {
                var invoice = await GetByIdAsync(invoiceId);
                if (invoice == null)
                {
                    throw new InvalidOperationException($"Invoice {invoiceId} not found.");
                }

                invoice.ReminderSent = true;
                invoice.ReminderSentOn = DateTime.UtcNow;

                await UpdateAsync(invoice);

                return invoice;
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "MarkReminderSent");
                throw;
            }
        }

        /// <summary>
        /// Updates the invoice status based on payments received.
        /// </summary>
        public async Task<Invoice> UpdateInvoiceStatusAsync(Guid invoiceId)
        {
            try
            {
                var invoice = await GetInvoiceWithRelationsAsync(invoiceId);
                if (invoice == null)
                {
                    throw new InvalidOperationException($"Invoice {invoiceId} not found.");
                }

                // Calculate total amount due (including late fees)
                var totalDue = invoice.Amount + (invoice.LateFeeAmount ?? 0);
                var totalPaid = invoice.Payments.Where(p => !p.IsDeleted).Sum(p => p.Amount);

                invoice.AmountPaid = totalPaid;

                // Update status
                if (totalPaid >= totalDue)
                {
                    invoice.Status = "Paid";
                    invoice.PaidOn = invoice.Payments
                        .Where(p => !p.IsDeleted)
                        .OrderByDescending(p => p.PaidOn)
                        .FirstOrDefault()?.PaidOn ?? DateTime.UtcNow;
                }
                else if (invoice.Status == "Cancelled")
                {
                    // Don't change cancelled status
                }
                else if (invoice.DueOn < DateTime.Today)
                {
                    invoice.Status = "Overdue";
                }
                else
                {
                    invoice.Status = "Pending";
                }

                await UpdateAsync(invoice);

                return invoice;
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "UpdateInvoiceStatus");
                throw;
            }
        }

        /// <summary>
        /// Calculates the total outstanding balance across all unpaid invoices.
        /// </summary>
        public async Task<decimal> CalculateTotalOutstandingAsync()
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                var total = await _context.Invoices
                    .Where(i => i.Status != "Paid"
                        && i.Status != "Cancelled"
                        && !i.IsDeleted
                        && i.OrganizationId == organizationId)
                    .SumAsync(i => (i.Amount + (i.LateFeeAmount ?? 0)) - i.AmountPaid);

                return total;
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "CalculateTotalOutstanding");
                throw;
            }
        }

        /// <summary>
        /// Gets invoices within a specific date range.
        /// </summary>
        public async Task<List<Invoice>> GetInvoicesByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var organizationId = await _userContext.GetActiveOrganizationIdAsync();

                return await _context.Invoices
                    .Include(i => i.Lease)
                        .ThenInclude(l => l.Property)
                    .Include(i => i.Lease)
                        .ThenInclude(l => l.Tenant)
                    .Include(i => i.Payments)
                    .Where(i => i.InvoicedOn >= startDate
                        && i.InvoicedOn <= endDate
                        && !i.IsDeleted
                        && i.OrganizationId == organizationId)
                    .OrderByDescending(i => i.InvoicedOn)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "GetInvoicesByDateRange");
                throw;
            }
        }

        #region Delete Override

        /// <summary>
        /// Deletes an invoice and its associated payments (cascade delete).
        /// </summary>
        public override async Task<bool> DeleteAsync(Guid id)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var payments = await _context.Payments.Where(p => p.InvoiceId == id).ToListAsync();
                _context.Payments.RemoveRange(payments);
                await _context.SaveChangesAsync();
                bool result = await base.DeleteAsync(id);
                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        #endregion

        #region Cascade Summary

        /// <summary>
        /// Returns a count of related records that would be permanently deleted with this invoice.
        /// </summary>
        public override async Task<CascadeSummary> GetDeleteCascadeSummaryAsync(Guid id)
        {
            var counts = new Dictionary<string, int>
            {
                ["Payments"] = await _context.Payments.CountAsync(x => x.InvoiceId == id && !x.IsDeleted),
            };
            return new CascadeSummary { EntityName = "Invoice", Counts = counts };
        }

        /// <summary>
        /// Returns a count of related records that would be archived with this invoice.
        /// </summary>
        public override async Task<CascadeSummary> GetArchiveCascadeSummaryAsync(Guid id)
        {
            var counts = new Dictionary<string, int>
            {
                ["Payments"] = await _context.Payments.CountAsync(x => x.InvoiceId == id && !x.IsDeleted && !x.IsArchived),
            };
            return new CascadeSummary { EntityName = "Invoice", Counts = counts };
        }

        #endregion

        #region Archive / Restore

        /// <summary>
        /// Archives an invoice and cascades to its payments.
        /// </summary>
        public override async Task<bool> ArchiveAsync(Guid id)
        {
            var userId = await _userContext.GetUserIdAsync();
            var now = DateTime.UtcNow;

            await _context.Payments
                .Where(p => p.InvoiceId == id && !p.IsDeleted && !p.IsArchived)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.IsArchived, true)
                    .SetProperty(p => p.ArchivedOn, now)
                    .SetProperty(p => p.ArchivedBy, userId));

            int rows = await _context.Invoices
                .Where(i => i.Id == id && !i.IsDeleted && !i.IsArchived)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(i => i.IsArchived, true)
                    .SetProperty(i => i.ArchivedOn, now)
                    .SetProperty(i => i.ArchivedBy, userId));

            return rows > 0;
        }

        /// <summary>
        /// Restores an archived invoice. Payments are not automatically restored.
        /// </summary>
        public override async Task<bool> RestoreAsync(Guid id)
        {
            int rows = await _context.Invoices
                .Where(i => i.Id == id && i.IsArchived)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.IsArchived, false));

            return rows > 0;
        }

        #endregion
    }
}
