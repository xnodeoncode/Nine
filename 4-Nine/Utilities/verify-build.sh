#!/bin/bash
#
# Nine Build Verification
# Runs the packaged Linux AppImage and checks the behavior that matters after every build:
# ports, host-header protection, the Electron socket, log quietness, listeners on loopback only,
# and every outbound connection attempt. Optionally repeats the run in an isolated network to
# capture what would leave the machine, and once more with no network at all.
#
# Usage:
#   ./verify-build.sh [path-to-appimage] [options]
#   ./nine-tool.sh verify [path-to-appimage] [options]
#
# Options:
#   --network     Also run in a private network namespace: once with a black-hole route, capturing all
#                 outbound packets, and once with no route at all (offline launch). Needs
#                 unprivileged user namespaces and ip.
#   --network-only  Run only the isolated-network tests (skips the normal run).
#   --yes         Do not ask for confirmation.
#   --keep        Keep the extracted copy and the logs (printed at the end).
#   -h, --help    Show this help.
#
# Notes:
#   - It STARTS the app against your real data folder (~/.config/Nine). It only reads; do not sign in.
#   - The AppImage is extracted and run from the extracted folder, because strace cannot trace the
#     FUSE mount. The contents are the same as the packaged build.
#   - Needs: strace, ss, curl, python3. Refuses to run while another Nine instance is open.
#

set -u

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; NC='\033[0m'

APPIMAGE=""
DO_NETWORK=0
SKIP_NORMAL=0
ASSUME_YES=0
KEEP=0
INNER=0

while [ $# -gt 0 ]; do
    case "$1" in
        --network) DO_NETWORK=1 ;;
        --network-only) DO_NETWORK=1; SKIP_NORMAL=1 ;;
        --yes) ASSUME_YES=1 ;;
        --keep) KEEP=1 ;;
        --inner-network) INNER=1 ;;
        -h|--help) sed -n '3,27p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        -*) echo -e "${RED}Error: unknown option: $1${NC}"; exit 1 ;;
        *) APPIMAGE="$1" ;;
    esac
    shift
done

PASS=0; FAIL=0; WARN=0
# The isolated-network run reports through a counts file so the outer run can add it to its totals.
count() { [ $INNER -eq 1 ] && echo "$1" >> "$WORK/counts"; }
ok()   { PASS=$((PASS + 1)); count P; echo -e "  ${GREEN}PASS${NC}  $*"; }
bad()  { FAIL=$((FAIL + 1)); count F; echo -e "  ${RED}FAIL${NC}  $*"; }
warn() { WARN=$((WARN + 1)); count W; echo -e "  ${YELLOW}WARN${NC}  $*"; }
info() { echo "        $*"; }

# ------------------------------------------------------------------------------------------
# Shared helpers (used by the outer run and by the isolated-network run)
# ------------------------------------------------------------------------------------------
start_wait() {
    # $1 = log file; remaining = command prefix and app. Sets DOTNET_PID, EPORT, WEB.
    local log="$1"; shift
    "$@" > "$log" 2>&1 &
    WRAPPER_PID=$!
    DOTNET_PID=""
    for _ in $(seq 1 240); do
        DOTNET_PID=$(pgrep -f "$ROOT/resources/bin/Nine /electronPort" | head -1)
        [ -n "$DOTNET_PID" ] && break; sleep 0.5
    done
    [ -z "$DOTNET_PID" ] && return 1
    EPORT=$(tr '\0' ' ' < "/proc/$DOTNET_PID/cmdline" | grep -o "electronPort=[0-9]*" | cut -d= -f2)
    WEB=""
    for _ in $(seq 1 240); do
        WEB=$(ss -ltnpH 2>/dev/null | grep "pid=$DOTNET_PID," | head -1 | awk '{print $4}'); WEB=${WEB##*:}
        [ -n "$WEB" ] && break; sleep 0.5
    done
    [ -n "$WEB" ]
}

stop_app() {
    local epid
    epid=$(pgrep -f "^$ROOT/Nine( |\$)" | head -1)
    [ -n "$epid" ] && kill "$epid" 2>/dev/null
    for _ in $(seq 1 20); do pgrep -f "$ROOT/resources/bin/Nine /electronPort" > /dev/null || break; sleep 0.5; done
    pgrep -f "$ROOT/resources/bin/Nine /electronPort" > /dev/null && { kill "$DOTNET_PID" 2>/dev/null; sleep 2; }
    kill "$WRAPPER_PID" 2>/dev/null; sleep 2
    local left; left=$(pgrep -fc "$ROOT")
    if [ "$left" -eq 0 ]; then ok "no processes left running after shutdown"; else bad "$left process(es) left running"; fi
}

ui_checks() {
    local code=000
    for _ in $(seq 1 120); do
        code=$(curl -s -o /dev/null -w "%{http_code}" "http://localhost:$WEB/" 2>/dev/null); [ "$code" != "000" ] && break; sleep 0.5
    done
    [ "$code" = "200" ] && ok "web UI answers (http://localhost:$WEB/ -> 200)" || bad "web UI returned $code"
}

host_checks() {
    local code
    for h in "evil.example.com" "evil.example.com:$WEB" "localhost.evil.com" "127.0.0.1.nip.io"; do
        code=$(curl -s -o /dev/null -w '%{http_code}' -H "Host: $h" "http://127.0.0.1:$WEB/")
        [ "$code" = "400" ] && ok "Host: $h -> 400" || bad "Host: $h -> $code (expected 400)"
    done
    for h in "localhost:$WEB" "127.0.0.1:$WEB"; do
        code=$(curl -s -o /dev/null -w '%{http_code}' -H "Host: $h" "http://127.0.0.1:$WEB/")
        [ "$code" = "200" ] && ok "Host: $h -> 200" || bad "Host: $h -> $code (expected 200)"
    done
    code=$(curl -s -o /dev/null -w '%{http_code}' -H "Host: [::1]:$WEB" "http://[::1]:$WEB/")
    [ "$code" = "200" ] && ok "Host: [::1]:$WEB over ::1 -> 200" || bad "Host: [::1]:$WEB over ::1 -> $code (expected 200)"
}

if [ $INNER -eq 0 ]; then
    # ----------------------------------------------------------------------------------
    # Outer run
    # ----------------------------------------------------------------------------------
    if [ -z "$APPIMAGE" ]; then
        APPIMAGE="$(ls "$HOME"/Applications/Nine/Nine-*-x86_64.AppImage 2>/dev/null | sort -V | tail -1)"
    fi
    if [ -z "$APPIMAGE" ] || [ ! -f "$APPIMAGE" ]; then
        echo -e "${RED}Error: AppImage not found. Usage: $0 [path-to-appimage] [options]${NC}"; exit 1
    fi
    if pgrep -f '\.mount_Nine|resources/bin/Nine' > /dev/null 2>&1; then
        echo -e "${RED}Nine is running. Close all Nine windows and run this script again.${NC}"; exit 1
    fi
    echo -e "${GREEN}Nine Build Verification${NC}"
    echo "========================================"
    echo "AppImage: $APPIMAGE"
    echo ""
    echo "Dependencies:"
    missing=""
    dep() {
        # $1 = command, $2 = what it is used for
        if command -v "$1" > /dev/null 2>&1; then
            echo -e "  ${GREEN}found${NC}    $1  ($2)"
        else
            echo -e "  ${RED}MISSING${NC}  $1  ($2)"; missing="$missing $1"
        fi
    }
    dep strace  "traces every connect() call the app makes"
    dep ss      "reads the ports the app listens on"
    dep curl    "sends the host-header and web UI requests"
    dep python3 "analyzes the trace and captures packets"
    if [ $DO_NETWORK -eq 1 ]; then
        dep unshare "creates the private network namespace (--network)"
        dep ip      "configures the isolated network (--network)"
    fi
    if [ -n "$missing" ]; then
        echo ""
        echo -e "${RED}Missing:${missing}${NC}"
        echo "Install them with your package manager, for example:"
        echo "  Fedora / RHEL 10:  sudo dnf install strace iproute curl python3 util-linux"
        echo "  Debian / Ubuntu:   sudo apt install strace iproute2 curl python3 util-linux"
        exit 1
    fi
    echo ""
    echo "What it does: extracts the AppImage to a temporary folder, starts the app against your real"
    echo "data folder (~/.config/Nine), checks it, and stops it. It only reads. Do not sign in while it runs."
    if [ $ASSUME_YES -eq 0 ]; then
        if [ ! -t 0 ]; then echo -e "${RED}Run in a terminal or pass --yes${NC}"; exit 1; fi
        read -r -p "Proceed? [y/N] " reply; [[ "$reply" =~ ^[Yy]$ ]] || { echo "Cancelled."; exit 0; }
    fi

    WORK="$(mktemp -d)"
    ROOT="$WORK/squashfs-root"
    cleanup() { [ $KEEP -eq 1 ] && echo "Kept: $WORK" || rm -rf "$WORK"; }
    trap cleanup EXIT

    echo; echo "Extracting the AppImage (no FUSE needed)..."
    (cd "$WORK" && "$APPIMAGE" --appimage-extract > /dev/null 2>&1)
    [ -x "$ROOT/AppRun" ] || { echo -e "${RED}Extraction failed${NC}"; exit 1; }

    export ROOT WORK
    # AppRun finds its directory from its first argument, which breaks when it is run directly with
    # options; the AppImage runtime normally sets APPDIR itself.
    export APPDIR="$ROOT"
    if [ $SKIP_NORMAL -eq 0 ]; then
    echo; echo "== 1. Normal run, tracing every connect() call"
    if start_wait "$WORK/app.log" strace -f -e trace=socket,connect -o "$WORK/strace.log" "$ROOT/AppRun"; then
        ok ".NET backend started (web port $WEB, Electron socket port ${EPORT:-?})"
        ui_checks
        host_checks

        hs=$(curl -s -m 5 "http://[::1]:$EPORT/socket.io/?EIO=4&transport=polling" 2>/dev/null)
        if [[ "$hs" == 0\{\"sid\"* ]]; then
            warn "Electron socket handshake returns a session with no token (known, O1)"
        else
            ok "Electron socket handshake did not return a session"
        fi

        bad_listen=$(ss -ltnpH 2>/dev/null | grep -E "pid=$DOTNET_PID,|:$EPORT " | awk '{print $4}' | grep -vE '^(127\.0\.0\.1|\[::1\]):' | tr '\n' ' ')
        [ -z "$bad_listen" ] && ok "all listeners are on loopback" || bad "non-loopback listeners: $bad_listen"

        info "idling 15 seconds to catch background connections..."
        sleep 15
    else
        bad "the app did not start (see $WORK/app.log)"; head -5 "$WORK/app.log"
    fi
    stop_app

    echo; echo "== 2. Log (normal run, Information level)"
    dbg=$(grep -c '^dbug' "$WORK/app.log"); [ "$dbg" -eq 0 ] && ok "no Debug lines" || bad "$dbg Debug lines in a normal run"
    old=$(grep -c 'KeychainService\]' "$WORK/app.log"); [ "$old" -eq 0 ] && ok "no old keychain console lines" || bad "$old old keychain console lines"
    grep -q "Backend health check: OK" "$WORK/app.log" && ok "backend health check OK" || bad "backend health check not OK"

    echo; echo "== 3. Outbound connection attempts (loopback and unix sockets excluded)"
    ( python3 - "$WORK/strace.log" <<'PY'
import re, sys
lines = open(sys.argv[1]).read().splitlines()
socks = {}
for l in lines:
    m = re.match(r'(\d+)\s+socket\((\w+), ([\w|]+), (\w+)\)\s+= (\d+)', l)
    if m: socks[(m.group(1), m.group(5))] = m.group(3).split('|')[0]
seen = {}
resolver = 0
for l in lines:
    if 'AF_UNIX' in l and re.search(r'resolve|nscd|avahi', l, re.I): resolver += 1
    m = re.match(r'(\d+)\s+connect\((\d+), \{sa_family=(AF_INET6?),.*?(?:inet_addr\("([^"]+)"\)|inet_pton\(AF_INET6, "([^"]+)")', l)
    if not m: continue
    pid, fd = m.group(1), m.group(2); addr = m.group(4) or m.group(5)
    if addr.startswith('127.') or addr in ('::1', '::ffff:127.0.0.1') or addr in ('0.0.0.0', '::'): continue
    port = re.search(r'htons\((\d+)\)', l).group(1)
    res = l.rsplit(' = ', 1)[-1][:34]
    seen[(addr, port, socks.get((pid, fd), '?'), res)] = 1
print("RESOLVER=%d" % resolver)
for (a, p, t, r) in sorted(seen): print("CONNECT %s:%s %s %s" % (a, p, t, r))
PY
    ) > "$WORK/conn.txt" 2>&1
    res=$(grep '^RESOLVER=' "$WORK/conn.txt" | cut -d= -f2)
    [ "${res:-0}" -eq 0 ] && ok "no connections to name-resolution services (no hostname lookups)" || warn "$res connection(s) to name-resolution services"
    n=$(grep -c '^CONNECT' "$WORK/conn.txt")
    if [ "$n" -eq 0 ]; then
        ok "no connection attempts to any non-loopback address"
    else
        warn "$n connection attempt(s) to non-loopback addresses:"
        grep '^CONNECT' "$WORK/conn.txt" | sed 's/^CONNECT /        /'
    fi
    fi

    if [ $DO_NETWORK -eq 1 ]; then
        echo; echo "== 4. Isolated network: black-hole route with packet capture, then no network at all"
        unshare -Urn bash "$0" --inner-network --yes >> "$WORK/inner.txt" 2>&1 < /dev/null
        cat "$WORK/inner.txt"
        if [ -f "$WORK/counts" ]; then
            PASS=$((PASS + $(grep -c '^P' "$WORK/counts"))); FAIL=$((FAIL + $(grep -c '^F' "$WORK/counts"))); WARN=$((WARN + $(grep -c '^W' "$WORK/counts")))
        else
            bad "the isolated-network run produced no results (are unprivileged user namespaces enabled?)"
        fi
    fi

    echo; echo "========================================"
    echo "Result: $PASS passed, $FAIL failed, $WARN warning(s)"
    [ $FAIL -eq 0 ]
    exit $?
fi

# ------------------------------------------------------------------------------------------
# Inner run: executes inside a private user and network namespace (set up by --network)
# ------------------------------------------------------------------------------------------
: "${ROOT:?}"; : "${WORK:?}"
CAP="$WORK/capture.txt"

ip link set lo up
ip link add d0 type dummy; ip link set d0 address 02:00:00:00:00:02; ip link set d0 up
ip addr add 192.0.2.2/24 dev d0; ip -6 addr add 2001:db8::2/64 dev d0 nodad
ip neigh add 192.0.2.1 lladdr 02:00:00:00:00:01 dev d0 nud permanent
ip -6 neigh add 2001:db8::1 lladdr 02:00:00:00:00:01 dev d0 nud permanent
ip route add default via 192.0.2.1; ip -6 route add default via 2001:db8::1

# Packet capture without tcpdump (tcpdump cannot drop privileges inside an unprivileged user namespace).
# Writes one line per outgoing IPv4/IPv6 TCP, UDP or ICMP packet: "PROTO destination port flags".
cat > "$WORK/sniff.py" <<'PY'
import socket, struct, sys
s = socket.socket(socket.AF_PACKET, socket.SOCK_RAW, socket.htons(3))
s.bind(('d0', 0))
out = open(sys.argv[1], 'a', buffering=1)
while True:
    pkt, addr = s.recvfrom(65535)
    if addr[2] != 4:                       # outgoing packets only
        continue
    et = struct.unpack('!H', pkt[12:14])[0]
    if et == 0x0800:
        ihl = (pkt[14] & 0x0f) * 4
        proto = pkt[23]; dst = socket.inet_ntoa(pkt[30:34]); body = pkt[14 + ihl:]
    elif et == 0x86dd:
        proto = pkt[20]; dst = socket.inet_ntop(socket.AF_INET6, pkt[38:54]); body = pkt[54:]
        if proto == 58 and body[0] in range(130, 144):   # neighbor discovery and multicast listener noise
            continue
    else:
        continue
    if proto == 6:
        port = struct.unpack('!H', body[2:4])[0]; out.write("TCP %s %d flags=0x%02x\n" % (dst, port, body[13]))
    elif proto == 17:
        port = struct.unpack('!H', body[2:4])[0]; out.write("UDP %s %d\n" % (dst, port))
    else:
        out.write("PROTO%d %s\n" % (proto, dst))
PY
python3 "$WORK/sniff.py" "$CAP" &
SNIFF_PID=$!
sleep 1

# Positive control: traffic we generate ourselves must show up in the capture, otherwise an empty
# capture would mean nothing.
curl -s -m 2 -o /dev/null http://198.51.100.7/ 2>/dev/null
curl -s -6 -m 2 -o /dev/null "http://[2001:db8:ffff::1]/" 2>/dev/null
sleep 1
if grep -q '198\.51\.100\.7' "$CAP" && grep -q '2001:db8:ffff::1' "$CAP"; then
    ok "packet capture works (test packets to 198.51.100.7 and 2001:db8:ffff::1 were seen)"
else
    bad "packet capture is not working; the results below cannot be trusted"
fi

echo "  -- 4a. Black-hole network (every outbound packet is captured on a dummy interface)"
# --no-sandbox: the namespace maps the user to root, which Chromium refuses to run as with its sandbox.
NET_STARTED=0
if start_wait "$WORK/app-net.log" "$ROOT/AppRun" --no-sandbox; then
    NET_STARTED=1
    ok "app started in the isolated network (web port $WEB)"
    ui_checks
    sleep 15
else
    bad "the app did not start in the isolated network (see $WORK/app-net.log)"; head -5 "$WORK/app-net.log"
fi
stop_app
sleep 1; kill "$SNIFF_PID" 2>/dev/null; sleep 1

# Everything except our own positive-control packets is traffic the app (or its helpers) tried to send.
out=$(grep -vE '198\.51\.100\.7|2001:db8:ffff::1' "$CAP")
if [ $NET_STARTED -eq 0 ]; then
    warn "packet capture not evaluated because the app did not start"
elif [ -z "$out" ]; then
    ok "no packets left the machine (nothing captured on the outbound interface)"
else
    warn "packets captured on the outbound interface (count, protocol, destination, port):"
    echo "$out" | sort | uniq -c | sort -rn | head -15 | sed 's/^/       /'
fi

echo "  -- 4b. No network at all (offline launch)"
ip route del default 2>/dev/null; ip -6 route del default 2>/dev/null; ip link set d0 down
if start_wait "$WORK/app-offline.log" "$ROOT/AppRun" --no-sandbox; then
    ok "app started with no network (web port $WEB)"
    ui_checks
    sleep 5
    grep -q "Backend health check: OK" "$WORK/app-offline.log" && ok "backend health check OK offline" || bad "backend health check not OK offline"
else
    bad "the app did not start offline"
fi
stop_app
exit 0
