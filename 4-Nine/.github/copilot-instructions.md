# Nine Property Management System - AI Agent Instructions

## Development Workflow

### Feature Branch Strategy

**CRITICAL: Always develop new features on feature branches, never directly on main**

1. **Create Feature Branch**: Before starting any new phase or major feature:

   ```bash
   git checkout -b Phase-X-Feature-Name
   ```

   - Use descriptive names: `Phase-6-Workflow-Services-and-Automation`
   - Branch from `main` to ensure clean starting point

2. **Development on Feature Branch**:
   - All commits for the feature go to the feature branch
   - Build and test frequently to ensure no breaking changes
   - Keep commits focused and atomic

3. **Merge to Main** (only when complete and error-free):

   ```bash
   # Ensure build succeeds with 0 errors
   dotnet build Nine.sln

   # Switch to main and merge
   git checkout main
   git merge Phase-X-Feature-Name

   # Push to remote
   git push origin main
   ```

4. **Protection**: Main branch should always be production-ready
   - Never commit directly to main during active development
   - Only merge tested, working code
   - If build fails, fix on feature branch before merging

---

## Project Overview

Nine is a multi-tenant property management system built with **ASP.NET Core 10.0 + Blazor Server**. It manages properties, tenants, leases, invoices, payments, documents, inspections, and maintenance requests with role-based access control.

## Architecture Fundamentals

### Multi-Tenant Design

- **Critical**: Every entity has an `OrganizationId` for tenant isolation
- **UserContextService** (`Services/UserContextService.cs`) provides cached access to current user's `OrganizationId`
- All service methods MUST filter by `OrganizationId` - see `PropertyManagementService` for the pattern
- Never hard-code organization filtering - always use `await _userContext.GetOrganizationIdAsync()`

### Service Layer Pattern

- **PropertyManagementService** is the central data access service (not repository pattern)
- Handles authorization, multi-tenant filtering, and business logic in one place
- All CRUD operations go through this service - components never access `ApplicationDbContext` directly
- Service is injected into Blazor components: `@inject PropertyManagementService PropertyService`

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
@inject PropertyManagementService PropertyService
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
- Or: `dotnet run` in `4-Nine/` directory
- Default URLs: Check terminal output for ports
- Default admin: `superadmin@example.local` / `SuperAdmin@123!`

### Build Tasks (VS Code)

- `build` - Debug build (Ctrl+Shift+B)
- `watch` - Hot reload development mode
- `build-release` - Production build
- `publish` - Create deployment package

### Background Services

- **ScheduledTaskService** runs daily/hourly automated tasks
- Registered as hosted service in `Program.cs`
- Add new scheduled tasks to `ScheduledTaskService.cs` with proper scoping

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

1. **DO NOT** access `ApplicationDbContext` directly in components - always use `PropertyManagementService`
2. **DO NOT** forget `OrganizationId` filtering - security breach waiting to happen
3. **DO NOT** hard-code status values - use `ApplicationConstants` classes
4. **DO NOT** hard delete entities - always soft delete (check `SoftDeleteEnabled` setting)
5. **DO NOT** forget to set audit fields (`CreatedBy`, `CreatedOn`) when creating entities
6. **DO NOT** query without `.Include()` for navigation properties you'll need
7. **DO NOT** use `!` null-forgiving operator without null checks - validate properly

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
- `PropertyManagementService.cs` - Service layer patterns
- `UserContextService.cs` - Multi-tenant context access
- `BaseModel.cs` - Audit field structure
- `ApplicationDbContext.cs` - Entity relationships

## When Adding New Features

1. Create model inheriting `BaseModel` with `OrganizationId` property
2. Add DbSet to `ApplicationDbContext` with proper relationships
3. Create SQL migration script in `Data/Scripts/`
4. Add CRUD methods to `PropertyManagementService` with org filtering
5. Create Blazor components following naming/routing conventions
6. Add constants to `ApplicationConstants` if needed
7. Update navigation in `NavMenu.razor` if top-level feature

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
