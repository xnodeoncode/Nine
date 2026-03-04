# Nine.

[![Version](https://img.shields.io/badge/version-1.0.0-blue.svg)](https://github.com/xnodeoncode/nine/releases)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Linux%20%7C%20Windows-lightgrey.svg)](#installation)
[![Website](https://img.shields.io/badge/website-nineapp.co-blue.svg)](https://nineapp.co/)

---

**Nine.** is a desktop application for real estate investors, property managers, and landlords managing up to nine residential rental properties.

Built with ASP.NET Core 10 and Blazor Server, wrapped in Electron for a native desktop experience.

**Perfect for:**

- Independent property managers and landlords
- Property owners who self-manage their rentals
- New Real Estate investors building their portfolio
- Anyone seeking focused, no-subscription, property management software

## ✨ Key Features

### Property Management

- 📋 Manage up to 9 residential properties
- 🏡 Property profiles with photos and documents
- 🔍 Track property status (Available, Occupied, Under Renovation)
- 📊 Portfolio overview and analytics

### Tenant Management

- Track tenants and leases

### Lease Management

- 📄 Digital lease creation and management
- 💰 Security deposit tracking

### Financial Management

- 🧾 Generate invoices for active leases
- 💳 Payment tracking by multiple methods
- ⏰ Automatic late fee application after grace period
- 📈 Financial reports and payment history

### Maintenance & Inspections

- 🔧 Capture repair services and costs
- ✅ Comprehensive 26-item inspection checklist
- 📅 Scheduled routine inspections
- 📄 PDF inspection reports

### Notifications & Automation

- 🔔 In-app notifications
- ⏰ Automatic late fees and lease expiration warnings
- 📅 Background tasks for scheduling and cleanup
- 🎯 Configurable notification preferences

### Database & Security

- 💾 SQLite file-based database (no server required)
- 🔒 Database encryption at rest (SQLCipher AES-256)
- 🔑 OS keychain integration for password management
- 🔄 Automatic schema migrations
- 📦 Manual and scheduled backups
- ♻️ Staged restore with preview
- 🔐 Content Security Policy (CSP) headers

---

## 📥 Download

**Latest Release: v1.0.0**

[![Download for Linux](https://img.shields.io/badge/Download-Linux%20AppImage-blue.svg?style=for-the-badge&logo=linux)](https://github.com/xnodeoncode/nine/releases/download/v1.0.0/Nine-1.0.0-x86_64.AppImage)
[![Download for Windows](https://img.shields.io/badge/Download-Windows%20Setup-blue.svg?style=for-the-badge&logo=windows)](https://github.com/xnodeoncode/nine/releases/download/v1.0.0/Nine-1.0.0-x64-Setup.exe)

**All Downloads:** [View v1.0.0 Release](https://github.com/xnodeoncode/nine/releases/tag/v1.0.0)

---

## 🚀 Quick Start

### Installation

#### Linux (AppImage)

```bash
# Download from releases page or use wget
wget https://github.com/xnodeoncode/nine/releases/download/v1.0.0/Nine-1.0.0-x86_64.AppImage

# Make executable
chmod +x Nine-1.0.0-x86_64.AppImage

# Option 1: Desktop integration (recommended)
wget https://github.com/xnodeoncode/nine/releases/download/v1.0.0/install-desktop-integration.sh
chmod +x install-desktop-integration.sh
./install-desktop-integration.sh Nine-1.0.0-x86_64.AppImage

# Option 2: Run directly
./Nine-1.0.0-x86_64.AppImage
```

#### Windows (Installer or Portable)

**Option A: Installer (Recommended)**

1. **Download** `Nine-1.0.0-x64-Setup.exe` from the [releases page](https://github.com/xnodeoncode/nine/releases/tag/v1.0.0)
2. **Run installer** and follow the setup wizard
3. **Launch** from Start Menu or Desktop shortcut

**Option B: Portable Executable**

1. **Download** `Nine-1.0.0-x64-Portable.exe` from the [releases page](https://github.com/xnodeoncode/nine/releases/tag/v1.0.0)
2. **Move to permanent location** (e.g., `C:\Program Files\Nine\`)
   - ⚠️ Database and settings are stored relative to the .exe location
3. **Double-click** to run

**Note:** Windows SmartScreen warning may appear (app is unsigned). Click **"More info"** → **"Run anyway"**.

### First Run

1. **Setup Wizard** guides you through initial configuration
2. Create your **organization** (business name and contact info)
3. Register your **first user account**
4. Start managing properties!

**New to Nine?** Follow the **[Quick Start Guide](Documentation/Quick-Start-Guide.md)** for a 15-minute walkthrough.

---

## 📋 System Requirements

|             | Minimum                                       | Recommended     |
| ----------- | --------------------------------------------- | --------------- |
| **OS**      | Linux (Ubuntu 20.04+) or Windows 10/11 64-bit | Same            |
| **CPU**     | 2-core, 1.5 GHz                               | 4-core, 2.5 GHz |
| **RAM**     | 2 GB                                          | 4 GB            |
| **Disk**    | 500 MB                                        | 1 GB            |
| **Display** | 1280x800                                      | 1920x1080       |

Nine is distributed as an **AppImage** on Linux (runs on all major distros — no installation required) and a **self-contained .exe** on Windows. All dependencies are bundled.

Optional: SendGrid (email) and Twilio (SMS) accounts for notifications.

---

## 📚 Documentation

- 🚀 **[Quick Start Guide](Documentation/Quick-Start-Guide.md)** — Get up and running in 15 minutes
- 💾 **[Database Management Guide](Documentation/Database-Management-Guide.md)** — Backup, restore, troubleshooting
- 📊 **[Compatibility Matrix](Documentation/Compatibility-Matrix.md)** — Version compatibility and upgrade paths
- 📝 **[Copilot Instructions](.github/copilot-instructions.md)** — Architecture and development guidelines
- 🔄 **[CHANGELOG](CHANGELOG.md)** — Version history

---

## ⚠️ Application Limits

Nine. is intentionally constrained for an optimal user experience:

| Items             | Limit                          | Reason                                |
| ----------------- | ------------------------------ | ------------------------------------- |
| **Properties**    | Maximum 9                      | Focused workflows — it's our identity |
| **Users**         | Maximum 3 (1 system + 2 login) | Simplified access control             |
| **Organizations** | 1                              | Desktop application scope             |
| **File uploads**  | 10 MB per file                 | Performance management                |

---

## 🛠️ Technology Stack

- **Framework:** ASP.NET Core 10.0 + Blazor Server
- **Desktop:** ElectronNET 23.6.2
- **Database:** SQLite with SQLCipher AES-256 encryption
- **PDF Generation:** QuestPDF (<a href='https://www.questpdf.com/license/community.html' target='_blank'>Community License</a>)
- **UI:** Bootstrap 5.3, Material Design Icons

---

## 🏗️ Project Structure

```
Nine/
├── 0-Nine.Core/           # Domain entities and interfaces
├── 1-Nine.Infrastructure/ # Data access and external services
├── 2-Nine.Application/    # Business logic and services
├── 3-Nine.Shared.UI/      # Shared Blazor components
├── 4-Nine/                # Desktop application (Electron host)
└── 6-Tests/               # Unit and integration tests
```

---

## 🧪 Testing

```bash
dotnet test Nine.sln
```

---

## 🤝 Contributing

1. **Fork** the repository
2. **Create a feature branch** from `development`:
   `git checkout -b feature/your-feature`
3. Read **[copilot-instructions.md](.github/copilot-instructions.md)** for architecture guidelines
4. Make changes, write tests, ensure build passes
5. **Submit a pull request** to `development`

**Branch strategy:**

```
main (protected, production-ready)
  ↑ Pull Request
development (integration testing)
  ↑ Direct merge
feature/your-feature
```

**Build and run:**

```bash
dotnet build Nine.sln

cd 4-Nine && dotnet watch
```

---

## 📊 Versioning

[Semantic Versioning](https://semver.org/):

- **MAJOR** = breaking/schema changes
- **MINOR** = new features
- **PATCH** = bug fixes

**Current version:** 1.0.0 · **Database schema:** 1.0.0

---

## 🗺️ Roadmap

### v1.0.0 (March 2026) ✅

- ✅ Complete property, tenant, and lease management
- ✅ Automated invoicing and payment tracking
- ✅ Maintenance request tracking with vendor management
- ✅ Comprehensive 26-item inspections with PDF reports
- ✅ Security deposit investment tracking with dividends
- ✅ Multi-user support with role-based access control
- ✅ Database encryption at rest (SQLCipher AES-256)
- ✅ Linux AppImage + Windows installer

### v1.1.0 (Q2 2026)

- 🎯 Windows/macOS keychain integration
- 🎯 Rate limiting and antiforgery tokens
- 🎯 Code signing for Windows
- 🎯 Calendar refactoring

---

## 📜 License

Copyright © 2026 CIS Guru. Licensed under the **MIT License** — see [LICENSE](LICENSE) for details.

---

## 📞 Support

- 🌐 **Website:** [nineapp.co](https://nineapp.co/)
- 🐛 **Bug Reports:** [GitHub Issues](https://github.com/xnodeoncode/nine/issues)
- 💡 **Feature Requests:** [GitHub Discussions](https://github.com/xnodeoncode/nine/discussions)
- 🔒 **Security Issues:** cisguru@outlook.com (private)

---

**Start with Nine.** 🏠
