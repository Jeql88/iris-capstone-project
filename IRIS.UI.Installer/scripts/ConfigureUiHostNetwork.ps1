param(
    [ValidateSet("Install", "Uninstall")]
    [string]$Mode = "Install",
    [string]$PowerRuleName = "IRIS UI Power Command TCP 5091",
    [int]$PowerPort = 5091,
    [string]$WallpaperRuleName = "IRIS UI Wallpaper HTTP 5092",
    [int]$WallpaperPort = 5092,
    [string]$UrlAclUser = "Everyone"
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

function Remove-FirewallRule {
    param([string]$RuleName)

    netsh advfirewall firewall delete rule name="$RuleName" | Out-Null
}

function Ensure-UrlAcl {
    param([int]$Port, [string]$User)

    $url = "http://+:$Port/"
    $existing = netsh http show urlacl url=$url 2>&1
    if (($LASTEXITCODE -eq 0) -and ($existing -match [Regex]::Escape($url))) {
        return
    }

    netsh http add urlacl url=$url user="$User" | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to add URL ACL for $url"
    }
}

function Remove-UrlAcl {
    param([int]$Port)

    $url = "http://+:$Port/"
    netsh http delete urlacl url=$url | Out-Null
}

if ($Mode -eq "Install") {
    Ensure-FirewallRule -RuleName $PowerRuleName -Port $PowerPort
    Ensure-FirewallRule -RuleName $WallpaperRuleName -Port $WallpaperPort
    Ensure-UrlAcl -Port $WallpaperPort -User $UrlAclUser
}
else {
    Remove-FirewallRule -RuleName $PowerRuleName
    Remove-FirewallRule -RuleName $WallpaperRuleName
    Remove-UrlAcl -Port $WallpaperPort
}
