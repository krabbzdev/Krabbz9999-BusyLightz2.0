# Using the Light

You control the light from the **app window** or the **tray icon**.

## Supported lights

| Light | Notes |
| --- | --- |
| **Embrava Blynclight** (USB) | Any Blynclight model that plugs in by USB. |
| **Luxafor Flag** (USB) | Controlled directly over USB; Luxafor's own app isn't needed. |

You can plug in **any number of each**. They all show the same color at the same time, including Test Lights, calls and Teams status. Lights plugged in later are found within a couple of seconds and set to match. The window shows what's connected, e.g. *Lights: Blynclight + 2 Luxafor*.

**Use a data cable.** Some USB cables only carry power: the light may glow at plug-in but Windows can't see it, so the app can't control it.

- **Open the window:** double-click the tray icon, or use the Start menu entry.
- **Closing the window** only hides it. The app keeps running in the tray, and the light keeps working.
- **The tray icon** is a colored dot that matches the light, including during Test Lights. A small white badge means an override is on.

## The window

Pick how the window looks under **Window style** in [Settings](#settings). Each style works in Light and Dark mode.

| Style | Looks like |
| --- | --- |
| **Orbit** (the default) | The top of the window fills with your status color, with a big ring showing the status, e.g. *Available* over *From Teams Status*. The modes are one pill-shaped switcher below. |
| **Color block** | The top of the window fills with your status color, with the status in large text. The modes are four tiles below. |
| **Fluent list** | A status card, with the modes as a list like Windows Settings, hotkeys on the right. |

Every style has the same parts:

| Part | What it does |
| --- | --- |
| **Status** | What the lights are doing right now, e.g. *Busy*, *Teams: Available*, *On a call (Zoom)*, *Test lights*, *Light not found*, and which lights are connected. |
| **Info line** (under the status) | While an override is on: how long until it goes back to Teams status, e.g. *Back to Teams status in 12:10*. Click **Back to Teams now** to go back early. While following Teams, a short note on what the app does, with a link to this guide. |
| **Busy (Alt+R)** (stop sign) | Sets the light red, then back to Follow Teams Status after the [override time](#override-timer). |
| **Available (Alt+G)** (check mark) | Sets the light green, then back to Follow Teams Status after the override time. |
| **On a call** (phone) | Flashes red and blue quickly, then back to Follow Teams Status after the override time. Handy for a call the app can't detect, such as a desk phone. |
| **Follow Teams Status** (people) | The light shows your Microsoft Teams status. See [Teams Status](Teams-Status.md). |
| **Test lights** (bottom left) | Smoothly cycles through every color, about once every 2.4 seconds. Handy for checking every light is working and matched. |
| **Off** (bottom left) | Turns the light off. |
| **⚙ Settings** (top right) | Opens [Settings](#settings). |
| **Version line** | Shows the version and build date, plus **Check for updates**, **Project page** and **Help** (this guide) links. |
| **☕ Buy me a coffee** (bottom right) | Opens [KrabbzDev's Buy Me a Coffee page](https://buymeacoffee.com/krabbzdev), if you'd like to say thanks. Also in the tray menu. |

The mode you picked is filled in with its color. The mode you pick is remembered, even after a restart, and is put back if you unplug and replug the light.

### Override timer

Busy, Available and On a call *override* your Teams status, so it's easy to forget one is still on. While one is on, the window shows the countdown and the tray icon gets a small white badge. After **15 minutes** the lights go back to **Follow Teams Status** on their own, and a notification says so. The countdown shows in the window and when you hover over the tray icon, and it keeps counting through restarts. Picking any mode again starts the timer over.

Change how long overrides last (5 minutes to 4 hours) in [Settings](#settings). To keep overrides on until you change them, untick **Go back to Follow Teams Status after**.

**During a call** the light flashes red and blue automatically (or shows steady red, if you turn flashing off in Settings) and goes back to your choice afterwards. See [Call Detection](Call-Detection.md). If you pick a color *during* a call, your choice wins until that call ends.

## The tray menu (right-click the tray icon)

| Item | What it does |
| --- | --- |
| **Open KrabbzDev-BusyLightz2.0** | Opens the window. |
| **Busy (red)** / **Available (green)** | Same as the window buttons. They take over from Teams status until the [override timer](#override-timer) runs out. |
| **On a call (flashing red/blue)** | Same as the window's On a call button. |
| **Test lights (cycling colors)** | Same as the window's Test lights button. |
| **Off** | Light off. |
| **Follow Teams Status** | Light follows Teams. |
| **Connect to Microsoft Teams** | Sign in so the app can read your Teams status (ticked when connected; click again to disconnect). |
| **Settings...** | Opens [Settings](#settings). |
| **Setup checklist...** | Opens the [Setup Checklist](Setup-Checklist.md). |
| **Version x** | The version you're running. |
| **Check for updates** | Looks for a newer version now. |
| **Project page on GitHub** | Opens this project's page. |
| **Help (user guide)** | Opens this guide. |
| **☕ Buy me a coffee** | Opens KrabbzDev's Buy Me a Coffee page. |
| **Exit tray app** | Closes the app. The light stays on its last color (Test Lights and flashing turn off). The app starts again at your next sign-in. |

The tick in the menu shows the color *you* chose, even while a call is temporarily showing red.

## Hotkeys

These work from any app, as long as the tray app is running:

| Keys | Action |
| --- | --- |
| **Alt+R** | Busy (red) |
| **Alt+G** | Available (green) |

Hotkeys take that key combination away from other programs; for example, Outlook uses Alt+R for Reply. If another program already owns one of them, the app tells you when it starts and that hotkey won't work.

## Notifications

Whenever what the light shows changes, a notification appears in the bottom-right corner, for example:

- *Light set to red: On a call (Zoom).*
- *Light set to purple: Teams: Do not disturb.*
- *No light found. Plug your Blynclight or Luxafor back in.*

Test Lights's color changes don't notify. Turn notifications off in [Settings](#settings).

## Settings

Open with the **⚙ Settings** button in the window or **Settings...** in the tray menu. Changes take effect immediately. Every checkbox is on by default.

| Setting | What it does |
| --- | --- |
| **Window style** | **Orbit** (the default), **Color block** or **Fluent list**. See [The window](#the-window). |
| **Appearance** | **Light** (the default) or **Dark** colors for the window and Settings, title bar included. Changing either reopens the windows in the new look. |
| **Start with Windows** | Starts the app when you sign in. |
| **Open the window at sign-in** | When off, the app starts quietly in the tray. |
| **Go back to Follow Teams Status after** | The [override timer](#override-timer) for Busy, Available and On a call. On by default. Pick how long overrides last on the right: 5, 10, 15 (default) or 30 minutes, or 1, 2 or 4 hours. |
| **Show a notification when the lights change** | The pop-ups described above. |
| **Hotkeys: Alt+R / Alt+G** | Turn off if they clash with another program. |
| **Install updates automatically** | When off, the app only updates when you click **Check now** / **Check for updates**. |
| **Turn lights red when my mic is in use** | [Call Detection](Call-Detection.md) on or off. **Ignore apps...** picks apps that shouldn't count. |
| **Flash red and blue instead of steady red** | On by default: lights flash red and blue quickly during calls. Untick for steady red. |
| **Show me as Busy in Teams during calls in other apps** | See [Teams Status](Teams-Status.md#busy-in-teams-during-other-calls). |
| **Microsoft Teams** | Shows who you're connected as, with **Connect** / **Disconnect**, and **Organization's Teams app...** for [organization setup](Setting-Up-Teams-for-Your-Organization.md). |
| **Match my Teams status when I pick Busy, Available or On a call** | See [Teams Status](Teams-Status.md#teams-matches-what-you-pick). |
| **Set my Teams status message while my lights are red** | On by default, showing *On a video call*. Pick another message, or **Custom message...** to type your own. See [Teams Status](Teams-Status.md#status-message-while-your-lights-are-red). |
| **Setup checklist...** | Opens the [Setup Checklist](Setup-Checklist.md). |

## Colors at a glance

| Light | Meaning |
| --- | --- |
| 🟢 Green | Open / Available |
| 🔴 Red | Busy, or on a call |
| 🟣 Purple | Teams: Do not disturb |
| 🟡 Yellow | Teams: Away / Be right back |
| ⚫ Off | Off, or Teams: Offline |
| 🔴🔵 Flashing red/blue | On a call (detected, or the On A Call button) |
| 🌈 Cycling | Test Lights |

---
[Guide home](README.md) · [Installing and Updating](Installing-and-Updating.md) · [Using the Light](Using-the-Light.md) · [Teams Status](Teams-Status.md) · [Call Detection](Call-Detection.md) · [Setup Checklist](Setup-Checklist.md) · [Setting Up Teams for Your Organization](Setting-Up-Teams-for-Your-Organization.md) · [Troubleshooting and FAQ](Troubleshooting-and-FAQ.md) · [For Developers](For-Developers.md)
