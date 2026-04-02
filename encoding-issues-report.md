# ELKH Project Encoding Issues Report
Generated: $(Get-Date)

## Summary
The ELKH project has widespread encoding issues affecting source files. While the application functions correctly, these characters may cause problems with:
- Source control (Git)
- Text editors that don't handle UTF-8 properly
- Build systems on different platforms
- Team collaboration with different locale settings

## Character Issues Found

### 1. Box Drawing Characters (═) - Unicode 9552
**Impact**: HIGH - Found in documentation headers throughout the codebase
**Files Affected**: 35+ files including Program.cs and view files
**Typical Usage**: Used in comment headers like:
```
// ═══════════════════════════════════════
// SECTION HEADER
// ═══════════════════════════════════════
```

### 2. Em Dashes (—) - Unicode 8212
**Impact**: MEDIUM - Found in comments and documentation
**Files Affected**: 20+ files
**Typical Usage**: Used instead of regular hyphens in comments:
```
// Settings — configuration values
```

### 3. Smart Quotes (' ' " ") - Unicode 8216-8221
**Impact**: HIGH - Found throughout view files and configuration
**Files Affected**: ALL view files, configuration files
**Typical Usage**: Used instead of regular quotes:
```
"Don't use smart quotes" (should be "Don't use smart quotes")
```

### 4. Ellipsis (…) - Unicode 8230
**Impact**: LOW - Limited occurrences
**Files Affected**: Few view files
**Typical Usage**: Used in truncated text displays

## Files Requiring Fixes

### Critical Files (Source Code)
- `ELKH\Program.cs` - Box drawing characters in documentation
- `ELKH\appsettings.json` - Em dashes in documentation comments
- All files in `ELKH\Data\` - Database seeding files with multiple issues

### View Files (All affected)
- All `.cshtml` files in `ELKH\Views\` directory
- Layout files, partials, and page-specific views

### Assets
- `ELKH\wwwroot\css\kawaii-theme.css` - Documentation comments
- `ELKH\wwwroot\css\swagger-custom.css` - Documentation comments
- `ELKH\wwwroot\js\*.js` files - Comments and strings

## Non-Issues (Intentional)
- Unicode emoji characters (🎉, 🔥, 🥇, etc.) - These are intentional UI elements
- Third-party library files (Bootstrap, jQuery) - Should not be modified

## Recommended Actions

### Immediate Fixes Needed
1. Replace box drawing characters (═) with regular equals signs (=)
2. Replace em dashes (—) with regular hyphens (-)
3. Replace smart quotes with regular ASCII quotes
4. Replace ellipsis (…) with three periods (...)

### File Encoding
- Ensure all source files are saved as UTF-8 without BOM
- Configure editors to use UTF-8 encoding consistently
- Set up Git attributes for consistent line endings

## Impact Assessment
- **Build Process**: No impact - application builds and runs correctly
- **Runtime**: No impact - functionality not affected
- **Development**: May cause issues with some editors/tools
- **Team Collaboration**: May cause merge conflicts or display issues
- **Cross-Platform**: May cause issues on non-Windows systems