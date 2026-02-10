# Semantic Release Setup

This document explains how ChainSharp uses semantic-release for automatic versioning and NuGet package releases.

## Overview

ChainSharp uses **[semantic-release](https://github.com/semantic-release/semantic-release)** to automatically:
- Determine the next version based on commit messages
- Generate CHANGELOG entries
- Update the version in [Directory.Build.props](../Directory.Build.props)
- Create GitHub releases
- Push packages to NuGet

This eliminates manual versioning and ensures consistency with [Semantic Versioning](https://semver.org/).

## How It Works

### 1. Commit Message Format

ChainSharp uses [Conventional Commits](https://www.conventionalcommits.org/):

```
type(scope): description

[optional body]
[optional footer]
```

**Types that trigger releases:**
- `feat:` - New feature → **Minor** version bump (e.g., 5.1.0 → 5.2.0)
- `fix:` - Bug fix → **Patch** version bump (e.g., 5.1.0 → 5.1.1)
- `perf:` - Performance improvement → **Patch** version bump
- `refactor:` - Code refactoring → **Patch** version bump

**Types that don't trigger releases:**
- `docs:` - Documentation changes
- `test:` - Test changes
- `chore:` - Maintenance (use for non-user-facing changes)
- `ci:` - CI/CD changes

**Breaking changes:** Add `BREAKING CHANGE:` in the footer to trigger a **Major** version bump:

```
feat(scheduler): rewrite job execution engine

BREAKING CHANGE: JobConfiguration API has changed
```

### 2. Automatic Release Process

When you push to `main`:

1. **GitHub Actions** runs the release workflow (`nuget_release.yml`)
2. **semantic-release** analyzes commits since last release
3. **If changes detected**, semantic-release:
   - Determines the next version
   - Updates `Directory.Build.props`
   - Generates/updates `CHANGELOG.md`
   - Creates a GitHub release
   - Commits changes back to `main` (marked with `[skip ci]`)
4. **GitHub Actions** continues:
   - Builds the project with new version
   - Packs NuGet packages
   - Pushes to NuGet.org (requires `NUGET_API_KEY` secret)

### 3. Configuration

The release process is configured in [.releaserc.json](../.releaserc.json):

- **Branches**: Only `main` triggers releases
- **Plugins**:
  - `@semantic-release/commit-analyzer` - Analyzes commits
  - `@semantic-release/release-notes-generator` - Generates changelog
  - `@semantic-release/changelog` - Updates CHANGELOG.md
  - `@semantic-release/exec` - Updates Directory.Build.props
  - `@semantic-release/git` - Commits version updates
  - `@semantic-release/github` - Creates GitHub releases

## Prerequisites

### GitHub Secrets

Configure these secrets in your GitHub repository settings:

1. **`NUGET_API_KEY`** (Required)
   - Your NuGet.org API key
   - Get it at https://www.nuget.org/account/apikeys

The `GITHUB_TOKEN` is automatically provided by GitHub Actions.

### Directory.Build.props

The version is stored in [Directory.Build.props](../Directory.Build.props):

```xml
<Project>
    <PropertyGroup>
        <Version>5.1.0</Version>
    </PropertyGroup>
</Project>
```

semantic-release automatically updates this version using `sed` during the release process.

## Examples

### Example 1: Bug Fix (Patch Release)

```bash
git commit -m "fix(effect): handle null effects in workflow"
# Version: 5.1.0 → 5.1.1
```

### Example 2: New Feature (Minor Release)

```bash
git commit -m "feat(scheduler): add cron expression support"
# Version: 5.1.0 → 5.2.0
```

### Example 3: Breaking Change (Major Release)

```bash
git commit -m "feat(effect): redesign effect metadata API

BREAKING CHANGE: EffectMetadata structure has changed"
# Version: 5.1.0 → 6.0.0
```

### Example 4: Multiple Changes (Combined Release)

If you push multiple commits:

```
feat(mediator): add workflow discovery caching
fix(effect): fix memory leak in effect tracking
docs: update architecture guide
```

Result:
- New feature (`feat`) + bug fix (`fix`) = **Minor** version bump (feature takes precedence)
- Documentation change ignored
- Version: 5.1.0 → 5.2.0

## Release Artifacts

After a successful release, you'll find:

1. **GitHub Release**: https://github.com/Theauxm/ChainSharp/releases
   - Includes changelog entries
   - Lists all affected packages

2. **NuGet Packages**:
   - Theauxm.ChainSharp
   - Theauxm.ChainSharp.Effect
   - Theauxm.ChainSharp.Effect.*
   - All published at https://www.nuget.org/packages?q=Theauxm.ChainSharp

3. **CHANGELOG.md**: Updated automatically with release notes

4. **Commit**: Version update committed to `main` (by the release workflow)

## CI/CD Behavior

### Pull Requests

- Run tests and code quality checks
- **No release** triggered
- Uses temporary version for builds

### Push to Main

1. Run tests (must pass)
2. If tests pass and commits indicate a version change, create release
3. If no version change detected, nothing happens (no empty release)

## Troubleshooting

### No Release Created

**Possible causes:**
1. Commit messages don't follow Conventional Commits format
2. Push is to a branch other than `main`
3. Previous commit was already released

**Check:**
```bash
# View recent commits
git log --oneline -10

# Ensure format: type(scope): description
```

### NuGet Publish Failed

**Causes:**
- `NUGET_API_KEY` secret not configured
- API key is expired or invalid
- Package version already exists on NuGet

**Fix:**
1. Verify secret in GitHub repo settings
2. Check NuGet.org for conflicting version
3. Review workflow logs for details

### Version Not Updated in Repo

The version is updated in:
- `Directory.Build.props` - Updated by semantic-release
- `CHANGELOG.md` - Updated by semantic-release
- GitHub release - Created by semantic-release

Check the release workflow run for errors.

## Related Files

- [.releaserc.json](../.releaserc.json) - Release configuration
- [.github/workflows/nuget_release.yml](../github/workflows/nuget_release.yml) - Release workflow
- [CHANGELOG.md](../CHANGELOG.md) - Generated changelog
- [Directory.Build.props](../Directory.Build.props) - Version source
- [package.json](../package.json) - Node dependencies (semantic-release)

## Further Reading

- [Semantic Release Documentation](https://github.com/semantic-release/semantic-release)
- [Conventional Commits Specification](https://www.conventionalcommits.org/)
- [Semantic Versioning](https://semver.org/)
- [NuGet API Keys](https://docs.microsoft.com/nuget/api/package-publish-resource)
