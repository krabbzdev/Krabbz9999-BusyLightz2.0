# Krabbz9999-BusyLightz2.0

**One app for your [Embrava Blynclight](https://embrava.com) and [Luxafor Flag](https://luxafor.com) busy lights, together.**

A small Windows tray app that controls USB busy lights directly over USB. Plug in any mix of Blynclights and Luxafor Flags, as many of each as you like, and they all show the same color at the same time. It replaces both Embrava Connect and Luxafor's own app. No vendor accounts or admin rights needed.

📖 **[User guide](docs/README.md)**: every feature and how to use it.

## Why I made this

> The reason I made this app, is I wanted an app to turn my lights red anytime I was on a call. Be it Zoom, Cytracom, Loom, Teams etc. If My MIC is hot, I want the lights red.
>
> — Krabbz9999

## ⬇️ [Download the installer (latest version)](https://github.com/showtechclove1/Krabbz9999-BusyLightz2.0/releases/latest/download/Krabbz9999-BusyLightz2.0.msi)

Release notes and older versions: [Releases](https://github.com/showtechclove1/Krabbz9999-BusyLightz2.0/releases)

## Features

- **Embrava Blynclight + Luxafor Flag together:** every connected light shows the same color; lights plugged in later are matched within seconds. The window shows what's connected (e.g. *Lights: Blynclight + 2 Luxafor*).
- **Red (Busy Mode) / Green (Open Mode)** from the window, tray menu or global hotkeys `Alt+R` / `Alt+G`
- **Override timer:** Red, Green and On A Call go back to Follow Teams Status after 1 hour, with a countdown in the window
- **Party Mode:** cycles smoothly through the full color wheel on every light
- **Flashes red and blue whenever you're on a call:** any app using your microphone (Teams, Zoom, Cytracom, browser calls…) sets the lights flashing (or steady red, your choice in Settings), then back afterward. An ignore list handles recorders and noise-cancelling tools.
- **Follow Teams Status:** green Available, red Busy / In a call, purple Do not disturb, yellow Away, off when Offline
- **Connect to Microsoft Teams:** one-time browser sign-in (delegated `Presence.ReadWrite`, no client secret, token encrypted to your Windows account). Reads your Teams status and shows you as *Busy, In a call* in Teams during calls in other apps.
- **Setup checklist** on first run, including setting up the Teams app for other organizations
- **Notifications** when the lights change
- **Settings window** (⚙ in the app or tray menu): startup, notifications, hotkeys, automatic updates, call detection, Teams Busy and the Teams connection
- **Automatic, silent updates** from this repo's Releases, verified against GitHub's SHA-256 digest
- Opens at sign-in, with a tray icon that matches the lights

Windows doesn't let installers pin apps to the Start menu. Pin it yourself (right-click the Start menu entry → **Pin to Start**), or push a Start pins policy through Intune/Group Policy on managed machines.

## Install

Run the downloaded `Krabbz9999-BusyLightz2.0.msi`. It installs just for you, with no admin prompt, and starts with Windows. The **Setup checklist** walks you through the rest. The installer isn't code-signed yet, so Windows may show an "unknown publisher" warning: click **More info > Run anyway**.

Updates install themselves silently. Details, IT deployment and uninstalling: [Installing and Updating](docs/Installing-and-Updating.md).

## Guide

| Page | What's in it |
| --- | --- |
| [Installing and Updating](docs/Installing-and-Updating.md) | Download, install, automatic updates, uninstall, deploying to many PCs |
| [Using the Light](docs/Using-the-Light.md) | Supported lights, the window, tray menu, hotkeys, notifications, Settings |
| [Teams Status](docs/Teams-Status.md) | Connecting to Teams, what each color means, Busy during calls |
| [Call Detection](docs/Call-Detection.md) | How the lights know you're on a call, ignoring apps |
| [Setup Checklist](docs/Setup-Checklist.md) | Every step of the checklist explained |
| [Setting Up Teams for Your Organization](docs/Setting-Up-Teams-for-Your-Organization.md) | One-time steps for an IT admin |
| [Troubleshooting and FAQ](docs/Troubleshooting-and-FAQ.md) | Common problems and questions |
| [For Developers](docs/For-Developers.md) | Building, publishing releases, project layout, light protocols |
