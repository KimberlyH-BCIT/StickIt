# 🚨 ELKH Team - Quick Setup Checklist
*Essential encoding configuration to prevent Unicode issues*

## ⚡ Immediate Actions Required

### 1. Visual Studio Settings
```
Tools → Options → Environment → Documents
✅ Check: "Save documents as Unicode (UTF-8) without signature"
✅ Uncheck: "Auto-detect UTF-8 encoding without signature"
```

### 2. Git Configuration (Run in terminal)
```bash
git config core.autocrlf false
git config core.eol lf
git config core.quotepath false
```

### 3. Never Use These Characters
- ═ (box drawing) → use =
- — (em dash) → use -  
- ' ' (smart quotes) → use '
- " " (smart quotes) → use "
- … (ellipsis) → use ...

### 4. Before Every Commit
```powershell
# Quick encoding check
Get-Content "YourFile.cs" | Select-String -Pattern "[═—''""…]"
```

### 🆘 Need Help?
- Run: `fix-encoding-issues.ps1` (fixes all files automatically)
- Read: `TEAM-EDITOR-SETUP-GUIDE.md` (complete instructions)
- Check: `encoding-issues-report.md` (detailed analysis)

---
*Share this in team channels! 📤*