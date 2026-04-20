param(
    [ValidateSet("Install", "Uninstall")]
    [string]$Mode = "Install",
    [string]$PowerRuleName = "IRIS UI Power Command TCP 5091",
    [int]$PowerPort = 5091,
    [string]$LegacyWallpaperRuleName = "IRIS UI Wallpaper HTTP 5092",
    [int]$LegacyWallpaperPort = 5092,
    [string]$SnapshotOutRuleName = "IRIS UI Snapshot Outbound TCP 5057",
    [int]$SnapshotPort = 5057,
    [string]$FileApiOutRuleName = "IRIS UI File API Outbound TCP 5065",
    [int]$FileApiPort = 5065
)

$ErrorActionPreference = "Stop"

function Ensure-FirewallRule {
    param([string]$RuleName, [int]$Port)

    $existing = netsh advfirewall firewall show rule name="$RuleName" 2>&1
    if (($LASTEXITCODE -eq 0) -and ($existing -notmatch "No rules match")) {
        return
    }

    netsh advfirewall firewall add rule name="$RuleName" dir=in action=allow protocol=TCP localport=$Port profile=private,domain remoteip=localsubnet | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to add firewall rule '$RuleName' for TCP $Port"
    }
}

function Ensure-OutboundFirewallRule {
    param([string]$RuleName, [int]$Port)

    $existing = netsh advfirewall firewall show rule name="$RuleName" 2>&1
    if (($LASTEXITCODE -eq 0) -and ($existing -notmatch "No rules match")) {
        return
    }

    netsh advfirewall firewall add rule name="$RuleName" dir=out action=allow protocol=TCP remoteport=$Port profile=any | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to add outbound firewall rule '$RuleName' for TCP $Port"
    }
}

function Remove-FirewallRule {
    param([string]$RuleName)

    netsh advfirewall firewall delete rule name="$RuleName" | Out-Null
}

if ($Mode -eq "Install") {
    Ensure-FirewallRule -RuleName $PowerRuleName -Port $PowerPort
    Ensure-OutboundFirewallRule -RuleName $SnapshotOutRuleName -Port $SnapshotPort
    Ensure-OutboundFirewallRule -RuleName $FileApiOutRuleName -Port $FileApiPort
    # Legacy: wallpapers are now stored in Postgres. Best-effort cleanup of older hosts.
    netsh advfirewall firewall delete rule name="$LegacyWallpaperRuleName" 2>&1 | Out-Null
    netsh http delete urlacl url="http://+:$LegacyWallpaperPort/" 2>&1 | Out-Null
}
else {
    Remove-FirewallRule -RuleName $PowerRuleName
    Remove-FirewallRule -RuleName $SnapshotOutRuleName
    Remove-FirewallRule -RuleName $FileApiOutRuleName
    netsh advfirewall firewall delete rule name="$LegacyWallpaperRuleName" 2>&1 | Out-Null
    netsh http delete urlacl url="http://+:$LegacyWallpaperPort/" 2>&1 | Out-Null
}
