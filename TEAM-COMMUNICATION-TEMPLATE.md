📧 TEAM COMMUNICATION TEMPLATE
==============================

Subject: 🚨 URGENT: Editor Setup Required - Encoding Standards for ELKH Project

Hi Team,

We've recently resolved encoding issues that were causing "Chinese characters" to appear in our source files. To prevent this from happening again, everyone needs to configure their development environment properly.

🎯 IMMEDIATE ACTION REQUIRED:

1️⃣ CONFIGURE GIT (2 minutes):
   • Open PowerShell/Terminal in project root
   • Run: `.\setup-team-git-config.ps1`
   • This automatically sets up proper Git encoding handling

2️⃣ CONFIGURE YOUR EDITOR:
   • Follow: `TEAM-EDITOR-SETUP-GUIDE.md` (detailed instructions)
   • Or use: `QUICK-SETUP-CHECKLIST.md` (fast setup)

3️⃣ FIX EXISTING FILES (if needed):
   • Run: `.\fix-encoding-issues.ps1` in project root
   • This automatically fixes any encoding issues in your local files

🚫 CRITICAL - NEVER USE THESE CHARACTERS:
   • ═ (box drawing) → use = instead
   • — (em dash) → use - instead  
   • ' ' (smart quotes) → use ' instead
   • " " (smart quotes) → use " instead

✅ VERIFICATION:
   Before committing, run this check:
   `Get-Content "YourFile.cs" | Select-String -Pattern "[═—''""…]"`

📞 NEED HELP?
   • Check the documentation files created in project root
   • Ask in this channel if you encounter issues
   • All files include step-by-step instructions

This is critical for our code quality and team collaboration. Please complete this setup by [INSERT DEADLINE].

Thanks for your cooperation!

Kimberly/Velyene

---

📎 ATTACHMENTS TO SHARE:
- TEAM-EDITOR-SETUP-GUIDE.md
- QUICK-SETUP-CHECKLIST.md  
- setup-team-git-config.ps1
- fix-encoding-issues.ps1

🔗 LINKS TO MENTION:
- Project Repository: [INSERT REPO URL]
- Encoding Issues Report: encoding-issues-report.md