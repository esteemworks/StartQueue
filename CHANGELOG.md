# Changelog

All notable changes to StartQueue. Downloads are on the [releases page](../../releases).

## Unreleased

- **Accent color** has a fourth choice, **Sand**: the quiet beige of the EsteemWorks apps, the same in each of them. The row now has the order StartQueue, Profile, Windows, Sand, with the same words and tooltips as in the other EsteemWorks apps; **Profile** stays the default

## 1.6.0

### Profiles shared with the family
- Profiles can be shared with the other EsteemWorks apps: **For all EsteemWorks apps** in the profile dialog offers a profile to DynaTones, and a profile DynaTones offers can be chosen under **Add profile**. It stays one profile; name, color and schedule apply in every app that uses it, which apps start in it stays StartQueue's own
- A profile can be chosen **by hand** from its menu, until the end of its hours or until you switch back; **Back to the schedule** ends it, in every app that uses the profile
- Removing a profile another app uses takes it out of StartQueue only
- "Standard" is now called **No profile**: what applies when no profile does
- Profiles move to `%APPDATA%\EsteemWorks\profiles.json` at the first start and stay in `config.json` too, so going back to 1.5 keeps them. Backups and exports hold them

### Settings in the family's order
- **General** first (color mode of this window, accent color, language), then **Startup at sign-in**, **New autostart entries** with the extra full scan in it (no card of its own any more), **This device** and **About**
- The language is a drop-down; "Same as Windows" reads **Like Windows**
- **About** is one card: version and privacy in one line, rows for updates, backing up the settings and troubleshooting

### Other
- "Privacy and notices" in the app names the update check as StartQueue's only connection, the backups, and where the profiles are kept; it said there was no connection at all
- An update by running a newer setup by hand now backs up the settings first too, as an update from inside the app does

## 1.5.3

### Calmer window
- The header shows the last run on a line of its own instead of after a dot, and the small "AUTOSTART" line above the title is gone
- The profile conditions read as one phrase ("Mo bis Fr, 08:00 bis 18:00, sonst Standard"), and list entries name their file after a colon; a sign-in task in the root folder shows just its program
- Pages longer than the window show a scroll bar that stays visible
- The setting for light and dark is called **Color mode**, as in Windows

### Troubleshooting
- Settings has a **Troubleshooting** card: diagnostics mode for 24 hours, the log, and a diagnostics package on the desktop that you can pass on. Nothing is sent
- The log keeps levels and rolls over at 1 MB, five files at most, none older than 30 days
- Important events also go to the Windows event log, with ids listed in the README. The setup's admin option sets it up in the same UAC prompt, or **Set up** under Troubleshooting
- `--diagnostics [folder]` and `--diagnostics-mode on|off|<hours>` for scripts and Intune
- Settings ends with **About** and **More from EsteemWorks**, the other apps of the family; the setup details are in **This device**

### Part of the EsteemWorks family
- Tray icon, About page, logging and setup come from the family's shared kit. The tray menu ends with **Quit**, its tooltip shows the number of new entries under the name
- **Family apps** like DynaTones come in quietly: first in the order and not new. Their own "Start with Windows" switch turns StartQueue's entry on and off, so the two no longer undo each other. A family app installed for all users stays in Windows

### Updates
- StartQueue can look for new versions on GitHub once a day and install one with a click, or by itself with **Install automatically**, both under Settings > **Updates**. The setup suggests the check with a ticked box. Nothing about you is sent, and the setup's SHA-256 digest is checked before it runs
- After an update StartQueue comes back to the tray quietly with a short note, without opening its window or starting the order again
- Installations per device never update themselves

### Backup and restore
- Settings > About > **Backup and restore**: **Export** saves your list, profiles and settings in one zip file, **Import** takes them over on this or another PC after backing up the current ones. The same from a script with `--export <file.zip>` and `--import <file.zip>`
- StartQueue saves the current state by itself before every update, import and restore, the last five stay under `backups` in the data folder and can be restored with one click
- Every new version takes over the list and settings of the earlier ones, after a backup when their shape changes. An export from a newer StartQueue is refused with the reason instead of being half read
- A list that cannot be read is kept as it was and shown under About with the last state to go back to; StartQueue never saves an empty list over it

### Fixed
- StartQueue could start twice at sign-in: when an earlier setup had run with admin rights, a later one without them (such as an update) could not replace the sign-in task and added the Run key as well. It now keeps such a task and removes the Run key, a setup with admin rights leaves the task replaceable for the user, and the admin features repair an older one
- The startup order could stay empty although its tab counted the apps: the rows were held at a width of 0 when the list was first laid out before it had a width

## 1.5.2

### Shut down with prepared apps
- **Shut down, prepare apps** and **Restart, prepare apps** in the tray menu and as two buttons next to the settings in the header. Windows signs you in by itself at the next start and locks right away, so the startup order runs before you are back and your apps are there when you unlock. This uses the automatic sign-in Windows already has for updates
- A confirmation says beforehand whether it can work on this PC and what is missing if not
- After the next start StartQueue tells you whether your apps were prepared, and if not, why
- **Prepare apps when switching on** in the settings sets it up with one click and can undo it; the setup offers the same as an option for admins. If the PC only allows Windows Hello sign-in, that is switched off, after a confirmation
- Where the organization requires Windows Hello, blocks the automatic sign-in by policy, or the PC is managed without BitLocker, StartQueue says so and changes nothing

### Faster start
- At sign-in the startup order now begins in StartQueue's own entry point, before WPF loads. The log says how long .NET took to load, to tell a slow runtime start from a slow app start

## 1.5.1

### Faster start
- At sign-in the startup order begins right away, before the window, the tray icon and the colors are built. On the author's PC the first app now starts about 8 s after sign-in instead of about 60 s
- No app waits for another any more: every start without "wait until it runs" gets a thread of its own, in the order of the list, so a slow shortcut, task or Store app no longer holds up the rest
- Start times count from the Windows sign-in; the header names a sign-in run as such, and the log marks each step in seconds after sign-in

### Windows at sign-in
- Store apps get 90 s instead of 30 s for their first window before StartQueue minimizes or hides it
- Apps that quit when their window is closed are remembered: "Background" is greyed out for them, with the reason in the tooltip

### Everything else
- **Check** says what it found: a short note below the button names what was taken over and what is new in the Windows autostart, sign-in tasks and services, or that nothing is new
- **Profile colors:** 16 gradients that are easy to tell apart; profiles with an earlier color get the closest new one. Buttons and the bar below the header stand out from the dark header in every color
- Lists keep the right width after maximizing and restoring the window

## 1.5.0

### Profiles
- **Order of the profiles:** drag profiles in the profile bar, with a marker showing where they land, or open **Order** to see every profile one below the other with rank, color, conditions and pause
- **Outside these hours:** per profile, what applies on its days outside its hours: the next profile in the order, Standard, or one chosen profile
- **All day:** a switch instead of times
- **Planned pauses:** pause from now until a moment, for a planned period like an out-of-office reply, or until you resume; all day or with times
- The header counts the apps of the profile you look at and names the one that applies now

### Services
- Sort by name, startup type or state
- Filter by startup type and state, several at once

### Everything else
- **Check** (<kbd>F5</kbd>) now covers new autostart entries, sign-in tasks, the Windows autostart list and services
- Windows autostart and services load in the background when the window opens
- Rounded window corners on Windows 11
- Day names of a pause follow the language of the app

## 1.4.0

- **Profile colors:** 16 color gradients per profile; with the accent color **Profile** the whole window takes on the color of the profile it shows
- **Own start parameters** per app: as before, add to the app's own ones, or replace the whole line, with an explanation window. Shortcuts and scheduled tasks are started directly then
- **Services:** a separate view like services.msc: change the startup type, start, stop, restart. Services of apps are kept apart from Windows services, with a warning before Windows services are changed. Nothing is taken over, every change asks through UAC
- **Privacy and notices** in the settings: what is read and stored, a note on AI, liability and licenses; the license texts of .NET ship in the installation folder

## 1.3.0

- **Profiles** with weekdays, hours, an order of precedence and pausing up to a date, in a profile bar above the list
- **Quiet start:** normal, minimized or in the background per app; start parameters like `-background` or `--silent` are recognized, and a library knows them for popular apps
- **Repeating tasks** can be taken over and keep their rhythm
- **Faster start:** the order starts right after sign-in; Windows' own startup delay can be switched off
- Explanations moved into tooltips

## 1.2.0

First release with a setup.

- Startup order with delays, "wait until it runs" and starting as administrator per app
- Takes over Run keys, startup folders, startup tasks of Store apps and sign-in tasks
- Watches for new entries and notifies you
- Light and dark, accent color StartQueue or Windows, English and German
- Uninstalling through Windows Settings > Apps gives every entry back to Windows
