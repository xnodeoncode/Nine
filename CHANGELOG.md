# Changelog

All notable changes to **Nine.** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

---

## [1.2.0] - 2026-04-08

### Changed

- Default theme set to Obsidian
- Notification Preferences: removed Email, SMS, and Daily Digest cards (channels not applicable to always-offline desktop app)
- About page overhauled with updated content and layout

### Added

- Paging for Outstanding Invoices on dashboard (5 per page)

### Removed

- SendGrid and Twilio packages, DI registrations, and appsettings entries removed from `4-Nine`; shared library implementations retained for Aquiis.Professional
- Registration link hidden after first non-system user account exists

### Fixed

- Change Database Password restricted to Administrator role only; hidden for all other roles
- Decrypted backup export now saves to `Backups/` folder
- Sidebar flex layout fix resolves scrollbar rendering issue
- Disabled Electron auto-opening DevTools on startup
- `IEmailService` / `ISMSService` DI registrations moved to Application layer; resolves startup crash after removing from product `Program.cs`

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

[Unreleased]: https://github.com/xnodeoncode/nine/compare/v1.2.0...HEAD
[1.2.0]: https://github.com/xnodeoncode/nine/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/xnodeoncode/nine/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/xnodeoncode/nine/releases/tag/v1.0.0
