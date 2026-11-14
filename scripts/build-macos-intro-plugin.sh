#!/bin/sh
set -eu

root=$(CDPATH='' cd -- "$(dirname -- "$0")/.." && pwd)
project="$root/unity/Homecoming"
bundle="$project/Assets/Plugins/macOS/HomecomingIntro.bundle"
mkdir -p "$bundle/Contents/MacOS"

cat > "$bundle/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>CFBundleExecutable</key><string>HomecomingIntro</string>
  <key>CFBundleIdentifier</key><string>kw.spaceapps.homecoming.intro</string>
  <key>CFBundlePackageType</key><string>BNDL</string>
  <key>CFBundleVersion</key><string>1</string>
</dict></plist>
PLIST

xcrun clang -arch arm64 -arch x86_64 -mmacosx-version-min=11.0 -O2 -fobjc-arc -fvisibility=hidden \
  -bundle -framework AppKit -framework AVFoundation -framework QuartzCore -framework CoreMedia \
  "$project/Native/HomecomingIntro.m" -o "$bundle/Contents/MacOS/HomecomingIntro"
codesign --force --sign - "$bundle"
