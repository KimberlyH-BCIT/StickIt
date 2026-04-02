# ELKH Team Environment Validation Script
# Checks if your development environment is properly configured for encoding standards
# Run this script to verify your setup is correct

Write-Host "🔍 ELKH Project - Environment Validation" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

$allChecksPass = $true

# Check 1: Git Configuration
Write-Host "1️⃣ Checking Git Configuration..." -ForegroundColor Yellow

$autocrlf = git config core.autocrlf
$eol = git config core.eol  
$quotepath = git config core.quotepath

if ($autocrlf -eq "false") {
    Write-Host "   ✅ core.autocrlf = false" -ForegroundColor Green
} else {
    Write-Host "   ❌ core.autocrlf should be 'false', currently: '$autocrlf'" -ForegroundColor Red
    $allChecksPass = $false
}

if ($eol -eq "lf") {
    Write-Host "   ✅ core.eol = lf" -ForegroundColor Green
} else {
    Write-Host "   ❌ core.eol should be 'lf', currently: '$eol'" -ForegroundColor Red
    $allChecksPass = $false
}

if ($quotepath -eq "false") {
    Write-Host "   ✅ core.quotepath = false" -ForegroundColor Green
} else {
    Write-Host "   ❌ core.quotepath should be 'false', currently: '$quotepath'" -ForegroundColor Red
    $allChecksPass = $false
}

Write-Host ""

# Check 2: Sample Files Encoding
Write-Host "2️⃣ Checking Sample Files for Encoding Issues..." -ForegroundColor Yellow

$sampleFiles = @(
    "ELKH\Program.cs",
    "ELKH\appsettings.json",
    "ELKH\Data\DbSeeder.cs"
)

$encodingIssuesFound = $false
foreach ($file in $sampleFiles) {
    if (Test-Path $file) {
        $content = Get-Content $file -Raw -ErrorAction SilentlyContinue
        if ($content -and ($content -match "[═—''""…]")) {
            Write-Host "   ❌ Encoding issues found in: $file" -ForegroundColor Red
            $encodingIssuesFound = $true
        } else {
            Write-Host "   ✅ Clean: $file" -ForegroundColor Green
        }
    }
}

if (-not $encodingIssuesFound) {
    Write-Host "   ✅ No encoding issues detected in sample files" -ForegroundColor Green
} else {
    Write-Host "   ⚠️  Run 'fix-encoding-issues.ps1' to fix these issues" -ForegroundColor Yellow
    $allChecksPass = $false
}

Write-Host ""

# Check 3: Git Attributes
Write-Host "3️⃣ Checking Git Attributes Configuration..." -ForegroundColor Yellow

if (Test-Path ".gitattributes") {
    $gitattributes = Get-Content ".gitattributes" -Raw
    if ($gitattributes -match "working-tree-encoding=UTF-8") {
        Write-Host "   ✅ .gitattributes configured for UTF-8 encoding" -ForegroundColor Green
    } else {
        Write-Host "   ❌ .gitattributes missing UTF-8 configuration" -ForegroundColor Red
        $allChecksPass = $false
    }
} else {
    Write-Host "   ❌ .gitattributes file not found" -ForegroundColor Red
    $allChecksPass = $false
}

Write-Host ""

# Final Results
Write-Host "🏁 Validation Results:" -ForegroundColor Cyan
Write-Host "=====================" -ForegroundColor Cyan

if ($allChecksPass) {
    Write-Host "🎉 ALL CHECKS PASSED! Your environment is properly configured." -ForegroundColor Green
    Write-Host ""
    Write-Host "✨ You're ready to contribute to the ELKH project!" -ForegroundColor Green
} else {
    Write-Host "⚠️  SOME CHECKS FAILED. Please fix the issues above." -ForegroundColor Red
    Write-Host ""
    Write-Host "🔧 Quick Fixes:" -ForegroundColor Yellow
    Write-Host "   • Git Config: Run 'setup-team-git-config.ps1'" -ForegroundColor White
    Write-Host "   • Encoding Issues: Run 'fix-encoding-issues.ps1'" -ForegroundColor White
    Write-Host "   • Editor Setup: Follow 'TEAM-EDITOR-SETUP-GUIDE.md'" -ForegroundColor White
}

Write-Host ""
Write-Host "📚 Documentation Available:" -ForegroundColor Cyan
Write-Host "   • TEAM-EDITOR-SETUP-GUIDE.md (complete setup guide)" -ForegroundColor White  
Write-Host "   • QUICK-SETUP-CHECKLIST.md (fast reference)" -ForegroundColor White
Write-Host "   • encoding-issues-report.md (detailed analysis)" -ForegroundColor White