#!/bin/bash
#
# Nine Startup Performance Diagnostic Tool
# Profiles AppImage startup to identify bottlenecks
#

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

if [ -n "$1" ]; then
    APPIMAGE="$1"
else
    # Default to the newest Nine AppImage installed by the desktop integration script
    APPIMAGE="$(ls "$HOME"/Applications/Nine/Nine-*-x86_64.AppImage 2>/dev/null | sort -V | tail -1)"
fi
LOG_FILE=/tmp/nine-startup.log

if [ -z "$APPIMAGE" ] || [ ! -f "$APPIMAGE" ]; then
    echo -e "${RED}Error: AppImage not found${NC}"
    echo "Usage: $0 [path-to-appimage]"
    exit 1
fi

echo -e "${BLUE}Nine Startup Performance Diagnostic${NC}"
echo "=========================================="
echo ""
echo "AppImage: $(basename "$APPIMAGE")"
echo "Size: $(du -h "$APPIMAGE" | cut -f1)"
echo ""

# Test 1: AppImage Mount Time
echo -e "${YELLOW}Test 1: AppImage FUSE Mount Time${NC}"
START=$(date +%s.%N)
"$APPIMAGE" --appimage-help > /dev/null 2>&1
END=$(date +%s.%N)
MOUNT_TIME=$(echo "$END - $START" | bc)
echo "  Mount time: ${MOUNT_TIME}s"
echo ""

# Test 2: .NET Runtime Initialization
echo -e "${YELLOW}Test 2: .NET Runtime Startup${NC}"
echo "  Starting AppImage with timestamps..."
START=$(date +%s.%N)

# Launch AppImage and capture its PID (the Electron main process)
"$APPIMAGE" > "$LOG_FILE" 2>&1 &
APPIMAGE_PID=$!

# Monitor for the .NET backend process, which Electron starts as a child
DOTNET_PID=""
ELAPSED=0
while [ -z "$DOTNET_PID" ] && [ $ELAPSED -lt 120 ]; do
    sleep 0.5
    DOTNET_PID=$(pgrep -P $APPIMAGE_PID -f "resources/bin/Nine" | head -1)
    ELAPSED=$((ELAPSED + 1))
done

if [ -n "$DOTNET_PID" ]; then
    DOTNET_START=$(date +%s.%N)
    DOTNET_LAUNCH=$(echo "$DOTNET_START - $START" | bc)
    echo "  .NET process spawned: ${DOTNET_LAUNCH}s"
    
    # Wait for Kestrel. Nine takes the next free port when 8888 is in use,
    # so read the port the .NET process is actually listening on.
    WEB_PORT=""
    KESTREL_ELAPSED=0
    while [ -z "$WEB_PORT" ] && [ $KESTREL_ELAPSED -lt 120 ]; do
        LISTEN_ADDR=$(ss -ltnpH 2>/dev/null | grep "pid=$DOTNET_PID," | head -1 | awk '{print $4}')
        WEB_PORT="${LISTEN_ADDR##*:}"
        if [ -n "$WEB_PORT" ]; then
            KESTREL_END=$(date +%s.%N)
            KESTREL_TIME=$(echo "$KESTREL_END - $DOTNET_START" | bc)
            echo "  Kestrel listening on port ${WEB_PORT}: ${KESTREL_TIME}s"
        else
            sleep 0.5
            KESTREL_ELAPSED=$((KESTREL_ELAPSED + 1))
        fi
    done
    
    if [ -z "$WEB_PORT" ]; then
        echo -e "  ${RED}Warning: Kestrel did not start within 60s${NC}"
    fi
    
    # Wait for the web UI to answer. The Electron window cannot be detected from a shell,
    # so a successful HTTP response is used as the "ready" signal.
    UI_READY=0
    UI_ELAPSED=0
    while [ -n "$WEB_PORT" ] && [ $UI_READY -eq 0 ] && [ $UI_ELAPSED -lt 120 ]; do
        if curl -s -o /dev/null "http://localhost:${WEB_PORT}/"; then
            UI_READY=1
            UI_END=$(date +%s.%N)
            UI_TIME=$(echo "$UI_END - $START" | bc)
            echo "  Web UI responding: ${UI_TIME}s"
        else
            sleep 0.5
            UI_ELAPSED=$((UI_ELAPSED + 1))
        fi
    done
    
    # Total startup time
    TOTAL_END=$(date +%s.%N)
    TOTAL_TIME=$(echo "$TOTAL_END - $START" | bc)
    
    echo ""
    echo -e "${GREEN}Startup Timeline:${NC}"
    echo "  1. AppImage mount: ${MOUNT_TIME}s"
    echo "  2. .NET launch: ${DOTNET_LAUNCH}s"
    echo "  3. Kestrel ready: ${KESTREL_TIME:-n/a}s"
    echo "  4. Web UI responding: ${UI_TIME:-n/a}s"
    echo ""
    echo -e "${BLUE}Total startup: ${TOTAL_TIME}s${NC}"
    
    # Stop only the instance this script started
    echo ""
    echo -e "${YELLOW}Stopping test instance...${NC}"
    kill $APPIMAGE_PID 2>/dev/null
    sleep 2
    kill -9 $APPIMAGE_PID $DOTNET_PID 2>/dev/null
else
    echo -e "  ${RED}Error: .NET process did not start${NC}"
    kill $APPIMAGE_PID 2>/dev/null
fi

echo ""
echo -e "${YELLOW}Test 3: System Information${NC}"
echo "  CPU: $(grep "model name" /proc/cpuinfo | head -1 | cut -d: -f2 | xargs)"
echo "  RAM: $(free -h | awk '/^Mem:/ {print $2}')"
echo "  Disk (AppImage): $(df -h "$APPIMAGE" | tail -1 | awk '{print $1 " (" $4 " free)"}')"
echo "  Disk (Config): $(df -h ~/.config/Nine 2>/dev/null | tail -1 | awk '{print $1 " (" $4 " free)"}')"

echo ""
echo -e "${YELLOW}Test 4: Database Check${NC}"
DB_FILES=$(ls ~/.config/Nine/app_v*.db 2>/dev/null)
if [ -n "$DB_FILES" ]; then
    for DB_FILE in $DB_FILES; do
        echo "  $(basename "$DB_FILE"): $(du -h "$DB_FILE" | cut -f1) ($DB_FILE)"
    done
else
    echo "  Database: Not initialized (first run will be slower)"
fi

echo ""
echo -e "${YELLOW}Test 5: AppImage Properties${NC}"
"$APPIMAGE" --appimage-version 2>/dev/null || echo "  AppImage runtime: Type 2"
# The AppImage is an ELF runtime followed by a squashfs image, so `file` cannot see the compression.
# Read the compression id from the squashfs superblock (2 bytes at offset 20 of the image).
APPIMAGE_OFFSET=$("$APPIMAGE" --appimage-offset 2>/dev/null)
COMPRESSION="unknown"
if [ -n "$APPIMAGE_OFFSET" ]; then
    COMP_ID=$(dd if="$APPIMAGE" bs=1 skip=$((APPIMAGE_OFFSET + 20)) count=1 2>/dev/null | od -An -tu1 | tr -d ' ')
    case "$COMP_ID" in
        1) COMPRESSION="gzip" ;;
        2) COMPRESSION="lzma" ;;
        3) COMPRESSION="lzo" ;;
        4) COMPRESSION="xz" ;;
        5) COMPRESSION="lz4" ;;
        6) COMPRESSION="zstd" ;;
    esac
fi
echo "  Compression: $COMPRESSION"

echo ""
echo -e "${GREEN}Diagnostic complete!${NC}"
echo ""
echo "If startup is still slow (>20s), check:"
echo "  1. Antivirus/security software scanning AppImage"
echo "  2. Slow disk (HDD vs SSD)"
echo "  3. Check $LOG_FILE for .NET errors"
echo "  4. Try: export ELECTRON_NO_ASAR=1 (before running)"
echo ""
