# ELKH Project - Comprehensive Encoding & Language Verification
# Final sweep to ensure UTF-8 without BOM and English-only content
# This script provides detailed analysis of all project files

Write-Host "🔍 ELKH Project - Final Encoding & Language Verification" -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "Checking for UTF-8 without BOM encoding and English-only content..." -ForegroundColor White
Write-Host ""

# Initialize counters and collections
$totalFiles = 0
$bomFiles = @()
$nonUtf8Files = @()
$chineseCharFiles = @()
$unicodeIssueFiles = @()
$cleanFiles = 0

# Define file types to check
$fileExtensions = @("*.cs", "*.cshtml", "*.json", "*.js", "*.css", "*.html", "*.xml", "*.txt", "*.md", "*.yml", "*.yaml", "*.config", "*.ps1")

# Get all relevant files (excluding build artifacts and third-party libraries)
Write-Host "📂 Scanning project files..." -ForegroundColor Yellow
$allFiles = Get-ChildItem -Path "ELKH" -Recurse -File -Include $fileExtensions |
    Where-Object { 
        $_.FullName -notmatch "\\(bin|obj|node_modules|\.vs|packages)\\" -and
        $_.FullName -notmatch "\\lib\\" -and
        $_.Name -notlike "*min.*" -and
        $_.Name -notlike "bootstrap*" -and
        $_.Name -notlike "jquery*" -and
        $_.Name -notlike "*.Designer.*" -and
        $_.Name -notlike "*.generated.*"
    }

# Also check root-level files
$rootFiles = Get-ChildItem -Path "." -File -Include $fileExtensions | Where-Object { $_.Name -ne "validate-team-setup.ps1" }
$allFiles = $allFiles + $rootFiles

$totalFiles = $allFiles.Count
Write-Host "Found $totalFiles files to analyze" -ForegroundColor Green
Write-Host ""

# Function to detect Chinese characters (CJK Unicode ranges)
function Test-ChineseCharacters {
    param([string]$content)
    
    # Chinese/Japanese/Korean Unicode ranges
    $cjkRanges = @(
        '\u4e00-\u9fff',  # CJK Unified Ideographs
        '\u3400-\u4dbf',  # CJK Extension A
        '\u20000-\u2a6df', # CJK Extension B
        '\uf900-\ufaff',  # CJK Compatibility Ideographs
        '\u3040-\u309f',  # Hiragana
        '\u30a0-\u30ff',  # Katakana
        '\uff00-\uffef'   # Halfwidth and Fullwidth Forms
    )
    
    foreach ($range in $cjkRanges) {
        if ($content -match "[$range]") {
            return $true
        }
    }
    return $false
}

# Function to check encoding
function Test-FileEncoding {
    param([string]$filePath)
    
    try {
        $bytes = [System.IO.File]::ReadAllBytes($filePath)
        
        # Check for BOM
        $hasBom = $false
        if ($bytes.Length -ge 3) {
            # UTF-8 BOM: EF BB BF
            if ($bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
                $hasBom = $true
            }
            # UTF-16 LE BOM: FF FE
            if ($bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) {
                $hasBom = $true
            }
            # UTF-16 BE BOM: FE FF
            if ($bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF) {
                $hasBom = $true
            }
        }
        
        # Try to decode as UTF-8
        try {
            $utf8 = [System.Text.Encoding]::UTF8.GetString($bytes)
            $isUtf8 = $true
        } catch {
            $isUtf8 = $false
        }
        
        return @{
            HasBOM = $hasBom
            IsUTF8 = $isUtf8
            Content = if ($isUtf8) { $utf8 } else { "" }
        }
    } catch {
        return @{
            HasBOM = $false
            IsUTF8 = $false
            Content = ""
            Error = $_.Exception.Message
        }
    }
}

# Process each file
Write-Host "🔍 Analyzing files..." -ForegroundColor Yellow
$progressCounter = 0

foreach ($file in $allFiles) {
    $progressCounter++
    $percentComplete = [math]::Round(($progressCounter / $totalFiles) * 100, 1)
    
    if ($progressCounter % 50 -eq 0) {
        Write-Host "   Progress: $progressCounter/$totalFiles ($percentComplete%)" -ForegroundColor Gray
    }
    
    $encodingResult = Test-FileEncoding -filePath $file.FullName
    
    # Track files with BOM
    if ($encodingResult.HasBOM) {
        $bomFiles += $file.FullName
    }
    
    # Track non-UTF-8 files
    if (-not $encodingResult.IsUTF8) {
        $nonUtf8Files += @{
            Path = $file.FullName
            Error = $encodingResult.Error
        }
        continue
    }
    
    # Check for Chinese characters in UTF-8 content
    if ($encodingResult.Content -and (Test-ChineseCharacters -content $encodingResult.Content)) {
        $chineseCharFiles += $file.FullName
    }
    
    # Check for problematic Unicode characters
    $problematicChars = @()
    if ($encodingResult.Content) {
        if ($encodingResult.Content -match '═') { $problematicChars += 'Box drawing (═)' }
        if ($encodingResult.Content -match '—') { $problematicChars += 'Em dash (—)' }
        if ($encodingResult.Content -match '[''']') { $problematicChars += 'Smart single quotes' }
        if ($encodingResult.Content -match '[""„"]') { $problematicChars += 'Smart double quotes' }
        if ($encodingResult.Content -match '…') { $problematicChars += 'Ellipsis (…)' }
    }
    
    if ($problematicChars.Count -gt 0) {
        $unicodeIssueFiles += @{
            Path = $file.FullName
            Issues = $problematicChars
        }
    } else {
        $cleanFiles++
    }
}

Write-Host ""
Write-Host "📋 COMPREHENSIVE ANALYSIS RESULTS" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

# Summary Statistics
Write-Host "📊 SUMMARY STATISTICS:" -ForegroundColor Green
Write-Host "   Total Files Analyzed: $totalFiles"
Write-Host "   Clean Files (UTF-8 without BOM, English): $cleanFiles" -ForegroundColor Green
Write-Host "   Files with Issues: $($totalFiles - $cleanFiles)" -ForegroundColor $(if ($totalFiles -eq $cleanFiles) { "Green" } else { "Yellow" })
Write-Host ""

# BOM Detection Results
if ($bomFiles.Count -gt 0) {
    Write-Host "❌ FILES WITH BYTE ORDER MARK (BOM) - $($bomFiles.Count) files:" -ForegroundColor Red
    foreach ($file in $bomFiles) {
        Write-Host "   • $file" -ForegroundColor Red
    }
    Write-Host ""
} else {
    Write-Host "✅ BOM CHECK: All files are UTF-8 without BOM" -ForegroundColor Green
    Write-Host ""
}

# Non-UTF-8 Files
if ($nonUtf8Files.Count -gt 0) {
    Write-Host "❌ NON-UTF-8 FILES - $($nonUtf8Files.Count) files:" -ForegroundColor Red
    foreach ($file in $nonUtf8Files) {
        Write-Host "   • $($file.Path)" -ForegroundColor Red
        if ($file.Error) {
            Write-Host "     Error: $($file.Error)" -ForegroundColor Gray
        }
    }
    Write-Host ""
} else {
    Write-Host "✅ ENCODING CHECK: All files are valid UTF-8" -ForegroundColor Green
    Write-Host ""
}

# Chinese Characters Detection
if ($chineseCharFiles.Count -gt 0) {
    Write-Host "❌ FILES WITH CHINESE/CJK CHARACTERS - $($chineseCharFiles.Count) files:" -ForegroundColor Red
    foreach ($file in $chineseCharFiles) {
        Write-Host "   • $file" -ForegroundColor Red
    }
    Write-Host ""
} else {
    Write-Host "✅ LANGUAGE CHECK: No Chinese/CJK characters found" -ForegroundColor Green
    Write-Host ""
}

# Unicode Issues Detection  
if ($unicodeIssueFiles.Count -gt 0) {
    Write-Host "⚠️  FILES WITH UNICODE ISSUES - $($unicodeIssueFiles.Count) files:" -ForegroundColor Yellow
    foreach ($file in $unicodeIssueFiles) {
        Write-Host "   • $($file.Path)" -ForegroundColor Yellow
        foreach ($issue in $file.Issues) {
            Write-Host "     - $issue" -ForegroundColor Gray
        }
    }
    Write-Host ""
} else {
    Write-Host "✅ UNICODE CHECK: No problematic Unicode characters found" -ForegroundColor Green
    Write-Host ""
}

# Final Verdict
Write-Host "🏁 FINAL VERDICT:" -ForegroundColor Cyan
Write-Host "================" -ForegroundColor Cyan

$allPassed = ($bomFiles.Count -eq 0) -and 
             ($nonUtf8Files.Count -eq 0) -and 
             ($chineseCharFiles.Count -eq 0) -and 
             ($unicodeIssueFiles.Count -eq 0)

if ($allPassed) {
    Write-Host "🎉 PERFECT! All files pass encoding and language verification:" -ForegroundColor Green
    Write-Host "   ✅ UTF-8 without BOM encoding: PASSED" -ForegroundColor Green
    Write-Host "   ✅ English-only content: PASSED" -ForegroundColor Green
    Write-Host "   ✅ No problematic Unicode: PASSED" -ForegroundColor Green
    Write-Host ""
    Write-Host "🚀 Your project is ready for international team collaboration!" -ForegroundColor Green
} else {
    Write-Host "⚠️  ISSUES DETECTED - Please address the following:" -ForegroundColor Red
    
    if ($bomFiles.Count -gt 0) {
        Write-Host "   🔧 Remove BOM from $($bomFiles.Count) files" -ForegroundColor Yellow
    }
    if ($nonUtf8Files.Count -gt 0) {
        Write-Host "   🔧 Convert $($nonUtf8Files.Count) files to UTF-8" -ForegroundColor Yellow
    }
    if ($chineseCharFiles.Count -gt 0) {
        Write-Host "   🔧 Review $($chineseCharFiles.Count) files for Chinese characters" -ForegroundColor Yellow
    }
    if ($unicodeIssueFiles.Count -gt 0) {
        Write-Host "   🔧 Fix Unicode issues in $($unicodeIssueFiles.Count) files" -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "💡 Run 'fix-encoding-issues.ps1' to automatically fix most issues" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "📅 Verification completed: $(Get-Date)" -ForegroundColor Gray
Write-Host "📁 Project path: $(Get-Location)" -ForegroundColor Gray