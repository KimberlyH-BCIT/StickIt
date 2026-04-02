# ELKH Team Git Configuration Script
# Automatically configures Git settings for proper encoding handling
# Run this script once when setting up your development environment

Write-Host "🔧 Configuring Git for ELKH Project Encoding Standards..." -ForegroundColor Green
Write-Host ""

# Set proper line ending handling
Write-Host "Setting line ending configuration..." -ForegroundColor Yellow
git config core.autocrlf false
git config core.eol lf
Write-Host "✅ Line endings set to LF (Unix style)" -ForegroundColor Green

# Set UTF-8 encoding preferences  
Write-Host "Setting UTF-8 encoding configuration..." -ForegroundColor Yellow
git config core.quotepath false
git config core.precomposeUnicode true
Write-Host "✅ UTF-8 encoding configured" -ForegroundColor Green

# Set editor preferences for commit messages
Write-Host "Setting commit message editor..." -ForegroundColor Yellow
if (Get-Command "code" -ErrorAction SilentlyContinue) {
    git config core.editor "code --wait"
    Write-Host "✅ VS Code set as default Git editor" -ForegroundColor Green
} else {
    Write-Host "⚠️  VS Code not found, keeping default editor" -ForegroundColor Yellow
}

# Configure merge tool (if available)
if (Get-Command "code" -ErrorAction SilentlyContinue) {
    Write-Host "Setting VS Code as merge tool..." -ForegroundColor Yellow
    git config merge.tool vscode
    git config mergetool.vscode.cmd 'code --wait $MERGED'
    Write-Host "✅ VS Code configured as merge tool" -ForegroundColor Green
}

Write-Host ""
Write-Host "🎉 Git configuration complete!" -ForegroundColor Green
Write-Host ""

# Display current configuration
Write-Host "📋 Current Git Configuration:" -ForegroundColor Cyan
Write-Host "----------------------------------------"
git config --list | Where-Object { 
    $_ -match "(core\.autocrlf|core\.eol|core\.quotepath|core\.precomposeUnicode|core\.editor|merge\.tool)" 
} | ForEach-Object {
    Write-Host "  $($_)" -ForegroundColor White
}

Write-Host ""
Write-Host "🚀 Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Configure your editor using TEAM-EDITOR-SETUP-GUIDE.md"
Write-Host "  2. Run fix-encoding-issues.ps1 if you have existing files"
Write-Host "  3. Review the QUICK-SETUP-CHECKLIST.md for daily practices"
Write-Host ""
Write-Host "✨ You're all set for proper encoding handling!" -ForegroundColor Green