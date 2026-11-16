<#
.SYNOPSIS
    Nine startup diagnostic (Windows). PLACEHOLDER, not implemented yet.

.DESCRIPTION
    Windows counterpart of diagnose-startup.sh. Only the parameter block is defined so that
    nine-tool.ps1 can call this script. The code is to be written and tested on a Windows
    computer. Until then it makes no changes and exits with code 1.

    Behavior to implement (same as the bash script): start Nine, time the .NET process start, the
    Kestrel port becoming available and the web UI responding, report the data folder and
    database files, then stop only the instance it started.

.PARAMETER Path
    Path to the Nine executable to test. Defaults to the newest installed Nine.
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Path
)

Write-Host 'diagnose-startup.ps1 is a placeholder and has not been implemented yet. Nothing was changed.' -ForegroundColor Yellow
exit 1
