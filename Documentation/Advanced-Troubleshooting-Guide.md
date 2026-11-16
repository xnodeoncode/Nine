# Advanced Troubleshooting Guide

This guide is for advanced troubleshooting and installation help, usually when you are working with the Nine development team.
Everyday installation, backup and upgrade steps are in the [Quick Start Guide](Quick-Start-Guide.md) and the
[Database Management Guide](Database-Management-Guide.md).

The commands below are for **Linux (AppImage)**. Windows versions of the scripts are planned; see [Windows](#windows).

---

## Where the scripts are

| Where | Folder |
| ----- | ------ |
| In the release download | The `Utilities` folder, next to the AppImage |
| After running the desktop integration installer | `~/Applications/Nine/Utilities/` (the AppImage is in `~/Applications/Nine/`) |
| Inside the application package (fallback) | `resources/bin/Utilities/` |

If you no longer have the scripts, you can read them out of the AppImage without unpacking all of it:

```bash
# Prints the folder where the AppImage is mounted, then waits. Leave it running and copy the files from a second terminal.
./Nine-1.2.0-x86_64.AppImage --appimage-mount
cp /tmp/.mount_Nine-XXXXXX/resources/bin/Utilities/* ~/
```

Press Ctrl+C in the first terminal to unmount. Replace `.mount_Nine-XXXXXX` with the path that was printed.

---

## nine-tool.sh

`nine-tool.sh` is one entry point for the other scripts. It finds them in its own folder, so keep the scripts together. The examples
below are run from the `Utilities` folder; the release download has the AppImage one level up.

```bash
chmod +x *.sh                # only needed once, if the executable bit was lost
./nine-tool.sh help          # list the commands
```

| Command | Runs | What it does |
| ------- | ---- | ------------ |
| `install <AppImage>` | `install-desktop-integration.sh` | Copies the AppImage to `~/Applications/Nine`, adds Nine to the application launcher, and copies the scripts to `~/Applications/Nine/Utilities` |
| `remove [options]` | `nine-removal.sh` | Removes the launcher entry, icon and AppImages; optionally the data and the saved encryption key |
| `diagnose [AppImage]` | `diagnose-startup.sh` | Starts Nine, times how long startup takes, and reports the database files |
| `verify [AppImage] [options]` | `verify-build.sh` | Starts Nine and checks ports, host-header protection and every outbound connection attempt |

Options after the command name are passed straight to the script, for example `./nine-tool.sh remove --dry-run`.

### install

```bash
./nine-tool.sh install ../Nine-1.2.0-x86_64.AppImage
```

Keep the scripts together (the `Utilities` folder already does). The installer copies every script in its folder into
`~/Applications/Nine/Utilities`, so `nine-tool.sh` keeps working from there. The commands find the installed AppImage in
`~/Applications/Nine` without being told.

### remove

```bash
./nine-tool.sh remove --dry-run                       # preview; nothing is changed
./nine-tool.sh remove                                 # launcher entry, icon and AppImages; keeps your data
./nine-tool.sh remove --only Nine-1.2.0-x86_64.AppImage   # one AppImage only; keeps everything else
./nine-tool.sh remove --remove-data                   # also deletes the data folder (all versions)
./nine-tool.sh remove --remove-keychain               # also deletes the saved database password
./nine-tool.sh remove --remove-keychain-only          # deletes only the saved database password
./nine-tool.sh remove --all                           # everything: AppImages, data and saved password
```

- **Back up first.** Copy your database backups to a location outside `~/.config/Nine`. Removing the data deletes them too.
- Deleting data or the saved password asks you to type `DELETE`. The script will not run while Nine is open.
- The data folder and the saved password are shared by every Nine version on the account.
- `./nine-tool.sh remove --help` lists every option.

### diagnose

```bash
./nine-tool.sh diagnose                       # newest AppImage in ~/Applications/Nine
./nine-tool.sh diagnose /path/to/Nine.AppImage
```

It starts Nine, measures how long the .NET backend and the web interface take to come up, lists your database files, and stops the
instance it started. Startup normally takes a few seconds; more than about 20 seconds is worth reporting. The startup log is
written to `/tmp/nine-startup.log`.

### verify

```bash
./nine-tool.sh verify /path/to/Nine-1.2.0-x86_64.AppImage             # normal run checks
./nine-tool.sh verify /path/to/Nine-1.2.0-x86_64.AppImage --network   # also test in an isolated network
```

Use it after building or when asked to by the development team. It lists what it needs, then asks before it starts anything:
`strace`, `ss`, `curl` and `python3`, plus `unshare` and `ip` for `--network`.

It checks that:

- the web interface and the Electron socket start on loopback only;
- requests with a foreign `Host` header are rejected (400) and loopback hosts are accepted (200);
- a normal run logs no Debug lines;
- every outbound connection attempt is listed;
- with `--network`, no packets leave the machine in an isolated network, and Nine also starts with no network at all.

The result is a list of PASS, FAIL and WARN lines and a summary. A warning is not a failure. Two warnings are expected today: the
Electron socket accepts a handshake without a token, and the app makes a route check to a Google IPv6 address that sends no data.

**It starts Nine against your real data folder and only reads. Do not sign in while it runs.** Options: `--network-only`,
`--yes` (skip the prompt) and `--keep` (keep the temporary files). The AppImage is extracted to a temporary folder and run from there
because `strace` cannot follow the normal AppImage mount.

---

## Log files and debug logging

Nine writes its log to the terminal. Start it from a terminal to see it, and use `tee` to keep a copy:

```bash
./Nine-1.2.0-x86_64.AppImage 2>&1 | tee ~/nine.log
```

To see more detail, set a log level in the environment for that run:

```bash
Logging__LogLevel__Nine=Debug ./Nine-1.2.0-x86_64.AppImage 2>&1 | tee ~/nine-debug.log
Logging__LogLevel__Default=Debug ./Nine-1.2.0-x86_64.AppImage 2>&1 | tee ~/nine-debug.log
```

The first shows Nine's own Debug messages, which appear when you use features that log them (for example unlocking an encrypted
database). The second also shows Debug messages from the framework and is much longer.

**Review a log before you send it.** Logs can include names and identifiers you entered, such as tenant names and user IDs.

---

## What to send when asking for help

1. The Nine version (the About page, or the AppImage file name).
2. Your operating system and version.
3. The output of `./nine-tool.sh diagnose`.
4. A log captured as above, after reviewing it.
5. The output of `./nine-tool.sh verify` if the question is about network behavior.

Do not send your database file or backups unless you were asked to.

---

## Windows

Windows versions (`nine-tool.ps1`, `nine-removal.ps1`, `install-desktop-integration.ps1`, `diagnose-startup.ps1`) exist only as
placeholders and make no changes. To uninstall the installer version, use **Add or Remove Programs**. Clean-up of the data folder and
the portable version on Windows is still being documented.
