# Nine. - Installation Guide

---

**Version:** 1.2.0  
**Last Updated:** November 16, 2026

---

## Windows

### Installer

Download `Nine-1.2.0-x64-Setup.exe`. Click to run the installer. The application appears in Start Menu.

### Portable

Download `Nine-1.2.0-x64-Portable.exe`. Copy the file to your desired location and double-click to run.

> **Note:** Although the portable app can run from a USB drive, other removable media, or a network drive, the database is stored on the local hard drive. See the Database Management Guide in the `Documentation` folder for details.

---

## Linux

### Install

Download and extract the compressed file that contains the [AppImage for this release](https://github.com/xnodeoncode/Nine/releases/tag/v1.2.0). It extracts to a folder named `Nine-1.2.0-Linux-x86_64` inside the folder you downloaded it to (for example `~/Downloads`). That folder holds `Documentation`, `Install` and this file. The `Install` folder has the AppImage and a `Utilities` folder with install, removal and diagnostic scripts.

Open a terminal in the extracted folder and make the files executable:

```bash
cd ~/Downloads/Nine-1.2.0-Linux-x86_64

chmod +x Install/Utilities/*.sh
chmod +x Install/Nine-1.2.0-x86_64.AppImage
```

**Run Nine without installing it:**

```bash
./Install/Nine-1.2.0-x86_64.AppImage
```

**Or add Nine to your application launcher:**

```bash
./Install/Utilities/nine-tool.sh install Install/Nine-1.2.0-x86_64.AppImage
```

The installer copies the AppImage to `~/Applications/Nine/` (the file you downloaded is left in place) and adds Nine to your application launcher. It also copies the scripts from `Utilities` to `~/Applications/Nine/Utilities/`, so you can use them later to uninstall Nine or to troubleshoot the application.

---

## First Launch

On first launch you will be prompted to create an admin account.
See the [Quick-Start-Guide.md](https://github.com/xnodeoncode/Nine/blob/main/Documentation/Quick-Start-Guide.md) for a full walk-through.

**Windows:** Search for **Nine** in the Start Menu.
**Linux:** Search for **Nine** in your application launcher.

---

## Uninstall

**Windows (Installer):** Use **Add or Remove Programs** in Windows Settings.

**Windows (Portable):** Delete the `Nine-1.2.0-x64-Portable.exe` file.

**Linux:**

Use the removal script from the `Utilities` folder (a copy is also in `~/Applications/Nine/Utilities/` after installing). It undoes the desktop integration, and removes your data and database encryption key only when you ask it to.

```bash
cd ~/Applications/Nine
chmod +x Utilities/*.sh
./Utilities/nine-tool.sh remove --help
```

Common uses:

```bash
cd ~/Applications/Nine
./Utilities/nine-tool.sh remove --dry-run   # preview; nothing is changed
./Utilities/nine-tool.sh remove             # remove the launcher entry, icon and AppImages; keep your data
./Utilities/nine-tool.sh remove --all       # remove everything, including all data and the encryption key (permanent)
```

Back up your database to a location outside of Nine first. See the [Database Management Guide](https://github.com/xnodeoncode/Nine/blob/main/Documentation/Database-Management-Guide.md).

---

## Troubleshooting

For startup timing, build verification and log capture, see [Advanced Troubleshooting Guide](https://github.com/xnodeoncode/Nine/blob/main/Documentation/Advanced-Troubleshooting-Guide.md).
