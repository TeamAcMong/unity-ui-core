# Git Subtree Deployment Guide — Unity UI Core

## Why Git Subtree?

| Method | Install Size | Speed | Use Case |
|--------|--------------|-------|----------|
| Basic Git URL | Entire Unity project (~MBs) | Slow | Prototype |
| **Git Subtree (this method)** | Package files only (~KBs) | Fast | Production |

Consumers only download `Packages/com.dreamtech.uicore/` content via tagged subtree branch — 95% smaller, faster installs.

## Pre-deploy Checklist

- [ ] All code changes committed to `main`
- [ ] Version bumped in `Packages/com.dreamtech.uicore/package.json`
- [ ] `CHANGELOG.md` updated with release notes
- [ ] No compile errors (test in Unity)
- [ ] `main` pushed to GitHub

## Deploy Process

### Step 1 — Bump version

`Packages/com.dreamtech.uicore/package.json`:
```json
{
  "version": "0.3.0"
}
```

### Step 2 — Update CHANGELOG

```markdown
## [0.3.0] - 2026-XX-XX

### Added
- New feature X

### Fixed
- Bug Y
```

### Step 3 — Commit & push

```bash
git add .
git commit -m "Release v0.3.0"
git push origin main
```

### Step 4 — Run deploy script

```bash
./deploy.sh --semver "0.3.0"
```

**What it does:**
1. `git subtree split --prefix="Packages/com.dreamtech.uicore" --branch upm` — extract subfolder as separate branch
2. `git tag 0.3.0 upm` — tag the version
3. `git push origin upm --tags` — publish tag (tag captures package-only commit tree)
4. Delete temporary `upm` branch (tag stays forever)

### Step 5 — Verify

Visit `https://github.com/TeamAcMong/unity-ui-core/tags` — should see new tag `0.3.0`. Click tag → repo browser shows only package contents at root.

## Install URL (for users)

```
https://github.com/TeamAcMong/unity-ui-core.git#0.3.0
```

Or latest tag:
```
https://github.com/TeamAcMong/unity-ui-core.git
```

## Troubleshooting

### "tag already exists"
```bash
git tag -d 0.3.0                          # delete local
git push origin :refs/tags/0.3.0          # delete remote
./deploy.sh --semver "0.3.0"              # redeploy
```

### "refusing to update checked out branch"
You're on `upm` branch — switch to main: `git checkout main`, then retry.

### Unity can't find package
- Verify tag exists: `git ls-remote --tags origin`
- URL format: `https://github.com/TeamAcMong/unity-ui-core.git#X.Y.Z` (note `#` before version)

## Versioning

Follow [Semantic Versioning](https://semver.org/):
- **MAJOR** (1.0.0) — breaking API changes
- **MINOR** (0.X.0) — new features, backward compatible
- **PATCH** (0.0.X) — bug fixes only

## Manual Steps (Educational)

If you want to skip `deploy.sh`:

```bash
git subtree split --prefix="Packages/com.dreamtech.uicore" --branch upm
git tag 0.3.0 upm
git push origin upm --tags
git push origin --delete upm
git branch -D upm
```

## DON'Ts

- ❌ Don't reuse version numbers — once tagged, treat as immutable
- ❌ Don't change `package.json` name after first release — breaks all existing installs
- ❌ Don't manually edit the `upm` branch — overwritten on next deploy
- ❌ Don't delete tags after publishing — breaks consumers
