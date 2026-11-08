# Privacy

StartQueue works on your device only. It collects no usage data, needs no account and passes nothing on to the author or anyone else. There are no ads and no analytics. Its one connection to the internet is the update check, which you can switch off (see below).

## What StartQueue reads

Only what the job needs, on the device it runs on:

- **Autostart entries** of Windows: Run keys in the registry, the startup folders, Task Scheduler and startup tasks of Store apps
- **Installed apps** from the Start menu, for "Add app"
- **Running processes**, to know whether an app already runs
- **Windows services**, for the services view
- **How often Windows counted an app being started** (UserAssist), only for the suggestions in "Add app". It is read when the dialog opens and not stored

## What StartQueue stores

In `%APPDATA%\StartQueue` on your device:

| File | Contents |
| --- | --- |
| `config.json` | your list, its order, which apps start in each profile, settings and where each entry came from |
| `last-run.json` | the last start at sign-in, per app with seconds and result |
| `shortcuts\` | shortcuts taken over from the startup folders |
| `StartQueue.log` | a log of take-overs, starts and errors, rolled over at 1 MB, at most five files, none older than 30 days |
| `diagnostics-until.txt` | only while diagnostics mode is on: until when |
| `updates.json` | whether StartQueue looks for updates, installs them by itself, and when it last looked |
| `backups\` | the last five states of `config.json` and `shortcuts\` as zip files, saved before every update, import and restore and before a new version takes them over; a list that could not be read is kept here as it was |

When the profiles apply (their names, colors, days, hours and pauses) is in `%APPDATA%\EsteemWorks\profiles.json`, which the EsteemWorks apps on this device share; it holds no app names and no paths.

These files contain app names and program paths, which can include your user name. They never leave the device. The folder stays when StartQueue is uninstalled, so a new installation finds your list again; you can delete it at any time.

**Export** under Settings > About > Back up settings (or `--export`) writes `config.json`, the `shortcuts` folder, the profiles from `profiles.json` and a small note with StartQueue's name and version and the date into a zip file where you choose. It holds the same app names and program paths; no log, no last run. It goes nowhere unless you take it somewhere.

Once the Windows event log is set up for StartQueue, important events (starts, take-overs, errors) also go there, on this device. A diagnostics package is made only when you ask for it under Troubleshooting: a zip on your desktop with the log, the list and the last run, with your user folder and name replaced by placeholders. Nothing is sent; you decide whom you give it to.

## Updates

When "Look for updates" is on (the setup suggests it, Settings > About > Updates switches it), StartQueue asks GitHub once a day for the latest release of StartQueue. The request carries StartQueue's name and version and nothing about you or your PC; like any connection it shows GitHub your IP address. When you install an update, the setup is downloaded from GitHub and checked against the SHA-256 digest GitHub gives before it runs. `updates.json` in the data folder keeps your choice and when it last looked. Installations per device never look for updates; the organization deploys new versions.

## What StartQueue changes in Windows

StartQueue switches off the autostart entries it takes over and starts those apps itself. Every entry can be given back, and uninstalling gives back all of them. Depending on your choices it also sets up tasks in Task Scheduler (start at sign-in, admin features), an entry in the Explorer context menu, Windows' startup delay and the startup type or state of services you change.

## AI

StartQueue was developed with the help of AI (Claude by Anthropic). The app itself contains no AI, makes no automated decisions about people and sends no data to AI services.

## Questions

Open an issue in this repository.
