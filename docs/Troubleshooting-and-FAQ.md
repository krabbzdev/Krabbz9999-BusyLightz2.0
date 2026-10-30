# Troubleshooting and FAQ

## The light doesn't change at all

- Check the window: **Light not found** means no light is detected, and the *Lights:* line shows which ones are. Unplug the light and plug it back in, or try another USB port. The app finds it again within a couple of seconds.
- **Try a different cable.** Charge-only cables power the light (it may glow when plugged in) but Windows can't see it. Use the cable that came with the light, or one that can transfer files to a phone.
- Is the app running? Look for its colored dot in the system tray. If it's not there, start it from the Start menu.
- Click **Test the light** in the [Setup Checklist](Setup-Checklist.md).

## One of several lights stays dark or doesn't match

The app sends every color to every light it can see, so a light that stays dark has usually dropped off USB. Check the *Lights:* line in the window. If that light isn't counted, reseat its plug or swap the cable. It's matched automatically as soon as it reconnects.

## The light keeps changing back on its own

Embrava Connect is probably still running and fighting the app. Quit it (Setup Checklist > **Quit it**) and uninstall it from **Settings > Apps**. If you're not connected to Teams yet, see [Teams Status](Teams-Status.md#without-connecting-to-teams).

## The light stays red when I'm not on a call

An app is holding your microphone open. The window says which one: *On a call (AppName)*.

- If it's a call app you have open, close the call or the app.
- If it's a tool that always keeps the mic open (noise cancelling, dictation, a recorder), add it to the [ignore list](Call-Detection.md#ignoring-apps).

## The light didn't go red on a call

- The calling app must use the microphone. Calls through a desk phone aren't detected.
- An app only appears after it has used the microphone once. Make one call, then check the Setup Checklist.
- Check the app isn't on the [ignore list](Call-Detection.md#ignoring-apps).

## Follow Teams Status doesn't follow my Teams status

- Are you connected? Tray menu > **Connect to Microsoft Teams** should be ticked.
- Changes can take up to 20 seconds to show.
- Did you pick Red or Green since? Those override Teams. Click **Follow Teams Status** again.

## "Connect to Microsoft Teams" fails

- **Microsoft says the application wasn't found** (in the browser): your organization hasn't set the app up. Ask IT; see [Setting Up Teams for Your Organization](Setting-Up-Teams-for-Your-Organization.md).
- **"Sign-in timed out":** the sign-in wasn't finished in the browser within 5 minutes. Try again.
- **You need admin approval:** IT needs to grant admin consent on the app registration.

## Alt+R or Alt+G doesn't work

Another program already uses that key combination; the app tells you when it starts. Use the window or tray menu instead.

## The app didn't update

- Click **Check for updates** in the window or tray menu. It says *"You're on the latest version"* if there's nothing newer.
- Updates only arrive once they're published on the [Releases](https://github.com/krabbzdev/KrabbzDev-BusyLightz2.0/releases) page.
- Update logs are in `%LOCALAPPDATA%\Krabbz9999 Blynclight\updates`.

## If the app crashes

The app writes any unexpected error to its log, with the version and Windows version, and keeps running when it can. To send the log:

1. Start the app again (Start menu), then click **Email crash log...** in Settings or the tray menu.
2. A new email to clove@showtechsolutions.com opens with the log attached. Add what you were doing when it crashed, and send it.

With classic Outlook and most desktop mail apps, the log is attached for you. With the new Outlook or webmail, the folder with the log file opens next to the email: drag the file into the email.

The log is `%LOCALAPPDATA%\Krabbz9999 Blynclight\app.log`. It holds error messages, such as Teams or light errors, and sometimes the text of a Teams status message you set yourself. It never holds passwords or sign-in tokens. You can open the attached file and read it before sending.

## Windows says the installer is from an unknown publisher

The installer isn't code-signed yet. Click **More info > Run anyway**.

## Too many notifications

Turn them off in **Settings** (⚙ in the window, or tray menu > **Settings...**).

## Is my data safe?

- The app only reads and sets **your own Teams presence**. It can't see mail, chats, files or calendars.
- Your Teams sign-in is stored encrypted, usable only by your Windows account. **Connect to Microsoft Teams** (when ticked) signs out and deletes it.
- Call detection only reads Windows' record of *which app* uses the microphone, never any audio.
- The only internet connections are to Microsoft (Teams status) and GitHub (updates).

## Can I pin it to the Start menu or taskbar?

Yes, but you have to do it yourself (Windows doesn't let installers do it): right-click the Start menu entry > **Pin to Start** or **Pin to taskbar**.

---
[Guide home](README.md) · [Installing and Updating](Installing-and-Updating.md) · [Using the Light](Using-the-Light.md) · [Teams Status](Teams-Status.md) · [Call Detection](Call-Detection.md) · [Setup Checklist](Setup-Checklist.md) · [Setting Up Teams for Your Organization](Setting-Up-Teams-for-Your-Organization.md) · [Troubleshooting and FAQ](Troubleshooting-and-FAQ.md) · [For Developers](For-Developers.md)
