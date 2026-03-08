#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_PATH="$ROOT_DIR/src/PdfFormFiller.Cli/PdfFormFiller.Cli.csproj"
RID="${1:-osx-arm64}"
OUT_ROOT="${2:-$ROOT_DIR/artifacts/release}"
VERSION="${PACKAGE_VERSION:-$(sed -n 's|.*<PackageVersion>\(.*\)</PackageVersion>.*|\1|p' "$PROJECT_PATH" | head -n 1)}"

if [[ -z "$VERSION" ]]; then
  echo "Unable to determine PackageVersion from $PROJECT_PATH" >&2
  exit 1
fi

PUBLISH_DIR="$OUT_ROOT/publish/$RID"
STAGING_ROOT="$OUT_ROOT/staging"
STAGING_DIR="$STAGING_ROOT/pdf-form-filler-v$VERSION-$RID"
DIST_DIR="$OUT_ROOT/dist"
ARCHIVE_PATH="$DIST_DIR/pdf-form-filler-v$VERSION-$RID.tar.gz"

rm -rf "$PUBLISH_DIR" "$STAGING_DIR"
mkdir -p "$PUBLISH_DIR" "$STAGING_DIR" "$DIST_DIR"

dotnet publish "$PROJECT_PATH" \
  -c Release \
  -r "$RID" \
  --self-contained false \
  -p:PublishSingleFile=true \
  -o "$PUBLISH_DIR"

cp "$PUBLISH_DIR/pdf-form-filler" "$STAGING_DIR/"
cp "$ROOT_DIR/LICENSE" "$STAGING_DIR/"

tar -C "$STAGING_ROOT" -czf "$ARCHIVE_PATH" "$(basename "$STAGING_DIR")"
shasum -a 256 "$ARCHIVE_PATH" | awk '{print $1}' > "$ARCHIVE_PATH.sha256"

echo "Created $ARCHIVE_PATH"
echo "SHA256: $(cat "$ARCHIVE_PATH.sha256")"
