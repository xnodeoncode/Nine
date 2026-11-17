#!/bin/bash
#
# Nine Tool
# One entry point for the Nine install and removal scripts.
#
# Usage:
#   nine-tool.sh <command> [options]
#
# Commands:
#   install <AppImage>   Install desktop integration. Runs install-desktop-integration.sh.
#                        Copies the AppImage to ~/Applications/Nine and the scripts to
#                        ~/Applications/Nine/Utilities.
#   remove [options]     Remove Nine. Runs nine-removal.sh; all options are passed through
#                        (try: nine-tool.sh remove --help).
#   diagnose [AppImage]  Measure startup time. Runs diagnose-startup.sh.
#   verify [AppImage]    Verify a build (ports, host header, connections). Runs verify-build.sh.
#                        Needs strace, ss, curl and python3; it lists them and asks before it runs.
#   help                 Show this help.
#
# The scripts are looked for in the same folder as this one. They are run with bash, so they
# do not need the executable bit.
#

set -u

RED='\033[0;31m'
NC='\033[0m' # No Color

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "$0")")" && pwd)"

usage() {
    sed -n '3,21p' "$0" | sed 's/^# \{0,1\}//'
}

# Runs a sibling script, passing every remaining argument through unchanged.
run_script() {
    local script="$SCRIPT_DIR/$1"
    shift
    if [ ! -f "$script" ]; then
        echo -e "${RED}Error: $(basename "$script") was not found in $SCRIPT_DIR${NC}" >&2
        exit 1
    fi
    exec bash "$script" "$@"
}

if [ $# -eq 0 ]; then
    usage
    exit 1
fi

COMMAND="$1"
shift

case "$COMMAND" in
    install)
        case "${1:-}" in
            -h|--help)
                echo "Usage: nine-tool.sh install /path/to/Nine-<version>-x86_64.AppImage"
                echo "Copies the AppImage to ~/Applications/Nine, adds Nine to the application launcher,"
                echo "and copies the scripts to ~/Applications/Nine/Utilities."
                exit 0
                ;;
        esac
        run_script install-desktop-integration.sh "$@"
        ;;
    remove)
        run_script nine-removal.sh "$@"
        ;;
    diagnose)
        run_script diagnose-startup.sh "$@"
        ;;
    verify)
        run_script verify-build.sh "$@"
        ;;
    help|-h|--help)
        usage
        ;;
    *)
        echo -e "${RED}Error: unknown command: $COMMAND${NC}" >&2
        usage
        exit 1
        ;;
esac
