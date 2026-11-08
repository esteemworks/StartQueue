# Roadmap

Where StartQueue is heading. The topics are listed in the order they are planned; what is already done is in the [changelog](CHANGELOG.md). Ideas and wishes are welcome as issues.

## 1. Startup times over time

The question StartQueue started with is why the desktop takes so long after sign-in. Today the window shows the last run; the next step is a history.

- Time from sign-in to a usable desktop, to the end of the startup order, and per app, across several sign-ins
- Comparison with earlier sign-ins, so you can see whether a change to the order paid off
- Apps that take unusually long or fail often stand out
- Shown in the details of an app and as an overview of all sign-ins

## 2. Start conditions per app

Fixed delays are guesswork. Better is waiting for a state, with a maximum like "wait until it runs" has today.

- Start only once the network is connected, for apps that sign in or sync right away
- Start only once CPU and disk have calmed down
- Skip or start later on battery, for laptops

## 3. Publisher and signature

- The publisher of every entry in the startup order and in the Windows autostart, read from the program's signature
- A hint for unsigned programs, especially for newly found entries, so malware in the autostart stands out

## 4. Matching the list on another device

Export and import already take the list, settings and profiles to another PC. Today the entries are taken over as
they are.

- On import, apps are matched by name and program, since paths differ between devices; what is not installed there is skipped and listed

## 5. Public signing

- A code signing certificate, so Windows SmartScreen no longer warns about an unknown publisher

## Ideas on hold

- **Prepared shutdown in the Windows menus.** "Shut down, prepare apps" and "Restart, prepare apps" in the power menu of Start and in the Win+X menu (right click on Start). Windows 11 offers no way for this: since 24H2 the Win+X menu ignores entries in its WinX folders, even with a valid hash, and the power menu of Start has no extension point. It would take code injected into Explorer and the Start menu, as Windhawk mods do; security software treats that like an attack, and it can break with every Start menu update. If at all, then as a separate Windhawk mod, not inside StartQueue. Without injection, entries in the Start menu (found by search, can be pinned) and tasks on the taskbar icon would be possible.

## Known limitations

- Changing a Windows service always asks through UAC, also with the admin features set up. This is deliberate: no other program under your account can change services through StartQueue without that prompt.
- Apps that add themselves to the autostart through a helper (for example Discord with `Update.exe --processStart Discord.exe`) show the helper's name and icon. Name and icon should come from the actual app.
- "Add app" can suggest an app that is already in the list under another name.
- The Explorer context menu switches its language after Explorer restarts.
- One pause per profile, either current or planned.
