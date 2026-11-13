# Nine. - Quick Start Guide

**Version:** 1.2.0  
**Last Updated:** November 12, 2026  
**Estimated Time:** 15 minutes

---

## Welcome

This guide gets you from a fresh install to your first invoice and payment. By the end you will have:

- Installed the application
- Created your account and organization
- Added a property
- Added a tenant
- Created a lease
- Created an invoice
- Recorded a payment
- Recorded a property inspection

Let's get started.

---

## Prerequisites

Before you begin, ensure you have:

- **Operating System:**
  - Linux (Ubuntu 20.04+, Debian 11+, Fedora 35+), OR
  - Windows 10/11 (64-bit)
- **Hardware:**
  - 2 GB RAM minimum (4 GB recommended)
  - 500 MB disk space
- **Downloaded:** Nine v1.2.0 for your platform

### Universal Linux Support

Nine is distributed as an AppImage, which runs on all major Linux distributions, including Ubuntu, Debian, Fedora, Red Hat, Arch and openSUSE. No installation is required: download, make executable, and run.

### Windows Portable Version

Nine is also available as a portable Windows executable (.exe). No installation is required. All application data is stored locally in the same folder, so you can run Nine from a USB drive or move between systems.

---

## Step 1: Installation (5 minutes)

### Linux

```bash
# 1. Download the file
# File: Nine-1.2.0-x86_64.AppImage

# 2. Make it executable
chmod +x Nine-1.2.0-x86_64.AppImage

# 3. Run the application
./Nine-1.2.0-x86_64.AppImage
```

### Windows

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

## Step 2: Create Your Account and Organization (2 minutes)

The first time Nine starts, there are no accounts, so the **Register** page shows a single form that creates both your organization and your account. Registration is only available for this first account.

1. Fill in **Organization Name** (for example "ABC Property Management" or "John Smith Rentals").
2. Choose your **State**.
3. Enter your **Email** (this is your login), **First Name** and **Last Name**.
4. Enter and confirm a **Password**. It must be at least **12 characters** and include an uppercase letter, a lowercase letter, a number and a special character.
5. Click **Register**.

You are the organization owner with full administrative privileges. The account is confirmed automatically; there is no email verification step.

**Checkpoint:** You are signed in and see the **Property Management Dashboard**.

### The Dashboard

The left navigation menu contains **Dashboard, Calendar, Properties, Tenants, Leases, Inspections, Repairs, Documents** and **Reports**. Administrators also see **Application** and **Organization**.

The dashboard shows property metrics, **Outstanding Invoices**, **Outstanding Repairs**, **Upcoming Lease Renewals** and **Available Properties**.

<!-- VERIFY: first-run message wording and whether the dashboard shows a sample-data option on a new database. -->

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
   - **Start Date** and **End Date**
   - **Monthly Rent** and **Security Deposit**
   - **Lease Type:** Term, Month-to-Month, Sublease or Other
   - **Status:** new leases default to **Pending**; choose **Active** if the lease is already in force
   - Optional: **Lease Terms** and **Notes**
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
2. Click **Create Invoice** (this opens the invoice form with the lease selected).
3. Fill in the form:
   - **Invoice Number** and **Invoice Date**
   - **Due Date**
   - **Lease** (already selected)
   - **Description** (for example "First month's rent")
   - **Amount**
   - **Status:** Pending (default), Paid or Cancelled
   - Optional: **Notes**
4. Click **Create Invoice**.

You can find all invoices later from the lease's **View Invoices** button, from **Outstanding Invoices** on the dashboard, or from the **Documents** page.

**Checkpoint:** The invoice shows status **Pending** with your due date.

<!-- VERIFY: whether Invoice Number is auto-generated, and how the Amount is filled in when the lease is selected. -->

---

## Step 7: Record a Payment (1 minute)

1. Open the invoice.
2. Click **Record Payment**.
3. Fill in the form:
   - **Invoice** (already selected)
   - **Payment Date**
   - **Amount**
   - **Payment Method:** Online Payment, Debit Card, Credit Card, Check, Cash, Bank Transfer or Other
   - Optional: **Notes**
4. Click **Record Payment**.

**Checkpoint:** Open the invoice again; it should show the payment.

<!-- VERIFY: that recording a full payment changes the invoice status to Paid and updates the paid amount. -->

### Payment receipt (optional)

Open the payment and use **Generate Receipt**, then **View Receipt** or **Download Receipt**.

---

## Step 8: Record an Inspection (3 minutes)

The **Inspections** page is a routine inspection schedule by property: it shows which properties are scheduled, due soon, overdue or not yet scheduled.

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

## Additional Resources

### Documentation

- **[CHANGELOG](../CHANGELOG.md)** - What's new in each release
- **[Database Management Guide](Database-Management-Guide.md)** - Backup, restore and troubleshooting
- **[Compatibility Matrix](Compatibility-Matrix.md)** - Version compatibility and upgrade paths

### Getting Help

- **Email Support:** cisguru@outlook.com
- **Report Bugs:** [GitHub Issues](https://github.com/xnodeoncode/Nine/issues)
- **Request Features:** [GitHub Discussions](https://github.com/xnodeoncode/Nine/discussions)
- **Documentation:** [`/Documentation`](.)

---

## Settings & Configuration

**Organization:** click **Organization** in the left menu (administrators) to edit your organization's details.

**Your account:** click your name or email in the navigation to open **Account → Manage**. From there you can update your profile, change your password, and set up two-factor authentication.

**Notifications:** open **Notifications → Preferences** to choose which in-app notifications you receive.

**Application settings:** click **Application** in the left menu. This page includes the **Late Fee Settings:**

- **Enable Late Fees** and **Auto-apply**
- **Grace Period** (days)
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

### Can I have more than 3 users?

Nine is limited to 3 users per organization.

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
- If you forgot your password, use the **Forgot your password?** link on the login page.

<!-- VERIFY: that the login page has a forgot-password link in the Electron build and that account lockout applies. Also confirm where application logs are stored so a logs entry can be added. -->

---

## Support

**Email:** cisguru@outlook.com  
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
