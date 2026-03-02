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

APPIMAGE="${1:-$HOME/Applications/Nine-1.0.0.AppImage}"

if [ ! -f "$APPIMAGE" ]; then
    echo -e "${RED}Error: AppImage not found at: $APPIMAGE${NC}"
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

# Launch AppImage and capture its PID
"$APPIMAGE" > /tmp/aquiis-startup.log 2>&1 &
APPIMAGE_PID=$!

# Monitor for process start
DOTNET_PID=""
ELAPSED=0
while [ -z "$DOTNET_PID" ] && [ $ELAPSED -lt 60 ]; do
    sleep 0.5
    DOTNET_PID=$(pgrep -P $APPIMAGE_PID | head -1)
    ELAPSED=$((ELAPSED + 1))
done

if [ -n "$DOTNET_PID" ]; then
    DOTNET_START=$(date +%s.%N)
    DOTNET_LAUNCH=$(echo "$DOTNET_START - $START" | bc)
    echo "  .NET process spawned: ${DOTNET_LAUNCH}s"
    
    # Wait for Kestrel to start (check for port 8888)
    KESTREL_READY=0
    KESTREL_ELAPSED=0
    while [ $KESTREL_READY -eq 0 ] && [ $KESTREL_ELAPSED -lt 120 ]; do
        if netstat -tunl 2>/dev/null | grep -q ":8888 " || ss -tunl 2>/dev/null | grep -q ":8888 "; then
            KESTREL_READY=1
            KESTREL_END=$(date +%s.%N)
            KESTREL_TIME=$(echo "$KESTREL_END - $DOTNET_START" | bc)
            echo "  Kestrel listening on port 8888: ${KESTREL_TIME}s"
        fi
        sleep 0.5
        KESTREL_ELAPSED=$((KESTREL_ELAPSED + 1))
    done
    
    if [ $KESTREL_READY -eq 0 ]; then
        echo "  ${RED}Warning: Kestrel did not start within 60s${NC}"
    fi
    
    # Wait for Electron window
    ELECTRON_PID=""
    ELECTRON_ELAPSED=0
    while [ -z "$ELECTRON_PID" ] && [ $ELECTRON_ELAPSED -lt 120 ]; do
        ELECTRON_PID=$(pgrep -f "electron.*aquiis" | head -1)
        if [ -z "$ELECTRON_PID" ]; then
            sleep 0.5
            ELECTRON_ELAPSED=$((ELECTRON_ELAPSED + 1))
        fi
    done
    
    if [ -n "$ELECTRON_PID" ]; then
        ELECTRON_END=$(date +%s.%N)
        ELECTRON_TIME=$(echo "$ELECTRON_END - $START" | bc)
        echo "  Electron window opened: ${ELECTRON_TIME}s"
    fi
    
    # Total startup time
    TOTAL_END=$(date +%s.%N)
    TOTAL_TIME=$(echo "$TOTAL_END - $START" | bc)
    
    echo ""
    echo -e "${GREEN}Startup Timeline:${NC}"
    echo "  1. AppImage mount: ${MOUNT_TIME}s"
    echo "  2. .NET launch: ${DOTNET_LAUNCH}s"
    echo "  3. Kestrel ready: ${KESTREL_TIME}s (at ${KESTREL_END}s total)"
    echo "  4. Window open: ${ELECTRON_TIME}s"
    echo ""
    echo -e "${BLUE}Total startup: ${TOTAL_TIME}s${NC}"
    
    # Kill the test instance
    echo ""
    echo -e "${YELLOW}Stopping test instance...${NC}"
    kill $APPIMAGE_PID 2>/dev/null
    sleep 2
    pkill -9 -f "Nine" 2>/dev/null
else
    echo "  ${RED}Error: .NET process did not start${NC}"
    kill $APPIMAGE_PID 2>/dev/null
fi

echo ""
echo -e "${YELLOW}Test 3: System Information${NC}"
echo "  CPU: $(grep "model name" /proc/cpuinfo | head -1 | cut -d: -f2 | xargs)"
echo "  RAM: $(free -h | awk '/^Mem:/ {print $2}')"
echo "  Disk (AppImage): $(df -h "$APPIMAGE" | tail -1 | awk '{print $1 " (" $4 " free)"}')"
echo "  Disk (Config): $(df -h ~/.config/Nine 2>/dev/null | tail -1 | awk '{print $1 " (" $4 " free)")' || echo "Not initialized"}"

echo ""
echo -e "${YELLOW}Test 4: Database Check${NC}"
if [ -f ~/.config/Nine/Data/app_v1.0.0.db ]; then
    DB_SIZE=$(du -h ~/.config/Nine/Data/app_v1.0.0.db | cut -f1)
    echo "  Database size: $DB_SIZE"
    echo "  Database location: ~/.config/Nine/Data/app_v1.0.0.db"
else
    echo "  Database: Not initialized (first run will be slower)"
fi

echo ""
echo -e "${YELLOW}Test 5: AppImage Properties${NC}"
"$APPIMAGE" --appimage-version 2>/dev/null || echo "  AppImage runtime: Type 2"
echo "  Compression: $(file "$APPIMAGE" | grep -o "gzip\|xz\|zstd" || echo "unknown")"

echo ""
echo -e "${GREEN}Diagnostic complete!${NC}"
echo ""
echo "If startup is still slow (>20s), check:"
echo "  1. Antivirus/security software scanning AppImage"
echo "  2. Slow disk (HDD vs SSD)"
echo "  3. Check /tmp/aquiis-startup.log for .NET errors"
echo "  4. Try: export ELECTRON_NO_ASAR=1 (before running)"
echo ""
