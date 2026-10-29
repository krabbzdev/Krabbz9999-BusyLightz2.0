# Teams Status

The app can show your **Microsoft Teams status** on the light and set Teams to **Busy** while you're on calls in other apps.

## Connect to Teams (once)

1. Right-click the tray icon > **Connect to Microsoft Teams** (or click **Connect** in the [Setup Checklist](Setup-Checklist.md)).
2. Your browser opens the normal Microsoft sign-in page. Sign in with your **work account**, including MFA if asked.
3. When the browser says *"Sign-in finished. You can close this tab."*, close it. A notification confirms *"Connected to Teams as you@company.com"*.

You stay connected from then on, including after restarts and updates. To disconnect, click **Connect to Microsoft Teams** again (it's ticked while connected).

**What the app can do with this sign-in:** read and set *your own* Teams presence, and nothing else. It can't read your mail, chats, calendar or files. The sign-in is stored encrypted, so only your Windows account can use it.

If **Connect** fails because Microsoft says the application wasn't found, your organization hasn't set the app up yet. See [Setting Up Teams for Your Organization](Setting-Up-Teams-for-Your-Organization.md).

## Follow Teams Status

**Follow Teams Status** is the default for new installs; you can also choose it anytime in the window or the tray menu. The app checks your Teams status every 20 seconds and sets the light:

| Teams status | Light |
| --- | --- |
| Available | 🟢 Green |
| Busy, In a call, In a meeting, Presenting | 🔴 Red |
| Do not disturb | 🟣 Purple |
| Away, Be right back | 🟡 Yellow |
| Offline | ⚫ Off |

The window shows the status, e.g. *Teams: Do not disturb*.

**Red or Green override Teams.** Picking Red or Green (or Alt+R / Alt+G) holds that color until you click **Follow Teams Status** again.

## Busy in Teams during other calls

While you're connected, when another app uses your microphone (a Cytracom phone call, Zoom, Webex and so on), the app sets your Teams status to **Busy, "In a call"**, so coworkers see it in Teams too. When the call ends, it clears that status and Teams goes back to normal.

- Teams calls and meetings aren't changed; Teams already shows those itself.
- If the app or PC stops mid-call, Microsoft drops the Busy status on its own within 5 minutes.
- Apps on your [ignore list](Call-Detection.md#ignoring-apps) don't set Busy.

## Status message while your lights are red

Turn on **Set my Teams status message while my lights are red** in [Settings](Using-the-Light.md#settings) and pick a message. Coworkers then see it next to your name in Teams whenever your lights are red: when you pick **Busy** or **On a call**, or when the app [detects a call](Call-Detection.md). It's off by default.

- **Messages:** *On a video call*, *In a meeting*, *On a phone call*, *Heads down, please message me*, *Back in a few minutes*, or **Custom message...** to type your own (up to 280 characters).
- **When the lights stop being red,** the message is taken away. If you had your own status message before, it's put back.
- **If you change the message in Teams** while the lights are red, the app leaves your change alone afterwards.
- **If the app or PC stops,** Microsoft removes the message on its own within an hour. The app renews it every 30 minutes while the lights stay red.
- It needs Teams connected (above). It uses the same sign-in and permission, so there's nothing extra to approve.

## Without connecting to Teams

You can still use the light without connecting. **Follow Teams Status** then hands the light to **Embrava Connect**, if it's installed, which shows your Teams status its own way. Embrava Connect only drives Blynclights, so any Luxafor lights keep their last color in this mode. Once you're connected, Embrava Connect isn't needed. Quit and uninstall it so the two apps don't fight over the light (the [Setup Checklist](Setup-Checklist.md) helps).

---
[Guide home](README.md) · [Installing and Updating](Installing-and-Updating.md) · [Using the Light](Using-the-Light.md) · [Teams Status](Teams-Status.md) · [Call Detection](Call-Detection.md) · [Setup Checklist](Setup-Checklist.md) · [Setting Up Teams for Your Organization](Setting-Up-Teams-for-Your-Organization.md) · [Troubleshooting and FAQ](Troubleshooting-and-FAQ.md) · [For Developers](For-Developers.md)
