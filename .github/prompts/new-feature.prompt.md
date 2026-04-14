---
description: "Use when adding a new feature to the C# Dependency Analyzer. Guides through requirements, implementation, testing, documentation, and git commit/push."
agent: "agent"
---

# New Feature Procedure

Follow these steps every time a new feature is added to the C# Dependency Analyzer.

---

## 1. Requirements Update

- Open [requirements.md](../../docs/requirements.md) and add or amend the relevant functional requirement(s) under the appropriate `FR-*` section.
- If the feature introduces new non-functional concerns (performance, usability, security), update the `NFR-*` section as well.
- If new CLI options, input formats, or output changes are involved, update section **5 — Input / Output Specification**.
- Commit message convention: requirements changes should be in the same commit as the feature implementation, not separately.

## 2. Design and Implementation

- Create or modify source files under `src/DependencyAnalyzer/`.
- Follow existing patterns:
  - **Models** go in `Models/`.
  - **Analysis logic** goes in `Analysis/`.
  - **Output formatting** goes in `Reporting/`.
  - **CLI wiring** goes in `Program.cs`.
- Keep changes focused. One feature per branch; do not bundle unrelated changes.
- Ensure nullable reference types remain enabled (`<Nullable>enable</Nullable>`) and resolve all warnings.
- Do not introduce `// TODO` or `// HACK` — address issues before merging.

## 3. Test Coverage

### 3.1 Add Progression Tests

Write tests that **prove the new feature works** and **would have failed before the change**. Place them in the appropriate test file:

| Scope | File |
|-------|------|
| Dependency detection for a new C# construct | `ComprehensiveDependencyTests.cs` or a new `Round*Tests.cs` |
| Graph building or type discovery | `DependencyGraphBuilderTests.cs` |
| Transitive closure / fan-in logic | `TransitiveFanInAnalyzerTests.cs` |
| Report output format | `MarkdownReportGeneratorTests.cs` |
| CLI behavior or full pipeline | `EndToEndTests.cs` |
| Roslyn compilation / target resolution | `RoslynWorkspaceBuilderTests.cs` |

If the feature spans multiple areas, add tests in each relevant file.

### 3.2 Negative / Edge-Case Tests

- Add at least one test for invalid input or boundary conditions where applicable.
- If the feature could interact with existing constructs, add a regression test combining old and new behavior.

### 3.3 Run the Full Test Suite

```bash
dotnet test --verbosity normal
```

**All tests must pass.** Do not proceed if any test fails — fix the failure first. A partial green suite is not acceptable.

## 4. Sample Codebase Update

If the feature changes dependency detection or reporting:

- Add or update `.cs` files in `samples/SampleCodebase/` to exercise the new behavior.
- Update `samples/SampleCodebase/filelist.txt` if new files were added.
- Update `samples/SampleCodebase/README.md` (expected results table) to reflect any changes in fan-in output.
- Re-run the tool on the sample and verify the report matches expectations:
  ```bash
  dotnet run --project src/DependencyAnalyzer -- \
    --target "SampleApp.Core.OrderService" \
    --files  samples/SampleCodebase/filelist.txt \
    --output samples/SampleCodebase/report.md
  ```

## 5. Documentation Update

### 5.1 README.md

Update the root [README.md](../../README.md) to reflect the new feature:

- **Usage section** — if new CLI options or workflows were added.
- **Supported C# Constructs** — if new language constructs are now detected.
- **Dependencies** — if new NuGet packages were introduced.
- **Project Structure** — if new files or directories were created.
- **Any other section** that the feature affects.

### 5.2 requirements.md

Already handled in step 1. Double-check that the requirement text is consistent with the actual implementation.

### 5.3 test-report.md

Update [docs/test-report.md](../../docs/test-report.md) to reflect the new or changed tests:

- **Section 2 (Summary by Test File)** — update test counts for affected files; add a new row if a new test file was created.
- **Section 3 (Test Inventory)** — add entries for every new test with ID, method name, description, and level. Follow the existing ID prefix convention for the file (e.g. `CD-*`, `GP-*`, `FA-*`).
- **Section 4 (Requirement Traceability Matrix)** — add the new test IDs to the corresponding requirement row(s).
- **Section 5 (Coverage by C# Construct Category)** — add new test IDs to the appropriate category row, or add a new category if needed.
- **Header metadata** — update `Total test cases` and `Last updated` in the document header.
- **Section 6 (Latest Run)** — update the total count to match the current `dotnet test` output.

## 6. Build Verification

Run a clean build to ensure there are no warnings or errors:

```bash
dotnet build --no-incremental -warnaserror
```

If warnings cannot be suppressed cleanly, document the reason.

## 7. Code Review Checklist

Before committing, verify:

- [ ] No hardcoded file paths or environment-specific values.
- [ ] No secrets, credentials, or API keys in source code.
- [ ] New code follows the existing naming conventions and file organization.
- [ ] Nullable annotations are correct — no `!` (null-forgiving) operator without justification.
- [ ] No unused `using` directives introduced.
- [ ] All public types and members that form the API surface have clear, self-documenting names.
- [ ] Error messages are actionable (tell the user what went wrong and what to do).
- [ ] Exit codes are used consistently (0 = success, 1 = user error, 2 = internal error).

## 8. Commit and Publish

Once all checks pass:

```bash
# Stage all changes
git add -A

# Commit with a descriptive message
git commit -m "feat: <short description of the feature>

- <bullet summarizing what was added/changed>
- <bullet summarizing test additions>
- Updated requirements.md and README.md"

# Push to the remote
git push origin HEAD
```

### Commit Message Guidelines

- Use [Conventional Commits](https://www.conventionalcommits.org/) format: `feat:`, `fix:`, `refactor:`, `test:`, `docs:`.
- First line: imperative mood, ≤72 characters (e.g. `feat: detect function pointer type references`).
- Body: explain *what* and *why*, not *how*. List notable changes as bullets.
- Reference related issues if applicable: `Closes #42`.

---

## Quick Reference Checklist

```
[ ] 1. Requirements updated in docs/requirements.md
[ ] 2. Feature implemented in src/DependencyAnalyzer/
[ ] 3. Progression tests added and all 145+ tests pass
[ ] 4. Sample codebase updated (if applicable)
[ ] 5. README.md updated
[ ] 6. Clean build with no warnings
[ ] 7. Code review checklist passed
[ ] 8. Committed and pushed to remote
```
