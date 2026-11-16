#!/bin/bash
#
# Nine Desktop Integration Installer
# Adds Nine to the application launcher for the Nine Property Management AppImage:
#   - copies the AppImage to ~/Applications/Nine (the original is left where it is)
#   - copies every script in this script's folder to ~/Applications/Nine/Utilities
#   - creates the launcher entry and icon
#
# Usage: 
#   First make this script executable: chmod +x install-desktop-integration.sh
#   Then run: ./install-desktop-integration.sh /path/to/Nine-1.0.0-x86_64.AppImage
#

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Check if AppImage path provided
if [ $# -eq 0 ]; then
    echo -e "${RED}Error: No AppImage path provided${NC}"
    echo "Usage: $0 /path/to/Nine.AppImage"
    exit 1
fi

APPIMAGE_PATH="$1"

# Check if AppImage exists
if [ ! -f "$APPIMAGE_PATH" ]; then
    echo -e "${RED}Error: AppImage not found at: $APPIMAGE_PATH${NC}"
    exit 1
fi

# Get absolute path
APPIMAGE_PATH="$(readlink -f "$APPIMAGE_PATH")"
APPIMAGE_DIR="$(dirname "$APPIMAGE_PATH")"
APPIMAGE_NAME="$(basename "$APPIMAGE_PATH")"
# Get version from filename (assumes format Nine-1.0.0-x86_64.AppImage)
# APPIMAGE_VERSION="$(echo "$APPIMAGE_NAME" | sed 's/Nine-\(.*\)-x86_64\.AppImage/\1/')"

# Extract version from filename (robust parsing) using parameter expansion
APPIMAGE_VERSION="${APPIMAGE_NAME#Nine-}"   # strip prefix
APPIMAGE_VERSION="${APPIMAGE_VERSION%-x86_64.AppImage}"  # strip suffix

echo -e "${GREEN}Nine Desktop Integration Installer${NC}"
echo "========================================"
echo ""

# Check if this script itself is executable (helpful reminder)
SCRIPT_PATH="$(readlink -f "$0")"
if [ ! -x "$SCRIPT_PATH" ]; then
    echo -e "${YELLOW}Note: This script should be made executable for convenience:${NC}"
    echo "  chmod +x $(basename "$SCRIPT_PATH")"
    echo ""
fi

echo "AppImage: $APPIMAGE_NAME"
echo "Location: $APPIMAGE_DIR"
echo ""

# Create application directory (app-specific subfolder avoids collisions)
APP_INSTALL_DIR="$HOME/Applications/Nine"
mkdir -p "$APP_INSTALL_DIR"

# Copy the AppImage to ~/Applications/Nine/ (the original is left where it was)
if [ "$APPIMAGE_PATH" -ef "$APP_INSTALL_DIR/$APPIMAGE_NAME" ]; then
    echo "✓ AppImage already in $APP_INSTALL_DIR/"
else
    echo "Copying AppImage to $APP_INSTALL_DIR/..."
    cp -f "$APPIMAGE_PATH" "$APP_INSTALL_DIR/$APPIMAGE_NAME" || { echo -e "${RED}Error: could not copy the AppImage to $APP_INSTALL_DIR/${NC}"; exit 1; }
    echo -e "${GREEN}✓ Copied to: $APP_INSTALL_DIR/$APPIMAGE_NAME${NC}"
fi
APPIMAGE_PATH="$APP_INSTALL_DIR/$APPIMAGE_NAME"
APPIMAGE_DIR="$APP_INSTALL_DIR"

# Make AppImage executable
chmod +x "$APPIMAGE_PATH"
echo "✓ Made AppImage executable"

# Keep copies of the Nine scripts (everything in this installer's folder: install, removal, tool,
# diagnostics, and so on) in ~/Applications/Nine/Utilities so they are still available after the
# download folder is gone.
UTILITIES_DIR="$APP_INSTALL_DIR/Utilities"
mkdir -p "$UTILITIES_DIR"
copy_script() {
    local source_file="$1"
    local name
    name="$(basename "$source_file")"
    local target_file="$UTILITIES_DIR/$name"
    if [ "$source_file" -ef "$target_file" ]; then
        [[ "$name" == *.sh ]] && chmod +x "$target_file"
        echo "✓ $name already in $UTILITIES_DIR/"
    elif cp -f "$source_file" "$target_file"; then
        [[ "$name" == *.sh ]] && chmod +x "$target_file"
        echo "✓ Copied $name to $UTILITIES_DIR/"
    else
        echo -e "${YELLOW}Warning: could not copy $name to $UTILITIES_DIR/${NC}"
    fi
}
SCRIPT_DIR="$(dirname "$SCRIPT_PATH")"
for source_file in "$SCRIPT_DIR"/*.sh "$SCRIPT_DIR"/*.ps1; do
    [ -f "$source_file" ] && copy_script "$source_file"
done
for required in nine-removal.sh nine-tool.sh; do
    [ -f "$SCRIPT_DIR/$required" ] || echo -e "${YELLOW}Warning: $required not found next to the installer; it was not copied.${NC}"
done

echo ""

# Create directories
mkdir -p ~/.local/share/applications
mkdir -p ~/.local/share/icons/hicolor/512x512/apps

# Remove any stale Aquiis files (Nine was previously named Aquiis)
if [ -f ~/.local/share/applications/aquiis.desktop ]; then
    rm ~/.local/share/applications/aquiis.desktop
    echo "✓ Removed legacy Aquiis desktop entry"
fi
if [ -f ~/.local/share/icons/hicolor/512x512/apps/aquiis.png ]; then
    rm ~/.local/share/icons/hicolor/512x512/apps/aquiis.png
    echo "✓ Removed legacy Aquiis icon"
fi
if [ -d "$HOME/Applications/Aquiis" ]; then
    echo -e "${YELLOW}Note: Legacy Aquiis app found at ~/Applications/Aquiis/ — remove it manually if no longer needed.${NC}"
fi

# Extract icon from inside the AppImage.
# Note: selective extraction (passing a filename to --appimage-extract) is not
# supported by all runtimes -- always do a full extract then pick the file out.
ICON_PATH="application-x-executable"
EXTRACT_WORKDIR="$(mktemp -d)"

echo "Extracting icon from AppImage..."
(cd "$EXTRACT_WORKDIR" && "$APPIMAGE_PATH" --appimage-extract > /dev/null 2>&1) || true

if [ -f "$EXTRACT_WORKDIR/squashfs-root/usr/share/icons/hicolor/512x512/apps/nine.png" ]; then
    cp "$EXTRACT_WORKDIR/squashfs-root/usr/share/icons/hicolor/512x512/apps/nine.png" \
        ~/.local/share/icons/hicolor/512x512/apps/nine.png
    ICON_PATH="nine"
    echo "✓ Extracted and installed icon from AppImage"
elif [ -f "$EXTRACT_WORKDIR/squashfs-root/nine.png" ]; then
    cp "$EXTRACT_WORKDIR/squashfs-root/nine.png" \
        ~/.local/share/icons/hicolor/512x512/apps/nine.png
    ICON_PATH="nine"
    echo "✓ Extracted and installed icon from AppImage"
elif [ -L "$EXTRACT_WORKDIR/squashfs-root/.DirIcon" ] || [ -f "$EXTRACT_WORKDIR/squashfs-root/.DirIcon" ]; then
    cp -L "$EXTRACT_WORKDIR/squashfs-root/.DirIcon" \
        ~/.local/share/icons/hicolor/512x512/apps/nine.png
    ICON_PATH="nine"
    echo "✓ Extracted and installed icon from AppImage (.DirIcon)"
else
    echo -e "${YELLOW}Warning: Could not extract icon from AppImage. Using generic icon.${NC}"
fi

rm -rf "$EXTRACT_WORKDIR"

# Create desktop entry
cat > ~/.local/share/applications/nine.desktop << EOF
[Desktop Entry]
Name=Nine Property Management
Comment=Multi-tenant property management system for DIY landlords and property managers
Exec=${APPIMAGE_PATH}
Icon=${ICON_PATH}
Type=Application
Categories=Office;Finance;
Terminal=false
StartupWMClass=Nine Property Management
X-AppImage-Version=${APPIMAGE_VERSION}
Keywords=property;management;landlord;rental;lease;tenant;invoice;
EOF

echo "✓ Created desktop entry"

# Make desktop file executable
chmod +x ~/.local/share/applications/nine.desktop
echo "✓ Made desktop entry executable"

# Update desktop database
if command -v update-desktop-database &> /dev/null; then
    update-desktop-database ~/.local/share/applications/
    echo "✓ Updated desktop database"
else
    echo -e "${YELLOW}Warning: update-desktop-database not found. You may need to log out and back in.${NC}"
fi

# Update icon cache
if command -v gtk-update-icon-cache &> /dev/null; then
    gtk-update-icon-cache -f -t ~/.local/share/icons/hicolor/ 2>/dev/null || true
    echo "✓ Updated icon cache"
elif command -v xdg-icon-resource &> /dev/null; then
    xdg-icon-resource forceupdate 2>/dev/null || true
    echo "✓ Updated icon cache"
fi

echo ""
echo -e "${GREEN}Installation complete!${NC}"
echo ""
echo "AppImage location: $APPIMAGE_PATH"
echo "✓ AppImage is executable and ready to use"
echo "✓ Desktop integration installed"
echo "✓ Icons and application launcher updated"
echo ""
echo "Nine Property Management should now appear in your application launcher."
echo "You can search for 'Nine' or find it in Office/Finance categories."
echo ""
echo "To remove Nine, use the tool that was copied to the Utilities folder:"
echo "  $UTILITIES_DIR/nine-tool.sh remove --help"
echo ""
echo "Or remove the desktop integration manually:"
echo "  rm ~/.local/share/applications/nine.desktop"
echo "  rm ~/.local/share/icons/hicolor/512x512/apps/nine.png"
echo "  update-desktop-database ~/.local/share/applications/"
echo "  rm -rf $APP_INSTALL_DIR"
echo ""
echo "These commands do not delete your data (~/.config/Nine) or the database encryption key."
echo ""
