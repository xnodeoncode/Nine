# Nine - Database Upgrade Strategy

**Version:** 1.1.0-dev  
**Last Updated:** March 12, 2026  
**Audience:** Developers, Contributors

---

## Overview

Nine uses versioned SQLite database files (`app_vX.Y.0.db`) tied to the application's MAJOR.MINOR version. This document describes how the upgrade path works, the known failure mode for version-skipping users, the fix implemented in `app-database-upgrade`, and how to test all upgrade scenarios.

**This fix ships in v1.1.0** alongside the `app_v1.1.0.db` database file. The normal upgrade path (v1.0.0 → v1.1.0) is covered by `PreviousDatabaseFileName`. The version-skip fallback scan is active in every release from v1.1.0 onward — it protects against any skip at any version boundary (v1.0.0 → v1.2.0, v1.1.0 → v1.3.0, v1.2.0 → v2.0.0, etc.).

---

## Database Versioning Policy

| App Version | Database File | Schema Version | Notes                              |
| ----------- | ------------- | -------------- | ---------------------------------- |
| v1.0.0      | app_v1.0.0.db | 1.0.0          | First public release               |
| v1.1.0      | app_v1.1.0.db | 1.1.0          | Additive columns only              |
| v2.0.0      | app_v2.0.0.db | 2.0.0          | Breaking schema changes            |
| v2.0.5      | app_v2.0.0.db | 2.0.0          | PATCH — same DB file               |
| v2.1.0      | app_v2.1.0.db | 2.1.0          | New DB file, EF migrations applied |

**Rules:**

- MAJOR or MINOR version bump → new `DatabaseFileName` (e.g. `app_v2.1.0.db`)
- PATCH version bump → same `DatabaseFileName`, no DB file change
- `PreviousDatabaseFileName` is set by `bump-version.sh` to the previous file name
- A compiled binary is **version-locked**: it only looks for the filename baked into its `appsettings.json`

---

## Upgrade Path: How It Works

### Normal upgrade (v1.0.0 → v1.1.0)

`appsettings.json` in the v1.1.0 binary:

```json
"DatabaseFileName": "app_v1.1.0.db",
"PreviousDatabaseFileName": "app_v1.0.0.db"
```

**Startup sequence (`Program.cs`):**

1. Target `app_v1.1.0.db` — not found
2. `PreviousDatabaseFileName` = `app_v1.0.0.db` — found
3. **Copy** `app_v1.0.0.db` → `app_v1.1.0.db`
4. EF Core migrations apply the delta to `app_v1.1.0.db`
5. App starts with all user data intact ✅

### Version-skip upgrade (any version)

This scenario can occur at any version boundary. The earliest possible real-world instance is a user on v1.0.0 who skips v1.1.0 and installs v1.2.0 directly. The same logic handles v1.1.0 → v1.3.0, v1.2.0 → v2.0.0, v1.0.0 → v10.0.0, or any other gap.

Example: user on v1.0.0 installs v1.2.0 directly.

`appsettings.json` in the v1.2.0 binary:

```json
"DatabaseFileName": "app_v1.2.0.db",
"PreviousDatabaseFileName": "app_v1.1.0.db"
```

**Without the fix (pre-v1.1.0 behaviour):**

1. Target `app_v1.2.0.db` — not found
2. `PreviousDatabaseFileName` = `app_v1.1.0.db` — **not found** (user skipped v1.1.0)
3. Warning logged; no copy performed
4. EF Core creates **blank** `app_v1.2.0.db` — **user data is lost** ❌

**With the fix (shipped in v1.1.0, present in every subsequent release):**

1. Target `app_v1.2.0.db` — not found
2. `PreviousDatabaseFileName` = `app_v1.1.0.db` — not found
3. **Fallback scan**: glob `app_v*.db` in the config directory, parse each filename as `System.Version`, pick the highest version that is **less than** the target
4. Finds `app_v1.0.0.db`, copies it to `app_v1.2.0.db`
5. EF Core migrations apply the **full delta** from v1.0.0 schema to v1.2.0 schema
6. App starts with all user data intact ✅

The same logic handles multi-step skips (e.g. only `app_v1.0.0.db` present when installing v1.4.0) and double-digit versions (v9.x → v10.x) correctly via `System.Version` numeric comparison.

### Fresh install (no previous DB)

1. Target `app_vX.Y.0.db` — not found
2. `PreviousDatabaseFileName` is empty (or not found on disk)
3. Scan finds nothing
4. EF Core creates a new blank database
5. Seed data applied ✅

### Already upgraded (DB file exists)

1. Target `app_vX.Y.0.db` — **found**
2. Entire upgrade block is skipped (guarded by `!File.Exists(dbPath)`)
3. EF checks for pending migrations, applies if any ✅

---

## The Fix: `System.Version` Fallback Scan

**File:** `4-Nine/Program.cs`

The existing `else` branch in the upgrade block (which only logged a warning) is replaced with:

```csharp
else
{
    // Direct predecessor not found — user may have skipped one or more versions.
    // Scan for any app_v*.db file in the same directory and use the highest version
    // that is older than this binary's target. System.Version ensures correct numeric
    // ordering (v10.0 > v9.0, which lexicographic sort would get wrong).
    var dbDir = Path.GetDirectoryName(dbPath)!;
    var targetVersion = Version.TryParse(
        Path.GetFileNameWithoutExtension(dbFileName).Replace("app_v", ""),
        out var tv) ? tv : null;

    var bestCandidate = Directory.Exists(dbDir)
        ? Directory.GetFiles(dbDir, "app_v*.db")
              .Where(f => f != dbPath)
              .Select(f => new
              {
                  Path = f,
                  Version = Version.TryParse(
                      Path.GetFileNameWithoutExtension(f).Replace("app_v", ""),
                      out var v) ? v : null
              })
              .Where(x => x.Version != null && (targetVersion == null || x.Version < targetVersion))
              .OrderByDescending(x => x.Version)
              .Select(x => x.Path)
              .FirstOrDefault()
        : null;

    if (bestCandidate != null)
    {
        app.Logger.LogInformation(
            "Version skip detected: copying {Found} → {NewFile} (expected {Missing} was absent)",
            Path.GetFileName(bestCandidate), dbFileName, previousDbFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        File.Copy(bestCandidate, dbPath);
    }
    else
    {
        app.Logger.LogWarning(
            "Version upgrade: no previous database found at {PreviousPath} and no app_v*.db candidates in {DbDir}. " +
            "A new empty database will be created.",
            previousDbPath, dbDir);
    }
}
```

**Why `System.Version`?**  
Lexicographic string sorting fails at double-digit major versions:  
`"app_v10.0.0.db" < "app_v9.0.0.db"` (string sort — wrong)  
`Version(10,0,0) > Version(9,0,0)` (`System.Version` — correct)

**Why `x.Version < targetVersion`?**  
Guards against accidentally picking a DB file from a _newer_ version than the current binary — which could only exist if the user manually placed it there (unsupported).

---

## Testing Plan

### Prerequisites

All scenarios require a test user data directory. On Linux this is `~/.config/Nine/`. The test steps below create and manipulate files in that directory directly.

> **Important:** Back up any real production database before running these tests.

---

### Scenario 1: Fresh Install

**Setup:** Delete `~/.config/Nine/app_v*.db` if any exist. Set `appsettings.json` to `DatabaseFileName: "app_v1.0.0.db"`, `PreviousDatabaseFileName: ""`.

**Run:** `dotnet run` (or launch AppImage)

**Expected:**

- Log: no upgrade messages
- `~/.config/Nine/app_v1.0.0.db` created
- App starts, seed data present, login works

**Pass criteria:** Fresh DB created, no errors in logs.

---

### Scenario 2: Normal Upgrade (Direct Predecessor)

**Setup:**

1. Run Scenario 1 to create `app_v1.0.0.db` with real data
2. Change `appsettings.json` to `DatabaseFileName: "app_v1.1.0.db"`, `PreviousDatabaseFileName: "app_v1.0.0.db"`
3. (Optional) Add a pending EF migration in the `1.1.0` schema to verify migrations apply

**Run:** `dotnet run`

**Expected:**

- Log: `"Version upgrade detected: copying app_v1.0.0.db → app_v1.1.0.db"`
- `app_v1.1.0.db` created
- All data from `app_v1.0.0.db` present in new file
- EF migrations applied (if any pending)
- App starts normally

**Pass criteria:** Data preserved, no EF errors.

---

### Scenario 3: Version Skip (Missing Predecessor)

Simulates a user on v1.0.0 who skips v1.1.0 and installs v1.2.0 directly. This is the earliest possible real-world version-skip.

**Setup:**

1. Run Scenario 1 to create `app_v1.0.0.db` with real data
2. Do **not** create `app_v1.1.0.db`
3. Change `appsettings.json` to `DatabaseFileName: "app_v1.2.0.db"`, `PreviousDatabaseFileName: "app_v1.1.0.db"`

**Run:** `dotnet run`

**Expected:**

- Log: `"Version skip detected: copying app_v1.0.0.db → app_v1.2.0.db (expected app_v1.1.0.db was absent)"`
- `app_v1.2.0.db` created from `app_v1.0.0.db`
- All data preserved
- EF migrations applied (full delta from v1.0.0 schema to v1.2.0)
- App starts normally

**Pass criteria:** Data preserved despite skipped version.

---

### Scenario 4: Multi-Version Skip (v1.0.0 → v10.0.0)

**Setup:**

1. Create `app_v1.0.0.db` with real data
2. Create a dummy `app_v9.0.0.db` in the config directory (copy of v1.0.0)
3. Change `appsettings.json` to `DatabaseFileName: "app_v10.0.0.db"`, `PreviousDatabaseFileName: "app_v9.9.0.db"` (missing)

**Run:** `dotnet run`

**Expected:**

- Log shows `app_v9.0.0.db` was selected (highest version below v10.0.0)
- `app_v10.0.0.db` created from `app_v9.0.0.db`, NOT `app_v1.0.0.db`
- `System.Version` correctly ranked v9.0.0 > v1.0.0

**Pass criteria:** v10 is handled correctly; v9 preferred over v1.

---

### Scenario 5: Already Upgraded (Idempotency)

**Setup:**

1. Complete Scenario 2 (v1.1.0 DB exists)
2. Restart the app without changing any config

**Run:** `dotnet run`

**Expected:**

- No copy block executing (guarded by `!File.Exists(dbPath)`)
- No upgrade log messages
- App starts normally from existing DB

**Pass criteria:** No duplicate copies, no accidental data overwrite.

---

### Scenario 6: No Previous DB, No Candidates

**Setup:**

1. Empty `~/.config/Nine/` (no DB files at all)
2. Set `appsettings.json` to `DatabaseFileName: "app_v1.2.0.db"`, `PreviousDatabaseFileName: "app_v1.1.0.db"`

**Run:** `dotnet run`

**Expected:**

- Log: warning that no previous database was found
- EF creates a new blank `app_v1.2.0.db`
- App starts, seed data present

**Pass criteria:** Clean degradation to fresh install behavior; no crash.

---

## Merge Plan

This fix ships as part of **v1.1.0**. The branch workflow:

```
phase-0-baseline  (base: Phase 18 + original one-step upgrade block)
    ↓ branch
app-database-upgrade  (this branch: version-skip scan + System.Version fix)
    ↓ test Scenarios 1-6
    ↓ merge back to phase-0-baseline
    ↓ merge to development
    ↓ PR to main
    ↓ bump-version.sh → v1.1.0
        sets DatabaseFileName: "app_v1.1.0.db"
        sets PreviousDatabaseFileName: "app_v1.0.0.db"
```

**Version notes:**

- `PreviousDatabaseFileName` covers the direct one-step upgrade (v1.0.0 → v1.1.0, v1.1.0 → v1.2.0, etc.)
- The fallback scan covers **any skip at any version boundary** — v1.0.0 → v1.2.0, v1.1.0 → v1.3.0, v1.2.0 → v2.0.0, v1.0.0 → v10.0.0, etc.
- This protection is present in every release from v1.1.0 onward; there is no version at which it "becomes" relevant
- Run all 6 scenarios against `~/.config/Nine/` before merging to `main`

```

**Version notes:**

- v1.1.0 `PreviousDatabaseFileName` = `app_v1.0.0.db` — covers the direct v1.0.0 → v1.1.0 upgrade path (no skip possible yet, only one prior version exists)
- The fallback scan becomes the safety net for v1.2.0+ when a user could first skip a version
- Once merged to `development`, run Scenarios 1 and 5 against the actual `~/.config/Nine/` directory (with a real v1.0.0 DB backup) before merging to `main`
```
