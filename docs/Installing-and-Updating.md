# Installing and Updating

## Install

1. **[Download the installer](https://github.com/krabbzdev/KrabbzDev-BusyLightz2.0/releases/latest/download/KrabbzDev-BusyLightz2.0.msi)**. The link always gets the newest version.
2. Run `KrabbzDev-BusyLightz2.0.msi`.
   - **No admin rights needed.** It installs just for you, to `%LOCALAPPDATA%\Programs\Krabbz9999 Blynclight App Control`.
   - **SmartScreen warning:** the installer isn't code-signed yet, so Windows may say *"Windows protected your PC"* or *"unknown publisher"*. Click **More info > Run anyway**.
3. The app opens, along with the **[Setup checklist](Setup-Checklist.md)**.

After installing:

- The app **starts automatically when you sign in to Windows**, with its window open and an icon in the system tray.
- There's a **Start menu** entry called *KrabbzDev-BusyLightz2.0*.
- **Pin it to Start yourself** if you like: right-click the Start menu entry > **Pin to Start**. Windows doesn't allow installers to pin apps.

## Updates

The app keeps itself up to date:

- It checks for a new version **1 minute after it starts, then every 6 hours**.
- To check right now, click **Check for updates** in the window (bottom line) or in the tray menu.
- When there's a new version, the app:
  1. downloads it from this project's [Releases](https://github.com/krabbzdev/KrabbzDev-BusyLightz2.0/releases) page,
  2. checks the download against GitHub's SHA-256 fingerprint and refuses anything that doesn't match,
  3. installs it silently in the background (no installer window, no admin prompt),
  4. reopens itself and shows *"Updated to version x"*.

The version you're running is shown at the bottom of the window and in the tray menu.

## Uninstall

**Settings > Apps > Installed apps > KrabbzDev-BusyLightz2.0 > Uninstall.** No admin rights needed.

Your settings stay in `%LOCALAPPDATA%\Krabbz9999 Blynclight` and the registry key `HKEY_CURRENT_USER\Software\Krabbz9999\Blynclight App Control`. Delete them too if you want a clean slate.

## Deploying to many PCs (IT)

The installer is a standard per-user MSI, so you can push it with your RMM tool or Intune. **Run it in the user's context, not as SYSTEM**, because it installs into the user's profile.

```
msiexec /i KrabbzDev-BusyLightz2.0.msi /qn
```

To pre-set your organization's Teams app (see [Setting Up Teams for Your Organization](Setting-Up-Teams-for-Your-Organization.md)), add the two IDs:

```
msiexec /i KrabbzDev-BusyLightz2.0.msi /qn TEAMSCLIENTID=<Application (client) ID> TEAMSTENANTID=<Directory (tenant) ID>
```

The IDs are kept through later automatic updates.

## Where the app keeps things

| What | Where |
| --- | --- |
| The app | `%LOCALAPPDATA%\Programs\Krabbz9999 Blynclight App Control` |
| Your chosen color | `%LOCALAPPDATA%\Krabbz9999 Blynclight\state.txt` |
| Teams sign-in (encrypted to your Windows account) | `%LOCALAPPDATA%\Krabbz9999 Blynclight\teams-presence.dat` |
| Error log, if anything went wrong | `%LOCALAPPDATA%\Krabbz9999 Blynclight\app.log` |
| Downloaded updates and update logs | `%LOCALAPPDATA%\Krabbz9999 Blynclight\updates` |
| Settings (ignored apps, notifications, Teams app IDs) | `HKEY_CURRENT_USER\Software\Krabbz9999\Blynclight App Control` |

---
[Guide home](README.md) · [Installing and Updating](Installing-and-Updating.md) · [Using the Light](Using-the-Light.md) · [Teams Status](Teams-Status.md) · [Call Detection](Call-Detection.md) · [Setup Checklist](Setup-Checklist.md) · [Setting Up Teams for Your Organization](Setting-Up-Teams-for-Your-Organization.md) · [Troubleshooting and FAQ](Troubleshooting-and-FAQ.md) · [For Developers](For-Developers.md)
