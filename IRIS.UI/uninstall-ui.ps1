#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Uninstalls IRIS.UI from a Windows host PC.
.DESCRIPTION
    Stops running IRIS.UI process, removes install folder, shortcuts, and optionally
    removes firewall rules / URL ACL created by install-ui.ps1.
.EXAMPLE
    .\uninstall-ui.ps1
    .\uninstall-ui.ps1 -KeepNetworkRules
#>
param(
    [string]$InstallDir = "C:\Program Files\IRIS\UI",
    [string]$StartMenuFolder = "IRIS",
    [string]$ShortcutName = "IRIS UI",
    [string]$FirewallRulePower = "IRIS UI Power Command TCP 5091",
    [string]$FirewallRuleWallpaper = "IRIS UI Wallpaper HTTP 5092",
    [switch]$KeepNetworkRules
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Write-Step {
    param([string]$Message)
    Write-Host "[IRIS.UI] $Message" -ForegroundColor Cyan
}

Write-Step "Stopping running IRIS.UI processes (if any)"
$running = Get-Process -Name "IRIS.UI" -ErrorAction SilentlyContinue
if ($running) {
    $running | Stop-Process -Force
}

Write-Step "Removing shortcuts"
$startMenuShortcut = Join-Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\$StartMenuFolder" ("$ShortcutName.lnk")
$desktopShortcut = Join-Path "$env:Public\Desktop" ("$ShortcutName.lnk")
$startMenuFolderPath = Join-Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" $StartMenuFolder

foreach ($path in @($startMenuShortcut, $desktopShortcut)) {
    if (Test-Path $path) {
        Remove-Item -Path $path -Force
    }
}

if (Test-Path $startMenuFolderPath) {
    $remaining = Get-ChildItem -Path $startMenuFolderPath -Force -ErrorAction SilentlyContinue
    if (-not $remaining) {
        Remove-Item -Path $startMenuFolderPath -Force
    }
}

Write-Step "Removing install directory"
if (Test-Path $InstallDir) {
    Remove-Item -Path $InstallDir -Recurse -Force
}

if (-not $KeepNetworkRules) {
    Write-Step "Removing firewall rules + URL ACL"
    netsh advfirewall firewall delete rule name="$FirewallRulePower" | Out-Null
    # Best-effort cleanup of the legacy wallpaper HTTP server (removed in favor of DB-backed wallpapers).
    netsh advfirewall firewall delete rule name="$FirewallRuleWallpaper" 2>&1 | Out-Null
    netsh http delete urlacl url=http://+:5092/ 2>&1 | Out-Null
}
else {
    Write-Step "Keeping firewall rules + URL ACL (requested)"
}

Write-Host ""
Write-Host "IRIS.UI uninstall complete." -ForegroundColor Green
Write-Host "  Removed path: $InstallDir"
Write-Host ""
if (-not $KeepNetworkRules) {
    Write-Host "Network rules removed."
} else {
    Write-Host "Network rules were kept."
}
