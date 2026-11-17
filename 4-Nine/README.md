# Nine. Property Management System

![.NET 10.0](https://img.shields.io/badge/.NET-10.0-blue)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-10.0-blueviolet)
![Blazor Server](https://img.shields.io/badge/Blazor-Server-orange)
![Entity Framework](https://img.shields.io/badge/Entity%20Framework-10.0-green)
![SQLite](https://img.shields.io/badge/Database-SQLite-lightblue)

A comprehensive property management solution built with ASP.NET Core and Blazor Server. Nine streamlines rental property management for property owners and managers with an intuitive interface and robust feature set.

## Overview

Nine. is designed to simplify property management operations through a centralized platform that handles everything from property, tenant, and lease tracking to document storage and rental invoice and expense tracking. Built with modern web technologies including AI (GitHub Copilot and Claude Sonnet), it provides a responsive, secure, and scalable solution for DIY landlords and property managers.

## Key Features

### Property Management

- **Property Portfolio** - Comprehensive property listings with detailed information
- **Property Details** - Address, type, rent, bedrooms, bathrooms, square footage
- **Availability Tracking** - Real-time property availability status
- **Property Analytics** - Dashboard with property performance metrics and reports

### Tenant Management

- **Tenant Profiles** - Complete tenant information management with phone, email, emergency contacts
- **Communication Tools** - Automated in-app notifications on lease status and other events

### Lease Management

- **Lease Creation** - Digital lease agreement generation
- **Lease Tracking** - Active, pending, expired, and terminated lease monitoring
- **Rent Tracking** - Monthly rent amounts and payment schedules
- **Security Deposits** - Security deposit tracking
- **Lease Renewals** - Lease renewals and conversions to month-to-month tenancy

### Financial Management

- **Payment Tracking** - Rent payment capture and generated PDF receipts.
- **Invoice Generation** - Create invoices and generated PDF files for print and email delivery
- **Payment Methods** - Record payments with multiple payment types
- **Financial Reporting** - Revenue and expense reporting, property performance reporting
- **Late Fee Management** - Automatic late fee calculation and invoice application

### Document Management

- **File Storage** - Document generation, upload and storage
- **Document Categories** - Organized by type (leases, receipts, inspections etc.)

### User Management & Security

- **Role-Based Access** - Owner, Administrator and Property Manager roles
- **Authentication** - Secure login with ASP.NET Core Identity
- **User Profiles** - Comprehensive user account management with role management and lockout capabilities
- **Permission Management** - Gated access control via role-based access
- **Data Security** - Encrypted data storage

### Administration Features

- **User Administration** - Create, lockout, and revoke access for user accounts
- **System Configuration** - Application settings and preferences
- **Application Monitoring** - Database health checks at startup and on demand
- **Backup Management** - In-app data backup, recovery, and encryption features
- **Audit Logging** - Record creation and change tracking

---

## Technology Stack

### Backend

- **Backend**: ASP.NET Core
- **UI Framework**: Blazor Server
- **Database**: SQLCipher (SQLite) with Entity Framework Core
- **Authentication**: ASP.NET Core Identity

### Frontend

- **UI Components**: Blazor Server Components
- **Styling**: Bootstrap 5 with custom CSS
- **Icons**: Bootstrap Icons
- **Responsive Design**: Responsive layout compatible with some mobile environments
- **Real-time Updates**: Blazor Server SignalR integration

### Development Tools

- **IDE**: Visual Studio Code with C# extension
- **Database Tools**: Entity Framework Core Tools
- **Version Control**: Git with GitHub integration
- **Package Management**: NuGet, libman
- **Build System**: .NET SDK build system
- **AI Assisted Coding**: GitHub Copilot, Claude Sonnet, GPT

### Distribution Channels

- [GitHub Releases](https://github.com/xnodeoncode/Nine/releases)
- [AppImagePool](https://appimage.github.io/AppImagePool/)

---

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js](https://nodejs.org/) (LTS) and npm, used once to download the Electron binary (see step 2)
- [Git](https://git-scm.com/)
- [Visual Studio Code](https://code.visualstudio.com/) (recommended) or Visual Studio 2022
- [C# Dev Kit Extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) for VS Code
- A graphical desktop session. Nine runs in an Electron window and cannot run headless.
- Development is supported on **Linux x64** and **Windows x64**.

**Optional tools:** [`dotnet-ef`](https://learn.microsoft.com/ef/core/cli/dotnet) (`dotnet tool install --global dotnet-ef`), only needed to change the database schema.

---

## Quick Start

### 1. Clone the Repository

```bash
git clone https://github.com/xnodeoncode/Nine.git
cd Nine
```

### 2. Build the Application

```bash
dotnet build
```

**First build only:** if the build prints `[ElectronRestore] SETUP REQUIRED`, the Electron binary has not been downloaded yet. Run the following once, then build again:

```bash
cd 4-Nine/bin/Debug/net10.0/linux-x64/.electron   # on Windows, use the matching win-x64 output folder
npm install
cd ../../../../../..
dotnet build
```

The exact folder is shown in the warning message. The downloaded Electron binary is cached in `~/.cache/electron` on Linux and `%LOCALAPPDATA%\electron\Cache` (`C:\Users\<username>\AppData\Local\electron\Cache`) on Windows, so later builds reuse it.

### 3. Start the Electron application

```bash
cd 4-Nine
dotnet run
```

The database is created and migrated automatically on first run.

### 4. Access the Application

The Electron splash screen, and then the application window, should load automatically. A new database opens to the home page, where you log in or register an account. The [Quick Start Guide](https://github.com/xnodeoncode/Nine/blob/main/Documentation/Quick-Start-Guide.md) walks through creating your first organization, property and lease.

To start over with a clean database, close Nine and delete the database file (`~/.config/Nine/app_v1.2.0.db` on Linux, `%APPDATA%\Nine\app_v1.2.0.db` on Windows, which is `C:\Users\<username>\AppData\Roaming\Nine`), or use **Reset Database** on the Manage Database page.

### 5. Testing

```bash
dotnet test Nine.sln
```

### Changing the database schema

If you change an entity, you must add a migration, update the database, and regenerate the compiled model. Skipping the last step causes a runtime error. See **Schema Migrations & Model Optimization** in the [Database Management Guide](https://github.com/xnodeoncode/Nine/blob/main/Documentation/Database-Management-Guide.md) for the commands.

## Project Structure

```
Nine/
├── 0-Nine.Core/           # Domain entities and interfaces
├── 1-Nine.Infrastructure/ # Data access and external services
├── 2-Nine.Application/    # Business logic and services
├── 3-Nine.Shared.UI/      # Shared Blazor components
├── 4-Nine/                # Desktop application (Electron host)
└── 6-Tests/               # Unit and integration tests
```

### Architecture Principles

**Layered Architecture:**

Arrows mean "depends on". Every project can also use Core directly.

```
4-Nine (Features, Shared, Electron host)
   └─▶ 3-Nine.Shared.UI
          └─▶ 2-Nine.Application
                 └─▶ 1-Nine.Infrastructure
                        └─▶ 0-Nine.Core
```

**Dependency Rules:**

- <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/check-circle-fill.svg" width="16" height="16" alt=""> **0-Nine.Core** has no dependencies (pure domain entities)
- <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/check-circle-fill.svg" width="16" height="16" alt=""> **1-Nine.Infrastructure** depends on Core (data access)
- <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/check-circle-fill.svg" width="16" height="16" alt=""> **2-Nine.Application** depends on Core and Infrastructure (business logic)
- <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/check-circle-fill.svg" width="16" height="16" alt=""> **3-Nine.Shared.UI** depends on Application and Core (reusable UI components)
- <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/check-circle-fill.svg" width="16" height="16" alt=""> **4-Nine** depends on Shared.UI, Application and Core (features, shared UI and the Electron host)

**Benefits:**

- **Separation of Concerns**: Domain, business logic, data access, and UI clearly separated
- **Testability**: Each layer can be tested independently
- **Maintainability**: Easy to locate and modify specific functionality
- **Scalability**: Simple to add new features as vertical slices
- **Reusability**: Domain and application layers can be shared across projects

## User Roles

The system includes three user roles:

### Owner (first account created, represents business owner)

- Full system access
- User management capabilities
- System configuration
- All property management features

### Administrator (same as owner, serves as a backup/co-owner account)

- Full system access
- User management capabilities
- System configuration
- All property management features

### Property Manager (no access to application and organization management features)

- Property portfolio management
- Tenant management
- Lease administration
- Financial tracking
- Document management

## Application Limits

Nine. is intentionally constrained for an optimal user experience:

| Items             | Limit                          | Reason                                |
| ----------------- | ------------------------------ | ------------------------------------- |
| **Properties**    | Maximum 9                      | Focused workflows — it's our identity |
| **Users**         | Maximum 4 (1 system + 3 login) | Simplified access control             |
| **Organizations** | 1                              | Desktop application scope             |
| **File uploads**  | 10 MB per file                 | Performance management                |

## Contributing

`main` is reserved for releases and is maintained by the project owner. Do not branch from it or open pull requests against it. All contributions go through `development`.

1. **Fork** [xnodeoncode/Nine](https://github.com/xnodeoncode/Nine) on GitHub, clone your fork, and add the original repository as `upstream`:

   ```bash
   git clone https://github.com/<your-account>/Nine.git
   cd Nine
   git remote add upstream https://github.com/xnodeoncode/Nine.git
   ```

2. **Start from an up-to-date `development`** and create a feature branch:

   ```bash
   git checkout development
   git pull upstream development
   git checkout -b feature/your-feature
   ```

3. **Read [copilot-instructions.md](../.github/copilot-instructions.md)** before changing code. It describes the architecture, service patterns, organization isolation, soft delete, constants, dependency-injection registration and the database schema change process.
4. **Make your changes** and add tests for new functionality.
5. **Merge your feature branch into your fork's `development`, then build and test there.** Everything must pass before you submit:

   ```bash
   git checkout development
   git merge feature/your-feature
   dotnet build Nine.sln
   dotnet test Nine.sln
   git push origin development
   ```

6. **Open a pull request** from your fork's `development` to `development` in `xnodeoncode/Nine`.

**Branch flow:**

```
feature/your-feature  ──merge──▶  your fork: development  ──pull request──▶  xnodeoncode/Nine: development
                                  (build and test here)
```

### Development Guidelines

- Follow C# coding conventions
- Use meaningful commit messages
- Update documentation for new features
- Add unit tests for new functionality
- Ensure responsive design compatibility

**Build and run:**

```bash
dotnet build Nine.sln

cd 4-Nine && dotnet watch
```

**Linux note:** `dotnet watch` may fail with `The configured user limit (128) on the number of inotify instances has been reached`. This is the default on some distros. Raise the limit and run it again:

```bash
sudo sysctl fs.inotify.max_user_instances=512
```

This lasts until the next reboot. To make it permanent, add `fs.inotify.max_user_instances=512` to a file in `/etc/sysctl.d/`. See [inotify(7)](https://man7.org/linux/man-pages/man7/inotify.7.html) for what these limits control.

## Versioning

[Semantic Versioning](https://semver.org/):

- **MAJOR** = breaking/schema changes
- **MINOR** = new features
- **PATCH** = bug fixes

**Current version:** 1.2.0 · **Database schema:** 1.2.0

## License

Copyright © 2026 CIS Guru. Licensed under the **MIT License** — see [LICENSE](https://github.com/xnodeoncode/Nine/blob/main/LICENSE) for details.

### Third-Party Licenses

Nine. includes third-party components that are licensed separately. The full list, with the license text for each
component, is in [Documentation/Third-Party-Licensing-Notices.md](https://github.com/xnodeoncode/Nine/blob/main/Documentation/Third-Party-Licensing-Notices.md), and the
license texts are also installed with the application (see the About page, "Third-Party Licenses").

**QuestPDF (PDF generation).** Nine. uses [QuestPDF](https://www.questpdf.com) to generate its PDF documents (leases,
invoices, receipts, inspections, checklists and reports). QuestPDF is not covered by Nine's MIT license and has its own
[license terms](https://www.questpdf.com/license/community.html). In summary, QuestPDF is free for organizations and
individuals with annual gross revenue under USD 1,000,000, for charitable and qualifying academic organizations, and for
open-source projects. Government and public-sector organizations and publicly traded companies require a paid QuestPDF
license.

**You are responsible for confirming that you are eligible to use QuestPDF before installing or operating Nine.** If you
are not eligible, obtain a license from QuestPDF or do not use Nine. This notice is informational and is not legal advice.

## Support

- <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/globe.svg" width="16" height="16" alt=""> **Website:** [nineapp.co](https://nineapp.co/)
- <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/book.svg" width="16" height="16" alt=""> **Documentation:** [project documentation](https://github.com/xnodeoncode/Nine/tree/main/Documentation) and the [release notes](https://github.com/xnodeoncode/Nine/releases)
- <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/bug.svg" width="16" height="16" alt=""> **Bug Reports:** [GitHub Issues](https://github.com/xnodeoncode/Nine/issues)
- <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/lightbulb.svg" width="16" height="16" alt=""> **Feature Requests:** [GitHub Discussions](https://github.com/xnodeoncode/Nine/discussions)
- <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/envelope.svg" width="16" height="16" alt=""> **Other Questions:** [cisguru@outlook.com](mailto:cisguru@outlook.com?subject=Nine%20Support%20-%20README) (private)

## **Start with Nine.** <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/house.svg" width="16" height="16" alt="">

Built with <img src="https://raw.githubusercontent.com/xnodeoncode/Nine/main/Documentation/images/icons/heart-fill.svg" width="16" height="16" alt=""> using ASP.NET Core 10.0, Blazor Server, and AI Tools (GitHub Copilot and Claude Sonnet)
