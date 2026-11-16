# Nine. - Quick Start Guide

**Version:** 1.2.0  
**Last Updated:** November 12, 2026  
**Estimated Time:** 15 minutes

---

## Welcome

This guide gets you from a fresh install to your first invoice and payment. By the end you will have:

- [Installing the application](#step-1-installation-5-minutes)
- [Create an account and organization](#step-2-create-your-account-and-organization-2-minutes)
- [Add a property](#step-3-add-your-first-property-3-minutes)
- [Add a tenant](#step-4-add-a-tenant-2-minutes)
- [Create a lease](#step-5-create-a-lease-2-minutes)
- [Create an invoice](#step-6-create-an-invoice-1-minute)
- [Record a payment](#step-7-record-a-payment-1-minute)
- [Record a property inspection](#step-8-record-an-inspection-3-minutes)
- [Uninstalling Nine](#uninstalling-nine)

Let's get started.

---

## Prerequisites

Before you begin, ensure you have:

- **Operating System:**
  - Linux, 64-bit (x86_64). Tested on Red Hat Enterprise Linux 10 with GNOME; see [AppImage Requirements](#appimage-requirements), OR
  - Windows 10/11 (64-bit)
- **Hardware:**
  - 8 GB RAM minimum (16 GB recommended)
  - 2 GB disk space
- **Download:** [Nine v1.2.0](https://github.com/xnodeoncode/Nine/releases/tag/v1.2.0) for your platform

### AppImage Requirements

**Nine as an AppImage.** The Nine AppImage has been tested on Red Hat Enterprise Linux 10 with the GNOME desktop. No installation is required: download, make executable (using `chmod +x Nine-1.2.0-x86_64.AppImage`), and run.

The AppImage format is designed to run on most Linux distributions. The AppImage bundles Nine and its runtime, but it still requires the following on your system:

- **A 64-bit (x86_64) Linux system with a recent C library (glibc).**
- **FUSE,** which AppImages use to start. Some distributions do not install it by default. If the AppImage will not start, install your distribution's FUSE package, or run it with `./Nine-1.2.0-x86_64.AppImage --appimage-extract-and-run`.
- **A graphical desktop with the common GTK and graphics libraries** that Electron applications use. Most desktop distributions already include them.
- **A Secret Service provider (for example, GNOME Keyring or KWallet)** to store the database password securely.

**Desktop integration script.** The optional `nine-tool.sh` script has been tested on RHEL 10 with GNOME, so not guaranteed to work on all distros. It writes to the standard locations (`~/.local/share/applications` and `~/.local/share/icons`). If it does not work for you, run the AppImage directly.

### Windows Portable

**Nine as a portable Windows executable (.exe).** The Nine portable exe file has been tested to run on Windows 11. No installation is required. Download the portable exe file, store it in a location of choice on your Windows 11 computer, and double-click the file to run.

### Windows Installer

**Using Windows Installer.** Nine can be installed using the Windows installer. This adds the application to the Windows Add/Remove programs list of programs and the Windows Start Menu. Optional desktop and task bar shortcuts can be created for ease of access.

## Step 1: Installation (5 minutes)

### Linux Installation Steps

**Option A: Manual AppImage copy**

```bash
# 1. Download the file
# File: Nine-1.2.0-x86_64.AppImage

# 2. Make it executable
chmod +x Nine-1.2.0-x86_64.AppImage

# 3. Run the application
./Nine-1.2.0-x86_64.AppImage
```

The application start-up displaying the start-up log in the terminal.

**Option B: Desktop Integration Option**

Nine can be integrated into your GNOME desktop environment.

1. Download the file: currently Nine-1.2.0-x86_64.AppImage
2. Download the installer script, `install-desktop-integration.sh`, and put it in the same folder as the AppImage. The release also has a `Utilities` folder with the other scripts (`nine-tool.sh`, `nine-removal.sh` and more). If you keep them in the same folder as the installer, it copies them to `~/Applications/Nine/Utilities` so you can use them later to uninstall Nine.
3. Make the files executable

```bash
chmod +x Nine-1.2.0-x86_64.AppImage
chmod +x install-desktop-integration.sh
```

4. Execute the script

```bash
./install-desktop-integration.sh Nine-1.2.0-x86_64.AppImage
```

The install script is expected to:

1. Copy the AppImage to the `~/Applications/Nine` directory (it creates the directory if it doesn't exist). The original file is left where it was.
2. Copy every script it finds in its own folder (the installer itself, the removal script, `nine-tool.sh` and the others) to `~/Applications/Nine/Utilities` and make the `.sh` files executable. If the removal script or `nine-tool.sh` is not found next to the installer, it shows a warning and continues.
3. Create a desktop entry file which adds Nine to the Launch Menu.

### Windows Installation Steps

**Option A: Installer (recommended)**

1. Download `Nine-1.2.0-x64-Setup.exe`
2. Double-click the installer
3. Follow the wizard (accept the license, choose the installation folder, create a desktop shortcut)
4. Launch Nine from the Start Menu or desktop shortcut

**Option B: Portable executable (no installation)**

1. Download `Nine-1.2.0-x64-Portable.exe`
2. Place it in the folder you want to run it from (for example `C:\Nine\`)
3. Double-click to run

**Checkpoint:** The Nine window opens and shows the **Register** page.

<!-- VERIFY: first launch on a clean profile. Confirm the first screen is Register, and that no database password prompt appears before it. -->

---

## Step 2: Create an Account and Organization (2 minutes)

The first time Nine starts, there are no accounts, so the **Register** page shows a single form that creates both your organization and your account. Registration is only available for this first account.

1. Fill in **Organization Name** (for example "ABC Property Management" or "John Smith Rentals").
2. Choose your **State**.
3. Enter your **Email** (this is your login), **First Name** and **Last Name**.
4. Enter and confirm a **Password**. It must be at least **12 characters** and include an uppercase letter, a lowercase letter, a number and a special character.
5. Click **Register**.

You are the organization's owner with full administrative privileges. The account is confirmed automatically; there is no email verification step.

**Checkpoint:** You are signed in and see the **Property Management Dashboard**. If not, click Log-In in the left navigation or Sign-In in the center of the page and log in to the application.

### The Dashboard

The left navigation menu contains **Dashboard, Calendar, Properties, Tenants, Leases, Inspections, Repairs, Documents** and **Reports**. Owners and Administrators also see **Application** and **Organization** as two top-most navigation links.

The dashboard shows property metrics, **Outstanding Invoices**, **Outstanding Repairs**, **Upcoming Lease Renewals** and **Available Properties**.

<!-- VERIFY: first-run message wording and whether the dashboard shows a sample-data option on a new database. NO SAMPLE DATA IS EXPLICITLY ADDED VIA ORGANIZATION SETTINGS-->

---

## Step 3: Add Your First Property (3 minutes)

1. Click **Properties** in the left menu.
2. Click **Add Property**.
3. Fill in the form:
   - **Address** (street address) and optional **Unit Number**
   - **City**, **State** and **Postal Code**
   - **Property Type:** House, Apartment, Condo, Townhouse, Duplex, Commercial or Other
   - **Monthly Rent**
   - **Status:** choose **Available** for a property that is ready to rent
   - Optional: **Bedrooms**, **Bathrooms**, **Square Feet** and **Description**
4. Click **Create Property**.

**Checkpoint:** The property appears in the property list.

You can record expenses for a property from the panel beside the form (or later from the property page).

---

## Step 4: Add a Tenant (2 minutes)

1. Click **Tenants** in the left menu.
2. Click **Add Tenant**.
3. Fill in the form:
   - **First Name**, **Last Name** and **Email** (required)
   - Optional: **Phone Number**, **Date of Birth**, **Identification Number**
   - Optional: **Emergency Contact Name** and **Emergency Contact Phone**
   - Optional: **Notes**
4. Click **Create Tenant**.

**Checkpoint:** The tenant appears in the tenant list.

---

## Step 5: Create a Lease (2 minutes)

1. Click **Leases** in the left menu.
2. Click **Add Lease**.
3. Fill in the form:
   - **Property** and **Tenant** (select from the dropdowns)
   - **Start Date** and **End Date** (defaulted to current date and 12 months later respectively.)
   - **Monthly Rent** and **Security Deposit**
   - **Lease Type:** Term, Month-to-Month
   - **Status:** new leases default to **Pending**; choose **Active** if the lease is ready to start.
   - Optional: **Lease Terms** and **Notes** (add the terms of the lease and any additional notes. Lease term regulations can differ from region to region so enter the text relevant for your state, county, region, etc.)
4. Click **Create Lease**.

You are taken to the property page. Open the lease from **Leases** to see its details.

**Checkpoint:** The lease appears in the lease list.

<!-- VERIFY: when a lease becomes Active, does the property change to Occupied? (LeaseWorkflowService.ActivateLeaseAsync sets it, but no button was found that calls it, and the create form sets the status directly.) Update this step and the "Status" bullet to match. -->

### Generate the Lease PDF (optional)

1. Open the lease from the lease list.
2. Click **Generate PDF**.
3. Use **View PDF** or **Download PDF**.

### Lease actions

The lease page also lets you **Edit Lease**, **Create Invoice**, **View Invoices** and **View Documents**, and provides actions for renewals, termination notices, early termination, move-out and converting to month-to-month.

---

## Step 6: Create an Invoice (1 minute)

Invoices and payments do not appear in the left menu. Start from the lease:

1. Open the lease.
2. Click **Create Invoice** (this opens the invoice form with the lease selected and fields populated by default using lease details).
3. Fill in the form:
   - **Invoice Number** and **Invoice Date** (Invoice number is autogenerated and should not be modified. Invoice date defaults to current date, but can be modified as needed.)
   - **Due Date** (keep the default, five days from current date or enter a new due date)
   - **Lease** (already selected)
   - **Description** (for example "First month's rent" or keep the default)
   - **Amount** (enter amount or keep the default which is based on the lease agreement)
   - **Status:** Pending (A new invoice is pending by default. When a payment is recorded, the invoice's payment status is updated)
   - Optional: **Notes**
4. Click **Create Invoice**.

You can find all invoices later from the lease's **View Invoices** button, from **Outstanding Invoices** on the dashboard, or from the **Documents** page.

**Checkpoint:** The invoice shows status **Pending** with your due date.

<!-- VERIFY: whether Invoice Number is auto-generated, and how the Amount is filled in when the lease is selected. INVOICE NUMBER IS AUTOGENERATED AND THE INVOICE AMOUNT IS PULLED FROM THE LEASE AGREEMENT MONTHLY RENT AMOUNT-->

---

## Step 7: Record a Payment (1 minute)

1. Open the invoice.
2. Click **Record Payment**.
3. Fill in the form:
   - **Invoice** (already selected)
   - **Payment Date** (defaults to current date)
   - **Amount** (defaults to the invoice amount. Partial payments may be accepted.)
   - **Payment Method:** Online Payment, Debit Card, Credit Card, Check, Cash, Bank Transfer or Other (Online payment is the default)
   - Optional: **Notes**
4. Click **Record Payment**.

**Checkpoint:** Open the invoice again; it should show the payment.

<!-- VERIFY: that recording a full payment changes the invoice status to Paid and updates the paid amount. VERIFIED-->

### Payment receipt (optional)

Open the payment and use **Generate Receipt**, then **View Receipt** or **Download Receipt**.

---

## Step 8: Record an Inspection (3 minutes)

The **Inspections** page is a routine inspection schedule by property: it shows which properties are scheduled, due soon, overdue or not yet scheduled. When a property is created, a routine inspection is automatically scheduled due within the first 90 days. Completion of an inspections automatically schedules another inspection for 12 months later. In essence, routine inspections are auto-generated and scheduled annually.

1. Click **Inspections** in the left menu.
2. On the property you want, click the clipboard icon (**Create Inspection**). You can also start from the property's own page.
3. Fill in **Inspection Details:**
   - **Inspection Date**
   - **Inspection Type:** Routine, Move-In, Move-Out or Maintenance
   - **Inspected By**
4. Work through the checklist sections: **Exterior, Interior, Kitchen, Bathroom** and **Systems**. Mark each item as good or not good and add notes for any issues.
5. Set the **Overall Condition** (Excellent, Good, Fair or Poor) and add **General Notes** and **Action Items Required**.
6. Click **Save Inspection**.

**Checkpoint:** The inspection appears on the property's inspection history.

Open the saved inspection to use **Generate PDF**, then **View PDF** or **Download PDF**.

<!-- VERIFY: where the saved inspection is listed (property page vs. Inspections page), and that the Systems checklist section is titled "Systems". -->

---

## Congratulations

You have completed the Quick Start Guide. You have:

- Installed Nine
- Created your account and organization
- Added a property
- Added a tenant
- Created a lease
- Created an invoice
- Recorded a payment
- Recorded an inspection

---

## What's Next

### Explore more features

- **Properties:** track expenses, upload documents, and mark properties as Under Renovation or Off Market.
- **Repairs:** record and track repair work and costs per property.
- **Calendar:** see events in a monthly calendar or list view.
- **Documents:** find lease PDFs, invoices and receipts in one place.
- **Reports:** Income Statement, Rent Roll, Property Performance and Tax Report.
- **Notifications:** use the notification bell and set your preferences under **Notifications**.
- **Late fees:** configure under **Application** (see Settings below).

<!-- VERIFY: the following older claims were removed because no matching UI was found: Prospect-to-Tenant workflow, recurring rent invoices, security deposit dividends UI, vendor assignment, email/SMS configuration. Re-add any that exist. -->

---

## Uninstalling Nine

**IMPORTANT: BEFORE GETTING STARTED CREATE A DATABASE BACKUP AND DOWNLOAD/COPY IT TO A LOCATION OUTSIDE OF THE APPLICATION.**

An encrypted or an unencrypted backup can be generated and downloaded to the local hard drive, a USB drive, or a cloud storage location.

**SEE THE [Database Management Guide](https://github.com/xnodeoncode/Nine/blob/main/Documentation/Database-Management-Guide.md)**

**BEFORE YOU BEGIN, CLOSE ALL RUNNING INSTANCES OF NINE.**

### Linux

You can use the removal script (recommended) or follow the manual steps below it.

#### Using the removal script

`nine-removal.sh` is in the `Utilities` folder of the Linux download, next to `install-desktop-integration.sh`. If you used the desktop integration script, it was also copied to `~/Applications/Nine/Utilities`, so you can run it from there. It undoes what the desktop integration script did, and removes your data and encryption key only when you ask it to.

```bash
chmod +x nine-removal.sh

# Preview what would be removed; nothing is changed
./nine-removal.sh --dry-run

# Remove one version only (Option 1 below); data and encryption key are kept
./nine-removal.sh --only Nine-1.2.0-x86_64.AppImage

# Remove the launcher entry, icon and AppImages; data and encryption key are kept
./nine-removal.sh

# Remove the launcher entry, icon, AppImage and data; encryption key is kept
./nine-removal.sh --remove-data

# Remove the launcher entry, icon, AppImage and encryption key; data is kept
./nine-removal.sh --remove-keychain

# Remove the encryption key only; the application, database, and configuration files are kept
./nine-removal.sh --remove-keychain-only

# Remove everything, including all data and the encryption key (Option 2 below)
./nine-removal.sh --all
```

- The script asks before it removes anything. Removing the data or the encryption key also requires typing `DELETE`.
- `--all` is the same as `--remove-data --remove-keychain`. Use those two options separately to remove only your data or only the encryption key.
- It will not run while Nine is open.
- It only knows about AppImages in `~/Applications/Nine`. Delete any AppImage stored elsewhere yourself.
- Run `./nine-removal.sh --help` for all options.

#### Manual steps

Choose the option that matches what you want to remove.

#### Option 1: Remove one version only

Use this when you keep another version of Nine installed. Your data stays in place.

1. Delete that version's AppImage: in your file manager, right-click it and choose "Delete", "Delete Permanently", or "Move to Trash". If the desktop integration script was used, the AppImages are in `~/Applications/Nine`. From the terminal:

```bash
rm ~/Applications/Nine/<appimage-name>   # for example Nine-1.2.0-x86_64.AppImage
```

2. If the Nine launcher entry pointed to the version you removed, it no longer works. Run `install-desktop-integration.sh` again with the version you kept to fix it.

**Do not delete `~/.config/Nine` or the keychain entry in this case.** They hold the databases and the database encryption key for **every** version of Nine on this computer.

#### Option 2: Remove Nine completely

Use this when you no longer need Nine or any of its data. **This permanently deletes the databases and backups of all versions.**

1. Delete the AppImage files. Delete them in your file manager if they are stored somewhere other than `~/Applications/Nine`.

2. Open the terminal and run the commands below.

```bash
# Remove application support, configuration, resource and database files
# (the default location; it is $XDG_CONFIG_HOME/Nine if that variable is set)
rm -rf ~/.config/Nine

# RUN THESE COMMANDS IF THE DESKTOP INTEGRATION SCRIPT WAS USED.

# Remove all installed releases and the scripts copied to the Utilities folder
rm -rf ~/Applications/Nine

# Remove the desktop integration entry (GNOME)
rm ~/.local/share/applications/nine.desktop

# Remove the desktop icon file
rm ~/.local/share/icons/hicolor/512x512/apps/nine.png

# Refresh the desktop database
update-desktop-database ~/.local/share/applications
```

3. Only if you ever turned on database encryption, remove the stored encryption key. This key is shared by every version of Nine on this account. Run the command only when you no longer need any encrypted database or encrypted backup, because they cannot be opened without it.

```bash
secret-tool clear key-type database-encryption-Nine-Electron
```

### Windows

## Additional Resources

### Documentation

- **[CHANGELOG](https://github.com/xnodeoncode/Nine/blob/main/CHANGELOG.md)** - What's new in each release
- **[Database Management Guide](https://github.com/xnodeoncode/Nine/blob/main/Documentation/Database-Management-Guide.md)** - Backup, restore and troubleshooting
- **[Compatibility Matrix](https://github.com/xnodeoncode/Nine/blob/main/Documentation/Compatibility-Matrix.md)** - Version compatibility and upgrade paths

### Getting Help

- **Email Support:** [cisguru@outlook.com](mailto:cisguru@outlook.com?subject=Nine%20Support%20-%20Quick%20Start%20Guide)
- **Report Bugs:** [GitHub Issues](https://github.com/xnodeoncode/Nine/issues)
- **Request Features:** [GitHub Discussions](https://github.com/xnodeoncode/Nine/discussions)
- **Documentation:** [Documentation](https://github.com/xnodeoncode/Nine/tree/main/Documentation)

---

## Settings & Configuration

**Organization:** click **Organization** in the left menu (administrators) to edit your organization's details.

**Your account:** click your name or email in the navigation to open **Account → Manage**. From there you can update your profile and change your password.

**Notifications:** open **Notifications → Preferences** to choose which in-app notifications you receive.

**Application settings:** click **Application** in the left menu. This page includes the **Late Fee Settings:**

- **Enable Late Fees** and **Auto-apply**
- **Grace Period** (days): the default is 5 days. Check the requirements where your property is and change it if they differ.
- **Late Fee Percentage** (and maximum fee)

**Database settings:** open the **Database Settings** page from **Application** (see "Backups and the database" below).

**Inspection scheduling:** the routine inspection interval is set per property on the property's edit page, not in a global setting.

<!-- VERIFY: exact path to Database Settings from the Application page, and the Late Fee field labels (percentage / maximum). -->

---

## Security & Data Protection

**Passwords:** use a strong, unique password (Nine requires at least 12 characters). Change it periodically and consider a password manager.

**Session security:** Nine signs you out after **18 minutes of inactivity** and warns you shortly before. Always log out when leaving your computer unattended.

**Database encryption:** database encryption is optional. Turn it on in **Database Settings → Database Encryption**; you create a password that is required to open the database. If encryption is off, the database file is stored unencrypted on your computer. Keep your password somewhere safe; it cannot be recovered.

**Archived and deleted records:** records can be archived and restored, and some can be permanently deleted. The delete confirmation lists the related records that will be affected.

**Audit trail:** Nine records who created or changed a record and when.

<!-- VERIFY: "audit trail" wording against what the UI actually shows; and the archive/delete behavior per record type. -->

---

## Common Questions

### Can I import data from another property management system?

Not directly. Enter your properties, tenants and leases manually, or restore from a Nine backup.

### Can I have more than 4 users?

Nine is limited to 4 accounts per organization: 1 system account and 3 login accounts.

<!-- VERIFY: property limit. The previous guide said 9 properties; no enforcement was found in the code. Confirm and add or remove. -->

### Is my data stored in the cloud?

No. Nine stores all data locally on your computer in a SQLite database file. The app does not send your data over the internet.

### Do I need internet access?

No. Nine works fully offline. Links to external documentation open in your default web browser.

### How do I back up my data?

Open **Database Settings** and use **Create Manual Backup**. Nine also creates a backup automatically before each database migration and encryption change, and keeps the 10 most recent backups. Use **Download** on a backup to copy it to an external drive or cloud storage.

### How do I restore a backup?

In **Database Settings → Available Backups**, use **Restore** on the backup you want (use **Preview** first to look inside it). You can also use **Upload Backup File** to bring in a backup from another location. Nine saves a copy of the current database before restoring.

### Can I run this on a Mac?

Not in this release. Use a Windows or Linux computer.

### Is there a mobile app?

Not yet.

<!-- VERIFY: macOS and mobile statements are carried over from the previous guide. -->

---

## Troubleshooting

### Application won't start

**Windows:**

- Right-click the installer and choose "Run as Administrator"
- Check whether antivirus is blocking the application

**Linux:**

- Make sure the AppImage is executable: `chmod +x Nine*.AppImage`
- Check system logs: `journalctl -xe`

### Database error or "Database Locked"

- If you enabled encryption, enter your database password on the unlock screen.
- In **Database Settings**, click **Check Health**.
- If the database is corrupted, try **Auto-Recover from Corruption**, or **Restore** a backup.
- As a last resort, **Reset Database (Create New Blank)** creates an empty database (this removes your data from the active database).

### Can't log in

- Verify your email and password and check Caps Lock.
- After repeated failed attempts the account can be locked temporarily; wait and try again.
- If you forgot your password, work with an administrator who has access to unlock your account.

<!-- VERIFY: that the login page has a forgot-password link in the Electron build and that account lockout applies. Also confirm where application logs are stored so a logs entry can be added. -->

---

## Support

**Email:** [cisguru@outlook.com](mailto:cisguru@outlook.com?subject=Nine%20Support%20-%20Quick%20Start%20Guide)  
**GitHub:** [https://github.com/xnodeoncode/Nine](https://github.com/xnodeoncode/Nine)

When contacting support, please include:

1. **Version number** (see the **About** page)
2. **Operating system** (Windows or Linux)
3. **What happened** compared with what you expected
4. **Steps to reproduce** the problem
5. **Screenshots**, if applicable

---

## Next Steps

1. **Add your remaining properties and tenants.**
2. **Set up late fees** under **Application**.
3. **Explore reports** under **Reports**.
4. **Review Database Settings** and decide whether to enable encryption.

Thank you for choosing Nine.
