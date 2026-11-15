# Nine. - Privacy Statement

**Version:** 1.2.0  
**Last Updated:** November 14, 2026

---

## Summary

Nine is a desktop application that runs entirely on your computer. It stores the information you enter on your computer, and
that information stays there. Nine does not transmit it or share it with anyone, and it has no accounts, servers or
services of its own.

- **Your data is stored locally** in a database file on your computer. You can encrypt it.
- **Nine shares nothing.** It does not send your information anywhere, and it has no analytics, telemetry, advertising or
  crash-reporting code. The only information it holds is what you enter.
- **Nine makes no outbound network connections.** It works fully offline.
- **You are in control.** You decide what to enter, back up, export or delete.

---

## What Nine Stores

Nine stores the information you enter so that you can manage your rental properties. Depending on how you use it, this
can include:

- Property details and expenses
- Tenant details, such as names, email addresses, phone numbers, dates of birth, identification numbers and emergency
  contacts
- Leases, invoices, payments, security deposits, inspections, repairs, checklists, notes and calendar events
- Documents you upload or generate, such as lease and receipt PDFs
- User accounts for the people who sign in to Nine (names, email addresses, and passwords stored as one-way hashes, not as
  readable text)
- Application and organization settings

All of this is stored in a local SQLite database file. It is not sent anywhere.

### Where it is stored

Nine keeps its data in a folder in your user profile:

- **Linux:** `~/.config/Nine/`
- **Windows:** `%APPDATA%\Nine\`

This folder contains the database (`app_vX.Y.Z.db`), the `Backups` folder, and the cookies and cache used by the
application window. The portable Windows version may keep its data with the program instead.

### Database encryption

Database encryption is optional. When you turn it on (Database Settings), Nine encrypts the database with a password you
choose, using SQLCipher. The key is kept in your operating system's credential store (the Windows credential store or the
Linux keychain), not in a file. If you lose the password, your data cannot be recovered, so keep it somewhere safe. If you
do not turn encryption on, the database file is not encrypted and is protected only by your computer's own security.

### Cookies

Nine uses a small number of cookies inside its own application window for signing in, keeping your session, and protecting
forms from forged requests. They stay on your computer and are not used for tracking.

---

## What Nine Does Not Do

- It does not collect anything about you or how you use it, such as usage data, analytics or diagnostics. The only
  information it holds is what you enter.
- It does not contain advertising or third-party tracking.
- It does not send crash reports.
- It does not send email or text messages. No email or SMS provider is configured in Nine, and it has no screens for
  setting one up.
- It does not check for updates in the background or contact any server.
- It does not sell, rent or share your information. It has no information about you to sell or share.

---

## Network Access

Nine runs a small web server that the Nine window uses on your computer. It listens on your local machine only
(`localhost`) and is not reachable from other computers. As with any local service, other programs running on your
computer can reach it. The Nine window is set to block its own requests to anything outside your computer.

Links to external websites, such as release notes and documentation, are handed to your **default web browser**. When you
open one, that is your browser connecting to the website, not Nine, and the website's and your browser's own privacy
practices apply.

---

## Backups, Exports and Documents

Backups, downloaded documents and exports are files on your computer. Nine does not upload them. If you copy them to an
external drive or a cloud storage service, you decide where they go and you are responsible for protecting them.

---

## Your Responsibilities

If you use Nine to keep information about tenants and applicants, you may have legal obligations for how you collect, store
and protect that information. Nine gives you the tools (local storage, optional encryption, backups, and archive or delete),
but you decide how to use them. Good practice includes:

- Turning on database encryption and using a strong password
- Keeping your computer's operating system and disk protected
- Keeping backups in a safe place, and removing ones you no longer need
- Archiving or deleting records you no longer need to keep

### Removing your data

To remove all Nine data from a computer, uninstall Nine and then delete its data folder (see above). Uninstalling the
application may leave that folder in place. Deleting records inside Nine removes them from the database, and deleted records
may still exist in older backups until you delete those too.

---

## Third-Party Software

Nine includes third-party components. See [Third-Party Licensing Notices](Third-Party-Licensing-Notices.md), or the
**Third-Party Licenses** page in the application, for the list and their licenses. None of these components is used to send
your information anywhere.

---

## Changes to This Statement

If a future version of Nine changes how it handles information, for example by adding an optional feature that connects
to the internet, this statement will be updated and the version at the top will change. The statement installed with the
application describes the version you are running.

---

## Contact

Questions about this statement can be sent to
[cisguru@outlook.com](mailto:cisguru@outlook.com?subject=Nine%20Support%20-%20Privacy%20Statement).

This statement is informational and is not legal advice.
