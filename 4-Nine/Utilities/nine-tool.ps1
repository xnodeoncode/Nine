<#
.SYNOPSIS
    Nine Tool (Windows). One entry point for the Nine install and clean-up scripts.

.DESCRIPTION
    PLACEHOLDER. This is the initial wrapper for the Windows scripts. It mirrors nine-tool.sh. The scripts it
    calls (install-desktop-integration.ps1, nine-removal.ps1, diagnose-startup.ps1) are placeholders too, so the
    plumbing is in place and the code is still to be written. None of it has been tested on Windows; it is meant
    to be completed and tested on a Windows computer.

    Commands:
      install   Runs install-desktop-integration.ps1 from the same folder (placeholder for now).
                On Windows you can also use the installer (Nine-<version>-x64-Setup.exe) or run the
                portable executable (Nine-<version>-x64-Portable.exe) directly.
      remove    Runs nine-removal.ps1 from the same folder, passing every option through (placeholder for now).
      diagnose  Runs diagnose-startup.ps1 from the same folder (placeholder for now).
      help      Show this help.

    Planned options for nine-removal.ps1 (same meaning as the bash script's switches):
      -All -RemoveData -RemoveKeychain -RemoveKeychainOnly -RemoveRegistry -KeepExecutables
      -Only <file> -WhatIf -Force

.EXAMPLE
    .\nine-tool.ps1 help

.EXAMPLE
    .\nine-tool.ps1 remove -WhatIf
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Command,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Rest
)

$scriptDir = Split-Path -Parent $PSCommandPath

function Show-Usage {
    Get-Help $PSCommandPath -Detailed | Out-String | Write-Host
}

function Write-Failure([string]$Message) {
    Write-Host "Error: $Message" -ForegroundColor Red
}

# Runs a sibling script in a child PowerShell so that -Switch style options pass through unchanged.
function Invoke-Sibling([string]$Name, [string[]]$Arguments) {
    $path = Join-Path $scriptDir $Name
    if (-not (Test-Path -LiteralPath $path)) {
        Write-Failure "$Name was not found in $scriptDir (not written yet)."
        exit 1
    }
    $hostExe = (Get-Process -Id $PID).Path
    & $hostExe -NoProfile -ExecutionPolicy Bypass -File $path @Arguments
    exit $LASTEXITCODE
}

switch ($Command) {
    'install' { Invoke-Sibling 'install-desktop-integration.ps1' $Rest }
    'remove' { Invoke-Sibling 'nine-removal.ps1' $Rest }
    'diagnose' { Invoke-Sibling 'diagnose-startup.ps1' $Rest }
    { $_ -in @('help', '-h', '--help') } { Show-Usage; exit 0 }
    default {
        if ($Command) { Write-Failure "Unknown command: $Command" }
        Show-Usage
        exit 1
    }
}
