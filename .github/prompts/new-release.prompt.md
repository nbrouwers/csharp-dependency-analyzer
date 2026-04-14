---
description: "Create a new versioned release of the C# Dependency Analyzer"
---

# New Release Procedure

Follow these steps to create a new versioned release of the C# Dependency Analyzer.

---

## 1. Determine the Version Bump

Apply [Semantic Versioning 2.0.0](https://semver.org/) rules to the changes since the last release:

| Change Type | Version Bump | Example |
|---|---|---|
| Breaking change to CLI, report format, or public API | **MAJOR** | `1.0.0` → `2.0.0` |
| New feature, new CLI option, new dependency construct detected | **MINOR** | `1.0.0` → `1.1.0` |
| Bug fix, performance improvement, documentation-only change | **PATCH** | `1.0.0` → `1.0.1` |

If in doubt, prefer MINOR over PATCH for user-visible changes.

## 2. Pre-Release Checks

### 2.1 All Tests Pass

```bash
dotnet test --verbosity normal
```

Do not proceed unless **all tests pass** with zero failures.

### 2.2 Clean Build

```bash
dotnet build --no-incremental
```

No errors or unexpected warnings.

### 2.3 Working Directory Clean

```bash
git status
```

Ensure there are no uncommitted changes. All work for this release must already be committed.

### 2.4 Main Branch Up-to-Date

```bash
git checkout main
git pull origin main
```

## 3. Bump the Version

Update the `<Version>` element in [`src/DependencyAnalyzer/DependencyAnalyzer.csproj`](../../src/DependencyAnalyzer/DependencyAnalyzer.csproj):

```xml
<Version>X.Y.Z</Version>
```

Replace `X.Y.Z` with the new version determined in step 1.

## 4. Update the Changelog

If a `CHANGELOG.md` does not yet exist in the repository root, create one following [Keep a Changelog](https://keepachangelog.com/) format.

Add a new section at the top of the changelog:

```markdown
## [X.Y.Z] — YYYY-MM-DD

### Added
- <new features>

### Changed
- <changed behavior>

### Fixed
- <bug fixes>

### Removed
- <removed features>
```

Guidelines:
- Use past tense: "Added", "Fixed", not "Add", "Fix".
- Group entries by type (`Added`, `Changed`, `Fixed`, `Removed`).
- Omit empty sections.
- Reference issue/PR numbers where applicable: `(#42)`.
- Write entries from the user's perspective, not implementation details.

## 5. Commit the Version Bump

```bash
git add -A
git commit -m "release: vX.Y.Z

- Bumped version to X.Y.Z
- Updated CHANGELOG.md"
```

## 6. Tag the Release

Create an annotated Git tag:

```bash
git tag -a vX.Y.Z -m "Release vX.Y.Z"
```

Tag naming convention: always prefix with `v` (e.g., `v1.0.0`, `v1.1.0`).

## 7. Push to Remote

```bash
git push origin main
git push origin vX.Y.Z
```

## 8. GitHub Release (Automated)

Pushing a `v*` tag in step 7 automatically triggers the CI workflow (`.github/workflows/ci.yml`), which will:

1. Run all tests.
2. Build and publish the self-contained portable executable.
3. Create a GitHub Release for the tag with auto-generated release notes.
4. Attach `DependencyAnalyzer.exe` as a binary asset.

**No manual action is needed.** Wait for the CI pipeline to complete.

To check the pipeline status:

```bash
gh run list --limit 1
```

### 8.1 Override Release Notes (Optional)

If you want to replace the auto-generated notes with the changelog section:

```bash
gh release edit vX.Y.Z --notes-file CHANGELOG_SECTION.md
```

## 9. Post-Release Verification

### 9.1 Verify the Release

```bash
gh release view vX.Y.Z
```

Confirm:
- Tag is correct.
- Release notes are present and well-formatted.
- Binary asset is attached (if applicable).

### 9.2 Verify the Published Artifact

Download the portable executable from the release and run:

```bash
./DependencyAnalyzer.exe --version
```

Confirm it prints the correct version `X.Y.Z`.

### 9.3 Run Smoke Test on Sample

```bash
./DependencyAnalyzer.exe \
  --target "SampleApp.Core.OrderService" \
  --files  samples/SampleCodebase/filelist.txt \
  --output smoke-test-report.md
```

Verify the report is generated and the version line in the report matches `vX.Y.Z`.

---

## Quick Reference Checklist

```
[ ] 1. Version bump type determined (MAJOR / MINOR / PATCH)
[ ] 2. All tests pass, clean build, working directory clean
[ ] 3. Version bumped in DependencyAnalyzer.csproj
[ ] 4. CHANGELOG.md updated
[ ] 5. Version bump committed
[ ] 6. Git tag created (vX.Y.Z)
[ ] 7. Pushed to remote (branch + tag) — CI creates release automatically
[ ] 8. CI pipeline completed — release with binary verified
[ ] 9. Post-release verification passed
```
