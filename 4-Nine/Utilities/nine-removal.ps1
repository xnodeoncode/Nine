<#
.SYNOPSIS
    Nine removal and clean-up (Windows). PLACEHOLDER, not implemented yet.

.DESCRIPTION
    Windows counterpart of nine-removal.sh. Only the parameter block is defined so that
    nine-tool.ps1 can call this script and pass options through. The code is to be written and
    tested on a Windows computer. Until then it makes no changes and exits with code 1.

    Design: see "Plans Pending Scope/application-configuration-isolation-plan.md", section 6b.

    Behavior to implement (same as the bash script):
      - Refuse to run while Nine is running.
      - Typed DELETE confirmation before removing data, the key file or registry entries; collect all
        confirmations before deleting anything.
      - Only delete folders named exactly "Nine" and exact, verified per-user registry keys.
      - Honor -WhatIf (preview) and -Force (no prompts).

.PARAMETER All
    Remove everything: executables, data, the key file and registry entries.

.PARAMETER RemoveData
    Delete %APPDATA%\Nine (databases, backups, settings) for all versions.

.PARAMETER RemoveKeychain
    Delete the DPAPI key file %APPDATA%\Nine\aquiis_Nine-Electron.key.

.PARAMETER RemoveKeychainOnly
    Delete only the key file; change nothing else.

.PARAMETER RemoveRegistry
    Delete the registry entries created by Nine's installer (list to be verified on Windows).

.PARAMETER KeepExecutables
    Do not delete portable executables.

.PARAMETER Only
    Remove one portable executable and nothing else.

.PARAMETER Force
    Do not ask for confirmation.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [switch]$All,
    [switch]$RemoveData,
    [switch]$RemoveKeychain,
    [switch]$RemoveKeychainOnly,
    [switch]$RemoveRegistry,
    [switch]$KeepExecutables,
    [string]$Only,
    [switch]$Force
)

Write-Host 'nine-removal.ps1 is a placeholder and has not been implemented yet. Nothing was changed.' -ForegroundColor Yellow
Write-Host 'For the installer build, use Add or Remove Programs. For the portable build, delete the executable.'
exit 1
