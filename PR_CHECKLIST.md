PR Checklist

Before requesting a review, ensure the following items are complete:

- [ ] Branch created from `WIP-Kimberly` (or designated base branch)
- [ ] PR title and description clearly explain the change and reference related issue(s)
- [ ] `dotnet build` completes successfully with no errors
- [ ] `dotnet test ELKH.Tests` passes locally
- [ ] New code includes unit tests for expected behavior and edge cases
- [ ] Existing tests updated if behavior changed
- [ ] No hard-coded secrets or credentials committed
- [ ] Migrations included (if database schema changed) and described in the PR
- [ ] Relevant documentation updated (`README.md`, service READMEs, or docs folder)
- [ ] Static analysis/formatting applied (optional: `dotnet format`)
- [ ] PR size is reasonable — consider splitting large changes into smaller PRs
- [ ] Assign at least one reviewer and add a label (e.g., `area:moderation`, `area:orders`)

Post-merge:

- [ ] Confirm CI has green build and tests
- [ ] If migration was added, run it on staging and document any manual steps

Notes:
- For large refactors that touch models and migrations, target a dedicated branch and include a single migration mapping old to new schema to preserve data.
- For security-related changes, add a short description of the threat model and mitigation in the PR description.
