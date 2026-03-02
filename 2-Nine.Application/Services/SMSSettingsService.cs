using Nine.Core.Interfaces.Services;
using Nine.Core.Constants;
using Nine.Core.Entities;
using Nine.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nine.Application.Services;

namespace Nine.Application.Services
{
    public class SMSSettingsService : BaseService<OrganizationSMSSettings>
    {
        private readonly ISMSProvider _smsProvider;

        public SMSSettingsService(
            ApplicationDbContext context,
            ILogger<SMSSettingsService> logger,
            IUserContextService userContext,
            IOptions<ApplicationSettings> settings,
            ISMSProvider smsProvider)
            : base(context, logger, userContext, settings)
        {
            _smsProvider = smsProvider;
        }

        public async Task<OrganizationSMSSettings> GetOrCreateSettingsAsync()
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
                settings = new OrganizationSMSSettings
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = orgId.Value,
                    IsSMSEnabled = false,
                    CostPerSMS = 0.0075m, // Approximate US cost
                    CreatedBy = await _userContext.GetUserIdAsync() ?? string.Empty,
                    CreatedOn = DateTime.UtcNow
                };
                await CreateAsync(settings);
            }

            return settings;
        }

        public async Task<OperationResult> UpdateTwilioConfigAsync(
            string accountSid,
            string authToken,
            string phoneNumber)
        {
            // Verify credentials work before saving
            if (!await _smsProvider.VerifyCredentialsAsync(accountSid, authToken))
            {
                return OperationResult.FailureResult(
                    "Invalid Twilio credentials or phone number. Please verify your Account SID, Auth Token, and phone number.");
            }

            var settings = await GetOrCreateSettingsAsync();

            settings.TwilioAccountSidEncrypted = _smsProvider.EncryptCredential(accountSid);
            settings.TwilioAuthTokenEncrypted = _smsProvider.EncryptCredential(authToken);
            settings.TwilioPhoneNumber = phoneNumber;
            settings.IsSMSEnabled = true;
            settings.IsVerified = true;
            settings.LastVerifiedOn = DateTime.UtcNow;
            settings.LastError = null;

            await UpdateAsync(settings);

            return OperationResult.SuccessResult("Twilio configuration saved successfully");
        }

        public async Task<OperationResult> DisableSMSAsync()
        {
            var settings = await GetOrCreateSettingsAsync();
            settings.IsSMSEnabled = false;
            await UpdateAsync(settings);

            return OperationResult.SuccessResult("SMS notifications disabled");
        }

        public async Task<OperationResult> EnableSMSAsync()
        {
            var settings = await GetOrCreateSettingsAsync();

            if (string.IsNullOrEmpty(settings.TwilioAccountSidEncrypted))
            {
                return OperationResult.FailureResult(
                    "Twilio credentials not configured. Please configure Twilio first.");
            }

            settings.IsSMSEnabled = true;
            await UpdateAsync(settings);

            return OperationResult.SuccessResult("SMS notifications enabled");
        }

        public async Task<OperationResult> TestSMSConfigurationAsync(string testPhoneNumber)
        {
            try
            {
                await _smsProvider.SendSMSAsync(
                    testPhoneNumber,
                    "Nine SMS Configuration Test: This message confirms your Twilio integration is working correctly.");

                return OperationResult.SuccessResult("Test SMS sent successfully! Check your phone.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Test SMS failed");
                return OperationResult.FailureResult($"Failed to send test SMS: {ex.Message}");
            }
        }
    }
}