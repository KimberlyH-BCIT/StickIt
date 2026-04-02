# ELKH Project - Team Editor Setup Guide
*Encoding Standards and Best Practices for Development Team*

## 🎯 Overview

This guide ensures all team members configure their development environments consistently to prevent encoding issues in the ELKH .NET 10 Razor Pages project. Following these guidelines will eliminate the Unicode character problems we recently resolved.

## 🚨 Critical Requirements

All team members MUST configure their editors with these settings:

1. **UTF-8 Encoding** (without BOM for source files)
2. **Consistent Line Endings** (LF for cross-platform compatibility)
3. **Visible Unicode Characters** (to detect encoding issues)
4. **Smart Quote Prevention** (auto-replace with ASCII quotes)

---

## 🛠️ Editor Configuration

### Microsoft Visual Studio 2026 (Recommended)

#### Encoding Settings:
1. **Tools** → **Options** → **Environment** → **Documents**
   - Check: "Save documents as Unicode (UTF-8) without signature"
   - Uncheck: "Auto-detect UTF-8 encoding without signature"

2. **Advanced Save Options** (per file):
   - **File** → **Advanced Save Options**
   - **Encoding**: Unicode (UTF-8 without signature) - Codepage 65001
   - **Line Endings**: Unix (LF)

#### Smart Quotes Prevention:
3. **Tools** → **Options** → **Text Editor** → **All Languages**
   - Check: "Show visible white space"
   - Under **Tabs**: Set to "Insert spaces" (not tabs)

4. **Tools** → **Options** → **Text Editor** → **C#** → **Advanced**
   - Check: "Show invisible characters"

#### Auto-Formatting (Critical):
5. **Tools** → **Options** → **Text Editor** → **C#** → **Code Style**
   - Configure to replace smart quotes automatically
   - Set quotation preferences to use straight quotes (")

### Visual Studio Code

#### Settings.json Configuration:
```json
{
  "files.encoding": "utf8",
  "files.eol": "\n",
  "files.insertFinalNewline": true,
  "files.trimTrailingWhitespace": true,
  "editor.insertSpaces": true,
  "editor.detectIndentation": false,
  "editor.renderWhitespace": "boundary",
  "editor.renderControlCharacters": true,
  
  // C# specific
  "[csharp]": {
    "editor.tabSize": 4,
    "editor.insertSpaces": true,
    "files.encoding": "utf8"
  },
  
  // Razor Pages specific
  "[razor]": {
    "editor.tabSize": 4,
    "editor.insertSpaces": true,
    "files.encoding": "utf8"
  },
  
  // Web files
  "[html]": {
    "files.encoding": "utf8"
  },
  "[css]": {
    "files.encoding": "utf8"
  },
  "[javascript]": {
    "files.encoding": "utf8"
  },
  "[json]": {
    "files.encoding": "utf8"
  }
}
```

#### Required Extensions:
- **C# Dev Kit** (Microsoft)
- **Auto Rename Tag** (prevent encoding in HTML)
- **Prettier** (configure for consistent formatting)

### JetBrains Rider

#### File Encodings:
1. **File** → **Settings** → **Editor** → **File Encodings**
   - **Global Encoding**: UTF-8
   - **Project Encoding**: UTF-8
   - **Default encoding for properties files**: UTF-8
   - Uncheck: "Transparent native-to-ascii conversion"

2. **File** → **Settings** → **Editor** → **Code Style**
   - **Line separator**: Unix and OS X (\n)
   - **Right margin**: 120 columns

### Notepad++ (Emergency Editing)

#### Configuration:
1. **Settings** → **Preferences** → **New Document**
   - **Encoding**: UTF-8 (without BOM)
   - **Format**: Unix (LF)

2. **View** → **Show Symbol**
   - Enable: "Show White Space and TAB"
   - Enable: "Show End of Line"

---

## 🔧 Git Configuration

### Local Git Settings (Run these commands):

```bash
# Set line ending preferences
git config core.autocrlf false
git config core.eol lf

# Set UTF-8 as default
git config core.quotepath false

# Ensure proper handling of Unicode
git config core.precomposeUnicode true
```

### Verify Git Configuration:
```bash
git config --list | grep -E "(eol|autocrlf|encoding|unicode)"
```

---

## 🚫 Characters to AVOID

### Never Use These Characters in Source Code:

| Character | Name | Unicode | Use Instead |
|-----------|------|---------|-------------|
| ═ | Box Drawing Double Horizontal | U+2550 | = (equals) |
| — | Em Dash | U+2014 | - (hyphen) |
| ' | Left Single Quote | U+2018 | ' (apostrophe) |
| ' | Right Single Quote | U+2019 | ' (apostrophe) |
| " | Left Double Quote | U+201C | " (quote) |
| " | Right Double Quote | U+201D | " (quote) |
| … | Horizontal Ellipsis | U+2026 | ... (three periods) |

### Detection Commands:
Use this PowerShell to check for issues in your files:
```powershell
# Check for Unicode characters in a file
Get-Content "YourFile.cs" | Select-String -Pattern "[═—''""…]"
```

---

## ✅ Validation Checklist

Before committing code, verify:

- [ ] **Encoding**: File saved as UTF-8 without BOM
- [ ] **Line Endings**: LF (\n) line endings
- [ ] **Characters**: No smart quotes or Unicode drawing characters
- [ ] **Whitespace**: No trailing spaces, consistent indentation
- [ ] **Build**: Code compiles without warnings

### Quick File Check (PowerShell):
```powershell
# Verify file encoding
file.exe "YourFile.cs"  # Should show "UTF-8 Unicode text"

# Check for problematic characters
Get-Content "YourFile.cs" -Raw | ForEach-Object { 
    if ($_ -match "[═—''""…]") { 
        Write-Host "⚠️ Unicode characters detected in file!" 
    } else { 
        Write-Host "✅ File encoding looks good" 
    } 
}
```

---

## 🛡️ Prevention Tools

### Automated Fixes Available:

1. **Project-wide Fix**: Run `fix-encoding-issues.ps1`
2. **Git Hooks**: Consider adding pre-commit hooks to prevent issues
3. **CI/CD Integration**: Add encoding validation to build pipeline

### Team Communication:

- **Slack/Teams Message**: Share this guide in team channel
- **Code Reviews**: Check for encoding issues during reviews
- **Onboarding**: Add to new team member checklist

---

## 📞 Support

### If You Encounter Issues:

1. **Immediate Fix**: Run `fix-encoding-issues.ps1` in project root
2. **Verification**: Check `encoding-issues-report.md` for details
3. **Team Support**: Ask in development channel
4. **Escalation**: Contact project maintainer

### Resources:

- **Project Documentation**: `encoding-issues-report.md`
- **Git Attributes**: `.gitattributes` (configured for UTF-8)
- **Fix Script**: `fix-encoding-issues.ps1`

---

## 🎯 Success Metrics

When properly configured, you should see:

- ✅ No "Chinese characters" in source files
- ✅ Consistent file encoding across team members  
- ✅ No merge conflicts due to encoding differences
- ✅ Proper display of files across different editors
- ✅ Successful builds on all platforms (Windows/Mac/Linux)

---

*Last Updated: March 2026*  
*ELKH .NET 10 Razor Pages Project*