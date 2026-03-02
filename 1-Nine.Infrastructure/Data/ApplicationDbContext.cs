using Microsoft.EntityFrameworkCore;
using Nine.Core.Entities;
using Nine.Core.Interfaces;

namespace Nine.Infrastructure.Data
{
    /// <summary>
    /// Main application database context for business entities only.
    /// Identity management is handled by product-specific contexts.
    /// Products can extend via partial classes following the Portable Feature pattern.
    /// </summary>
    public partial class ApplicationDbContext : DbContext, IApplicationDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public override int SaveChanges()
        {
            SanitizeStringProperties();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SanitizeStringProperties();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void SanitizeStringProperties()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                foreach (var property in entry.Properties)
                {
                    if (property.Metadata.ClrType == typeof(string) && property.CurrentValue != null)
                    {
                        var value = property.CurrentValue as string;
                        if (!string.IsNullOrEmpty(value))
                        {
                            // Trim leading/trailing whitespace
                            property.CurrentValue = value.Trim();
                        }
                    }
                }
            }
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            
            // Suppress pending model changes warning - bidirectional Document-Invoice/Payment relationship issue
            // TODO: Fix the Document-Invoice and Document-Payment bidirectional relationships properly
            optionsBuilder.ConfigureWarnings(warnings =>
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        }

        public DbSet<Property> Properties { get; set; }
        public DbSet<Lease> Leases { get; set; }
        public DbSet<LeaseOffer> LeaseOffers { get; set; }
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<Inspection> Inspections { get; set; }
        public DbSet<MaintenanceRequest> MaintenanceRequests { get; set; }
        public DbSet<Repair> Repairs { get; set; }
        public DbSet<OrganizationSettings> OrganizationSettings { get; set; }
        public DbSet<SchemaVersion> SchemaVersions { get; set; }
        public DbSet<DatabaseSettings> DatabaseSettings { get; set; }
        public DbSet<ChecklistTemplate> ChecklistTemplates { get; set; }
        public DbSet<ChecklistTemplateItem> ChecklistTemplateItems { get; set; }
        public DbSet<Checklist> Checklists { get; set; }
        public DbSet<ChecklistItem> ChecklistItems { get; set; }
        public DbSet<ProspectiveTenant> ProspectiveTenants { get; set; }
        public DbSet<Tour> Tours { get; set; }
        public DbSet<RentalApplication> RentalApplications { get; set; }
        public DbSet<ApplicationScreening> ApplicationScreenings { get; set; }
        public DbSet<CalendarEvent> CalendarEvents { get; set; }
        public DbSet<CalendarSettings> CalendarSettings { get; set; }
        public DbSet<Note> Notes { get; set; }
        public DbSet<SecurityDeposit> SecurityDeposits { get; set; }
        public DbSet<SecurityDepositInvestmentPool> SecurityDepositInvestmentPools { get; set; }
        public DbSet<SecurityDepositDividend> SecurityDepositDividends { get; set; }
        
        // Multi-organization support
        public DbSet<Organization> Organizations { get; set; }
        public DbSet<OrganizationUser> OrganizationUsers { get; set; }
        
        // User profiles (business context - separate from Identity)
        public DbSet<UserProfile> UserProfiles { get; set; }
        
        // Workflow audit logging
        public DbSet<WorkflowAuditLog> WorkflowAuditLogs { get; set; }


        // Notification system
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<NotificationPreferences> NotificationPreferences { get; set; }
        public DbSet<OrganizationEmailSettings> OrganizationEmailSettings { get; set; }
        public DbSet<OrganizationSMSSettings> OrganizationSMSSettings { get; set; }

         protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Property entity
            modelBuilder.Entity<Property>(entity =>
            {
                entity.HasIndex(e => e.Address);
                entity.Property(e => e.MonthlyRent).HasPrecision(18, 2);
                
                // Configure relationship with Organization
                entity.HasOne<Organization>()
                    .WithMany(o => o.Properties)
                    .HasForeignKey(e => e.OrganizationId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // Configure Tenant entity
            modelBuilder.Entity<Tenant>(entity =>
            {
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.IdentificationNumber).IsUnique();
                
                // Configure relationship with Organization
                entity.HasOne<Organization>()
                    .WithMany(o => o.Tenants)
                    .HasForeignKey(e => e.OrganizationId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // Configure Lease entity
            modelBuilder.Entity<Lease>(entity =>
            {
                entity.HasOne(l => l.Property)
                    .WithMany(p => p.Leases)
                    .HasForeignKey(l => l.PropertyId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(l => l.Tenant)
                    .WithMany(t => t.Leases)
                    .HasForeignKey(l => l.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(l => l.Document)
                    .WithMany()
                    .HasForeignKey(l => l.DocumentId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.Property(e => e.MonthlyRent).HasPrecision(18, 2);
                entity.Property(e => e.SecurityDeposit).HasPrecision(18, 2);
                
                // Configure relationship with Organization
                entity.HasOne<Organization>()
                    .WithMany(o => o.Leases)
                    .HasForeignKey(e => e.OrganizationId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure Invoice entity
            modelBuilder.Entity<Invoice>(entity =>
            {
                entity.HasOne(i => i.Lease)
                    .WithMany(l => l.Invoices)
                    .HasForeignKey(i => i.LeaseId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(i => i.Document)
                    .WithMany()
                    .HasForeignKey(i => i.DocumentId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Unique constraint on (OrganizationId, InvoiceNumber) for multi-tenant isolation
                entity.HasIndex(e => new { e.OrganizationId, e.InvoiceNumber })
                    .IsUnique()
                    .HasDatabaseName("IX_Invoice_OrgId_InvoiceNumber");
                    
                entity.Property(e => e.Amount).HasPrecision(18, 2);
                entity.Property(e => e.AmountPaid).HasPrecision(18, 2);
                
                // Configure relationship with User
                entity.HasOne<Organization>()
                    .WithMany()
                    .HasForeignKey(e => e.OrganizationId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure Payment entity
            modelBuilder.Entity<Payment>(entity =>
            {
                entity.HasOne(p => p.Invoice)
                    .WithMany(i => i.Payments)
                    .HasForeignKey(p => p.InvoiceId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.Document)
                    .WithMany()
                    .HasForeignKey(p => p.DocumentId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Unique constraint on (OrganizationId, PaymentNumber) for multi-tenant isolation
                entity.HasIndex(e => new { e.OrganizationId, e.PaymentNumber })
                    .IsUnique()
                    .HasDatabaseName("IX_Payment_OrgId_PaymentNumber");

                entity.Property(e => e.Amount).HasPrecision(18, 2);
                
                // Configure relationship with User
                entity.HasOne<Organization>()
                    .WithMany()
                    .HasForeignKey(e => e.OrganizationId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure Document entity
            modelBuilder.Entity<Document>(entity =>
            {
                entity.HasOne(d => d.Property)
                    .WithMany(p => p.Documents)
                    .HasForeignKey(d => d.PropertyId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(d => d.Tenant)
                    .WithMany()
                    .HasForeignKey(d => d.TenantId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(d => d.Lease)
                    .WithMany(l => l.Documents)
                    .HasForeignKey(d => d.LeaseId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(d => d.Invoice)
                    .WithMany()
                    .HasForeignKey(d => d.InvoiceId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(d => d.Payment)
                    .WithMany()
                    .HasForeignKey(d => d.PaymentId)
                    .OnDelete(DeleteBehavior.SetNull);
                
                // FileData is automatically stored as BLOB in SQLite
                // No need to specify column type
                
                // Configure relationship with User
                entity.HasOne<Organization>()
                    .WithMany()
                    .HasForeignKey(e => e.OrganizationId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure Inspection entity
            modelBuilder.Entity<Inspection>(entity =>
            {
                entity.HasOne(i => i.Property)
                    .WithMany()
                    .HasForeignKey(i => i.PropertyId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(i => i.Lease)
                    .WithMany()
                    .HasForeignKey(i => i.LeaseId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(i => i.Document)
                    .WithMany()
                    .HasForeignKey(i => i.DocumentId)
                    .OnDelete(DeleteBehavior.SetNull);
                
                entity.HasIndex(e => e.PropertyId);
                entity.HasIndex(e => e.CompletedOn);
            });

            // Configure MaintenanceRequest entity
            modelBuilder.Entity<MaintenanceRequest>(entity =>
            {
                entity.HasOne(m => m.Property)
                    .WithMany(p => p.MaintenanceRequests)
                    .HasForeignKey(m => m.PropertyId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(m => m.Lease)
                    .WithMany()
                    .HasForeignKey(m => m.LeaseId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.Property(e => e.EstimatedCost).HasPrecision(18, 2);
                entity.Property(e => e.ActualCost).HasPrecision(18, 2);
                
                entity.HasIndex(e => e.PropertyId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.Priority);
                entity.HasIndex(e => e.RequestedOn);
            });

            // Configure Repair entity
            modelBuilder.Entity<Repair>(entity =>
            {
                // Required relationship: Property (Restrict - can't delete property with repairs)
                entity.HasOne(r => r.Property)
                    .WithMany(p => p.Repairs)
                    .HasForeignKey(r => r.PropertyId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Optional relationship: MaintenanceRequest (SetNull - repairs can outlive MR)
                entity.HasOne(r => r.MaintenanceRequest)
                    .WithMany(mr => mr.Repairs)
                    .HasForeignKey(r => r.MaintenanceRequestId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Optional relationship: Lease (SetNull - repairs can outlive lease)
                entity.HasOne(r => r.Lease)
                    .WithMany()
                    .HasForeignKey(r => r.LeaseId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Decimal precision for Cost field
                entity.Property(e => e.Cost).HasPrecision(18, 2);
                
                // Indexes for query optimization
                entity.HasIndex(e => e.OrganizationId);
                entity.HasIndex(e => e.PropertyId);
                entity.HasIndex(e => e.MaintenanceRequestId);
                entity.HasIndex(e => e.LeaseId);
                entity.HasIndex(e => e.CompletedOn);
                entity.HasIndex(e => e.RepairType);
            });

            // Configure OrganizationSettings entity
            modelBuilder.Entity<OrganizationSettings>(entity =>
            {
                entity.Property(e => e.OrganizationId).HasConversion<string>();
                entity.HasIndex(e => e.OrganizationId).IsUnique();
                entity.Property(e => e.LateFeePercentage).HasPrecision(5, 4);
                entity.Property(e => e.MaxLateFeeAmount).HasPrecision(18, 2);
                entity.Property(e => e.DefaultApplicationFee).HasPrecision(18, 2);
                entity.Property(e => e.OrganizationSharePercentage).HasPrecision(18, 6);
                entity.Property(e => e.SecurityDepositMultiplier).HasPrecision(18, 2);
            });

            // Configure ChecklistTemplate entity
            modelBuilder.Entity<ChecklistTemplate>(entity =>
            {
                entity.HasIndex(e => e.OrganizationId);
                entity.HasIndex(e => e.Category);
            });

            // Configure ChecklistTemplateItem entity
            modelBuilder.Entity<ChecklistTemplateItem>(entity =>
            {
                entity.HasOne(cti => cti.ChecklistTemplate)
                    .WithMany(ct => ct.Items)
                    .HasForeignKey(cti => cti.ChecklistTemplateId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.ChecklistTemplateId);
            });

            // Configure Checklist entity
            modelBuilder.Entity<Checklist>(entity =>
            {
                entity.HasOne(c => c.Property)
                    .WithMany()
                    .HasForeignKey(c => c.PropertyId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.Lease)
                    .WithMany()
                    .HasForeignKey(c => c.LeaseId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(c => c.ChecklistTemplate)
                    .WithMany(ct => ct.Checklists)
                    .HasForeignKey(c => c.ChecklistTemplateId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.Document)
                    .WithMany()
                    .HasForeignKey(c => c.DocumentId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(e => e.PropertyId);
                entity.HasIndex(e => e.LeaseId);
                entity.HasIndex(e => e.ChecklistType);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.CompletedOn);
            });

            // Configure ChecklistItem entity
            modelBuilder.Entity<ChecklistItem>(entity =>
            {
                entity.HasOne(ci => ci.Checklist)
                    .WithMany(c => c.Items)
                    .HasForeignKey(ci => ci.ChecklistId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.ChecklistId);
            });

            // Configure ProspectiveTenant entity
            modelBuilder.Entity<ProspectiveTenant>(entity =>
            {
                entity.HasOne(pt => pt.InterestedProperty)
                    .WithMany()
                    .HasForeignKey(pt => pt.InterestedPropertyId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(e => e.Email);
                entity.HasIndex(e => e.OrganizationId);
                entity.HasIndex(e => e.Status);
            });

            // Configure Tour entity
            modelBuilder.Entity<Tour>(entity =>
            {
                entity.HasOne(s => s.ProspectiveTenant)
                    .WithMany(pt => pt.Tours)
                    .HasForeignKey(s => s.ProspectiveTenantId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(s => s.Property)
                    .WithMany()
                    .HasForeignKey(s => s.PropertyId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.OrganizationId);
                entity.HasIndex(e => e.ScheduledOn);
                entity.HasIndex(e => e.Status);
            });

            // Configure RentalApplication entity
            // A prospect may have multiple applications over time, but only one "active" application at a time.
            // Active = not yet disposed (not approved/denied/withdrawn/expired/lease-declined)
            modelBuilder.Entity<RentalApplication>(entity =>
            {
                entity.HasOne(ra => ra.ProspectiveTenant)
                    .WithMany(pt => pt.Applications)
                    .HasForeignKey(ra => ra.ProspectiveTenantId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(ra => ra.Property)
                    .WithMany()
                    .HasForeignKey(ra => ra.PropertyId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(e => e.CurrentRent).HasPrecision(18, 2);
                entity.Property(e => e.MonthlyIncome).HasPrecision(18, 2);
                entity.Property(e => e.ApplicationFee).HasPrecision(18, 2);

                entity.HasIndex(e => e.OrganizationId);
                entity.HasIndex(e => e.AppliedOn);
                entity.HasIndex(e => e.Status);
            });

            // Configure ApplicationScreening entity
            modelBuilder.Entity<ApplicationScreening>(entity =>
            {
                entity.HasOne(asc => asc.RentalApplication)
                    .WithOne(ra => ra.Screening)
                    .HasForeignKey<ApplicationScreening>(asc => asc.RentalApplicationId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.OrganizationId);
                entity.HasIndex(e => e.OverallResult);
            });

            // Configure CalendarEvent entity
            modelBuilder.Entity<CalendarEvent>(entity =>
            {
                entity.HasOne(ce => ce.Property)
                    .WithMany()
                    .HasForeignKey(ce => ce.PropertyId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(e => e.OrganizationId);
                entity.HasIndex(e => e.StartOn);
                entity.HasIndex(e => e.EventType);
                entity.HasIndex(e => e.SourceEntityId);
                entity.HasIndex(e => new { e.SourceEntityType, e.SourceEntityId });
            });

            // Configure CalendarSettings entity
            modelBuilder.Entity<CalendarSettings>(entity =>
            {
                entity.HasIndex(e => e.OrganizationId);
                entity.HasIndex(e => new { e.OrganizationId, e.EntityType }).IsUnique();
            });

            // Configure SecurityDeposit entity
            modelBuilder.Entity<SecurityDeposit>(entity =>
            {
                entity.HasOne(sd => sd.Lease)
                    .WithMany()
                    .HasForeignKey(sd => sd.LeaseId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(sd => sd.Tenant)
                    .WithMany()
                    .HasForeignKey(sd => sd.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(e => e.Amount).HasPrecision(18, 2);
                entity.Property(e => e.RefundAmount).HasPrecision(18, 2);
                entity.Property(e => e.DeductionsAmount).HasPrecision(18, 2);

                entity.HasIndex(e => e.LeaseId).IsUnique();
                entity.HasIndex(e => e.TenantId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.InInvestmentPool);
            });

            // Configure SecurityDepositInvestmentPool entity
            modelBuilder.Entity<SecurityDepositInvestmentPool>(entity =>
            {
                entity.Property(e => e.StartingBalance).HasPrecision(18, 2);
                entity.Property(e => e.EndingBalance).HasPrecision(18, 2);
                entity.Property(e => e.TotalEarnings).HasPrecision(18, 2);
                entity.Property(e => e.ReturnRate).HasPrecision(18, 6);
                entity.Property(e => e.OrganizationSharePercentage).HasPrecision(18, 6);
                entity.Property(e => e.OrganizationShare).HasPrecision(18, 2);
                entity.Property(e => e.TenantShareTotal).HasPrecision(18, 2);
                entity.Property(e => e.DividendPerLease).HasPrecision(18, 2);

                entity.HasIndex(e => e.OrganizationId);
                entity.HasIndex(e => e.Year).IsUnique();
                entity.HasIndex(e => e.Status);
            });

            // Configure SecurityDepositDividend entity
            modelBuilder.Entity<SecurityDepositDividend>(entity =>
            {
                entity.HasOne(sdd => sdd.SecurityDeposit)
                    .WithMany(sd => sd.Dividends)
                    .HasForeignKey(sdd => sdd.SecurityDepositId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(sdd => sdd.InvestmentPool)
                    .WithMany(ip => ip.Dividends)
                    .HasForeignKey(sdd => sdd.InvestmentPoolId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(sdd => sdd.Lease)
                    .WithMany()
                    .HasForeignKey(sdd => sdd.LeaseId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(sdd => sdd.Tenant)
                    .WithMany()
                    .HasForeignKey(sdd => sdd.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(e => e.BaseDividendAmount).HasPrecision(18, 2);
                entity.Property(e => e.ProrationFactor).HasPrecision(18, 6);
                entity.Property(e => e.DividendAmount).HasPrecision(18, 2);

                entity.HasIndex(e => e.SecurityDepositId);
                entity.HasIndex(e => e.InvestmentPoolId);
                entity.HasIndex(e => e.LeaseId);
                entity.HasIndex(e => e.TenantId);
                entity.HasIndex(e => e.Year);
                entity.HasIndex(e => e.Status);
            });

            // Configure Organization entity
            modelBuilder.Entity<Organization>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.HasIndex(e => e.OwnerId);
                entity.HasIndex(e => e.IsActive);
                
                // OwnerId is a string foreign key to AspNetUsers (managed by NineDbContext)
                // No navigation property configured here
            });

            // Configure OrganizationUser entity
            modelBuilder.Entity<OrganizationUser>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.HasOne(uo => uo.Organization)
                    .WithMany(o => o.OrganizationUsers)
                    .HasForeignKey(uo => uo.OrganizationId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                // UserId and GrantedBy are string foreign keys to AspNetUsers (managed by NineDbContext)
                // No navigation properties configured here

                // Unique constraint: one role per user per organization
                entity.HasIndex(e => new { e.UserId, e.OrganizationId }).IsUnique();
                entity.HasIndex(e => e.OrganizationId);
                entity.HasIndex(e => e.Role);
                entity.HasIndex(e => e.IsActive);
            });

            // Configure WorkflowAuditLog entity
            modelBuilder.Entity<WorkflowAuditLog>(entity =>
            {
                entity.HasIndex(e => e.OrganizationId);
                entity.HasIndex(e => e.EntityType);
                entity.HasIndex(e => e.EntityId);
                entity.HasIndex(e => new { e.EntityType, e.EntityId });
                entity.HasIndex(e => e.Action);
                entity.HasIndex(e => e.PerformedOn);
                entity.HasIndex(e => e.PerformedBy);
            });

            // Configure Notification entity
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.HasIndex(e => e.RecipientUserId);
                entity.HasIndex(e => e.OrganizationId);
                entity.HasIndex(e => e.SentOn);
                entity.HasIndex(e => e.IsRead);
                entity.HasIndex(e => e.Category);
                
                // Organization relationship
                entity.HasOne(n => n.Organization)
                    .WithMany()
                    .HasForeignKey(n => n.OrganizationId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                // RecipientUserId is a string foreign key to AspNetUsers (managed by NineDbContext)
                // No navigation property configured here
            });

            // Configure NotificationPreferences entity
            modelBuilder.Entity<NotificationPreferences>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.OrganizationId);
                
                // Unique constraint: one preference record per user per organization
                entity.HasIndex(e => new { e.UserId, e.OrganizationId })
                    .IsUnique();
                
                // Organization relationship
                entity.HasOne(np => np.Organization)
                    .WithMany()
                    .HasForeignKey(np => np.OrganizationId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                // UserId is a string foreign key to AspNetUsers (managed by NineDbContext)
                // No navigation property configured here
            });

            // Configure OrganizationEmailSettings entity
            modelBuilder.Entity<OrganizationEmailSettings>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.HasIndex(e => e.OrganizationId).IsUnique();
                
                // Organization relationship - one settings record per organization
                entity.HasOne(es => es.Organization)
                    .WithMany()
                    .HasForeignKey(es => es.OrganizationId)
                    .OnDelete(DeleteBehavior.Cascade);
                
            });

            // Configure OrganizationSMSSettings entity
            modelBuilder.Entity<OrganizationSMSSettings>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.HasIndex(e => e.OrganizationId).IsUnique();
                
                // Organization relationship - one settings record per organization
                entity.HasOne(ss => ss.Organization)
                    .WithMany()
                    .HasForeignKey(ss => ss.OrganizationId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                // Precision for financial fields
                entity.Property(e => e.AccountBalance).HasPrecision(18, 2);
                entity.Property(e => e.CostPerSMS).HasPrecision(18, 4);
            });

            // Configure UserProfile entity
            modelBuilder.Entity<UserProfile>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                // Unique constraint: one profile per user
                entity.HasIndex(e => e.UserId).IsUnique();
                
                // Additional indexes for common queries
                entity.HasIndex(e => e.Email);
                entity.HasIndex(e => e.OrganizationId);
                entity.HasIndex(e => e.ActiveOrganizationId);
                entity.HasIndex(e => e.IsDeleted);
                
                // Note: No navigation property to AspNetUsers (different context)
                // UserId is a string FK to Identity context, but no EF relationship configured
            });

            // Seed System Checklist Templates
            SeedChecklistTemplates(modelBuilder);
        }

        private void SeedChecklistTemplates(ModelBuilder modelBuilder)
        {
            var systemTimestamp = DateTime.Parse("2025-11-30T00:00:00Z").ToUniversalTime();
            
            // Fixed GUIDs for system templates (consistent across deployments)
            var propertyTourTemplateId = Guid.Parse("00000000-0000-0000-0001-000000000001");
            var moveInTemplateId = Guid.Parse("00000000-0000-0000-0001-000000000002");
            var moveOutTemplateId = Guid.Parse("00000000-0000-0000-0001-000000000003");
            var openHouseTemplateId = Guid.Parse("00000000-0000-0000-0001-000000000004");
            
            // Seed ChecklistTemplates
            modelBuilder.Entity<ChecklistTemplate>().HasData(
                new ChecklistTemplate
                {
                    Id = propertyTourTemplateId,
                    Name = "Property Tour",
                    Description = "Standard property showing checklist",
                    Category = "Tour",
                    IsSystemTemplate = true,
                    OrganizationId = Guid.Empty,
                    CreatedOn = systemTimestamp,
                    CreatedBy = string.Empty,
                    IsDeleted = false
                },
                new ChecklistTemplate
                {
                    Id = moveInTemplateId,
                    Name = "Move-In",
                    Description = "Move-in inspection checklist",
                    Category = "MoveIn",
                    IsSystemTemplate = true,
                    OrganizationId = Guid.Empty,
                    CreatedOn = systemTimestamp,
                    CreatedBy = string.Empty,
                    IsDeleted = false
                },
                new ChecklistTemplate
                {
                    Id = moveOutTemplateId,
                    Name = "Move-Out",
                    Description = "Move-out inspection checklist",
                    Category = "MoveOut",
                    IsSystemTemplate = true,
                    OrganizationId = Guid.Empty,
                    CreatedOn = systemTimestamp,
                    CreatedBy = string.Empty,
                    IsDeleted = false
                },
                new ChecklistTemplate
                {
                    Id = openHouseTemplateId,
                    Name = "Open House",
                    Description = "Open house event checklist",
                    Category = "Tour",
                    IsSystemTemplate = true,
                    OrganizationId = Guid.Empty,
                    CreatedOn = systemTimestamp,
                    CreatedBy = string.Empty,
                    IsDeleted = false
                }
            );

            // Seed Property Tour Checklist Items
            modelBuilder.Entity<ChecklistTemplateItem>().HasData(
                // Arrival & Introduction (Section 1)
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000001"), ChecklistTemplateId = propertyTourTemplateId, ItemText = "Greeted prospect and verified appointment", ItemOrder = 1, CategorySection = "Arrival & Introduction", SectionOrder = 1, IsRequired = true, RequiresValue = false, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false },
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000002"), ChecklistTemplateId = propertyTourTemplateId, ItemText = "Reviewed property exterior and curb appeal", ItemOrder = 2, CategorySection = "Arrival & Introduction", SectionOrder = 1, IsRequired = true, RequiresValue = false, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false },
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000003"), ChecklistTemplateId = propertyTourTemplateId, ItemText = "Showed parking area/garage", ItemOrder = 3, CategorySection = "Arrival & Introduction", SectionOrder = 1, IsRequired = true, RequiresValue = false, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false },

                // Interior Tour (Section 2)
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000004"), ChecklistTemplateId = propertyTourTemplateId, ItemText = "Toured living room/common areas", ItemOrder = 4, CategorySection = "Interior Tour", SectionOrder = 2, IsRequired = true, RequiresValue = false, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false },
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000005"), ChecklistTemplateId = propertyTourTemplateId, ItemText = "Showed all bedrooms", ItemOrder = 5, CategorySection = "Interior Tour", SectionOrder = 2, IsRequired = true, RequiresValue = false, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false },
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000006"), ChecklistTemplateId = propertyTourTemplateId, ItemText = "Showed all bathrooms", ItemOrder = 6, CategorySection = "Interior Tour", SectionOrder = 2, IsRequired = true, RequiresValue = false, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false },

                // Kitchen & Appliances (Section 3)
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000007"), ChecklistTemplateId = propertyTourTemplateId, ItemText = "Toured kitchen and demonstrated appliances", ItemOrder = 7, CategorySection = "Kitchen & Appliances", SectionOrder = 3, IsRequired = true, RequiresValue = false, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false },
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000008"), ChecklistTemplateId = propertyTourTemplateId, ItemText = "Explained which appliances are included", ItemOrder = 8, CategorySection = "Kitchen & Appliances", SectionOrder = 3, IsRequired = true, RequiresValue = false, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false },
                // Utilities & Systems (Section 4)
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000009"), ChecklistTemplateId = propertyTourTemplateId, ItemText = "Explained HVAC system and thermostat controls", ItemOrder = 9, CategorySection = "Utilities & Systems", SectionOrder = 4, IsRequired = true, RequiresValue = false, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false },
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000010"), ChecklistTemplateId = propertyTourTemplateId, ItemText = "Reviewed utility responsibilities (tenant vs landlord)", ItemOrder = 10, CategorySection = "Utilities & Systems", SectionOrder = 4, IsRequired = true, RequiresValue = false, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false },
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000011"), ChecklistTemplateId = propertyTourTemplateId, ItemText = "Showed water heater location", ItemOrder = 11, CategorySection = "Utilities & Systems", SectionOrder = 4, IsRequired = true, RequiresValue = false, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false },
                // Storage & Amenities (Section 5)
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000012"), ChecklistTemplateId = propertyTourTemplateId, ItemText = "Showed storage areas (closets, attic, basement)", ItemOrder = 12, CategorySection = "Storage & Amenities", SectionOrder = 5, IsRequired = true, RequiresValue = false, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false },
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000013"), ChecklistTemplateId = propertyTourTemplateId, ItemText = "Showed laundry facilities", ItemOrder = 13, CategorySection = "Storage & Amenities", SectionOrder = 5, IsRequired = true, RequiresValue = false, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false },
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000014"), ChecklistTemplateId = propertyTourTemplateId, ItemText = "Showed outdoor space (yard, patio, balcony)", ItemOrder = 14, CategorySection = "Storage & Amenities", SectionOrder = 5, IsRequired = true, RequiresValue = false, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false },
                // Lease Terms (Section 6)
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000015"), ChecklistTemplateId = propertyTourTemplateId, ItemText = "Discussed monthly rent amount", ItemOrder = 15, CategorySection = "Lease Terms", SectionOrder = 6, IsRequired = true, RequiresValue = false, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false },
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000016"), ChecklistTemplateId = propertyTourTemplateId, ItemText = "Explained security deposit and move-in costs", ItemOrder = 16, CategorySection = "Lease Terms", SectionOrder = 6, IsRequired = true, RequiresValue = false, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false },
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000017"), ChecklistTemplateId = propertyTourTemplateId, ItemText = "Reviewed lease term length and start date", ItemOrder = 17, CategorySection = "Lease Terms", SectionOrder = 6, IsRequired = true, RequiresValue = false, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false },
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000018"), ChecklistTemplateId = propertyTourTemplateId, ItemText = "Explained pet policy", ItemOrder = 18, CategorySection = "Lease Terms", SectionOrder = 6, IsRequired = true, RequiresValue = false, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false },

                // Next Steps (Section 7)
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000019"), ChecklistTemplateId = propertyTourTemplateId, ItemText = "Explained application process and requirements", ItemOrder = 19, CategorySection = "Next Steps", SectionOrder = 7, IsRequired = true, RequiresValue = false, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false },
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000020"), ChecklistTemplateId = propertyTourTemplateId, ItemText = "Reviewed screening process (background, credit check)", ItemOrder = 20, CategorySection = "Next Steps", SectionOrder = 7, IsRequired = true, RequiresValue = false, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false },
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000021"), ChecklistTemplateId = propertyTourTemplateId, ItemText = "Answered all prospect questions", ItemOrder = 21, CategorySection = "Next Steps", SectionOrder = 7, IsRequired = true, RequiresValue = false, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false },

                // Assessment (Section 8)
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000022"), ChecklistTemplateId = propertyTourTemplateId, ItemText = "Prospect Interest Level", ItemOrder = 22, CategorySection = "Assessment", SectionOrder = 8, IsRequired = true, RequiresValue = true, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false },
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000023"), ChecklistTemplateId = propertyTourTemplateId, ItemText = "Overall showing feedback and notes", ItemOrder = 23, CategorySection = "Assessment", SectionOrder = 8, IsRequired = true, RequiresValue = false, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false },
                // Move-In Checklist Items (Placeholders)
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000024"), ChecklistTemplateId = moveInTemplateId, ItemText = "Document property condition", ItemOrder = 1, CategorySection = "General", SectionOrder = 1, IsRequired = true, RequiresValue = false, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false },
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000025"), ChecklistTemplateId = moveInTemplateId, ItemText = "Collect keys and access codes", ItemOrder = 2, CategorySection = "General", SectionOrder = 1, IsRequired = true, RequiresValue = false, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false },
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000026"), ChecklistTemplateId = moveInTemplateId, ItemText = "Review lease terms with tenant", ItemOrder = 3, CategorySection = "General", SectionOrder = 1, IsRequired = true, RequiresValue = false, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false },
                // Move-Out Checklist Items (Placeholders)
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000027"), ChecklistTemplateId = moveOutTemplateId, ItemText = "Inspect property condition", ItemOrder = 1, CategorySection = "General", SectionOrder = 1, IsRequired = true, RequiresValue = false, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false },
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000028"), ChecklistTemplateId = moveOutTemplateId, ItemText = "Collect all keys and access devices", ItemOrder = 2, CategorySection = "General", SectionOrder = 1, IsRequired = true, RequiresValue = false, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false },
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000029"), ChecklistTemplateId = moveOutTemplateId, ItemText = "Document damages and needed repairs", ItemOrder = 3, CategorySection = "General", SectionOrder = 1, IsRequired = true, RequiresValue = false, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false },
                // Open House Checklist Items (Placeholders)
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000030"), ChecklistTemplateId = openHouseTemplateId, ItemText = "Set up signage and directional markers", ItemOrder = 1, CategorySection = "Preparation", SectionOrder = 1, IsRequired = true, RequiresValue = false, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false },
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000031"), ChecklistTemplateId = openHouseTemplateId, ItemText = "Prepare information packets", ItemOrder = 2, CategorySection = "Preparation", SectionOrder = 1, IsRequired = true, RequiresValue = false, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false },
                new ChecklistTemplateItem { Id = Guid.Parse("00000000-0000-0000-0002-000000000032"), ChecklistTemplateId = openHouseTemplateId, ItemText = "Set up visitor sign-in sheet", ItemOrder = 3, CategorySection = "Preparation", SectionOrder = 1, IsRequired = true, RequiresValue = false, AllowsNotes = true, OrganizationId = Guid.Empty, CreatedOn = systemTimestamp, CreatedBy = string.Empty, IsDeleted = false }
            );

            // Call partial method hook for features to add custom configuration
            OnModelCreatingPartial(modelBuilder);
        }

        /// <summary>
        /// Partial method hook for features to add custom model configuration.
        /// Features can implement this in their partial ApplicationDbContext classes.
        /// </summary>
        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}