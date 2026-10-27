# Using the Light

You control the light from the **app window** or the **tray icon**.

## Supported lights

| Light | Notes |
| --- | --- |
| **Embrava Blynclight** (USB) | Any Blynclight model that plugs in by USB. |
| **Luxafor Flag** (USB) | Controlled directly over USB; Luxafor's own app isn't needed. |

You can plug in **any number of each**. They all show the same color at the same time, including Party Mode, calls and Teams status. Lights plugged in later are found within a couple of seconds and set to match. The window shows what's connected, e.g. *Lights: Blynclight + 2 Luxafor*.

**Use a data cable.** Some USB cables only carry power: the light may glow at plug-in but Windows can't see it, so the app can't control it.

- **Open the window:** double-click the tray icon, or use the Start menu entry.
- **Closing the window** only hides it. The app keeps running in the tray, and the light keeps working.
- **The tray icon** is a colored dot that matches the light, including during Party Mode.

## The window

| Part | What it does |
| --- | --- |
| **Current color** | The swatch and text show what the lights are doing right now, e.g. *Red*, *Teams: Available*, *On a call (Zoom)*, *Party mode*, *Light not found*. Underneath: which lights are connected. |
| **Red (Alt+R) / Busy Mode** | Sets the light red until you choose something else. |
| **Green (Alt+G) / Open Mode** | Sets the light green until you choose something else. |
| **Cycling Colors / PARTY MODE** | Smoothly cycles through every color, about once every 2.4 seconds. |
| **Off** | Turns the light off. |
| **Follow Teams Status** | The light shows your Microsoft Teams status. See [Teams Status](Teams-Status.md). |
| **⚙ Settings** (top right) | Opens [Settings](#settings). |
| **Version line** | Shows the version and build date, plus **Check for updates**, **Project page** and **Help** (this guide) links. |

The color you pick is remembered, even after a restart, and is put back if you unplug and replug the light.

**During a call** the light turns red automatically and goes back to your choice afterwards. See [Call Detection](Call-Detection.md). If you pick a color *during* a call, your choice wins until that call ends.

## The tray menu (right-click the tray icon)

| Item | What it does |
| --- | --- |
| **Open Krabbz9999-BusyLightz2.0** | Opens the window. |
| **Red (Busy Mode) [Override Color]** / **Green (Open Mode) [Override Color]** | Same as the window buttons. "Override" because they take over from Teams status until you choose Follow Teams Status again. |
| **Cycling Colors (PARTY MODE)** | Party Mode. |
| **Off** | Light off. |
| **Follow Teams Status** | Light follows Teams. |
| **Connect to Microsoft Teams** | Sign in so the app can read your Teams status (ticked when connected; click again to disconnect). |
| **Settings...** | Opens [Settings](#settings). |
| **Setup checklist...** | Opens the [Setup Checklist](Setup-Checklist.md). |
| **Version x** | The version you're running. |
| **Check for updates** | Looks for a newer version now. |
| **Project page on GitHub** | Opens this project's page. |
| **Help (user guide)** | Opens this guide. |
| **Exit tray app** | Closes the app. The light stays on its last color (Party Mode turns off). The app starts again at your next sign-in. |

The tick in the menu shows the color *you* chose, even while a call is temporarily showing red.

## Hotkeys

These work from any app, as long as the tray app is running:

| Keys | Action |
| --- | --- |
| **Alt+R** | Red (Busy Mode) |
| **Alt+G** | Green (Open Mode) |

Hotkeys take that key combination away from other programs; for example, Outlook uses Alt+R for Reply. If another program already owns one of them, the app tells you when it starts and that hotkey won't work.

## Notifications

Whenever what the light shows changes, a notification appears in the bottom-right corner, for example:

- *Light set to red: On a call (Zoom).*
- *Light set to purple: Teams: Do not disturb.*
- *No light found. Plug your Blynclight or Luxafor back in.*

Party Mode's color changes don't notify. Turn notifications off in [Settings](#settings).

## Settings

Open with the **⚙ Settings** button in the window or **Settings...** in the tray menu. Changes take effect immediately. Everything is on by default.

| Setting | What it does |
| --- | --- |
| **Start with Windows** | Starts the app when you sign in. |
| **Open the window at sign-in** | When off, the app starts quietly in the tray. |
| **Show a notification when the lights change** | The pop-ups described above. |
| **Hotkeys: Alt+R / Alt+G** | Turn off if they clash with another program. |
| **Install updates automatically** | When off, the app only updates when you click **Check now** / **Check for updates**. |
| **Turn lights red when my mic is in use** | [Call Detection](Call-Detection.md) on or off. **Ignore apps...** picks apps that shouldn't count. |
| **Show me as Busy in Teams during calls in other apps** | See [Teams Status](Teams-Status.md#busy-in-teams-during-other-calls). |
| **Microsoft Teams** | Shows who you're connected as, with **Connect** / **Disconnect**, and **Organization's Teams app...** for [organization setup](Setting-Up-Teams-for-Your-Organization.md). |
| **Setup checklist...** | Opens the [Setup Checklist](Setup-Checklist.md). |

## Colors at a glance

| Light | Meaning |
| --- | --- |
| 🟢 Green | Open / Available |
| 🔴 Red | Busy, or on a call |
| 🟣 Purple | Teams: Do not disturb |
| 🟡 Yellow | Teams: Away / Be right back |
| ⚫ Off | Off, or Teams: Offline |
| 🌈 Cycling | Party Mode |

---
[Guide home](README.md) · [Installing and Updating](Installing-and-Updating.md) · [Using the Light](Using-the-Light.md) · [Teams Status](Teams-Status.md) · [Call Detection](Call-Detection.md) · [Setup Checklist](Setup-Checklist.md) · [Setting Up Teams for Your Organization](Setting-Up-Teams-for-Your-Organization.md) · [Troubleshooting and FAQ](Troubleshooting-and-FAQ.md) · [For Developers](For-Developers.md)
