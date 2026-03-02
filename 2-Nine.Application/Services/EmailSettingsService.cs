using Nine.Core.Interfaces.Services;
using Nine.Core.Constants;
using Nine.Core.Entities;
using Nine.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nine.Application.Services
{
    public class EmailSettingsService : BaseService<OrganizationEmailSettings>
    {
        private readonly IEmailProvider _emailProvider;

        public EmailSettingsService(
            ApplicationDbContext context,
            ILogger<EmailSettingsService> logger,
            IUserContextService userContext,
            IOptions<ApplicationSettings> settings,
            IEmailProvider emailProvider)
            : base(context, logger, userContext, settings)
        {
            _emailProvider = emailProvider;
        }

        /// <summary>
        /// Get email settings for current organization or create default disabled settings
        /// </summary>
        public async Task<OrganizationEmailSettings> GetOrCreateSettingsAsync()
        {
            var orgId = await _userContext.GetActiveOrganizationIdAsync();
            if (orgId == null)
            {
                throw new UnauthorizedAccessException("No active organization");
            }

            var settings = await _dbSet
                .FirstOrDefaultAsync(s => s.OrganizationId == orgId && !s.IsDeleted);

            if (settings == null)
            {
                settings = new OrganizationEmailSettings
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = orgId.Value,
                    IsEmailEnabled = false,
                    DailyLimit = 100,  // SendGrid free tier default
                    MonthlyLimit = 40000,
                    CreatedBy = await _userContext.GetUserIdAsync() ?? string.Empty,
                    CreatedOn = DateTime.UtcNow
                };
                await CreateAsync(settings);
            }

            return settings;
        }

        /// <summary>
        /// Configure SendGrid API key and enable email functionality
        /// </summary>
        public async Task<OperationResult> UpdateSendGridConfigAsync(
            string apiKey,
            string fromEmail,
            string fromName)
        {
            // Verify the API key works before saving
            if (!await _emailProvider.VerifyApiKeyAsync(apiKey))
            {
                return OperationResult.FailureResult(
                    "Invalid SendGrid API key. Please verify the key has Mail Send permissions.");
            }

            var settings = await GetOrCreateSettingsAsync();

            settings.SendGridApiKeyEncrypted = _emailProvider.EncryptApiKey(apiKey);
            settings.FromEmail = fromEmail;
            settings.FromName = fromName;
            settings.IsEmailEnabled = true;
            settings.IsVerified = true;
            settings.LastVerifiedOn = DateTime.UtcNow;
            settings.LastError = null;

            await UpdateAsync(settings);

            return OperationResult.SuccessResult("SendGrid configuration saved successfully");
        }

        /// <summary>
        /// Disable email functionality for organization
        /// </summary>
        public async Task<OperationResult> DisableEmailAsync()
        {
            var settings = await GetOrCreateSettingsAsync();
            settings.IsEmailEnabled = false;
            await UpdateAsync(settings);

            return OperationResult.SuccessResult("Email notifications disabled");
        }

        /// <summary>
        /// Re-enable email functionality
        /// </summary>
        public async Task<OperationResult> EnableEmailAsync()
        {
            var settings = await GetOrCreateSettingsAsync();

            if (string.IsNullOrEmpty(settings.SendGridApiKeyEncrypted))
            {
                return OperationResult.FailureResult(
                    "SendGrid API key not configured. Please configure SendGrid first.");
            }

            settings.IsEmailEnabled = true;
            await UpdateAsync(settings);

            return OperationResult.SuccessResult("Email notifications enabled");
        }

        /// <summary>
        /// Send a test email to verify configuration
        /// </summary>
        public async Task<OperationResult> TestEmailConfigurationAsync(string testEmail)
        {
            try
            {
                await _emailProvider.SendEmailAsync(
                    testEmail,
                    "Nine Email Configuration Test",
                    "<h2>Configuration Test Successful!</h2>" +
                    "<p>This is a test email to verify your SendGrid configuration is working correctly.</p>" +
                    "<p>If you received this email, your email integration is properly configured.</p>");

                return OperationResult.SuccessResult("Test email sent successfully! Check your inbox.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Test email failed");
                return OperationResult.FailureResult($"Failed to send test email: {ex.Message}");
            }
        }

        /// <summary>
        /// Update email sender information
        /// </summary>
        public async Task<OperationResult> UpdateSenderInfoAsync(string fromEmail, string fromName)
        {
            var settings = await GetOrCreateSettingsAsync();

            settings.FromEmail = fromEmail;
            settings.FromName = fromName;

            await UpdateAsync(settings);

            return OperationResult.SuccessResult("Sender information updated");
        }
    }
}