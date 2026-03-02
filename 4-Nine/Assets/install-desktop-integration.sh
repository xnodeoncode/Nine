#!/bin/bash
#
# Nine Desktop Integration Installer
# Automatically creates desktop entry for Nine Property Management AppImage
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

# Create Applications directory
mkdir -p ~/Applications

# Move AppImage to ~/Applications/ if not already there
if [[ "$APPIMAGE_DIR" != "$HOME/Applications" ]]; then
    echo "Moving AppImage to ~/Applications/..."
    mv "$APPIMAGE_PATH" ~/Applications/
    APPIMAGE_PATH="$HOME/Applications/$APPIMAGE_NAME"
    APPIMAGE_DIR="$HOME/Applications"
    echo -e "${GREEN}✓ Moved to: $APPIMAGE_PATH${NC}"
else
    echo "✓ AppImage already in ~/Applications/"
fi

# Make AppImage executable
chmod +x "$APPIMAGE_PATH"
echo "✓ Made AppImage executable"

echo ""

# Create directories
mkdir -p ~/.local/share/applications
mkdir -p ~/.local/share/icons/hicolor/512x512/apps

# Check if icon exists in same directory
ICON_PATH="${APPIMAGE_DIR}/aquiis-icon.png"
if [ ! -f "$ICON_PATH" ]; then
    # Try to find icon.png in same directory
    ICON_PATH="${APPIMAGE_DIR}/icon.png"
    
    if [ ! -f "$ICON_PATH" ]; then
        echo -e "${YELLOW}Warning: Icon file not found. Using generic AppImage icon.${NC}"
        ICON_PATH="application-x-executable"
    else
        # Copy icon to system location
        cp "$ICON_PATH" ~/.local/share/icons/hicolor/512x512/apps/aquiis.png
        ICON_PATH="aquiis"
        echo "✓ Copied icon to system icons directory"
    fi
else
    # Copy icon to system location
    cp "$ICON_PATH" ~/.local/share/icons/hicolor/512x512/apps/aquiis.png
    ICON_PATH="aquiis"
    echo "✓ Copied icon to system icons directory"
fi

# Create desktop entry
cat > ~/.local/share/applications/aquiis.desktop << EOF
[Desktop Entry]
Name=Nine Property Management
Comment=Multi-tenant property management system for DIY landlords and property managers
Exec=${APPIMAGE_PATH}
Icon=${ICON_PATH}
Type=Application
Categories=Office;Finance;
Terminal=false
StartupWMClass=Nine Property Management
X-AppImage-Version=1.1.0
Keywords=property;management;landlord;rental;lease;tenant;invoice;
EOF

echo "✓ Created desktop entry"

# Make desktop file executable
chmod +x ~/.local/share/applications/aquiis.desktop
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
    gtk-update-icon-cache ~/.local/share/icons/hicolor/ 2>/dev/null || true
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
echo "To uninstall desktop integration:"
echo "  rm ~/.local/share/applications/aquiis.desktop"
echo "  rm ~/.local/share/icons/hicolor/512x512/apps/aquiis.png"
echo "  update-desktop-database ~/.local/share/applications/"
echo ""
echo "To completely remove Nine:"
echo "  rm $APPIMAGE_PATH"
echo ""
