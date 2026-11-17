# Nine. Compatibility Matrix

**Last Updated:** November 11, 2026  
**Current Release:** v1.2.0  
**Next Release:** v1.2.1 (in development)

---

## Overview

This matrix tracks version compatibility across Nine releases, enabling you to:

- <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/check-circle-fill.svg" width="16" height="16" alt=""> **Verify upgrade/downgrade compatibility** - Check if app versions work with your database
- <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/check-circle-fill.svg" width="16" height="16" alt=""> **Identify component versions** - Know which dependencies are installed
- <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/check-circle-fill.svg" width="16" height="16" alt=""> **Plan rollbacks safely** - Understand which versions can coexist
- <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/check-circle-fill.svg" width="16" height="16" alt=""> **Troubleshoot version mismatches** - Diagnose compatibility issues
- <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/check-circle-fill.svg" width="16" height="16" alt=""> **Track breaking changes** - See when incompatibilities were introduced

**For detailed release information**, see version-specific [Release Notes](#support--resources).

---

## Version History

| Release Date | Product | App Version | Database Schema | .NET SDK | ElectronNET | Bootstrap | QuestPDF  | Migration Required   | Breaking Changes | Status        | Download                                                               |
| ------------ | ------- | ----------- | --------------- | -------- | ----------- | --------- | --------- | -------------------- | ---------------- | ------------- | ---------------------------------------------------------------------- |
| 2026-11-11   | Nine    | **1.2.0**   | v1.2.0          | 10.0.1   | Core 0.4.1  | 5.3.3     | 2025.12.1 | Yes (v1.1.0→1.2.0)   | New tables/cols  | **Current**   | [Release](https://github.com/xnodeoncode/Nine/releases/tag/v1.2.0)     |
| 2026-04-04   | Nine    | 1.1.0       | v1.1.0          | 10.0.1   | Core 0.4.1  | 5.3.3     | 2025.12.1 | Auto (EF migrations) | No               | Previous      | [Release](https://github.com/xnodeoncode/Nine/releases/tag/v1.1.0)     |
| 2026-03-17   | Aquiis  | 1.1.3       | v1.1.0          | 10.0.1   | 23.6.2      | 5.3.3     | 2025.12.1 | No                   | No               | Latest Aquiis | [Release](https://github.com/xnodeoncode/Aquiis/releases/tag/v1.1.3)   |
| 2026-03-11   | Nine    | 1.0.0       | v1.0.0          | 10.0.1   | Core 0.4.1  | 5.3.3     | 2025.12.1 | No                   | No               | Superseded    | [Release](https://github.com/xnodeoncode/Nine/releases/tag/v1.0.0)     |
| 2026-03-02   | Aquiis  | 1.1.2       | v1.1.0          | 10.0.1   | 23.6.2      | 5.3.3     | 2025.12.1 | No                   | No               | Superseded    | [Release](https://github.com/xnodeoncode/Aquiis/releases/tag/v1.1.2)   |
| 2026-03-01   | Aquiis  | 1.1.1       | v1.1.0          | 10.0.1   | 23.6.2      | 5.3.3     | 2025.12.1 | No                   | No               | Superseded    | [Release](https://github.com/xnodeoncode/Aquiis/releases/tag/v1.1.1.0) |
| 2026-02-19   | Aquiis  | 1.1.0       | v1.1.0          | 10.0.1   | 23.6.2      | 5.3.3     | 2025.12.1 | Yes (v1.0.0→1.1.0)   | New tables/cols  | Superseded    | [Release](https://github.com/xnodeoncode/Aquiis/releases/tag/v1.1.0)   |
| 2026-01-29   | Aquiis  | 1.0.1       | v1.0.0          | 10.0.1   | 23.6.2      | 5.3.3     | 2025.12.1 | No                   | No               | Superseded    | [Release](https://github.com/xnodeoncode/Aquiis/releases/tag/v1.0.1)   |
| 2026-01-28   | Aquiis  | 1.0.0       | v1.0.0          | 10.0.1   | 23.6.2      | 5.3.3     | 2025.12.1 | No                   | No               | Superseded    | [Release](https://github.com/xnodeoncode/Aquiis/releases/tag/v1.0.0)   |

> **Note:** Nine began as a fork of Aquiis at v1.0.0. Releases listed under the **Product** column as Aquiis were published in the
> [Aquiis repository](https://github.com/xnodeoncode/Aquiis). Check the
> **Database Schema** column when importing data from an Aquiis version into Nine, because the schemas differ between versions.
> Schema versions follow the application's MAJOR.MINOR version (`MAJOR.MINOR.0`), and patch releases never change the schema,
> so app v1.1.x always uses schema v1.1.0.

## Professional Version History

| Release Date | App Version | Database Schema | .NET SDK | ElectronNET | Bootstrap | QuestPDF  | Migration Required | Breaking Changes | Status             | Download |
| ------------ | ----------- | --------------- | -------- | ----------- | --------- | --------- | ------------------ | ---------------- | ------------------ | -------- |
| TBD          | **0.3.1**   | v0.0.0          | 10.0.1   | 23.6.2      | 5.3.3     | 2025.12.1 | Auto (location)    | Database path    | **In Development** | -        |
| 2026-01-15   | 0.3.0       | v0.0.0          | 10.0.1   | 23.6.2      | 5.3.3     | 2025.12.1 | No                 | No               | **Current**        | -        |

## Pre-Release History (Archived)

| Release Date | App Version | Database Schema | .NET SDK | Status      |
| ------------ | ----------- | --------------- | -------- | ----------- |
| 2026-01-05   | 0.2.0       | v0.0.0          | 10.0.0   | Pre-release |
| 2025-12-20   | 0.1.0       | v0.0.0          | 9.0.0    | Alpha       |

---

## Component Details

### Core Framework

| Component         | Current Version | Purpose          | Upgrade Notes                    |
| ----------------- | --------------- | ---------------- | -------------------------------- |
| **.NET SDK**      | 10.0.1          | Runtime platform | Auto-included in Electron builds |
| **ASP.NET Core**  | 10.0.1          | Web framework    | Included with .NET SDK           |
| **Blazor Server** | 10.0.1          | UI framework     | Included with ASP.NET Core       |

### Desktop Integration

| Component               | Current Version | Purpose           | Upgrade Notes                                                                                            |
| ----------------------- | --------------- | ----------------- | -------------------------------------------------------------------------------------------------------- |
| **ElectronNET Core**    | 0.4.1           | Desktop framework | Major version changes may require electron.manifest.json updates                                         |
| ~~**ElectronNET.API**~~ | 23.6.2          | Desktop framework | (deprecated - used in earlier releases) Major version changes may require electron.manifest.json updates |
| **electron-builder**    | 26.4.0          | Build/packaging   | Controls AppImage/exe generation                                                                         |

### Database & Storage

| Component           | Current Version | Purpose         | Upgrade Notes                                  |
| ------------------- | --------------- | --------------- | ---------------------------------------------- |
| **SQLite**          | 3.46.0          | Database engine | Via Microsoft.Data.Sqlite                      |
| **EF Core**         | 10.0.1          | ORM             | Breaking changes uncommon in minor versions    |
| **Database Schema** | v1.2.0 (Nine)   | Data structure  | Tracks with app version MAJOR.MINOR milestones |

### UI & Front-end

| Component                 | Current Version | Purpose       | Upgrade Notes                 |
| ------------------------- | --------------- | ------------- | ----------------------------- |
| **Bootstrap**             | 5.3.8           | CSS framework | Generally backward compatible |
| **Bootstrap Icons**       | 1.13.1          | Icon font     | Additive changes only         |
| **Material Design Icons** | 7.4.47          | Icon font     | Additive changes only         |

### Document Generation

| Component    | Current Version | Purpose        | Upgrade Notes                                      |
| ------------ | --------------- | -------------- | -------------------------------------------------- |
| **QuestPDF** | 2025.12.1       | PDF generation | Annual major versions, breaking changes documented |

---

## Database Schema Versioning

**Current Schema:**

- **Nine:** v1.2.0 (current)
- **Professional:** v0.0.0 (pre-release)

### Schema Version Strategy

- **v1.2.0** (Nine)
  - Added tables for tracking property expenses
  - Added columns to support data archiving
  - Modified/removed columns to support updates to lease management
  - Database filename: `app_v1.2.0.db`

- **v1.1.0** (Nine): Database encryption and sample data features
  - Added DatabaseSettings table for encryption state tracking
  - Added IsSampleData column to all entities (30+ tables)
  - Fixed multi-tenant invoice/payment indexes
  - Database filename: `app_v1.1.0.db`

- **v1.0.0** (Nine): Initial production schema
  - Entity models stabilized for production
  - Schema managed via EF Core Migrations
  - Database filename: `app_v1.0.0.db`

- **v0.0.0** (Professional): Pre-v1.0.0 rapid iteration
  - Schema changes without version increments
  - Allows fast development iterations
  - Database filename: `app_v0.0.0.db`

- **Future versions**:
  - **PATCH** (v1.0.X): Additive-only schema changes — same DB file
  - **MINOR** (v1.X.0): Destructive or non-nullable changes — new DB file
  - **MAJOR** (vX.0.0): Breaking changes requiring manual data migration — new DB file + backup enforced

#### Schema Change Classification

| Change Type                                          | Version | DB File Changes | EF Compatible                                                                                                                                                      | Notes                                                                                             |
| ---------------------------------------------------- | ------- | --------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------- |
| Add nullable column                                  | PATCH   | No              | <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/check-circle-fill.svg" width="16" height="16" alt=""> Yes             | Existing rows get NULL automatically                                                              |
| Add new table                                        | PATCH   | No              | <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/check-circle-fill.svg" width="16" height="16" alt=""> Yes             | No impact on existing data                                                                        |
| Add new index                                        | PATCH   | No              | <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/check-circle-fill.svg" width="16" height="16" alt=""> Yes             | No impact on existing data                                                                        |
| Remove column (unused/obsolete)                      | MINOR   | Yes             | <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/check-circle-fill.svg" width="16" height="16" alt=""> Yes             | EF generates `DropColumn`; data lost                                                              |
| Remove table                                         | MINOR   | Yes             | <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/check-circle-fill.svg" width="16" height="16" alt=""> Yes             | EF generates `DropTable`; data lost                                                               |
| Add non-nullable column with default/backfill        | MINOR   | Yes             | <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/check-circle-fill.svg" width="16" height="16" alt=""> Yes             | EF adds column + default; migration review required                                               |
| Rename column or table                               | MAJOR   | Yes             | <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/exclamation-triangle-fill.svg" width="16" height="16" alt=""> Partial | EF generates drop+add (data loss); must hand-edit migration to use `RenameColumn` / `RenameTable` |
| Change column type (compatible, e.g. int → bigint)   | MAJOR   | Yes             | <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/check-circle-fill.svg" width="16" height="16" alt=""> Yes             | EF generates `AlterColumn`; verify data integrity                                                 |
| Change column type (incompatible, e.g. string → int) | MAJOR   | Yes             | <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/x-circle-fill.svg" width="16" height="16" alt=""> No                  | EF cannot convert data; manual migration script needed                                            |
| Restructure relationship or foreign key              | MAJOR   | Yes             | <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/exclamation-triangle-fill.svg" width="16" height="16" alt=""> Partial | May require data migration depending on cardinality                                               |

> **Rollback note:** PATCH releases share the same DB file across all patch versions. Rolling back from v1.0.3 → v1.0.1 leaves any patch-added columns in place. SQLite ignores unknown columns on reads and leaves them NULL on writes — safe but unsupported.

---

## Version Compatibility

### Rollback Safety

| From Version | To Version | Database Compatible                                                                                                                                              | Safe Rollback                                                                                                                                          | Notes                                 |
| ------------ | ---------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------- |
| v1.1.0       | v1.0.1     | <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/x-circle-fill.svg" width="16" height="16" alt=""> No (v1.1.0→1.0.0) | <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/x-circle-fill.svg" width="16" height="16" alt=""> No      | v1.0.1 missing DatabaseSettings table |
| v1.0.1       | v1.0.0     | <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/check-circle-fill.svg" width="16" height="16" alt=""> Yes (v1.0.0)  | <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/check-circle-fill.svg" width="16" height="16" alt=""> Yes | Drop-in replacement                   |
| v1.1.0       | v1.0.0     | <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/x-circle-fill.svg" width="16" height="16" alt=""> No (v1.1.0→1.0.0) | <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/x-circle-fill.svg" width="16" height="16" alt=""> No      | v1.0.0 missing DatabaseSettings table |

### Upgrade Compatibility

| From Version | To Version | Migration Type | Breaking Changes | Notes                                                |
| ------------ | ---------- | -------------- | ---------------- | ---------------------------------------------------- |
| v1.1.0       | v1.2.0     | Automatic      | Schema v1.2.0    | New tables, new columns, dropped columns and indexes |
| v1.0.1       | v1.1.0     | Automatic      | Schema v1.1.0    | New DatabaseSettings table, IsSampleData columns     |
| v1.0.0       | v1.0.1     | None           | No               | Drop-in replacement                                  |
| v0.3.0       | v0.3.1     | Automatic      | Database path    | Same migration as Nine                               |
| v1.x.x       | v2.0.0     | Automatic      | Schema changes   | Future: Major version, backup enforced               |

---

## Breaking Changes Summary

| Version | Breaking Changes       | Impact                                                | Migration Strategy      |
| ------- | ---------------------- | ----------------------------------------------------- | ----------------------- |
| v1.2.0  | Schema v1.2.0 required | Application version v1.1.0 cannot run on db v1.2.0 DB | Automatic EF migrations |
| v1.1.0  | Schema v1.1.0 required | Application version v1.1.0 cannot run on db v1.0.0 DB | Automatic EF migrations |
| v1.0.1  | None                   | Backward compatible                                   | Drop-in replacement     |
| v1.0.0  | Org structure          | Pre-release users only                                | Manual migration        |

**For detailed migration procedures**, see version-specific Release Notes.

---

## Platform Support

| Platform                | v1.0.0                                                                                                                                                | v1.0.1                                                                                                                                                | v1.1.0                                                                                                                                                | v1.2.0                                                                                                                                                | Notes                                              |
| ----------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------- |
| **Linux (AppImage)**    | <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/check-circle-fill.svg" width="16" height="16" alt="Yes"> | <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/check-circle-fill.svg" width="16" height="16" alt="Yes"> | <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/check-circle-fill.svg" width="16" height="16" alt="Yes"> | <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/check-circle-fill.svg" width="16" height="16" alt="Yes"> | Red Hat Enterprise Linux 10 (RHEL 10) tested       |
| **Windows 10/11 (x64)** | <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/check-circle-fill.svg" width="16" height="16" alt="Yes"> | <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/check-circle-fill.svg" width="16" height="16" alt="Yes"> | <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/check-circle-fill.svg" width="16" height="16" alt="Yes"> | <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/check-circle-fill.svg" width="16" height="16" alt="Yes"> | Portable exe, Windows Installer, Windows 11 tested |
| **macOS**               | <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/x-circle-fill.svg" width="16" height="16" alt="No">      | <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/x-circle-fill.svg" width="16" height="16" alt="No">      | <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/x-circle-fill.svg" width="16" height="16" alt="No">      | <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/check-circle-fill.svg" width="16" height="16" alt="No">  | Currently not supported                            |

---

## System Requirements

### Minimum

- **OS:** Linux (RHEL 10 tested) or Windows 11 (64-bit)
- **CPU:** 4-core, 2.5 GHz
- **RAM:** 8 GB
- **Disk:** 2G MB (application + data)

### Recommended

- **CPU:** 4-core, 2.5 GHz
- **RAM:** 16 GB
- **Disk:** 4 GB
- **Display:** 1920x1080

---

## Database Schema Compatibility

| App Version | Database Schema | Database File | Forward Compatible  | Backward Compatible | Notes                                                             |
| ----------- | --------------- | ------------- | ------------------- | ------------------- | ----------------------------------------------------------------- |
| v1.2.0      | v1.2.0          | app_v1.2.0.db | Yes (auto-migrated) | No                  | Path: Linux - ~/.config/Nine/, Windows - ~\APPDATA\Roaming\Nine\  |
| v1.1.0      | v1.1.0          | app_v1.1.0.db | Yes (auto-migrated) | No                  | Path: Linux - ~/.config/Nine/, Windows - ~\APPDATA\Roaming\Nine\  |
| v1.0.0      | v1.0.0          | app_v1.0.0.db | Yes (auto-migrated) | NO                  | Path: Linux - ~/.config/Nine/, Windows - ~\APPDATA\Roaming\Nine\  |

**Key:**

- **Forward Compatible**: Newer app can open older database
- **Backward Compatible**: Older app can open newer database

---

## Known Limitations (\* Limits enforced by design)

| Limitation                | All Versions           | Reason                       |
| ------------------------- | ---------------------- | ---------------------------- |
| **Maximum Properties** \* | 9 (Nine)               | Simple Start tier constraint |
| **Maximum Users** \*      | 4 (1 system + 3 login) | Simplified access control    |
| **Organizations** \*      | 1                      | Desktop application scope    |
| **File Upload Size** \*   | 10 MB per file         | Performance management       |
| **SQLite Concurrency**    | Single writer          | SQLite WAL mode limitation   |

---

## Third-Party Licenses

| Component     | License Type     | Eligibility Notes                                                  |
| ------------- | ---------------- | ------------------------------------------------------------------ |
| **QuestPDF**  | Community (Free) | Free for <$1M revenue, individuals, non-profits, FOSS. Honor-based |
| **.NET**      | MIT              | Open source, commercial use allowed                                |
| **Bootstrap** | MIT              | Open source, commercial use allowed                                |
| **Electron**  | MIT              | Open source, commercial use allowed                                |

---

## Support & Resources

### Release Notes

- **v1.2.0:** [Release Notes](https://github.com/xnodeoncode/Nine/releases/tag/v1.2.0)
- **v1.1.0:** [Release Notes](https://github.com/xnodeoncode/Nine/releases/tag/v1.1.0)
- **v1.0.0:** [Release Notes](https://github.com/xnodeoncode/Nine/releases/tag/v1.0.0)

### Getting Help

- <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/envelope.svg" width="16" height="16" alt=""> **Email:** [cisguru@outlook.com](mailto:cisguru@outlook.com?subject=Nine%20Support%20-%20Compatibility%20Matrix)
- <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/bug.svg" width="16" height="16" alt=""> **Bug Reports:** [GitHub Issues](https://github.com/xnodeoncode/Nine/issues)
- <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/book.svg" width="16" height="16" alt=""> **Documentation:** [/Documentation/](https://github.com/xnodeoncode/Nine/tree/main/Documentation)

---

## Change Log

| Date       | Change                            | Updated By   |
| ---------- | --------------------------------- | ------------ |
| 2026-11-11 | Updated added v1.2.0 information  | Release Team |
| 2026-02-01 | Refocused as Compatibility Matrix | Release Team |
| 2026-02-01 | Added v1.1.0 compatibility info   | Release Team |
| 2026-01-29 | Added v1.0.1 entry                | Release Team |
| 2026-01-28 | Initial compatibility tracking    | Release Team |

---

**Maintained by:** Nine Development Team  
**Document Version:** 1.2.0 (version aligned with release version) - Compatibility Matrix
