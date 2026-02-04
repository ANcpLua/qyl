#!/bin/bash
# Mintlify Migration Conversion Script
# Usage: ./convert.sh <source-dir> <target-dir>

set -euo pipefail

SOURCE_DIR="${1:?Usage: $0 <source-dir> <target-dir>}"
TARGET_DIR="${2:?Usage: $0 <source-dir> <target-dir>}"

echo "=== Mintlify Migration ==="
echo "Source: $SOURCE_DIR"
echo "Target: $TARGET_DIR"

# Create target directory
mkdir -p "$TARGET_DIR"

# Count source files
SOURCE_COUNT=$(find "$SOURCE_DIR" -name "*.md" -type f | wc -l | tr -d ' ')
echo "Found $SOURCE_COUNT markdown files"

# Copy and rename files
echo "Copying and renaming .md -> .mdx..."
find "$SOURCE_DIR" -name "*.md" -type f | while read -r file; do
    # Get relative path
    rel_path="${file#$SOURCE_DIR/}"
    # Change extension
    new_path="${rel_path%.md}.mdx"
    # Rename index.md to overview.mdx
    new_path="${new_path/index.mdx/overview.mdx}"

    # Create target directory
    target_file="$TARGET_DIR/$new_path"
    mkdir -p "$(dirname "$target_file")"

    # Copy file
    cp "$file" "$target_file"
    echo "  $rel_path -> $new_path"
done

# Convert DocFX callouts
echo "Converting DocFX callouts..."
find "$TARGET_DIR" -name "*.mdx" -type f -exec sed -i '' \
    -e 's/> \[!NOTE\]/<Note>/g' \
    -e 's/> \[!WARNING\]/<Warning>/g' \
    -e 's/> \[!TIP\]/<Tip>/g' \
    -e 's/> \[!IMPORTANT\]/<Warning>/g' \
    {} \;

# Convert details to Accordion (basic pattern)
echo "Converting <details> to <Accordion>..."
find "$TARGET_DIR" -name "*.mdx" -type f -exec sed -i '' \
    -e 's/<details>/<Accordion>/g' \
    -e 's/<\/details>/<\/Accordion>/g' \
    -e 's/<summary>\(.*\)<\/summary>/title="\1">/g' \
    {} \;

# Fix link extensions
echo "Fixing link extensions..."
find "$TARGET_DIR" -name "*.mdx" -type f -exec sed -i '' \
    -e 's/\.md)/)/g' \
    -e 's/\.md#/)#/g' \
    {} \;

# Count target files
TARGET_COUNT=$(find "$TARGET_DIR" -name "*.mdx" -type f | wc -l | tr -d ' ')
echo ""
echo "=== Migration Complete ==="
echo "Converted $TARGET_COUNT files"
echo ""
echo "Next steps:"
echo "  1. Create docs.json configuration"
echo "  2. Run: npx mintlify broken-links"
echo "  3. Fix any remaining issues"
