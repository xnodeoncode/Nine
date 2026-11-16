#!/bin/bash
#
# Nine Removal Script
# Undoes install-desktop-integration.sh and, only when asked, also removes Nine's data
# and its database encryption key.
#
# Usage:
#   First make this script executable: chmod +x nine-removal.sh
#   Then run: ./nine-removal.sh [options]
#
# By default it removes what install-desktop-integration.sh created:
#   - the launcher entry   ~/.local/share/applications/nine.desktop
#   - the icon             ~/.local/share/icons/hicolor/512x512/apps/nine.png
#   - the AppImages        ~/Applications/Nine
#
# Options:
#   --all               Remove everything: the launcher entry, icon, AppImages, ALL data and the
#                       database encryption key. Same as --remove-data --remove-keychain.
#   --remove-data       Also delete Nine's data folder (databases, backups, settings) for ALL versions.
#                       Default location: ~/.config/Nine (or $XDG_CONFIG_HOME/Nine).
#   --remove-keychain   Also delete the stored database encryption key. It is shared by every
#                       version of Nine on this account.
#   --remove-keychain-only
#                       Delete ONLY the database encryption key. The AppImages, launcher entry
#                       and data are left untouched.
#   --keep-appimages    Do not delete ~/Applications/Nine.
#   --only NAME         Remove one AppImage from ~/Applications/Nine (for example
#                       Nine-1.2.0-x86_64.AppImage) and nothing else. Your data and the
#                       encryption key are left in place.
#   --dry-run           Show what would be removed without changing anything.
#   --yes               Do not ask for confirmation.
#   -h, --help          Show this help.
#

set -u

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

REMOVE_DATA=0
REMOVE_KEYCHAIN=0
ALL_FLAG=0
KEYCHAIN_ONLY=0
KEEP_APPIMAGES=0
ONLY_NAME=""
DRY_RUN=0
ASSUME_YES=0

APP_INSTALL_DIR="${HOME:?HOME is not set}/Applications/Nine"
DESKTOP_FILE="$HOME/.local/share/applications/nine.desktop"
ICON_FILE="$HOME/.local/share/icons/hicolor/512x512/apps/nine.png"
DATA_DIR="${XDG_CONFIG_HOME:-$HOME/.config}/Nine"
KEYCHAIN_ATTRIBUTE="key-type"
KEYCHAIN_VALUE="database-encryption-Nine-Electron"

usage() {
    sed -n '3,32p' "$0" | sed 's/^# \{0,1\}//'
}

while [ $# -gt 0 ]; do
    case "$1" in
        --remove-keychain-only) KEYCHAIN_ONLY=1 ;;
        --all) REMOVE_DATA=1; REMOVE_KEYCHAIN=1; ALL_FLAG=1 ;;
        --remove-data) REMOVE_DATA=1 ;;
        --remove-keychain) REMOVE_KEYCHAIN=1 ;;
        --keep-appimages) KEEP_APPIMAGES=1 ;;
        --only)
            shift
            if [ $# -eq 0 ]; then
                echo -e "${RED}Error: --only needs an AppImage file name${NC}"
                exit 1
            fi
            ONLY_NAME="$1"
            ;;
        --dry-run) DRY_RUN=1 ;;
        --yes) ASSUME_YES=1 ;;
        -h|--help) usage; exit 0 ;;
        *)
            echo -e "${RED}Error: unknown option: $1${NC}"
            usage
            exit 1
            ;;
    esac
    shift
done

if [ $KEYCHAIN_ONLY -eq 1 ] && { [ $ALL_FLAG -eq 1 ] || [ $REMOVE_DATA -eq 1 ] || [ $REMOVE_KEYCHAIN -eq 1 ] || [ $KEEP_APPIMAGES -eq 1 ] || [ -n "$ONLY_NAME" ]; }; then
    echo -e "${RED}Error: --remove-keychain-only cannot be combined with any other removal option${NC}"
    exit 1
fi

if [ -n "$ONLY_NAME" ] && { [ $REMOVE_DATA -eq 1 ] || [ $REMOVE_KEYCHAIN -eq 1 ] || [ $KEEP_APPIMAGES -eq 1 ]; }; then
    echo -e "${RED}Error: --only cannot be combined with --all, --remove-data, --remove-keychain or --keep-appimages${NC}"
    exit 1
fi

if [ $ALL_FLAG -eq 1 ] && [ $KEEP_APPIMAGES -eq 1 ]; then
    echo -e "${RED}Error: --all removes everything, so it cannot be combined with --keep-appimages${NC}"
    exit 1
fi

if [ $ASSUME_YES -eq 0 ] && [ $DRY_RUN -eq 0 ] && [ ! -t 0 ]; then
    echo -e "${RED}Error: this script asks for confirmation. Run it in a terminal, or pass --yes.${NC}"
    exit 1
fi

# Runs a command, or only prints it in dry-run mode.
run() {
    if [ $DRY_RUN -eq 1 ]; then
        echo "  [dry-run] $*"
    else
        "$@"
    fi
}

# Deletes a directory only if it is one of the exact folders this script manages.
safe_remove_dir() {
    local dir="$1"
    if [ -z "$dir" ] || [ "$dir" = "/" ] || [ "$dir" = "$HOME" ] || [ "$(basename "$dir")" != "Nine" ]; then
        echo -e "${RED}Refusing to delete unexpected path: '$dir'${NC}"
        return 1
    fi
    run rm -rf -- "$dir"
}

confirm() {
    if [ $ASSUME_YES -eq 1 ] || [ $DRY_RUN -eq 1 ]; then
        return 0
    fi
    local reply
    read -r -p "$1 [y/N] " reply
    [[ "$reply" =~ ^[Yy]$ ]]
}

# Requires typing a word, so irreversible deletions are never confirmed by a stray keypress.
confirm_typed() {
    if [ $ASSUME_YES -eq 1 ] || [ $DRY_RUN -eq 1 ]; then
        return 0
    fi
    local reply
    read -r -p "$1" reply
    [ "$reply" = "$2" ]
}

# Prints a result line only for real runs (dry-run already printed what it would do).
report() {
    [ $DRY_RUN -eq 1 ] || echo "$*"
}

echo -e "${GREEN}Nine Removal${NC}"
echo "========================================"
[ $DRY_RUN -eq 1 ] && echo -e "${YELLOW}Dry run: nothing will be changed.${NC}"
echo ""

# Deleting files while Nine is running can leave a database in an inconsistent state.
if pgrep -f '\.mount_Nine|resources/bin/Nine' > /dev/null 2>&1; then
    echo -e "${RED}Nine is running. Close all Nine windows and run this script again.${NC}"
    exit 1
fi

# ---- Remove the encryption key only ----
if [ $KEYCHAIN_ONLY -eq 1 ]; then
    if ! command -v secret-tool > /dev/null 2>&1; then
        echo -e "${YELLOW}secret-tool is not installed, so the encryption key cannot be removed by this script.${NC}"
        exit 1
    fi
    if ! secret-tool lookup "$KEYCHAIN_ATTRIBUTE" "$KEYCHAIN_VALUE" > /dev/null 2>&1; then
        echo "No Nine encryption key was found in the keychain. Nothing to do."
        exit 0
    fi
    echo -e "${RED}This deletes the database encryption key shared by every version of Nine on this account.${NC}"
    echo "Encrypted databases and encrypted backups cannot be opened without it."
    echo -e "${YELLOW}Nothing else is removed. Any encrypted database in $DATA_DIR becomes unreadable.${NC}"
    confirm_typed "Type DELETE to remove the encryption key: " "DELETE" || { echo "Cancelled."; exit 0; }
    run secret-tool clear "$KEYCHAIN_ATTRIBUTE" "$KEYCHAIN_VALUE" && report "Removed the database encryption key"
    echo -e "${GREEN}Done.${NC}"
    exit 0
fi

# ---- Remove a single AppImage only ----
if [ -n "$ONLY_NAME" ]; then
    case "$ONLY_NAME" in
        */*|..*|"")
            echo -e "${RED}Error: --only takes a file name, not a path${NC}"
            exit 1
            ;;
    esac
    TARGET="$APP_INSTALL_DIR/$ONLY_NAME"
    if [ ! -f "$TARGET" ]; then
        echo -e "${RED}Not found: $TARGET${NC}"
        exit 1
    fi
    echo "Will remove: $TARGET"
    echo "Your data and encryption key are not touched."
    confirm "Remove this AppImage?" || { echo "Cancelled."; exit 0; }
    run rm -f -- "$TARGET"
    if [ -f "$DESKTOP_FILE" ] && grep -qF "$TARGET" "$DESKTOP_FILE"; then
        echo -e "${YELLOW}The launcher entry pointed to this AppImage and no longer works.${NC}"
        echo "Run install-desktop-integration.sh (kept in $APP_INSTALL_DIR/Utilities) with the version you kept to fix it."
    fi
    echo -e "${GREEN}Done.${NC}"
    exit 0
fi

# ---- Build and show the plan ----
echo "The following will be removed:"
[ -f "$DESKTOP_FILE" ] && echo "  - Launcher entry: $DESKTOP_FILE"
[ -f "$ICON_FILE" ] && echo "  - Icon: $ICON_FILE"
if [ $KEEP_APPIMAGES -eq 0 ] && [ -d "$APP_INSTALL_DIR" ]; then
    echo "  - AppImages folder: $APP_INSTALL_DIR"
    for f in "$APP_INSTALL_DIR"/*; do [ -e "$f" ] && echo "      $(basename "$f")"; done
fi
[ $REMOVE_DATA -eq 1 ] && echo "  - DATA FOLDER (all versions): $DATA_DIR"
[ $REMOVE_KEYCHAIN -eq 1 ] && echo "  - DATABASE ENCRYPTION KEY (all versions) in the system keychain"
echo ""
if [ $REMOVE_DATA -eq 0 ]; then
    echo "Kept: your data folder $DATA_DIR (use --remove-data to delete it)."
fi
if [ $REMOVE_KEYCHAIN -eq 0 ]; then
    echo "Kept: the database encryption key (use --remove-keychain to delete it)."
fi
echo ""

confirm "Continue?" || { echo "Cancelled."; exit 0; }

# ---- Extra confirmation for irreversible removals ----
if [ $REMOVE_DATA -eq 1 ]; then
    echo ""
    echo -e "${RED}This permanently deletes every Nine database and backup stored here:${NC}"
    echo "  $DATA_DIR"
    if [ -d "$DATA_DIR" ]; then
        for db in "$DATA_DIR"/app_v*.db; do
            [ -e "$db" ] && echo "    database: $(basename "$db") ($(du -h "$db" | cut -f1))"
        done
        if [ -d "$DATA_DIR/Backups" ]; then
            echo "    backups:  $(ls "$DATA_DIR/Backups" | wc -l) file(s) in $DATA_DIR/Backups"
        fi
    else
        echo "    (folder does not exist)"
    fi
    echo "Copy anything you want to keep to another location first."
    confirm_typed "Type DELETE to permanently delete this data: " "DELETE" || { echo "Cancelled."; exit 0; }
fi

if [ $REMOVE_KEYCHAIN -eq 1 ]; then
    echo ""
    if ! command -v secret-tool > /dev/null 2>&1; then
        echo -e "${YELLOW}secret-tool is not installed, so the encryption key cannot be removed by this script.${NC}"
        REMOVE_KEYCHAIN=0
    elif ! secret-tool lookup "$KEYCHAIN_ATTRIBUTE" "$KEYCHAIN_VALUE" > /dev/null 2>&1; then
        echo "No Nine encryption key was found in the keychain."
        REMOVE_KEYCHAIN=0
    else
        echo -e "${RED}This deletes the database encryption key shared by every version of Nine on this account.${NC}"
        echo "Encrypted databases and encrypted backups cannot be opened without it."
        if [ $REMOVE_DATA -eq 0 ]; then
            echo -e "${YELLOW}Your data folder is being kept, so any encrypted database in it would become unreadable.${NC}"
        fi
        confirm_typed "Type DELETE to remove the encryption key: " "DELETE" || { echo "Cancelled."; exit 0; }
    fi
fi

# ---- Remove ----
echo ""
if [ -f "$DESKTOP_FILE" ]; then
    run rm -f -- "$DESKTOP_FILE" && report "Removed launcher entry"
fi
if [ -f "$ICON_FILE" ]; then
    run rm -f -- "$ICON_FILE" && report "Removed icon"
fi
if [ $KEEP_APPIMAGES -eq 0 ] && [ -d "$APP_INSTALL_DIR" ]; then
    safe_remove_dir "$APP_INSTALL_DIR" && report "Removed $APP_INSTALL_DIR"
fi
if [ $REMOVE_DATA -eq 1 ] && [ -d "$DATA_DIR" ]; then
    safe_remove_dir "$DATA_DIR" && report "Removed $DATA_DIR"
fi
if [ $REMOVE_KEYCHAIN -eq 1 ]; then
    run secret-tool clear "$KEYCHAIN_ATTRIBUTE" "$KEYCHAIN_VALUE" && report "Removed the database encryption key"
fi

# ---- Refresh desktop caches (same as the installer) ----
if [ $DRY_RUN -eq 0 ]; then
    if command -v update-desktop-database > /dev/null 2>&1 && [ -d "$HOME/.local/share/applications" ]; then
        update-desktop-database "$HOME/.local/share/applications" 2>/dev/null || true
    fi
    if command -v gtk-update-icon-cache > /dev/null 2>&1 && [ -d "$HOME/.local/share/icons/hicolor" ]; then
        gtk-update-icon-cache -f -t "$HOME/.local/share/icons/hicolor/" 2>/dev/null || true
    elif command -v xdg-icon-resource > /dev/null 2>&1; then
        xdg-icon-resource forceupdate 2>/dev/null || true
    fi
fi

echo ""
echo -e "${GREEN}Done.${NC}"
if [ $REMOVE_DATA -eq 1 ] && [ $REMOVE_KEYCHAIN -eq 0 ]; then
    echo "The database encryption key (if any) is still in the keychain. Run again with --remove-keychain to delete it."
fi
