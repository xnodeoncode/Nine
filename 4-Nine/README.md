# Nine - Property Management System

![.NET 10.0](https://img.shields.io/badge/.NET-10.0-blue)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-10.0-blueviolet)
![Blazor Server](https://img.shields.io/badge/Blazor-Server-orange)
![Entity Framework](https://img.shields.io/badge/Entity%20Framework-10.0-green)
![SQLite](https://img.shields.io/badge/Database-SQLite-lightblue)

A comprehensive web-based property management system built with ASP.NET Core 10.0 and Blazor Server. Nine streamlines rental property management for property owners and managers with an intuitive interface and robust feature set.

## Overview

Nine is designed to simplify property management operations through a centralized platform that handles everything from property, tenant, and lease tracking to document storage and rental invoice and expense tracking. Built with modern web technologies including AI (GitHub Copilot and Claude Sonnet), it provides a responsive, secure, and scalable solution for DIY landlords and property managers.

## Key Features

### Property Management

- **Property Portfolio** - Comprehensive property listings with detailed information
- **Property Details** - Address, type, rent, bedrooms, bathrooms, square footage
- **Availability Tracking** - Real-time property availability status
- **Search & Filter** - Property search and filtering capabilities
- **Property Analytics** - Dashboard with property performance metrics and reports

### Tenant Management

- **Tenant Profiles** - Complete tenant information management with phone, email, emergency contacts
- **Tenant History** - Track tenant interactions and lease history
- **Communication Tools** - Automated notifications on lease status and other events

### Lease Management

- **Lease Creation** - Digital lease agreement generation
- **Lease Tracking** - Active, pending, expired, and terminated lease monitoring
- **Rent Tracking** - Monthly rent amounts and payment schedules
- **Security Deposits** - Security deposit tracking
- **Lease Renewals** - Lease renewals and conversions to month-to-month tenancy

### Financial Management

- **Payment Tracking** - Rent payment monitoring and history
- **Invoice Generation** - Create invoices and generated PDF files for print and email delivery
- **Payment Methods** - Record payments with multiple payment types
- **Financial Reporting** - Revenue and expense reporting
- **Late Fee Management** - Automatic late fee calculation

### Document Management

- **File Storage** - Document generation, upload and storage
- **Document Categories** - Organized by type (leases, receipts, inspections etc.)

### User Management & Security

- **Role-Based Access** - Owner, Administrator and Property Manager roles
- **Authentication** - Secure login with ASP.NET Core Identity
- **User Profiles** - Comprehensive user account management with role management and lockout capabilities
- **Permission Management** - Granular access control via role based access
- **Data Security** - Encrypted data storage

### Administration Features

- **User Administration** - Create, lockout, and revoke access for user accounts
- **System Configuration** - Application settings and preferences
- **Application Monitoring** - Database health checks at start-up
- **Backup Management** - Data backup, recovery, and encryption tools
- **Audit Logging** - Record creation and change tracking

## Technology Stack

### Backend

- **Backend**: ASP.NET Core 10.0
- **UI Framework**: Blazor Server
- **Database**: SQLCipher (SQLite) with Entity Framework Core 10.0
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
- **Package Management**: NuGet
- **Build System**: .NET SDK build system
- **AI Assisted Coding**: GitHub Copilot, Claude Sonnet, GPT

### Distribution Channels

- GitHub Releases
- AppImageHub/AppImagePool (Linux)

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Git](https://git-scm.com/)
- [Visual Studio Code](https://code.visualstudio.com/) (recommended) or Visual Studio 2022
- [C# Dev Kit Extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) for VS Code

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

### 3. Run Database Migrations

```bash
cd 4-Nine
dotnet ef database update
```

### 4. Start the Development Server

```bash
dotnet run
```

### 5. Access the Application

- The Electron splash screen, and then application window, should automatically load.

## Project Structure

The application follows Clean Architecture principles with clear separation of concerns:

**Note: the file names and directories may change, but the architecture will remain the same.**

```
Nine/
├── Nine.Core/                         # Domain Layer (no dependencies)
│   ├── Entities/                      # Domain models & business entities
│   │   ├── BaseModel.cs               # Base entity with common properties
│   │   ├── Property.cs                # Property entity
│   │   ├── Tenant.cs                  # Tenant entity
│   │   ├── Lease.cs                   # Lease entity
│   │   ├── SecurityDeposit.cs         # Security deposit entity
│   │   └── ...                        # Other domain entities
│   └── Constants/                     # Application constants
│       ├── ApplicationConstants.cs
│       └── ApplicationSettings.cs
│
├── Nine.Infrastructure/               # Infrastructure Layer
│   ├── Data/                          # Database & persistence
│   │   ├── ApplicationDbContext.cs    # EF Core DbContext
│   │   ├── Migrations/                # EF Core migrations (44 files)
│   │   ├── Scripts/                   # SQL scripts for manual operations
│   │   └── Backups/                   # Database backups
│   └── Services/                      # External service implementations
│
├── Nine.Application/                  # Application Layer (business logic)
│   └── Services/                      # Domain services
│       ├── PropertyManagementService.cs
│       ├── SecurityDepositService.cs
│       ├── TenantConversionService.cs
│       ├── FinancialReportService.cs
│       ├── ChecklistService.cs
│       ├── CalendarEventService.cs
│       ├── NoteService.cs
│       └── PdfGenerators/             # PDF generation services
│           ├── LeasePdfGenerator.cs
│           ├── InvoicePdfGenerator.cs
│           ├── PaymentPdfGenerator.cs
│           └── ...
│
├── Nine.Shared.UI.Library/            # Reusable UI component library that can be used across different UI implementations
│   ├── Data/                          # Database & persistence
│   │   ├── ApplicationDbContext.cs    # EF Core DbContext
│   │   ├── Migrations/                # EF Core migrations (44 files)
│   │   ├── Scripts/                   # SQL scripts for manual operations
│   │   └── Backups/                   # Database backups
│   └── Services/                      # External service implementations
│
├── Nine/                              # Primary web application designed to run within Electron
│   │
│   ├── Assets/                        # Artifacts need to support the Electron build process
│   │   └── ...
│   ├── Utilities/                     # Install, removal, diagnostic and verification scripts (bash and PowerShell)
│   │   └── ...
│   ├── Data/                          # Database context, creation and migration scripts
│   │   └── ...
│   ├── Entities/                      # Local application specific entities
│   │   └── ...
│   ├── Extensions/                    # Application utilities and extensions
│   │   └── ...
│   ├── Features/                      # Presentation Layer (Vertical Slices)
│   │   ├── Administration/            # Admin features
│   │   │   ├── Application/           # Daily report and schema management
│   │   │   ├── Organizations/         # Organization information and settings
│   │   │   ├── Settings/              # Application settings and database management
│   │   │   ├── Users/                 # User management
│   │   │   └── Dashboard.razor
│   │   ├── Calendar/                  # Calendar grid and list views
│   │   ├── DatabaseUnlock/            # Database security features
│   │   ├── Notifications/             # Notification hub and settings
│   │   ├── PropertyManagement/        # Property management features
│   │   │   ├── Checklists/            # Checklist CRUD & management
│   │   │   ├── Checklist Templates/   # Checklist Template CRUD & management
│   │   │   ├── Documents/             # Document management
│   │   │   ├── Inspections/           # Property inspection CRUD and management
│   │   │   ├── Invoices/              # Invoice CRUD & management
│   │   │   ├── Leases/                # Lease CRUD & management
│   │   │   ├── Payments/              # Payment CRUD & management
│   │   │   ├── Properties/            # Property CRUD & management
│   │   │   ├── Repairs/               # Repair CRUD & management
│   │   │   ├── Reports/               # Financial & operational reports
│   │   │   └── SecurityDeposits/      # Security deposit management
│   │   │   └── Tenants/               # Tenant CRUD & management
│   │   └── ...
│   │   │
│   ├── Fonts/                         # Licensed fonts for use in the UI
│   │   │
│   │   └── ...
│   │   │
│   ├── Properties/                    # Publish profiles and configuration files used by Electron builder
│   │   ├── PublishProfiles/
│   │   └── ...
│   │   │
│   ├── Services/                      # UI level services
│   │   └── ...
│   │
│   ├── Shared/                        # Shared UI Layer
│   │   ├── Authorization/             # Organization access control components
│   │   │   └── ...
│   │   │
│   │   ├── Components/                # Reusable UI components
│   │   │   ├── Account/               # Authentication components
│   │   │   │   └── ...
│   │   │   ├── Pages/                 # Shared pages (Home, About, Error)
│   │   │   │   └── ...
│   │   │   ├── Shared/
│   │   │   │   └── ...
│   │   │   ├── NotesTimeline.razor
│   │   │   ├── SessionTimeoutModal.razor
│   │   │   └── ToastContainer.razor
│   │   │   └── ...
│   │   │
│   │   ├── Layout/                    # Layout components
│   │   │   ├── MainLayout.razor
│   │   │   └── NavMenu.razor
│   │   │   └── ...
│   │   │
│   │   └── Services/                  # UI-specific services
│   │   │   ├── ToastService.cs
│   │   │   ├── ThemeService.cs
│   │   │   ├── SessionTimeoutService.cs
│   │   │   ├── UserContextService.cs
│   │   │   └── DocumentService.cs
│   │   │   └── ...
│   │   │
│   │   └── ...
│   │
│   ├── wwwroot/                       # Static files
│   │   ├── assets/                    # Images & static assets
│   │   ├── js/                        # JavaScript files
│   │   └── lib/                       # Client libraries
│   │
│   ├── Program.cs                     # Application entry point
│   ├── appsettings.json               # Configuration
│   └── appsettings.Development.json   # Development config
```

### Architecture Principles

**Clean Architecture Layers:**

```
Features → Application → Core
    ↓
Infrastructure → Core
    ↓
Shared → Core
```

**Dependency Rules:**

- ✅ **Core** has NO dependencies (pure domain entities)
- ✅ **Infrastructure** depends only on Core (data access)
- ✅ **Application** depends only on Core (business logic)
- ✅ **Features** depends on Application + Core (UI features)
- ✅ **Shared** depends on Core (cross-cutting UI)

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

### Administrator (same as owner, serves as a back-up/co-owner account)

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

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Development Guidelines

- Follow C# coding conventions
- Use meaningful commit messages
- Update documentation for new features
- Add unit tests for new functionality
- Ensure responsive design compatibility

## License

This project is licensed under the MIT License - see the [LICENSE](https://github.com/xnodeoncode/Nine/blob/main/LICENSE) file for details.

## Support

### Documentation

- Check the [Release Notes](https://github.com/xnodeoncode/Nine/releases) for the specific release for recent changes
- Review component-specific README files in subdirectories
- Refer to ASP.NET Core and Blazor documentation

### Getting Help

- Create an [issue on GitHub](https://github.com/xnodeoncode/Nine/issues) for bugs
- Check existing issues for known problems
- Review the [project documentation](https://github.com/xnodeoncode/Nine/tree/main/Documentation)
- Contact the [development team](mailto:cisguru@outlook.com?subject=Nine%20Support%20-%20README)

---

**Nine**

Built with ❤️ using ASP.NET Core 10.0, Blazor Server, and AI Tools (GitHub Copilot and Claude Sonnet)
