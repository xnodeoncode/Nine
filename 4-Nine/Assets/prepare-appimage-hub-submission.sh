#!/bin/bash

# AppImageHub Submission Helper Script
# This script helps prepare files for AppImageHub submission

set -e

echo "🚀 AppImageHub Submission Preparation"
echo "======================================"
echo ""

# Check if appimage.github.io is already forked/cloned
if [ -d ~/appimage.github.io ]; then
    echo "✅ appimage.github.io repository found"
    cd ~/appimage.github.io
    git pull upstream master 2>/dev/null || echo "⚠️  No upstream remote configured"
else
    echo "📥 Cloning your fork of appimage.github.io..."
    echo "   Make sure you've forked https://github.com/AppImage/appimage.github.io first!"
    echo ""
    read -p "Enter your GitHub username: " username
    
    if [ -z "$username" ]; then
        echo "❌ Username required"
        exit 1
    fi
    
    git clone "https://github.com/$username/appimage.github.io.git" ~/appimage.github.io
    cd ~/appimage.github.io
    
    # Add upstream remote
    git remote add upstream https://github.com/AppImage/appimage.github.io.git
    git fetch upstream
fi

# Create application directory
APP_DIR="database/Nine"
echo ""
echo "📁 Creating directory: $APP_DIR"
mkdir -p "$APP_DIR"

# Copy desktop file
echo "📄 Copying desktop file..."
cp ~/Source/Nine/4-Nine/Assets/nine.desktop \
   "$APP_DIR/nine.desktop"

# Copy icon
echo "🎨 Copying icon..."
cp ~/Source/Nine/4-Nine/Assets/icon.png \
   "$APP_DIR/nine.png"

# Copy AppStream metadata
echo "📋 Copying AppStream metadata..."
cp ~/Source/Nine/4-Nine/Assets/com.nineapp.nine.appdata.xml \
   "$APP_DIR/com.nineapp.nine.appdata.xml"

# Copy screenshot (use dashboard as primary)
if [ -f ~/Source/Nine/Documentation/Screenshots/dashboard.png ]; then
    echo "📸 Copying screenshot..."
    cp ~/Source/Nine/Documentation/Screenshots/dashboard.png \
       "$APP_DIR/screenshot.png"
else
    echo "⚠️  Screenshot not found: ~/Source/Nine/Documentation/Screenshots/dashboard.png"
    echo "   You'll need to add screenshots manually"
fi

echo ""
echo "✅ Files prepared in: ~/appimage.github.io/$APP_DIR"
echo ""
echo "📝 Next steps:"
echo "   1. Take screenshots of your application (if not done)"
echo "   2. Run application and capture:"
echo "      - Dashboard (primary screenshot)"
echo "      - Property management interface"
echo "      - Lease workflow"
echo "      - Invoice tracking"
echo "   3. Save screenshots to: ~/Source/Nine/Documentation/Screenshots/"
echo "   4. Verify files in: ~/appimage.github.io/$APP_DIR"
echo "   5. Create branch: cd ~/appimage.github.io && git checkout -b add-nine"
echo "   6. Commit changes: git add . && git commit -m 'Add Nine Property Management'"
echo "   7. Push: git push origin add-nine"
echo "   8. Create PR on GitHub: https://github.com/AppImage/appimage.github.io/compare"
echo ""
