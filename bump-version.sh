#!/bin/bash

# Nine Semantic Version Bumper
# Usage: ./bump-version.sh [major|minor|patch]

set -e

VERSION_TYPE="${1:-patch}"
CSPROJ_FILE="4-Nine/Nine.csproj"
APPSETTINGS_FILE="4-Nine/appsettings.json"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${YELLOW}🔄 Nine Version Bumper${NC}"
echo ""

# Extract current version from .csproj
CURRENT_VERSION=$(grep -oP '<Version>\K[^<]+' "$CSPROJ_FILE" | head -1)

if [ -z "$CURRENT_VERSION" ]; then
    echo -e "${RED}❌ Could not find version in $CSPROJ_FILE${NC}"
    exit 1
fi

echo -e "Current Version: ${GREEN}$CURRENT_VERSION${NC}"

# Parse version components
IFS='.' read -r MAJOR MINOR PATCH <<< "$CURRENT_VERSION"

# Increment based on type
case "$VERSION_TYPE" in
    major)
        MAJOR=$((MAJOR + 1))
        MINOR=0
        PATCH=0
        echo -e "Bump Type: ${YELLOW}MAJOR${NC} (breaking changes, database migration required)"
        ;;
    minor)
        MINOR=$((MINOR + 1))
        PATCH=0
        echo -e "Bump Type: ${YELLOW}MINOR${NC} (new features, backward compatible)"
        ;;
    patch)
        PATCH=$((PATCH + 1))
        echo -e "Bump Type: ${YELLOW}PATCH${NC} (bug fixes, no DB changes)"
        ;;
    *)
        echo -e "${RED}❌ Invalid version type. Use: major, minor, or patch${NC}"
        exit 1
        ;;
esac

NEW_VERSION="$MAJOR.$MINOR.$PATCH"
echo -e "New Version: ${GREEN}$NEW_VERSION${NC}"
echo ""

# Determine database version (ignores PATCH for DB filename)
DB_VERSION="$MAJOR.$MINOR.0"

# Ask for confirmation
read -p "Continue with version bump? (y/n) " -n 1 -r
echo ""
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo -e "${RED}❌ Aborted${NC}"
    exit 1
fi

# Update .csproj file
echo -e "${YELLOW}📝 Updating $CSPROJ_FILE...${NC}"
sed -i "s|<Version>$CURRENT_VERSION</Version>|<Version>$NEW_VERSION</Version>|g" "$CSPROJ_FILE"
sed -i "s|<AssemblyVersion>$CURRENT_VERSION.0</AssemblyVersion>|<AssemblyVersion>$NEW_VERSION.0</AssemblyVersion>|g" "$CSPROJ_FILE"
sed -i "s|<FileVersion>$CURRENT_VERSION.0</FileVersion>|<FileVersion>$NEW_VERSION.0</FileVersion>|g" "$CSPROJ_FILE"
sed -i "s|<InformationalVersion>$CURRENT_VERSION</InformationalVersion>|<InformationalVersion>$NEW_VERSION</InformationalVersion>|g" "$CSPROJ_FILE"

# Update appsettings.json
echo -e "${YELLOW}📝 Updating $APPSETTINGS_FILE...${NC}"
sed -i "s|\"Version\": \"$CURRENT_VERSION\"|\"Version\": \"$NEW_VERSION\"|g" "$APPSETTINGS_FILE"

# Update database settings if MAJOR or MINOR version changed
if [ "$VERSION_TYPE" == "major" ] || [ "$VERSION_TYPE" == "minor" ]; then
    echo -e "${YELLOW}📝 Updating database version to app_v${DB_VERSION}.db...${NC}"
    
    # Get current database filename
    CURRENT_DB=$(grep -oP '"DatabaseFileName": "\K[^"]+' "$APPSETTINGS_FILE")
    NEW_DB="app_v${DB_VERSION}.db"
    
    # Update DatabaseFileName and PreviousDatabaseFileName
    sed -i "s|\"DatabaseFileName\": \"$CURRENT_DB\"|\"DatabaseFileName\": \"$NEW_DB\"|g" "$APPSETTINGS_FILE"
    sed -i "s|\"PreviousDatabaseFileName\": \"[^\"]*\"|\"PreviousDatabaseFileName\": \"$CURRENT_DB\"|g" "$APPSETTINGS_FILE"
    sed -i "s|\"SchemaVersion\": \"[^\"]*\"|\"SchemaVersion\": \"$DB_VERSION\"|g" "$APPSETTINGS_FILE"
    
    # Update connection string
    sed -i "s|DataSource=Infrastructure/Data/$CURRENT_DB|DataSource=Infrastructure/Data/$NEW_DB|g" "$APPSETTINGS_FILE"
    
    echo -e "${YELLOW}⚠️  Database version changed!${NC}"
    echo -e "   - New DB file: ${GREEN}$NEW_DB${NC}"
    echo -e "   - Previous DB: ${GREEN}$CURRENT_DB${NC}"
    echo -e "   - ${RED}You may need to create a migration or copy the old database${NC}"
else
    echo -e "${GREEN}✓ Database version unchanged (PATCH update)${NC}"
fi

echo ""
echo -e "${GREEN}✅ Version bump complete!${NC}"
echo -e "   $CURRENT_VERSION → ${GREEN}$NEW_VERSION${NC}"
echo ""
echo -e "${YELLOW}Next steps:${NC}"
echo "   1. Review changes: git diff"
echo "   2. Build and test: dotnet build Nine.sln"
echo "   3. Commit: git add . && git commit -m 'chore: bump version to $NEW_VERSION'"
echo "   4. Tag: git tag v$NEW_VERSION"
echo "   5. Push: git push && git push --tags"
