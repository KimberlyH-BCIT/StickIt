# ===============================================================================
# ELKH Documentation Validation Script
# ===============================================================================
# Purpose: Validates documentation standards compliance across the project
# Usage: Run from project root or integrate with MSBuild process
# ===============================================================================

param(
    [Parameter(Mandatory = $false)]
    [string]$ProjectPath = ".",
    [Parameter(Mandatory = $false)]
    [switch]$DetailedOutput,
    [Parameter(Mandatory = $false)]
    [switch]$FailOnError
)

$ErrorActionPreference = if ($FailOnError) { "Stop" } else { "Continue" }
$script:ValidationErrors = @()
$script:ValidationWarnings = @()  
$script:ValidationSuccesses = @()

# Configuration
$TOC_REQUIRED_LINES = 150
$CLASS_SUMMARY_PATTERN = '/// <summary>'
$TOC_PATTERN = 'TABLE OF CONTENTS'

function Write-ValidationMessage {
    param([string]$Message, [string]$Type = "INFO", [string]$File = "")
    
    $timestamp = Get-Date -Format "HH:mm:ss"
    $prefix = switch ($Type) {
        "ERROR" { "[ERROR]" }
        "WARNING" { "[WARN]" }
        "SUCCESS" { "[OK]" }
        default { "[INFO]" }
    }
    
    $output = "[$timestamp] $prefix $Message"
    if ($File) { $output += " | File: $File" }
    
    Write-Host $output -ForegroundColor $(
        switch ($Type) {
            "ERROR" { "Red" }
            "WARNING" { "Yellow" }
            "SUCCESS" { "Green" }
            default { "White" }
        }
    )
    
    switch ($Type) {
        "ERROR" { $script:ValidationErrors += @{ Message = $Message; File = $File } }
        "WARNING" { $script:ValidationWarnings += @{ Message = $Message; File = $File } }
        "SUCCESS" { $script:ValidationSuccesses += @{ Message = $Message; File = $File } }
    }
}

function Get-FileLineCount {
    param([string]$FilePath)
    try {
        return (Get-Content $FilePath -ErrorAction SilentlyContinue | Measure-Object -Line).Lines
    }
    catch { return 0 }
}

function Test-HasTableOfContents {
    param([string]$FilePath)
    try {
        $content = Get-Content $FilePath -Raw -ErrorAction SilentlyContinue
        return $content -match $TOC_PATTERN
    }
    catch { return $false }
}

function Test-HasClassDocumentation {
    param([string]$FilePath)
    try {
        $content = Get-Content $FilePath -Raw -ErrorAction SilentlyContinue
        return $content -match $CLASS_SUMMARY_PATTERN
    }
    catch { return $false }
}

function Validate-LargeFiles {
    param([string]$ProjectPath)
    
    Write-ValidationMessage "Validating Table of Contents for large files (150+ lines)..."
    
    $csFiles = Get-ChildItem -Path $ProjectPath -Filter "*.cs" -Recurse | 
               Where-Object { $_.FullName -notmatch "\\obj\\|\\bin\\|\\Migrations\\|\\wwwroot\\" }
    
    foreach ($file in $csFiles) {
        $lineCount = Get-FileLineCount $file.FullName
        
        if ($lineCount -ge $TOC_REQUIRED_LINES) {
            $hasToC = Test-HasTableOfContents $file.FullName
            
            if ($hasToC) {
                Write-ValidationMessage "Table of Contents found" "SUCCESS" $file.Name
            } else {
                Write-ValidationMessage "Missing Table of Contents ($lineCount lines)" "ERROR" $file.Name
            }
        }
    }
}

function Validate-ClassDocumentation {
    param([string]$ProjectPath)
    
    Write-ValidationMessage "Validating class-level documentation..."
    
    $csFiles = Get-ChildItem -Path $ProjectPath -Filter "*.cs" -Recurse |
               Where-Object { $_.FullName -notmatch "\\obj\\|\\bin\\|\\Migrations\\|\\wwwroot\\" }
    
    $publicClassFiles = @()
    
    foreach ($file in $csFiles) {
        try {
            $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
            
            if ($content -match 'public\s+(class|interface|controller)') {
                $publicClassFiles += $file
                
                $hasDocumentation = Test-HasClassDocumentation $file.FullName
                
                if ($hasDocumentation) {
                    Write-ValidationMessage "Class documentation found" "SUCCESS" $file.Name
                } else {
                    Write-ValidationMessage "Missing class documentation" "WARNING" $file.Name
                }
            }
        }
        catch {
            Write-ValidationMessage "Error reading file for class validation" "ERROR" $file.Name
        }
    }
    
    Write-ValidationMessage "Processed $($publicClassFiles.Count) public class files"
}

function Generate-ValidationReport {
    Write-Host "`n================================================================" -ForegroundColor Cyan
    Write-Host "                DOCUMENTATION VALIDATION REPORT                " -ForegroundColor Cyan  
    Write-Host "================================================================" -ForegroundColor Cyan
    
    Write-Host "`nSummary:" -ForegroundColor White
    Write-Host "[OK] Successful validations: $($script:ValidationSuccesses.Count)" -ForegroundColor Green
    Write-Host "[WARN] Warnings: $($script:ValidationWarnings.Count)" -ForegroundColor Yellow  
    Write-Host "[ERROR] Errors: $($script:ValidationErrors.Count)" -ForegroundColor Red
    
    if ($script:ValidationErrors.Count -gt 0) {
        Write-Host "`nErrors requiring attention:" -ForegroundColor Red
        foreach ($error in $script:ValidationErrors) {
            Write-Host "  - $($error.Message)" -ForegroundColor Red
            if ($error.File) {
                Write-Host "    File: $($error.File)" -ForegroundColor DarkRed
            }
        }
    }
    
    if ($script:ValidationWarnings.Count -gt 0) {
        Write-Host "`nWarnings for consideration:" -ForegroundColor Yellow
        foreach ($warning in $script:ValidationWarnings) {
            Write-Host "  - $($warning.Message)" -ForegroundColor Yellow
            if ($warning.File) {
                Write-Host "    File: $($warning.File)" -ForegroundColor DarkYellow
            }
        }
    }
    
    $overallStatus = if ($script:ValidationErrors.Count -eq 0) { "PASSED" } else { "FAILED" }
    $statusColor = if ($overallStatus -eq "PASSED") { "Green" } else { "Red" }
    
    Write-Host "`nOverall Status: $overallStatus" -ForegroundColor $statusColor
    Write-Host "================================================================" -ForegroundColor Cyan
    
    return ($script:ValidationErrors.Count -eq 0)
}

# Main Execution
Write-Host "Documentation Validation Started" -ForegroundColor Cyan
Write-Host "Project Path: $ProjectPath" -ForegroundColor Gray
Write-Host "Timestamp: $(Get-Date)" -ForegroundColor Gray
Write-Host ""

try {
    if (-not (Test-Path $ProjectPath)) {
        Write-ValidationMessage "Project path does not exist: $ProjectPath" "ERROR"
        exit 1
    }
    
    Validate-LargeFiles $ProjectPath
    Validate-ClassDocumentation $ProjectPath
    
    $validationPassed = Generate-ValidationReport
    
    if ($FailOnError -and -not $validationPassed) {
        Write-Host "`nValidation failed with errors. Build should be stopped." -ForegroundColor Red
        exit 1
    }
    
    Write-Host "`nDocumentation validation completed successfully." -ForegroundColor Green
    exit 0
}
catch {
    Write-ValidationMessage "Unexpected error during validation: $($_.Exception.Message)" "ERROR"
    if ($FailOnError) {
        exit 1
    }
    exit 0
}
