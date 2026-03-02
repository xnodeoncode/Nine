# Nine Property Management System - AI Agent Instructions

## Development Workflow

### Feature Branch Strategy

**CRITICAL: Use the feature → development → main workflow. Main branch is protected and only accepts pull requests.**

**Branch Hierarchy:**

```
main (protected, production-ready)
  ↑ Pull Request (after CI passes)
development (integration testing)
  ↑ Direct merge
feature/Phase-X-Feature-Name
```

**Workflow:**

1. **Create Feature Branch from Development**:

   ```bash
   # Ensure you're on development branch
   git checkout development
   git pull origin development

   # Create feature branch
   git checkout -b feature/Phase-X-Feature-Name
   ```

   - Use descriptive names: `feature/Phase-6-Workflow-Services-and-Automation`
   - Always branch from `development`, never from `main`

2. **Development on Feature Branch**:
   - All commits for the feature go to the feature branch
   - Build and test frequently to ensure no breaking changes
   - Keep commits focused and atomic
   - Push feature branch to remote regularly for backup

3. **Merge to Development** (after feature is complete and tested):

   ```bash
   # Ensure build succeeds with 0 errors
   dotnet build Nine.sln

   # Switch to development and merge
   git checkout development
   git pull origin development
   git merge feature/Phase-X-Feature-Name

   # Test the merged code in development
   dotnet build Nine.sln
   dotnet test
   ```

4. **Create Pull Request to Main**:

   ```bash
   # Push development branch to remote
   git push origin development
   ```

   - Create pull request on GitHub: `development` → `main`
   - Wait for CI tests to pass
   - Review and merge PR on GitHub.com

5. **Pull Changes Locally**:

   ```bash
   # Switch to main and pull merged changes
   git checkout main
   git pull origin main

   # Test locally to verify
   dotnet build Nine.sln
   dotnet test
   ```

**Branch Protection Rules:**

- **Main**: Protected, requires pull request, cannot push directly
- **Development**: Integration branch for testing features before PR
- **Feature branches**: Short-lived, deleted after merge to development

---

## Project Overview

Nine is a multi-tenant property management system built with **ASP.NET Core 9.0 + Blazor Server**. It manages properties, tenants, leases, invoices, payments, documents, inspections, and maintenance requests with role-based access control.

## Architecture Fundamentals

### Multi-Tenant Design

- **Critical**: Every entity has an `OrganizationId` for tenant isolation
- **UserContextService** (`Services/UserContextService.cs`) provides cached access to current user's `OrganizationId`
- All service methods MUST filter by `OrganizationId` - this is handled automatically by `BaseService<TEntity>`
- Never hard-code organization filtering - always use `await _userContext.GetActiveOrganizationIdAsync()`

### Service Layer Architecture

Nine uses a **layered service architecture** with entity-specific services inheriting from a base service:

**Service Hierarchy:**

```
BaseService<TEntity>                    (2-Nine.Application/Services/BaseService.cs)
  ├─ LeaseService                       (Entity-specific CRUD + business logic)
  ├─ PropertyService
  ├─ TenantService
  ├─ InvoiceService
  ├─ MaintenanceService
  └─ [Entity]Service

Workflow Services                       (2-Nine.Application/Services/Workflows/)
  ├─ LeaseWorkflowService               (Complex lease lifecycle management)
  ├─ ApplicationWorkflowService         (Rental application processing)
  └─ AccountWorkflowService             (User account workflows)

Legacy:
  └─ PropertyManagementService          (Being phased out - do not extend)
```

**BaseService<TEntity> Pattern:**

All entity services inherit from `BaseService<TEntity>` which provides:

- ✅ **Automatic organization isolation** - All queries filtered by `OrganizationId`
- ✅ **Audit field management** - `CreatedBy`, `CreatedOn`, `LastModifiedBy`, `LastModifiedOn` set automatically
- ✅ **Soft delete support** - Respects `ApplicationSettings.SoftDeleteEnabled`
- ✅ **Validation hooks** - Override `ValidateEntityAsync()` for custom rules
- ✅ **Lifecycle hooks** - `SetCreateDefaultsAsync()`, `AfterCreateAsync()`

**Standard CRUD Methods (inherited from BaseService):**

- `GetByIdAsync(Guid id)` - Retrieves entity with org isolation
- `GetAllAsync()` - Returns all entities for active organization
- `CreateAsync(TEntity entity)` - Creates with automatic tracking fields
- `UpdateAsync(TEntity entity)` - Updates with org verification
- `DeleteAsync(Guid id)` - Soft/hard deletes based on settings

**Entity-Specific Service Pattern:**

```csharp
public class LeaseService : BaseService<Lease>
{
    public LeaseService(
        ApplicationDbContext context,
        ILogger<LeaseService> logger,
        IUserContextService userContext,
        IOptions<ApplicationSettings> settings)
        : base(context, logger, userContext, settings)
    {
    }

    // Override for custom validation
    protected override async Task ValidateEntityAsync(Lease entity)
    {
        // Check for overlapping leases
        // Validate date ranges
        // Business rule validation

        await base.ValidateEntityAsync(entity);
    }

    // Add entity-specific methods
    public async Task<List<Lease>> GetLeasesForPropertyAsync(Guid propertyId)
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();

        return await _dbSet
            .Where(l => l.PropertyId == propertyId && !l.IsDeleted)
            .Include(l => l.Property)
            .Include(l => l.Tenant)
            .Where(l => l.Property.OrganizationId == orgId)
            .ToListAsync();
    }
}
```

**Workflow Services Pattern:**

For complex multi-step business processes, use workflow services:

```csharp
public class LeaseWorkflowService : BaseWorkflowService, IWorkflowState<LeaseStatus>
{
    // State machine for lease transitions
    public bool IsValidTransition(LeaseStatus from, LeaseStatus to)

    // Workflow orchestration methods
    public async Task<WorkflowResult> ActivateLeaseAsync(Guid leaseId)
    public async Task<WorkflowResult> RenewLeaseAsync(Guid leaseId, RenewalData data)
    public async Task<WorkflowResult> TerminateLeaseAsync(Guid leaseId, string reason)
}
```

**When to Use Each:**

- **Entity Service** - CRUD operations, simple queries, validation
- **Workflow Service** - Multi-step processes, state transitions, orchestration
- **PropertyManagementService** - Legacy code only (do not extend)

**Blazor Component Injection:**

```csharp
@inject LeaseService LeaseService
@inject PropertyService PropertyService
@inject TenantService TenantService

// NOT: @inject PropertyManagementService (legacy)
```

**CRITICAL: Tracking Fields Must Be Set at Service Layer**

All tracking fields and organization context MUST be set at the **service layer**, never in UI components:

- **Tracking Fields**: `CreatedBy`, `CreatedOn`, `LastModifiedBy`, `LastModifiedOn`, `OrganizationId`
- **Source of Truth**: Services inject `UserContextService` to get current user and active organization
- **Security**: UI cannot manipulate tracking fields or bypass organization isolation
- **Maintainability**: All tracking logic centralized in services (change once, apply everywhere)
- **Simplicity**: UI components don't need to inject `UserContextService` or pass these values

**Service Method Pattern (CORRECT):**

```csharp
// Service injects UserContextService
private readonly UserContextService _userContext;

// Create method - tracking fields set internally
public async Task<Property> CreatePropertyAsync(Property property)
{
    var userId = await _userContext.GetUserIdAsync();
    var activeOrgId = await _userContext.GetActiveOrganizationIdAsync();

    // Service sets tracking fields - UI never touches these
    property.CreatedBy = userId;
    property.CreatedOn = DateTime.UtcNow;
    property.OrganizationId = activeOrgId;

    _dbContext.Properties.Add(property);
    await _dbContext.SaveChangesAsync();
    return property;
}

// Update method - LastModified tracking + org security check
public async Task<bool> UpdatePropertyAsync(Property property)
{
    var userId = await _userContext.GetUserIdAsync();
    var activeOrgId = await _userContext.GetActiveOrganizationIdAsync();

    // Verify property belongs to active organization (security)
    var existing = await _dbContext.Properties
        .FirstOrDefaultAsync(p => p.Id == property.Id && p.OrganizationId == activeOrgId);

    if (existing == null) return false;

    // Service sets tracking fields
    property.LastModifiedBy = userId;
    property.LastModifiedOn = DateTime.UtcNow;
    property.OrganizationId = activeOrgId; // Prevent org hijacking

    _dbContext.Entry(existing).CurrentValues.SetValues(property);
    await _dbContext.SaveChangesAsync();
    return true;
}

// Query method - automatic active organization filtering
public async Task<List<Property>> GetPropertiesAsync()
{
    var activeOrgId = await _userContext.GetActiveOrganizationIdAsync();

    return await _dbContext.Properties
        .Where(p => p.OrganizationId == activeOrgId && !p.IsDeleted)
        .ToListAsync();
}
```

**UI Pattern (CORRECT):**

```csharp
// UI only passes entity - NO userId, NO organizationId
private async Task CreateProperty()
{
    // Simple, clean, secure
    var created = await PropertyService.CreatePropertyAsync(newProperty);
    Navigation.NavigateTo("/propertymanagement/properties");
}
```

**❌ ANTI-PATTERN (DO NOT DO THIS):**

```csharp
// BAD: UI passes tracking values (insecure, boilerplate, wrong layer)
public async Task<Property> CreatePropertyAsync(Property property, string userId, string organizationId)
{
    property.CreatedBy = userId;
    property.OrganizationId = organizationId;
    // ...
}

// BAD: UI must inject UserContextService (wrong responsibility)
@inject UserContextService UserContext

private async Task CreateProperty()
{
    var userId = await UserContext.GetUserIdAsync();
    var orgId = await UserContext.GetActiveOrganizationIdAsync();
    await PropertyService.CreatePropertyAsync(newProperty, userId, orgId); // WRONG
}
```

**Key Principles:**

1. Services own tracking field logic - UI never sets these values
2. All queries automatically filter by active organization (via UserContextService)
3. Update operations verify entity belongs to active org (security)
4. UI components stay simple - just pass entities, no context plumbing
5. Future refactoring happens in services only (change once, not in 40+ UI files)

### Component Architecture

Nine uses a **three-tier component hierarchy** to enable code reuse across Nine and Professional products while maintaining different complexity levels:

**Component Tiers:**

```
Entity Components (Tier 1)          (3-Nine.Shared.UI/Components/Entities/)
  ├─ Pure presentation components
  ├─ Minimal business logic
  ├─ Maximum reusability
  └─ Examples: LeaseListView, PropertyCard, TenantSearchBox

Feature Components (Tier 2)         (3-Nine.Shared.UI/Features/)
  ├─ Fully-featured implementations
  ├─ Complete CRUD operations
  ├─ Business logic included
  ├─ Service injection
  └─ Examples: LeaseManagement, PropertyManagement, TenantManagement

Product Pages (Tier 3)              (4-Nine/Features/ or 5-Nine.Professional (not included)/Features/)
  ├─ Composition layer
  ├─ Product-specific UX
  ├─ Routing decisions
  └─ Examples: Nine uses Feature components directly, Professional uses custom compositions
```

**When to Use Each Tier:**

- **Entity Component**: Building block UI elements (lists, cards, search boxes)
  - Example: `LeaseListView.razor` displays leases with filters and grouping
  - Usage: `<LeaseListView Leases="@leases" GroupedLeases="@groupedLeases" />`
- **Feature Component**: Complete feature implementation ready to use
  - Example: `LeaseManagement.razor` provides full lease CRUD with navigation
  - Usage: Nine: `<LeaseManagement />` (5 lines), Professional: Custom composition
- **Product Page**: Top-level routing and product-specific UX
  - Example: Nine `/propertymanagement/leases` → Uses LeaseManagement directly
  - Example: Professional `/leases` → Custom layout with LeaseListView + custom panels

**File Naming & Organization:**

```
3-Nine.Shared.UI/
  ├── Components/
  │   ├── Common/          (Cross-cutting: EntityFilterBar, Modal, etc.)
  │   └── Entities/        (Entity-specific: Leases/, Properties/, Tenants/)
  └── Features/
      ├── PropertyManagement/
      ├── LeaseManagement/
      └── TenantManagement/

4-Nine/Features/
  └── PropertyManagement/Index.razor (@page "/propertymanagement/leases")

5-Nine.Professional (not included)/Features/
  └── Leases/Index.razor (@page "/leases")
```

**Shared UI Components:**

The `Components/Common/` folder contains reusable components used across all entities:

- **EntityFilterBar.razor**: Generic filter bar with optional search, status, priority, type filters
  - Generic types: `TStatus`, `TPriority`, `TType` (unconstrained for flexibility)
  - Optional elements: `ShowSearch`, `ShowStatusFilter`, `ShowGroupToggle`, etc.
  - Custom filter slot: `<SecondaryFilter>` RenderFragment for entity-specific filters
  - Pattern: `@if (ShowProperty && data.Any())` for conditional rendering

- **LeaseMetricsCard.razor**: Dashboard metric display with icon, title, value, trend
- **Modal.razor**: Reusable modal dialog with customizable content and actions
- **ConfirmDialog.razor**: Confirmation prompts with Yes/No actions

**Example: Nine vs Professional Usage:**

```csharp
// Nine: Direct feature usage (simple, fast to implement)
@page "/propertymanagement/leases"
<LeaseManagement />

// Professional: Custom composition (flexible, branded UX)
@page "/leases"
<div class="professional-layout">
    <LeaseListView Leases="@leases" OnLeaseSelected="@HandleSelection" />
    <CustomLeaseDetailsPanel SelectedLease="@selectedLease" />
</div>
```

### Soft Delete Pattern

- Entities inherit from `BaseModel` which provides audit fields (`CreatedOn`, `CreatedBy`, `LastModifiedOn`, `LastModifiedBy`, `IsDeleted`)
- **Never hard delete** - always set `IsDeleted = true` when deleting
- Controlled by `ApplicationSettings.SoftDeleteEnabled` configuration
- All queries must filter `.Where(x => !x.IsDeleted)` - this is non-negotiable

### Authentication & Authorization

- ASP.NET Core Identity with custom `ApplicationUser` (adds `OrganizationId`, `FirstName`, `LastName`)
- Three primary roles: `Administrator`, `PropertyManager`, `Tenant` (defined in `ApplicationConstants`)
- Use `@attribute [Authorize(Roles = "Administrator,PropertyManager")]` on Blazor pages
- User context pattern: Inject `UserContextService` instead of repeatedly querying `AuthenticationStateProvider`

## Property & Tenant Lifecycle Workflows

### Property Status Management

Properties follow a status-driven lifecycle (string values from `ApplicationConstants.PropertyStatuses`):

- **Available** - Ready to market and show to prospects
- **ApplicationPending** - One or more applications submitted and under review
- **LeasePending** - Application approved, lease offered, awaiting tenant signature
- **Occupied** - Active lease in place
- **UnderRenovation** - Not marketable, undergoing repairs/upgrades
- **OffMarket** - Temporarily unavailable

**Important:** `Property.Status` is a `string` field (max 50 chars), NOT an enum. Always use `ApplicationConstants.PropertyStatuses.*` constants.

**Status transitions are automatic** based on application/lease workflow events.

### Prospect-to-Tenant Journey

1. **Lead/Inquiry** → ProspectiveTenant created with Status: `Inquiry`
2. **Tour Scheduled** → Tour record created, Status: `TourScheduled`
3. **Tour Completed** → Status: `Toured`, interest level captured
4. **Application Submitted** → RentalApplication created, **Property.Status → ApplicationPending**, Status: `ApplicationSubmitted`
   - **Page:** `/propertymanagement/prospects/{id}/submit-application`
   - Application fee collected (per-application, non-refundable)
   - Application valid for 30 days, auto-expires if not processed
   - Property status automatically changes from Available to ApplicationPending
   - All required fields: current address, landlord info, employment, references
   - Income-to-rent ratio calculated and displayed
5. **Screening** → ApplicationScreening created (background + credit checks), Status: `UnderReview`
   - **Page:** `/propertymanagement/applications/{id}/review` (Initiate Screening button)
   - Background check requested with status tracking
   - Credit check requested with credit score capture
   - Overall screening result: Pending, Passed, Failed, ConditionalPass
6. **Application Approved** → Lease created with Status: `Offered`, **Property.Status → LeasePending**, Status: `ApplicationApproved`
   - **Page:** `/propertymanagement/applications/{id}/review` (Approve button after screening passes)
   - All other pending applications for this property auto-denied
   - Lease offer expires in 30 days if not signed
   - `Lease.OfferedOn` and `Lease.ExpiresOn` (30 days) are set
7. **Lease Signed** → **Tenant created from ProspectiveTenant**, SecurityDeposit collected, **Property.Status → Occupied**, Status: `ConvertedToTenant`
   - **Page:** `/propertymanagement/leases/{id}/accept`
   - `TenantConversionService` handles conversion with validation
   - `Tenant.ProspectiveTenantId` links back to prospect for audit trail
   - `Lease.SignedOn` timestamp recorded for compliance
   - SecurityDeposit must be paid in full upfront
   - Move-in inspection auto-scheduled
8. **Lease Declined** → **Property.Status → Available or ApplicationPending** (if other apps exist), Status: `LeaseDeclined`
   - `Lease.DeclinedOn` timestamp recorded
9. **Application Denied** → Status: `ApplicationDenied`, Property returns to Available if no other pending apps

**Key Services:**

- `TenantConversionService` - Handles ProspectiveTenant → Tenant conversion
  - `ConvertProspectToTenantAsync(prospectId, userId)` - Creates tenant with audit trail
  - Returns existing Tenant if already converted (idempotent operation)
  - `IsProspectAlreadyConvertedAsync()` - Prevents duplicate conversions
  - `GetProspectHistoryForTenantAsync()` - Retrieves full prospect history for compliance

**Key Pages:**

- `GenerateLeaseOffer.razor` - `/propertymanagement/applications/{id}/generate-lease-offer`
  - Generates lease offer from approved application
  - Sets `Lease.OfferedOn` and `Lease.ExpiresOn` (30 days)
  - Updates Property.Status to LeasePending
  - Auto-denies all competing applications for the property
  - Accessible to PropertyManager and Administrator roles only

- `AcceptLease.razor` - `/propertymanagement/leases/{id}/accept`
  - Accepts lease offer with full signature audit trail
  - Captures: timestamp, IP address, user ID, payment method
  - Calls TenantConversionService to create Tenant record
  - Sets `Lease.SignedOn`, updates status to Active
  - Updates Property.Status to Occupied
  - Prevents acceptance of expired offers (checks `Lease.ExpiresOn`)
  - Includes decline workflow (sets `Lease.DeclinedOn`)

**Lease Lifecycle Fields:**

- `OfferedOn` (DateTime?) - When lease offer was generated
- `SignedOn` (DateTime?) - When tenant accepted/signed the lease
- `DeclinedOn` (DateTime?) - When tenant declined the offer
- `ExpiresOn` (DateTime?) - Offer expiration date (30 days from OfferedOn)

**Status Constants:**

- ProspectiveStatuses: `LeaseOffered`, `LeaseDeclined`, `ConvertedToTenant`
- ApplicationStatuses: `LeaseOffered`, `LeaseAccepted`, `LeaseDeclined`
- LeaseStatuses: `Offered`, `Active`, `Declined`, `Terminated`, `Expired`

### Multi-Lease Support

- Tenants can have **multiple active leases simultaneously**
- Same tenant can lease multiple units in same or different buildings
- Each lease has independent security deposit, dividend tracking, and payment schedule

### Security Deposit Investment Model

**Investment Pool Approach:**

- All security deposits pooled into investment account
- Annual earnings distributed as dividends
- Organization takes configurable percentage (default 20%), remainder distributed to tenants
- Dividend = (TenantShare / ActiveLeaseCount) per lease
- **Losses absorbed by organization** - no negative dividends

**Dividend Distribution Rules:**

- **Pro-rated** for tenants who moved in mid-year (e.g., 6 months = 50% dividend)
- Distributed at year-end even if tenant has moved out (sent to forwarding address)
- Tenant chooses: apply as lease credit OR receive as check
- Each active lease gets separate dividend (tenant with 2 leases gets 2 dividends)

**Tracking:**

- `SecurityDepositInvestmentPool` - annual pool performance
- `SecurityDepositDividend` - per-lease dividend with payment method choice
- Full audit trail of investment performance visible in tenant portal

### E-Signature & Audit Trail

- Lease offers require acceptance (checkbox "I Accept" for dev/demo)
- Full signature audit: IP address, timestamp, document version, user agent
- Lease offer expires after 30 days if not signed
- Unsigned leases roll to month-to-month at higher rate

## Code Patterns & Conventions

### Enums & Constants Location

- **Status and type values** stored as string constants in `ApplicationConstants.cs` static classes
- Example: `ApplicationConstants.PropertyStatuses.Available`, `ApplicationConstants.LeaseStatuses.Active`
- **Enums** (PropertyStatus, ProspectStatus, etc.) defined in `ApplicationSettings.cs` for type safety but NOT used in database
- Database fields use `string` type with validation against ApplicationConstants values
- Never hard-code status/type values - always reference ApplicationConstants classes

### Blazor Component Structure

```csharp
@page "/propertymanagement/entities/create"
@using Nine.Components.PropertyManagement.Entities
@attribute [Authorize(Roles = "Administrator,PropertyManager")]
@inject PropertyService PropertyService
@inject UserContextService UserContext
@inject NavigationManager Navigation
@rendermode InteractiveServer

// Component code follows...
```

### Creating Entities

```csharp
// UI component creates entity with business data only
private async Task CreateEntity()
{
    // Service handles CreatedBy, CreatedOn, OrganizationId automatically
    var created = await PropertyService.AddEntityAsync(entity);
    Navigation.NavigateTo("/propertymanagement/entities");
}
```

**Note:** Do NOT set tracking fields in UI. The service layer automatically sets:

- `CreatedBy` - from `UserContextService.GetUserIdAsync()`
- `CreatedOn` - `DateTime.UtcNow`
- `OrganizationId` - from `UserContextService.GetActiveOrganizationIdAsync()`

### Service Method Pattern

```csharp
// Service method automatically handles organization context and tracking
public async Task<List<Entity>> GetEntitiesAsync()
{
    // Get active organization from UserContextService (injected in constructor)
    var activeOrgId = await _userContext.GetActiveOrganizationIdAsync();

    return await _dbContext.Entities
        .Include(e => e.RelatedEntity)
        .Where(e => !e.IsDeleted && e.OrganizationId == activeOrgId)
        .ToListAsync();
}

// Create method sets tracking fields internally
public async Task<Entity> AddEntityAsync(Entity entity)
{
    var userId = await _userContext.GetUserIdAsync();
    var activeOrgId = await _userContext.GetActiveOrganizationIdAsync();

    // Service owns this logic - UI never sets these
    entity.CreatedBy = userId;
    entity.CreatedOn = DateTime.UtcNow;
    entity.OrganizationId = activeOrgId;

    _dbContext.Entities.Add(entity);
    await _dbContext.SaveChangesAsync();
    return entity;
}
```

**Important:** Never expose `organizationId` or `userId` as parameters in service methods. Services get these values from `UserContextService` automatically.

### Constants Usage

- All dropdown values come from `ApplicationConstants` (never hard-code)
- Examples: `ApplicationConstants.LeaseStatuses.Active`, `ApplicationConstants.PropertyTypes.Apartment`
- Status/type classes are nested: `ApplicationConstants.PaymentMethods.AllPaymentMethods`

### Entity Relationships

- Properties have many Leases, Documents, Inspections
- Leases belong to Property and Tenant
- Invoices belong to Lease (get Property/Tenant through navigation)
- Always use `.Include()` to eager-load related entities in services

## Development Workflows

### Database Changes

1. **EF Core Migrations**: Primary approach for schema changes
   - Migrations stored in `Data/Migrations/`
   - Run `dotnet ef migrations add MigrationName --project Nine`
   - Apply with `dotnet ef database update --project Nine`
   - Generate SQL script: `dotnet ef migrations script --output schema.sql`
2. **SQL Scripts**: Reference scripts in `Data/Scripts/` (not executed, for documentation)
3. Update `ApplicationDbContext.cs` with DbSet and entity configuration
4. Connection string in `appsettings.json`: `"DefaultConnection": "DataSource=Infrastructure/Data/app.db;Cache=Shared"`
5. **Database**: SQLite (not SQL Server) - scripts will be SQLite syntax

### Development Workflows

**Running the Application:**

- **Ctrl+Shift+B** to run `dotnet watch` (hot reload, default build task)
- **F5** in VS Code to debug (configured in `.vscode/launch.json`)
- Or: `dotnet run` in `Nine/` directory
- Default URLs: Check terminal output for ports
- Default admin: `superadmin@example.local` / `SuperAdmin@123!`

### Build Tasks (VS Code)

- `build` - Debug build (Ctrl+Shift+B)
- `watch` - Hot reload development mode
- `build-release` - Production build
- `publish` - Create deployment package

### Background Services

- **ScheduledTaskService** runs daily/hourly automated tasks
  - Daily tasks: Late fee application, inspection scheduling, lease expiration notifications
  - Hourly tasks: Data cleanup, cache refresh
- Registered as hosted service in `Program.cs`
- Add new scheduled tasks to `ScheduledTaskService.cs` with proper scoping
- Pattern: Create scoped service instances to access DbContext (avoid singleton issues)

## PDF Generation

- Uses **QuestPDF 2025.7.4** with Community License (configured in Program.cs)
- PDF generators in `Components/PropertyManagement/Documents/` (e.g., `LeasePdfGenerator.cs`)
- Always save generated PDFs to `Documents` table with proper associations
- Pattern: Generate → Save to DB → Return Document object → Navigate to view

## File Naming & Organization

### Component Structure

```
Components/PropertyManagement/[Entity]/
  ├── [Entity].cs (Model - inherits BaseModel)
  ├── Pages/
  │   ├── [Entities].razor (List view)
  │   ├── Create[Entity].razor
  │   ├── View[Entity].razor
  │   └── Edit[Entity].razor
  └── [Entity]PdfGenerator.cs (if applicable)
```

### Route Patterns

- List: `/propertymanagement/entities`
- Create: `/propertymanagement/entities/create`
- View: `/propertymanagement/entities/view/{id:int}`
- Edit: `/propertymanagement/entities/edit/{id:int}`

## Common Pitfalls to Avoid

1. **DO NOT** access `ApplicationDbContext` directly in components - always use entity-specific services
2. **DO NOT** extend or add methods to `PropertyManagementService` - it's legacy code being phased out
3. **DO NOT** forget `OrganizationId` filtering - security breach waiting to happen
4. **DO NOT** hard-code status values - use `ApplicationConstants` classes
5. **DO NOT** hard delete entities - always soft delete (check `SoftDeleteEnabled` setting)
6. **DO NOT** set tracking fields (`CreatedBy`, `CreatedOn`, `LastModifiedBy`, `LastModifiedOn`) in UI - services handle this automatically
7. **DO NOT** query without `.Include()` for navigation properties you'll need
8. **DO NOT** use `!` null-forgiving operator without null checks - validate properly
9. **DO NOT** create services without inheriting from `BaseService<TEntity>` - you'll lose org isolation and audit tracking
10. **DO NOT** forget to register new services in `DependencyInjection.cs`

## Toast Notifications

- Use `ToastService` (singleton) for user feedback instead of JavaScript alerts
- Pattern: `await JSRuntime.InvokeVoidAsync("toastService.showSuccess", "Message")`
- Types: Success, Error, Warning, Info
- Auto-dismiss after 5 seconds (configurable)

## Document Management

- Binary storage in `Documents.FileData` (VARBINARY(MAX))
- View in browser: Use Blob URLs via `wwwroot/js/fileDownload.js`
- Download: Base64 encode and trigger download
- 10MB upload limit configured in components

## Financial Features

- Late fees auto-applied by `ScheduledTaskService` (daily at 2 AM)
- Payment tracking updates invoice status automatically
- Financial reports use `FinancialReportService` with PDF export
- Decimal precision: 18,2 for all monetary values

## Inspection Tracking

- 26-item checklist organized in 5 categories (Exterior, Interior, Kitchen, Bathroom, Systems)
- Routine inspections update `Property.NextRoutineInspectionDueDate`
- Generate PDFs with `InspectionPdfGenerator` and save to Documents

## Key Files to Reference

- `Program.cs` - Service registration, Identity config, startup logic
- `ApplicationConstants.cs` - All dropdown values, roles, statuses
- `BaseService.cs` - Abstract base service with org isolation and audit tracking
- `[Entity]Service.cs` - Entity-specific service patterns (LeaseService, PropertyService, etc.)
- `[Entity]WorkflowService.cs` - Workflow orchestration patterns for complex state machines
- `UserContextService.cs` - Multi-tenant context access
- `BaseModel.cs` - Audit field structure
- `ApplicationDbContext.cs` - Entity relationships
- `EntityFilterBar.razor` - Generic reusable filter component pattern
- `DependencyInjection.cs` - Service registration in Application layer

## When Adding New Features

1. **Create entity model** inheriting `BaseModel` with `OrganizationId` property
2. **Add DbSet** to `ApplicationDbContext` with proper relationships and configuration
3. **Create EF Core migration**: `dotnet ef migrations add [Name] --project Nine`
4. **Create entity-specific service** inheriting `BaseService<TEntity>`:
   - Add constructor with dependencies (DbContext, Logger, UserContext, Settings)
   - Override `ValidateEntityAsync()` for business rules
   - Add entity-specific query methods with org filtering
   - Register service in DI container (`2-Nine.Application/DependencyInjection.cs`)
5. **Create workflow service** (if complex state transitions needed):
   - Inherit from `BaseWorkflowService`
   - Implement `IWorkflowState<TStatus>` if status machine required
   - Add orchestration methods for multi-step processes
   - Register in DI container
6. **Create Blazor components** following three-tier architecture:
   - Entity component (if reusable primitive needed)
   - Feature component (complete CRUD implementation)
   - Product pages (composition and routing)
7. **Add constants** to `ApplicationConstants` for status/type values
8. **Update navigation** in `NavMenu.razor` if top-level feature
9. **Add scheduled tasks** to `ScheduledTaskService` if automation needed

**Example Entity Service Creation:**

```csharp
// 2-Nine.Application/Services/[Entity]Service.cs
public class DocumentService : BaseService<Document>
{
    public DocumentService(
        ApplicationDbContext context,
        ILogger<DocumentService> logger,
        IUserContextService userContext,
        IOptions<ApplicationSettings> settings)
        : base(context, logger, userContext, settings)
    {
    }

    protected override async Task ValidateEntityAsync(Document entity)
    {
        if (entity.FileData == null || entity.FileData.Length == 0)
            throw new ValidationException("Document must have file data");

        if (entity.FileData.Length > 10 * 1024 * 1024) // 10MB limit
            throw new ValidationException("File size cannot exceed 10MB");

        await base.ValidateEntityAsync(entity);
    }

    public async Task<List<Document>> GetDocumentsForPropertyAsync(Guid propertyId)
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();

        return await _dbSet
            .Where(d => d.PropertyId == propertyId && !d.IsDeleted)
            .Include(d => d.Property)
            .Where(d => d.Property.OrganizationId == orgId)
            .ToListAsync();
    }
}

// Register in DependencyInjection.cs
services.AddScoped<DocumentService>();
```

## Code Style Notes

- Use async/await consistently (no `.Result` or `.Wait()`)
- Prefer explicit typing over `var` for service/entity types
- Use string interpolation for logging: `$"Processing {entityId}"`
- Handle errors with try-catch and user-friendly messages
- Include XML comments on service methods describing purpose

## Documentation & Roadmap Management

### Documentation Structure

The project maintains comprehensive documentation organized by implementation status and version:

**Roadmap Folder** (`/Documentation/Roadmap/`):

- **Purpose:** Implementation planning and feature proposals
- **Status:** Active consideration - may be approved or rejected
- **Workflow:** One file at a time - focus on current implementation
- **File Naming:** Descriptive names (e.g., `00-PROPERTY-TENANT-LIFECYCLE-ROADMAP.md`)
- **Rejection:** Rejected proposals have rejection reason added at the top of the file

**Version Folders** (`/Documentation/vX.X.X/`):

- **Purpose:** Completed implementation notes for each version release
- **Status:** Historical record of what was actually implemented
- **Content:** Feature additions, changes, and implementation details for that specific release
- **File Naming:** Match feature/module names (e.g., `multi-organization-management.md`)

### Semantic Versioning & Database Management

The project follows **Semantic Versioning (MAJOR.MINOR.PATCH)**:

- **MAJOR** version (X.0.0): Breaking changes that trigger database schema updates
- **MINOR** version (0.X.0): Significant UI changes or new features (backward compatible)
- **PATCH** version (0.0.X): Bug fixes, minor updates, safe application updates

**Current Development Status:**

- **Production version:** v0.1.1 (in production)
- **Development version:** v0.2.0 (current work in progress)
- **Next major milestone:** v1.0.0 (when entity refactoring stabilizes)

**Database Version Management:**

The database filename and schema version are tracked separately from the application patch version:

**Configuration in `appsettings.json`:**

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "DataSource=Infrastructure/Data/app_v0.0.0.db;Cache=Shared"
  },
  "DatabaseSettings": {
    "DatabaseFileName": "app_v0.0.0.db",
    "PreviousDatabaseFileName": "",
    "SchemaVersion": "0.0.0"
  }
}
```

**Versioning Rules:**

1. **Database filename** follows pattern: `app_v{MAJOR}.{MINOR}.0.db`
   - Tracks MAJOR and MINOR app versions only (ignores PATCH)
   - Example: App v2.1.25 uses database `app_v2.1.0.db`
   - Current: App v0.2.0 uses database `app_v0.0.0.db` (pre-v1.0.0)

2. **Schema version** (`SchemaVersion` in settings):
   - Matches database filename version
   - Example: `app_v2.1.0.db` has `SchemaVersion: "2.1.0"`
   - Current: `SchemaVersion: "0.0.0"` (active refactoring phase)

3. **Version 1.0.0 milestone**:
   - At v1.0.0, database management becomes more formal
   - Database filename becomes: `app_v1.0.0.db` (from `app_v0.0.0.db`)
   - `SchemaVersion` initializes to `"1.0.0"`
   - Indicates entity models have stabilized

4. **Migration triggers**:
   - MAJOR version bump → Database schema migration required
   - MINOR version bump → Database filename updates (new .db file)
   - PATCH version bump → No database changes (application updates only)

**Example Version Progression:**

| App Version | Database File | Schema Version | Notes                               |
| ----------- | ------------- | -------------- | ----------------------------------- |
| v0.1.1      | app_v0.0.0.db | 0.0.0          | Production (active refactoring)     |
| v0.2.0      | app_v0.0.0.db | 0.0.0          | Development (same schema)           |
| v1.0.0      | app_v1.0.0.db | 1.0.0          | Milestone (entities stabilized)     |
| v1.0.5      | app_v1.0.0.db | 1.0.0          | Patches (no DB change)              |
| v1.1.0      | app_v1.1.0.db | 1.1.0          | Minor (new DB file)                 |
| v1.1.8      | app_v1.1.0.db | 1.1.0          | Patches (same DB)                   |
| v2.0.0      | app_v2.0.0.db | 2.0.0          | Major (breaking changes, migration) |

**Implementation Workflow:**

1. When incrementing MAJOR or MINOR version:
   - Update `DatabaseFileName` in `appsettings.json` to new version
   - Update `SchemaVersion` to match
   - Set `PreviousDatabaseFileName` to old database name (for migration reference)
   - Create EF Core migration if schema changes required

2. When incrementing PATCH version:
   - No changes to database settings
   - Application version increments only

3. Document completed features in `/Documentation/v{MAJOR}.{MINOR}.{PATCH}/`

**Pre-v1.0.0 Strategy:**

- Database remains at `app_v0.0.0.db` until v1.0.0
- Allows rapid iteration and entity refactoring
- Schema migrations managed via EF Core Migrations folder
- At v1.0.0 release, formalize database versioning with `app_v1.0.0.db`
