#!/usr/bin/env bash
set -euo pipefail

# Placeholder for future macOS signing / notarization.
# MVP intentionally skips Apple signing. This script is kept so the release
# pipeline has a stable integration point once signing is added.

echo "macOS signing is not enabled in MVP."
echo "Expected future inputs: APP_PATH, CODESIGN_IDENTITY, APPLE_ID, TEAM_ID, APP_PASSWORD."
