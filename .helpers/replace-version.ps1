# Ecoride Version Replacement Script
# Recursively searches and replaces strings across all project files
# Will also replace any version of the string that contains at least the number and alpha text
# such as 0.5.0--alpha and 0.5.0-alpha, both will be replace by their equivalent 0.6.0--alpha and 0.6.0-alpha
# Usage:
# .\replace-version.ps1 -Search "0.6.0-alpha" -Inject "0.6.0-alpha"
# .\replace-version.ps1 -Search "0.6.0-alpha" -Inject "0.6.0-alpha" -WhatIf

param(
    [Parameter(Mandatory=$true)]
    [string]$Search,
    
    [Parameter(Mandatory=$true)]
    [string]$Inject,
    
    [Parameter(Mandatory=$false)]
    [switch]$WhatIf
)

# Extract version components from search string (e.g., "0.6.0-alpha" -> "0.6.0" + "alpha", or "1.0.0" -> "1.0.0")
# Pattern: "X.Y.Z" or "X.Y.Z-suffix" (suffix is optional)
$versionMatch = $Search -match '^([\d.]+)(?:-(.+))?$'
if (-not $versionMatch) {
    Write-Error "Invalid version format. Expected 'X.Y.Z' or 'X.Y.Z-suffix' (e.g., '1.0.0' or '0.6.0-alpha')"
    exit 1
}

$versionNumber = $matches[1]  # "0.6.0" or "1.0.0"
$suffix = $matches[2]         # "alpha" or $null if no suffix

# Extract inject version number (e.g., "0.6.1-alpha" -> "0.6.1", or "1.0.01" -> "1.0.01")
$injectMatch = $Inject -match '^([\d.]+)(?:-(.+))?$'
if (-not $injectMatch) {
    Write-Error "Invalid inject format. Expected 'X.Y.Z' or 'X.Y.Z-suffix' (e.g., '1.0.01' or '0.6.1-alpha')"
    exit 1
}

$injectVersionNumber = $matches[1]  # "0.6.1" or "1.0.01"
$injectSuffix = $matches[2]         # "alpha" or $null if no suffix

# Build pattern to match all hyphen variations while preserving hyphen count
# This regex matches: "0.6.0-alpha", "0.6.0--alpha", "0.6.0---alpha", "1.0.0", etc.
# If no suffix: match plain version number (e.g., "1.0.0")
# If suffix: match version + hyphen(s) + suffix, preserving hyphen count
if ($suffix) {
    # With suffix: match version + hyphen(s) + suffix
    $searchPattern = [regex]::Escape($versionNumber) + '(-+)' + [regex]::Escape($suffix)
    # Replacement uses $1 backreference to preserve captured hyphen count
    $replacePattern = "$injectVersionNumber`$1$injectSuffix"
} else {
    # Without suffix: match plain version number
    $searchPattern = [regex]::Escape($versionNumber)
    # Replacement is just the new version number
    $replacePattern = $injectVersionNumber
}

# Color output functions
function Write-Success {
    param([string]$Message)
    Write-Host "✅ $Message" -ForegroundColor Green
    Write-Host "----------------------------------------" -ForegroundColor Green
}

function Write-Info {
    param([string]$Message)
    Write-Host "ℹ️  $Message" -ForegroundColor Cyan
    Write-Host "----------------------------------------" -ForegroundColor Cyan
}

function Write-Warning {
    param([string]$Message)
    Write-Host "⚠️  $Message" -ForegroundColor Yellow
    Write-Host "----------------------------------------" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Message)
    Write-Host "❌ $Message" -ForegroundColor Red
    Write-Host "----------------------------------------" -ForegroundColor Red
}

# Navigate to repository root
$repoRoot = Split-Path $PSScriptRoot -Parent
Set-Location $repoRoot

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Ecoride Version Replacement" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Info "Repository Root: $repoRoot"
Write-Info "Search String: '$Search'"
Write-Info "Replace With: '$Inject'"
if ($WhatIf) {
    Write-Warning "DRY RUN MODE - No files will be modified"
}
Write-Host ""

# Excluded directories
$excludedDirs = @(
    ".commits",
    ".secrets",
    "build",
    ".git",
    "Flutter",
    "node_modules",
    "bin",
    "obj",
    "logs",
    ".vs",
    ".vscode"
)

# Excluded file patterns
$excludedPatterns = @(
    "*.dll",
    "*.exe",
    "*.pdb",
    "*.cache",
    "*.suo",
    "*.user",
    "package-lock.json",
    "*.min.js",
    "*.min.css",
    "*.ps1",
    "*.xconfig",
    "*.rc",
    "*.xml",
    "*.jar",
    "*\zip-cache\*",
    "*\Flutter\*",
    "*.stamp",
    "package_config.json",
    "CHANGELOG.md"
)

# Get all files recursively, excluding specified directories and patterns
Write-Info "Scanning files..."
$allFiles = Get-ChildItem -Path $repoRoot -Recurse -File | Where-Object {
    $file = $_
    
    # Check if file is in excluded directory
    $isExcluded = $false
    foreach ($excludedDir in $excludedDirs) {
        if ($file.FullName -like "*\$excludedDir\*") {
            $isExcluded = $true
            break
        }
    }
    
    # Check if file matches excluded patterns
    if (-not $isExcluded) {
        foreach ($pattern in $excludedPatterns) {
            if ($file.Name -like $pattern) {
                $isExcluded = $true
                break
            }
        }
    }
    
    -not $isExcluded
}

Write-Success "Found $($allFiles.Count) files to scan"
Write-Host ""

# Search for files containing the search string
$matchedFiles = @()
$totalMatches = 0

Write-Info "Searching for occurrences..."
foreach ($file in $allFiles) {
    try {
        # Read file content
        $content = Get-Content -Path $file.FullName -Raw -ErrorAction SilentlyContinue

        # IMPORTANT: match all hyphen variations.
        # Example input Search=0.5.5-alpha should match 0.5.5-alpha, 0.5.5--alpha, 0.5.5---alpha, etc.
        if ($null -ne $content) {
            $foundMatches = [regex]::Matches($content, $searchPattern)
            $matchCount = $foundMatches.Count
            if ($matchCount -gt 0) {
                $matchedFiles += @{
                    Path = $file.FullName
                    RelativePath = $file.FullName.Replace($repoRoot, "").TrimStart('\')
                    Matches = $matchCount
                }
                $totalMatches += $matchCount
            }
        }
    }
    catch {
        # Skip binary or inaccessible files
        continue
    }
}

Write-Host ""
Write-Success "Found $totalMatches occurrences in $($matchedFiles.Count) files:"
Write-Host ""

# Display matched files
foreach ($match in $matchedFiles) {
    Write-Host "  📄 " -NoNewline -ForegroundColor Yellow
    Write-Host $match.RelativePath -NoNewline
    Write-Host " ($($match.Matches) occurrence(s))" -ForegroundColor DarkGray
}

if ($matchedFiles.Count -eq 0) {
    Write-Host ""
    Write-Warning "No occurrences found. Nothing to replace."
    exit 0
}

Write-Host ""

# Confirm replacement
if (-not $WhatIf) {
    Write-Warning "This will replace all occurrences of '$Search' with '$Inject'"
    $confirm = Read-Host "Continue? (y/N)"
    if ($confirm -ne 'y' -and $confirm -ne 'Y') {
        Write-Info "Operation cancelled"
        exit 0
    }
    Write-Host ""
}

# Perform replacement
$replacedCount = 0
$filesModified = 0

Write-Info "Processing files..."
foreach ($match in $matchedFiles) {
    Write-Host "Processing: $($match.RelativePath)" -ForegroundColor Cyan
    try {
        if ($WhatIf) {
            Write-Host "  [DRY RUN] Would modify: $($match.RelativePath)" -ForegroundColor DarkGray
        }
        else {
            # Read, replace, and write back
            # $replacePattern uses $1 backreference to preserve hyphen count
            $content = Get-Content -Path $match.Path -Raw
            $newContent = $content -replace $searchPattern, $replacePattern
            Set-Content -Path $match.Path -Value $newContent -NoNewline
            
            $replacedCount += $match.Matches
            $filesModified++
            
            Write-Host "  ✓ Modified: $($match.RelativePath)" -ForegroundColor Green
        }
    }
    catch {
        Write-Error "Failed to process: $($match.RelativePath) - $($_.Exception.Message)"
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green

if ($WhatIf) {
    Write-Info "DRY RUN COMPLETE"
    Write-Info "Would have replaced $totalMatches occurrences in $($matchedFiles.Count) files"
}
else {
    Write-Success "REPLACEMENT COMPLETE"
    Write-Success "Replaced $replacedCount occurrences in $filesModified files"
}

Write-Host "========================================" -ForegroundColor Green
Write-Host ""
