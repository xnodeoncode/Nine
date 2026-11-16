<#
.SYNOPSIS
    Nine install helper (Windows). PLACEHOLDER, not implemented yet.

.DESCRIPTION
    Windows counterpart of install-desktop-integration.sh. Only the parameter block is defined so
    that nine-tool.ps1 can call this script. What it should do on Windows is still to be decided
    (for example, shortcuts and a stable folder for the portable build, and copying the Nine
    scripts next to it). The code is to be written and tested on a Windows computer. Until then
    it makes no changes and exits with code 1.

.PARAMETER Path
    Path to the Nine executable to set up (Nine-<version>-x64-Portable.exe).
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Position = 0)]
    [string]$Path
)

Write-Host 'install-desktop-integration.ps1 is a placeholder and has not been implemented yet. Nothing was changed.' -ForegroundColor Yellow
Write-Host 'Use the Nine installer (Nine-<version>-x64-Setup.exe), or run the portable executable directly.'
exit 1
