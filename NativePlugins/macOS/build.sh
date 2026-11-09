#!/bin/sh
# Builds the macOS sign-in sheet plugin (universal: Apple silicon + Intel) into the Unity project.
set -e
here="$(cd "$(dirname "$0")" && pwd)"
out="$here/../../Assets/Plugins/macOS/MindscapeAuth.bundle"
mkdir -p "$out/Contents/MacOS"
clang -bundle -fobjc-arc -O2 -arch arm64 -arch x86_64 -mmacosx-version-min=10.15 \
  -framework AppKit -framework AuthenticationServices \
  -o "$out/Contents/MacOS/MindscapeAuth" "$here/MindscapeAuth.m"
cat > "$out/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>CFBundleExecutable</key><string>MindscapeAuth</string>
  <key>CFBundleIdentifier</key><string>com.mindscape.auth</string>
  <key>CFBundleName</key><string>MindscapeAuth</string>
  <key>CFBundlePackageType</key><string>BNDL</string>
  <key>CFBundleVersion</key><string>1</string>
  <key>CFBundleShortVersionString</key><string>1.0</string>
</dict></plist>
PLIST
codesign --force --sign - "$out"
echo "built $out"
