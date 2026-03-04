# IRIS Migration Verification Script
# This script checks if your migrations are in the correct state

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "IRIS Migration Verification" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$ErrorActionPreference = "Continue"

# Expected files
$expectedFiles = @(
    "IRIS.Core\Migrations\20251014161408_InitialCreate.cs",
    "IRIS.Core\Migrations\20251014161408_InitialCreate.Designer.cs",
    "IRIS.Core\Migrations\IRISDbContextModelSnapshot.cs"
)

# Files that should NOT exist
$deprecatedFiles = @(
    "IRIS.Core\Migrations\20251110112050_UpdatePasswordsToBCrypt.cs",
    "IRIS.Core\Migrations\20251110112050_UpdatePasswordsToBCrypt.Designer.cs",
    "IRIS.Core\Migrations\20251110113434_FixBCryptHashes.cs",
    "IRIS.Core\Migrations\20251110113434_FixBCryptHashes.Designer.cs",
    "IRIS.Core\Migrations\20251110120226_SyncModel.cs",
    "IRIS.Core\Migrations\20251110120226_SyncModel.Designer.cs"
)

Write-Host "Checking expected migration files..." -ForegroundColor Yellow
$allExpectedExist = $true
foreach ($file in $expectedFiles) {
    if (Test-Path $file) {
        Write-Host "  ? $file" -ForegroundColor Green
    } else {
        Write-Host "  ? MISSING: $file" -ForegroundColor Red
        $allExpectedExist = $false
    }
}

Write-Host ""
Write-Host "Checking for deprecated migration files..." -ForegroundColor Yellow
$noDeprecatedExist = $true
foreach ($file in $deprecatedFiles) {
    if (Test-Path $file) {
        Write-Host "  ? SHOULD NOT EXIST: $file" -ForegroundColor Red
        $noDeprecatedExist = $false
    } else {
        Write-Host "  ? Correctly removed: $file" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "Checking InitialCreate for user seeding..." -ForegroundColor Yellow
$initialCreateContent = Get-Content "IRIS.Core\Migrations\20251014161408_InitialCreate.cs" -Raw
if ($initialCreateContent -match '\$2a\$11\$e6AtSfzSfXfCHsk5yjXWIuzIGGfaXRe') {
    Write-Host "  ? BCrypt user seeding found in InitialCreate" -ForegroundColor Green
    $userSeedingCorrect = $true
} else {
    Write-Host "  ? User seeding not found or incorrect in InitialCreate" -ForegroundColor Red
    $userSeedingCorrect = $false
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
if ($allExpectedExist -and $noDeprecatedExist -and $userSeedingCorrect) {
    Write-Host "? All Checks Passed!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Your migrations are correctly configured." -ForegroundColor Green
    Write-Host "Run '.\RESET_DATABASE.ps1' to apply them." -ForegroundColor Yellow
} else {
    Write-Host "? Issues Found!" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Please review the errors above." -ForegroundColor Red
    Write-Host "Refer to MIGRATION_FIX_GUIDE.md for solutions." -ForegroundColor Yellow
}
Write-Host ""
