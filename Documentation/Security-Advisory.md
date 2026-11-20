# Nine. - Security Advisory

**Version:** 1.2.1  
**Last Updated:** November 18, 2026

The following security advisories are surfaced during application builds. When patches or work-arounds are made available, they are worked into the application.

**Click the package name to view advisory details.**

## Security Advisories and Fixes

| Advisory                                                                                          | Package or Feature                          | Mitigated? | Mitigation Plan                                                                                                                                                                                                                                                                                                                                                                                                        |
| ------------------------------------------------------------------------------------------------- | ------------------------------------------- | ---------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| ['Microsoft.AspNetCore.SignalR.Common' 10.0.1](https://github.com/advisories/GHSA-334f-vvp4-7g6p) | Microsoft.AspNetCore.SignalR.Common' 10.0.1 | Yes        | Upgraded to patched version 10.0.13                                                                                                                                                                                                                                                                                                                                                                                    |
| ['SQLitePCLRaw.lib.e_sqlite3' 2.1.11](https://github.com/advisories/GHSA-2m69-gcr7-jv3q)          | 'SQLitePCLRaw.lib.e_sqlite3' 2.1.11         | Yes        | Patch not available for this release. This is a transitive dependency of SQLitePCLRaw.bundle_e_sqlcipher. Adding the latest version of the dependent library to the affected projects overrides the bundled version with the patched version. **Added patched version SQLitePCLRaw.lib.e_sqlite3 Version="3.5.3.4.1-pre1" to affected projects.** Local packages take precedence over transient packages during build. |
