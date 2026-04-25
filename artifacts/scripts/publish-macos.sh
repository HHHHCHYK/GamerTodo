#!/usr/bin/env bash
set -euo pipefail

CONFIGURATION="${1:-Release}"
RUNTIME="${2:-osx-arm64}"
VERSION="${3:-0.1.0}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CLIENT_PROJECT="$ROOT/src/HeyeTodo.Client/HeyeTodo.Client.csproj"
OUTPUT_ROOT="$ROOT/artifacts/releases"
PUBLISH_DIR="$OUTPUT_ROOT/client-$RUNTIME"
APP_DIR="$OUTPUT_ROOT/HeyeTodo.app"
APP_CONTENTS="$APP_DIR/Contents"
APP_MACOS="$APP_CONTENTS/MacOS"
APP_RESOURCES="$APP_CONTENTS/Resources"
DMG_STAGING="$OUTPUT_ROOT/dmg-root"
DMG_PATH="$OUTPUT_ROOT/HeyeTodo-$RUNTIME-$VERSION.dmg"

mkdir -p "$PUBLISH_DIR" "$APP_MACOS" "$APP_RESOURCES" "$DMG_STAGING"

echo "Publishing client for $RUNTIME..."
dotnet publish "$CLIENT_PROJECT" -c "$CONFIGURATION" -r "$RUNTIME" --self-contained false -p:PublishSingleFile=false -o "$PUBLISH_DIR"

rm -rf "$APP_DIR"
mkdir -p "$APP_MACOS" "$APP_RESOURCES"
cp -R "$PUBLISH_DIR"/. "$APP_MACOS/"

cat > "$APP_CONTENTS/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>CFBundleDisplayName</key>
    <string>HeyeTodo</string>
    <key>CFBundleExecutable</key>
    <string>HeyeTodo.Client</string>
    <key>CFBundleIdentifier</key>
    <string>com.heyetodo.client</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>HeyeTodo</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>0.1.0</string>
    <key>CFBundleVersion</key>
    <string>0.1.0</string>
    <key>LSMinimumSystemVersion</key>
    <string>13.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
PLIST

chmod +x "$APP_MACOS/HeyeTodo.Client" || true

rm -rf "$DMG_STAGING"/*
cp -R "$APP_DIR" "$DMG_STAGING/HeyeTodo.app"

if command -v hdiutil >/dev/null 2>&1; then
    rm -f "$DMG_PATH"
    hdiutil create -volname "HeyeTodo" -srcfolder "$DMG_STAGING" -ov -format UDZO "$DMG_PATH"
    echo "DMG created at: $DMG_PATH"
else
    echo "hdiutil was not found. Skipping dmg creation."
    echo "App bundle is available at: $APP_DIR"
fi
