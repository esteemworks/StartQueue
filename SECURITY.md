# Security

StartQueue changes the Windows autostart, can start apps with administrator rights through its own scheduled task, and can change Windows services. Security issues are therefore taken seriously.

## Supported versions

Only the [latest release](../../releases/latest) receives fixes.

## Reporting a vulnerability

Please do not open a public issue for a vulnerability. Report it privately through **Security > Report a vulnerability** in this repository (GitHub private vulnerability reporting), with the version, what you did and what happened.

You will get an answer as soon as possible. Once a fix is released, the report can be published.

## Design notes

- The tray app always runs with normal rights. Only the scheduled task `\StartQueue\Elevated` runs with the highest privileges, and only for entries for all users and apps you marked "Run as administrator".
- Service changes are never made by that task. Each one runs in its own process started through UAC, so no other program under your account can change services through StartQueue without the prompt.
- The context menu package is signed with a certificate created on the build machine. The setup trusts it only under "Trusted People", never as a root certificate.
