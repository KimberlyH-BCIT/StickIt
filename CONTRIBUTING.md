# Contributing to StickIt (ELKH)

Thanks for contributing. This document describes the recommended workflow, code standards, and testing requirements for changes to this Razor Pages project.

## Branching & Workflows
- Create a short-lived feature branch off `WIP-Kimberly` (or the project branch your team uses):
  - `git checkout -b feature/your-feature-name`
- Keep changes small and focused. One logical change per PR.
- Update or add tests for any behavioral change.

## Coding Standards
- Follow idiomatic C# conventions:
  - PascalCase for public types and members, camelCase for private locals/fields.
  - Single responsibility: controllers should be thin; business logic belongs in services (`ELKH/Services`).
- Navigation properties:
  - Use singular names for single entities (e.g., `Product`), plural for collections (e.g., `OrderItems`).
- Prefer dependency injection; avoid service locator patterns.
- Add XML docs for public types and methods where helpful for maintainability.

## Naming & Database
- Current models use `Pk...`/`Fk...` prefixes. Do not rename these in isolated PRs — follow the project plan for a coordinated rename on a dedicated branch.
- When adding migrations, keep them minimal and descriptive. If renaming database columns/tables, prefer a single refactor branch plus migration to preserve data.

## Tests
- Add unit tests for new logic and bug fixes.
- Tests are located in the `ELKH.Tests` project and use xUnit + EF Core InMemory.
- Run tests locally before opening a PR:

```bash
dotnet test ELKH.Tests
```

## Security & Secrets
- Never commit secrets (API keys, SMTP credentials) to source. Use environment variables or a secrets store.
- Moderation and admin routes are centralized in `ELKH.Constants.ModerationRoutes`; use these helpers to avoid open-redirect vulnerabilities.

## Formatting & Linting
- Run `dotnet build` and fix any compiler warnings that are relevant.
- Optionally run `dotnet format` to apply consistent formatting.

## Commits
- Use clear, imperative commit messages, e.g., `Fix: prevent null on order details`.
- Squash or rebase as needed so the PR history is clear.

## Documentation
- Update `README.md`, service README files, or area-specific docs for behavior changes.
- Add a short entry to the project changelog when relevant.

## Code Review
- Request at least one reviewer from the team.
- Provide a clear description of the problem, the approach taken, and any migration/upgrade steps required.

---

If you need a PR template added to `.github/PULL_REQUEST_TEMPLATE.md`, I can create one. Would you like that now?