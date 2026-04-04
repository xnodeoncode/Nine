# Database Era Migration Plan

**Feature Branch:** `app-database-upgrade`  
**Target Release:** v1.1.0  
**Author:** CIS Guru  
**Created:** March 25, 2026  
**Status:** In Progress

---

## 1. Problem Statement

Nine v1.1.0 ships with a squashed migration history from its predecessor, Aquiis. The squash replaced the original incremental migration chain (starting with `20260106195859_InitialCreate`) with a clean baseline (`20260128153724_v1_0_0_InitialCreate`). All Aquiis releases (v0.3.0, v1.0.0, v1.1.0) still contain the orginal marker ID in `__EFMigrationsHistory`. When EF Core's `MigrateAsync` runs against such a database, it cannot reconcile the history with the current migration chain and crashes.

Note on version number overlap: both Aquiis and Nine released a `v1.0.0`, but they are different products on different migration chains. An Aquiis `app_v1.0.0.db` is a pre-squash era database. A Nine `app_v1.0.0.db` is a current-era database. The bridge detects by migration ID content, never by filename.

A secondary problem: the pre-squash schema is missing five migrations added between January 28 and March 13, 2026. Without these, the current application code will fail on startup (wrong indexes, missing columns, wrong column names).

---

## 2. Scope

### Feature Scope

The bridge is implemented in `DatabaseService.cs` — pure .NET code with no platform-specific dependencies. It runs identically on all platforms.

| In Scope                                                         | Out of Scope                                                                                                                                                 |
| ---------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| All Aquiis release databases: v0.3.0, v1.0.0, v1.1.0             | Nine databases (current era — bridge is a no-op by design)                                                                                                   |
| All platforms: Linux, Windows, macOS                             | Encrypted (SQLCipher) databases — supported (bridge opens via EF pipeline; `SqlCipherConnectionInterceptor` applies `PRAGMA key` before any bridge SQL runs) |
| Single-SQLite-file deployments (business + identity in one file) | Manual migration tooling                                                                                                                                     |
| Automated bridge on app startup                                  |                                                                                                                                                              |

### Test Coverage (this branch)

All manual test runs in this branch are on **Linux AppImage only**. The bridge will fire on Windows and macOS in exactly the same way — there is no platform-specific code path — but those scenarios have not been exercised yet.

| Platform         | Tested Here | Notes                  |
| ---------------- | ----------- | ---------------------- |
| Linux (AppImage) | ✅          | All tests 1–6          |
| Windows          | ❌          | Future branch / CI run |
| macOS            | ❌          | Future branch / CI run |

---

## 3. Era Definitions

`DetectEraAsync()` in `DatabaseService.cs` returns one of the following `Era` enum values by inspecting the three known marker IDs in `__EFMigrationsHistory`:

| `Era` value      | Constant               | Marker Migration ID                   | Era Range                                   | Description                                        |
| ---------------- | ---------------------- | ------------------------------------- | ------------------------------------------- | -------------------------------------------------- |
| `Fresh`          | —                      | _(no history table, or table empty)_  | New install                                 | No migrations run yet                              |
| `Current`        | `CurrentEraMarker`     | `20260128153724_v1_0_0_InitialCreate` | Nine ≥ v1.0.0                               | Squashed baseline — no bridge needed               |
| `FirstAncestor`  | `FirstAncestorMarker`  | `20260106195859_InitialCreate`        | All Aquiis releases: v0.3.0, v1.0.0, v1.1.0 | Original incremental chain — bridge required       |
| `SecondAncestor` | `SecondAncestorMarker` | _(empty — TODO at next major squash)_ | Not yet defined                             | Placeholder — bridge method not yet populated      |
| `NotSupported`   | —                      | _(history exists, no known marker)_   | > 2 generations behind, or foreign/corrupt  | DB backed up; `SchemaNotSupportedException` thrown |

**Detection logic (`DetectEraAsync`):**

1. No `__EFMigrationsHistory` table, or table is empty → `Era.Fresh`
2. `SecondAncestorMarker` present (and non-empty) → `Era.SecondAncestor`
3. `FirstAncestorMarker` present → `Era.FirstAncestor`
4. `CurrentEraMarker` present → `Era.Current`
5. History rows exist but no known marker found → `Era.NotSupported`

**Support policy:** Nine supports the current era plus two generations back. `Era.NotSupported` triggers a database backup followed by `DatabaseExceptions.SchemaNotSupportedException`, directing the user to the import workflow.

**Updating markers at each major squash:**

- `CurrentEraMarker` → first migration ID of the new chain
- `FirstAncestorMarker` → previous `CurrentEraMarker`
- `SecondAncestorMarker` → previous `FirstAncestorMarker`
- `ApplyFirstAncestorBridgeAsync()` → new bridge SQL
- `ApplySecondAncestorBridgeAsync()` → previous `ApplyFirstAncestorBridgeAsync()` SQL

---

## 4. Pre-Squash Schema State (Aquiis Era)

Schema confirmed by inspecting `~/.config/Nine/Backups/app_v0.3.0.db` (Aquiis v0.3.0, 946,176 bytes, 43 tables). Aquiis v1.0.0 and v1.1.0 databases carry the same four migration IDs and the same five missing migrations — the pre-squash chain was never updated after the squash occurred. Nine began at v1.0.0 with the squashed baseline already in place.

### Migration history (all Aquiis releases: v0.3.0, v1.0.0, v1.1.0)

| MigrationId                           | ProductVersion |
| ------------------------------------- | -------------- |
| `20260106195859_InitialCreate`        | 10.0.1         |
| `20260128153724_v1_0_0_InitialCreate` | 10.0.1         |
| `20260201231400_AddDatabaseSettings`  | 10.0.1         |
| `20260201234216_AddEncryptionSalt`    | 10.0.1         |

### Missing migrations (not yet applied)

| MigrationId                                           | Applied |
| ----------------------------------------------------- | ------- |
| `20260209120000_FixInvoicePaymentUniqueIndexes`       | ❌      |
| `20260212163628_AddIsSampleDataFlag`                  | ❌      |
| `20260212165047_UpdateExistingSampleDataFlag`         | ❌      |
| `20260216205819_ConsolidateOrganizationIdToBaseModel` | ❌      |
| `20260313122831_RenameIsAvailableToIsActive`          | ❌      |

### Key schema differences vs current

| Table                 | Column / Index                   | v0.3.0 state         | Current state                           |
| --------------------- | -------------------------------- | -------------------- | --------------------------------------- |
| `Properties`          | `IsAvailable` column             | Present              | Renamed to `IsActive`                   |
| `Invoices`            | `IX_Invoices_InvoiceNumber`      | Single-column UNIQUE | Dropped                                 |
| `Invoices`            | `IX_Invoice_OrgId_InvoiceNumber` | Missing              | Composite UNIQUE (OrgId, InvoiceNumber) |
| `Invoices`            | `IX_Invoices_OrganizationId`     | Present              | Dropped                                 |
| `Payments`            | `IX_Payments_OrganizationId`     | Present              | Dropped                                 |
| `Payments`            | `IX_Payment_OrgId_PaymentNumber` | Missing              | Composite UNIQUE (OrgId, PaymentNumber) |
| All ~25 entity tables | `IsSampleData` column            | Missing              | `INTEGER NOT NULL DEFAULT 0`            |

---

## 5. Bridge Implementation

### 5.1 Location

```
2-Nine.Application/Services/DatabaseService.cs
  └─ DetectEraAsync()                         (private) — returns Era enum value
  └─ ApplyFirstAncestorBridgeAsync()          (private) — Aquiis → Nine v1.x bridge
  └─ ApplySecondAncestorBridgeAsync()         (private) — placeholder; populate at next squash
  └─ BackupUnsupportedDatabaseAsync()         (private) — copies DB before NotSupported throw
  └─ InitializeAsync()                        (public)  — orchestrates detection, bridging, MigrateAsync

0-Nine.Core/Exceptions/DatabaseExceptions.cs
  └─ DatabaseExceptions.SchemaNotSupportedException   — era outside support window; carries BackupPath
  └─ DatabaseExceptions.SchemaInvalidException        — schema unrecognisable or corrupt
  └─ DatabaseExceptions.MigrationException            — bridge step failure
```

### 5.2 Startup Call Sequence

```
InitializeAsync()
  │
  ├─ CloseConnection() + ClearAllPools()
  │
  ├─ OpenConnectionAsync() (via EF — SqlCipherConnectionInterceptor fires, PRAGMA key applied)
  ├─ DetectEraAsync()                         ← ERA DETECTION
  │    ├─ Queries __EFMigrationsHistory for three marker IDs
  │    └─ Returns Era enum value
  ├─ CloseConnection() + ClearAllPools()
  │
  ├─ switch (era)
  │    ├─ Era.NotSupported
  │    │    ├─ BackupUnsupportedDatabaseAsync()  — copies DB file to Backups/ folder
  │    │    └─ throw SchemaNotSupportedException(backupPath)
  │    │
  │    ├─ Era.SecondAncestor
  │    │    └─ ApplySecondAncestorBridgeAsync()   ← ERA BRIDGE (placeholder)
  │    │         ├─ OpenConnectionAsync() (via EF)
  │    │         ├─ BEGIN TRANSACTION
  │    │         │    └─ (TODO: bridge SQL at next squash)
  │    │         ├─ COMMIT  (or ROLLBACK on error)
  │    │         └─ CloseConnection()
  │    │
  │    ├─ Era.FirstAncestor
  │    │    └─ ApplyFirstAncestorBridgeAsync()    ← ERA BRIDGE
  │    │         ├─ OpenConnectionAsync() (via EF)
  │    │         ├─ BEGIN TRANSACTION
  │    │         │    ├─ Repair migration history (remove previous-era marker, add identity baseline)
  │    │         │    ├─ Fix Invoice/Payment indexes
  │    │         │    ├─ Add IsSampleData column to ~25 tables
  │    │         │    ├─ Tag existing system-seeded rows (CreatedBy = SystemUser GUID)
  │    │         │    ├─ Rename Properties.IsAvailable → IsActive
  │    │         │    └─ INSERT 5 missing migration IDs into __EFMigrationsHistory
  │    │         ├─ COMMIT  (or ROLLBACK on any error — database left intact for retry)
  │    │         └─ CloseConnection()
  │    │
  │    └─ Era.Fresh / Era.Current → no-op
  │
  ├─ ClearAllPools()
  │
  ├─ _identityContext.MigrateAsync()          (0 pending — history already correct)
  │
  ├─ CloseConnection() + ClearAllPools()
  │
  └─ _businessContext.MigrateAsync()          (any migrations added after bridge point)
```

### 5.3 Bridge SQL Steps

**Step 1 — Repair migration history**

```sql
DELETE FROM "__EFMigrationsHistory"
    WHERE "MigrationId" = '20260106195859_InitialCreate';

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260104205913_InitialCreate', '10.0.1');
```

_Rationale:_ The pre-squash marker is removed. The current-era identity InitialCreate is registered so EF sees zero pending identity migrations (the identity tables already exist in the file).

**Step 2 — Fix Invoice/Payment indexes** _(migration: FixInvoicePaymentUniqueIndexes)_

```sql
DROP INDEX IF EXISTS "IX_Invoices_InvoiceNumber";
DROP INDEX IF EXISTS "IX_Invoices_OrganizationId";
DROP INDEX IF EXISTS "IX_Payments_OrganizationId";
CREATE UNIQUE INDEX "IX_Invoice_OrgId_InvoiceNumber"
    ON "Invoices" ("OrganizationId", "InvoiceNumber");
CREATE UNIQUE INDEX "IX_Payment_OrgId_PaymentNumber"
    ON "Payments" ("OrganizationId", "PaymentNumber");
```

_Rationale:_ The v0.3.0 single-column `IX_Invoices_InvoiceNumber` unique constraint is per-database, not per-organisation. In a multi-tenant environment this would prevent two organisations from both using invoice number `INV-001`. The composite indexes enforce uniqueness within an organisation only.

**Step 3 — Add IsSampleData column** _(migration: AddIsSampleDataFlag)_

```sql
ALTER TABLE "<TableName>" ADD COLUMN "IsSampleData" INTEGER NOT NULL DEFAULT 0;
```

Applied to 31 tables: `ApplicationScreenings`, `CalendarEvents`, `CalendarSettings`, `ChecklistItems`, `Checklists`, `ChecklistTemplateItems`, `ChecklistTemplates`, `Documents`, `Inspections`, `Invoices`, `LeaseOffers`, `Leases`, `MaintenanceRequests`, `Notes`, `NotificationPreferences`, `Notifications`, `OrganizationEmailSettings`, `OrganizationSettings`, `OrganizationSMSSettings`, `Payments`, `Properties`, `ProspectiveTenants`, `RentalApplications`, `Repairs`, `SecurityDepositDividends`, `SecurityDepositInvestmentPools`, `SecurityDeposits`, `Tenants`, `Tours`, `UserProfiles`, `WorkflowAuditLogs`.

_Rationale:_ Allows the UI to filter out system seed/demo data from user-entered records. All pre-existing rows default to `0` (real data) — the next step overrides system-seeded rows.

**Step 4 — Tag system-seeded rows** _(migration: UpdateExistingSampleDataFlag)_

```sql
UPDATE "<TableName>" SET "IsSampleData" = 1
    WHERE "CreatedBy" = '00000000-0000-0000-0000-000000000001';
```

Applied to: `Properties`, `Tenants`, `Leases`, `Invoices`, `Payments`.

_Rationale:_ The SystemUser GUID (`00000000-0000-0000-0000-000000000001`) is the seeder identity. Any row it created is demo/sample data.

**Step 5 — ConsolidateOrganizationIdToBaseModel** _(no-op)_

No DDL required. This migration was a code-only refactor (moved `OrganizationId` from child entities into `BaseModel`). The column already existed on all relevant tables.

**Step 6 — Rename IsAvailable → IsActive** _(migration: RenameIsAvailableToIsActive)_

```sql
ALTER TABLE "Properties" RENAME COLUMN "IsAvailable" TO "IsActive";
```

_Rationale:_ `IsAvailable` was renamed to `IsActive` to align with the `Property.IsActive` property name used throughout the codebase and to avoid confusion with the `Property.Status` workflow field (`ApplicationConstants.PropertyStatuses.Available`).

**Step 7 — Record applied migrations**

```sql
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260212163628_AddIsSampleDataFlag',                    '10.0.1');
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260212165047_UpdateExistingSampleDataFlag',           '10.0.1');
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260216205819_ConsolidateOrganizationIdToBaseModel',   '10.0.1');
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260313122831_RenameIsAvailableToIsActive',            '10.0.1');
```

After the bridge completes, `__EFMigrationsHistory` contains all current-era IDs. `MigrateAsync` will find 0 pending migrations across both contexts and proceed without schema changes.

### 5.4 Transaction Safety

The entire bridge executes inside a single `DbConnection.BeginTransaction()`. If any SQL statement fails, `transaction.Rollback()` is called in the `catch` block, leaving the database in exactly its original state. The next application startup will re-detect the pre-squash marker and retry the bridge from scratch.

The bridge never partially updates the database.

---

## 6. Feature Impact

### Modified Files

| File                                                                                  | Change Type                    | Purpose                                                                                                                                                     |
| ------------------------------------------------------------------------------------- | ------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `2-Nine.Application/Services/DatabaseService.cs`                                      | Added methods, modified method | `DetectEraAsync()`, `ApplyFirstAncestorBridgeAsync()`, `ApplySecondAncestorBridgeAsync()`, `BackupUnsupportedDatabaseAsync()` + updated `InitializeAsync()` |
| `0-Nine.Core/Exceptions/DatabaseExceptions.cs`                                        | New file                       | `SchemaNotSupportedException`, `SchemaInvalidException`, `MigrationException`                                                                               |
| `0-Nine.Core/Entities/Property.cs`                                                    | Property rename                | `IsAvailable` → `IsActive`                                                                                                                                  |
| `2-Nine.Application/Services/LeaseService.cs`                                         | Reference update               | `IsAvailable` → `IsActive`                                                                                                                                  |
| `2-Nine.Application/Services/PropertyService.cs`                                      | Reference update               | `IsAvailable` → `IsActive`                                                                                                                                  |
| `2-Nine.Application/Services/PropertyManagementService.cs`                            | Reference update               | `IsAvailable` → `IsActive`                                                                                                                                  |
| `2-Nine.Application/Services/SampleDataWorkflowService.cs`                            | Reference update               | `IsAvailable` → `IsActive`                                                                                                                                  |
| `1-Nine.Infrastructure/Data/Migrations/20260313122831_RenameIsAvailableToIsActive.cs` | New file                       | EF migration for current-era DB files                                                                                                                       |
| `1-Nine.Infrastructure/Data/Migrations/ApplicationDbContextModelSnapshot.cs`          | Updated                        | Reflects `IsActive` column name                                                                                                                             |
| `4-Nine/appsettings.json`                                                             | Config fix                     | Version/SchemaVersion/DatabaseFileName alignment                                                                                                            |

### Deleted Files

| File                                                 | Reason                                                  |
| ---------------------------------------------------- | ------------------------------------------------------- |
| `0-Nine.Core/Entities/DatabaseEraResult.cs`          | Dead code — era concept replaced by inline bridge logic |
| `1-Nine.Infrastructure/Services/DatabaseEraState.cs` | Dead code — same reason                                 |

### Interface Changes

| Interface          | Change                                                            |
| ------------------ | ----------------------------------------------------------------- |
| `IDatabaseService` | Removed `CheckEraAsync()` — no longer part of the public contract |

### Feature Behaviours Changed

| Feature               | Before                                | After                                    |
| --------------------- | ------------------------------------- | ---------------------------------------- |
| Property availability | `Property.IsAvailable` boolean        | `Property.IsActive` boolean              |
| Invoice uniqueness    | Per-database unique invoice numbers   | Per-organisation unique invoice numbers  |
| Payment uniqueness    | No uniqueness constraint              | Per-organisation unique payment numbers  |
| Sample data filtering | Not possible                          | `IsSampleData` flag on all entity tables |
| Pre-squash DB startup | Crash (EF migration history mismatch) | Automatic bridge, then normal startup    |

---

## 7. Testing Plan

### Test Matrix

| #   | Test                                   | Database State                                                                   | Expected Result                                                 |
| --- | -------------------------------------- | -------------------------------------------------------------------------------- | --------------------------------------------------------------- |
| 1   | Pre-squash fast-fail guard             | Any Aquiis DB without bridge code                                                | App refuses to start with clear error ✅ Done                   |
| 2   | Nine v1.0.0 → v1.1.0 upgrade           | Nine `app_v1.0.0.db` present, `app_v1.1.0.db` absent                             | File copied, EF migrations applied, app starts ✅ Done          |
| 3   | Nine v1.1.0 same-version restart       | Nine `app_v1.1.0.db` present, 0 pending migrations                               | App starts immediately, no migration work ✅ Done               |
| 4a  | Aquiis v0.3.0 → Nine v1.1.0 era bridge | Aquiis `app_v0.3.0.db` placed as `app_v1.1.0.db`                                 | Bridge fires, all DDL applied, app fully functional 🔴 Pending  |
| 4b  | Aquiis v1.0.0 → Nine v1.1.0 era bridge | Aquiis `app_v1.0.0.db` picked up by version-skip scan, copied to `app_v1.1.0.db` | Bridge fires, all DDL applied, app fully functional 🔴 Pending  |
| 4c  | Aquiis v1.1.0 → Nine v1.1.0 era bridge | Aquiis `app_v1.1.0.db` already in place (no copy needed)                         | Bridge fires, all DDL applied, app fully functional 🔴 Pending  |
| 5   | Fresh install                          | No `.db` file present                                                            | EF creates blank DB, seed data applied 🔴 Pending               |
| 6   | Same-version restart (post-bridge)     | `app_v1.1.0.db` previously bridged                                               | Bridge guard returns early, 0 migrations, fast start 🔴 Pending |

### Test 4 Setup (Aquiis Era Bridge)

Run once for each Aquiis source database. The verification checklist is identical for all three.

**Test 4a — Aquiis v0.3.0 source:**

```bash
mv ~/.config/Nine/Data/app_v1.1.0.db ~/.config/Nine/Data/app_v1.1.0.db.bak 2>/dev/null || true
cp ~/.config/Nine/Backups/app_v0.3.0.db ~/.config/Nine/Data/app_v1.1.0.db
cd /home/cisguru/Source/Nine/4-Nine && dotnet run --unpacked
```

**Test 4b — Aquiis v1.0.0 source** (version-skip scan path — Aquiis `app_v1.0.0.db`, no `app_v1.1.0.db`):

```bash
mv ~/.config/Nine/Data/app_v1.1.0.db ~/.config/Nine/Data/app_v1.1.0.db.bak 2>/dev/null || true
cp ~/.config/Nine/Backups/app_v1.0.0.db.aquiis ~/.config/Nine/Data/app_v1.0.0.db
cd /home/cisguru/Source/Nine/4-Nine && dotnet run --unpacked
# Program.cs version-skip scan should copy app_v1.0.0.db → app_v1.1.0.db, then bridge fires
```

**Test 4c — Aquiis v1.1.0 source** (file already in place, no copy needed):

```bash
cp ~/.config/Nine/Backups/app_v1.1.0.db.aquiis ~/.config/Nine/Data/app_v1.1.0.db
cd /home/cisguru/Source/Nine/4-Nine && dotnet run --unpacked
# Nine sees app_v1.1.0.db already exists, goes straight to DatabaseService; bridge fires
```

**Verification checklist:**

- [ ] Log line: `First-ancestor era database detected. Applying bridge...`
- [ ] Log line: `Database initialization complete.`
- [ ] App UI loads without errors
- [ ] Properties list shows `IsActive` filter working
- [ ] Invoice and payment records display correctly
- [ ] `IsSampleData` column visible to admin tools (optional)
- [ ] `sqlite3 ~/.config/Nine/Data/app_v1.1.0.db "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId"` — shows 8 current-era IDs, no `20260106195859_InitialCreate`

---

## 8. Remaining Work

| #   | Task                                                   | Status     | Notes                                                                         |
| --- | ------------------------------------------------------ | ---------- | ----------------------------------------------------------------------------- |
| 1   | `DetectEraAsync()` + era switch in `InitializeAsync()` | ✅ Done    | `DatabaseService.cs` — returns `Era` enum; switch drives bridge or backup     |
| 2   | `ApplyFirstAncestorBridgeAsync()`                      | ✅ Done    | Aquiis → Nine v1.x bridge SQL                                                 |
| 2b  | `ApplySecondAncestorBridgeAsync()`                     | ✅ Done    | Placeholder; populate at next major squash                                    |
| 2c  | `BackupUnsupportedDatabaseAsync()`                     | ✅ Done    | Copies DB to Backups/ before `SchemaNotSupportedException` is thrown          |
| 2d  | `DatabaseExceptions.cs`                                | ✅ Done    | `SchemaNotSupportedException`, `SchemaInvalidException`, `MigrationException` |
| 3   | Build verification                                     | 🔴 Pending | `dotnet build Nine.sln` — confirm 0 errors                                    |
| 4   | Run Tests 4–6                                          | 🔴 Pending | See test matrix above                                                         |
| 5   | Commit and merge                                       | 🔴 Pending | `app-database-upgrade` → `phase-0-baseline`                                   |

---

## 9. Future Era Bridges

This implementation establishes the pattern for all future era bridges.

### Terminology

The term "previous" refers to the application version, not the user's data. A user running Nine v1.x has current, active data — it is stored in a schema that predates the current release. The bridge performs a schema transformation only; the data arrives intact on the other side.

### Marker Rotation at Each Major Squash

At each major squash (e.g. v1.x → v2.0.0):

1. The squash produces a new baseline migration ID (e.g. `20270XXX_v2_0_0_InitialCreate`)
2. Rotate the three marker constants in `DatabaseService.cs`:
   - `CurrentEraMarker` → new baseline ID
   - `FirstAncestorMarker` → previous `CurrentEraMarker`
   - `SecondAncestorMarker` → previous `FirstAncestorMarker`
3. Write new `ApplyFirstAncestorBridgeAsync()` SQL for the previous-era → current-era schema transformation
4. Populate `ApplySecondAncestorBridgeAsync()` — see note below
5. `DetectEraAsync()` and the `InitializeAsync()` switch require no structural changes

### Composing the Second-Ancestor Bridge

`ApplySecondAncestorBridgeAsync()` cannot simply copy the previous `ApplyFirstAncestorBridgeAsync()` SQL. A database two generations behind must arrive at the **current** schema, not an intermediate one. The method must contain all schema transformations needed to reach the current era directly:

**Example at v2.0.0:**

| Detected era                     | Method called                      | SQL required                                                                                                         |
| -------------------------------- | ---------------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| `FirstAncestor` (Nine v1.x)      | `ApplyFirstAncestorBridgeAsync()`  | v1.x schema → v2.x schema                                                                                            |
| `SecondAncestor` (Aquiis v0.3.x) | `ApplySecondAncestorBridgeAsync()` | Aquiis schema → v1.x schema transformations **+** v1.x schema → v2.x schema transformations, as a single transaction |

The second-ancestor bridge starts from the previous `ApplyFirstAncestorBridgeAsync()` SQL (Aquiis → v1.x) and appends the new `ApplyFirstAncestorBridgeAsync()` SQL (v1.x → v2.x). Both sets of steps run in one transaction — the data is transformed directly from two generations back to current without stopping at an intermediate state.

### Support Policy

Nine supports the current era plus two generations back.

| Era distance   | `DetectEraAsync` result | Action                                                                                                                |
| -------------- | ----------------------- | --------------------------------------------------------------------------------------------------------------------- |
| 0 (current)    | `Era.Current`           | No-op; `MigrateAsync` handles any pending migrations                                                                  |
| 1 generation   | `Era.FirstAncestor`     | `ApplyFirstAncestorBridgeAsync()` transforms schema to current era                                                    |
| 2 generations  | `Era.SecondAncestor`    | `ApplySecondAncestorBridgeAsync()` transforms schema to current era in a single transaction                           |
| 3+ generations | `Era.NotSupported`      | DB backed up to `Backups/`; `DatabaseExceptions.SchemaNotSupportedException` thrown; user directed to import workflow |

---

## 10. Related Documents

- [Database-Upgrade-Strategy.md](Database-Upgrade-Strategy.md) — Runtime upgrade path (version-skip scan, `PreviousDatabaseFileName` logic)
- [Database-Management-Guide.md](Database-Management-Guide.md) — Operational guidance for database files
- [Compatibility-Matrix.md](Compatibility-Matrix.md) — Supported upgrade paths by version
