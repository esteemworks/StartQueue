# Changelog

All notable changes to StartQueue. Downloads are on the [releases page](../../releases).

## Unreleased

### Setup and first start
- **The setup asks one question:** **Basic, without admin rights** (chosen) or **Full, with admin rights**, each with one sentence on what it means for StartQueue. Without admin rights Full is greyed out with the reason. The window is called "StartQueue einrichten" / "Set up StartQueue"; there is no "for all users" dialog any more (`/ALLUSERS` on the command line still works, `/FULL` chooses Full in a silent installation)
- **Shutting down with prepared apps** is no longer offered in the setup, only in the settings
- **At the first start StartQueue changes nothing in Windows.** It opens with three steps in place of the tabs: **Welcome**, **Take over** (the apps it found, each with its switch; apps for all users named apart; what happens when an app adds itself later) and **Switch on** (a summary, then **Switch StartQueue on**). Unticked apps stay with Windows, on the keep list as *left with Windows by you*
- **Look around first** opens StartQueue as a preview: a note under the brand line, every app marked *still with Windows*, **Switch on** in the header. What you arrange there stays when you switch on; a sign-in meanwhile starts nothing through StartQueue
- `--switch-on` switches StartQueue on from the command line, for deployments
- `--setup-full` sets up fully from the command line (one UAC prompt)
- **An update changes nothing:** no question, no introduction, StartQueue goes on as before

### Full means no more UAC prompts
- Set up fully, giving back an entry for all users, changing a Windows service and setting up prepared shutdowns go through StartQueue's admin helper: Windows asks nothing more (uninstalling still asks once). In Basic they ask through UAC as before
- In the settings the step up is called **Set up fully** and sits under **General**; the card "This device" is gone

### Appearance
- **Calmer colors:** switches and radio buttons in one solid tone instead of the gradient; side actions such as Order, Change pause, Edit, Mark all seen and Start all now in neutral text, the accent stays on Add app and Add profile
- **A row shows its actions** (start now, remove, more) while it is hovered, chosen or has the keyboard focus, not on every row at once
- **Menus grow out of their button** and close at once; opened from the keyboard they show at once with the first entry highlighted, Home and End and the first letter of an entry work
- **The keyboard, as in every EsteemWorks app:** the tabs and segments are one Tab stop each, the arrow keys move and choose, Home and End jump; a question about removing puts the focus on Cancel first, Tab stays inside it, and the focus goes back where it was when it closes
- **Search fields** in Services and in Add app: Ctrl+F jumps into them, Esc clears the text, a clear button appears once there is text; in Add app an empty field's Esc closes the dialog
- **Notifications** at the bottom of the window: Undo after removing an app is one of them; several stay at once (the newest in front, hover unfolds them), the time stops while the pointer is on them or the window is in the background, F6 moves to them and back
- The counts on the tabs roll to their new value when they change while the window is open
- **Add app:** the Add button of each app is a plain button now; Done stays the one main button of the dialog
- **Services:** start, stop and restart show on the row under the pointer or with the keyboard focus, like the actions in the startup order
- **Calmer still, as the EsteemWorks design system has it since its version 21:**
  - The chips on a row (minimized, background, waits, a delay, admin, repeat) are grey; only *still with Windows* and *New* keep the color. In Windows autostart and Services the state chips are grey too, whatever the origin
  - The details show the app's name first and its kind (program, shortcut, Store app, scheduled task) as a quiet line below it; the first start's welcome has no capitals line above its title, and its icons are grey
  - Shadows only on what floats, soft and in two layers: menus, tooltips, notifications and the note under Scan, a stronger one for dialogs. Cards and rows have none
  - Every movement takes the family's times and curve: hovering fades in, a press shrinks a button to 97 percent, dialogs fade in from 96 percent, the note under **Scan** grows out of the button instead of out of the middle
  - **Tooltips** wait 600 ms, and once one has shown, the next comes at once; they look like the menus (white or dark card, thin line, soft shadow)
  - Spacing on the family's scale, icons in buttons at one size
  - **The tray menu is new:** a head with StartQueue's tile, its name and what it does right now (such as *On, 6 apps in "Work"* or *1 new autostart entry*; a click opens StartQueue), each entry with its icon, Quit last; light or dark with the app, and it grows out of the corner at the tray. From the keyboard: Win+B, the arrow keys to StartQueue's icon, then Enter, Shift+F10 or the menu key; the arrow keys, Home, End and a letter move in it, Esc closes it and goes back to the icon
- **Stop sharing a profile without deleting it:** switching off "For all EsteemWorks apps" while DynaTones uses the profile asks first and then gives StartQueue a copy of its own; DynaTones keeps its version. Switching it on again joins the shared profile of the same name instead of making another one: silently when days and times match, otherwise you choose between keeping it apart (rename it), taking the existing one, or overwriting it with yours for the other apps too. Switching back and forth never makes more profiles
- **The settings are calmer, as in every EsteemWorks app:** one column in the middle of the window, a label above each group, every setting with its icon and one line under it that says what it does; the info sign only where more needs saying. One control per setting: **New autostart entries** is now a drop-down with its meaning under it. **Updates**, **Back up settings**, **Troubleshooting** and **Privacy and notices** are rows that open a page of their own, with Back or Esc to return to the same place; an update that is ready shows **Install** right in its row. **Privacy and notices** is such a page now instead of a dialog. **More from EsteemWorks** shows DynaTones with its tile
- **Profiles work the same in every EsteemWorks app:** the profile bar shows each profile's color as a dot, the one that applies now says so in small letters in its chip (so it reads apart from the one shown), **No profile** last; a profile's tooltip opens above it and leaves the line below free; the line under the bar says **Pause profile** and **Resume profile**, apart from **Pause** in the header; a profile's menu edits, uses it by hand (until the end of its hours or until you switch back), pauses, moves and removes it. **Editing a profile, its pause and the order of the profiles open as pages of the startup order** instead of dialogs, with Back or Esc to return. While you edit a profile, the window takes its color
- **General is the same in every EsteemWorks app:** color mode, accent color, language and set up, with the same words and controls. **Accent color** is a drop-down with each color's swatch (StartQueue (Glut), Like the profile, Like Windows, Sand) instead of four segments, and applies at once. **Set up** says Basic or Full; while it is Basic, **Set up fully** stands at the right
- **Updates** names a build between releases with its commit and build time, such as "1.6.0, build bb40159 from 11/12/2026 4:40 PM", so you can tell which state you have
- The mark before the chosen entry of a drop-down list takes StartQueue's accent instead of Windows blue
- **Buttons show their hover color again:** since the calmer look of this version no button showed it, the row's actions and the header's buttons included
- **Every button that opens a menu works the same** (the EsteemWorks design system, "Menu buttons, for every one"): a second click on **Filter**, a row's **⋯** or **More actions** closes the menu instead of opening it again; the menu opens below the button, lined up with its edge and at least as wide, above only where there is no room below. **Filter** and **More actions** show a small arrow after the label that points where the menu opens; the button keeps its hover color while its menu is open. Enter, Space, Down and Alt+Down open the menu with the first entry chosen, Up with the last, Esc closes it and the focus stays on the button. The drop-down fields (sorting in Services, the start type, the language) open with the same keys and have the same small arrow
- In the profile editor the button says **Remove from StartQueue** while another app uses the profile, and **Delete profile** only when it is gone afterwards
- **Services, Filter:** the menu shows a check mark next to each ticked start type and state; before, it showed the names of the start types where the icon goes
- **The window's groundwork is the same in every EsteemWorks app:** the body text in Segoe UI Variable Text, the pages in line with the title (32 px from the edge), F5, Ctrl+N and Esc from the kit, shut down and restart with prepared apps on the tab line left of the gear. An empty startup order offers **Add app** right under its text
- **Dialogs and Check as in every EsteemWorks app:** **Add app**, the questions before removing or changing something and the explanations open on the family's dialog card. **Check** tells what it found, or that nothing new turned up, as a notification at the bottom of the window instead of its own note under the button; F5 does the same
- **The first start as in every EsteemWorks app:** the steps of the introduction use the family's pieces (heading, features, facts, notes, **Set up fully**). **Look around first** is kept: StartQueue opens as it was left after a restart, with **Continue setup**, instead of at Welcome again. The marker of scrolling with the middle mouse button is the family's
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
