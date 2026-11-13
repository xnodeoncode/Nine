# Changelog

All notable changes to **Nine.** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

---

## [1.2.0] - 2026-11-11

### Changed

- Default theme set to Obsidian
- Notification Preferences: removed Email, SMS, and Daily Digest cards (channels not applicable to always-offline desktop app)
- About page overhauled with updated content and layout
- Property list replaced Active/Inactive tabs with Available/Occupied/Off-Market market-status tabs mapped to the existing Property.Status lifecycle
- Leases list now defaults to the Active status filter instead of showing all statuses
- Lease.IsActive is now synced from Lease.Status on creation so active-lease queries correctly exclude leases created with a non-active status
- Income Statement, Tax Report (Schedule E), and Property Performance reports now populate expense categories from real `PropertyExpense` data instead of hardcoded `$0` placeholders
- Schedule E Mortgage Expense line now references Form 1098, Box 1 for the tax-deductible interest portion, instead of a vague "verify" instruction

### Added

- Paging for Outstanding Invoices on dashboard (5 per page)
- Archive/delete workflow for Properties, Repairs, Leases, Invoices, Payments, Maintenance, and Tenants, with cascade summaries, a show-archived toggle, and restore support
- Property expense tracking: recurring and one-time expenses with due-date generation, payment history, waive/skip handling, and lease type support
- Repair supporting-document uploads (receipts, warranties, user guides, photos) via a new RepairDocumentsCard component
- Repairs list status filter (All/Pending/Completed), combinable with the existing In-Progress-Only toggle
- Dynamic port selection for the Electron desktop shell to prevent startup port conflicts
- Actual-expense-by-category queries in `FinancialReportService` and `NineFinancialReportService` backing the report pages above

### Removed

- SendGrid and Twilio packages, DI registrations, and appsettings entries removed from `4-Nine`; shared library implementations retained for Aquiis.Professional
- Registration link hidden after first non-system user account exists
- Lease.DocumentId / Document navigation property removed to break a circular FK cycle (Lease → Document → Lease); replaced with the Documents collection
- Orphaned Applications feature pages removed (`Index.razor`, `Edit.razor`, `View.razor`) — unreachable, no linked Prospects UI or NavMenu entry
- Unused `PropertyManagementService` injection removed from Calendar views and DI registration

### Fixed

- Change Database Password restricted to Administrator role only; hidden for all other roles
- Decrypted backup export now saves to `Backups/` folder
- Sidebar flex layout fix resolves scrollbar rendering issue
- Disabled Electron auto-opening DevTools on startup
- `IEmailService` / `ISMSService` DI registrations moved to Application layer; resolves startup crash after removing from product `Program.cs`
- Inspection Edit page rebuilt — missing route parameter caused an exception when editing a saved inspection
- Electron window now resolves its URL from `app.Urls` after Kestrel starts instead of a pre-captured port variable, fixing a blank/offline window on certain startup paths
- RepairService.GetByIdAsync now explicitly includes Property, fixing a blank Property when navigating directly to a repair's URL
- Cascade-delete support added with explicit transactions across LeaseService, TenantService, InvoiceService, MaintenanceService, InspectionService, and PropertyService

---

## [1.1.0] - 2026-04

### Added

- Built-in upgrade path between releases
- Generational database migration support
- Full-coverage data import

---

## [1.0.0] - 2026-03-01

### 🎉 Initial Release

**Nine.** v1.0.0 is the first production release — property management perfected for up to nine properties.

### Added

#### Core Property Management

- Property profiles with status tracking (Available, Occupied, Under Renovation, Off Market)
- Prospect-to-tenant journey: lead capture, tour scheduling, rental applications, screening
- Digital lease creation with offer/acceptance workflow and e-signature audit trail
- Multi-lease support (tenants can hold multiple active leases simultaneously)
- Tenant profiles with full contact information and lease history

#### Financial Management

- Automated rent invoice generation based on lease schedules
- Payment tracking across multiple payment methods
- Automatic late fee application after configurable grace period
- Security deposit investment tracking with annual dividend distribution
- Financial reports with PDF export

#### Maintenance & Inspections

- Maintenance request tracking with vendor assignment and status management
- Comprehensive 26-item inspection checklist (5 categories)
- Scheduled routine inspections and move-in/move-out tracking
- PDF inspection reports saved to document store

#### Notifications & Automation

- In-app, email (SendGrid), and SMS (Twilio) notifications
- Background task service for automated late fees, lease expiration warnings, and scheduling
- Configurable notification preferences per user

#### Database & Security

- SQLite file-based database (no server required)
- Database encryption at rest (SQLCipher AES-256)
- OS keychain integration for password management
- Automatic schema migrations via EF Core
- Manual and scheduled database backups with staged restore
- Content Security Policy (CSP) headers

#### Multi-User & Access Control

- Role-based access control: Administrator, Property Manager, Tenant
- Maximum 3 users (1 system account + 2 login users)
- Multi-tenant design with organization isolation

#### Desktop Application

- Native desktop experience via ElectronNET on Linux and Windows
- Linux AppImage (x86_64) with desktop integration script
- Windows NSIS installer and portable executable
- AppImageHub catalog integration with embedded license metadata

### Technical Details

- **Application Version:** 1.0.0
- **Database Schema:** v1.0.0
- **Assembly Version:** 1.0.0.0
- **Framework:** ASP.NET Core 10.0 + Blazor Server
- **Desktop:** ElectronNET 23.6.2
- **PDF Generation:** QuestPDF

### Platform Support

- **Linux:** AppImage (x86_64) ✅
- **Windows:** NSIS Installer and Portable (x64) ✅

---

[Unreleased]: https://github.com/xnodeoncode/Nine/compare/v1.2.0...HEAD
[1.2.0]: https://github.com/xnodeoncode/Nine/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/xnodeoncode/Nine/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/xnodeoncode/Nine/releases/tag/v1.0.0
