#!/bin/bash

# Nine Version Setter
# Directly sets a specific version without calculating a bump.
#
# Usage:
#   ./set-version.sh "1.1.0"                                             # set app version only
#   ./set-version.sh "1.1.0" --db-version "1.0.0"                       # set app + DB version (prev = current)
#   ./set-version.sh "1.1.0" --db-version "1.0.0" --prev-db "0.3.0"    # full manual control

set -e

NEW_VERSION="${1}"
DB_VERSION=""
PREV_DB_VERSION=""

# Parse flags
NEXT_FLAG=""
for arg in "$@"; do
    case "$arg" in
        --db-version)
            NEXT_FLAG="db"
            ;;
        --prev-db)
            NEXT_FLAG="prev"
            ;;
        *)
            if [ "$NEXT_FLAG" == "db" ]; then
                DB_VERSION="$arg"
                NEXT_FLAG=""
            elif [ "$NEXT_FLAG" == "prev" ]; then
                PREV_DB_VERSION="$arg"
                NEXT_FLAG=""
            fi
            ;;
    esac
done

CSPROJ_FILE="4-Nine/Nine.csproj"
APPSETTINGS_FILE="4-Nine/appsettings.json"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${YELLOW}🔧 Nine Version Setter${NC}"
echo ""

# Validate new version
if [ -z "$NEW_VERSION" ]; then
    echo -e "${RED}❌ Usage: ./set-version.sh \"1.1.0\" [--db-version \"1.0.0\"]${NC}"
    exit 1
fi

if ! [[ "$NEW_VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo -e "${RED}❌ Invalid version format: $NEW_VERSION (expected MAJOR.MINOR.PATCH)${NC}"
    exit 1
fi

if [ -n "$DB_VERSION" ] && ! [[ "$DB_VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo -e "${RED}❌ Invalid db-version format: $DB_VERSION (expected MAJOR.MINOR.PATCH)${NC}"
    exit 1
fi

if [ -n "$PREV_DB_VERSION" ] && ! [[ "$PREV_DB_VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo -e "${RED}❌ Invalid prev-db format: $PREV_DB_VERSION (expected MAJOR.MINOR.PATCH)${NC}"
    exit 1
fi

# Read current version
CURRENT_VERSION=$(grep -oP '<Version>\K[^<]+' "$CSPROJ_FILE" | head -1)
if [ -z "$CURRENT_VERSION" ]; then
    echo -e "${RED}❌ Could not find version in $CSPROJ_FILE${NC}"
    exit 1
fi

echo -e "Current Version: ${GREEN}$CURRENT_VERSION${NC}"
echo -e "New Version:     ${GREEN}$NEW_VERSION${NC}"

if [ -n "$DB_VERSION" ]; then
    CURRENT_DB=$(grep -oP '"DatabaseFileName": "\K[^"]+' "$APPSETTINGS_FILE")
    NEW_DB="app_v${DB_VERSION}.db"
    # Determine what PreviousDatabaseFileName will be set to
    if [ -n "$PREV_DB_VERSION" ]; then
        PREV_DB="app_v${PREV_DB_VERSION}.db"
    else
        PREV_DB="$CURRENT_DB"
    fi
    echo -e "Database:        ${GREEN}$CURRENT_DB${NC} → ${GREEN}$NEW_DB${NC} (schema: $DB_VERSION)"
    echo -e "Previous DB:     ${GREEN}$PREV_DB${NC}"
fi

echo ""
read -p "Continue? (y/n) " -n 1 -r
echo ""
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo -e "${RED}❌ Aborted${NC}"
    exit 1
fi

# Update .csproj
echo -e "${YELLOW}📝 Updating $CSPROJ_FILE...${NC}"
sed -i "s|<Version>$CURRENT_VERSION</Version>|<Version>$NEW_VERSION</Version>|g" "$CSPROJ_FILE"
sed -i "s|<AssemblyVersion>$CURRENT_VERSION.0</AssemblyVersion>|<AssemblyVersion>$NEW_VERSION.0</AssemblyVersion>|g" "$CSPROJ_FILE"
sed -i "s|<FileVersion>$CURRENT_VERSION.0</FileVersion>|<FileVersion>$NEW_VERSION.0</FileVersion>|g" "$CSPROJ_FILE"
sed -i "s|<InformationalVersion>$CURRENT_VERSION</InformationalVersion>|<InformationalVersion>$NEW_VERSION</InformationalVersion>|g" "$CSPROJ_FILE"

# Update appsettings.json version
echo -e "${YELLOW}📝 Updating $APPSETTINGS_FILE...${NC}"
sed -i "s|\"Version\": \"$CURRENT_VERSION\"|\"Version\": \"$NEW_VERSION\"|g" "$APPSETTINGS_FILE"

# Update database settings if --db-version was provided
if [ -n "$DB_VERSION" ]; then
    sed -i "s|\"DatabaseFileName\": \"$CURRENT_DB\"|\"DatabaseFileName\": \"$NEW_DB\"|g" "$APPSETTINGS_FILE"
    sed -i "s|\"PreviousDatabaseFileName\": \"[^\"]*\"|\"PreviousDatabaseFileName\": \"$PREV_DB\"|g" "$APPSETTINGS_FILE"
    sed -i "s|\"SchemaVersion\": \"[^\"]*\"|\"SchemaVersion\": \"$DB_VERSION\"|g" "$APPSETTINGS_FILE"
    sed -i "s|DataSource=[^;]*/app_v[^;.]*\.db|DataSource=Data/$NEW_DB|g" "$APPSETTINGS_FILE"
    echo -e "   DB file: ${GREEN}$NEW_DB${NC}, PreviousDB: ${GREEN}$PREV_DB${NC}"
fi

echo ""
echo -e "${GREEN}✅ Version set to $NEW_VERSION${NC}"
