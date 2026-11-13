# Third-Party Licensing Notices

Nine is licensed under the [MIT License](../LICENSE). Nine depends on third-party software that is licensed separately. This document lists those components and their licenses so that people who install, operate, or redistribute Nine can review the terms that apply to them.

> This document is informational and is not legal advice. License details were taken from package metadata in the local NuGet cache and from the vendors' published terms at the time of writing. Re-check them when dependencies are upgraded.

_Last reviewed: 2026-11-12 (Nine dependency versions as listed below)._

---

## Action Required: QuestPDF

Nine uses **QuestPDF** to generate its PDF documents (leases, lease renewals, invoices, payment receipts, inspections, checklists, and financial reports). QuestPDF is **not** covered by Nine's MIT license. It has its own license terms, and **you are responsible for confirming that you are eligible to use it.**

| Item                       | Detail                                                                                    |
| -------------------------- | ----------------------------------------------------------------------------------------- |
| Package / version          | `QuestPDF` 2025.12.1 (referenced in `1-Nine.Infrastructure` and `4-Nine`)                 |
| Vendor                     | Marcin Ziąbek (QuestPDF), <https://www.questpdf.com>                                      |
| License selection in code  | Each PDF generator sets `QuestPDF.Settings.License = LicenseType.Community`               |
| Use in Nine                | Only for documents Nine itself creates. Nine does not expose general-purpose PDF generation. |

### Eligibility summary (verify with QuestPDF)

The QuestPDF Community License is free for:

- Individuals and organizations with annual gross revenue under **USD 1,000,000** (measured across entities under common control).
- Charitable organizations and qualifying academic institutions.
- Open-source projects distributed under an OSI-approved license.

A **paid license is required** for:

- Organizations above the revenue threshold.
- Government and public-sector organizations (other than qualifying academic institutions).
- Publicly traded companies.

Eligibility is self-certified by the user. See the vendor's current terms:

- Community License: <https://www.questpdf.com/license/community.html>
- License Selection Guide: <https://www.questpdf.com/license/guide.html>
- Pricing and FAQ: <https://www.questpdf.com/pricing.html>

### Which terms apply to the bundled version

The QuestPDF license published with a given release governs that release. The package metadata for `QuestPDF 2025.12.1` (file `PackageLicense.md` inside the NuGet package) presents a "Community MIT License" with the same USD 1M small-business / open-source / non-profit selection guide. The vendor's website now publishes a newer Community License that describes itself as a source-available commercial license. When upgrading QuestPDF, read the license shipped with the new version before upgrading.

### Open question

It has not been confirmed with QuestPDF whether a business that installs and runs a compiled copy of Nine (without writing code that calls QuestPDF) needs its own QuestPDF license. See the Phase 0 / Option A2 notes in the PDF license plan. Until QuestPDF replies, treat each installer as responsible for their own eligibility.

### Bundled QuestPDF third-party components

The QuestPDF package ships additional components under their own licenses (license texts are included in the package's `ExternalDependencyLicenses` and `LatoFont` folders): Skia, HarfBuzz, ICU, FreeType-related libraries, libjpeg-turbo, libpng, libwebp, zlib, expat, qpdf, wuffs, and the Lato font (SIL Open Font License).

---

## Runtime Dependencies (shipped with Nine)

Licenses below were read from each package's `.nuspec` in the NuGet cache unless noted.

| Component                                            | Version     | License          | Notes                                                       |
| ---------------------------------------------------- | ----------- | ---------------- | ----------------------------------------------------------- |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore    | 10.0.1      | MIT              | Authentication / user management                            |
| Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore | 10.0.1      | MIT              |                                                             |
| Microsoft.AspNetCore.Components.Web                  | 10.0.1      | MIT              | Blazor components                                           |
| Microsoft.AspNetCore.SignalR.Client                  | 10.0.1      | MIT              |                                                             |
| Microsoft.EntityFrameworkCore                        | 10.0.1      | MIT              |                                                             |
| Microsoft.EntityFrameworkCore.Sqlite                 | 10.0.1      | MIT              |                                                             |
| Microsoft.Data.Sqlite.Core                           | 10.0.9      | MIT              |                                                             |
| Microsoft.Extensions.Logging.Abstractions            | 10.0.1      | MIT              |                                                             |
| Microsoft.Extensions.Options                         | 10.0.1      | MIT              |                                                             |
| System.Security.Cryptography.ProtectedData           | 9.0.0       | MIT              |                                                             |
| System.Security.Cryptography.Pkcs                    | 9.0.21      | MIT              |                                                             |
| SQLitePCLRaw.bundle_e_sqlcipher                      | 2.1.11      | Apache-2.0       | SourceGear. Bundles native SQLCipher builds (see below)     |
| SendGrid                                             | 9.29.3      | MIT              | Email delivery client                                       |
| Twilio                                               | 7.14.0      | MIT              | SMS client. License file at the project's GitHub repository |
| ElectronNET.Core / ElectronNET.Core.AspNet           | 0.4.1       | MIT              | Desktop host                                                |
| QuestPDF                                             | 2025.12.1   | See above        | **Action required — see QuestPDF section**                  |

### Transitive dependencies (pulled in by the packages above)

Found with `dotnet list Nine.sln package --include-transitive`; licenses read from each package's `.nuspec` (non-Microsoft packages only).

| Component                                   | Version        | License    | Pulled in by                                              |
| ------------------------------------------- | -------------- | ---------- | --------------------------------------------------------- |
| ElectronNET.Core.API                        | 0.4.1          | MIT        | ElectronNET.Core                                          |
| SocketIOClient, SocketIO.Core, SocketIO.Serializer.Core, SocketIO.Serializer.SystemTextJson | 3.1.2 | MIT | ElectronNET.Core (host/app communication) |
| Newtonsoft.Json                             | 13.0.1 / 13.0.3 | MIT       | SendGrid / Twilio / ElectronNET                           |
| starkbank-ecdsa                             | 1.3.3          | MIT        | SendGrid                                                  |
| SQLitePCLRaw.core, .provider.e_sqlcipher, .lib.e_sqlcipher | 2.1.11 | Apache-2.0 | SQLitePCLRaw.bundle_e_sqlcipher       |
| SQLitePCLRaw.bundle_e_sqlite3, .provider.e_sqlite3, .lib.e_sqlite3 | 2.1.11 | Apache-2.0 | Microsoft.EntityFrameworkCore.Sqlite |
| Humanizer.Core                              | 2.14.1         | MIT        | Microsoft.EntityFrameworkCore.Design (design-time)        |
| Mono.TextTemplating                         | 3.0.0          | MIT        | Microsoft.EntityFrameworkCore.Design (design-time)        |

Transitive Microsoft packages (`Microsoft.*`, `System.*`) are omitted; they are MIT licensed. Twilio, SendGrid, Newtonsoft.Json (via SendGrid/Twilio) and starkbank-ecdsa will be removed when the SendGrid and Twilio integrations are phased out.

### SQLCipher (database encryption)

`SQLitePCLRaw.bundle_e_sqlcipher` includes `SQLitePCLRaw.lib.e_sqlcipher`, which contains platform-specific native builds of **SQLCipher**. The NuGet package notes these are unofficial, unsupported builds. SQLCipher Community Edition is distributed by Zetetic under a BSD-style license that requires its copyright notice and license text to be reproduced in documentation or other materials supplied with binary distributions. **To do:** confirm the exact SQLCipher license text and add it to the notices that ship with binary packages.

### Electron (desktop builds)

The desktop build uses ElectronNET.Core to host the application in **Electron** (`ElectronVersion` 30.4.0, `ElectronBuilderVersion` 26.0 in `4-Nine/Nine.csproj`). Electron and electron-builder are MIT licensed. Electron bundles **Chromium** and other third-party components under their own licenses (primarily BSD-style, plus others); these license texts are included in the Electron distribution as `LICENSES.chromium.html` and `LICENSE`. Packaged desktop builds (AppImage, Flatpak, installers) should keep these files. **To do:** confirm they are retained in each packaging target.

### .NET runtime

Nine targets .NET 10. The .NET runtime and ASP.NET Core framework are MIT licensed (Microsoft / .NET Foundation).

---

## Front-End Assets

| Component         | Version | License | Source                                                                                         |
| ----------------- | ------- | ------- | ---------------------------------------------------------------------------------------------- |
| Bootstrap         | 5.3.3   | MIT     | Referenced by the app: `4-Nine/wwwroot/lib/bootstrap/dist/css/` (license header in the CSS file) |
| Bootstrap (libman copy) | 5.3.8 | MIT | `4-Nine/libman.json` restores `bootstrap@5.3.8` into `wwwroot/lib/bootstrap/css/`; not referenced by `App.razor` |
| Bootstrap Icons   | 1.13.1  | MIT     | Loaded at runtime from the jsDelivr CDN (`cdn.jsdelivr.net`); not bundled in the repository    |

> **Note:** There are two Bootstrap copies with different versions (5.3.3 in `dist/`, used by `App.razor`; 5.3.8 from `libman.json`). Consolidate them so the shipped version is clear.

> **npm packages:** the repository has no `package.json` or lockfile, so there are no npm dependencies in the codebase itself. Front-end libraries are managed through LibMan (`libman.json`, cdnjs provider) and static files. The desktop build uses npm tooling (Electron, electron-builder) through ElectronNET.Core at build time; those are covered in the Electron section and are not listed by `dotnet list`.

> **Note:** Bootstrap Icons is fetched from a public CDN at runtime, so the application needs internet access to display those icons and sends requests to a third party. If offline or privacy-preserving use matters, bundle the icon font locally (together with its license).

---

## Development and Test Dependencies (not shipped to end users)

| Component                          | Version  | License    |
| ---------------------------------- | -------- | ---------- |
| bUnit                              | 2.7.2    | MIT        |
| AngleSharp                         | 1.5.0    | MIT        |
| xUnit                              | 2.9.3    | Apache-2.0 |
| xunit.runner.visualstudio          | 3.1.4    | Apache-2.0 |
| Microsoft.NET.Test.Sdk             | 17.14.1  | MIT        |
| coverlet.collector                 | 6.0.4    | MIT        |
| Microsoft.EntityFrameworkCore.Design | 10.0.1 | MIT        |
| Microsoft.EntityFrameworkCore.Tools  | 10.0.1 | MIT        |

FluentAssertions was removed from the test projects in favor of xUnit `Assert` because FluentAssertions 8.x is under the Xceed commercial license (free for non-commercial use only).

---

## Items to Complete

- [ ] Hear back from QuestPDF about distributing Nine with QuestPDF and downstream business use (see the PDF license plan).
- [ ] Surface the QuestPDF notice in the README, public information pages, and the in-app About page.
- [ ] Add the SQLCipher license text to binary distributions.
- [ ] Confirm Electron / Chromium license files ship in every packaged desktop build.
- [ ] Decide whether to bundle Bootstrap Icons locally instead of using the CDN.
- [ ] Re-review this document whenever dependencies change (`dotnet list package` for each project).

## How to Refresh This List

```bash
dotnet list Nine.sln package --include-transitive
```

For each package, read the `license` element in its `.nuspec` under `~/.nuget/packages/<name>/<version>/`, and review any `LICENSE` / `ExternalDependencyLicenses` files in the package.

`dotnet list` only covers NuGet packages. Also check `4-Nine/libman.json` (LibMan front-end libraries), the static files under `wwwroot/`, any external URLs in `4-Nine/Shared/App.razor`, and the Electron / electron-builder versions in `4-Nine/Nine.csproj`.
