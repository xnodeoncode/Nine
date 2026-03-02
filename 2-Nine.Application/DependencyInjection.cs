using Nine.Application.Services;
using Nine.Application.Services.PdfGenerators;
using Nine.Application.Services.Workflows;
using Nine.Infrastructure;
using Nine.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Nine.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Register Application layer services and Infrastructure internally.
    /// This is the ONLY method products should call for dependency registration.
    /// Note: IDatabaseService must be registered by the product layer since it requires
    /// the product-specific Identity context (e.g., NineDbContext).
    /// </summary>
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        string connectionString,
        string? encryptionPassword = null,
        SqlCipherConnectionInterceptor? interceptor = null)
    {
        // Call Infrastructure registration internally with encryption interceptor
        services.AddInfrastructure(connectionString, encryptionPassword, interceptor);
        
        // Register all Application services
        services.AddScoped<AccountWorkflowService>();
        services.AddScoped<ApplicationService>();
        services.AddScoped<ApplicationWorkflowService>();
        services.AddScoped<CalendarEventService>();
        services.AddScoped<CalendarSettingsService>();
        services.AddScoped<ChecklistService>();
        services.AddScoped<DigestService>();
        services.AddScoped<DocumentNotificationService>();
        services.AddScoped<DocumentService>();
        services.AddScoped<DatabasePreviewService>();
        services.AddScoped<EmailService>();
        services.AddScoped<EmailSettingsService>();
        services.AddScoped<FinancialReportService>();
        services.AddScoped<InspectionService>();
        services.AddScoped<InvoiceService>();
        services.AddScoped<LeaseNotificationService>();
        services.AddScoped<LeaseOfferService>();
        services.AddScoped<LeasePdfGenerator>();
        services.AddScoped<LeaseService>();
        services.AddScoped<LeaseWorkflowService>();
        services.AddScoped<MaintenanceNotificationService>();
        services.AddScoped<MaintenanceService>();
        services.AddScoped<NoteService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<OrganizationService>();
        services.AddScoped<PaymentService>();
        services.AddScoped<PropertyManagementService>();
        services.AddScoped<PropertyService>();
        services.AddScoped<ProspectiveTenantService>();
        services.AddScoped<RentalApplicationService>();
        services.AddScoped<RepairService>();
        services.AddScoped<SampleDataWorkflowService>();
        services.AddScoped<ScheduledTaskService>();
        services.AddScoped<SchemaValidationService>();
        services.AddScoped<ScreeningService>();
        services.AddScoped<SecurityDepositService>();
        services.AddScoped<SMSService>();
        services.AddScoped<SMSSettingsService>();
        services.AddScoped<TenantConversionService>();
        services.AddScoped<TenantService>();
        services.AddScoped<TourService>();
        
        return services;
    }
}
