# KrabbzDev-BusyLightz2.0

**One app for your Embrava Blynclight and Luxafor Flag busy lights, together.**

A small Windows app that controls USB busy lights, so the people around you can see at a glance whether you're free, busy or on a call. Plug in any mix of **Embrava Blynclight** and **Luxafor Flag** lights, as many of each as you like, and they all show the same color at the same time. It replaces both the Embrava Connect app and Luxafor's own app.

> **This app is still in BETA.** If something doesn't work the way this guide describes, see [Troubleshooting and FAQ](Troubleshooting-and-FAQ.md).

## Why I made this

> The reason I made this app, is I wanted an app to turn my lights red anytime I was on a call. Be it Zoom, Cytracom, Loom, Teams etc. If My MIC is hot, I want the lights red.
>
> — KrabbzDev

<img src="images/main-window.png" alt="The main window in the Orbit style: a green top with a ring reading Available, From Teams Status, and a switcher for Busy, Available, On a call and Follow Teams Status" width="440">

## What it does

- **Shows your status on the light:** pick **Busy** (red), **Available** (green) or **On a call** (flashing red and blue) yourself, or let the light **Follow Teams Status**. Choose from three window styles, in Light or Dark. See [Using the Light](Using-the-Light.md).
- **Goes back to Teams on its own:** Busy, Available and On a call switch back to Follow Teams Status after 15 minutes, with a countdown so you know one is on. Change the time, or turn it off, in Settings. See [Override timer](Using-the-Light.md#override-timer).
- **Flashes whenever you're on a call:** any app using your microphone (Teams, Zoom, Cytracom, a browser call and so on) makes the light flash red and blue (or steady red, if you prefer), then it goes back when you hang up. See [Call Detection](Call-Detection.md).
- **Tells Teams you're busy:** picking Busy or On a call sets your Teams status to Busy (and Available sets Available), and during calls in other apps, such as a Cytracom phone call, Teams shows you as *Busy, In a call*. See [Teams Status](Teams-Status.md).
- **Sets a Teams status message:** coworkers see a message like *On a video call* (the default) or *In a meeting*, or your own, next to your name while your lights are red. See [Status message](Teams-Status.md#status-message-while-your-lights-are-red).
- **Test lights:** cycles through every color the light can show, to check every light is working.
- **Works with several lights at once:** every Blynclight and Luxafor Flag on the PC shows the same color, including ones plugged in later.
- **Keeps itself up to date:** new versions install silently in the background, then a card lists what changed.

## Quick start

1. **[Download the installer](https://github.com/krabbzdev/KrabbzDev-BusyLightz2.0/releases/latest/download/KrabbzDev-BusyLightz2.0.msi)** and run it. No admin rights are needed.
2. The **Setup checklist** opens the first time. Follow it top to bottom:
   - Check your lights are found.
   - Click **Connect** and sign in with your work account.
   - Click **Quit it** for Embrava Connect, then uninstall Embrava Connect from **Settings > Apps**.
   - *Light follows your Teams status* is already on.
3. That's it. The app starts with Windows and lives in the system tray (bottom-right, near the clock).

## Pages

| Page | What's in it |
| --- | --- |
| [Installing and Updating](Installing-and-Updating.md) | Download, install, automatic updates, uninstall, deploying to many PCs |
| [Using the Light](Using-the-Light.md) | The window, every button and menu item, override timer, hotkeys, notifications, Settings |
| [Teams Status](Teams-Status.md) | Connecting to Teams, what each color means, Busy in Teams, status messages |
| [Call Detection](Call-Detection.md) | How the light knows you're on a call, ignoring apps |
| [Setup Checklist](Setup-Checklist.md) | Every step of the checklist explained |
| [Setting Up Teams for Your Organization](Setting-Up-Teams-for-Your-Organization.md) | One-time steps for an IT admin |
| [Troubleshooting and FAQ](Troubleshooting-and-FAQ.md) | Common problems and questions |
| [For Developers](For-Developers.md) | Building, publishing releases, project layout |

---
[Guide home](README.md) · [Installing and Updating](Installing-and-Updating.md) · [Using the Light](Using-the-Light.md) · [Teams Status](Teams-Status.md) · [Call Detection](Call-Detection.md) · [Setup Checklist](Setup-Checklist.md) · [Setting Up Teams for Your Organization](Setting-Up-Teams-for-Your-Organization.md) · [Troubleshooting and FAQ](Troubleshooting-and-FAQ.md) · [For Developers](For-Developers.md)
