# Nine Compatibility Matrix

**Last Updated:** March 1, 2026  
**Current Release: v1.0.0
**Next Release: v1.1.0 (planned)

---

## Overview

This matrix tracks version compatibility across Nine releases, enabling you to:

- ✅ **Verify upgrade/downgrade compatibility** - Check if app versions work with your database
- ✅ **Identify component versions** - Know which dependencies are installed
- ✅ **Plan rollbacks safely** - Understand which versions can coexist
- ✅ **Troubleshoot version mismatches** - Diagnose compatibility issues
- ✅ **Track breaking changes** - See when incompatibilities were introduced

**For detailed release information**, see version-specific Release Notes in `Documentation/vX.X.X/`.

---

## Nine Version History

| Release Date | App Version | Database Schema | .NET SDK | ElectronNET | Bootstrap | QuestPDF  | Migration Required | Breaking Changes | Status             | Download                                                           |
| ------------ | ----------- | --------------- | -------- | ----------- | --------- | --------- | ------------------ | ---------------- | ------------------ | ------------------------------------------------------------------ |
| TBD          | **1.2.0**   | v1.2.0          | 10.0.1   | 23.6.2      | 5.3.3     | 2025.12.1 | Yes (v1.1.0→1.2.0) | TBD              | **In Development** | -                                                                  |
| 2026-03-01   | 1.1.2       | v1.1.0          | 10.0.1   | 23.6.2      | 5.3.3     | 2025.12.1 | No                 | No               | **Current**        | [Release](https://github.com/xnodeoncode/nine/releases/tag/v1.1.2) |
| 2026-02-28   | 1.1.1       | v1.1.0          | 10.0.1   | 23.6.2      | 5.3.3     | 2025.12.1 | No                 | No               | Previous           | [Release](https://github.com/xnodeoncode/nine/releases/tag/v1.1.1) |
| 2026-02-18   | 1.1.0       | v1.1.0          | 10.0.1   | 23.6.2      | 5.3.3     | 2025.12.1 | Yes (v1.0.0→1.1.0) | New tables/cols  | Superseded         | [Release](https://github.com/xnodeoncode/nine/releases/tag/v1.1.0) |
| 2026-01-29   | 1.0.1       | v1.0.0          | 10.0.1   | 23.6.2      | 5.3.3     | 2025.12.1 | No                 | No               | Superseded         | [Release](https://github.com/xnodeoncode/nine/releases/tag/v1.0.1) |
| 2026-01-28   | 1.0.0       | v1.0.0          | 10.0.1   | 23.6.2      | 5.3.3     | 2025.12.1 | No                 | No               | Superseded         | [Release](https://github.com/xnodeoncode/nine/releases/tag/v1.0.0) |

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

| Component            | Current Version | Purpose           | Upgrade Notes                                                    |
| -------------------- | --------------- | ----------------- | ---------------------------------------------------------------- |
| **ElectronNET.API**  | 23.6.2          | Desktop framework | Major version changes may require electron.manifest.json updates |
| **electron-builder** | 26.4.0          | Build/packaging   | Controls AppImage/exe generation                                 |

### Database & Storage

| Component           | Current Version       | Purpose         | Upgrade Notes                                  |
| ------------------- | --------------------- | --------------- | ---------------------------------------------- |
| **SQLite**          | 3.46.0                | Database engine | Via Microsoft.Data.Sqlite                      |
| **EF Core**         | 10.0.1                | ORM             | Breaking changes uncommon in minor versions    |
| **Database Schema** | v1.0.0 (Nine)         | Data structure  | Tracks with app version MAJOR.MINOR milestones |
|                     | v0.0.0 (Professional) | Data structure  | Pre-v1.0.0 rapid iteration phase               |

### UI & Front-end

| Component                 | Current Version | Purpose       | Upgrade Notes                 |
| ------------------------- | --------------- | ------------- | ----------------------------- |
| **Bootstrap**             | 5.3.3           | CSS framework | Generally backward compatible |
| **Bootstrap Icons**       | 1.11.3          | Icon font     | Additive changes only         |
| **Material Design Icons** | 7.4.47          | Icon font     | Additive changes only         |

### Document Generation

| Component    | Current Version | Purpose        | Upgrade Notes                                      |
| ------------ | --------------- | -------------- | -------------------------------------------------- |
| **QuestPDF** | 2025.12.1       | PDF generation | Annual major versions, breaking changes documented |

### External Services (Optional)

| Component    | Current Version | Purpose        | Upgrade Notes                                      |
| ------------ | --------------- | -------------- | -------------------------------------------------- |
| **SendGrid** | 9.29.3          | Email delivery | API key required for email notifications           |
| **Twilio**   | 7.14.0          | SMS delivery   | Account credentials required for SMS notifications |

---

## Database Schema Versioning

**Current Schema:**

- **Nine:** v1.1.0 (current)
- **Professional:** v0.0.0 (pre-release)

### Schema Version Strategy

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

| Change Type                                          | Version | DB File Changes | EF Compatible | Notes                                                                                             |
| ---------------------------------------------------- | ------- | --------------- | ------------- | ------------------------------------------------------------------------------------------------- |
| Add nullable column                                  | PATCH   | No              | ✅ Yes        | Existing rows get NULL automatically                                                              |
| Add new table                                        | PATCH   | No              | ✅ Yes        | No impact on existing data                                                                        |
| Add new index                                        | PATCH   | No              | ✅ Yes        | No impact on existing data                                                                        |
| Remove column (unused/obsolete)                      | MINOR   | Yes             | ✅ Yes        | EF generates `DropColumn`; data lost                                                              |
| Remove table                                         | MINOR   | Yes             | ✅ Yes        | EF generates `DropTable`; data lost                                                               |
| Add non-nullable column with default/backfill        | MINOR   | Yes             | ✅ Yes        | EF adds column + default; migration review required                                               |
| Rename column or table                               | MAJOR   | Yes             | ⚠️ Partial    | EF generates drop+add (data loss); must hand-edit migration to use `RenameColumn` / `RenameTable` |
| Change column type (compatible, e.g. int → bigint)   | MAJOR   | Yes             | ✅ Yes        | EF generates `AlterColumn`; verify data integrity                                                 |
| Change column type (incompatible, e.g. string → int) | MAJOR   | Yes             | ❌ No         | EF cannot convert data; manual migration script needed                                            |
| Restructure relationship or foreign key              | MAJOR   | Yes             | ⚠️ Partial    | May require data migration depending on cardinality                                               |

> **Rollback note:** PATCH releases share the same DB file across all patch versions. Rolling back from v1.0.3 → v1.0.1 leaves any patch-added columns in place. SQLite ignores unknown columns on reads and leaves them NULL on writes — safe but unsupported.

---

## Version Compatibility

### Rollback Safety

| From Version | To Version | Database Compatible  | Safe Rollback | Notes                                 |
| ------------ | ---------- | -------------------- | ------------- | ------------------------------------- |
| v1.1.0       | v1.0.1     | ❌ No (v1.1.0→1.0.0) | ❌ No         | v1.0.1 missing DatabaseSettings table |
| v1.0.1       | v1.0.0     | ✅ Yes (v1.0.0)      | ✅ Yes        | Drop-in replacement                   |
| v1.1.0       | v1.0.0     | ❌ No (v1.1.0→1.0.0) | ❌ No         | v1.0.0 missing DatabaseSettings table |

### Upgrade Compatibility

| From Version | To Version | Migration Type | Breaking Changes | Notes                                            |
| ------------ | ---------- | -------------- | ---------------- | ------------------------------------------------ |
| v1.0.1       | v1.1.0     | Automatic      | Schema v1.1.0    | New DatabaseSettings table, IsSampleData columns |
| v1.0.0       | v1.0.1     | None           | No               | Drop-in replacement                              |
| v0.3.0       | v0.3.1     | Automatic      | Database path    | Same migration as Nine                           |
| v1.x.x       | v2.0.0     | Automatic      | Schema changes   | Future: Major version, backup enforced           |

---

## Breaking Changes Summary

| Version | Breaking Changes       | Impact                  | Migration Strategy      |
| ------- | ---------------------- | ----------------------- | ----------------------- |
| v1.1.0  | Schema v1.1.0 required | Cannot run on v1.0.0 DB | Automatic EF migrations |
| v1.0.1  | None                   | Backward compatible     | Drop-in replacement     |
| v1.0.0  | Org structure          | Pre-release users only  | Manual migration        |

**For detailed migration procedures**, see version-specific Release Notes.

---

## Platform Support

| Platform                | v1.0.0 | v1.0.1 | v1.1.0 | Notes                               |
| ----------------------- | ------ | ------ | ------ | ----------------------------------- |
| **Linux (AppImage)**    | ✅     | ✅     | ✅     | Ubuntu 20.04+, Debian 11+ tested    |
| **Windows 10/11 (x64)** | ✅     | ✅     | ✅     | Portable exe, no installer required |
| **macOS**               | ❌     | ❌     | ❌     | Planned for v1.2.0+                 |

---

## System Requirements

### Minimum

- **OS:** Linux (Ubuntu 20.04+) or Windows 10 (64-bit)
- **CPU:** 2-core, 1.5 GHz
- **RAM:** 2 GB
- **Disk:** 500 MB (application + data)

### Recommended

- **CPU:** 4-core, 2.5 GHz
- **RAM:** 4 GB
- **Disk:** 1 GB
- **Display:** 1920x1080

---

## Database Schema Compatibility

| App Version | Database Schema | Database File | Forward Compatible | Backward Compatible | Notes                           |
| ----------- | --------------- | ------------- | ------------------ | ------------------- | ------------------------------- |
| v1.1.0      | v1.1.0          | app_v1.1.0.db | No                 | No                  | Requires DatabaseSettings table |
| v1.0.1      | v1.0.0          | app_v1.0.0.db | Yes                | Yes                 | Path: ~/.config/Electron/       |
| v1.0.0      | v1.0.0          | app_v1.0.0.db | Yes                | Yes                 | Path: ~/.config/Electron/       |
| v0.3.0      | v0.0.0          | app_v0.0.0.db | No                 | No                  | Pre-release, rapid iteration    |

**Key:**

- **Forward Compatible**: Newer app can open older database
- **Backward Compatible**: Older app can open newer database

---

## Known Limitations

| Limitation             | All Versions           | Reason                       |
| ---------------------- | ---------------------- | ---------------------------- |
| **Maximum Properties** | 9 (Nine)               | Simple Start tier constraint |
| **Maximum Users**      | 3 (1 system + 3 login) | Simplified access control    |
| **Organizations**      | 1                      | Desktop application scope    |
| **File Upload Size**   | 10 MB per file         | Performance management       |
| **SQLite Concurrency** | Single writer          | SQLite WAL mode limitation   |

---

## Third-Party Licenses

| Component     | License Type          | Eligibility Notes                                                  |
| ------------- | --------------------- | ------------------------------------------------------------------ |
| **QuestPDF**  | Community (Free)      | Free for <$1M revenue, individuals, non-profits, FOSS. Honor-based |
| **.NET**      | MIT                   | Open source, commercial use allowed                                |
| **Bootstrap** | MIT                   | Open source, commercial use allowed                                |
| **Electron**  | MIT                   | Open source, commercial use allowed                                |
| **SendGrid**  | Commercial (Optional) | Requires API key and account                                       |
| **Twilio**    | Commercial (Optional) | Requires credentials and account                                   |

**QuestPDF Community License**: Nine (max 9 properties) qualifies as most users will be under $1M annual revenue. Professional edition users must verify eligibility.

---

## Support & Resources

### Detailed Release Information

- **v1.1.0:** [Release Notes](v1.1.0/v1.1.0-Release-Notes.md) - What's new, migration procedures, testing
- **v1.0.1:** [Release Notes](v1.0.1/v1.0.1-Release-Notes.md) - Bug fixes and improvements
- **v1.0.0:** [Release Notes](v1.0.0/v1.0.0-Release-Notes.md) - Initial production release

### Getting Help

- 📧 **Email:** cisguru@outlook.com
- 🐛 **Bug Reports:** [GitHub Issues](https://github.com/xnodeoncode/nine/issues)
- 💡 **Feature Requests:** [GitHub Discussions](https://github.com/xnodeoncode/nine/discussions)
- 📖 **Documentation:** [/Documentation/](https://github.com/xnodeoncode/nine/tree/main/Documentation)
- 🏛️ **Roadmap:** [/Documentation/Roadmap/](https://github.com/xnodeoncode/nine/tree/main/Documentation/Roadmap)

---

## Change Log

| Date       | Change                            | Updated By   |
| ---------- | --------------------------------- | ------------ |
| 2026-02-01 | Refocused as Compatibility Matrix | Release Team |
| 2026-02-01 | Added v1.1.0 compatibility info   | Release Team |
| 2026-01-29 | Added v1.0.1 entry                | Release Team |
| 2026-01-28 | Initial compatibility tracking    | Release Team |

---

**Maintained by:** Nine Development Team  
**Document Version:** 2.0 - Compatibility Matrix
