# Call Detection

**Whenever any app is using your microphone, the light turns red.** When the app releases the microphone (you hang up or leave the meeting), the light goes back to what it was showing before.

This covers Teams, Zoom, Webex, Google Meet or any call in a browser, and softphones like Cytracom. No sign-ins or API keys are involved.

## How it works

Windows keeps a record of which apps use the microphone (it's what shows the microphone icon in the taskbar). The app checks that record about once a second. When an app has the microphone open, the light turns red and the window says which app, e.g. **On a call (Zoom)**.

- **Mute doesn't count as hanging up.** Most calling apps keep the microphone open while you're muted, so the light stays red.
- **If you pick a color during a call,** your choice wins until that call ends.
- **When connected to Teams,** calls in other apps also set Teams to *Busy, In a call*. See [Teams Status](Teams-Status.md#busy-in-teams-during-other-calls).
- **An app only shows up after it has used the microphone at least once** on that PC.

## Ignoring apps

Some apps use the microphone without being on a call, for example:

- voice typing or dictation,
- screen recorders such as Loom,
- noise-cancelling tools such as Krisp or NVIDIA Broadcast, which keep the microphone open all the time and would keep the light red.

To ignore them:

1. Open the **[Setup Checklist](Setup-Checklist.md)** (tray menu > **Setup checklist...**).
2. Next to *Red while your microphone is in use*, click **Ignore apps...**.
3. Tick the apps that shouldn't turn the light red. The list shows every app Windows has seen use the microphone on this PC, and when each last used it.
4. Click **Save**.

Ignored apps also don't set Teams to Busy.

## Testing it

- **Test the light** in the Setup Checklist shows red for 3 seconds.
- Or open the Windows Sound Recorder, or test your microphone in Teams or Zoom settings. The light should turn red while the microphone is open.

---
[Guide home](README.md) · [Installing and Updating](Installing-and-Updating.md) · [Using the Light](Using-the-Light.md) · [Teams Status](Teams-Status.md) · [Call Detection](Call-Detection.md) · [Setup Checklist](Setup-Checklist.md) · [Setting Up Teams for Your Organization](Setting-Up-Teams-for-Your-Organization.md) · [Troubleshooting and FAQ](Troubleshooting-and-FAQ.md) · [For Developers](For-Developers.md)
