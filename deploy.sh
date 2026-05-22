#!/bin/sh -ex

# Unity UI Core - UPM Deployment Script
# Creates a clean UPM branch with only the package contents via git subtree split.
# Usage: ./deploy.sh --semver "0.2.0"

# Parse arguments
while [ $# -gt 0 ]; do
  case "$1" in
    --semver=*)
      SEMVER="${1#*=}"
      ;;
    --semver)
      SEMVER="$2"
      shift
      ;;
    *)
      echo "Usage: ./deploy.sh --semver \"0.2.0\""
      exit 1
      ;;
  esac
  shift
done

# Validate semver argument
if [ -z "$SEMVER" ]; then
  echo "Error: --semver argument is required"
  echo "Usage: ./deploy.sh --semver \"0.2.0\""
  exit 1
fi

# Configuration
PREFIX="Packages/com.dreamtech.uicore"
BRANCH="upm"

echo "================================"
echo "Deploying Unity UI Core"
echo "Version: $SEMVER"
echo "Prefix:  $PREFIX"
echo "Branch:  $BRANCH"
echo "================================"

# Step 1: Split the package folder into a separate branch
echo "Step 1/5: Splitting package from main branch..."
git subtree split --prefix="$PREFIX" --branch $BRANCH

# Step 2: Tag the version on the UPM branch
echo "Step 2/5: Creating tag $SEMVER..."
git tag $SEMVER $BRANCH

# Step 3: Push the UPM branch and tags to remote
echo "Step 3/5: Pushing to origin..."
git push origin $BRANCH --tags

# Step 4: Clean up remote branch (keeps tags)
echo "Step 4/5: Cleaning up remote branch..."
git push origin --delete $BRANCH || true

# Step 5: Clean up local branch
echo "Step 5/5: Cleaning up local branch..."
git branch -D $BRANCH

echo "================================"
echo "✅ Deployment Complete!"
echo ""
echo "Installation URL for users:"
echo "https://github.com/TeamAcMong/unity-ui-core.git#$SEMVER"
echo ""
echo "Or in manifest.json:"
echo "{"
echo "  \"dependencies\": {"
echo "    \"com.dreamtech.uicore\": \"https://github.com/TeamAcMong/unity-ui-core.git#$SEMVER\""
echo "  }"
echo "}"
echo "================================"
